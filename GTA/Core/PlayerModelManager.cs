using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.Input;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Swaps the player onto an online-only ped model and back again.
    ///
    /// Changing the player model destroys the current ped and builds a new one,
    /// which means everything carried on the old ped is lost unless it is copied
    /// across by hand: position, heading, health, armour and the entire weapon
    /// inventory. All of that is captured before the swap and restored after.
    ///
    /// The state is deliberately held here rather than in a menu, because the
    /// original model has to be restored on script shutdown too. A reload while
    /// transformed would otherwise leave the player permanently stuck as an NPC
    /// model with no menu left to change it back.
    /// </summary>
    public class PlayerModelManager
    {
        #region Types

        /// <summary>One carried weapon, so the inventory survives the swap.</summary>
        private struct CarriedWeapon
        {
            public WeaponHash Hash;
            public int Ammo;
        }

        #endregion

        #region Constants

        // The ballistic armour treatment, taken from Rockstar's own scripts.
        // The Paleto Score (rural_bank_heist_stage_chicken_factory.sch) and the
        // online juggernaut handling (FM_Mission_Controller_Players_2020.sch)
        // both dress the suit the same way: these movement clip sets, the
        // "Ballistic" weapon animation override, and the juggernaut footstep
        // sweeteners - the heavy stomp, which for a blind player is the most
        // informative part of the whole outfit.
        private const string SUIT_MOVE_CLIPSET = "ANIM_GROUP_MOVE_BALLISTIC";
        private const string SUIT_STRAFE_CLIPSET = "MOVE_STRAFE_BALLISTIC";
        private const string SUIT_WEAPON_ANIM = "Ballistic";
        private const string SUIT_FOOTSTEP_SOUNDS = "DLC_IE_JN_Footstep_Sounds";

        // NOT USED, and deliberately so - kept named because the reason matters.
        // Rockstar puts the mix group "DLC_IE_JN_JN_Player_Group" on OTHER
        // players' suits and explicitly never on your own: PROCESS_ALL_JUGGERNAUTS
        // guards it with `IF iPart != iPartToUse`. A mix group is a mixing patch,
        // and one written for a juggernaut heard from across the street is the
        // wrong shape for the one you are standing inside - it can duck the very
        // footsteps it is meant to colour. Applying it to the local player was
        // a guess that went past what the reference actually does, and the suit
        // came back silent. Following Rockstar exactly instead.

        // Ped reset flags Rockstar sets every frame on a suited player, with the
        // integer values from their commands_ped.sch. Reset flags clear each
        // frame by design, so these are re-asserted from Update.
        private const int PRF_DISABLE_PLAYER_JUMPING = 46;
        private const int PRF_DISABLE_ACTION_MODE = 200;
        private const int PRF_DISABLE_MELEE_HIT_REACTIONS = 335;
        private const int PRF_DISABLE_MELEE_WEAPON_SELECTION = 417;
        private const int PRF_DISABLE_PLAYER_COMBAT_ROLL = 446;

        /// <summary>The ped models that count as a ballistic suit.</summary>
        private static readonly HashSet<int> BallisticModels = new HashSet<int>
        {
            unchecked((int)PedHash.Juggernaut01M),
            unchecked((int)PedHash.Juggernaut02UMY),
            unchecked((int)PedHash.Juggernaut03UMM)
        };

        #endregion

        #region Fields

        private readonly AudioManager _audio;
        private int _originalModelHash;
        private int _swappedModelHash;
        private string _swappedName;

        // Ballistic suit state. _suitOn drives the per-frame control blocks;
        // the health and minigun bookkeeping exists so taking the suit off
        // gives back exactly what the player had, no more and no less.
        private bool _suitOn;
        private int _originalMaxHealth;
        private bool _grantedMinigun;

        // The walk animations stream in like a model does. Waiting for them in
        // a loop meant waiting inside a key press, and if they were slow the
        // rest of the suit was skipped entirely. They are applied from Update
        // instead, once the game says they have arrived.
        private bool _clipsetsPending;
        private long _lastAudioAssertTick;
        private long _lastFootstepTick;

        #endregion

        #region Construction

        public PlayerModelManager(AudioManager audio)
        {
            _audio = audio;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Whether a swapped model is on RIGHT NOW, checked against the live ped
        /// rather than a flag we set earlier. Dying, switching character or any
        /// other script can put the original model back without telling us, and a
        /// stale flag would then confidently report the wrong thing - which is
        /// exactly the question the player cannot answer by looking.
        /// </summary>
        public bool IsSwapped
        {
            get
            {
                if (_swappedModelHash == 0)
                    return false;

                try
                {
                    Ped player = Game.Player?.Character;
                    if (player == null || !player.Exists())
                        return false;

                    if (player.Model.Hash == _swappedModelHash)
                        return true;

                    // The model went away without us doing it - forget the state
                    // so nothing keeps claiming the suit is on. The player-level
                    // flags have to come back too: sprint and cover are set on
                    // the Player, not the ped, so they survive whatever took the
                    // model and would leave the player unable to run forever.
                    Logger.Info($"MODEL|lost|expected={_swappedModelHash}|actual={player.Model.Hash}");
                    _swappedModelHash = 0;
                    _originalModelHash = 0;
                    _swappedName = null;
                    if (_suitOn)
                    {
                        _suitOn = false;
                        _grantedMinigun = false;
                        RestorePlayerMovementFlags();
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// What to say when the player asks what they are wearing, or null when
        /// they are in their own character.
        /// </summary>
        public string GetStatusText()
        {
            return IsSwapped ? $"Wearing the {_swappedName ?? "online outfit"}" : null;
        }

        /// <summary>
        /// Whether this game build actually has the model. Online models come
        /// from DLC, so this is worth asking before promising anything.
        /// </summary>
        public static bool IsModelAvailable(PedHash pedHash)
        {
            try
            {
                var model = new Model(pedHash);
                return model.IsValid && model.IsInCdImage;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Swap to the given model, or back to the original if already swapped.
        /// Returns the message that was spoken, for logging.
        /// </summary>
        public string Toggle(PedHash pedHash, string friendlyName)
        {
            if (IsSwapped)
                return Restore(true);

            return SwapTo(pedHash, friendlyName);
        }

        /// <summary>
        /// Put the original model back. Safe to call when nothing is swapped.
        /// Called on script shutdown as well as from the menu.
        /// </summary>
        public string Restore(bool announce)
        {
            if (!IsSwapped || _originalModelHash == 0)
                return null;

            string message;
            try
            {
                // Undress before undoing the swap. ApplyModel captures the
                // current ped's health and weapons to carry across, so the
                // suit's 1000 max health and granted minigun have to be gone
                // BEFORE that capture or they walk out of the suit with the
                // player.
                RemoveSuitEffects();

                var original = new Model(_originalModelHash);
                message = ApplyModel(original)
                    ? "Back to your own character."
                    : "Failed to change back. Try again, or reload a save.";
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.Restore");
                message = "Failed to change back. Try again, or reload a save.";
            }

            _swappedModelHash = 0;
            _swappedName = null;
            _originalModelHash = 0;

            if (announce && _audio != null && !string.IsNullOrEmpty(message))
                _audio.Speak(message, true);

            Logger.Info($"MODEL|restore|{message}");
            return message;
        }

        #endregion

        #region Swapping

        private string SwapTo(PedHash pedHash, string friendlyName)
        {
            string message;

            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                    return Announce("Player not available.");

                if (!IsModelAvailable(pedHash))
                {
                    return Announce($"{friendlyName} is not in this copy of the game. " +
                                    "It needs the online update that added it.");
                }

                _originalModelHash = player.Model.Hash;

                if (!ApplyModel(new Model(pedHash)))
                {
                    _originalModelHash = 0;
                    return Announce($"Could not put on the {friendlyName}.");
                }

                message = $"Wearing the {friendlyName}.";

                _swappedModelHash = new Model(pedHash).Hash;
                _swappedName = friendlyName;
                Logger.Info($"MODEL|swap|to={pedHash}|from={_originalModelHash}");

                // A ballistic suit is more than a costume - give it Rockstar's
                // own treatment: the health, the minigun, the heavy walk
                if (BallisticModels.Contains(_swappedModelHash))
                {
                    ApplySuitEffects();

                    return Announce(message +
                        " You are five times tougher and carrying a minigun, and you will " +
                        "hear your own heavy footsteps. The armour is too stiff to sprint, " +
                        "jump, roll, take cover or enter vehicles. It comes off if you die. " +
                        "Change back before saving, and your special ability is gone while " +
                        "you wear it.");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.SwapTo");
                _originalModelHash = 0;
                return Announce($"Failed to put on the {friendlyName}.");
            }

            return Announce(message + " It comes off if you die. Change back before saving, " +
                            "and your special ability is gone while you wear it.");
        }

        /// <summary>
        /// Load the model, carry everything across, and swap. Returns whether it
        /// worked; the caller words its own announcement, since "wearing the X"
        /// and "back to your own character" are not the same sentence.
        /// </summary>
        private bool ApplyModel(Model model)
        {
            if (!model.IsValid || !model.IsInCdImage)
                return false;

            Ped before = Game.Player.Character;

            // Capture everything the new ped will not inherit
            Vector3 position = before.Position;
            float heading = before.Heading;
            int health = before.Health;
            int maxHealth = before.MaxHealth;
            int armor = before.Armor;
            bool inVehicle = before.IsInVehicle();
            bool wasDead = before.IsDead;
            List<CarriedWeapon> weapons = CaptureWeapons(before);

            // The suit's minigun goes with the suit. Without this the death
            // path carries it across and dying in the suit becomes a free
            // minigun forever.
            if (_grantedMinigun)
            {
                weapons.RemoveAll(w => w.Hash == WeaponHash.Minigun);
                _grantedMinigun = false;
            }

            // Which weapon was actually in hand. Without this the game picks for
            // you after the swap - you look away for a second and come back
            // holding something else, which you cannot see and would only find
            // out by pulling the trigger.
            WeaponHash equipped = WeaponHash.Unarmed;
            try { equipped = before.Weapons.Current?.Hash ?? WeaponHash.Unarmed; } catch { }

            if (!model.Request(Constants.MODEL_REQUEST_TIMEOUT_MS))
            {
                model.MarkAsNoLongerNeeded();
                return false;
            }

            if (!Game.Player.ChangeModel(model))
            {
                model.MarkAsNoLongerNeeded();
                return false;
            }

            Ped after = Game.Player.Character;

            try
            {
                // Dying is a special case. The swap is being undone mid death
                // sequence purely so the respawn does not hang, so the ped's
                // position and health belong to the game now - forcing a dead
                // ped's zero health and last position onto the replacement fights
                // the respawn we are trying to let happen. Weapons still come
                // across, because the swap empties the inventory either way.
                if (wasDead)
                {
                    // Sprint and cover live on the Player, not the ped, so the
                    // respawn will not clear them - do it here or the player
                    // comes back permanently unable to run
                    if (_suitOn)
                    {
                        _suitOn = false;
                        RestorePlayerMovementFlags();
                    }

                    RestoreWeapons(after, weapons);
                    model.MarkAsNoLongerNeeded();
                    Logger.Info("MODEL|restored during death - state left to the respawn");
                    return true;
                }

                // On foot the new ped spawns where the old one stood, but nudging
                // it back keeps things exact; in a vehicle the game seats it and
                // moving it would eject the player
                if (!inVehicle)
                {
                    after.Position = position;
                    after.Heading = heading;
                }

                // What the model brings on its own, before we overwrite it. If an
                // online model turns out to carry more health than the player,
                // that is the only thing about it that is not purely cosmetic -
                // worth knowing rather than silently discarding.
                Logger.Info($"MODEL|native|maxHealth={after.MaxHealth}|playerMaxHealth={maxHealth}");

                after.MaxHealth = maxHealth;
                after.Health = health;
                after.Armor = armor;

                RestoreWeapons(after, weapons);

                // Put the same weapon back in hand
                if (equipped != WeaponHash.Unarmed)
                {
                    try { after.Weapons.Select(equipped, true); }
                    catch (Exception ex) { Logger.Exception(ex, "PlayerModelManager re-equip"); }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.ApplyModel restore state");
            }

            model.MarkAsNoLongerNeeded();
            return true;
        }

        #endregion

        #region Suit Effects

        /// <summary>
        /// Dress the suit the way Rockstar does, researched from their own
        /// scripts rather than guessed:
        ///
        /// The Paleto Score (story mode) sets 1000 max health with scale, gives
        /// the big gun with 1500 rounds, and turns off sprinting and cover. The
        /// online juggernaut handling additionally sets the ballistic movement
        /// and strafe clip sets, the "Ballistic" weapon animation override, the
        /// juggernaut footstep sounds, and blocks jumping, combat rolls, melee
        /// and vehicle entry every frame. Neither adds damage modifiers or
        /// headshot immunity - the suit's toughness IS the health figure.
        /// </summary>
        private void ApplySuitEffects()
        {
            try
            {
                Ped ped = Game.Player?.Character;
                if (ped == null || !ped.Exists())
                    return;

                // Ask for the walk animations, but do not wait here - this runs
                // inside a key press, and Update applies them the moment the
                // game reports them loaded
                Function.Call(Hash.REQUEST_ANIM_SET, SUIT_MOVE_CLIPSET);
                Function.Call(Hash.REQUEST_ANIM_SET, SUIT_STRAFE_CLIPSET);
                _clipsetsPending = true;

                // How weapons are held
                Function.Call(Hash.SET_WEAPON_ANIMATION_OVERRIDE, ped, StringHash.AtStringHash(SUIT_WEAPON_ANIM));

                // The audible part of the suit, applied through the one helper
                // that Update also calls, so it can be put back if anything
                // resets the ped's audio
                AssertSuitAudio(ped);

                // Sneaking in powered armour is not a thing
                Function.Call(Hash.SET_PED_STEALTH_MOVEMENT, ped, false, 0);

                // 1000 max health, preserving the fraction the player was on -
                // Rockstar's SET_PED_MAX_HEALTH_WITH_SCALE semantics
                _originalMaxHealth = ped.MaxHealth;
                float fraction = _originalMaxHealth > 0
                    ? (float)ped.Health / _originalMaxHealth
                    : 1f;
                ped.MaxHealth = Constants.SUIT_MAX_HEALTH;
                ped.Health = Math.Max(1, (int)(Constants.SUIT_MAX_HEALTH * fraction));

                // The armour is too stiff to sprint or slot into cover
                Function.Call(Hash.SET_PLAYER_SPRINT, Game.Player, false);
                Function.Call(Hash.SET_PLAYER_CAN_USE_COVER, Game.Player, false);

                // The suit's weapon. If the player already owns a minigun it is
                // theirs and stays theirs; otherwise this one belongs to the
                // suit and leaves with it.
                bool ownedMinigun = ped.Weapons.HasWeapon(WeaponHash.Minigun);
                if (!ownedMinigun)
                {
                    ped.Weapons.Give(WeaponHash.Minigun, Constants.SUIT_MINIGUN_AMMO, false, true);
                    _grantedMinigun = true;
                }
                try { ped.Weapons.Select(WeaponHash.Minigun, true); } catch { }

                _suitOn = true;
                _lastAudioAssertTick = Game.GameTime;

                // The sound set hash is logged so a silent suit can be told
                // apart from a suit whose sound set never resolved
                Logger.Info($"MODEL|suit-on|health={ped.Health}/{ped.MaxHealth}" +
                            $"|minigun={(ownedMinigun ? "already owned" : "granted")}" +
                            $"|footstepSet={SUIT_FOOTSTEP_SOUNDS}" +
                            $"|footstepHash={StringHash.AtStringHash(SUIT_FOOTSTEP_SOUNDS)}" +
                            $"|clipsets=requested|mixGroup=none by design");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.ApplySuitEffects");
            }
        }

        /// <summary>
        /// The mod's own heavy tread, paced by how fast the player is actually
        /// moving on foot.
        ///
        /// Rockstar's footstep sound set is applied to the ped exactly as their
        /// scripts do it and produces nothing audible here - the log confirms
        /// the call is made with a resolved sound set on a ped with action mode
        /// off, and the suit is still silent. Their sweeteners appear to need
        /// the online context that owns that audio. So the mod supplies the
        /// tread itself.
        ///
        /// This is not decoration. A sighted player knows they are wearing
        /// powered armour because they can see it; without the sound there is
        /// nothing at all to tell you the suit is on, which is exactly the gap
        /// this mod exists to close.
        /// </summary>
        private void UpdateFootsteps(Ped ped, long now)
        {
            try
            {
                // No tread while seated or in the air - only actual walking
                if (ped.IsInVehicle() || ped.IsInAir || ped.IsRagdoll)
                    return;

                float speed = ped.Speed;
                if (speed < Constants.SUIT_FOOTSTEP_MIN_SPEED)
                    return;

                long interval = speed >= Constants.SUIT_FOOTSTEP_RUN_SPEED
                    ? Constants.SUIT_FOOTSTEP_RUN_INTERVAL
                    : Constants.SUIT_FOOTSTEP_WALK_INTERVAL;

                if (now - _lastFootstepTick < interval)
                    return;

                _lastFootstepTick = now;
                _audio?.PlaySuitFootstep();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.UpdateFootsteps");
            }
        }

        /// <summary>
        /// The suit's audio, exactly as Rockstar sets it and in their order.
        ///
        /// Action mode goes off FIRST. Rockstar calls SET_PED_USING_ACTION_MODE
        /// immediately before the footstep sweetener in PROCESS_ALL_JUGGERNAUTS,
        /// and it is not decoration: action mode is a whole locomotion state
        /// with its own movement and footstep behaviour, so leaving it on
        /// fights the sweetener that is supposed to replace those steps. It was
        /// missing from the first attempt.
        ///
        /// Then the sweetener itself - the heavy stomp, and the thing a blind
        /// player actually experiences the suit through.
        ///
        /// Deliberately NOT here: the audio mix group. See the note on the
        /// constants above.
        ///
        /// Called again periodically from Update, because these are settings on
        /// a ped rather than events, and anything that rebuilds the ped's audio
        /// state would otherwise silently take the suit's voice away for good.
        /// </summary>
        private static void AssertSuitAudio(Ped ped)
        {
            if (ped == null || !ped.Exists())
                return;

            try
            {
                Function.Call(Hash.SET_PED_USING_ACTION_MODE, ped, false, -1, 0);
                Function.Call(Hash.USE_FOOTSTEP_SCRIPT_SWEETENERS, ped, true,
                              StringHash.AtStringHash(SUIT_FOOTSTEP_SOUNDS));

                // A faceless armoured suit neither grunts nor chatters - taken
                // from Rockstar's survival juggernauts
                Function.Call(Hash.DISABLE_PED_PAIN_AUDIO, ped, true);
                Function.Call(Hash.STOP_PED_SPEAKING, ped, true);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.AssertSuitAudio");
            }
        }

        /// <summary>
        /// Take the suit's effects off the CURRENT ped, before the model swap
        /// back. Everything ApplySuitEffects did is undone here, in the same
        /// order Rockstar's cleanup does it.
        /// </summary>
        private void RemoveSuitEffects()
        {
            if (!_suitOn)
                return;

            _suitOn = false;
            _clipsetsPending = false;
            _audio?.StopSuitFootstep();

            try
            {
                Ped ped = Game.Player?.Character;
                if (ped != null && ped.Exists())
                {
                    Function.Call(Hash.RESET_PED_MOVEMENT_CLIPSET, ped, 0.25f);
                    Function.Call(Hash.RESET_PED_STRAFE_CLIPSET, ped);
                    Function.Call(Hash.SET_WEAPON_ANIMATION_OVERRIDE, ped, StringHash.AtStringHash("Default"));
                    Function.Call(Hash.USE_FOOTSTEP_SCRIPT_SWEETENERS, ped, false,
                                  StringHash.AtStringHash(SUIT_FOOTSTEP_SOUNDS));
                    Function.Call(Hash.DISABLE_PED_PAIN_AUDIO, ped, false);
                    Function.Call(Hash.STOP_PED_SPEAKING, ped, false);

                    // Health back to the original ceiling, keeping the fraction
                    if (_originalMaxHealth > 0)
                    {
                        float fraction = ped.MaxHealth > 0
                            ? (float)ped.Health / ped.MaxHealth
                            : 1f;
                        ped.MaxHealth = _originalMaxHealth;
                        ped.Health = Math.Max(1, (int)(_originalMaxHealth * fraction));
                    }

                    // The suit's minigun goes with the suit
                    if (_grantedMinigun)
                    {
                        try { ped.Weapons.Remove(WeaponHash.Minigun); } catch { }
                    }
                }

                _grantedMinigun = false;
                RestorePlayerMovementFlags();

                // Give the streamed animations back
                Function.Call(Hash.REMOVE_ANIM_SET, SUIT_MOVE_CLIPSET);
                Function.Call(Hash.REMOVE_ANIM_SET, SUIT_STRAFE_CLIPSET);

                Logger.Info("MODEL|suit-off");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.RemoveSuitEffects");
            }
        }

        /// <summary>
        /// Re-enable sprint and cover. These sit on the Player rather than the
        /// ped, so they survive death, respawn and model changes - every path
        /// out of the suit must run this or the player is left slow forever.
        /// </summary>
        private static void RestorePlayerMovementFlags()
        {
            try
            {
                Function.Call(Hash.SET_PLAYER_SPRINT, Game.Player, true);
                Function.Call(Hash.SET_PLAYER_CAN_USE_COVER, Game.Player, true);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.RestorePlayerMovementFlags");
            }
        }

        /// <summary>
        /// Called every frame from the tick. Rockstar blocks these actions
        /// per frame while the suit is on: reset flags clear themselves each
        /// frame by design, and control blocks only last the frame they are
        /// asked for. Cheap - two control calls and five flag sets - and does
        /// nothing at all when the suit is off.
        /// </summary>
        public void Update()
        {
            if (!_suitOn)
                return;

            try
            {
                Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.Enter);
                Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.Jump);

                Ped ped = Game.Player?.Character;
                if (ped == null || !ped.Exists())
                    return;

                // The walk, applied the moment the animations finish streaming.
                // Doing this here rather than waiting inside the key press means
                // a slow load delays the walk instead of cancelling the suit.
                if (_clipsetsPending &&
                    Function.Call<bool>(Hash.HAS_ANIM_SET_LOADED, SUIT_MOVE_CLIPSET))
                {
                    Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET, ped, SUIT_MOVE_CLIPSET, 0.25f);
                    Function.Call(Hash.SET_PED_STRAFE_CLIPSET, ped, SUIT_STRAFE_CLIPSET);
                    _clipsetsPending = false;
                    Logger.Info("MODEL|suit-clipsets|applied");
                }

                // Put the audio settings back periodically. They are ped state,
                // not events, and a suit that goes quiet halfway through is
                // worse than one that never spoke.
                long now = Game.GameTime;
                if (now - _lastAudioAssertTick >= Constants.SUIT_AUDIO_REASSERT_INTERVAL)
                {
                    _lastAudioAssertTick = now;
                    AssertSuitAudio(ped);
                }

                UpdateFootsteps(ped, now);

                Function.Call(Hash.SET_PED_RESET_FLAG, ped, PRF_DISABLE_PLAYER_JUMPING, true);
                Function.Call(Hash.SET_PED_RESET_FLAG, ped, PRF_DISABLE_ACTION_MODE, true);
                Function.Call(Hash.SET_PED_RESET_FLAG, ped, PRF_DISABLE_MELEE_HIT_REACTIONS, true);
                Function.Call(Hash.SET_PED_RESET_FLAG, ped, PRF_DISABLE_MELEE_WEAPON_SELECTION, true);
                Function.Call(Hash.SET_PED_RESET_FLAG, ped, PRF_DISABLE_PLAYER_COMBAT_ROLL, true);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.Update");
            }
        }

        #endregion

        #region Inventory

        /// <summary>
        /// Note every weapon carried, with its ammo. Changing model empties the
        /// inventory, so without this the player is disarmed by the swap.
        /// </summary>
        private static List<CarriedWeapon> CaptureWeapons(Ped player)
        {
            var carried = new List<CarriedWeapon>();

            try
            {
                foreach (WeaponHash hash in Weapon.GetAllWeaponHashesForHumanPeds())
                {
                    if (hash == WeaponHash.Unarmed)
                        continue;
                    if (!player.Weapons.HasWeapon(hash))
                        continue;

                    int ammo = 0;
                    try { ammo = player.Weapons[hash]?.Ammo ?? 0; } catch { }

                    carried.Add(new CarriedWeapon { Hash = hash, Ammo = ammo });
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayerModelManager.CaptureWeapons");
            }

            return carried;
        }

        private static void RestoreWeapons(Ped player, List<CarriedWeapon> weapons)
        {
            if (weapons == null || weapons.Count == 0)
                return;

            foreach (CarriedWeapon weapon in weapons)
            {
                try
                {
                    player.Weapons.Give(weapon.Hash, Math.Max(weapon.Ammo, 1), false, true);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, $"PlayerModelManager.RestoreWeapons({weapon.Hash})");
                }
            }
        }

        #endregion

        #region Helpers

        private string Announce(string message)
        {
            if (_audio != null && !string.IsNullOrEmpty(message))
                _audio.Speak(message, true);
            return message;
        }

        #endregion
    }
}
