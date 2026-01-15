using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using GrandTheftAccessibility.Menus;

namespace GrandTheftAccessibility
{
/// <summary>
/// Information about the optimal flight path considering terrain and aircraft performance
/// </summary>
internal struct FlightPathInfo
{
    public float RequiredHeadingChange;    // Degrees to turn (positive = right)
    public float RecommendedAltitude;     // Optimal altitude for this segment
    public float TerrainClearance;        // Distance above terrain ahead
}

/// <summary>
/// Manages autonomous aircraft flight including cruise, waypoint navigation, and autoland.
/// Uses GTA V's native AI flight tasks (issued ONCE, not every frame) for smooth flight.
/// Follows the same architectural patterns as AutoDriveManager.
/// Enhanced with realistic flight path planning including wind compensation and terrain avoidance.
/// </summary>
public class AutoFlyManager
    {
        #region Fields

        // Dependencies
        private readonly AudioManager _audio;
        private readonly SettingsManager _settings;
        private readonly WeatherManager _weatherManager;  // For weather-based speed adjustments
        private readonly CollisionDetector _collisionDetector;  // For collision-based speed adjustments (Grok)

        // Core state
        private bool _autoFlyActive;
        private int _flightMode;           // FLIGHT_MODE_NONE, CRUISE, WAYPOINT, DESTINATION
        private bool _taskIssued;          // Ensures task issued only once (CRITICAL!)
        private int _aircraftType;         // AIRCRAFT_TYPE_FIXED_WING, HELICOPTER, etc.
        private int _lastAircraftHandle;   // Track aircraft for change detection

        // Flight phase state machine (for DESTINATION mode)
        private int _currentPhase;         // PHASE_CRUISE, APPROACH, FINAL, etc.

        // Target tracking
        private Vector3 _destinationPos;
        private Vector3 _runwayEndPos;     // For TASK_PLANE_LAND (fixed-wing)
        private float _runwayHeading;      // Runway heading in degrees (-1 for helipads)
        private bool _isHelipad;
        private string _destinationName;

        // Cruise mode tracking
        private float _targetAltitude;     // meters
        private float _targetHeading;      // degrees
        private float _targetSpeed;        // m/s
        private float _initialAltitude;    // Starting altitude when cruise began

        // Distance tracking for announcements
        private float _lastDistanceToDestination;
        private long _lastDistanceAnnounceTick;

        // Landing gear tracking (fixed-wing)
        private bool _gearDeployed;
        private bool _gearDeployAnnounced;

        // Enhanced landing tracking
        private float _approachStartAltitude;     // Altitude when approach phase started
        private float _glideslopeTargetAltitude;  // Target altitude based on glideslope
        private int _stableOnGroundCount;         // Count of consecutive stable-on-ground checks
        private float _lastHeightAboveGround;     // For rate of descent monitoring

        // Announcement priority queue (same pattern as AutoDriveManager)
        private long _lastAnnouncementTick;
        private int _lastAnnouncementPriority;

        // Throttling tick trackers
        private long _lastApproachAnnounceTick;

        // Pause state
        private int _pauseState;
        private float _prePauseSpeed;
        private int _prePauseMode;

        // Cruise correction throttling
        private long _lastCruiseCorrectionTick;

        // Waypoint mode state
        private bool _circleModeActive;    // For waypoint mode - circle after arrival

        // Stuck detection (Grok optimization) - for aerial recovery
        private int _flightStuckCounter;          // Count of consecutive non-progress checks
        private float _lastProgressDistance;      // Distance to destination at last check
        private float _lastProgressAltitude;      // Altitude at last check

        // Cached ground height at destination (avoid repeated World.GetGroundHeight calls)
        private float _destinationGroundZ;
        private bool _groundHeightCached;

        // Pre-allocated OutputArgument to avoid per-tick allocations in terrain checks
        // CRITICAL: Reused across calls to eliminate GC pressure
        private readonly OutputArgument _terrainHeightArg = new OutputArgument();

        // Pre-allocated StringBuilder for announcement formatting (avoids string allocations)
        private readonly System.Text.StringBuilder _announceBuilder = new System.Text.StringBuilder(128);

        // Landing phase timeout tracking - prevents stuck state during landing
        private long _landingPhaseStartTick;
        private const long LANDING_PHASE_TIMEOUT = 600_000_000; // 60 seconds max for touchdown/taxiing phases

        #endregion

        #region Properties

        public bool IsActive => _autoFlyActive;
        public int FlightMode => _flightMode;
        public int CurrentPhase => _currentPhase;
        public bool IsPaused => _pauseState == Constants.PAUSE_STATE_PAUSED;
        public string DestinationName => _destinationName;
        public float TargetAltitude => _targetAltitude;
        public float TargetSpeed => _targetSpeed;

        #endregion

        #region Constructor

        public AutoFlyManager(AudioManager audio, SettingsManager settings)
        {
            _audio = audio;
            _settings = settings;
            _weatherManager = new WeatherManager();  // Initialize weather manager for flight speed adjustments
            _collisionDetector = new CollisionDetector();  // Initialize collision detector (Grok optimization)

            // Initialize defaults
            _targetSpeed = Constants.AUTOFLY_DEFAULT_SPEED;
            _targetAltitude = Constants.AUTOFLY_DEFAULT_ALTITUDE;
            _autoFlyActive = false;
            _taskIssued = false;
            _flightMode = Constants.FLIGHT_MODE_NONE;
            _currentPhase = Constants.PHASE_INACTIVE;
            _pauseState = Constants.PAUSE_STATE_NONE;

            // Initialize tracking state
            ResetState();
        }

        #endregion

        #region Public Methods - Start/Stop

        /// <summary>
        /// Start cruise mode - maintain current altitude and heading
        /// </summary>
        public void StartCruise()
        {
            Ped player = Game.Player.Character;
            Vehicle aircraft = player.CurrentVehicle;

            if (!ValidateAircraft(player, aircraft))
                return;

            // Stop any existing flight task
            Stop(false);

            try
            {
                _aircraftType = GetAircraftType(aircraft);
                _lastAircraftHandle = aircraft.Handle;

                // Get current flight parameters
                _targetAltitude = aircraft.Position.Z;
                _targetHeading = aircraft.Heading;
                _initialAltitude = _targetAltitude;

                // Set appropriate speed for aircraft type
                if (_aircraftType == Constants.AIRCRAFT_TYPE_BLIMP)
                {
                    _targetSpeed = Constants.AUTOFLY_BLIMP_DEFAULT_SPEED;
                }
                else if (_aircraftType == Constants.AIRCRAFT_TYPE_HELICOPTER ||
                    _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_HOVER)
                {
                    _targetSpeed = Constants.AUTOFLY_HELI_DEFAULT_SPEED;
                }
                else
                {
                    _targetSpeed = Constants.AUTOFLY_DEFAULT_SPEED;
                }

                // Issue cruise task based on aircraft type
                IssueCruiseTask(player, aircraft);

                _autoFlyActive = true;
                _flightMode = Constants.FLIGHT_MODE_CRUISE;
                _currentPhase = Constants.PHASE_CRUISE;
                _taskIssued = true;

                int altitudeFeet = (int)(_targetAltitude * Constants.METERS_TO_FEET);
                int headingInt = (int)_targetHeading;
                string headingName = SpatialCalculator.GetDirectionFromHeading(_targetHeading);

                _audio.Speak($"Cruise mode active, altitude {altitudeFeet} feet, heading {headingInt} degrees {headingName}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.StartCruise");
                _audio.Speak("Failed to start cruise mode");
            }
        }

        /// <summary>
        /// Start cruise mode with specific altitude and heading
        /// </summary>
        public void StartCruise(float altitude, float heading)
        {
            Ped player = Game.Player.Character;
            Vehicle aircraft = player.CurrentVehicle;

            if (!ValidateAircraft(player, aircraft))
                return;

            Stop(false);

            try
            {
                _aircraftType = GetAircraftType(aircraft);
                _lastAircraftHandle = aircraft.Handle;
                _targetAltitude = altitude;
                _targetHeading = heading;
                _initialAltitude = aircraft.Position.Z;

                // Set appropriate speed for aircraft type
                if (_aircraftType == Constants.AIRCRAFT_TYPE_BLIMP)
                {
                    _targetSpeed = Constants.AUTOFLY_BLIMP_DEFAULT_SPEED;
                }
                else if (_aircraftType == Constants.AIRCRAFT_TYPE_HELICOPTER ||
                    _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_HOVER)
                {
                    _targetSpeed = Constants.AUTOFLY_HELI_DEFAULT_SPEED;
                }
                else
                {
                    _targetSpeed = Constants.AUTOFLY_DEFAULT_SPEED;
                }

                IssueCruiseTask(player, aircraft);

                _autoFlyActive = true;
                _flightMode = Constants.FLIGHT_MODE_CRUISE;
                _currentPhase = Constants.PHASE_CRUISE;
                _taskIssued = true;

                int altitudeFeet = (int)(_targetAltitude * Constants.METERS_TO_FEET);
                int headingInt = (int)_targetHeading;
                string headingName = SpatialCalculator.GetDirectionFromHeading(_targetHeading);

                _audio.Speak($"Cruise mode active, target altitude {altitudeFeet} feet, heading {headingInt} degrees {headingName}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.StartCruise(altitude,heading)");
                _audio.Speak("Failed to start cruise mode");
            }
        }

