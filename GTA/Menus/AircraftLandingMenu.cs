using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using DavyKager;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for aircraft landing destinations with in-flight navigation guidance.
    /// Provides airports, helipads, and other landing locations with approach info.
    /// Supports AutoFly integration for automatic flight and landing.
    /// </summary>
    public class AircraftLandingMenu : IMenuState
    {
        private readonly SettingsManager _settings;
        private readonly AutoFlyManager _autoFlyManager;
        private readonly List<LandingDestination> _destinations;
        private int _currentIndex;

        // Navigation state
        private bool _navigationActive;
        private LandingDestination _activeDestination;
        private long _lastNavAnnounceTick;
        private float _lastAnnouncedDistance;

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
                // GTA V coordinate system: heading 0° = North (+Y), 90° = East (+X), 180° = South (-Y), 270° = West (-X)
                // X = sin(heading), Y = cos(heading)
                if (runwayHeading >= 0 && !isHelipad)
                {
                    float radians = runwayHeading * (float)Math.PI / 180f;
                    // Runway end is in the direction of the runway heading (aircraft lands INTO the heading)
                    // Position is the touchdown point (threshold), RunwayEndPosition is where the rollout ends
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

            /// <summary>
            /// Constructor with explicit runway endpoint (for precision landings)
            /// </summary>
            public LandingDestination(string name, float x, float y, float z, float runwayHeading,
                float endX, float endY, float endZ)
            {
                Name = name;
                Position = new Vector3(x, y, z);
                RunwayHeading = runwayHeading;
                IsHelipad = false;
                Elevation = z * Constants.METERS_TO_FEET;
                RunwayEndPosition = new Vector3(endX, endY, endZ);
            }
        }

        /// <summary>
        /// Create AircraftLandingMenu with optional AutoFly integration
        /// </summary>
        /// <param name="settings">Settings manager</param>
        /// <param name="autoFlyManager">Optional AutoFlyManager for automatic flight (null for navigation-only)</param>
        public AircraftLandingMenu(SettingsManager settings, AutoFlyManager autoFlyManager = null)
        {
            _settings = settings;
            _autoFlyManager = autoFlyManager;
            _navigationActive = false;
            _activeDestination = null;
            _lastNavAnnounceTick = 0;
            _lastAnnouncedDistance = float.MaxValue;

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

            _currentIndex = 0;
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            int step = fastScroll ? 10 : 1;

            if (_currentIndex >= step)
                _currentIndex -= step;
            else
                _currentIndex = _destinations.Count - 1;
        }

        public void NavigateNext(bool fastScroll = false)
        {
            int step = fastScroll ? 10 : 1;

            if (_currentIndex < _destinations.Count - step)
                _currentIndex += step;
            else
                _currentIndex = 0;
        }

        public string GetCurrentItemText()
        {
            LandingDestination dest = _destinations[_currentIndex];
            Vector3 playerPos = Game.Player.Character.Position;
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
            return $"{_currentIndex + 1} of {_destinations.Count}: {dest.Name}, {typeText}, {distanceText}";
        }

        public void ExecuteSelection()
        {
            LandingDestination dest = _destinations[_currentIndex];

            // Check if we're in an aircraft and AutoFly is available
            Ped player = Game.Player.Character;
            Vehicle vehicle = player?.CurrentVehicle;
            bool inAircraft = vehicle != null &&
                (vehicle.ClassType == VehicleClass.Planes || vehicle.ClassType == VehicleClass.Helicopters);

            if (inAircraft && _autoFlyManager != null)
            {
                // Launch AutoFly to destination with autoland
                _autoFlyManager.StartDestination(dest);
                GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                // Cancel any existing navigation-only mode
                _navigationActive = false;
                _activeDestination = null;
                return;
            }

            // Fallback: Navigation-only mode (set waypoint and provide voice guidance)
            Function.Call(Hash.SET_NEW_WAYPOINT, dest.Position.X, dest.Position.Y);
            GTA.Audio.PlaySoundFrontend("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");

            // Activate navigation
            _navigationActive = true;
            _activeDestination = dest;
            _lastAnnouncedDistance = float.MaxValue;
            _lastNavAnnounceTick = 0;

            // Announce initial info
            Vector3 playerPos = player.Position;
            float distance = (dest.Position - playerPos).Length();
            float distanceMiles = distance * Constants.METERS_TO_MILES;
            string direction = SpatialCalculator.GetDirectionTo(playerPos, dest.Position);

            string announcement = $"Navigation active to {dest.Name}, {direction}, {distanceMiles:F1} miles";

            if (!dest.IsHelipad && dest.RunwayHeading >= 0)
            {
                int runwayNumber = (int)Math.Round(dest.RunwayHeading / 10f);
                if (runwayNumber == 0) runwayNumber = 36;
                announcement += $", runway heading {(int)dest.RunwayHeading} degrees";
            }

            Tolk.Speak(announcement);
        }

        /// <summary>
        /// Called each tick to provide in-flight navigation updates.
        /// Should be called from GTA11Y.OnTick when in aircraft.
        /// </summary>
        public void UpdateNavigation(Vehicle aircraft, Vector3 position, long currentTick)
        {
            if (!_navigationActive || _activeDestination == null || aircraft == null)
                return;

            // Throttle announcements to every 5 seconds minimum
            if (currentTick - _lastNavAnnounceTick < 50_000_000) // 5 seconds
                return;

            float distance = (_activeDestination.Position - position).Length();
            float distanceMiles = distance * Constants.METERS_TO_MILES;

            // Check if arrived (within 100 meters)
            if (distance < 100f)
            {
                Tolk.Speak("Arriving at destination");
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
                int runwayNumber = (int)Math.Round(_activeDestination.RunwayHeading / 10f);
                if (runwayNumber == 0) runwayNumber = 36;
                announcement += $", align runway {(int)_activeDestination.RunwayHeading}";
            }

            Tolk.Speak(announcement);
        }

        /// <summary>
        /// Check if navigation is currently active
        /// </summary>
        public bool IsNavigationActive => _navigationActive;

        /// <summary>
        /// Cancel active navigation
        /// </summary>
        public void CancelNavigation()
        {
            if (_navigationActive)
            {
                _navigationActive = false;
                _activeDestination = null;
                Tolk.Speak("Navigation cancelled");
            }
        }

        public string GetMenuName()
        {
            return "Aircraft Landing";
        }

        public bool HasActiveSubmenu => false;

        public void ExitSubmenu()
        {
            // No submenu - do nothing
        }
    }
}
