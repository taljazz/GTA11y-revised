using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;

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

        #region Fields

        private readonly AudioManager _audio;
        private int _originalModelHash;
        private int _swappedModelHash;
        private string _swappedName;

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
                    // so nothing keeps claiming the suit is on
                    Logger.Info($"MODEL|lost|expected={_swappedModelHash}|actual={player.Model.Hash}");
                    _swappedModelHash = 0;
                    _originalModelHash = 0;
                    _swappedName = null;
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