        /// <summary>
        /// Start waypoint navigation - fly to GPS waypoint, then circle
        /// </summary>
        public void StartWaypoint()
        {
            Ped player = Game.Player.Character;
            Vehicle aircraft = player.CurrentVehicle;

            if (!ValidateAircraft(player, aircraft))
                return;

            // Check if waypoint is set
            if (!Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
            {
                _audio.Speak("No waypoint set. Set a waypoint on the map first.");
                return;
            }

            Stop(false);

            try
            {
                _aircraftType = GetAircraftType(aircraft);
                _lastAircraftHandle = aircraft.Handle;

                // Get waypoint position (X, Y only - we'll maintain current altitude)
                Vector3 waypointPos = World.WaypointPosition;
                _destinationPos = new Vector3(waypointPos.X, waypointPos.Y, aircraft.Position.Z);
                _destinationName = "GPS Waypoint";
                _runwayHeading = -1f;
                _isHelipad = true;  // Treat waypoints like helipads (any heading OK)
                _circleModeActive = false;

                // Cache ground height at destination (avoid repeated lookups in Update)
                _destinationGroundZ = World.GetGroundHeight(new GTA.Math.Vector2(waypointPos.X, waypointPos.Y));
                if (_destinationGroundZ == 0)
                {
                    _destinationGroundZ = Constants.AUTOFLY_DEFAULT_GROUND_HEIGHT;  // Default fallback
                }
                _groundHeightCached = true;

                // Set appropriate speed
                if (_aircraftType == Constants.AIRCRAFT_TYPE_HELICOPTER ||
                    _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_HOVER)
                {
                    _targetSpeed = Constants.AUTOFLY_HELI_DEFAULT_SPEED;
                }
                else
                {
                    _targetSpeed = Constants.AUTOFLY_DEFAULT_SPEED;
                }

                _targetAltitude = aircraft.Position.Z;

                // Issue navigation task
                IssueNavigationTask(player, aircraft, _destinationPos);

                _autoFlyActive = true;
                _flightMode = Constants.FLIGHT_MODE_WAYPOINT;
                _currentPhase = Constants.PHASE_CRUISE;
                _taskIssued = true;

                // Initialize distance tracking
                _lastDistanceToDestination = (aircraft.Position - _destinationPos).Length();

                // Announce start
                float distanceMiles = _lastDistanceToDestination * Constants.METERS_TO_MILES;
                string direction = SpatialCalculator.GetDirectionTo(aircraft.Position, _destinationPos);
                _audio.Speak($"Flying to waypoint, {distanceMiles:F1} miles {direction}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.StartWaypoint");
                _audio.Speak("Failed to start waypoint navigation");
            }
        }

        /// <summary>
        /// Land at the current location
        /// For helicopters: Lands directly below current position
        /// For fixed-wing: Descends and announces manual landing required
        /// </summary>
        public void LandHere()
        {
            Ped player = Game.Player.Character;
            Vehicle aircraft = player.CurrentVehicle;

            if (!ValidateAircraft(player, aircraft))
                return;

            Stop(false);

            try
            {
                _aircraftType = GetAircraftType(aircraft);
                _lastAircraftHandle = aircraft.Handle;

                Vector3 currentPos = aircraft.Position;

                // Get ground height at current location
                float groundZ = World.GetGroundHeight(new GTA.Math.Vector2(currentPos.X, currentPos.Y));
                if (groundZ == 0)
                {
                    // Fallback if ground height not available
                    groundZ = currentPos.Z - aircraft.HeightAboveGround;
                }

                _destinationPos = new Vector3(currentPos.X, currentPos.Y, groundZ);
                _destinationName = "current location";
                _isHelipad = true;  // Treat as helipad for landing logic
                _runwayHeading = -1;

                if (_aircraftType == Constants.AIRCRAFT_TYPE_HELICOPTER ||
                    _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_HOVER)
                {
                    // Helicopter can land anywhere - issue landing task
                    _targetSpeed = Constants.AUTOFLY_HELI_APPROACH_SPEED;  // Slow approach
                    _targetAltitude = groundZ + 5f;

                    // Issue helicopter land task - TASK_HELI_MISSION with MISSION_LAND
                    // Per FiveM example: TaskHeliMission(pilot, heli, 0, 0, x, y, z, 19, speed, -1, -1, -1, -1, -1, 96)
                    Function.Call(
                        (Hash)Constants.NATIVE_TASK_HELI_MISSION,
                        player.Handle, aircraft.Handle,
                        0, 0,  // No target vehicle/ped
                        currentPos.X, currentPos.Y, groundZ,
                        Constants.MISSION_LAND,  // Mission type 19 = LAND
                        _targetSpeed,
                        -1f,   // radius (-1 = default)
                        -1f,   // heading (-1 = any, will use current)
                        -1f,   // height (-1 = default)
                        -1f,   // minHeight (-1 = default)
                        -1f,   // slowDist (-1 = default)
                        Constants.HELI_FLAG_LAND_NO_AVOIDANCE);  // 96 = LandOnArrival + DontDoAvoidance

                    _autoFlyActive = true;
                    _flightMode = Constants.FLIGHT_MODE_DESTINATION;
                    _currentPhase = Constants.PHASE_FINAL;
                    _taskIssued = true;

                    float heightAboveGround = aircraft.HeightAboveGround;
                    int heightFeet = (int)(heightAboveGround * Constants.METERS_TO_FEET);
                    _audio.Speak($"Landing here, {heightFeet} feet above ground");
                }
                else if (_aircraftType == Constants.AIRCRAFT_TYPE_BLIMP)
                {
                    // Blimps can't autoland
                    _audio.Speak("Blimps require manual landing. Reducing speed for manual control.");
                    _targetSpeed = Constants.AUTOFLY_BLIMP_MIN_SPEED;

                    // Just slow down, player takes manual control
                    Function.Call(
                        (Hash)Constants.NATIVE_SET_DRIVE_TASK_CRUISE_SPEED,
                        player.Handle, _targetSpeed);
                }
                else
                {
                    // Fixed-wing aircraft - can't land without a runway
                    // Descend to low altitude and announce manual landing
                    _audio.Speak("Fixed-wing aircraft require a runway. Descending for manual landing.");

                    // Set up a slow descent
                    _targetAltitude = groundZ + Constants.AUTOFLY_DESCEND_AGL;  // ~1000 feet AGL
                    _targetSpeed = Constants.AUTOFLY_FINAL_SPEED;

                    // Issue task to fly to lower altitude at current position
                    Vector3 descendPos = new Vector3(currentPos.X + Constants.AUTOFLY_DESCEND_OFFSET, currentPos.Y, _targetAltitude);

                    // TASK_PLANE_MISSION: 14 parameters
                    Function.Call(
                        (Hash)Constants.NATIVE_TASK_PLANE_MISSION,
                        player.Handle, aircraft.Handle,
                        0, 0,  // No target vehicle/ped
                        descendPos.X, descendPos.Y, _targetAltitude,
                        Constants.MISSION_CRUISE,
                        _targetSpeed,
                        Constants.AUTOFLY_TASK_REACH_DISTANCE,  // targetReachedDist
                        aircraft.Heading,
                        (int)_targetAltitude,  // iFlightHeight - must be INT
                        (int)Constants.AUTOFLY_MIN_TERRAIN_CLEARANCE,  // iMinHeightAboveTerrain - must be INT
                        true);  // bPrecise

                    _autoFlyActive = true;
                    _flightMode = Constants.FLIGHT_MODE_CRUISE;
                    _currentPhase = Constants.PHASE_CRUISE;
                    _taskIssued = true;

                    // Deploy landing gear
                    DeployLandingGear(aircraft);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.LandHere");
                _audio.Speak("Failed to initiate landing");
            }
        }

        /// <summary>
        /// Start destination flight with autoland
        /// </summary>
        internal void StartDestination(AircraftLandingMenu.LandingDestination destination)
        {
            Ped player = Game.Player.Character;
            Vehicle aircraft = player.CurrentVehicle;

            if (!ValidateAircraft(player, aircraft))
                return;

            if (destination == null)
            {
                _audio.Speak("No destination selected");
                return;
            }

            Stop(false);

            try
            {
                _aircraftType = GetAircraftType(aircraft);
                _lastAircraftHandle = aircraft.Handle;

                // Store destination info
                _destinationPos = destination.Position;
                _runwayEndPos = destination.RunwayEndPosition;
                _runwayHeading = destination.RunwayHeading;
                _isHelipad = destination.IsHelipad;
                _destinationName = destination.Name;

                // Set speed based on aircraft type
                if (_aircraftType == Constants.AIRCRAFT_TYPE_HELICOPTER ||
                    _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_HOVER)
                {
                    _targetSpeed = Constants.AUTOFLY_HELI_DEFAULT_SPEED;
                }
                else
                {
                    _targetSpeed = Constants.AUTOFLY_DEFAULT_SPEED;
                }

                _targetAltitude = aircraft.Position.Z;
                _gearDeployed = false;
                _gearDeployAnnounced = false;

                // Issue navigation task to destination
                IssueNavigationTask(player, aircraft, _destinationPos);

                _autoFlyActive = true;
                _flightMode = Constants.FLIGHT_MODE_DESTINATION;
                _currentPhase = Constants.PHASE_CRUISE;
                _taskIssued = true;

                // Initialize distance tracking
                _lastDistanceToDestination = (aircraft.Position - _destinationPos).Length();

                // Announce start
                float distanceMiles = _lastDistanceToDestination * Constants.METERS_TO_MILES;
                string direction = SpatialCalculator.GetDirectionTo(aircraft.Position, _destinationPos);

                string announcement = $"Flying to {_destinationName}, {distanceMiles:F1} miles {direction}";
                if (!_isHelipad && _runwayHeading >= 0)
                {
                    int runwayNumber = (int)Math.Round(_runwayHeading / 10f);
                    if (runwayNumber == 0) runwayNumber = 36;
                    announcement += $", runway {runwayNumber}";
                }

                _audio.Speak(announcement);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.StartDestination");
                _audio.Speak("Failed to start destination flight");
            }
        }

        /// <summary>
        /// Stop all autofly operations
        /// </summary>
        public void Stop(bool announce = true)
        {
            if (_autoFlyActive || _taskIssued)
            {
                try
                {
                    Ped player = Game.Player.Character;
                    Function.Call((Hash)Constants.NATIVE_CLEAR_PED_TASKS, player.Handle);
                }
                catch { /* Expected during stop - player state may be invalid */ }
            }

            ResetState();

            if (announce)
            {
                _audio.Speak("AutoFly stopped");
            }
        }

        /// <summary>
        /// Pause autofly
        /// </summary>
        public void Pause()
        {
            if (!_autoFlyActive || _pauseState != Constants.PAUSE_STATE_NONE)
                return;

            try
            {
                Ped player = Game.Player.Character;

                // Save state before pause
                _prePauseSpeed = _targetSpeed;
                _prePauseMode = _flightMode;

                // Clear current task - aircraft will maintain momentum
                Function.Call((Hash)Constants.NATIVE_CLEAR_PED_TASKS, player.Handle);

                _pauseState = Constants.PAUSE_STATE_PAUSED;
                _taskIssued = false;

                _audio.Speak("AutoFly paused");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.Pause");
            }
        }

        /// <summary>
        /// Resume autofly from pause
        /// </summary>
        public void Resume()
        {
            if (_pauseState != Constants.PAUSE_STATE_PAUSED)
                return;

            Ped player = Game.Player.Character;
            Vehicle aircraft = player.CurrentVehicle;

            if (!ValidateAircraft(player, aircraft))
            {
                Stop();
                return;
            }

            try
            {
                _pauseState = Constants.PAUSE_STATE_RESUMING;
                _targetSpeed = _prePauseSpeed;

                // Re-issue appropriate task based on mode
                switch (_prePauseMode)
                {
                    case Constants.FLIGHT_MODE_CRUISE:
                        IssueCruiseTask(player, aircraft);
                        break;

                    case Constants.FLIGHT_MODE_WAYPOINT:
                        // Check if waypoint still exists
                        if (Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
                        {
                            Vector3 waypointPos = World.WaypointPosition;
                            _destinationPos = new Vector3(waypointPos.X, waypointPos.Y, aircraft.Position.Z);
                            IssueNavigationTask(player, aircraft, _destinationPos);
                        }
                        else
                        {
                            _audio.Speak("Waypoint removed, switching to cruise mode");
                            _flightMode = Constants.FLIGHT_MODE_CRUISE;
                            IssueCruiseTask(player, aircraft);
                        }
                        break;

                    case Constants.FLIGHT_MODE_DESTINATION:
                        // Resume navigation to destination
                        IssueNavigationTask(player, aircraft, _destinationPos);
                        break;
                }

                _taskIssued = true;
                _pauseState = Constants.PAUSE_STATE_NONE;

                _audio.Speak("AutoFly resumed");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.Resume");
                Stop();
            }
        }

        #endregion

        #region Public Methods - Speed/Altitude Control

        /// <summary>
        /// Increase target altitude by 500 feet
        /// </summary>
        public void IncreaseAltitude()
        {
            Logger.Debug($"IncreaseAltitude called, _autoFlyActive={_autoFlyActive}");

            if (!_autoFlyActive)
            {
                Logger.Debug("IncreaseAltitude: AutoFly not active, returning");
                return;
            }

            float oldAltitude = _targetAltitude;
            float newAltitude = Math.Min(_targetAltitude + Constants.AUTOFLY_ALTITUDE_INCREMENT,
                                         Constants.AUTOFLY_MAX_ALTITUDE);

            Logger.Debug($"IncreaseAltitude: old={oldAltitude}, new={newAltitude}, max={Constants.AUTOFLY_MAX_ALTITUDE}");

            if (Math.Abs(newAltitude - _targetAltitude) < 1f)
            {
                _audio.Speak("Maximum altitude reached");
                return;
            }

            _targetAltitude = newAltitude;

            // Update the task with new altitude
            Logger.Info($"IncreaseAltitude: Updating flight task with new altitude {_targetAltitude}m ({(int)(_targetAltitude * Constants.METERS_TO_FEET)}ft)");
            UpdateFlightAltitude();

            int altitudeFeet = (int)(_targetAltitude * Constants.METERS_TO_FEET);
            _audio.Speak($"Target altitude {altitudeFeet} feet");
        }

        /// <summary>
        /// Decrease target altitude by 500 feet
        /// </summary>
        public void DecreaseAltitude()
        {
            Logger.Debug($"DecreaseAltitude called, _autoFlyActive={_autoFlyActive}");

            if (!_autoFlyActive)
            {
                Logger.Debug("DecreaseAltitude: AutoFly not active, returning");
                return;
            }

            float oldAltitude = _targetAltitude;
            float newAltitude = Math.Max(_targetAltitude - Constants.AUTOFLY_ALTITUDE_INCREMENT,
                                         Constants.AUTOFLY_MIN_ALTITUDE);

            Logger.Debug($"DecreaseAltitude: old={oldAltitude}, new={newAltitude}, min={Constants.AUTOFLY_MIN_ALTITUDE}");

            if (Math.Abs(newAltitude - _targetAltitude) < 1f)
            {
                _audio.Speak("Minimum altitude reached");
                return;
            }

            _targetAltitude = newAltitude;

            Logger.Info($"DecreaseAltitude: Updating flight task with new altitude {_targetAltitude}m ({(int)(_targetAltitude * Constants.METERS_TO_FEET)}ft)");
            UpdateFlightAltitude();

            int altitudeFeet = (int)(_targetAltitude * Constants.METERS_TO_FEET);
            _audio.Speak($"Target altitude {altitudeFeet} feet");
        }

        /// <summary>
        /// Increase speed by fixed increment (user-initiated speed change)
        /// </summary>
        public void IncreaseSpeed()
        {
            if (!_autoFlyActive) return;

            float maxSafeSpeed = CalculateMaxSafeSpeed();
            // Use fixed increment for user-initiated speed changes (more predictable behavior)
            float newSpeed = Math.Min(_targetSpeed + Constants.AUTOFLY_SPEED_INCREMENT, maxSafeSpeed);

            if (Math.Abs(newSpeed - _targetSpeed) < 0.1f)
            {
                _audio.Speak("Maximum speed reached");
                return;
            }

            ApplySmoothSpeedChange(newSpeed);

            int mph = (int)(newSpeed * Constants.METERS_PER_SECOND_TO_MPH);
            _audio.Speak($"Speed {mph} miles per hour");
        }

        /// <summary>
        /// Decrease speed by fixed increment (user-initiated speed change)
        /// </summary>
        public void DecreaseSpeed()
        {
            if (!_autoFlyActive) return;

            // Calculate safe speed decrease
            float minSafeSpeed = CalculateMinSafeSpeed();
            // Use fixed decrement for user-initiated speed changes (more predictable behavior)
            float proposedSpeed = _targetSpeed - Constants.AUTOFLY_SPEED_INCREMENT;

            float newSpeed = Math.Max(proposedSpeed, minSafeSpeed);

            if (Math.Abs(newSpeed - _targetSpeed) < 0.1f)
            {
                _audio.Speak("Minimum safe speed reached");
                return;
            }

            // Apply smooth deceleration
            ApplySmoothSpeedChange(newSpeed);

            int mph = (int)(newSpeed * Constants.METERS_PER_SECOND_TO_MPH);
            _audio.Speak($"Speed {mph} miles per hour");
        }

        /// <summary>
        /// Announce current status
        /// </summary>
        public void AnnounceStatus()
        {
            if (!_autoFlyActive)
            {
                _audio.Speak("AutoFly inactive");
                return;
            }

            Ped player = Game.Player.Character;
            Vehicle aircraft = player.CurrentVehicle;

            // Use safe bounds-checked accessors to prevent IndexOutOfRangeException
            string modeName = Constants.GetFlightModeName(_flightMode);
            string phaseName = Constants.GetFlightPhaseName(_currentPhase);

            string status = $"{modeName} mode, {phaseName}";

            if (aircraft != null)
            {
                int altitudeFeet = (int)(aircraft.Position.Z * Constants.METERS_TO_FEET);
                int speedMph = (int)(aircraft.Speed * Constants.METERS_PER_SECOND_TO_MPH);
                status += $", altitude {altitudeFeet} feet, speed {speedMph} miles per hour";
            }

            if (_flightMode == Constants.FLIGHT_MODE_DESTINATION ||
                _flightMode == Constants.FLIGHT_MODE_WAYPOINT)
            {
                if (aircraft != null)
                {
                    float distance = (aircraft.Position - _destinationPos).Length();
                    float distanceMiles = distance * Constants.METERS_TO_MILES;

                    if (distanceMiles >= 0.1f)
                        status += $", {distanceMiles:F1} miles to destination";
                    else
                    {
                        int feet = (int)(distance * Constants.METERS_TO_FEET);
                        status += $", {feet} feet to destination";
                    }
                }
            }

            _audio.Speak(status);
        }

        #endregion

        #region Update Method (Called from OnTick)

        /// <summary>
        /// Update autofly state - called from OnTick with throttling
        /// </summary>
        public void Update(Vehicle aircraft, Vector3 position, long currentTick)
        {
            if (!_autoFlyActive)
                return;

            // Handle paused state
            if (_pauseState == Constants.PAUSE_STATE_PAUSED)
                return;

            // CRITICAL: Null and Exists check for aircraft to prevent NullReferenceException
            // This can happen if aircraft is destroyed between IsInVehicle check and Update call
            if (aircraft == null || !aircraft.Exists())
            {
                Logger.Warning("AutoFlyManager.Update: aircraft is null or doesn't exist, stopping");
                Stop(false);
                return;
            }

            // Defensive: Validate position (guard against NaN/Infinity)
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
                float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
            {
                Logger.Warning("AutoFlyManager.Update: invalid position, skipping update");
                return;
            }

            // Defensive: Guard against invalid tick values
            if (currentTick < 0)
                return;

            // Validate still in aircraft as pilot
            Ped player = Game.Player.Character;
            if (player == null || !player.IsInVehicle() || player.SeatIndex != VehicleSeat.Driver)
            {
                _audio.Speak("Left pilot seat, AutoFly stopping");
                Stop(false);
                return;
            }

            // Check if aircraft changed (compare by Handle to avoid SHVDN wrapper issues)
            if (aircraft.Handle != _lastAircraftHandle)
            {
                _audio.Speak("Aircraft changed, AutoFly stopping");
                Stop(false);
                return;
            }

            // Check aircraft health - use try/catch since accessing IsDead/Health on destroyed entity can throw
            try
            {
                if (aircraft.IsDead || aircraft.Health <= 0)
                {
                    _audio.Speak("Aircraft destroyed, AutoFly stopping");
                    Stop(false);
                    return;
                }
            }
            catch (Exception)
            {
                // Entity no longer valid
                Logger.Warning("AutoFlyManager.Update: aircraft entity invalid, stopping");
                Stop(false);
                return;
            }

            // Check for VTOL mode change (player may toggle during flight)
            if (Constants.VTOL_VEHICLE_HASHES.Contains(aircraft.Model.Hash))
            {
                int currentVtolType = GetAircraftType(aircraft);
                if (currentVtolType != _aircraftType)
                {
                    int previousType = _aircraftType;
                    _aircraftType = currentVtolType;

                    // Announce mode change and re-issue task with appropriate parameters
                    string modeName = currentVtolType == Constants.AIRCRAFT_TYPE_VTOL_HOVER
                        ? "hover mode" : "plane mode";
                    _audio.Speak($"VTOL {modeName}");

                    // Re-issue the current task with the new aircraft type's parameters
                    try
                    {
                        switch (_flightMode)
                        {
                            case Constants.FLIGHT_MODE_CRUISE:
                                IssueCruiseTask(player, aircraft);
                                break;
                            case Constants.FLIGHT_MODE_WAYPOINT:
                            case Constants.FLIGHT_MODE_DESTINATION:
                                IssueNavigationTask(player, aircraft, _destinationPos);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "AutoFlyManager.VTOLModeChange");
                    }
                }
            }

            // Stuck detection (Grok optimization) - check if aircraft is making progress
            // Only apply to waypoint/destination modes where we have a target
            if (_flightMode == Constants.FLIGHT_MODE_WAYPOINT || _flightMode == Constants.FLIGHT_MODE_DESTINATION)
            {
                float currentDistance = (position - _destinationPos).Length();
                float currentAltitude = position.Z;

                // Check if making progress (closer to destination OR altitude changing significantly)
                bool isProgressing = currentDistance < _lastProgressDistance - 5f ||  // Getting 5m+ closer
                                    Math.Abs(currentAltitude - _lastProgressAltitude) > 10f;  // Altitude change

                if (isProgressing)
                {
                    _flightStuckCounter = 0;
                    _lastProgressDistance = currentDistance;
                    _lastProgressAltitude = currentAltitude;
                }
                else
                {
                    _flightStuckCounter++;

                    // Stuck threshold reached - announce and attempt recovery
                    if (_flightStuckCounter >= Constants.FLIGHT_STUCK_THRESHOLD)
                    {
                        Logger.Warning($"AutoFlyManager: Flight stuck detected after {_flightStuckCounter} checks");
                        _audio.Speak("Flight stuck, attempting recovery");

                        // Re-issue the navigation task to attempt unsticking
                        try
                        {
                            Ped pilot = Game.Player.Character;
                            IssueNavigationTask(pilot, aircraft, _destinationPos);
                            _flightStuckCounter = 0;  // Reset counter after recovery attempt
                        }
                        catch (Exception ex)
                        {
                            Logger.Exception(ex, "AutoFlyManager.StuckRecovery");
                        }
                    }
                }
            }

            // Update based on flight mode
            switch (_flightMode)
            {
                case Constants.FLIGHT_MODE_CRUISE:
                    UpdateCruise(aircraft, position, currentTick);
                    break;

                case Constants.FLIGHT_MODE_WAYPOINT:
                    UpdateWaypoint(aircraft, position, currentTick);
                    break;

                case Constants.FLIGHT_MODE_DESTINATION:
                    UpdateDestination(aircraft, position, currentTick);
                    break;
            }
        }

        #endregion

        #region Private Methods - Mode Updates

        private void UpdateCruise(Vehicle aircraft, Vector3 position, long currentTick)
        {
            // Throttle cruise corrections to every 5 seconds max
            if (currentTick - _lastCruiseCorrectionTick < 50_000_000)
                return;

            // Monitor for significant drift from target altitude/heading
            float altitudeDrift = Math.Abs(position.Z - _targetAltitude);
            float headingDrift = Math.Abs(NormalizeAngleDiff(aircraft.Heading - _targetHeading));

            // If drifted significantly, re-issue the cruise task to correct
            // This handles cases where AI task may have completed or been interrupted
            if (altitudeDrift > Constants.CRUISE_ALTITUDE_TOLERANCE * 2 ||
                headingDrift > Constants.CRUISE_HEADING_TOLERANCE * 5)
            {
                _lastCruiseCorrectionTick = currentTick;
                try
                {
                    Ped player = Game.Player.Character;
                    IssueCruiseTask(player, aircraft);
                    Logger.Debug($"Cruise correction: altitude drift {altitudeDrift:F0}m, heading drift {headingDrift:F0}°");
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "UpdateCruise.Correction");
                }
            }
        }

        private void UpdateWaypoint(Vehicle aircraft, Vector3 position, long currentTick)
        {
            // Check if GPS waypoint has changed (player may have moved it)
            if (Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
            {
                Vector3 currentWaypoint = World.WaypointPosition;
                // Only update if waypoint moved significantly (more than 50m)
                float waypointDelta = Math.Abs(currentWaypoint.X - _destinationPos.X) +
                                     Math.Abs(currentWaypoint.Y - _destinationPos.Y);
                if (waypointDelta > Constants.WAYPOINT_MOVED_THRESHOLD)
                {
                    // Waypoint moved - update destination and re-issue task
                    _destinationPos = new Vector3(currentWaypoint.X, currentWaypoint.Y, _targetAltitude);
                    _destinationGroundZ = World.GetGroundHeight(new GTA.Math.Vector2(currentWaypoint.X, currentWaypoint.Y));
                    if (_destinationGroundZ == 0) _destinationGroundZ = Constants.AUTOFLY_DEFAULT_GROUND_HEIGHT;

                    try
                    {
                        Ped player = Game.Player.Character;
                        IssueNavigationTask(player, aircraft, _destinationPos);
                        _audio.Speak("Waypoint updated, adjusting course");
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "UpdateWaypoint.WaypointChanged");
                    }
                }
            }
            else
            {
                // Waypoint was removed - switch to cruise mode
                _audio.Speak("Waypoint removed, switching to cruise mode");
                _flightMode = Constants.FLIGHT_MODE_CRUISE;
                _currentPhase = Constants.PHASE_CRUISE;
                _targetHeading = aircraft.Heading;
                try
                {
                    Ped player = Game.Player.Character;
                    IssueCruiseTask(player, aircraft);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "UpdateWaypoint.SwitchToCruise");
                }
                return;
            }

            float distance = (_destinationPos - position).Length();

            // Announce distance milestones
            AnnounceDistanceMilestones(distance, currentTick);

            // Use cached ground height (calculated once in StartWaypoint)
            float groundZ = _groundHeightCached ? _destinationGroundZ : Constants.AUTOFLY_DEFAULT_GROUND_HEIGHT;

            // Approach phase - begin descent when within 2 miles
            if (distance < Constants.AUTOFLY_APPROACH_DISTANCE && _currentPhase == Constants.PHASE_CRUISE)
            {
                _currentPhase = Constants.PHASE_APPROACH;

                // Calculate descent target - ~1500 feet AGL for circling
                float circleAltitude = groundZ + Constants.AUTOFLY_CIRCLE_APPROACH_AGL;  // ~1500 feet AGL

                if (_targetAltitude > circleAltitude)
                {
                    _targetAltitude = circleAltitude;
                    _audio.Speak("Approaching waypoint, descending");

                    // Update the flight task with new altitude
                    try
                    {
                        Ped player = Game.Player.Character;
                        IssueNavigationTask(player, aircraft, _destinationPos);
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "UpdateWaypoint.Descent");
                    }
                }
            }

            // Final approach - lower altitude further when within half mile
            if (distance < Constants.AUTOFLY_FINAL_DISTANCE && _currentPhase == Constants.PHASE_APPROACH)
            {
                _currentPhase = Constants.PHASE_FINAL;

                // Lower to ~500 feet AGL for final circling
                float finalAltitude = groundZ + Constants.AUTOFLY_CIRCLE_FINAL_AGL;  // ~500 feet AGL

                if (_targetAltitude > finalAltitude)
                {
                    _targetAltitude = finalAltitude;
                    _targetSpeed = Constants.AUTOFLY_APPROACH_SPEED;  // Slow down

                    _audio.Speak("Final approach to waypoint");

                    try
                    {
                        Ped player = Game.Player.Character;
                        IssueNavigationTask(player, aircraft, _destinationPos);
                        UpdateFlightSpeed();
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "UpdateWaypoint.FinalApproach");
                    }
                }
            }

