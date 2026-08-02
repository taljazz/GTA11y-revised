using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for fun functions like chaos modes.
    /// Uses HashManager for shared hash lookups (loaded once, shared across classes).
    /// </summary>
    public class FunctionsMenu : MenuBase
    {
        #region Constants

        // Cached mission blip sprite IDs to avoid allocation on each call
        private static readonly int[] MissionBlipSprites = new int[]
        {
            1,    // Standard circle (often mission objectives)
            2,    // Destination marker
            3,    // Destination
            38,   // Destination flag
            40,   // Helipad
            90,   // Yellow destination
            143,  // Objective
            225,  // Pickup
            227,  // Dropoff
            280,  // Yellow mission
            304,  // Taxi destination
            309,  // Yellow marker
            380,  // Mission area
            417,  // Yellow circle with arrow
            478,  // Mission destination
            480,  // Mission pickup
        };

        // Item indices for ExecuteSelection dispatch
        private const int ITEM_MARK_MISSION_WAYPOINT = 0;
        private const int ITEM_TOGGLE_TURRET_CREW = 1;
        private const int ITEM_EXPLODE_VEHICLES = 2;
        private const int ITEM_PEDS_ATTACK = 3;
        private const int ITEM_KILL_PEDS = 4;
        private const int ITEM_RAISE_WANTED = 5;
        private const int ITEM_CLEAR_WANTED = 6;
        private const int ITEM_JUGGERNAUT_SUIT = 7;

        #endregion

        #region Fields

        private readonly List<string> _functions;
        private readonly SettingsManager _settings;
        private readonly Random _random;
        private readonly TurretCrewManager _turretCrewManager;
        private readonly PlayerModelManager _playerModel;

        #endregion

        #region Construction

        public FunctionsMenu(SettingsManager settings, TurretCrewManager turretCrewManager,
            PlayerModelManager playerModel, AudioManager audio)
            : base(audio)
        {
            _settings = settings;
            _turretCrewManager = turretCrewManager;
            _playerModel = playerModel;
            _random = new Random();

            _functions = new List<string>
            {
                "Mark Waypoint to Mission Objective",
                "Toggle Turret Crew",
                "Blow up all nearby vehicles",
                "Make all nearby pedestrians attack each other",
                "Instantly kill all nearby pedestrians",
                "Raise Wanted Level",
                "Clear Wanted Level",
                "Toggle Juggernaut suit (online armoured suit)"
            };
        }

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount => _functions.Count;

        protected override string GetItemText(int index)
        {
            // For turret crew, show current status
            if (index == ITEM_TOGGLE_TURRET_CREW && _turretCrewManager != null)
            {
                return _turretCrewManager.GetStatusText();
            }

            if (index == ITEM_JUGGERNAUT_SUIT && _playerModel != null)
            {
                return _playerModel.IsSwapped
                    ? "Take off the Juggernaut suit"
                    : "Put on the Juggernaut suit (online armoured suit)";
            }

            return _functions[index];
        }

        protected override void OnItemActivated(int index)
        {
            switch (index)
            {
                case ITEM_MARK_MISSION_WAYPOINT:
                    MarkWaypointToMissionObjective();
                    break;
                case ITEM_TOGGLE_TURRET_CREW:
                    ToggleTurretCrew();
                    break;
                case ITEM_EXPLODE_VEHICLES:
                    ExplodeNearbyVehicles();
                    break;
                case ITEM_PEDS_ATTACK:
                    MakePedsAttackEachOther();
                    break;
                case ITEM_KILL_PEDS:
                    KillAllNearbyPeds();
                    break;
                case ITEM_RAISE_WANTED:
                    RaiseWantedLevel();
                    break;
                case ITEM_CLEAR_WANTED:
                    ClearWantedLevel();
                    break;
                case ITEM_JUGGERNAUT_SUIT:
                    ToggleJuggernautSuit();
                    break;
            }
        }

        public override string GetMenuName()
        {
            return "Functions";
        }

        #endregion

        #region Function Implementations

        private void ToggleTurretCrew()
        {
            if (_turretCrewManager != null)
            {
                _turretCrewManager.ToggleTurretCrew();
            }
            else
            {
                Speak("Turret crew system unavailable");
            }
        }

        private void ExplodeNearbyVehicles()
        {
            // Defensive: Validate player
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
                return;

            Vehicle[] vehicles = World.GetNearbyVehicles(player.Position, Constants.NEARBY_VEHICLE_EXPLODE_RADIUS);
            if (vehicles == null || vehicles.Length == 0)
                return;

            Vehicle playerVehicle = player.CurrentVehicle;
            int explodedCount = 0;

            foreach (Vehicle v in vehicles)
            {
                // Defensive: Check vehicle is valid
                if (v == null || !v.Exists() || v.IsDead) continue;

                // Temporarily disable god mode on player vehicle if needed
                // Compare by Handle - SHVDN returns new wrapper objects each call
                if (!_settings.GetSetting("vehicleGodMode") && playerVehicle != null && playerVehicle.Handle == v.Handle)
                {
                    v.CanBeVisiblyDamaged = true;
                    v.CanEngineDegrade = true;
                    v.CanTiresBurst = true;
                    v.CanWheelsBreak = true;
                    v.IsExplosionProof = false;
                    v.IsFireProof = false;
                    v.IsInvincible = false;
                    v.IsBulletProof = false;
                    v.IsMeleeProof = false;
                }

                v.Explode();
                v.MarkAsNoLongerNeeded();
                explodedCount++;
            }

            Speak($"Exploded {explodedCount} vehicles");
        }

        private void MakePedsAttackEachOther()
        {
            List<Ped> eligiblePeds = GetEligiblePeds();

            if (eligiblePeds.Count < 4)
            {
                Speak("More nearby people are needed.");
                return;
            }

            for (int i = 0; i < eligiblePeds.Count; i++)
            {
                // Pick a random target (not self) - avoid potential infinite loop
                int targetIndex = (i + 1 + _random.Next(eligiblePeds.Count - 1)) % eligiblePeds.Count;

                Ped ped = eligiblePeds[i];
                ped.Task.ClearAllImmediately();
                ped.KeepTaskWhenMarkedAsNoLongerNeeded = false;
                ped.BlockPermanentEvents = false;
                ped.Weapons.Give(WeaponHash.APPistol, 1000, true, true);
                ped.Task.Combat(eligiblePeds[targetIndex]);
                ped.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                ped.BlockPermanentEvents = true;
            }

            Speak($"{eligiblePeds.Count} peds attacking each other");
        }

        private void KillAllNearbyPeds()
        {
            List<Ped> eligiblePeds = GetEligiblePeds();

            foreach (Ped ped in eligiblePeds)
            {
                ped.Kill();
            }

            Speak($"Killed {eligiblePeds.Count} peds");
        }

        private void RaiseWantedLevel()
        {
            Wanted wanted = Game.Player.Wanted;
            int currentLevel = wanted.WantedLevel;

            if (currentLevel < 5)
            {
                wanted.SetWantedLevel(currentLevel + 1, false);
                wanted.ApplyWantedLevelChangeNow(false);
                Speak($"Wanted level {currentLevel + 1}");
            }
            else
            {
                Speak("Already at maximum wanted level");
            }
        }

        private void ClearWantedLevel()
        {
            Wanted wanted = Game.Player.Wanted;
            wanted.SetWantedLevel(0, false);
            wanted.ApplyWantedLevelChangeNow(false);
            Speak("Wanted level cleared");
        }

        /// <summary>
        /// Put on or take off the online Juggernaut suit. This replaces the whole
        /// player ped, so the manager carries health, armour and the entire weapon
        /// inventory across and puts them back on the way out.
        /// </summary>
        private void ToggleJuggernautSuit()
        {
            if (_playerModel == null)
            {
                Speak("The suit is not available.");
                return;
            }

            // The manager speaks its own result, including why it failed
            _playerModel.Toggle(PedHash.Juggernaut01M, "Juggernaut suit");
        }

        private void MarkWaypointToMissionObjective()
        {
            // Defensive: Validate player
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
            {
                Speak("Player unavailable");
                return;
            }

            GTA.Math.Vector3 playerPos = player.Position;
            float closestDistance = float.MaxValue;
            GTA.Math.Vector3 closestBlipPos = GTA.Math.Vector3.Zero;
            bool foundBlip = false;

            // Iterate through common mission blip sprites to find objectives
            foreach (int sprite in MissionBlipSprites)
            {
                // Get first blip of this sprite type
                int blipHandle = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, sprite);

                while (Function.Call<bool>(Hash.DOES_BLIP_EXIST, blipHandle))
                {
                    // Get blip position
                    GTA.Math.Vector3 blipPos = Function.Call<GTA.Math.Vector3>(Hash.GET_BLIP_INFO_ID_COORD, blipHandle);

                    // Skip if this is the player's waypoint
                    if (Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
                    {
                        Blip waypoint = World.WaypointBlip;
                        if (waypoint != null)
                        {
                            float waypointDist = (blipPos - waypoint.Position).Length();
                            if (waypointDist < 5f)
                            {
                                blipHandle = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, sprite);
                                continue;
                            }
                        }
                    }

                    // Calculate distance
                    float distance = (blipPos - playerPos).Length();

                    // Keep track of closest mission blip
                    if (distance < closestDistance && distance > 10f) // Skip very close blips
                    {
                        closestDistance = distance;
                        closestBlipPos = blipPos;
                        foundBlip = true;
                    }

                    // Get next blip of this sprite type
                    blipHandle = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, sprite);
                }
            }

            if (foundBlip)
            {
                // Set waypoint to the mission objective
                Function.Call(Hash.SET_NEW_WAYPOINT, closestBlipPos.X, closestBlipPos.Y);
                int soundId = Function.Call<int>(Hash.GET_SOUND_ID);
                Function.Call(Hash.PLAY_SOUND_FRONTEND, soundId, "WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                Function.Call(Hash.RELEASE_SOUND_ID, soundId);

                // Announce distance
                float distanceMiles = closestDistance * Constants.METERS_TO_MILES;
                if (distanceMiles < 0.1f)
                {
                    int feet = (int)(closestDistance * Constants.METERS_TO_FEET);
                    Speak($"Waypoint set, {feet} feet away");
                }
                else
                {
                    Speak($"Waypoint set, {distanceMiles:F1} miles away");
                }
            }
            else
            {
                Speak("No mission objective found");
            }
        }

        private List<Ped> GetEligiblePeds()
        {
            // Defensive: Validate player
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
                return new List<Ped>();

            Ped[] allPeds = World.GetNearbyPeds(player.Position, Constants.NEARBY_PED_COMBAT_RADIUS);
            if (allPeds == null || allPeds.Length == 0)
                return new List<Ped>();

            List<Ped> eligible = new List<Ped>(allPeds.Length);

            foreach (Ped ped in allPeds)
            {
                // Defensive: Check ped is valid
                if (ped == null || !ped.Exists() || ped.IsDead) continue;
                // Use int directly - avoids ToString() allocation
                if (!HashManager.TryGetName((int)ped.Model.NativeValue, out string name)) continue;
                if (Array.IndexOf(Constants.PLAYER_MODELS, name) >= 0) continue;
                eligible.Add(ped);
            }

            return eligible;
        }

        #endregion
    }
}
