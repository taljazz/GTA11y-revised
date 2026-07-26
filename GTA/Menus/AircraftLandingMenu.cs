using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for aircraft landing destinations with in-flight navigation guidance.
    /// Provides airports, helipads, and other landing locations with approach info.
    /// Top level = destinations; submenu per destination = actions:
    /// beacon/voice navigation toggle, autopilot landing (TASK_PLANE_LAND for
    /// planes on runways, heli land mission for helicopters), and cancel.
    /// </summary>
    public class AircraftLandingMenu : HierarchicalMenuBase
    {
        #region Types

        /// <summary>
        /// Represents a landing destination with approach information.
        /// For runways, includes calculated endpoint for TASK_PLANE_LAND.
        /// </summary>
        internal class LandingDestination
        {
            public string Name { get; }
            public Vector3 Position { get; }
            public float RunwayHeading { get; }  // -1 for helipads (any heading OK)
            public bool IsHelipad { get; }
            public float Elevation { get; }  // Ground elevation in feet
            public Vector3 RunwayEndPosition { get; }  // Calculated runway endpoint for TASK_PLANE_LAND

            public LandingDestination(string name, float x, float y, float z, float runwayHeading = -1f, bool isHelipad = false)
            {
                Name = name;
                Position = new Vector3(x, y, z);
                RunwayHeading = runwayHeading;
                IsHelipad = isHelipad;
                Elevation = z * Constants.METERS_TO_FEET;

                // Calculate runway end position for fixed-wing landing (TASK_PLANE_LAND)
                // GTA V coordinate system: heading 0° = North (+Y), 90° = East (+X)
                // X = sin(heading), Y = cos(heading)
                if (runwayHeading >= 0 && !isHelipad)
                {
                    float radians = runwayHeading * Constants.DEG_TO_RAD;
                    // Runway end is in the direction of the runway heading (aircraft lands INTO the heading)
                    RunwayEndPosition = Position + new Vector3(
                        (float)Math.Sin(radians) * Constants.DEFAULT_RUNWAY_LENGTH,
                        (float)Math.Cos(radians) * Constants.DEFAULT_RUNWAY_LENGTH,
                        0f);
                }
                else
                {
                    // Helipad - endpoint is same as position
                    RunwayEndPosition = Position;
                }
            }
        }

        #endregion

        #region Fields

        private readonly SettingsManager _settings;
        private readonly AudioManager _audio;
        private readonly List<LandingDestination> _destinations;

        // Navigation state
        private bool _navigationActive;
        private LandingDestination _activeDestination;
        private long _lastNavAnnounceTick;
        private float _lastAnnouncedDistance;

        // Landing beacon state
        private bool _beaconActive;
        private int _beaconDestinationIndex;
        private long _lastBeaconPulseTick;
        private long _nextBeaconPulseInterval;

        #endregion

        #region Construction

        public AircraftLandingMenu(SettingsManager settings, AudioManager audio) : base(audio)
        {
            _settings = settings;
            _audio = audio;
            _navigationActive = false;
            _activeDestination = null;
            _lastNavAnnounceTick = 0;
            _lastAnnouncedDistance = float.MaxValue;
            _beaconActive = false;
            _beaconDestinationIndex = -1;

            // Initialize landing destinations
            _destinations = new List<LandingDestination>
            {
                // === MAJOR AIRPORTS ===
                // LSIA - Los Santos International Airport (main runways)
                new LandingDestination("LSIA Runway 3 West", -1336f, -2434f, 13.9f, 93f),
                new LandingDestination("LSIA Runway 3 East", -942f, -2988f, 13.9f, 273f),
                new LandingDestination("LSIA Runway 12 South", -1850f, -2978f, 13.9f, 183f),
                new LandingDestination("LSIA Runway 12 North", -1218f, -2563f, 13.9f, 3f),

                // Sandy Shores Airfield
                new LandingDestination("Sandy Shores Runway North", 1747f, 3273f, 41.1f, 118f),
                new LandingDestination("Sandy Shores Runway South", 1395f, 3130f, 40.4f, 298f),

                // McKenzie Field
                new LandingDestination("McKenzie Field East", 2134f, 4801f, 41.2f, 100f),
                new LandingDestination("McKenzie Field West", 2012f, 4750f, 40.5f, 280f),

                // Fort Zancudo (Military)
                new LandingDestination("Fort Zancudo Runway East", -2259f, 3102f, 32.8f, 117f),
                new LandingDestination("Fort Zancudo Runway West", -2454f, 3015f, 32.8f, 297f),

                // === HELIPADS ===
                // Hospital Helipads
                new LandingDestination("Central Los Santos Hospital Helipad", 338f, -1463f, 46.5f, -1f, true),
                new LandingDestination("Pillbox Hill Hospital Helipad", 307f, -1433f, 46.5f, -1f, true),
                new LandingDestination("Mount Zonah Hospital Helipad", -449f, -340f, 78.2f, -1f, true),
                new LandingDestination("Sandy Shores Medical Center", 1839f, 3672f, 34.3f, -1f, true),

                // Police Station Helipads
                new LandingDestination("LSPD Headquarters Helipad", 449f, -981f, 43.7f, -1f, true),
                new LandingDestination("Vespucci Police Helipad", -1108f, -845f, 37.7f, -1f, true),
                new LandingDestination("Mission Row Police Helipad", 474f, -1019f, 28.0f, -1f, true),

                // Government/Official
                new LandingDestination("FIB Building Helipad", 150f, -749f, 262.9f, -1f, true),
                new LandingDestination("IAA Building Helipad", 93f, -620f, 262.0f, -1f, true),
                new LandingDestination("City Hall Helipad", -544f, -204f, 82.0f, -1f, true),
                new LandingDestination("NOOSE Headquarters Helipad", 2535f, -384f, 100.0f, -1f, true),

                // Corporate Buildings
                new LandingDestination("Maze Bank Tower Helipad", -75f, -818f, 326.2f, -1f, true),
                new LandingDestination("Maze Bank West Helipad", -1380f, -504f, 33.2f, -1f, true),
                new LandingDestination("Arcadius Business Center Helipad", -141f, -598f, 211.8f, -1f, true),
                new LandingDestination("Lombank West Helipad", -1578f, -567f, 115.0f, -1f, true),
                new LandingDestination("Del Perro Heights Helipad", -1447f, -538f, 74.0f, -1f, true),

                // Media
                new LandingDestination("Weazel News Helipad", -598f, -930f, 36.7f, -1f, true),
                new LandingDestination("Lifeinvader Helipad", -1047f, -233f, 44.0f, -1f, true),

                // Docks/Industrial
                new LandingDestination("Merryweather Dock Helipad", 486f, -3339f, 6.1f, -1f, true),
                new LandingDestination("Port of LS Helipad", 1067f, -2970f, 5.9f, -1f, true),

                // Recreational/Other
                new LandingDestination("Playboy Mansion Helipad", -1475f, 167f, 55.7f, -1f, true),
                new LandingDestination("Kortz Center Helipad", -2243f, 264f, 195.0f, -1f, true),
                new LandingDestination("Paleto Bay Sheriff Helipad", -437f, 6019f, 31.5f, -1f, true),
                new LandingDestination("Trevor's Airfield Hangar", 1770f, 3239f, 42.0f, -1f, true),

                // === MILITARY/SPECIAL ===
                new LandingDestination("Fort Zancudo Helipad Main", -2148f, 3176f, 33.0f, -1f, true),
                new LandingDestination("Fort Zancudo Helipad Control Tower", -2358f, 3249f, 101.5f, -1f, true),
                new LandingDestination("Aircraft Carrier Deck", 3082f, -4711f, 15.3f, 60f),
                new LandingDestination("Humane Labs Helipad", 3614f, 3752f, 28.7f, -1f, true),

                // === YACHT/WATER ===
                new LandingDestination("Galaxy Super Yacht Helipad", -2023f, -1038f, 8.97f, -1f, true),

                // === MOUNTAIN/REMOTE ===
                new LandingDestination("Mount Chiliad Summit", 451f, 5566f, 795.4f, -1f, true),
                new LandingDestination("Altruist Camp Clearing", -1170f, 4926f, 224.3f, -1f, true),
                new LandingDestination("Vinewood Sign (Flat area)", 711f, 1198f, 348.5f, -1f, true),
                new LandingDestination("Galileo Observatory Parking", -438f, 1076f, 352.4f, -1f, true),
                new LandingDestination("Land Act Dam Top", 1660f, -13f, 169.4f, -1f, true),
                new LandingDestination("Epsilon Building Helipad", -695f, 82f, 55.9f, -1f, true),

                // === BEACHES/FLAT AREAS ===
                new LandingDestination("Vespucci Beach", -1336f, -1266f, 4.5f, 180f),
                new LandingDestination("Del Perro Beach", -1816f, -1172f, 13.0f, 270f),
                new LandingDestination("Paleto Beach", -276f, 6635f, 7.5f, 0f),
                new LandingDestination("Sandy Shores Beach", 1770f, 3864f, 33.5f, -1f, true),

                // === ROADS (Emergency Landing) ===
                new LandingDestination("Great Ocean Highway (Flat)", -2665f, 2553f, 16.1f, 135f),
                new LandingDestination("Route 68 (Flat stretch)", 1211f, 2908f, 38.7f, 90f),
                new LandingDestination("Senora Freeway (Desert)", 2417f, 3132f, 48.2f, 0f),
            };
        }

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount => _destinations.Count;

        protected override int FastScrollStep => 10;

        protected override string GetItemText(int index)
        {
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists()) return "Unavailable";

            LandingDestination dest = _destinations[index];
            Vector3 playerPos = player.Position;
            float distance = (dest.Position - playerPos).Length();
            float distanceMiles = distance * Constants.METERS_TO_MILES;

            string distanceText;
            if (distanceMiles < 0.1f)
            {
                int feet = (int)(distance * Constants.METERS_TO_FEET);
                distanceText = $"{feet} feet";
            }
            else
            {
                distanceText = $"{distanceMiles:F1} miles";
            }

            string typeText = dest.IsHelipad ? "Helipad" : "Runway";
            string beaconText = (_beaconActive && _beaconDestinationIndex == index) ? ", beacon active" : "";
            return $"{index + 1} of {_destinations.Count}: {dest.Name}, {typeText}, {distanceText}{beaconText}";
        }

        protected override void OnItemActivated(int index)
        {
            // Open the actions submenu for this destination
            EnterSubmenu();
        }

        public override string GetMenuName()
        {
            if (InSubmenu && SelectedIndex >= 0 && SelectedIndex < _destinations.Count)
            {
                return _destinations[SelectedIndex].Name;
            }
            return "Aircraft Landing";
        }

        #endregion

        #region Actions Submenu

        private const int ACTION_TOGGLE_BEACON = 0;
        private const int ACTION_AUTO_LAND = 1;
        private const int ACTION_TAXI = 2;
        private const int ACTION_CANCEL_ALL = 3;

        protected override int SubmenuItemCount => 4;

        protected override string GetSubmenuItemText(int index)
        {
            switch (index)
            {
                case ACTION_TOGGLE_BEACON:
                    bool beaconOnHere = _beaconActive && _beaconDestinationIndex == SelectedIndex;
                    return beaconOnHere
                        ? "Turn off beacon and voice navigation"
                        : "Set beacon and voice navigation";
                case ACTION_AUTO_LAND:
                    return "Auto-land here (autopilot)";
                case ACTION_TAXI:
                    return "Taxi here (planes on the ground)";
                case ACTION_CANCEL_ALL:
                    return "Cancel navigation and autopilot";
                default:
                    return "Unknown";
            }
        }

        protected override void OnSubmenuItemActivated(int index)
        {
            switch (index)
            {
                case ACTION_TOGGLE_BEACON:
                    ToggleBeacon(SelectedIndex);
                    break;
                case ACTION_AUTO_LAND:
                    EngageAutoLand(SelectedIndex);
                    break;
                case ACTION_TAXI:
                    EngageTaxi(SelectedIndex);
                    break;
                case ACTION_CANCEL_ALL:
                    CancelAllGuidance();
                    break;
            }
        }

        /// <summary>
        /// Toggle the audio beacon and voice navigation for a destination.
        /// </summary>
        private void ToggleBeacon(int index)
        {
            LandingDestination dest = _destinations[index];

            // Turn off if already active for this destination
            if (_beaconActive && _beaconDestinationIndex == index)
            {
                _beaconActive = false;
                _beaconDestinationIndex = -1;
                _audio?.StopBeacon();

                // Also cancel voice navigation
                _navigationActive = false;
                _activeDestination = null;

                Speak($"Beacon off, {dest.Name}");
                return;
            }

            EnableBeaconAndNavigation(index);

            // Announce with bearing and distance
            Ped player = Game.Player.Character;
            Vector3 playerPos = player.Position;
            float distance = (dest.Position - playerPos).Length();
            float distanceMiles = distance * Constants.METERS_TO_MILES;
            string direction = SpatialCalculator.GetDirectionTo(playerPos, dest.Position);

            string announcement = $"Beacon on, {dest.Name}, {direction}, {distanceMiles:F1} miles";

            if (!dest.IsHelipad && dest.RunwayHeading >= 0)
            {
                announcement += $", runway heading {(int)dest.RunwayHeading} degrees";
            }

            Speak(announcement);
        }

        /// <summary>
        /// Turn on the beacon, GPS waypoint, and voice navigation for a destination
        /// without announcing (callers announce their own context).
        /// </summary>
        private void EnableBeaconAndNavigation(int index)
        {
            LandingDestination dest = _destinations[index];

            _beaconActive = true;
            _beaconDestinationIndex = index;
            _lastBeaconPulseTick = 0;
            _nextBeaconPulseInterval = 0;

            // Set GPS waypoint
            Function.Call(Hash.SET_NEW_WAYPOINT, dest.Position.X, dest.Position.Y);
            int soundId = Function.Call<int>(Hash.GET_SOUND_ID);
            Function.Call(Hash.PLAY_SOUND_FRONTEND, soundId, "WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
            Function.Call(Hash.RELEASE_SOUND_ID, soundId);

            // Activate voice navigation
            _navigationActive = true;
            _activeDestination = dest;
            _lastAnnouncedDistance = float.MaxValue;
            _lastNavAnnounceTick = 0;
        }

        /// <summary>
        /// Engage autopilot landing at the destination.
        /// Planes use TASK_PLANE_LAND with the stored runway start/end points;
        /// helicopters fly a heli land-and-wait mission that touches down on arrival.
        /// The beacon and voice navigation stay on for audio progress feedback.
        /// </summary>
        private void EngageAutoLand(int index)
        {
            LandingDestination dest = _destinations[index];

            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                Speak("You must be flying an aircraft to auto-land.");
                return;
            }

            Vehicle aircraft = player.CurrentVehicle;
            if (aircraft == null || !aircraft.Exists())
            {
                Speak("You must be flying an aircraft to auto-land.");
                return;
            }

            if (player.SeatIndex != VehicleSeat.Driver)
            {
                Speak("You must be the pilot to auto-land.");
                return;
            }

            VehicleClass aircraftClass = aircraft.ClassType;

            try
            {
                if (aircraftClass == VehicleClass.Helicopters)
                {
                    // Helicopter: land-and-wait mission that touches down on arrival.
                    // Runway heading doubles as the requested landing orientation when set.
                    float orientation = dest.RunwayHeading >= 0 ? dest.RunwayHeading : -1f;

                    player.Task.StartHeliMission(
                        aircraft,
                        dest.Position,
                        VehicleMissionType.LandAndWait,
                        30f,     // cruise speed (m/s)
                        5f,      // target reached distance
                        30,      // flight height above route
                        20,      // minimum height above terrain
                        orientation,
                        100f,    // slow-down distance
                        HeliMissionFlags.LandOnArrival);

                    EnableBeaconAndNavigation(index);
                    Speak($"Helicopter autopilot engaged, landing at {dest.Name}. Beacon and voice guidance active.");
                    Logger.Info($"AircraftLandingMenu: Heli auto-land engaged to {dest.Name}");
                }
                else if (aircraftClass == VehicleClass.Planes)
                {
                    if (dest.IsHelipad)
                    {
                        // VTOL-capable planes (Hydra, Avenger, Tula...) can hover
                        // to the pad with the precision VTOL task; the pilot does
                        // the final few meters of descent guided by the beacon.
                        if (Constants.VTOL_VEHICLE_HASHES.Contains(aircraft.Model.Hash))
                        {
                            float? orientation = dest.RunwayHeading >= 0 ? (float?)dest.RunwayHeading : null;

                            player.Task.GoToPlanePreciseVtol(
                                aircraft,
                                dest.Position + new Vector3(0f, 0f, 3f),
                                (int)(dest.Position.Z + 3f),   // hold altitude just above the pad
                                2,                             // minimum height above terrain
                                orientation);

                            EnableBeaconAndNavigation(index);
                            Speak($"VTOL approach engaged, hovering to {dest.Name}. When the beacon pitch levels out, ease down to touch down.");
                            Logger.Info($"AircraftLandingMenu: VTOL precise approach engaged to {dest.Name}");
                        }
                        else
                        {
                            Speak("Planes need a runway. Choose a runway destination, or fly a helicopter or VTOL aircraft for helipads.");
                        }
                        return;
                    }

                    // Plane: engine-level landing task using the stored runway
                    // threshold and rollout endpoint.
                    player.Task.LandPlane(dest.Position, dest.RunwayEndPosition, aircraft);

                    EnableBeaconAndNavigation(index);
                    Speak($"Plane autopilot engaged, landing on {dest.Name}. Beacon and voice guidance active.");
                    Logger.Info($"AircraftLandingMenu: Plane auto-land engaged to {dest.Name}");
                }
                else
                {
                    Speak("You must be flying a plane or helicopter to auto-land.");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AircraftLandingMenu.EngageAutoLand");
                Speak("Failed to engage autopilot.");
            }
        }

        /// <summary>
        /// Taxi a plane along the ground to the destination using the engine's
        /// taxi task, which follows taxiways instead of cutting across grass.
        /// </summary>
        private void EngageTaxi(int index)
        {
            LandingDestination dest = _destinations[index];

            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                Speak("You must be in a plane on the ground to taxi.");
                return;
            }

            Vehicle aircraft = player.CurrentVehicle;
            if (aircraft == null || !aircraft.Exists() || aircraft.ClassType != VehicleClass.Planes)
            {
                Speak("Taxiing is for planes. Helicopters can auto-land directly.");
                return;
            }

            if (player.SeatIndex != VehicleSeat.Driver)
            {
                Speak("You must be the pilot to taxi.");
                return;
            }

            if (aircraft.IsInAir)
            {
                Speak("You are airborne. Use auto-land instead, then taxi after touchdown.");
                return;
            }

            try
            {
                player.Task.PlaneTaxi(aircraft, dest.Position, 12f, 10f);

                EnableBeaconAndNavigation(index);
                Speak($"Taxiing to {dest.Name}. Beacon and voice guidance active.");
                Logger.Info($"AircraftLandingMenu: Taxi engaged to {dest.Name}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AircraftLandingMenu.EngageTaxi");
                Speak("Failed to start taxiing.");
            }
        }

        /// <summary>
        /// Cancel the autopilot task, beacon, and voice navigation.
        /// </summary>
        private void CancelAllGuidance()
        {
            try
            {
                // Clear the landing task so the player regains manual control
                Ped player = Game.Player?.Character;
                if (player != null && player.Exists())
                {
                    player.Task.ClearAll();
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AircraftLandingMenu.CancelAllGuidance");
            }

            _navigationActive = false;
            _activeDestination = null;
            StopBeacon();
            Speak("Navigation and autopilot cancelled. You have manual control.");
        }

        #endregion

        #region Navigation Updates

        /// <summary>
        /// Called each tick to provide in-flight navigation updates.
        /// Should be called from GTA11Y.OnTick when in aircraft.
        /// </summary>
        public void UpdateNavigation(Vehicle aircraft, Vector3 position, long currentTick)
        {
            if (!_navigationActive || _activeDestination == null || aircraft == null)
                return;

            // Throttle announcements to every 5 seconds minimum
            if (currentTick - _lastNavAnnounceTick < 5_000) // 5 seconds
                return;

            float distance = (_activeDestination.Position - position).Length();
            float distanceMiles = distance * Constants.METERS_TO_MILES;

            // Check if arrived (within 100 meters)
            if (distance < 100f)
            {
                Speak("Arriving at destination");
                _navigationActive = false;
                _activeDestination = null;
                return;
            }

            // Determine announcement intervals based on distance
            float announcementInterval;
            if (distanceMiles > 5f)
                announcementInterval = 2f;  // Every 2 miles when far
            else if (distanceMiles > 1f)
                announcementInterval = 0.5f;  // Every half mile
            else
                announcementInterval = 0.25f;  // Every quarter mile when close

            // Check if we should announce
            float distanceChange = _lastAnnouncedDistance - distanceMiles;
            if (distanceChange < announcementInterval)
                return;

            _lastNavAnnounceTick = currentTick;
            _lastAnnouncedDistance = distanceMiles;

            // Calculate direction to destination
            string direction = SpatialCalculator.GetDirectionTo(position, _activeDestination.Position);
            float angle = (float)SpatialCalculator.CalculateAngle(
                position.X, position.Y, _activeDestination.Position.X, _activeDestination.Position.Y);

            // Calculate heading to destination (for approach)
            float headingToDestination = angle;
            float aircraftHeading = aircraft.Heading;
            float headingDiff = headingToDestination - aircraftHeading;
            if (headingDiff > 180f) headingDiff -= 360f;
            if (headingDiff < -180f) headingDiff += 360f;

            // Calculate altitude difference
            float currentAltitude = position.Z * Constants.METERS_TO_FEET;
            float targetElevation = _activeDestination.Elevation;
            float altitudeDiff = currentAltitude - targetElevation;

            // Build announcement
            string distanceText;
            if (distanceMiles >= 1f)
            {
                distanceText = $"{distanceMiles:F1} miles";
            }
            else
            {
                // Use quarter mile increments
                if (distanceMiles >= 0.75f)
                    distanceText = "three quarters of a mile";
                else if (distanceMiles >= 0.5f)
                    distanceText = "half a mile";
                else if (distanceMiles >= 0.25f)
                    distanceText = "a quarter mile";
                else
                {
                    int feet = (int)(distance * Constants.METERS_TO_FEET);
                    distanceText = $"{feet} feet";
                }
            }

            string announcement = $"{distanceText}, {direction}";

            // Add turn guidance if significantly off-course
            if (Math.Abs(headingDiff) > 30f)
            {
                if (headingDiff > 0)
                    announcement += $", turn right {(int)Math.Abs(headingDiff)} degrees";
                else
                    announcement += $", turn left {(int)Math.Abs(headingDiff)} degrees";
            }

            // Add altitude guidance when close
            if (distanceMiles < 2f)
            {
                if (altitudeDiff > 500f)
                    announcement += $", descend {(int)altitudeDiff} feet";
                else if (altitudeDiff < -100f)
                    announcement += $", climb {(int)Math.Abs(altitudeDiff)} feet";
            }

            // Add runway heading info when very close
            if (distanceMiles < 0.5f && !_activeDestination.IsHelipad && _activeDestination.RunwayHeading >= 0)
            {
                announcement += $", align runway {(int)_activeDestination.RunwayHeading}";
            }

            Speak(announcement);
        }

        /// <summary>
        /// Check if navigation is currently active
        /// </summary>
        public bool IsNavigationActive => _navigationActive;

        /// <summary>
        /// Cancel active navigation and stop beacon
        /// </summary>
        public void CancelNavigation()
        {
            if (_navigationActive)
            {
                _navigationActive = false;
                _activeDestination = null;
                Speak("Navigation cancelled");
            }
            StopBeacon();
        }

        #endregion

        #region Beacon

        /// <summary>
        /// Check if the landing beacon is currently active
        /// </summary>
        public bool IsBeaconActive => _beaconActive;

        /// <summary>
        /// Stop the landing beacon
        /// </summary>
        public void StopBeacon()
        {
            if (_beaconActive)
            {
                _beaconActive = false;
                _beaconDestinationIndex = -1;
                _audio?.StopBeacon();
            }
        }

        /// <summary>
        /// Update the landing beacon audio. Called from OnTick when in aircraft.
        /// Calculates stereo pan (bearing), frequency (altitude), and pulse rate (distance).
        /// </summary>
        public void UpdateBeacon(Vehicle aircraft, Vector3 position, long currentTick)
        {
            if (!_beaconActive || _audio == null || aircraft == null ||
                _beaconDestinationIndex < 0 || _beaconDestinationIndex >= _destinations.Count)
                return;

            // Throttle pulses based on distance-dependent interval
            if (_nextBeaconPulseInterval > 0 && currentTick - _lastBeaconPulseTick < _nextBeaconPulseInterval)
                return;

            LandingDestination dest = _destinations[_beaconDestinationIndex];
            Vector3 destPos = dest.Position;

            // 1. Calculate horizontal distance squared (avoid sqrt for distance thresholds)
            float dx = destPos.X - position.X;
            float dy = destPos.Y - position.Y;
            float distanceSq = dx * dx + dy * dy;

            // 2. Calculate bearing to destination in GTA V convention (counterclockwise: 0=N, 90=W, 180=S, 270=E)
            // Atan2(-dx, dy) produces GTA-convention bearing directly, matching aircraft.Heading
            float bearingToDestination = (float)Math.Atan2(-dx, dy) * Constants.RAD_TO_DEG;
            if (bearingToDestination < 0f) bearingToDestination += 360f;

            // 3. Calculate relative bearing (both in GTA convention now)
            // Positive = destination is to the right, Negative = destination is to the left
            float relativeBearing = bearingToDestination - aircraft.Heading;
            if (relativeBearing > 180f) relativeBearing -= 360f;
            if (relativeBearing < -180f) relativeBearing += 360f;

            // 4. Calculate stereo pan from relative bearing
            float absRelBearing = Math.Abs(relativeBearing);
            float pan;

            if (absRelBearing < Constants.BEACON_PAN_DEAD_ZONE)
            {
                pan = 0f;
            }
            else
            {
                float panAmount = (absRelBearing - Constants.BEACON_PAN_DEAD_ZONE) * Constants.BEACON_PAN_RANGE_INV;
                if (panAmount > 1f) panAmount = 1f;
                pan = relativeBearing > 0 ? panAmount : -panAmount;
            }

            // 5. Reduce volume when beacon is behind aircraft (>120 degrees off heading)
            float gainMultiplier = absRelBearing > 120f ? Constants.BEACON_BEHIND_GAIN_FACTOR : 1f;

            // 6. Calculate frequency from altitude difference
            // Higher above destination = lower pitch, at level = base frequency, below = higher pitch
            float frequency = Constants.BEACON_BASE_FREQUENCY -
                ((position.Z * Constants.METERS_TO_FEET - dest.Elevation) * Constants.BEACON_ALTITUDE_SCALE);

            // 7. Calculate pulse interval from squared distance (avoids sqrt)
            long pulseIntervalTicks;
            if (distanceSq < Constants.BEACON_OVERHEAD_DISTANCE_SQ)
                pulseIntervalTicks = Constants.BEACON_PULSE_OVERHEAD_TICKS;
            else if (distanceSq < Constants.BEACON_CLOSE_DISTANCE_SQ)
                pulseIntervalTicks = Constants.BEACON_PULSE_CLOSE_TICKS;
            else if (distanceSq < Constants.BEACON_NEAR_DISTANCE_SQ)
                pulseIntervalTicks = Constants.BEACON_PULSE_NEAR_TICKS;
            else if (distanceSq < Constants.BEACON_MEDIUM_DISTANCE_SQ)
                pulseIntervalTicks = Constants.BEACON_PULSE_MEDIUM_TICKS;
            else
                pulseIntervalTicks = Constants.BEACON_PULSE_FAR_TICKS;

            // 8. Play the pulse
            _audio.PlayBeaconPulse(pan, frequency, gainMultiplier);

            _lastBeaconPulseTick = currentTick;
            _nextBeaconPulseInterval = pulseIntervalTicks;
        }

        #endregion
    }
}
