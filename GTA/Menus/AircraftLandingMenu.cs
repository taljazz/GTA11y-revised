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

            /// <summary>
            /// Unit vector pointing down the runway in the landing direction.
            /// Zero for helipads. Used to place the approach fix behind the threshold.
            /// </summary>
            public Vector3 RunwayDirection { get; }

            public LandingDestination(string name, float x, float y, float z, float runwayHeading = -1f, bool isHelipad = false)
            {
                Name = name;
                Position = new Vector3(x, y, z);
                RunwayHeading = runwayHeading;
                IsHelipad = isHelipad;
                Elevation = z * Constants.METERS_TO_FEET;

                // Calculate runway end position for fixed-wing landing (TASK_PLANE_LAND).
                // GTA V headings run COUNTERCLOCKWISE: 0 = North (+Y), 90 = West (-X),
                // 180 = South, 270 = East, which is the same convention the landing
                // beacon uses via Atan2(-dx, dy) to match Entity.Heading. The forward
                // vector is therefore (-sin, cos), NOT (sin, cos) - that mirror gives
                // the direction of heading (360 - h), so a runway stored as 100 came
                // out pointing down the 260 approach and every landing task was handed
                // the runway line reversed.
                if (runwayHeading >= 0 && !isHelipad)
                {
                    float radians = runwayHeading * Constants.DEG_TO_RAD;
                    // Runway end is in the direction of the runway heading (aircraft lands INTO the heading)
                    RunwayDirection = new Vector3(-(float)Math.Sin(radians), (float)Math.Cos(radians), 0f);
                    RunwayEndPosition = Position + RunwayDirection * Constants.DEFAULT_RUNWAY_LENGTH;
                }
                else
                {
                    // Helipad - endpoint is same as position
                    RunwayDirection = Vector3.Zero;
                    RunwayEndPosition = Position;
                }
            }

            /// <summary>
            /// The point a plane should be flown to before the engine landing task
            /// takes over: out along the extended centerline behind the threshold,
            /// at pattern altitude above the field.
            /// </summary>
            public Vector3 ApproachFix =>
                Position
                - RunwayDirection * Constants.APPROACH_FIX_DISTANCE
                + new Vector3(0f, 0f, Constants.APPROACH_FIX_ALTITUDE);
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

        // Autopilot state machine - the engine tasks only fly part of an approach,
        // so the menu sequences them and narrates each stage for the pilot
        private AutopilotPhase _autopilotPhase;
        private LandingDestination _autopilotDestination;
        private Vehicle _autopilotVehicle;
        private long _autopilotStartTick;
        private long _lastAutopilotCheckTick;
        private long _lastProgressTick;
        private float _lastProgressDistance;
        private bool _stallWarned;
        private bool _wasAirborne;
        private bool _shortFinalCalled;
        private long _finalStartTick;
        private long _lastTelemetryTick;
        private Vector3 _autopilotFix;      // Approach fix, resolved once at engage
        private int _goAroundCount;

        // Vehicle-side mission tracking. Polling GetActiveMissionType is how the
        // engine actually reports whether a scripted mission took and is still
        // running - physical state can only guess. Null means the current phase
        // runs on a PED task (TASK_PLANE_LAND, taxi, VTOL goto), which registers
        // no vehicle mission, so there is nothing to verify.
        private VehicleMissionType? _expectedMission;
        private long _missionIssuedTick;
        private bool _missionReissued;

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
            _autopilotPhase = AutopilotPhase.Off;
            _autopilotDestination = null;
            _autopilotVehicle = null;

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
        private const int ACTION_LINE_UP = 3;
        private const int ACTION_CANCEL_ALL = 4;

        protected override int SubmenuItemCount => 5;

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
                    return IsAutopilotActive && _autopilotDestination == _destinations[SelectedIndex]
                        ? $"Auto-land here (autopilot), currently {DescribePhase()}"
                        : "Auto-land here (autopilot)";
                case ACTION_TAXI:
                    return "Taxi here (planes on the ground)";
                case ACTION_LINE_UP:
                    return "Line up for takeoff (moves you onto the runway)";
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
                case ACTION_LINE_UP:
                    LineUpForTakeoff(SelectedIndex);
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
                    StartHelicopterLanding(player, aircraft, dest, index);
                }
                else if (aircraftClass == VehicleClass.Planes)
                {
                    if (dest.IsHelipad)
                    {
                        // VTOL-capable planes (Hydra, Avenger, Tula...) can hover
                        // to the pad with the precision VTOL task; the pilot does
                        // the final few meters of descent guided by the beacon.
                        if (Constants.VTOL_VEHICLE_HASHES.Contains(aircraft.Model.Hash))
                            StartVtolApproach(player, aircraft, dest, index);
                        else
                            Speak("Planes need a runway. Choose a runway destination, or fly a helicopter or VTOL aircraft for helipads.");
                        return;
                    }

                    if (!aircraft.IsInAir)
                    {
                        Speak("You are on the ground. Take off first, or use taxi to move along the airfield.");
                        return;
                    }

                    StartPlaneApproach(player, aircraft, dest, index);
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
                ResetAutopilot();
            }
        }

        /// <summary>
        /// Fly a helicopter to the destination and set it down.
        /// flightHeight is an absolute Z in meters above sea level, so it has to
        /// clear both the aircraft and a rooftop pad - a fixed value would order
        /// the heli down to near sea level on the way to somewhere like Maze Bank.
        /// The requested touchdown orientation is only honored when
        /// AttainRequestedOrientation is set alongside LandOnArrival.
        /// </summary>
        private void StartHelicopterLanding(Ped player, Vehicle aircraft, LandingDestination dest, int index)
        {
            bool wantsOrientation = dest.RunwayHeading >= 0;
            float orientation = wantsOrientation ? dest.RunwayHeading : -1f;

            HeliMissionFlags flags = HeliMissionFlags.LandOnArrival;
            if (wantsOrientation)
                flags |= HeliMissionFlags.AttainRequestedOrientation;

            int cruiseZ = (int)(Math.Max(aircraft.Position.Z, dest.Position.Z) + Constants.HELI_CRUISE_CLEARANCE);

            player.Task.StartHeliMission(
                aircraft,
                dest.Position,
                VehicleMissionType.LandAndWait,
                Constants.HELI_CRUISE_SPEED,
                Constants.HELI_TARGET_REACHED_DIST,
                cruiseZ,                                 // absolute altitude to hold en route
                Constants.HELI_MIN_TERRAIN_CLEARANCE,
                orientation,
                Constants.HELI_SLOWDOWN_DISTANCE,
                flags);

            BeginAutopilot(AutopilotPhase.Final, dest, aircraft, index);
            SetExpectedMission(VehicleMissionType.LandAndWait);
            Speak($"Helicopter autopilot engaged, landing at {dest.Name}. {DescribeApproach(aircraft, dest)}");
            LogAutopilotParams("heli", aircraft, dest,
                $"cruiseZ={cruiseZ}|minTerrain={Constants.HELI_MIN_TERRAIN_CLEARANCE}" +
                $"|orient={orientation:F0}|flags={flags}");
        }

        /// <summary>
        /// Hold a VTOL plane over a helipad. The engine task flies it there and
        /// hovers; the pilot eases it down the last few meters on the beacon.
        /// autoPilot stays false - that flag is for running the task with no
        /// driver aboard, which is not what we want with the player flying.
        /// </summary>
        private void StartVtolApproach(Ped player, Vehicle aircraft, LandingDestination dest, int index)
        {
            float? orientation = dest.RunwayHeading >= 0 ? (float?)dest.RunwayHeading : null;

            player.Task.GoToPlanePreciseVtol(
                aircraft,
                dest.Position + new Vector3(0f, 0f, Constants.VTOL_HOVER_HEIGHT),
                (int)(dest.Position.Z + Constants.VTOL_HOVER_HEIGHT),  // absolute hold altitude
                Constants.VTOL_MIN_TERRAIN_CLEARANCE,
                orientation);

            BeginAutopilot(AutopilotPhase.Hovering, dest, aircraft, index);
            SetExpectedMission(null);   // VTOL goto is a ped task, not a vehicle mission
            Speak($"VTOL approach engaged to {dest.Name}. {DescribeApproach(aircraft, dest)} I will tell you when you are over the pad.");
            LogAutopilotParams("vtol", aircraft, dest,
                $"holdZ={(int)(dest.Position.Z + Constants.VTOL_HOVER_HEIGHT)}" +
                $"|minTerrain={Constants.VTOL_MIN_TERRAIN_CLEARANCE}" +
                $"|orient={(orientation.HasValue ? orientation.Value.ToString("F0") : "none")}");
        }

        /// <summary>
        /// Begin a plane landing. TASK_PLANE_LAND only flies the last part of an
        /// approach - handed the runway from far out and off-axis it wanders or
        /// gives up - so unless the plane is already close and lined up, fly a
        /// positioning leg to the approach fix first and hand over from there.
        /// precise is false so a VTOL keeps its nozzles in horizontal flight.
        /// </summary>
        private void StartPlaneApproach(Ped player, Vehicle aircraft, LandingDestination dest, int index)
        {
            Vector3 position = aircraft.Position;
            float distanceToThreshold = (dest.Position - position).Length();

            // Without a runway heading there is no centerline to position along,
            // so hand straight to the landing task and let it sort the approach out
            if (dest.RunwayHeading < 0f || dest.RunwayDirection == Vector3.Zero)
            {
                BeginAutopilot(AutopilotPhase.Final, dest, aircraft, index);
                LogAutopilotParams("plane-direct-final", aircraft, dest, "skip=no-runway-heading");
                EngageFinalApproach(player, aircraft, dest, true);
                return;
            }

            if (IsReadyForFinal(aircraft, dest, position))
            {
                BeginAutopilot(AutopilotPhase.Final, dest, aircraft, index);
                LogAutopilotParams("plane-direct-final", aircraft, dest,
                    $"skip=already-lined-up|handoff={Constants.APPROACH_HANDOFF_DISTANCE}" +
                    $"|alignTol={Constants.APPROACH_ALIGN_TOLERANCE}");
                EngageFinalApproach(player, aircraft, dest, true);
                return;
            }

            Vector3 fix = ResolveApproachFix(dest);
            _autopilotFix = fix;

            IssuePositioningLeg(player, aircraft, fix);
            BeginAutopilot(AutopilotPhase.Positioning, dest, aircraft, index);
            SetExpectedMission(VehicleMissionType.GoTo);

            float fixMiles = (fix - position).Length() * Constants.METERS_TO_MILES;
            Speak($"Autopilot engaged for {dest.Name}. Positioning for the approach, {fixMiles:F1} miles to the turn, runway heading {(int)dest.RunwayHeading} degrees.");
            LogAutopilotParams("plane-positioning", aircraft, dest,
                $"fix={fix.X:F0},{fix.Y:F0},{fix.Z:F0}|toFix={(fix - position).Length():F0}" +
                $"|cruiseSpd={Constants.APPROACH_CRUISE_SPEED}|capture={Constants.APPROACH_CAPTURE_RADIUS}" +
                $"|handoff={Constants.APPROACH_HANDOFF_DISTANCE}|alignTol={Constants.APPROACH_ALIGN_TOLERANCE}");
        }

        /// <summary>
        /// Fly to the approach fix. Mirrors how Rockstar's own scripts move a
        /// plane between points: a GoTo mission with an absolute flight height
        /// and a terrain floor, orientation left to the AI.
        /// </summary>
        private static void IssuePositioningLeg(Ped player, Vehicle aircraft, Vector3 fix)
        {
            player.Task.StartPlaneMission(
                aircraft,
                fix,
                VehicleMissionType.GoTo,
                Constants.APPROACH_CRUISE_SPEED,
                Constants.APPROACH_CAPTURE_RADIUS,
                (int)fix.Z,                                    // absolute altitude to hold
                Constants.APPROACH_MIN_TERRAIN_CLEARANCE,
                -1f,                                           // orientation: let the AI fly it
                false);                                        // no VTOL nozzles on the cruise leg
        }

        /// <summary>
        /// Hand the plane to the engine landing task along the runway line,
        /// dropping the gear on the way in.
        /// </summary>
        private void EngageFinalApproach(Ped player, Vehicle aircraft, LandingDestination dest, bool immediate)
        {
            string method = IssueLandingTask(player, aircraft, dest);

            _autopilotPhase = AutopilotPhase.Final;
            _shortFinalCalled = false;
            _finalStartTick = Game.GameTime;

            // TASK_PLANE_LAND is a ped task and registers no vehicle mission;
            // the Land mission does, so only then is there something to verify
            SetExpectedMission(Constants.USE_PLANE_LAND_MISSION
                ? (VehicleMissionType?)VehicleMissionType.Land
                : null);

            // New leg, new yardstick for the stall check
            ResetProgress((dest.Position - aircraft.Position).Length());

            bool gearCommanded = DeployLandingGear(aircraft);
            string gear = gearCommanded ? " Gear down." : "";
            Speak(immediate
                ? $"On final approach to {dest.Name}.{gear}"
                : $"Lined up. On final approach to {dest.Name}.{gear}");
            LogAutopilotEvent("FINAL", aircraft, dest,
                $"method={method}|immediate={(immediate ? 1 : 0)}|gearCommanded={(gearCommanded ? 1 : 0)}" +
                $"|rwyEnd={dest.RunwayEndPosition.X:F0},{dest.RunwayEndPosition.Y:F0}");
        }

        /// <summary>
        /// Issue whichever engine task flies the final approach, per
        /// Constants.USE_PLANE_LAND_MISSION. Returns the method name for the log
        /// so a trace always says which one produced it.
        /// </summary>
        private static string IssueLandingTask(Ped player, Vehicle aircraft, LandingDestination dest)
        {
            if (!Constants.USE_PLANE_LAND_MISSION)
            {
                // Both points sit on the runway: threshold and far end
                player.Task.LandPlane(dest.Position, dest.RunwayEndPosition, aircraft);
                return "land-task";
            }

            // Aim at the touchdown zone rather than the threshold itself, so the
            // descent finishes over runway instead of short of it
            Vector3 aim = dest.Position + (dest.RunwayDirection * Constants.LAND_MISSION_AIM_DISTANCE);

            // Mirrors Rockstar's own landing call in fm_mission_controller.ysc:
            // TASK_VEHICLE_MISSION_COORS_TARGET(ped, veh, coords, 19, 20f, 786468, -1f, -1f, 1).
            // Note this is the VEHICLE mission native, not TASK_PLANE_MISSION -
            // the Land mission type is only known to be exercised through this one.
            player.Task.StartVehicleMission(
                aircraft,
                aim,
                VehicleMissionType.Land,
                Constants.LAND_MISSION_SPEED,
                (VehicleDrivingFlags)Constants.LAND_MISSION_DRIVING_FLAGS,
                Constants.LAND_MISSION_REACHED_DIST,
                Constants.LAND_MISSION_STRAIGHT_LINE_DIST,
                true);

            return "land-mission";
        }

        /// <summary>
        /// Whether the aircraft is genuinely on a final approach: in the approach
        /// corridor before the threshold, near the centerline, and pointed down
        /// the runway. All four tests matter - straight-line range alone passes an
        /// aircraft sitting kilometers beyond the far end of the runway.
        /// </summary>
        private static bool IsReadyForFinal(Vehicle aircraft, LandingDestination dest, Vector3 position)
        {
            // No centerline to judge against - let the landing task decide
            if (dest.RunwayDirection == Vector3.Zero)
                return true;

            float along, lateral;
            RunwayOffsets(dest, position, out along, out lateral);

            // "along" is signed along the landing direction from the threshold,
            // so an aircraft still to fly is at a negative value
            if (along > -Constants.APPROACH_MIN_FINAL_DISTANCE)
                return false;   // past the threshold, or too close to line up
            if (along < -Constants.APPROACH_HANDOFF_DISTANCE)
                return false;   // still too far out

            if (Math.Abs(lateral) > Constants.APPROACH_MAX_CROSSTRACK)
                return false;   // off to one side of the extended centerline

            return HeadingErrorTo(aircraft.Heading, dest.RunwayHeading) <= Constants.APPROACH_ALIGN_TOLERANCE;
        }

        /// <summary>Smallest absolute angle in degrees between two headings.</summary>
        private static float HeadingErrorTo(float heading, float target)
        {
            float diff = Math.Abs(heading - target) % 360f;
            return diff > 180f ? 360f - diff : diff;
        }

        /// <summary>
        /// Lower the landing gear if it is not already down.
        /// Returns true when this call actually commanded it, so the caller only
        /// says "gear down" when something changed.
        /// </summary>
        private static bool DeployLandingGear(Vehicle aircraft)
        {
            try
            {
                VehicleLandingGearState state = aircraft.LandingGearState;
                if (state == VehicleLandingGearState.Deployed ||
                    state == VehicleLandingGearState.Deploying ||
                    state == VehicleLandingGearState.Broken)
                    return false;

                aircraft.LandingGearState = VehicleLandingGearState.Deploying;
                return true;
            }
            catch (Exception ex)
            {
                // Not every aircraft has retractable gear - not an error
                Logger.Debug($"AircraftLandingMenu: landing gear unavailable ({ex.Message})");
                return false;
            }
        }

        /// <summary>Bearing and distance phrase used when engaging the autopilot.</summary>
        private static string DescribeApproach(Vehicle aircraft, LandingDestination dest)
        {
            Vector3 position = aircraft.Position;
            float miles = (dest.Position - position).Length() * Constants.METERS_TO_MILES;
            string direction = SpatialCalculator.GetDirectionTo(position, dest.Position);
            return $"{direction}, {miles:F1} miles.";
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

            if (dest.IsHelipad)
            {
                Speak("That is a helipad, not somewhere a plane can taxi to. Pick a runway.");
                return;
            }

            // The taxi task drives the plane but will not start it, so with the
            // engine off it sits there at zero throttle looking like a hang
            if (!SafeEngineRunning(aircraft))
            {
                Speak("Start the engine first, then taxi.");
                LogAutopilotEvent("TAXI-REFUSED", aircraft, dest, "reason=engine-off");
                return;
            }

            // Taxiing follows the ground - a destination on the far side of the map
            // means grinding cross-country for many minutes, which is never what
            // was meant. Fly there instead.
            float distance = (dest.Position - aircraft.Position).Length();

            // Already parked on it - running the whole arrive-and-brake sequence
            // just to announce a stop half a second later helps nobody
            if (distance <= Constants.TAXI_ARRIVAL_RADIUS)
            {
                Speak($"You are already at {dest.Name}.");
                return;
            }

            if (distance > Constants.TAXI_MAX_DISTANCE)
            {
                float miles = distance * Constants.METERS_TO_MILES;
                Speak($"{dest.Name} is {miles:F1} miles away, too far to taxi. Take off and use auto-land instead.");
                return;
            }

            try
            {
                player.Task.PlaneTaxi(aircraft, dest.Position, Constants.TAXI_CRUISE_SPEED, Constants.TAXI_TARGET_REACHED_DIST);

                BeginAutopilot(AutopilotPhase.Taxiing, dest, aircraft, index);
                SetExpectedMission(null);   // TASK_PLANE_TAXI is a ped task
                Speak($"Taxiing to {dest.Name}, {(int)distance} meters. I will tell you when you arrive.");
                LogAutopilotParams("taxi", aircraft, dest,
                    $"cruiseSpd={Constants.TAXI_CRUISE_SPEED}|reachDist={Constants.TAXI_TARGET_REACHED_DIST}" +
                    $"|arrivalRadius={Constants.TAXI_ARRIVAL_RADIUS}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AircraftLandingMenu.EngageTaxi");
                Speak("Failed to start taxiing.");
                ResetAutopilot();
            }
        }

        /// <summary>
        /// Put the aircraft on the runway threshold pointing down it, ready to
        /// depart. Parked on an apron there is no way to tell which way to face,
        /// and guessing means taxiing into fences and buildings - so this places
        /// the aircraft rather than only rotating it.
        /// </summary>
        private void LineUpForTakeoff(int index)
        {
            LandingDestination dest = _destinations[index];

            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                Speak("You must be in an aircraft to line up.");
                return;
            }

            Vehicle aircraft = player.CurrentVehicle;
            if (aircraft == null || !aircraft.Exists())
            {
                Speak("You must be in an aircraft to line up.");
                return;
            }

            if (player.SeatIndex != VehicleSeat.Driver)
            {
                Speak("You must be the pilot to line up.");
                return;
            }

            if (dest.IsHelipad || dest.RunwayDirection == Vector3.Zero)
            {
                Speak($"{dest.Name} is a helipad. Helicopters and V TOLs lift off straight up, no lineup needed.");
                return;
            }

            if (aircraft.IsInAir)
            {
                Speak("You are airborne. Land first, then line up.");
                return;
            }

            // Placing the aircraft is a short reposition onto the runway, not a
            // cross-map jump - flying somewhere is what auto-land is for
            float distance = (dest.Position - aircraft.Position).Length();
            if (distance > Constants.LINEUP_MAX_DISTANCE)
            {
                float miles = distance * Constants.METERS_TO_MILES;
                Speak($"{dest.Name} is {miles:F1} miles away. Get to that airfield first, then line up.");
                return;
            }

            try
            {
                // Any autopilot or halt request would fight the reposition
                ClearFlightTasks();
                ResetAutopilot();

                Vector3 start = dest.Position + (dest.RunwayDirection * Constants.LINEUP_OFFSET);

                aircraft.Velocity = Vector3.Zero;
                aircraft.Position = start;
                aircraft.Heading = dest.RunwayHeading;
                aircraft.PlaceOnGround();

                if (!aircraft.IsEngineRunning)
                    aircraft.IsEngineRunning = true;

                int runwayAhead = (int)(Constants.DEFAULT_RUNWAY_LENGTH - Constants.LINEUP_OFFSET);
                Speak($"Lined up on {dest.Name}, heading {(int)dest.RunwayHeading} degrees, " +
                      $"about {runwayAhead} meters of runway ahead. Full throttle when ready.");

                Logger.Info($"AP|LINEUP|dest={dest.Name}|hdg={dest.RunwayHeading:F0}" +
                            $"|pos={start.X:F0},{start.Y:F0},{start.Z:F0}|movedFrom={distance:F0}m");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AircraftLandingMenu.LineUpForTakeoff");
                Speak("Failed to line up on the runway.");
            }
        }

        /// <summary>
        /// Cancel the autopilot task, beacon, and voice navigation.
        /// </summary>
        private void CancelAllGuidance()
        {
            ClearFlightTasks();

            _navigationActive = false;
            _activeDestination = null;
            ResetAutopilot();
            StopBeacon();
            Speak("Navigation and autopilot cancelled. You have manual control.");
        }

        /// <summary>
        /// Drop the engine flight task and any pending halt request so the pilot
        /// gets the controls back cleanly.
        /// </summary>
        private void ClearFlightTasks()
        {
            try
            {
                Ped player = Game.Player?.Character;

                // Resolve the aircraft BEFORE clearing tasks. Clearing drops the
                // ped's in-vehicle task, after which CurrentVehicle can read back
                // null - and then the halt request below was never cancelled and
                // held the aircraft frozen for the rest of its duration, which is
                // exactly the "says manual control but will not release" symptom.
                Vehicle aircraft = _autopilotVehicle != null && _autopilotVehicle.Exists()
                    ? _autopilotVehicle
                    : player?.CurrentVehicle;

                bool aircraftValid = aircraft != null && aircraft.Exists();
                bool wasSeated = player != null && player.Exists() &&
                                 aircraftValid && player.IsSittingInVehicle(aircraft);

                VehicleMissionType missionBefore = aircraftValid
                    ? SafeActiveMission(aircraft)
                    : VehicleMissionType.None;

                if (player != null && player.Exists())
                {
                    // CLEAR_PED_TASKS only - NEVER the immediate variant here. Per
                    // the native docs, CLEAR_PED_TASKS_IMMEDIATELY "teleports the
                    // ped": it rebuilds the ped's state on the spot, which warps
                    // the pilot out of the seat. Using it on release ejected the
                    // player from the plane the moment the autopilot disengaged.
                    player.Task.ClearAll();
                }

                if (aircraftValid)
                {
                    // The ped-side clear abandons the mission, but the vehicle-side
                    // task can keep flying the aircraft. Clear it on the VEHICLE -
                    // no typed wrapper exists for this native (verified against the
                    // 3.7 assembly), hence the direct call. This replaces what the
                    // immediate ped clear was wrongly being used for.
                    Function.Call(Hash.CLEAR_PRIMARY_VEHICLE_TASK, aircraft.Handle);
                    aircraft.StopBringingToHalt();

                    // Any eject here is a bug in the release path - put the pilot
                    // straight back in the seat rather than leaving them falling
                    // next to a rolling aircraft
                    if (wasSeated && player != null && player.Exists() &&
                        !player.IsSittingInVehicle(aircraft))
                    {
                        player.SetIntoVehicle(aircraft, VehicleSeat.Driver);
                        Logger.Warning("AP|RELEASE|re-seated pilot after unexpected ejection");
                    }

                    // Confirm the release rather than assume it. If a mission is
                    // still registered the clear did not take, so try once more.
                    VehicleMissionType missionAfter = SafeActiveMission(aircraft);
                    if (missionAfter != VehicleMissionType.None)
                    {
                        Function.Call(Hash.CLEAR_PRIMARY_VEHICLE_TASK, aircraft.Handle);
                        missionAfter = SafeActiveMission(aircraft);
                        Logger.Warning($"AP|RELEASE|retry|stillActive={missionAfter}");
                    }

                    Logger.Info($"AP|RELEASE|missionBefore={missionBefore}|missionAfter={missionAfter}" +
                                $"|spd={aircraft.Speed:F1}|seated={(wasSeated ? 1 : 0)}");
                }
                else
                {
                    Logger.Info("AP|RELEASE|no-aircraft");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AircraftLandingMenu.ClearFlightTasks");
            }
        }

        /// <summary>
        /// The vehicle-side mission currently driving this aircraft. This is the
        /// engine's own answer to "is my scripted task still running", which the
        /// community uses to verify a task took and to re-issue when it did not.
        /// </summary>
        private static VehicleMissionType SafeActiveMission(Vehicle aircraft)
        {
            try { return aircraft.GetActiveMissionType(); }
            catch { return VehicleMissionType.None; }
        }

        /// <summary>
        /// Record what vehicle mission the phase just issued should show up as.
        /// Pass null for phases driven by a ped task, which register no mission.
        /// </summary>
        private void SetExpectedMission(VehicleMissionType? mission)
        {
            _expectedMission = mission;
            _missionIssuedTick = Game.GameTime;
            _missionReissued = false;
        }

        /// <summary>
        /// Re-issue the task for the current phase. Used when the engine reports
        /// the mission is no longer running but the phase has not finished.
        /// Returns false when the phase has no re-issuable leg.
        /// </summary>
        private bool ReissueCurrentLeg(Ped player, Vehicle aircraft, LandingDestination dest)
        {
            switch (_autopilotPhase)
            {
                case AutopilotPhase.Positioning:
                    IssuePositioningLeg(player, aircraft, _autopilotFix);
                    return true;
                case AutopilotPhase.Intercept:
                    StartInterceptLeg(player, aircraft, dest);
                    return true;
                case AutopilotPhase.Final:
                    IssueLandingTask(player, aircraft, dest);
                    SetExpectedMission(Constants.USE_PLANE_LAND_MISSION
                        ? (VehicleMissionType?)VehicleMissionType.Land
                        : null);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Verify the issued mission is actually running. A task the engine has
        /// silently dropped otherwise looks identical to one flying perfectly:
        /// the aircraft simply coasts while the menu reports an active approach.
        /// </summary>
        private void CheckMissionAlive(Vehicle aircraft, LandingDestination dest, long currentTick)
        {
            if (!_expectedMission.HasValue)
                return;   // Ped-task phase - nothing registers on the vehicle

            if (currentTick - _missionIssuedTick < Constants.MISSION_CHECK_GRACE)
                return;   // Still settling

            VehicleMissionType active = SafeActiveMission(aircraft);
            if (active == _expectedMission.Value)
                return;

            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
                return;

            if (_missionReissued)
            {
                LogAutopilotEvent("MISSION-LOST", aircraft, dest,
                    $"expected={_expectedMission.Value}|actual={active}|reissuedAlready=1");
                FinishAutopilot("Autopilot task was rejected. You have manual control.", true, "mission-lost");
                return;
            }

            LogAutopilotEvent("MISSION-REISSUE", aircraft, dest,
                $"expected={_expectedMission.Value}|actual={active}");

            if (ReissueCurrentLeg(player, aircraft, dest))
            {
                _missionIssuedTick = currentTick;
                _missionReissued = true;
            }
        }

        #endregion

        #region Autopilot State Machine

        /// <summary>Whether an autopilot phase is currently running.</summary>
        public bool IsAutopilotActive => _autopilotPhase != AutopilotPhase.Off;

        /// <summary>Spoken name of the current phase, for the menu item text.</summary>
        private string DescribePhase()
        {
            switch (_autopilotPhase)
            {
                case AutopilotPhase.Positioning: return "positioning for the approach";
                case AutopilotPhase.Intercept: return "lining up with the runway";
                case AutopilotPhase.Final: return "on final approach";
                case AutopilotPhase.Hovering: return "holding over the pad";
                case AutopilotPhase.Rollout: return "braking on the ground";
                case AutopilotPhase.Taxiing: return "taxiing";
                default: return "off";
            }
        }

        /// <summary>
        /// Arm the state machine for a newly issued task and turn on the beacon
        /// so the pilot gets continuous audio position feedback alongside it.
        /// </summary>
        private void BeginAutopilot(AutopilotPhase phase, LandingDestination dest, Vehicle aircraft, int index)
        {
            _autopilotPhase = phase;
            _autopilotDestination = dest;
            _autopilotVehicle = aircraft;
            _autopilotStartTick = Game.GameTime;
            _lastAutopilotCheckTick = 0;
            _lastProgressTick = Game.GameTime;
            _lastProgressDistance = (dest.Position - aircraft.Position).Length();
            _stallWarned = false;
            _shortFinalCalled = false;
            _wasAirborne = aircraft.IsInAir;
            _goAroundCount = 0;

            EnableBeaconAndNavigation(index);
        }

        /// <summary>Clear all autopilot state without touching tasks or speech.</summary>
        private void ResetAutopilot()
        {
            _autopilotPhase = AutopilotPhase.Off;
            _autopilotDestination = null;
            _autopilotVehicle = null;
            _autopilotStartTick = 0;
            _stallWarned = false;
            _shortFinalCalled = false;
            _wasAirborne = false;
        }

        /// <summary>
        /// Finish cleanly: stop the beacon and voice nav, drop the tasks, and say
        /// what happened. The pilot has the controls again afterwards.
        /// </summary>
        private void FinishAutopilot(string message, bool clearTasks, string reason = null)
        {
            LogAutopilotEvent("END", _autopilotVehicle, _autopilotDestination,
                $"reason={reason ?? "finished"}");

            if (clearTasks)
                ClearFlightTasks();

            ResetAutopilot();
            _navigationActive = false;
            _activeDestination = null;
            StopBeacon();

            if (!string.IsNullOrEmpty(message))
                Speak(message, true);
        }

        /// <summary>
        /// Drive the autopilot forward one step. Called every tick from
        /// UpdateNavigation, then throttled internally.
        /// </summary>
        private void UpdateAutopilot(Vehicle aircraft, Vector3 position, long currentTick)
        {
            if (_autopilotPhase == AutopilotPhase.Off || _autopilotDestination == null)
                return;

            // A different aircraft than the one under autopilot. Announce it only
            // when the original still exists, so a deliberate swap is explained
            // but a stale approach left behind by a crash or death dies quietly.
            if (aircraft == null || !aircraft.Exists() ||
                _autopilotVehicle == null || aircraft.Handle != _autopilotVehicle.Handle)
            {
                bool originalStillFlyable = _autopilotVehicle != null && _autopilotVehicle.Exists();
                FinishAutopilot(originalStillFlyable ? "Autopilot disengaged." : null, false, originalStillFlyable ? "aircraft-changed" : "aircraft-gone");
                return;
            }

            if (currentTick - _lastAutopilotCheckTick < Constants.AUTOPILOT_UPDATE_INTERVAL)
                return;
            _lastAutopilotCheckTick = currentTick;

            try
            {
                // Only a flying phase needs a running engine - a rough landing can
                // kill it during the rollout, and that still ends as an arrival
                bool airbornePhase = _autopilotPhase != AutopilotPhase.Rollout &&
                                     _autopilotPhase != AutopilotPhase.Taxiing;
                if (aircraft.IsDead || (airbornePhase && !aircraft.IsEngineRunning))
                {
                    FinishAutopilot("Autopilot lost, the aircraft is not flyable.", false, "not-flyable");
                    return;
                }

                if (currentTick - _autopilotStartTick > Constants.AUTOPILOT_TIMEOUT)
                {
                    FinishAutopilot("Autopilot timed out. You have manual control.", true, "timeout");
                    return;
                }

                LandingDestination dest = _autopilotDestination;
                float distance = (dest.Position - position).Length();

                // Progress is measured against the leg being flown, not the field.
                // While positioning, the approach fix can lie further from the
                // runway than the aircraft is, so closing on it looks like losing
                // ground if the destination is used as the yardstick.
                float legDistance = _autopilotPhase == AutopilotPhase.Positioning
                    ? (_autopilotFix - position).Length()
                    : distance;
                CheckProgress(legDistance, currentTick);

                LogAutopilotTick(aircraft, position, dest, currentTick);

                // Ask the engine whether the task is still running before acting on
                // a phase that assumes it is. A dropped mission looks exactly like a
                // healthy one from the outside - the aircraft just coasts.
                CheckMissionAlive(aircraft, dest, currentTick);
                if (_autopilotPhase == AutopilotPhase.Off)
                    return;

                switch (_autopilotPhase)
                {
                    case AutopilotPhase.Positioning:
                        UpdatePositioning(aircraft, position, dest, distance);
                        break;
                    case AutopilotPhase.Intercept:
                        UpdateIntercept(aircraft, position, dest, distance);
                        break;
                    case AutopilotPhase.Final:
                        UpdateFinal(aircraft, dest, distance, currentTick);
                        break;
                    case AutopilotPhase.Hovering:
                        UpdateHovering(aircraft, dest, distance);
                        break;
                    case AutopilotPhase.Rollout:
                        UpdateRollout(aircraft, dest);
                        break;
                    case AutopilotPhase.Taxiing:
                        UpdateTaxiing(aircraft, dest, distance);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AircraftLandingMenu.UpdateAutopilot");
                FinishAutopilot("Autopilot error. You have manual control.", true, "error");
            }
        }

        /// <summary>
        /// Positioning leg: fly to the approach fix, then hand over to the engine
        /// landing task once the runway can be captured from here.
        /// </summary>
        private void UpdatePositioning(Vehicle aircraft, Vector3 position, LandingDestination dest, float distance)
        {
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
                return;

            // Close and already pointed down the runway - no intercept leg needed
            if (IsReadyForFinal(aircraft, dest, position))
            {
                EngageFinalApproach(player, aircraft, dest, false);
                return;
            }

            if ((_autopilotFix - position).Length() > Constants.APPROACH_CAPTURE_RADIUS)
                return;

            // At the fix. Arriving here says nothing about which way the nose is
            // pointing - the aircraft flies direct to the fix and gets there on
            // whatever heading the geometry gave it. Fly the centerline in to the
            // threshold next, which is what actually puts it on runway heading.
            StartInterceptLeg(player, aircraft, dest);
        }

        /// <summary>
        /// Second positioning leg: from the approach fix down the extended
        /// centerline toward the threshold, descending toward circuit height.
        /// </summary>
        private void StartInterceptLeg(Ped player, Vehicle aircraft, LandingDestination dest)
        {
            Vector3 target = dest.Position + new Vector3(0f, 0f, Constants.APPROACH_INTERCEPT_ALTITUDE);

            player.Task.StartPlaneMission(
                aircraft,
                target,
                VehicleMissionType.GoTo,
                Constants.APPROACH_CRUISE_SPEED,
                Constants.APPROACH_CAPTURE_RADIUS,
                (int)target.Z,
                Constants.APPROACH_INTERCEPT_CLEARANCE,
                -1f,
                false);

            _autopilotPhase = AutopilotPhase.Intercept;
            ResetProgress((dest.Position - aircraft.Position).Length());
            SetExpectedMission(VehicleMissionType.GoTo);

            Speak("Turning onto the runway heading.");
            LogAutopilotEvent("INTERCEPT", aircraft, dest,
                $"target={target.X:F0},{target.Y:F0},{target.Z:F0}");
        }

        /// <summary>
        /// Intercept leg: hand to the landing task the moment the aircraft is
        /// both close enough and actually tracking the runway heading. If it gets
        /// past the threshold without ever lining up, go back out and try again
        /// rather than letting the landing task circle indefinitely.
        /// </summary>
        private void UpdateIntercept(Vehicle aircraft, Vector3 position, LandingDestination dest, float distance)
        {
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
                return;

            if (IsReadyForFinal(aircraft, dest, position))
            {
                EngageFinalApproach(player, aircraft, dest, false);
                return;
            }

            float along, lateral;
            RunwayOffsets(dest, position, out along, out lateral);

            if (along > Constants.APPROACH_GO_AROUND_OVERSHOOT)
                GoAround(player, aircraft, dest);
        }

        /// <summary>
        /// Overshot the runway without lining up. Fly back out to the approach fix
        /// and re-run the intercept, giving up after a couple of attempts so the
        /// pilot is not left circling forever.
        /// </summary>
        private void GoAround(Ped player, Vehicle aircraft, LandingDestination dest)
        {
            _goAroundCount++;

            if (_goAroundCount > Constants.APPROACH_MAX_GO_AROUNDS)
            {
                LogAutopilotEvent("GO-AROUND-LIMIT", aircraft, dest, $"attempts={_goAroundCount}");
                FinishAutopilot(
                    "Autopilot could not line up with the runway. You have manual control.",
                    true, "go-around-limit");
                return;
            }

            IssuePositioningLeg(player, aircraft, _autopilotFix);

            _autopilotPhase = AutopilotPhase.Positioning;
            ResetProgress((_autopilotFix - aircraft.Position).Length());
            SetExpectedMission(VehicleMissionType.GoTo);

            Speak($"Going around, attempt {_goAroundCount + 1}.");
            LogAutopilotEvent("GO-AROUND", aircraft, dest, $"attempt={_goAroundCount}");
        }

        /// <summary>
        /// The approach fix, with its altitude checked against the terrain under
        /// it. Pattern height above the field is meaningless where the extended
        /// centerline runs over high ground - McKenzie's fix sits over terrain
        /// that rises well above the strip.
        /// </summary>
        private static Vector3 ResolveApproachFix(LandingDestination dest)
        {
            Vector3 fix = dest.ApproachFix;

            try
            {
                // The probe only answers for terrain that is streamed in, which at
                // this range often it is not - the bool says whether to believe it.
                // Water counts as ground so an over-water fix is not raised.
                float ground;
                bool probed = World.GetGroundHeight(fix, out ground,
                    GetGroundHeightMode.ConsiderWaterAsGround);

                if (probed && ground > dest.Position.Z)
                    fix.Z = ground + Constants.APPROACH_FIX_ALTITUDE;
            }
            catch (Exception ex)
            {
                Logger.Debug($"AircraftLandingMenu: ground probe failed at the approach fix ({ex.Message})");
            }

            return fix;
        }

        /// <summary>
        /// Final approach: call short final once, then watch for the wheels to
        /// touch and switch to the rollout.
        /// </summary>
        private void UpdateFinal(Vehicle aircraft, LandingDestination dest, float distance, long currentTick)
        {
            if (DetectTouchdown(aircraft, dest))
                return;

            // A good final is short - roughly the approach corridor flown at the
            // landing task's own approach speed. Well past that the task is not
            // converging, it is orbiting the field, which it will do indefinitely.
            if (currentTick - _finalStartTick > Constants.APPROACH_FINAL_TIMEOUT)
            {
                Ped player = Game.Player?.Character;
                if (player != null && player.Exists())
                {
                    LogAutopilotEvent("FINAL-TIMEOUT", aircraft, dest,
                        $"elapsed={(currentTick - _finalStartTick) / 1000f:F1}");
                    GoAround(player, aircraft, dest);
                    return;
                }
            }

            if (!_shortFinalCalled && aircraft.IsInAir &&
                aircraft.HeightAboveGround <= Constants.SHORT_FINAL_HEIGHT)
            {
                _shortFinalCalled = true;
                Speak("Short final, about to touch down.");
            }
        }

        /// <summary>
        /// VTOL hold: tell the pilot once they are over the pad, then wait for
        /// them to set it down.
        /// </summary>
        private void UpdateHovering(Vehicle aircraft, LandingDestination dest, float distance)
        {
            if (DetectTouchdown(aircraft, dest))
                return;

            if (!_shortFinalCalled && distance <= Constants.VTOL_PAD_ARRIVAL_RADIUS)
            {
                _shortFinalCalled = true;
                Speak("Holding over the pad. Ease off the throttle to set down.");
            }
        }

        /// <summary>
        /// Wheels are down and the plane is braking - finish when it stops.
        /// </summary>
        private void UpdateRollout(Vehicle aircraft, LandingDestination dest)
        {
            if (aircraft.Speed > Constants.AUTOPILOT_STOPPED_SPEED)
                return;

            // Slow is not the same as stopped: a halt request can catch the
            // aircraft mid-bounce, and calling that "stopped" while it is still
            // several meters up hands back control at the worst moment
            if (aircraft.IsInAir)
                return;

            FinishAutopilot($"Stopped at {dest.Name}. You have manual control.", true, "stopped");
        }

        /// <summary>
        /// Ground taxi: PlaneTaxi stops steering once it is within its arrival
        /// distance but leaves the plane rolling, so stop it here and say so.
        /// </summary>
        private void UpdateTaxiing(Vehicle aircraft, LandingDestination dest, float distance)
        {
            if (distance > Constants.TAXI_ARRIVAL_RADIUS)
                return;

            LogAutopilotEvent("TAXI-ARRIVED", aircraft, dest);
            ClearFlightTasks();
            BringToStop(aircraft);
            _autopilotPhase = AutopilotPhase.Rollout;
            Speak($"Arrived at {dest.Name}, braking.");
        }

        /// <summary>
        /// Watch for the transition from airborne to wheels-on-ground. On the
        /// first touchdown, stop the aircraft (planes) or finish (helicopters).
        /// Returns true when the phase changed.
        /// </summary>
        private bool DetectTouchdown(Vehicle aircraft, LandingDestination dest)
        {
            bool inAir = aircraft.IsInAir;

            if (inAir)
            {
                _wasAirborne = true;
                return false;
            }

            if (!_wasAirborne)
                return false;  // Never left the ground - nothing to announce yet

            // This is the record that says whether the approach geometry worked:
            // where the wheels actually met the ground relative to the threshold
            LogAutopilotEvent("TOUCHDOWN", aircraft, dest);

            // Helicopters and VTOLs are down as soon as they are on the ground
            if (aircraft.ClassType == VehicleClass.Helicopters || _autopilotPhase == AutopilotPhase.Hovering)
            {
                FinishAutopilot($"Touchdown at {dest.Name}. You have manual control.", true, "touchdown");
                return true;
            }

            // A plane still has its landing roll to fly
            ClearFlightTasks();
            BringToStop(aircraft);
            _autopilotPhase = AutopilotPhase.Rollout;
            Speak("Touchdown. Braking.", true);
            return true;
        }

        /// <summary>Request a controlled stop over the rollout distance.</summary>
        private static void BringToStop(Vehicle aircraft)
        {
            try
            {
                aircraft.BringToHalt(Constants.ROLLOUT_STOP_DISTANCE, Constants.ROLLOUT_STOP_TIME_MS, false);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AircraftLandingMenu.BringToStop");
            }
        }

        #endregion

        #region Autopilot Telemetry

        // Every record is one greppable line tagged "AP|" at Info level, because
        // MinLevel defaults to Info and raising it to Debug would flood the log
        // with every other subsystem. Field order is stable so a whole approach
        // can be read as a table:
        //   t     seconds since the autopilot engaged
        //   dist  straight-line meters to the destination
        //   along signed meters along the runway from the threshold (+ = past it)
        //   lat   signed meters off the runway centerline
        //   agl   height above ground, asl height above sea level
        //   spd   m/s, hdg heading, hdgErr degrees off the runway heading

        /// <summary>The common per-record state block.</summary>
        private string AutopilotState(Vehicle aircraft, Vector3 position, LandingDestination dest)
        {
            float along, lateral;
            RunwayOffsets(dest, position, out along, out lateral);

            float distance = (dest.Position - position).Length();
            float headingError = dest.RunwayHeading >= 0f
                ? HeadingErrorTo(aircraft.Heading, dest.RunwayHeading)
                : -1f;
            float elapsed = (Game.GameTime - _autopilotStartTick) / 1000f;

            return $"t={elapsed:F1}|phase={_autopilotPhase}|dist={distance:F0}|along={along:F0}|lat={lateral:F0}" +
                   $"|agl={SafeHeightAboveGround(aircraft):F0}|asl={position.Z:F0}|spd={aircraft.Speed:F1}" +
                   $"|hdg={aircraft.Heading:F0}|hdgErr={headingError:F1}|air={(aircraft.IsInAir ? 1 : 0)}" +
                   $"|eng={(SafeEngineRunning(aircraft) ? 1 : 0)}|gear={SafeGearState(aircraft)}" +
                   $"|mis={SafeActiveMission(aircraft)}|want={(_expectedMission.HasValue ? _expectedMission.Value.ToString() : "ped-task")}";
        }

        /// <summary>
        /// Position relative to the runway: distance along it from the threshold
        /// (negative means still short of it) and cross-track offset from the
        /// centerline. Both zero for helipads, which have no runway axis.
        /// </summary>
        private static void RunwayOffsets(LandingDestination dest, Vector3 position, out float along, out float lateral)
        {
            Vector3 dir = dest.RunwayDirection;
            if (dir == Vector3.Zero)
            {
                along = 0f;
                lateral = 0f;
                return;
            }

            Vector3 rel = position - dest.Position;
            along = (rel.X * dir.X) + (rel.Y * dir.Y);
            lateral = (rel.X * dir.Y) - (rel.Y * dir.X);
        }

        private static string SafeGearState(Vehicle aircraft)
        {
            try { return aircraft.LandingGearState.ToString(); }
            catch { return "none"; }
        }

        private static bool SafeEngineRunning(Vehicle aircraft)
        {
            try { return aircraft.IsEngineRunning; }
            catch { return false; }
        }

        private static float SafeHeightAboveGround(Vehicle aircraft)
        {
            try { return aircraft.HeightAboveGround; }
            catch { return -1f; }
        }

        /// <summary>Record the task parameters actually handed to the engine.</summary>
        private void LogAutopilotParams(string mode, Vehicle aircraft, LandingDestination dest, string parameters)
        {
            string model;
            try { model = aircraft.DisplayName; } catch { model = "unknown"; }

            Logger.Info($"AP|ENGAGE|mode={mode}|dest={dest.Name}|pad={(dest.IsHelipad ? 1 : 0)}" +
                        $"|rwyHdg={dest.RunwayHeading:F0}|rwyZ={dest.Position.Z:F1}|veh={model}" +
                        $"|{AutopilotState(aircraft, aircraft.Position, dest)}|{parameters}");
        }

        /// <summary>Record a phase change with the state that triggered it.</summary>
        private void LogAutopilotEvent(string kind, Vehicle aircraft, LandingDestination dest, string extra = null)
        {
            if (aircraft == null || !aircraft.Exists() || dest == null)
            {
                Logger.Info($"AP|{kind}|(no aircraft state){(extra != null ? "|" + extra : "")}");
                return;
            }

            Logger.Info($"AP|{kind}|{AutopilotState(aircraft, aircraft.Position, dest)}" +
                        $"{(extra != null ? "|" + extra : "")}");
        }

        /// <summary>Periodic trace line while any phase is running.</summary>
        private void LogAutopilotTick(Vehicle aircraft, Vector3 position, LandingDestination dest, long currentTick)
        {
            if (currentTick - _lastTelemetryTick < Constants.AUTOPILOT_TELEMETRY_INTERVAL)
                return;

            _lastTelemetryTick = currentTick;

            string leg = _autopilotPhase == AutopilotPhase.Positioning
                ? $"|toFix={(_autopilotFix - position).Length():F0}"
                : "";

            Logger.Info($"AP|TICK|{AutopilotState(aircraft, position, dest)}{leg}");
        }

        #endregion

        #region Autopilot Helpers

        /// <summary>Restart stall tracking against a new leg target.</summary>
        private void ResetProgress(float distance)
        {
            _lastProgressDistance = distance;
            _lastProgressTick = Game.GameTime;
            _stallWarned = false;
        }

        /// <summary>
        /// Warn once if the aircraft has stopped closing on the leg target -
        /// a stalled engine task is otherwise silent and invisible to the pilot.
        /// </summary>
        private void CheckProgress(float distance, long currentTick)
        {
            if (_autopilotPhase == AutopilotPhase.Rollout)
                return;

            if (_lastProgressDistance - distance >= Constants.AUTOPILOT_STALL_PROGRESS)
            {
                _lastProgressDistance = distance;
                _lastProgressTick = currentTick;
                _stallWarned = false;
                return;
            }

            if (_stallWarned || currentTick - _lastProgressTick < Constants.AUTOPILOT_STALL_WARNING)
                return;

            _stallWarned = true;
            Speak("Autopilot is not making progress. Cancel and fly manually if this continues.");
            LogAutopilotEvent("STALL", _autopilotVehicle, _autopilotDestination,
                $"legDist={distance:F0}|sinceProgress={(currentTick - _lastProgressTick) / 1000f:F1}");
        }

        #endregion

        #region Navigation Updates

        /// <summary>
        /// Called each tick to provide in-flight navigation updates.
        /// Should be called from GTA11Y.OnTick when in aircraft.
        /// </summary>
        public void UpdateNavigation(Vehicle aircraft, Vector3 position, long currentTick)
        {
            // The autopilot runs on its own schedule and must keep going even
            // after voice navigation has announced arrival and switched itself off
            UpdateAutopilot(aircraft, position, currentTick);

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

            // Hand the controls back too - an engine flight task outlives a script
            // reload, so leaving one running would strand the pilot in an aircraft
            // flying itself with no menu left to cancel it
            if (IsAutopilotActive)
            {
                ClearFlightTasks();
                ResetAutopilot();
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
