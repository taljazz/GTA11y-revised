using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using DavyKager;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for fun functions like chaos modes
    /// Uses HashManager for shared hash lookups (loaded once, shared across classes)
    /// </summary>
    public class FunctionsMenu : IMenuState
    {
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

        private readonly List<string> _functions;
        private readonly SettingsManager _settings;
        private readonly Random _random;
        private int _currentIndex;

        public FunctionsMenu(SettingsManager settings)
        {
            _settings = settings;
            _random = new Random();

            _functions = new List<string>
            {
                "Mark Waypoint to Mission Objective",
                "Blow up all nearby vehicles",
                "Make all nearby pedestrians attack each other",
                "Instantly kill all nearby pedestrians",
                "Raise Wanted Level",
                "Clear Wanted Level"
            };

            _currentIndex = 0;
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            if (_currentIndex > 0)
                _currentIndex--;
            else
                _currentIndex = _functions.Count - 1;
        }

        public void NavigateNext(bool fastScroll = false)
        {
            if (_currentIndex < _functions.Count - 1)
                _currentIndex++;
            else
                _currentIndex = 0;
        }

        public string GetCurrentItemText()
        {
            return _functions[_currentIndex];
        }

        public void ExecuteSelection()
        {
            switch (_currentIndex)
            {
                case 0:
                    MarkWaypointToMissionObjective();
                    break;
                case 1:
                    ExplodeNearbyVehicles();
                    break;
                case 2:
                    MakePedsAttackEachOther();
                    break;
                case 3:
                    KillAllNearbyPeds();
                    break;
                case 4:
                    RaiseWantedLevel();
                    break;
                case 5:
                    ClearWantedLevel();
                    break;
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

            foreach (Vehicle v in vehicles)
            {
                // Defensive: Check vehicle is valid
                if (v == null || !v.Exists() || v.IsDead) continue;

                // Temporarily disable god mode on player vehicle if needed
                // Compare by Handle - SHVDN returns new wrapper objects each call
                Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;
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
            }
        }

        private void MakePedsAttackEachOther()
        {
            List<Ped> eligiblePeds = GetEligiblePeds();

            if (eligiblePeds.Count < 4)
            {
                Tolk.Speak("More nearby people are needed.");
                return;
            }

            for (int i = 0; i < eligiblePeds.Count; i++)
            {
                // Pick a random target (not self)
                int targetIndex = _random.Next(0, eligiblePeds.Count);
                while (targetIndex == i)
                {
                    targetIndex = _random.Next(0, eligiblePeds.Count);
                }

                Ped ped = eligiblePeds[i];
                ped.Task.ClearAllImmediately();
                ped.AlwaysKeepTask = false;
                ped.BlockPermanentEvents = false;
                ped.Weapons.Give(WeaponHash.APPistol, 1000, true, true);
                ped.Task.FightAgainst(eligiblePeds[targetIndex]);
                ped.AlwaysKeepTask = true;
                ped.BlockPermanentEvents = true;
            }
        }

        private void KillAllNearbyPeds()
        {
            List<Ped> eligiblePeds = GetEligiblePeds();

            foreach (Ped ped in eligiblePeds)
            {
                ped.Kill();
            }
        }

        private void RaiseWantedLevel()
        {
            if (Game.Player.WantedLevel < 5)
                Game.Player.WantedLevel++;
        }

        private void ClearWantedLevel()
        {
            Game.Player.WantedLevel = 0;
        }

        private void MarkWaypointToMissionObjective()
        {
            // Defensive: Validate player
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
            {
                Tolk.Speak("Player unavailable");
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
                GTA.Audio.PlaySoundFrontend("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                // Announce distance
                float distanceMiles = closestDistance * Constants.METERS_TO_MILES;
                if (distanceMiles < 0.1f)
                {
                    int feet = (int)(closestDistance * Constants.METERS_TO_FEET);
                    Tolk.Speak($"Waypoint set, {feet} feet away");
                }
                else
                {
                    Tolk.Speak($"Waypoint set, {distanceMiles:F1} miles away");
                }
            }
            else
            {
                Tolk.Speak("No mission objective found");
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

        public string GetMenuName()
        {
            return "Functions";
        }

        public bool HasActiveSubmenu => false;

        public void ExitSubmenu()
        {
            // No submenu - do nothing
        }
    }
}