            // Check if arrived at waypoint
            if (distance < Constants.AUTOFLY_ARRIVAL_RADIUS * 2)
            {
                if (!_circleModeActive)
                {
                    _circleModeActive = true;
                    _targetAltitude = groundZ + Constants.AUTOFLY_CIRCLE_ARRIVED_AGL;  // ~300 feet AGL for circling

                    _audio.Speak("Arrived at waypoint, circling");

                    // Switch to circle mode
                    try
                    {
                        Ped player = Game.Player.Character;
                        IssueCircleTask(player, aircraft, _destinationPos);
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "UpdateWaypoint.Circle");
                    }
                }
            }

            _lastDistanceToDestination = distance;
        }

        private void UpdateDestination(Vehicle aircraft, Vector3 position, long currentTick)
        {
            float distance = (_destinationPos - position).Length();

            // Announce distance milestones
            AnnounceDistanceMilestones(distance, currentTick);

            switch (_currentPhase)
            {
                case Constants.PHASE_CRUISE:
                    // Check for transition to APPROACH
                    if (distance < Constants.AUTOFLY_APPROACH_DISTANCE)
                    {
                        TransitionToApproach(aircraft, position);
                    }
                    break;

                case Constants.PHASE_APPROACH:
                    // Announce approach guidance
                    AnnounceApproachGuidance(aircraft, position, currentTick);

                    // Check landing gear deployment (fixed-wing)
                    CheckLandingGearDeployment(aircraft, position, distance);

                    // Check for transition to FINAL
                    if (distance < Constants.AUTOFLY_FINAL_DISTANCE)
                    {
                        TransitionToFinal(aircraft, position);
                    }
                    break;

                case Constants.PHASE_FINAL:
                    // Check for issuing landing task
                    if (distance < Constants.AUTOFLY_TOUCHDOWN_DISTANCE)
                    {
                        IssueLandingTask(aircraft, position);
                    }
                    break;

                case Constants.PHASE_TOUCHDOWN:
                    // Monitor landing progress
                    CheckTouchdown(aircraft, position, currentTick);
                    break;

                case Constants.PHASE_TAXIING:
                    // Monitor taxi completion
                    CheckTaxiing(aircraft, currentTick);
                    break;

                case Constants.PHASE_LANDED:
                    // Flight complete - nothing to do
                    break;
            }

            _lastDistanceToDestination = distance;
        }

        #endregion

        #region Private Methods - Phase Transitions

        private void TransitionToApproach(Vehicle aircraft, Vector3 position)
        {
            _currentPhase = Constants.PHASE_APPROACH;

            // Store approach start altitude for glideslope calculation
            _approachStartAltitude = position.Z;
            _lastHeightAboveGround = aircraft.HeightAboveGround;

            // Calculate glideslope target altitude at destination
            // Standard 3-degree glideslope: altitude = distance * tan(3°) ≈ distance * 0.0524
            float distance = (_destinationPos - position).Length();
            float destinationGroundZ = _destinationPos.Z;
            _glideslopeTargetAltitude = destinationGroundZ + (distance * 0.0524f);

            string announcement = "Beginning approach";
            if (!_isHelipad && _runwayHeading >= 0)
            {
                int runwayNumber = (int)Math.Round(_runwayHeading / 10f);
                if (runwayNumber == 0) runwayNumber = 36;
                announcement += $", runway {runwayNumber}";
            }

            _audio.Speak(announcement);

            // Reduce speed for approach
            _targetSpeed = Constants.AUTOFLY_APPROACH_SPEED;
            UpdateFlightSpeed();

            // Deploy landing gear early for fixed-wing aircraft (gives time to stabilize)
            if (!_gearDeployed &&
                (_aircraftType == Constants.AIRCRAFT_TYPE_FIXED_WING ||
                 _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_PLANE))
            {
                DeployLandingGear(aircraft);
            }
        }

        private void TransitionToFinal(Vehicle aircraft, Vector3 position)
        {
            _currentPhase = Constants.PHASE_FINAL;
            _audio.Speak("Final approach");

            // Further reduce speed
            _targetSpeed = Constants.AUTOFLY_FINAL_SPEED;
            UpdateFlightSpeed();

            // Deploy gear for fixed-wing if not already deployed
            if (!_gearDeployed &&
                (_aircraftType == Constants.AIRCRAFT_TYPE_FIXED_WING ||
                 _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_PLANE))
            {
                DeployLandingGear(aircraft);
            }
        }

        private void IssueLandingTask(Vehicle aircraft, Vector3 position)
        {
            _currentPhase = Constants.PHASE_TOUCHDOWN;

            try
            {
                Ped player = Game.Player.Character;

                if (_aircraftType == Constants.AIRCRAFT_TYPE_BLIMP)
                {
                    // Blimps cannot autoland - circle over destination instead
                    IssueCircleTask(player, aircraft, _destinationPos);
                    _currentPhase = Constants.PHASE_CRUISE;  // Stay in cruise (circling)
                    _audio.Speak($"Arrived at {_destinationName}, circling. Blimps require manual landing");
                    return;
                }
                else if (_aircraftType == Constants.AIRCRAFT_TYPE_FIXED_WING ||
                    _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_PLANE)
                {
                    // Fixed-wing: Use TASK_PLANE_LAND with runway start/end
                    // TASK_PLANE_LAND: pilot, plane, runwayStartX/Y/Z, runwayEndX/Y/Z
                    Function.Call(
                        (Hash)Constants.NATIVE_TASK_PLANE_LAND,
                        player.Handle, aircraft.Handle,
                        _destinationPos.X, _destinationPos.Y, _destinationPos.Z,
                        _runwayEndPos.X, _runwayEndPos.Y, _runwayEndPos.Z);

                    _audio.Speak("Landing");
                }
                else
                {
                    // Helicopter/VTOL hover: Use TASK_HELI_MISSION with MISSION_LAND and landing flags
                    // Per FiveM example: TaskHeliMission(pilot, heli, 0, 0, x, y, z, 19, speed, -1, -1, -1, -1, -1, 96)
                    Function.Call(
                        (Hash)Constants.NATIVE_TASK_HELI_MISSION,
                        player.Handle, aircraft.Handle,
                        0, 0,  // No target vehicle/ped
                        _destinationPos.X, _destinationPos.Y, _destinationPos.Z,
                        Constants.MISSION_LAND,  // Mission type 19 = LAND
                        Constants.HELI_LANDING_SPEED,
                        -1f,   // radius (-1 = default)
                        -1f,   // heading (-1 = any)
                        -1f,   // height (-1 = default)
                        -1f,   // minHeight (-1 = default)
                        -1f,   // slowDist (-1 = default)
                        Constants.HELI_FLAG_LAND_NO_AVOIDANCE);  // 96 = LandOnArrival + DontDoAvoidance

                    _audio.Speak("Landing");
                }

                _taskIssued = true;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.IssueLandingTask");
                _audio.Speak("Landing task failed");
            }
        }

        private void CheckTouchdown(Vehicle aircraft, Vector3 position, long currentTick)
        {
            // Initialize landing phase timer if not set
            if (_landingPhaseStartTick == 0)
            {
                _landingPhaseStartTick = currentTick;
            }

            // Check for landing phase timeout (prevents stuck state)
            if (currentTick - _landingPhaseStartTick > LANDING_PHASE_TIMEOUT)
            {
                Logger.Warning("AutoFlyManager: Landing phase timeout, stopping");
                _audio.Speak("Landing timeout, stopping AutoFly");
                Stop(false);
                return;
            }

            // Check if on ground and slow enough
            float heightAboveGround = aircraft.HeightAboveGround;
            float speed = aircraft.Speed;

            // Track height for rate of descent monitoring
            float descentRate = _lastHeightAboveGround - heightAboveGround;
            _lastHeightAboveGround = heightAboveGround;

            // Use IsOnAllWheels for more reliable touchdown detection
            bool isOnAllWheels = false;
            try
            {
                isOnAllWheels = Function.Call<bool>((Hash)Constants.NATIVE_IS_VEHICLE_ON_ALL_WHEELS, aircraft.Handle);
            }
            catch
            {
                // Fallback if native fails - use height check
                isOnAllWheels = heightAboveGround < Constants.AUTOFLY_TOUCHDOWN_HEIGHT_WHEELS;
            }

            if (_aircraftType == Constants.AIRCRAFT_TYPE_FIXED_WING ||
                _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_PLANE)
            {
                // Fixed-wing: Need to be on wheels AND slow enough
                if (heightAboveGround < Constants.AUTOFLY_TOUCHDOWN_HEIGHT_PLANE && (isOnAllWheels || heightAboveGround < Constants.AUTOFLY_TOUCHDOWN_HEIGHT_WHEELS))
                {
                    _stableOnGroundCount++;

                    // Require 3 consecutive stable-on-ground checks before transitioning
                    if (_stableOnGroundCount >= Constants.AUTOFLY_TOUCHDOWN_STABLE_COUNT_PLANE && speed < Constants.AUTOFLY_TOUCHDOWN_SPEED)  // ~45 mph touchdown speed acceptable
                    {
                        _currentPhase = Constants.PHASE_TAXIING;
                        _landingPhaseStartTick = currentTick;  // Reset timer for taxiing phase
                        _stableOnGroundCount = 0;
                        _audio.Speak("Touchdown, taxiing");
                    }
                }
                else
                {
                    // Reset counter if we bounced
                    _stableOnGroundCount = 0;
                }
            }
            else
            {
                // Helicopter: More stringent requirements
                if (heightAboveGround < Constants.AUTOFLY_TOUCHDOWN_HEIGHT_HELI)
                {
                    _stableOnGroundCount++;

                    // Require 5 consecutive stable checks (helicopter settles slower)
                    if (_stableOnGroundCount >= Constants.AUTOFLY_TOUCHDOWN_STABLE_COUNT_HELI && speed < 1f)
                    {
                        _currentPhase = Constants.PHASE_LANDED;
                        _stableOnGroundCount = 0;
                        _audio.Speak($"Landed at {_destinationName}");
                        Stop(false);
                    }
                }
                else
                {
                    _stableOnGroundCount = 0;
                }
            }
        }

        private void CheckTaxiing(Vehicle aircraft, long currentTick)
        {
            // Check for taxiing phase timeout (prevents stuck state)
            if (_landingPhaseStartTick > 0 && currentTick - _landingPhaseStartTick > LANDING_PHASE_TIMEOUT)
            {
                Logger.Warning("AutoFlyManager: Taxiing phase timeout, stopping");
                _audio.Speak("Taxiing timeout, stopping AutoFly");
                Stop(false);
                return;
            }

            // Check if stopped
            if (aircraft.Speed < 1f)
            {
                _currentPhase = Constants.PHASE_LANDED;
                _audio.Speak($"Arrived at {_destinationName}");
                Stop(false);
            }
        }

        #endregion

        #region Private Methods - Task Issuance

        private void IssueCruiseTask(Ped player, Vehicle aircraft)
        {
            // Calculate a waypoint far ahead in the target heading direction
            // GTA V heading: 0° = North (+Y), 90° = East (+X in standard, but mirrored in GTA V)
            // GTA V coordinate system: heading increases clockwise, but X/Y are mirrored
            // Formula: X = sin(heading), Y = cos(heading) for GTA V's coordinate system
            float radians = _targetHeading * (float)Math.PI / 180f;
            Vector3 cruiseTarget = new Vector3(
                aircraft.Position.X + (float)Math.Sin(radians) * Constants.AUTOFLY_CRUISE_FAR_DISTANCE,  // 10km ahead
                aircraft.Position.Y + (float)Math.Cos(radians) * Constants.AUTOFLY_CRUISE_FAR_DISTANCE,
                _targetAltitude);

            if (_aircraftType == Constants.AIRCRAFT_TYPE_HELICOPTER ||
                _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_HOVER)
            {
                // Helicopter cruise - TASK_HELI_MISSION: 15 parameters
                // ped, heli, vehicleTarget, pedTarget, x, y, z, missionType, speed, radius, heading, height, minHeight, slowDist, missionFlags
                Function.Call(
                    (Hash)Constants.NATIVE_TASK_HELI_MISSION,
                    player.Handle, aircraft.Handle,
                    0, 0,  // No target vehicle/ped
                    cruiseTarget.X, cruiseTarget.Y, _targetAltitude,
                    Constants.MISSION_GOTO,
                    _targetSpeed,
                    Constants.AUTOFLY_HELI_CIRCLE_RADIUS,  // radius
                    _targetHeading,
                    _targetAltitude,  // height
                    Constants.AUTOFLY_MIN_TERRAIN_CLEARANCE,  // minHeight
                    -1f,  // slowDist
                    Constants.HELI_FLAG_MAINTAIN_HEIGHT_ABOVE_TERRAIN |
                    Constants.HELI_FLAG_START_ENGINE_IMMEDIATELY);
            }
            else
            {
                // Fixed-wing cruise - TASK_PLANE_MISSION: 14 parameters
                // ped, vehicle, targetVehicle, targetPed, x, y, z, missionType, speed, targetReachedDist, orientation, flightHeight (INT), minHeightAboveTerrain (INT), precise
                Function.Call(
                    (Hash)Constants.NATIVE_TASK_PLANE_MISSION,
                    player.Handle, aircraft.Handle,
                    0, 0,  // No target vehicle/ped
                    cruiseTarget.X, cruiseTarget.Y, _targetAltitude,
                    Constants.MISSION_CRUISE,
                    _targetSpeed,
                    Constants.AUTOFLY_TASK_REACH_DISTANCE,  // targetReachedDist
                    _targetHeading,
                    (int)_targetAltitude,  // iFlightHeight - must be INT
                    (int)Constants.AUTOFLY_MIN_TERRAIN_CLEARANCE,  // iMinHeightAboveTerrain - must be INT
                    true);  // bPrecise
            }
        }

        private void IssueNavigationTask(Ped player, Vehicle aircraft, Vector3 destination)
        {
            if (_aircraftType == Constants.AIRCRAFT_TYPE_HELICOPTER ||
                _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_HOVER)
            {
                // Helicopter navigation - TASK_HELI_MISSION: 15 parameters
                Function.Call(
                    (Hash)Constants.NATIVE_TASK_HELI_MISSION,
                    player.Handle, aircraft.Handle,
                    0, 0,  // No target vehicle/ped
                    destination.X, destination.Y, _targetAltitude,
                    Constants.MISSION_GOTO,
                    _targetSpeed,
                    Constants.AUTOFLY_HELI_TASK_RADIUS,   // radius
                    -1f,   // Any heading
                    _targetAltitude,  // height
                    Constants.AUTOFLY_MIN_TERRAIN_CLEARANCE,  // minHeight
                    -1f,   // slowDist
                    Constants.HELI_FLAG_MAINTAIN_HEIGHT_ABOVE_TERRAIN |
                    Constants.HELI_FLAG_START_ENGINE_IMMEDIATELY);
            }
            else
            {
                // Fixed-wing navigation - TASK_PLANE_MISSION: 14 parameters
                float heading = -1f;
                if (_runwayHeading >= 0)
                {
                    heading = _runwayHeading;
                }

                Function.Call(
                    (Hash)Constants.NATIVE_TASK_PLANE_MISSION,
                    player.Handle, aircraft.Handle,
                    0, 0,  // No target vehicle/ped
                    destination.X, destination.Y, _targetAltitude,
                    Constants.MISSION_GOTO,
                    _targetSpeed,
                    Constants.AUTOFLY_TASK_REACH_DISTANCE,  // targetReachedDist
                    heading,
                    (int)_targetAltitude,  // iFlightHeight - must be INT
                    (int)Constants.AUTOFLY_MIN_TERRAIN_CLEARANCE,  // iMinHeightAboveTerrain - must be INT
                    true);  // bPrecise
            }
        }

        private void IssueCircleTask(Ped player, Vehicle aircraft, Vector3 center)
        {
            if (_aircraftType == Constants.AIRCRAFT_TYPE_HELICOPTER ||
                _aircraftType == Constants.AIRCRAFT_TYPE_VTOL_HOVER)
            {
                // Helicopter circle - TASK_HELI_MISSION: 15 parameters
                Function.Call(
                    (Hash)Constants.NATIVE_TASK_HELI_MISSION,
                    player.Handle, aircraft.Handle,
                    0, 0,  // No target vehicle/ped
                    center.X, center.Y, _targetAltitude,
                    Constants.MISSION_CIRCLE,
                    _targetSpeed,
                    Constants.AUTOFLY_HELI_CIRCLE_RADIUS,  // Circle radius
                    -1f,   // Any heading
                    _targetAltitude,  // height
                    Constants.AUTOFLY_MIN_TERRAIN_CLEARANCE,  // minHeight
                    -1f,   // slowDist
                    Constants.HELI_FLAG_MAINTAIN_HEIGHT_ABOVE_TERRAIN);
            }
            else
            {
                // Fixed-wing circle - TASK_PLANE_MISSION: 14 parameters
                Function.Call(
                    (Hash)Constants.NATIVE_TASK_PLANE_MISSION,
                    player.Handle, aircraft.Handle,
                    0, 0,  // No target vehicle/ped
                    center.X, center.Y, _targetAltitude,
                    Constants.MISSION_CIRCLE,
                    _targetSpeed,
                    Constants.AUTOFLY_PLANE_CIRCLE_RADIUS,  // Larger circle for planes
                    -1f,   // Any heading
                    (int)_targetAltitude,  // iFlightHeight - must be INT
                    (int)Constants.AUTOFLY_MIN_TERRAIN_CLEARANCE,  // iMinHeightAboveTerrain - must be INT
                    true);  // bPrecise
            }

            _taskIssued = true;
        }

        #endregion

        #region Private Methods - Flight Control

        /// <summary>
        /// Get fully adjusted flight speed (Grok optimization)
        /// Applies weather multiplier, collision multiplier, and type caps for realistic flight
        /// </summary>
        private float GetAdjustedFlightSpeed(Vehicle aircraft = null)
        {
            long currentTick = DateTime.Now.Ticks;

            // Update weather state
            _weatherManager?.Update(currentTick, out _, out _);

            float weatherMult = _weatherManager?.SpeedMultiplier ?? 1.0f;

            // Check for nearby aircraft/obstacles (collision avoidance)
            float collisionMult = 1.0f;
            if (aircraft != null && _collisionDetector != null)
            {
                int followingState = _collisionDetector.CheckFollowingDistance(aircraft, currentTick);
                // Slow down if close to something ahead
                if (followingState >= 3) collisionMult = 0.7f;       // Close/dangerous
                else if (followingState >= 2) collisionMult = 0.85f; // Normal following
            }

            // Apply weather and collision multipliers
            float adjustedSpeed = _targetSpeed * weatherMult * collisionMult;

            // Apply aircraft type speed cap
            float typeCap = Constants.GetAircraftTypeSpeedCap(_aircraftType);
            adjustedSpeed = Math.Min(adjustedSpeed, typeCap);

            return adjustedSpeed;
        }

        private void UpdateFlightSpeed()
        {
            try
            {
                Ped player = Game.Player.Character;
                Vehicle aircraft = player.IsInVehicle() ? player.CurrentVehicle : null;
                float adjustedSpeed = GetAdjustedFlightSpeed(aircraft);
                Function.Call(
                    (Hash)Constants.NATIVE_SET_DRIVE_TASK_CRUISE_SPEED,
                    player.Handle, adjustedSpeed);  // Must use .Handle for native calls
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.UpdateFlightSpeed");
            }
        }

        /// <summary>
        /// Checks if the flight task needs to be re-issued (Grok optimization)
        /// Ultra-precise: requires ALL THREE conditions (distance deviation + heading deviation + low speed)
        /// Prevents jerky re-tasking that causes erratic flight behavior
        /// </summary>
        private bool NeedsFlightTaskReissue(Vehicle aircraft, Vector3 targetPosition)
        {
            if (aircraft == null || !aircraft.Exists())
                return false;

            float distanceToTarget = (aircraft.Position - targetPosition).Length();

            // Check distance deviation - only consider re-issue if significantly off course
            if (distanceToTarget <= Constants.TASK_DEVIATION_THRESHOLD)
                return false;

            // Check heading deviation
            float targetHeading = (float)SpatialCalculator.CalculateAngle(
                aircraft.Position.X, aircraft.Position.Y,
                targetPosition.X, targetPosition.Y);
            float headingDiff = NormalizeAngleDiff(aircraft.Heading - targetHeading);

            if (Math.Abs(headingDiff) <= Constants.TASK_HEADING_DEVIATION_THRESHOLD)
                return false;

            // Check speed - only re-issue if aircraft is slow (possibly stuck or stalled)
            float speed = aircraft.Speed;
            if (speed >= Constants.STUCK_SPEED_THRESHOLD)
                return false;

            // All three conditions met - aircraft is off course, deviated, AND slow
            Logger.Debug($"NeedsFlightTaskReissue: dist={distanceToTarget:F1}m, headingDiff={headingDiff:F1}°, speed={speed:F1}m/s");
            return true;
        }

        private void UpdateFlightAltitude()
        {
            Logger.Debug($"UpdateFlightAltitude called, _flightMode={_flightMode}, _taskIssued={_taskIssued}");

            // Re-issue task with new altitude
            try
            {
                Ped player = Game.Player.Character;

                // Use IsInVehicle() to avoid stale CurrentVehicle references
                if (!player.IsInVehicle())
                {
                    Logger.Warning("UpdateFlightAltitude: Player not in vehicle");
                    return;
                }

                Vehicle aircraft = player.CurrentVehicle;

                if (aircraft == null)
                {
                    Logger.Warning("UpdateFlightAltitude: aircraft is null");
                    return;
                }

                if (!_taskIssued)
                {
                    Logger.Warning("UpdateFlightAltitude: _taskIssued is false, cannot update");
                    return;
                }

                Logger.Debug($"UpdateFlightAltitude: Re-issuing task for flight mode {_flightMode} with altitude {_targetAltitude}m");

                switch (_flightMode)
                {
                    case Constants.FLIGHT_MODE_CRUISE:
                        IssueCruiseTask(player, aircraft);
                        Logger.Info("UpdateFlightAltitude: Cruise task re-issued");
                        break;

                    case Constants.FLIGHT_MODE_WAYPOINT:
                    case Constants.FLIGHT_MODE_DESTINATION:
                        IssueNavigationTask(player, aircraft, _destinationPos);
                        Logger.Info($"UpdateFlightAltitude: Navigation task re-issued to {_destinationPos}");
                        break;

                    default:
                        Logger.Warning($"UpdateFlightAltitude: Unknown flight mode {_flightMode}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.UpdateFlightAltitude");
            }
        }

        private void CheckLandingGearDeployment(Vehicle aircraft, Vector3 position, float distance)
        {
            if (_gearDeployed) return;

            // Only for fixed-wing
            if (_aircraftType != Constants.AIRCRAFT_TYPE_FIXED_WING &&
                _aircraftType != Constants.AIRCRAFT_TYPE_VTOL_PLANE)
                return;

            // Deploy gear at 500ft AGL or 1 mile from destination, whichever comes first
            float heightAboveGround = aircraft.HeightAboveGround;

            if (heightAboveGround < Constants.AUTOFLY_GEAR_DEPLOY_ALTITUDE ||
                distance < Constants.AUTOFLY_GEAR_DEPLOY_DISTANCE)
            {
                DeployLandingGear(aircraft);
            }
        }

        private void DeployLandingGear(Vehicle aircraft)
        {
            if (_gearDeployed) return;

            try
            {
                Function.Call(
                    (Hash)Constants.NATIVE_CONTROL_LANDING_GEAR,
                    aircraft, Constants.LANDING_GEAR_DEPLOYED);

                _gearDeployed = true;

                if (!_gearDeployAnnounced)
                {
                    _gearDeployAnnounced = true;
                    _audio.Speak("Gear down");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AutoFlyManager.DeployLandingGear");
            }
        }

        #endregion

        #region Private Methods - Announcements

        private void AnnounceDistanceMilestones(float distance, long currentTick)
        {
            // Throttle announcements
            if (currentTick - _lastDistanceAnnounceTick < Constants.TICK_INTERVAL_AUTOFLY_DISTANCE)
                return;

            float distanceMiles = distance * Constants.METERS_TO_MILES;

            // Check miles milestones (when > 0.5 miles)
            if (distanceMiles >= 0.5f)
            {
                float[] milestones = Constants.AUTOFLY_DISTANCE_MILESTONES_MILES;
                float lastMiles = _lastDistanceToDestination * Constants.METERS_TO_MILES;

                for (int i = 0; i < milestones.Length; i++)
                {
                    float milestone = milestones[i];

                    // Crossed this milestone?
                    if (lastMiles >= milestone && distanceMiles < milestone)
                    {
                        _lastDistanceAnnounceTick = currentTick;
                        // Use StringBuilder to avoid string allocation
                        _announceBuilder.Clear();
                        _announceBuilder.Append(milestone.ToString("F1"))
                            .Append(" miles to destination");
                        TryAnnounce(_announceBuilder.ToString(),
                            Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                        break;
                    }
                }
            }
            else
            {
                // Check feet milestones (when < 0.5 miles)
                int distanceFeet = (int)(distance * Constants.METERS_TO_FEET);
                int lastFeet = (int)(_lastDistanceToDestination * Constants.METERS_TO_FEET);
                int[] milestones = Constants.AUTOFLY_DISTANCE_MILESTONES_FEET;

                for (int i = 0; i < milestones.Length; i++)
                {
                    int milestone = milestones[i];

                    // Crossed this milestone?
                    if (lastFeet >= milestone && distanceFeet < milestone)
                    {
                        _lastDistanceAnnounceTick = currentTick;
                        // Use StringBuilder to avoid string allocation
                        _announceBuilder.Clear();
                        _announceBuilder.Append(milestone).Append(" feet");
                        TryAnnounce(_announceBuilder.ToString(),
                            Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                        break;
                    }
                }
            }
        }

        private void AnnounceApproachGuidance(Vehicle aircraft, Vector3 position, long currentTick)
        {
            // Throttle announcements
            if (currentTick - _lastApproachAnnounceTick < Constants.TICK_INTERVAL_AUTOFLY_APPROACH * 2)
                return;

            float distance = (_destinationPos - position).Length();
            float groundSpeed = aircraft.Speed;

            // Enhanced flight path planning with wind compensation and terrain avoidance
            FlightPathInfo pathInfo = CalculateOptimalFlightPath(aircraft, position, distance, groundSpeed);

            // Announce course corrections with wind compensation
            if (Math.Abs(pathInfo.RequiredHeadingChange) > Constants.AUTOFLY_TURN_GUIDANCE_THRESHOLD)
            {
                _lastApproachAnnounceTick = currentTick;

                string turnDirection = pathInfo.RequiredHeadingChange > 0 ? "right" : "left";
                int turnDegrees = (int)Math.Abs(pathInfo.RequiredHeadingChange);

                TryAnnounce($"Turn {turnDirection} {turnDegrees} degrees",
                    Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
            }

            // Enhanced altitude guidance with terrain awareness
            if (distance < Constants.AUTOFLY_APPROACH_DISTANCE / 2)
            {
                float currentAltitude = position.Z;
                float optimalAltitude = pathInfo.RecommendedAltitude;
                float altitudeDiff = currentAltitude - optimalAltitude;

                if (Math.Abs(altitudeDiff) > Constants.AUTOFLY_ALTITUDE_GUIDANCE_THRESHOLD)
                {
                    int diffFeet = (int)(altitudeDiff * Constants.METERS_TO_FEET);

                    if (altitudeDiff > 0)
                    {
                        string terrainInfo = pathInfo.TerrainClearance < Constants.AUTOFLY_TERRAIN_WARNING_THRESHOLD ? " (terrain ahead)" : "";
                        TryAnnounce($"Descend {Math.Abs(diffFeet)} feet{terrainInfo}",
                            Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                    }
                    else
                    {
                        TryAnnounce($"Climb {Math.Abs(diffFeet)} feet",
                            Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                    }
                }

                // Announce glideslope information for landing approaches
                if (distance < Constants.AUTOFLY_FINAL_DISTANCE * 2)
                {
                    AnnounceGlideslopeGuidance(aircraft, position, distance, pathInfo);
                }
            }

            // Speed guidance based on aircraft performance
            float optimalSpeed = CalculateOptimalApproachSpeed(aircraft, distance, pathInfo);
            float currentSpeed = aircraft.Speed;
            float speedDiff = optimalSpeed - currentSpeed;

            if (Math.Abs(speedDiff) > 5f) // Significant speed difference
            {
                string speedCommand = speedDiff > 0 ? "increase speed" : "reduce speed";
                int speedChange = (int)Math.Abs(speedDiff);
                TryAnnounce($"{speedCommand} by {speedChange} knots",
                    Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
            }
        }

        /// <summary>
        /// Calculate optimal flight path considering terrain and aircraft performance
        /// </summary>
        private FlightPathInfo CalculateOptimalFlightPath(Vehicle aircraft, Vector3 position, float distance, float groundSpeed)
        {
            // Safety check for edge cases
            if (distance <= 0 || aircraft == null)
            {
                return new FlightPathInfo
                {
                    RequiredHeadingChange = 0f,
                    RecommendedAltitude = _targetAltitude,
                    TerrainClearance = Constants.AUTOFLY_DEFAULT_TERRAIN_CLEARANCE
                };
            }

            // Calculate direct heading to destination using GTA V's coordinate system
            // SpatialCalculator.CalculateAngle returns 0-360 degrees with 0=North
            float directHeading = (float)SpatialCalculator.CalculateAngle(
                position.X, position.Y, _destinationPos.X, _destinationPos.Y);

            // GTA V's aircraft.Heading is also 0-360 with 0=North
            float aircraftHeading = aircraft.Heading;

            // Check terrain clearance ahead on the flight path
            float terrainClearance = CheckTerrainClearance(position, directHeading, distance);

            // Determine optimal altitude based on phase and terrain
            float optimalAltitude = CalculateOptimalAltitude(position, distance, terrainClearance);

            // Calculate required heading change (signed: + = turn right, - = turn left)
            // Both headings are 0-360, so we need to find shortest turn direction
            float headingChange = NormalizeAngleDiff(directHeading - aircraftHeading);

            return new FlightPathInfo
            {
                RequiredHeadingChange = headingChange,
                RecommendedAltitude = optimalAltitude,
                TerrainClearance = terrainClearance
            };
        }

        /// <summary>
        /// Check terrain clearance ahead on the flight path
        /// Uses pre-allocated _terrainHeightArg to avoid per-tick allocations
        /// </summary>
        private float CheckTerrainClearance(Vector3 position, float heading, float distance)
        {
            // Validate inputs to prevent issues
            if (float.IsNaN(heading) || float.IsInfinity(heading) || distance <= 0)
                return Constants.AUTOFLY_DEFAULT_TERRAIN_CLEARANCE; // Default safe clearance

            // Sample terrain at multiple points ahead
            float minClearance = float.MaxValue;
            float sampleDistance = Math.Min(distance, Constants.AUTOFLY_TERRAIN_SAMPLE_MAX); // Check up to 2km ahead

            // GTA V coordinate system: heading 0=North(+Y), 90=East(+X), uses sin/cos as per standard GTA
            // X = sin(heading), Y = cos(heading)
            float radians = heading * (float)Math.PI / 180f;
            float sinHeading = (float)Math.Sin(radians);  // X direction
            float cosHeading = (float)Math.Cos(radians);  // Y direction

            for (float d = Constants.AUTOFLY_TERRAIN_SAMPLE_START; d <= sampleDistance; d += Constants.AUTOFLY_TERRAIN_SAMPLE_INTERVAL)
            {
                // GTA V: X = sin(heading) * distance, Y = cos(heading) * distance
                Vector3 samplePos = position + new Vector3(
                    sinHeading * d,
                    cosHeading * d,
                    0f);

                // Get ground height at sample position
                // CRITICAL: Reuse pre-allocated _terrainHeightArg to avoid GC pressure
                bool success = Function.Call<bool>(
                    (Hash)Constants.NATIVE_GET_GROUND_Z_FOR_3D_COORD,
                    samplePos.X, samplePos.Y, samplePos.Z,
                    _terrainHeightArg,
                    false);  // ignoreWater = false

                if (success)
                {
                    float groundZ = _terrainHeightArg.GetResult<float>();
                    float clearance = position.Z - groundZ;
                    minClearance = Math.Min(minClearance, clearance);
                }
            }

            return minClearance < float.MaxValue ? minClearance : Constants.AUTOFLY_DEFAULT_TERRAIN_CLEARANCE; // Default safe clearance
        }

        /// <summary>
        /// Calculate optimal altitude for current flight phase
        /// </summary>
        private float CalculateOptimalAltitude(Vector3 position, float distance, float terrainClearance)
        {
            float baseAltitude = _destinationPos.Z;

            // Ensure minimum terrain clearance
            float minSafeAltitude = GetTerrainHeightAt(position) + Constants.AUTOFLY_MIN_SAFE_ALTITUDE_AGL; // 1000 feet AGL minimum
            float terrainBasedAltitude = baseAltitude + Math.Max(0, Constants.AUTOFLY_MIN_SAFE_ALTITUDE_AGL - terrainClearance);

            // Phase-based altitude adjustments
            switch (_currentPhase)
            {
                case Constants.PHASE_CRUISE:
                    // Cruise at higher altitude for efficiency
                    return Math.Max(minSafeAltitude, Math.Max(terrainBasedAltitude, baseAltitude + Constants.AUTOFLY_CRUISE_ALTITUDE_OFFSET));

                case Constants.PHASE_APPROACH:
                    // Descend gradually during approach
                    return Math.Max(minSafeAltitude, baseAltitude + Constants.AUTOFLY_APPROACH_ALTITUDE_OFFSET);

                case Constants.PHASE_FINAL:
                    // Low approach altitude
                    return Math.Max(minSafeAltitude, baseAltitude + Constants.AUTOFLY_FINAL_ALTITUDE_OFFSET);

                default:
                    return Math.Max(minSafeAltitude, baseAltitude + Constants.AUTOFLY_DEFAULT_ALTITUDE_OFFSET);
            }
        }

        /// <summary>
        /// Get terrain height at position
        /// Uses pre-allocated _terrainHeightArg to avoid per-tick allocations
        /// </summary>
        private float GetTerrainHeightAt(Vector3 position)
        {
            // CRITICAL: Reuse pre-allocated _terrainHeightArg to avoid GC pressure
            bool success = Function.Call<bool>(
                (Hash)Constants.NATIVE_GET_GROUND_Z_FOR_3D_COORD,
                position.X, position.Y, position.Z,
                _terrainHeightArg,
                false);  // ignoreWater = false

            if (success)
            {
                return _terrainHeightArg.GetResult<float>();
            }
            return 0f;  // Default to sea level if ground check fails
        }

        /// <summary>
        /// Calculate optimal approach speed based on aircraft type and conditions
        /// </summary>
        private float CalculateOptimalApproachSpeed(Vehicle aircraft, float distance, FlightPathInfo pathInfo)
        {
            float baseSpeed = _targetSpeed;
            int aircraftType = GetAircraftType(aircraft);

            // Distance-based speed adjustments
            if (distance < Constants.AUTOFLY_FINAL_DISTANCE)
            {
                // Slow down significantly for final approach
                baseSpeed *= 0.6f;
            }
            else if (distance < Constants.AUTOFLY_APPROACH_DISTANCE)
            {
                // Moderate slowdown during approach
                baseSpeed *= 0.8f;
            }

            // Aircraft type specific limits
            switch (aircraftType)
            {
                case Constants.AIRCRAFT_TYPE_HELICOPTER:
                    baseSpeed = Math.Min(baseSpeed, Constants.AUTOFLY_HELI_DEFAULT_SPEED);
                    break;
                case Constants.AIRCRAFT_TYPE_BLIMP:
                    baseSpeed = Math.Min(baseSpeed, Constants.AUTOFLY_BLIMP_DEFAULT_SPEED);
                    break;
                default: // Fixed-wing
                    baseSpeed = Math.Min(baseSpeed, Constants.AUTOFLY_DEFAULT_SPEED);
                    break;
            }

            return Math.Max(Constants.AUTOFLY_APPROACH_MIN_SPEED, baseSpeed); // Minimum safe speed
        }

        /// <summary>
        /// Announce glideslope guidance for landing approaches
        /// </summary>
        private void AnnounceGlideslopeGuidance(Vehicle aircraft, Vector3 position, float distance, FlightPathInfo pathInfo)
        {
            // Calculate glideslope angle (ideal 3° approach)
            float heightAboveGround = position.Z - GetTerrainHeightAt(position);
            float glideslopeAngle = (float)(Math.Atan2(heightAboveGround, distance) * 180f / Math.PI);

            float idealGlideslope = 3f; // 3° approach
            float glideslopeError = glideslopeAngle - idealGlideslope;

            if (Math.Abs(glideslopeError) > 0.5f) // Significant deviation
            {
                string guidance = glideslopeError > 0 ? "too high" : "too low";
                TryAnnounce($"Glideslope {guidance}, adjust descent",
                    Constants.ANNOUNCE_PRIORITY_MEDIUM, 0); // currentTick not needed here
            }
        }

        /// <summary>
        /// Calculate maximum safe speed based on aircraft type and conditions
        /// </summary>
        private float CalculateMaxSafeSpeed()
        {
            float baseMaxSpeed;

            switch (_aircraftType)
            {
                case Constants.AIRCRAFT_TYPE_BLIMP:
                    baseMaxSpeed = Constants.AUTOFLY_BLIMP_MAX_SPEED;
                    break;
                case Constants.AIRCRAFT_TYPE_HELICOPTER:
                case Constants.AIRCRAFT_TYPE_VTOL_HOVER:
                    baseMaxSpeed = Constants.AUTOFLY_HELI_DEFAULT_SPEED * 1.2f; // Slight over-speed capability
                    break;
                default: // Fixed-wing
                    baseMaxSpeed = Constants.AUTOFLY_MAX_SPEED;
                    break;
            }

            // Adjust for altitude (less dense air at high altitude)
            float altitudeFactor = Math.Max(Constants.AUTOFLY_ALTITUDE_FACTOR_MIN, 1.0f - (_targetAltitude / Constants.AUTOFLY_ALTITUDE_FACTOR_REFERENCE) * Constants.AUTOFLY_ALTITUDE_FACTOR_RATE);
            baseMaxSpeed *= altitudeFactor;

            return baseMaxSpeed;
        }

        /// <summary>
        /// Calculate minimum safe speed to prevent stall
        /// </summary>
        private float CalculateMinSafeSpeed()
        {
            float baseMinSpeed;

            switch (_aircraftType)
            {
                case Constants.AIRCRAFT_TYPE_BLIMP:
                    baseMinSpeed = Constants.AUTOFLY_BLIMP_MIN_SPEED;
                    break;
                case Constants.AIRCRAFT_TYPE_HELICOPTER:
                case Constants.AIRCRAFT_TYPE_VTOL_HOVER:
                    baseMinSpeed = 8f; // Helicopters can hover but need some forward speed for control
                    break;
                default: // Fixed-wing - minimum speed
                    baseMinSpeed = GetMinimumSafeSpeed();
                    break;
            }

            return Math.Max(Constants.AUTOFLY_MIN_SPEED, baseMinSpeed);
        }

        /// <summary>
        /// Get minimum safe speed for fixed-wing aircraft
        /// </summary>
        private float GetMinimumSafeSpeed()
        {
            return Constants.AUTOFLY_MIN_SPEED; // 25 m/s
        }

        /// <summary>
        /// Get aircraft acceleration rate based on type and conditions
        /// </summary>
        private float GetAircraftAccelerationRate()
        {
            float baseAcceleration;

            switch (_aircraftType)
            {
                case Constants.AIRCRAFT_TYPE_BLIMP:
                    baseAcceleration = 0.5f; // Very slow acceleration
                    break;
                case Constants.AIRCRAFT_TYPE_HELICOPTER:
                case Constants.AIRCRAFT_TYPE_VTOL_HOVER:
                    baseAcceleration = 2.0f; // Moderate acceleration
                    break;
                default: // Fixed-wing
                    baseAcceleration = 4.0f; // Good acceleration
                    break;
            }

            // Reduce acceleration at high altitude
            float altitudeFactor = Math.Max(Constants.AUTOFLY_ACCEL_ALTITUDE_FACTOR_MIN, 1.0f - (_targetAltitude / Constants.AUTOFLY_ALTITUDE_FACTOR_REFERENCE) * Constants.AUTOFLY_ACCEL_ALTITUDE_FACTOR_RATE);

            return baseAcceleration * altitudeFactor;
        }

        /// <summary>
        /// Get aircraft deceleration rate (drag-based)
        /// </summary>
        private float GetAircraftDecelerationRate()
        {
            float baseDeceleration;

            switch (_aircraftType)
            {
                case Constants.AIRCRAFT_TYPE_BLIMP:
                    baseDeceleration = 0.3f; // Slow deceleration
                    break;
                case Constants.AIRCRAFT_TYPE_HELICOPTER:
                case Constants.AIRCRAFT_TYPE_VTOL_HOVER:
                    baseDeceleration = 1.5f; // Can decelerate by reducing power
                    break;
                default: // Fixed-wing
                    baseDeceleration = 2.5f; // Good deceleration with flaps/spoilers
                    break;
            }

            return baseDeceleration;
        }

        /// <summary>
        /// Apply speed change to the flight task
        /// </summary>
        private void ApplySmoothSpeedChange(float newTargetSpeed)
        {
            _targetSpeed = newTargetSpeed;

            // Always apply speed changes immediately - the native handles smooth transitions
            UpdateFlightSpeed();
        }

        private bool TryAnnounce(string message, int priority, long currentTick)
        {
            // Critical always speaks
            if (priority >= Constants.ANNOUNCE_PRIORITY_CRITICAL)
            {
                _audio.Speak(message);
                _lastAnnouncementTick = currentTick;
                _lastAnnouncementPriority = priority;
                return true;
            }

            // Check time elapsed
            long elapsed = currentTick - _lastAnnouncementTick;

            long requiredGap = Constants.ANNOUNCE_MIN_GAP;
            if (priority > _lastAnnouncementPriority)
            {
                requiredGap = Constants.ANNOUNCE_MIN_GAP / 2;  // Half gap for higher priority
            }

            // Speak if gap exceeded
            if (elapsed >= requiredGap)
            {
                _audio.Speak(message);
                _lastAnnouncementTick = currentTick;
                _lastAnnouncementPriority = priority;
                return true;
            }

            return false;
        }

        #endregion

        #region Private Methods - Validation & Helpers

        private bool ValidateAircraft(Ped player, Vehicle aircraft)
        {
            if (aircraft == null || !player.IsInVehicle())
            {
                _audio.Speak("You must be in an aircraft to use AutoFly");
                return false;
            }

            if (player.SeatIndex != VehicleSeat.Driver)
            {
                _audio.Speak("You must be the pilot");
                return false;
            }

            VehicleClass vehicleClass = aircraft.ClassType;
            if (vehicleClass != VehicleClass.Planes && vehicleClass != VehicleClass.Helicopters)
            {
                _audio.Speak("You must be in an aircraft to use AutoFly");
                return false;
            }

            return true;
        }

        private int GetAircraftType(Vehicle aircraft)
        {
            int modelHash = aircraft.Model.Hash;

            // Check for blimp
            if (Constants.BLIMP_VEHICLE_HASHES.Contains(modelHash))
                return Constants.AIRCRAFT_TYPE_BLIMP;

            // Check for VTOL
            if (Constants.VTOL_VEHICLE_HASHES.Contains(modelHash))
            {
                float nozzlePosition = Function.Call<float>(
                    (Hash)Constants.NATIVE_GET_VEHICLE_FLIGHT_NOZZLE_POSITION, aircraft);

                return nozzlePosition > Constants.VTOL_HOVER_THRESHOLD
                    ? Constants.AIRCRAFT_TYPE_VTOL_HOVER
                    : Constants.AIRCRAFT_TYPE_VTOL_PLANE;
            }

            // Check for helicopter using hash lookup (O(1) - Grok optimization)
            if (Constants.HELICOPTER_HASHES.Contains(modelHash))
                return Constants.AIRCRAFT_TYPE_HELICOPTER;

            // Fallback to vehicle class check for any helicopters not in hash list
            if (aircraft.ClassType == VehicleClass.Helicopters)
                return Constants.AIRCRAFT_TYPE_HELICOPTER;

            return Constants.AIRCRAFT_TYPE_FIXED_WING;
        }

        /// <summary>
        /// Fast helicopter check using hash lookup (O(1) - Grok optimization)
        /// </summary>
        private bool IsHelicopter(int modelHash)
        {
            return Constants.HELICOPTER_HASHES.Contains(modelHash);
        }

        private float NormalizeAngleDiff(float angle)
        {
            // Handle edge cases: NaN, Infinity would cause infinite loops
            if (float.IsNaN(angle) || float.IsInfinity(angle))
                return 0f;

            // Use modulo for efficiency (avoids potential infinite loops with extreme values)
            angle = angle % 360f;
            if (angle > 180f) angle -= 360f;
            else if (angle < -180f) angle += 360f;
            return angle;
        }

        private void ResetState()
        {
            _autoFlyActive = false;
            _taskIssued = false;
            _flightMode = Constants.FLIGHT_MODE_NONE;
            _currentPhase = Constants.PHASE_INACTIVE;
            _pauseState = Constants.PAUSE_STATE_NONE;

            _destinationPos = Vector3.Zero;
            _runwayEndPos = Vector3.Zero;
            _runwayHeading = -1f;
            _isHelipad = false;
            _destinationName = null;

            _lastDistanceToDestination = float.MaxValue;
            _lastDistanceAnnounceTick = 0;
            _lastApproachAnnounceTick = 0;
            _lastAnnouncementTick = 0;
            _lastAnnouncementPriority = 0;

            _gearDeployed = false;
            _gearDeployAnnounced = false;
            _circleModeActive = false;

            // Reset cached ground height
            _destinationGroundZ = 0;
            _groundHeightCached = false;

            // Reset landing phase timeout tracker
            _landingPhaseStartTick = 0;

            // Reset cruise correction throttle
            _lastCruiseCorrectionTick = 0;

            // Reset enhanced landing tracking
            _approachStartAltitude = 0;
            _glideslopeTargetAltitude = 0;
            _stableOnGroundCount = 0;
            _lastHeightAboveGround = 0;

            // Reset stuck detection (Grok optimization)
            _flightStuckCounter = 0;
            _lastProgressDistance = float.MaxValue;
            _lastProgressAltitude = 0;
        }

        #endregion
    }
}
