using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
/// <summary>
/// Tracking info for overtaking detection
/// </summary>
internal struct OvertakeTrackingInfo
{
    public int VehicleHandle;
    public int State;  // 0=ahead, 1=beside, 2=behind (passed)
    public long FirstSeenTick;
    public float InitialDistance;

    public OvertakeTrackingInfo(int handle, float distance, long tick)
    {
        VehicleHandle = handle;
        State = 0;  // Start as ahead
        FirstSeenTick = tick;
        InitialDistance = distance;
    }
}

    // CurveInfo, CurveSeverity, CurveDirection are now defined in CurveTypes.cs

    /// <summary>
    /// Manages autonomous driving and road feature detection for accessibility
    /// Uses GTA V's native AI driving tasks (issued ONCE, not every frame) for smooth driving
    /// Refactored to use extracted components for separation of concerns.
    /// </summary>
    public class AutoDriveManager
    {
        private readonly AudioManager _audio;
        private readonly SettingsManager _settings;

        // ===== EXTRACTED COMPONENTS =====
        private readonly WeatherManager _weatherManager;
        private readonly CollisionDetector _collisionDetector;
        private readonly CurveAnalyzer _curveAnalyzer;
        private readonly RecoveryManager _recoveryManager;
        private readonly AnnouncementQueue _announcementQueue;

        // New extracted managers for separation of concerns
        private readonly NavigationManager _navigationManager;
        private readonly RoadFeatureDetector _roadFeatureDetector;
        private readonly TrafficAwarenessManager _trafficAwarenessManager;
        private readonly EnvironmentalManager _environmentalManager;
        private readonly EmergencyVehicleHandler _emergencyVehicleHandler;
        private readonly StructureDetector _structureDetector;
        private readonly RoadTypeManager _roadTypeManager;
        private readonly ETACalculator _etaCalculator;

        // Autodrive state
        private bool _autoDriveActive;
        private bool _wanderMode;          // true = wander, false = waypoint
        private float _targetSpeed;
        private bool _taskIssued;          // Ensures task issued only once
        private Vector3 _lastWaypointPos;
        private float _lastDistanceToWaypoint;  // For distance progress announcements

        // Road feature detection state
        private long _lastCurveAnnounceTick;
        private long _lastIntersectionAnnounceTick;
        private long _lastTrafficLightAnnounceTick;

        // Curve slowdown state
        private bool _curveSlowdownActive;
        private long _curveSlowdownEndTick;
        private float _originalSpeed;              // Speed before curve slowdown
        private float _curveSlowdownSpeed;         // Reduced speed during curve

        // Arrival slowdown state
        private bool _arrivalSlowdownActive;
        private float _lastArrivalSpeed;           // Track speed during arrival approach
        private int _lastAnnouncedArrivalDistance; // Track which distance milestone was announced

        // Final approach for precise waypoint arrival
        private bool _inFinalApproach;
        private Vector3 _safeArrivalPosition;      // Road-safe position near waypoint
        private Vector3 _originalWaypointPos;      // Original waypoint for reference

        // Deferred restart flag (prevents crash when changing waypoint during active drive)
        private bool _pendingRestart;

        // Deferred start flag (prevents crash when switching modes in same frame)
        private bool _pendingWaypointStart;

        // Road type speed adjustment
        private float _roadTypeSpeedMultiplier = 1.0f;
        private int _lastSpeedAdjustedRoadType = -1;

        // Traffic light state tracking
        private bool _stoppedAtLight;
        private long _lastTrafficLightStateTick;
        private Vector3 _trafficLightStopPosition;

        // U-turn detection
        private Vector3 _uturnTrackingPosition;
        private float _uturnTrackingHeading;
        private long _lastUturnAnnounceTick;

        // Hill/gradient detection
        private long _lastHillAnnounceTick;
        private bool _announcedCurrentHill;
        private float _lastHillGradient;

        // ===== NEW FEATURES STATE =====

        // Weather-based driving
        private int _currentWeatherHash;
        private float _weatherSpeedMultiplier = 1.0f;
        private long _lastWeatherCheckTick;
        private bool _weatherAnnounced;

        // Collision warning
        private float _lastVehicleAheadDistance = float.MaxValue;
        private int _lastCollisionWarningLevel;  // 0=none, 1=far, 2=medium, 3=close
        private long _lastCollisionCheckTick;
        private long _lastCollisionAnnounceTick;

        // Time-of-day awareness
        private int _lastTimeOfDay;  // 0=day, 1=dawn/dusk, 2=night
        private float _timeSpeedMultiplier = 1.0f;
        private long _lastTimeCheckTick;
        private bool _headlightsOn;

        // Emergency vehicle awareness
        private bool _yieldingToEmergency;
        private long _emergencyYieldStartTick;
        private long _lastEmergencyCheckTick;
        private Vector3 _emergencyVehiclePosition;
        private bool _emergencyApproachingFromBehind;  // Track if emergency is behind us (needs more urgent yield)

        // ETA announcements
        private float _lastAnnouncedETA;  // seconds
        private long _lastETAAnnounceTick;
        private float[] _speedSamples;
        private int _speedSampleIndex;
        private float _averageSpeed;

        // Pause/Resume capability
        private int _pauseState;
        private long _pauseStartTick;
        private float _prePauseSpeed;
        private bool _wasPausedWander;  // Track mode before pause

        // Following distance feedback
        private float _followingTimeGap;  // Time-based following gap in seconds
        private int _lastFollowingState;  // 0=clear, 1=comfortable, 2=close, 3=too close, 4=dangerous
        private long _lastFollowingCheckTick;
        private long _lastFollowingAnnounceTick;

        // Tunnel/Bridge detection
        private int _currentStructureType;
        private long _lastStructureCheckTick;
        private long _lastStructureAnnounceTick;
        private bool _inStructure;

        // Lane change detection
        private Vector3 _laneTrackingPosition;
        private float _laneTrackingHeading;
        private long _lastLaneCheckTick;
        private long _lastLaneChangeAnnounceTick;
        private bool _laneChangeInProgress;

        // Overtaking detection
        private Dictionary<int, OvertakeTrackingInfo> _overtakeTracking;
        private long _lastOvertakeCheckTick;
        private long _lastOvertakeAnnounceTick;

        // Pre-allocated collections to avoid per-call allocations
        private readonly HashSet<int> _visibleHandles = new HashSet<int>();
        private readonly List<int> _handleRemovalList = new List<int>();

        // Intersection tracking for turn direction
        private bool _inIntersection;
        private float _preIntersectionHeading;
        private Vector3 _intersectionPosition;

        // Road type tracking
        private int _currentRoadType;
        private int _lastAnnouncedRoadType;
        private long _lastRoadTypeAnnounceTick;
        private long _lastRoadTypeCheckTick;

        // Road seeking state
        private int _seekMode;
        private bool _seekingRoad;
        private Vector3 _seekTargetPosition;
        private long _lastSeekScanTick;
        private long _seekStartTick;  // Track when seeking started for timeout
        private int _seekAttempts;    // Track number of scan attempts
        private bool _onDesiredRoadType;

        // PERFORMANCE: Pre-allocated vector for ScanForRoadType to avoid allocations in loop
        private Vector3 _scanSamplePos;

        // Task spam prevention - track last issued task
        private Vector3 _lastIssuedSeekTarget;  // Last target we issued a drive task for
        private bool _lastIssuedTaskWasWander;  // True if last task was wander, false if drive-to-coord

        // Driving style
        private int _currentDrivingStyleMode = Constants.DRIVING_STYLE_MODE_NORMAL;

        // Dead-end detection and avoidance
        private bool _inDeadEnd;
        private long _lastDeadEndCheckTick;
        private int _deadEndTurnCount;
        private Vector3 _deadEndEntryPosition;

        // ===== RECOVERY SYSTEM STATE =====

        // Stuck detection
        private Vector3 _lastStuckCheckPosition;
        private float _lastStuckCheckHeading;
        private long _lastStuckCheckTick;
        private int _stuckCheckCount;              // Consecutive stuck checks
        private bool _isStuck;

        // Recovery state
        private int _recoveryState;
        private long _recoveryStartTick;
        private int _recoveryAttempts;
        private long _lastRecoveryTick;
        private int _recoveryTurnDirection;        // 1 = right, -1 = left

        // Progress tracking (for waypoint timeout)
        private float _lastProgressDistance;
        private long _lastProgressTick;

        // Vehicle state
        private bool _vehicleFlipped;
        private bool _vehicleInWater;
        private bool _vehicleOnFire;
        private bool _vehicleCriticalDamage;
        private long _lastVehicleStateCheckTick;

        // PERFORMANCE: Pre-cached Hash values to avoid repeated casting in hot paths
        private static readonly Hash _sirenAudioOnHash = (Hash)Constants.NATIVE_IS_VEHICLE_SIREN_AUDIO_ON;
        private static readonly Hash _setCruiseSpeedHash = (Hash)Constants.NATIVE_SET_DRIVE_TASK_CRUISE_SPEED;
        private static readonly Hash _clearPedTasksHash = (Hash)Constants.NATIVE_CLEAR_PED_TASKS;
        private static readonly Hash _setHandbrakeHash = (Hash)Constants.NATIVE_SET_VEHICLE_HANDBRAKE;
        private static readonly Hash _getVehicleNodePropsHash = (Hash)Constants.NATIVE_GET_VEHICLE_NODE_PROPERTIES;
        private static readonly Hash _getClosestNodeWithHeadingHash = (Hash)Constants.NATIVE_GET_CLOSEST_VEHICLE_NODE_WITH_HEADING;
        private static readonly Hash _taskVehicleDriveWanderHash = (Hash)Constants.NATIVE_TASK_VEHICLE_DRIVE_WANDER;
        private static readonly Hash _setDriverAbilityHash = (Hash)Constants.NATIVE_SET_DRIVER_ABILITY;
        private static readonly Hash _setDriverAggressivenessHash = (Hash)Constants.NATIVE_SET_DRIVER_AGGRESSIVENESS;
        private static readonly Hash _taskDriveToCoordLongrangeHash = (Hash)Constants.NATIVE_TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE;
        private static readonly Hash _taskDriveToCoordHash = (Hash)Constants.NATIVE_TASK_VEHICLE_DRIVE_TO_COORD;
        private static readonly Hash _getPrevWeatherHash = (Hash)Constants.NATIVE_GET_PREV_WEATHER_TYPE_HASH_NAME;
        private static readonly Hash _setVehicleLightsHash = (Hash)Constants.NATIVE_SET_VEHICLE_LIGHTS;
        private static readonly Hash _getGroundZHash = (Hash)Constants.NATIVE_GET_GROUND_Z_FOR_3D_COORD;
        private static readonly Hash _generateDirectionsHash = (Hash)Constants.NATIVE_GENERATE_DIRECTIONS_TO_COORD;
        private static readonly Hash _getClosestNodeHash = (Hash)Constants.NATIVE_GET_CLOSEST_VEHICLE_NODE;
        private static readonly Hash _getNthClosestNodeHash = (Hash)Constants.NATIVE_GET_NTH_CLOSEST_VEHICLE_NODE;
        private static readonly Hash _getSafeCoordForPedHash = (Hash)Constants.NATIVE_GET_SAFE_COORD_FOR_PED;
        private static readonly Hash _getPointOnRoadSideHash = (Hash)Constants.NATIVE_GET_POINT_ON_ROAD_SIDE;
        private static readonly Hash _taskVehicleTempActionHash = (Hash)Constants.NATIVE_TASK_VEHICLE_TEMP_ACTION;
        private static readonly Hash _isEntityInWaterHash = (Hash)Constants.NATIVE_IS_ENTITY_IN_WATER;

        // Pre-allocated OutputArguments to avoid allocations
        private readonly OutputArgument _nodePos = new OutputArgument();
        private readonly OutputArgument _nodeHeading = new OutputArgument();
        private readonly OutputArgument _density = new OutputArgument();
        private readonly OutputArgument _flags = new OutputArgument();

        // Pre-allocated for seeking scans
        private readonly OutputArgument _seekNodePos = new OutputArgument();
        private readonly OutputArgument _seekNodeHeading = new OutputArgument();
        private readonly OutputArgument _seekDensity = new OutputArgument();
        private readonly OutputArgument _seekFlags = new OutputArgument();

        // Pre-allocated for structure checks (avoid per-tick allocations)
        private readonly OutputArgument _structureBelowArg = new OutputArgument();

        // Pre-allocated for ETA road distance calculation
        private readonly OutputArgument _roadDistanceArg = new OutputArgument();
        private readonly OutputArgument _roadDirectionArg1 = new OutputArgument();
        private readonly OutputArgument _roadDirectionArg2 = new OutputArgument();

        // Pre-allocated for safe arrival position calculation
        private readonly OutputArgument _safeArrivalPosArg = new OutputArgument();
        private readonly OutputArgument _safeArrivalHeadingArg = new OutputArgument();
        private readonly OutputArgument _safePedCoordArg = new OutputArgument();
        private readonly OutputArgument _roadSidePosArg = new OutputArgument();

        public bool IsActive => _autoDriveActive;
        public bool IsSeeking => _seekingRoad;
        public int CurrentRoadType => _currentRoadType;
        public int SeekMode => _seekMode;
        public int CurrentDrivingStyleMode => _currentDrivingStyleMode;
        public bool IsRecovering => _recoveryState != Constants.RECOVERY_STATE_NONE;
        public bool IsStuck => _isStuck;
        public bool IsPaused => _pauseState == Constants.PAUSE_STATE_PAUSED;
        public bool IsYieldingToEmergency => _emergencyVehicleHandler?.IsYieldingToEmergency ?? _yieldingToEmergency;

        public AutoDriveManager(AudioManager audio, SettingsManager settings)
        {
            _audio = audio;
            _settings = settings;
            _targetSpeed = Constants.AUTODRIVE_DEFAULT_SPEED;
            _autoDriveActive = false;
            _taskIssued = false;
            ResetRecoveryState();

            // Initialize extracted components (order matters - some depend on others)
            _weatherManager = new WeatherManager();
            _announcementQueue = new AnnouncementQueue(audio, settings);  // FIX: Pass settings for announcement toggles
            _collisionDetector = new CollisionDetector();
            _curveAnalyzer = new CurveAnalyzer(_weatherManager);
            _recoveryManager = new RecoveryManager();

            // Initialize new extracted managers
            _navigationManager = new NavigationManager(audio, _announcementQueue);
            _roadFeatureDetector = new RoadFeatureDetector(audio, _announcementQueue, _weatherManager);
            _trafficAwarenessManager = new TrafficAwarenessManager(audio, _announcementQueue);
            _environmentalManager = new EnvironmentalManager(audio, _announcementQueue, _weatherManager);
            _emergencyVehicleHandler = new EmergencyVehicleHandler(audio, _announcementQueue);
            _structureDetector = new StructureDetector(audio, _announcementQueue);
            _roadTypeManager = new RoadTypeManager(audio, _announcementQueue);
            _etaCalculator = new ETACalculator(audio, _announcementQueue);

            // Initialize ETA speed samples array (legacy - TODO: move to ETACalculator)
            _speedSamples = new float[Constants.ETA_SPEED_SAMPLES];
            _speedSampleIndex = 0;

            // Initialize overtake tracking dictionary
            _overtakeTracking = new Dictionary<int, OvertakeTrackingInfo>(Constants.OVERTAKE_TRACKING_MAX);
        }

        /// <summary>
        /// Reset all recovery-related state
        /// </summary>
        private void ResetRecoveryState()
        {
            _lastStuckCheckPosition = Vector3.Zero;
            _lastStuckCheckHeading = 0f;
            _lastStuckCheckTick = 0;
            _stuckCheckCount = 0;
            _isStuck = false;

            _recoveryState = Constants.RECOVERY_STATE_NONE;
            _recoveryStartTick = 0;
            _recoveryAttempts = 0;
            _lastRecoveryTick = 0;
            _recoveryTurnDirection = 1;

            _lastProgressDistance = float.MaxValue;
            _lastProgressTick = 0;

            _vehicleFlipped = false;
            _vehicleInWater = false;
            _vehicleOnFire = false;
            _vehicleCriticalDamage = false;
            _lastVehicleStateCheckTick = 0;
        }

        /// <summary>
        /// Start aimless wandering driving
        /// </summary>
        public void StartWander()
        {
            Ped player = Game.Player.Character;
            Vehicle vehicle = player.CurrentVehicle;

            if (vehicle == null || !player.IsInVehicle())
            {
                _audio.Speak("You must be in a vehicle to use AutoDrive.");
                return;
            }

            if (player.SeatIndex != VehicleSeat.Driver)
            {
                _audio.Speak("You must be in the driver seat.");
                return;
            }

            // Stop any existing driving task
            Stop(false);

            try
            {
                // Use the user's selected driving style (not hardcoded CRUISE)
                // This respects the user's choice when cycling through driving styles
                int styleValue = Constants.GetDrivingStyleValue(_currentDrivingStyleMode);
                float ability = Constants.GetDrivingStyleAbility(_currentDrivingStyleMode);
                float aggressiveness = Constants.GetDrivingStyleAggressiveness(_currentDrivingStyleMode);

                if (Logger.IsDebugEnabled) Logger.Debug($"StartWander: Using style={styleValue} ({Constants.GetDrivingStyleName(_currentDrivingStyleMode)}), ability={ability}, aggression={aggressiveness}");

                // Issue drive task ONCE - this is the key to smooth driving!
                // Must use .Handle for native calls - SHVDN wrapper objects don't work directly
                Function.Call(
                    _taskVehicleDriveWanderHash,
                    player.Handle,
                    vehicle.Handle,
                    _targetSpeed,
                    styleValue);

                // Set driver ability - 1.0 for best vehicle control
                try
                {
                    Function.Call(
                        _setDriverAbilityHash,
                        player.Handle,
                        ability);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "SET_DRIVER_ABILITY failed");
                }

                // Set aggressiveness - 0.0 for calmest, smoothest driving
                try
                {
                    Function.Call(
                        _setDriverAggressivenessHash,
                        player.Handle,
                        aggressiveness);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "SET_DRIVER_AGGRESSIVENESS failed");
                }

                _autoDriveActive = true;
                _wanderMode = true;
                _taskIssued = true;

                // Initialize advanced driving features (using extracted managers)
                _structureDetector.InitializeUturnTracking(vehicle);
                _trafficAwarenessManager.InitializeLaneTracking(vehicle);
                _roadFeatureDetector.Reset();
                _roadTypeManager.Reset();
                _navigationManager.Reset();
                _lastAnnouncedArrivalDistance = int.MaxValue;

                int mph = (int)(_targetSpeed * Constants.METERS_PER_SECOND_TO_MPH);
                string styleName = Constants.GetDrivingStyleName(_currentDrivingStyleMode);
                _audio.Speak($"AutoDrive started, wander mode, {styleName} style, at {mph} miles per hour");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StartWander");
                _audio.Speak("Failed to start AutoDrive.");
            }
        }

        /// <summary>
        /// Start driving to a GPS waypoint
        /// </summary>
        public void StartWaypoint()
        {
            Logger.Info($"StartWaypoint called, _autoDriveActive={_autoDriveActive}, _wanderMode={_wanderMode}, _pendingWaypointStart={_pendingWaypointStart}");

            // FIX: Clear any stale pending flag from previous attempts
            // This prevents double-start when user calls StartWaypoint multiple times
            if (_pendingWaypointStart)
            {
                Logger.Info("StartWaypoint: Clearing stale pending flag from previous attempt");
                _pendingWaypointStart = false;
            }

            Ped player = Game.Player.Character;
            Vehicle vehicle = player.CurrentVehicle;

            if (vehicle == null || !player.IsInVehicle())
            {
                _audio.Speak("You must be in a vehicle to use AutoDrive.");
                return;
            }

            if (player.SeatIndex != VehicleSeat.Driver)
            {
                _audio.Speak("You must be in the driver seat.");
                return;
            }

            // Check if waypoint is set
            if (!Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
            {
                _audio.Speak("No waypoint set. Set a waypoint on the map first.");
                return;
            }

            // FIX: If already driving (wander or waypoint), defer start to next frame
            // This prevents same-frame task conflicts when Stop() clears tasks
            if (_autoDriveActive || _taskIssued)
            {
                Logger.Info("StartWaypoint: AutoDrive active, calling Stop() and deferring start to next frame");
                Stop(false);
                _pendingWaypointStart = true;
                return;
            }

            // Not currently driving, safe to start immediately
            Logger.Info("StartWaypoint: Not currently driving, starting immediately");
            StartWaypointInternal();
        }

        /// <summary>
        /// Internal method that actually initializes and starts waypoint navigation
        /// Called either immediately from StartWaypoint() or deferred to next frame
        /// </summary>
        private void StartWaypointInternal()
        {
            Logger.Info($"StartWaypointInternal: Starting waypoint navigation, _autoDriveActive={_autoDriveActive}, _taskIssued={_taskIssued}");

            // FIX: Double-start protection - if already started, don't start again
            if (_autoDriveActive || _taskIssued)
            {
                Logger.Info("StartWaypointInternal: Already started, aborting to prevent double-start");
                return;
            }

            Ped player = Game.Player.Character;
            Vehicle vehicle = player?.CurrentVehicle;

            // Re-validate (entities could have changed between frames if deferred)
            // CRITICAL: Check Exists() - entity wrapper could be invalid
            if (player == null || !player.Exists() ||
                vehicle == null || !vehicle.Exists() ||
                !player.IsInVehicle() ||
                player.SeatIndex != VehicleSeat.Driver)
            {
                Logger.Warning("StartWaypointInternal: Entity validation failed");
                _audio.Speak("Failed to start AutoDrive - invalid state.");
                return;
            }

            // Validate vehicle is drivable
            if (vehicle.IsDead || vehicle.Model == null)
            {
                Logger.Warning("StartWaypointInternal: Vehicle is dead or has no model");
                _audio.Speak("Vehicle is not drivable.");
                return;
            }

            // Re-check waypoint (could have been removed)
            if (!Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
            {
                _audio.Speak("Waypoint was removed.");
                return;
            }

            // Get waypoint position
            Vector3 waypointPos = World.WaypointPosition;
            Logger.Info($"StartWaypointInternal: waypointPos={waypointPos}");

            try
            {
                // Initialize navigation (delegated to NavigationManager)
                _navigationManager.InitializeWaypoint(waypointPos, player.Position);

                // Get safe arrival position from navigation manager
                Vector3 safePosition = _navigationManager.SafeArrivalPosition;

                // FIX: Validate safe position isn't NaN or Infinity
                if (float.IsNaN(safePosition.X) || float.IsNaN(safePosition.Y) || float.IsNaN(safePosition.Z) ||
                    float.IsInfinity(safePosition.X) || float.IsInfinity(safePosition.Y) || float.IsInfinity(safePosition.Z))
                {
                    Logger.Error($"GetSafeArrivalPosition returned invalid position: {safePosition}");
                    _audio.Speak("Failed to find safe arrival position for waypoint.");
                    return;
                }

                // Keep local copies for backward compatibility (legacy code references these)
                _originalWaypointPos = _navigationManager.OriginalWaypointPos;
                _safeArrivalPosition = safePosition;
                _inFinalApproach = false;

                // Get current driving style settings (using safe bounds-checked accessors)
                int styleValue = Constants.GetDrivingStyleValue(_currentDrivingStyleMode);
                float ability = Constants.GetDrivingStyleAbility(_currentDrivingStyleMode);
                float aggressiveness = Constants.GetDrivingStyleAggressiveness(_currentDrivingStyleMode);

                // CRITICAL: Validate all parameters before passing to native function
                // Invalid parameters can cause game engine crashes
                int vehicleModelHash = vehicle.Model.Hash;

                // Validate speed is within reasonable bounds
                if (_targetSpeed <= 0 || _targetSpeed > 200f || float.IsNaN(_targetSpeed) || float.IsInfinity(_targetSpeed))
                {
                    Logger.Error($"Invalid target speed: {_targetSpeed}, resetting to default");
                    _targetSpeed = Constants.AUTODRIVE_DEFAULT_SPEED;
                }

                // Validate driving style value (must be non-zero and within valid range)
                if (styleValue == 0)
                {
                    Logger.Warning($"Driving style value is 0, using NORMAL style instead");
                    styleValue = Constants.DRIVING_STYLE_NORMAL;
                }

                // Calculate distance to waypoint to determine which native to use
                float distanceToWaypoint = Vector3.Distance(player.Position, safePosition);
                bool useLongRange = distanceToWaypoint > Constants.AUTODRIVE_LONGRANGE_THRESHOLD;

                // For longrange driving, use optimized style for better pathfinding
                int effectiveStyleValue = useLongRange ? Constants.DRIVING_STYLE_LONGRANGE : styleValue;

                // Log parameters before issuing task
                Logger.Info($"StartWaypointInternal: Distance={distanceToWaypoint:F0}m, UseLongRange={useLongRange}");
                Logger.Info($"  Player Handle: {player.Handle}");
                Logger.Info($"  Vehicle Handle: {vehicle.Handle}, Model Hash: {vehicleModelHash}");
                Logger.Info($"  Destination: X={safePosition.X:F2}, Y={safePosition.Y:F2}, Z={safePosition.Z:F2}");
                Logger.Info($"  Speed: {_targetSpeed:F2} m/s, Style: {effectiveStyleValue}, Stop Range: {Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS}");

                // Issue drive task - use LONGRANGE for distant waypoints (better pathfinding)
                // VAutodrive research: LONGRANGE has simpler params and better long-distance pathing
                try
                {
                    if (useLongRange)
                    {
                        // TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE: 8 params
                        // (ped, vehicle, x, y, z, speed, drivingStyle, stopRange)
                        Function.Call(
                            _taskDriveToCoordLongrangeHash,
                            player.Handle,
                            vehicle.Handle,
                            safePosition.X,
                            safePosition.Y,
                            safePosition.Z,
                            _targetSpeed,
                            effectiveStyleValue,
                            Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS);

                        Logger.Info("StartWaypointInternal: TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE issued successfully");
                    }
                    else
                    {
                        // TASK_VEHICLE_DRIVE_TO_COORD: 11 params (for shorter distances)
                        Function.Call(
                            _taskDriveToCoordHash,
                            player.Handle,
                            vehicle.Handle,
                            safePosition.X,
                            safePosition.Y,
                            safePosition.Z,
                            _targetSpeed,
                            0,  // p6 - not used
                            vehicleModelHash,
                            effectiveStyleValue,
                            Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS,
                            0f);  // p10

                        Logger.Info("StartWaypointInternal: TASK_VEHICLE_DRIVE_TO_COORD issued successfully");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, $"StartWaypointInternal: Drive task failed (LongRange={useLongRange})");
                    _audio.Speak("Failed to start driving - task error.");
                    return;
                }

                Logger.Info("StartWaypointInternal: About to call SET_DRIVER_ABILITY");
                // Set driver ability based on style
                try
                {
                    Function.Call(
                        _setDriverAbilityHash,
                        player.Handle,
                        ability);
                    Logger.Info("StartWaypointInternal: SET_DRIVER_ABILITY completed");
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "SET_DRIVER_ABILITY failed");
                }

                Logger.Info("StartWaypointInternal: About to call SET_DRIVER_AGGRESSIVENESS");
                // Set aggressiveness based on style
                try
                {
                    Function.Call(
                        _setDriverAggressivenessHash,
                        player.Handle,
                        aggressiveness);
                    Logger.Info("StartWaypointInternal: SET_DRIVER_AGGRESSIVENESS completed");
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "SET_DRIVER_AGGRESSIVENESS failed");
                }

                Logger.Info("StartWaypointInternal: Setting state flags");

                _autoDriveActive = true;
                _wanderMode = false;
                _taskIssued = true;
                _lastWaypointPos = safePosition;  // Use safe position for tracking
                _lastDistanceToWaypoint = _navigationManager.LastDistanceToWaypoint;

                Logger.Info("StartWaypointInternal: Initializing advanced driving features");

                // Initialize advanced driving features (using extracted managers)
                _structureDetector.InitializeUturnTracking(vehicle);
                Logger.Info("StartWaypointInternal: InitializeUturnTracking completed");

                _trafficAwarenessManager.InitializeLaneTracking(vehicle);
                Logger.Info("StartWaypointInternal: InitializeLaneTracking completed");

                _roadFeatureDetector.Reset();
                Logger.Info("StartWaypointInternal: RoadFeatureDetector reset");

                _roadTypeManager.Reset();
                Logger.Info("StartWaypointInternal: RoadTypeManager reset");

                // Announce start with distance
                Logger.Info("StartWaypointInternal: Preparing announcement");
                float distanceMiles = _lastDistanceToWaypoint * Constants.METERS_TO_MILES;
                int mph = (int)(_targetSpeed * Constants.METERS_PER_SECOND_TO_MPH);

                if (distanceMiles < 0.1f)
                {
                    int feet = (int)(_lastDistanceToWaypoint * Constants.METERS_TO_FEET);
                    _audio.Speak($"AutoDrive started, navigating to waypoint {feet} feet away at {mph} miles per hour");
                }
                else
                {
                    _audio.Speak($"AutoDrive started, navigating to waypoint {distanceMiles:F1} miles away at {mph} miles per hour");
                }

                Logger.Info("StartWaypointInternal: Completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StartWaypoint");
                _audio.Speak("Failed to start AutoDrive.");
            }
        }

        /// <summary>
        /// Callback for EmergencyVehicleHandler - resumes driving after emergency passes
        /// </summary>
        private void ResumeFromEmergencyYield(Vehicle vehicle, long currentTick)
        {
            if (!_autoDriveActive || vehicle == null || !vehicle.Exists())
                return;

            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.IsInVehicle())
                    return;

                // Re-issue driving task
                if (_wanderMode)
                {
                    IssueWanderTask(player, vehicle, _targetSpeed);
                }
                else
                {
                    // Use helper method for LONGRANGE support
                    IssueDriveToCoordTask(player, vehicle, _safeArrivalPosition, _targetSpeed,
                        Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS);
                }

                _emergencyVehicleHandler.ReleaseHandbrake(vehicle);
                Logger.Debug("Resumed driving after emergency vehicle passed");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ResumeFromEmergencyYield");
            }
        }

        /// <summary>
        /// Stop autodrive
        /// </summary>
        public void Stop(bool announce = true)
        {
            if (_autoDriveActive || _taskIssued)
            {
                // Intentionally swallow - player may be invalid during cleanup
                try
                {
                    Ped player = Game.Player.Character;
                    Function.Call(_clearPedTasksHash, player.Handle);
                }
                catch { /* Expected during stop - player state may be invalid */ }
            }

            _autoDriveActive = false;
            _taskIssued = false;
            _inIntersection = false;

            // FIX: Clear mode flags to ensure consistent state
            _wanderMode = false;

            // Clear seeking state
            _seekingRoad = false;
            _seekMode = Constants.ROAD_SEEK_MODE_ANY;
            _onDesiredRoadType = false;
            _seekTargetPosition = Vector3.Zero;

            // FIX: Clear task tracking state (prevents crash when switching modes)
            _lastIssuedSeekTarget = Vector3.Zero;
            _lastIssuedTaskWasWander = false;

            // FIX: Clear deferred restart and start flags
            _pendingRestart = false;
            _pendingWaypointStart = false;

            // Clear recovery state
            ResetRecoveryState();

            // Clear slowdown states
            _curveSlowdownActive = false;
            _arrivalSlowdownActive = false;
            _lastArrivalSpeed = 0f;

            // Clear advanced driving states
            ResetAdvancedDrivingState();

            if (announce)
            {
                _audio.Speak("AutoDrive stopped");
            }
        }

        /// <summary>
        /// Increase speed by increment
        /// </summary>
        public void IncreaseSpeed()
        {
            float newSpeed = Math.Min(_targetSpeed + Constants.AUTODRIVE_SPEED_INCREMENT, Constants.AUTODRIVE_MAX_SPEED);

            if (Math.Abs(newSpeed - _targetSpeed) < 0.1f)
            {
                _audio.Speak("Maximum speed reached");
                return;
            }

            _targetSpeed = newSpeed;
            ApplySpeedChange();

            int mph = (int)(_targetSpeed * Constants.METERS_PER_SECOND_TO_MPH);
            _audio.Speak($"Speed: {mph} miles per hour");
        }

        /// <summary>
        /// Decrease speed by increment
        /// </summary>
        public void DecreaseSpeed()
        {
            float newSpeed = Math.Max(_targetSpeed - Constants.AUTODRIVE_SPEED_INCREMENT, Constants.AUTODRIVE_MIN_SPEED);

            if (Math.Abs(newSpeed - _targetSpeed) < 0.1f)
            {
                _audio.Speak("Minimum speed reached");
                return;
            }

            _targetSpeed = newSpeed;
            ApplySpeedChange();

            int mph = (int)(_targetSpeed * Constants.METERS_PER_SECOND_TO_MPH);
            _audio.Speak($"Speed: {mph} miles per hour");
        }

        /// <summary>
        /// Apply speed change without re-issuing task (smooth!)
        /// </summary>
        private void ApplySpeedChange()
        {
            if (!_autoDriveActive) return;

            try
            {
                Ped player = Game.Player.Character;
                Function.Call(
                    _setCruiseSpeedHash,
                    player.Handle,
                    _targetSpeed);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ApplySpeedChange");
            }
        }

        /// <summary>
        /// Announce current status
        /// </summary>
        public void AnnounceStatus()
        {
            if (!_autoDriveActive)
            {
                _audio.Speak("AutoDrive is not active");
                return;
            }

            // If recovering, announce that first
            if (_recoveryState != Constants.RECOVERY_STATE_NONE)
            {
                AnnounceRecoveryStatus();
                return;
            }

            int mph = (int)(_targetSpeed * Constants.METERS_PER_SECOND_TO_MPH);
            string mode = _wanderMode ? "wander mode" : "waypoint mode";
            string styleName = Constants.GetDrivingStyleName(_currentDrivingStyleMode);

            // Include seeking info if active
            string seekInfo = "";
            if (_seekingRoad && _seekMode != Constants.ROAD_SEEK_MODE_ANY)
            {
                string seekRoadName = Constants.GetRoadSeekModeName(_seekMode);
                if (_onDesiredRoadType)
                {
                    seekInfo = $", staying on {seekRoadName}";
                }
                else
                {
                    seekInfo = $", seeking {seekRoadName}";
                }
            }

            // Include recovery attempt count if we've had to recover
            string recoveryInfo = "";
            if (_recoveryAttempts > 0)
            {
                recoveryInfo = $", {_recoveryAttempts} recovery attempts";
            }

            if (_wanderMode)
            {
                _audio.Speak($"AutoDrive active, {mode}, {styleName} style{seekInfo}{recoveryInfo}, at {mph} miles per hour");
            }
            else
            {
                // Calculate current distance to waypoint
                Vector3 playerPos = Game.Player.Character.Position;
                float distance = (_lastWaypointPos - playerPos).Length();
                float distanceMiles = distance * Constants.METERS_TO_MILES;

                if (distanceMiles < 0.1f)
                {
                    int feet = (int)(distance * Constants.METERS_TO_FEET);
                    _audio.Speak($"AutoDrive active, {mode}, {styleName} style{seekInfo}{recoveryInfo}, {feet} feet to destination, {mph} miles per hour");
                }
                else
                {
                    _audio.Speak($"AutoDrive active, {mode}, {styleName} style{seekInfo}{recoveryInfo}, {distanceMiles:F1} miles to destination, {mph} miles per hour");
                }
            }
        }

        /// <summary>
        /// Update autodrive state - called from OnTick
        /// </summary>
        public void Update(Vehicle vehicle, Vector3 position, long currentTick)
        {
            // FIX: Handle deferred waypoint start BEFORE checking _autoDriveActive
            // This flag is set when Stop() is called from StartWaypoint()
            // We must wait one frame after Stop() before issuing the new drive task
            if (_pendingWaypointStart)
            {
                _pendingWaypointStart = false;
                Logger.Info("Executing deferred waypoint start (one frame after Stop)");
                StartWaypointInternal();
                return;
            }

            if (!_autoDriveActive) return;

            // FIX: Handle deferred restart from waypoint change (prevents crash)
            // Must happen at the start of the frame, not during Update() execution
            if (_pendingRestart)
            {
                _pendingRestart = false;
                Logger.Info("Executing deferred restart after waypoint change");
                StartWaypoint();
                return;
            }

            // CRITICAL: Validate vehicle exists before any operations
            // Vehicle can become null if destroyed, player exits, or game state changes
            if (vehicle == null || !vehicle.Exists())
            {
                Stop();
                return;
            }

            // Defensive: Validate position (guard against NaN/Infinity)
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
                float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
            {
                Logger.Warning("AutoDriveManager.Update: invalid position, skipping update");
                return;
            }

            // Defensive: Guard against invalid tick values
            if (currentTick < 0)
                return;

            // Skip updates while paused (except emergency check for resume)
            if (_pauseState == Constants.PAUSE_STATE_PAUSED)
            {
                // Still check emergency vehicles even while paused (to resume when they pass)
                if (_emergencyVehicleHandler.IsYieldingToEmergency)
                {
                    _emergencyVehicleHandler.CheckEmergencyVehicles(vehicle, position, currentTick,
                        _currentDrivingStyleMode, ResumeFromEmergencyYield);
                }
                return;
            }

            // Check if we're still in a valid driving state
            Ped player = Game.Player.Character;
            if (player == null || !player.IsInVehicle() || player.SeatIndex != VehicleSeat.Driver)
            {
                Stop();
                return;
            }

            // === RECOVERY SYSTEM CHECKS ===

            // Check vehicle state (flipped, water, fire, damage)
            if (CheckVehicleState(vehicle, currentTick))
            {
                // Critical vehicle state - already handled
                return;
            }

            // If in recovery mode, process recovery actions
            if (_recoveryState != Constants.RECOVERY_STATE_NONE)
            {
                UpdateRecovery(player, vehicle, position, currentTick);
                return;
            }

            // Check if stuck
            CheckIfStuck(vehicle, position, currentTick);

            // If stuck was detected, start recovery
            if (_isStuck)
            {
                StartRecovery(player, vehicle, currentTick);
                return;
            }

            // Check progress timeout (waypoint mode only)
            if (!_wanderMode)
            {
                CheckProgressTimeout(position, currentTick);
            }

            // === ADVANCED DRIVING FEATURES (using extracted managers) ===

            // Check traffic light state (still inline - consider extracting to RoadFeatureDetector)
            CheckTrafficLightState(vehicle, position, currentTick);

            // Check for U-turns (delegated to StructureDetector)
            _structureDetector.CheckUturn(vehicle, position, currentTick);

            // Check hill/gradient (delegated to StructureDetector)
            _structureDetector.CheckHillGradient(vehicle, position, currentTick);

            // === ENVIRONMENTAL AWARENESS (using extracted managers) ===

            // Check weather conditions (still inline - uses _weatherManager)
            CheckWeather(vehicle, currentTick);

            // Check time of day (delegated to EnvironmentalManager)
            _environmentalManager.CheckTimeOfDay(vehicle, currentTick);

            // Check for vehicles ahead (collision warning - still inline)
            CheckCollisionWarning(vehicle, position, currentTick);

            // Check following distance (delegated to TrafficAwarenessManager)
            _trafficAwarenessManager.CheckFollowingDistance(vehicle, currentTick, _targetSpeed,
                _roadTypeSpeedMultiplier, _weatherManager.SpeedMultiplier, _timeSpeedMultiplier,
                _currentDrivingStyleMode, _taskIssued);

            // Check for emergency vehicles (delegated to EmergencyVehicleHandler)
            _yieldingToEmergency = _emergencyVehicleHandler.CheckEmergencyVehicles(vehicle, position, currentTick,
                _currentDrivingStyleMode, ResumeFromEmergencyYield);

            // Check for tunnels/bridges (delegated to StructureDetector)
            _structureDetector.CheckStructures(vehicle, position, currentTick, _currentRoadType);

            // Update ETA (waypoint mode only, delegated to ETACalculator)
            if (!_wanderMode)
            {
                _etaCalculator.UpdateETA(vehicle, position, _lastWaypointPos, currentTick, _wanderMode);
            }

            // === LANE CHANGE AND OVERTAKING (using extracted managers) ===

            // Check for lane changes (delegated to TrafficAwarenessManager)
            _trafficAwarenessManager.CheckLaneChange(vehicle, position, currentTick);

            // Check for overtaking maneuvers (delegated to TrafficAwarenessManager)
            _trafficAwarenessManager.CheckOvertaking(vehicle, position, currentTick);

            // === ROAD FEATURES (using extracted managers) ===

            // Check road features - curves, intersections, traffic lights (delegated to RoadFeatureDetector)
            _roadFeatureDetector.Update(vehicle, position, currentTick, _targetSpeed,
                _currentDrivingStyleMode, _autoDriveActive);

            // Update curve slowdown state from detector
            _curveSlowdownActive = _roadFeatureDetector.IsCurveSlowdownActive;
            _curveSlowdownSpeed = _roadFeatureDetector.CurveSlowdownSpeed;

            // === ROAD TYPE MANAGEMENT (using extracted managers) ===

            // Check road type changes (delegated to RoadTypeManager)
            _currentRoadType = _roadTypeManager.GetRoadTypeAtPosition(position);
            _roadTypeManager.CheckRoadTypeChange(position, currentTick, true, _targetSpeed,
                _curveSlowdownActive, _arrivalSlowdownActive);

            // Apply road type speed adjustment
            _roadTypeManager.ApplyRoadTypeSpeedAdjustment(_currentRoadType, _targetSpeed,
                _curveSlowdownActive, _arrivalSlowdownActive);
            _roadTypeSpeedMultiplier = _roadTypeManager.RoadTypeSpeedMultiplier;

            // === NORMAL OPERATION ===

            // Waypoint mode: check for arrival and distance updates (delegated to NavigationManager)
            if (!_wanderMode)
            {
                bool shouldStop, shouldRestart;
                bool stillNavigating = _navigationManager.UpdateProgress(position, _targetSpeed,
                    _curveSlowdownActive, currentTick, out shouldStop, out shouldRestart);

                // Sync arrival slowdown state from manager
                _arrivalSlowdownActive = _navigationManager.IsArrivalSlowdownActive;

                if (shouldStop)
                {
                    Stop(false);
                    return;
                }

                if (shouldRestart)
                {
                    // FIX: Defer restart to next frame to prevent crash
                    // Calling StartWaypoint() here while Update() is running causes native task conflicts
                    _pendingRestart = true;
                    Logger.Info("Waypoint changed, deferring restart to next frame");
                    return;
                }
            }
        }

        /// <summary>
        /// Check waypoint progress and arrival
        /// Now includes smooth arrival deceleration
        /// </summary>
        private void UpdateWaypointProgress(Vector3 position)
        {
            // Check if waypoint was removed
            if (!Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
            {
                _audio.Speak("Waypoint removed. AutoDrive stopping.");
                EndArrivalSlowdown();
                Stop(false);
                return;
            }

            // Check if waypoint moved (compare with original position)
            Vector3 currentWaypoint = World.WaypointPosition;
            if ((currentWaypoint - _originalWaypointPos).Length() > Constants.WAYPOINT_MOVED_THRESHOLD)
            {
                // Waypoint moved significantly - restart navigation
                _audio.Speak("Waypoint moved. Recalculating route.");
                _inFinalApproach = false;
                EndArrivalSlowdown();
                StartWaypoint();
                return;
            }

            // Check distance to both safe position and original waypoint
            float distanceToSafe = (_safeArrivalPosition - position).Length();
            float distanceToOriginal = (_originalWaypointPos - position).Length();

            // Use the closer distance for arrival checks (game might expect original position)
            float distance = Math.Min(distanceToSafe, distanceToOriginal);

            // === FINAL APPROACH PHASE ===
            // When close to destination, enter final approach for precise arrival
            if (!_inFinalApproach && distance < Constants.AUTODRIVE_FINAL_APPROACH_DISTANCE)
            {
                _inFinalApproach = true;
                if (Logger.IsDebugEnabled) Logger.Debug($"Entering final approach at {distance:F0}m");

                // Reduce speed for final approach
                Ped player = Game.Player.Character;
                Function.Call(
                    _setCruiseSpeedHash,
                    player.Handle,
                    Constants.AUTODRIVE_FINAL_APPROACH_SPEED);
            }

            // Check for arrival - use precise radius for close approach
            float arrivalRadius = _inFinalApproach ?
                Constants.AUTODRIVE_PRECISE_ARRIVAL_RADIUS :
                Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS;

            if (distance < arrivalRadius)
            {
                _audio.Speak("You have arrived at your destination.");
                _inFinalApproach = false;
                EndArrivalSlowdown();
                Stop(false);
                return;
            }

            // === SMOOTH ARRIVAL DECELERATION ===
            // Gradually slow down as we approach the destination
            if (distance <= Constants.ARRIVAL_SLOWDOWN_DISTANCE)
            {
                // Calculate target speed based on distance: closer = slower
                // Speed = max(finalSpeed, distance * factor)
                float targetArrivalSpeed = Math.Max(
                    Constants.ARRIVAL_FINAL_SPEED,
                    distance * Constants.ARRIVAL_SPEED_FACTOR);

                // Only adjust if speed changed significantly (avoid spamming the native)
                if (!_arrivalSlowdownActive || Math.Abs(targetArrivalSpeed - _lastArrivalSpeed) > 1f)
                {
                    if (!_arrivalSlowdownActive)
                    {
                        _arrivalSlowdownActive = true;
                        _audio.Speak("Approaching destination");
                        if (Logger.IsDebugEnabled) Logger.Debug($"Arrival slowdown started at {distance:F0}m");
                    }

                    _lastArrivalSpeed = targetArrivalSpeed;
                    ApplyArrivalSpeed(targetArrivalSpeed);
                }
            }
            else if (_arrivalSlowdownActive)
            {
                // We moved away from destination (waypoint changed?), end slowdown
                EndArrivalSlowdown();
            }

            // Announce distance milestones
            float distanceMiles = distance * Constants.METERS_TO_MILES;
            float lastMiles = _lastDistanceToWaypoint * Constants.METERS_TO_MILES;
            long currentTick = DateTime.Now.Ticks;

            // Use granular announcements for close distances (under 0.5 miles / ~800m)
            if (distanceMiles < 0.5f)
            {
                CheckGranularArrivalAnnouncements(distance, currentTick);
            }
            // Use 0.5 mile intervals for longer distances
            else if ((int)(lastMiles * 2) > (int)(distanceMiles * 2))
            {
                // Crossed a 0.5 mile boundary
                TryAnnounce($"{distanceMiles:F1} miles to destination",
                    Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
            }

            _lastDistanceToWaypoint = distance;
        }

        /// <summary>
        /// Apply arrival speed to the driving task
        /// </summary>
        private void ApplyArrivalSpeed(float speed)
        {
            try
            {
                Ped player = Game.Player.Character;
                Function.Call(
                    _setCruiseSpeedHash,
                    player.Handle,
                    speed);

                if (Logger.IsDebugEnabled) Logger.Debug($"Arrival speed adjusted to {speed:F1} m/s");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ApplyArrivalSpeed");
            }
        }

        /// <summary>
        /// End arrival slowdown and restore normal speed
        /// </summary>
        private void EndArrivalSlowdown()
        {
            if (!_arrivalSlowdownActive) return;

            _arrivalSlowdownActive = false;
            _lastArrivalSpeed = 0f;

            // Only restore if still driving (not at destination)
            if (_autoDriveActive && !_curveSlowdownActive)
            {
                try
                {
                    Ped player = Game.Player.Character;
                    Function.Call(
                        _setCruiseSpeedHash,
                        player.Handle,
                        _targetSpeed);

                    if (Logger.IsDebugEnabled) Logger.Debug($"Arrival slowdown ended, restored to {_targetSpeed:F1} m/s");
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "EndArrivalSlowdown");
                }
            }
        }

        /// <summary>
        /// Check and announce road features - called from OnTick when enabled
        /// Now includes dynamic lookahead, speed-scaled cooldowns, and proactive curve slowdown
        /// </summary>
        public void CheckRoadFeatures(Vehicle vehicle, Vector3 position, long currentTick)
        {
            // Defensive: Validate vehicle parameter
            if (vehicle == null || !vehicle.Exists())
                return;

            // Defensive: Validate position (guard against NaN/Infinity)
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
                float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
                return;

            // Skip if vehicle is slow or stopped
            float speed = vehicle.Speed;
            if (speed < Constants.ROAD_FEATURE_MIN_SPEED) return;

            // Check if curve slowdown should expire
            if (_curveSlowdownActive && currentTick > _curveSlowdownEndTick)
            {
                EndCurveSlowdown();
            }

            float vehicleHeading = vehicle.Heading;

            // Dynamic lookahead based on speed: faster = look further ahead
            float lookaheadDistance = Math.Min(
                Constants.ROAD_LOOKAHEAD_MAX,
                Math.Max(Constants.ROAD_LOOKAHEAD_MIN, speed * Constants.ROAD_LOOKAHEAD_SPEED_FACTOR));

            // Speed-scaled cooldown: faster = more frequent announcements
            long cooldown = CalculateSpeedScaledCooldown(speed);

            // Look ahead at multiple distances and check for curves/intersections
            // PERFORMANCE: Calculate radians once outside the loop, use pre-calculated DEG_TO_RAD
            float radians = (90f - vehicleHeading) * Constants.DEG_TO_RAD;
            float cosRad = (float)Math.Cos(radians);
            float sinRad = (float)Math.Sin(radians);

            for (float distance = Constants.ROAD_SAMPLE_INTERVAL; distance <= lookaheadDistance; distance += Constants.ROAD_SAMPLE_INTERVAL)
            {
                // Calculate look-ahead position using pre-calculated cos/sin
                Vector3 lookAheadPos = position + new Vector3(
                    cosRad * distance,
                    sinRad * distance,
                    0f);

                // Get road node at look-ahead position
                bool found = Function.Call<bool>(
                    _getClosestNodeWithHeadingHash,
                    lookAheadPos.X, lookAheadPos.Y, lookAheadPos.Z,
                    _nodePos, _nodeHeading, 1, 3f, 0f);

                if (!found) continue;

                float roadHeading = _nodeHeading.GetResult<float>();

                // Enhanced curve detection with better prediction and classification
                CurveInfo curveInfo = AnalyzeCurveCharacteristics(vehicleHeading, roadHeading, distance, speed);

                if (curveInfo.Severity != CurveSeverity.None)
                {
                    if (currentTick - _lastCurveAnnounceTick > cooldown)
                    {
                        _lastCurveAnnounceTick = currentTick;
                        string announcement = GenerateCurveAnnouncement(curveInfo, distance);
                        _audio.Speak(announcement);

                        // Speed-dependent slowdown distance for curves
                        // At higher speeds, need more distance to safely slow down
                        float curveSlowdownDistance = CalculateCurveSlowdownDistance(speed, curveInfo.Severity);

                        // Intelligent slowdown based on curve characteristics
                        if (_autoDriveActive && distance <= curveSlowdownDistance && !_curveSlowdownActive)
                        {
                            ApplyIntelligentCurveSlowdown(curveInfo, speed, currentTick);
                        }

                        return; // Only announce one feature per check
                    }
                }

                // Check for intersections and traffic lights
                Vector3 nodePosition = _nodePos.GetResult<Vector3>();
                Function.Call(
                    _getVehicleNodePropsHash,
                    nodePosition.X, nodePosition.Y, nodePosition.Z,
                    _density, _flags);

                int nodeFlags = _flags.GetResult<int>();

                // Traffic light check (flag bit 8 = 256)
                bool hasTrafficLight = (nodeFlags & 256) != 0;
                if (hasTrafficLight && currentTick - _lastTrafficLightAnnounceTick > cooldown)
                {
                    _lastTrafficLightAnnounceTick = currentTick;
                    string distanceText = FormatDistance(distance);
                    _audio.Speak($"Traffic light ahead, {distanceText}");
                    return;
                }

                // Junction/intersection check (flag bit 7 = 128)
                bool isJunction = (nodeFlags & 128) != 0;
                if (isJunction && !hasTrafficLight && currentTick - _lastIntersectionAnnounceTick > cooldown)
                {
                    _lastIntersectionAnnounceTick = currentTick;
                    string distanceText = FormatDistance(distance);
                    _audio.Speak($"Intersection ahead, {distanceText}");
                    return;
                }
            }

            // Track intersection entry/exit for turn direction announcement
            UpdateIntersectionTracking(vehicle, position);
        }

        /// <summary>
        /// Calculate cooldown based on speed - faster speeds get shorter cooldowns
        /// </summary>
        private long CalculateSpeedScaledCooldown(float speed)
        {
            if (speed >= Constants.ROAD_FEATURE_COOLDOWN_SPEED_THRESHOLD)
            {
                return Constants.ROAD_FEATURE_COOLDOWN_MIN;
            }

            // Linear interpolation between max and min cooldown
            float speedRatio = speed / Constants.ROAD_FEATURE_COOLDOWN_SPEED_THRESHOLD;
            long cooldownRange = Constants.ROAD_FEATURE_COOLDOWN_MAX - Constants.ROAD_FEATURE_COOLDOWN_MIN;
            return Constants.ROAD_FEATURE_COOLDOWN_MAX - (long)(cooldownRange * speedRatio);
        }

        /// <summary>
        /// Calculate speed-dependent slowdown distance for curves
        /// At higher speeds, we need more distance to safely decelerate
        /// </summary>
        private float CalculateCurveSlowdownDistance(float speed, CurveSeverity severity)
        {
            // Base distance + speed factor
            // At 40 m/s (90 mph), we need at least 160m (4 seconds) to slow down safely
            float baseDistance = Constants.CURVE_SLOWDOWN_DISTANCE_BASE;
            float speedBasedDistance = speed * Constants.CURVE_SLOWDOWN_DISTANCE_SPEED_FACTOR;

            // Sharper curves need even more distance
            float severityMultiplier = 1.0f;
            switch (severity)
            {
                case CurveSeverity.Gentle:
                    severityMultiplier = 0.8f;  // Less distance needed for gentle curves
                    break;
                case CurveSeverity.Moderate:
                    severityMultiplier = 1.0f;  // Standard
                    break;
                case CurveSeverity.Sharp:
                    severityMultiplier = 1.3f;  // More distance for sharp curves
                    break;
                case CurveSeverity.Hairpin:
                    severityMultiplier = 1.6f;  // Maximum distance for hairpins
                    break;
            }

            float calculatedDistance = Math.Max(baseDistance, speedBasedDistance) * severityMultiplier;

            // Clamp to reasonable bounds
            return Math.Max(
                Constants.CURVE_SLOWDOWN_DISTANCE_MIN,
                Math.Min(Constants.CURVE_SLOWDOWN_DISTANCE_MAX, calculatedDistance));
        }

        /// <summary>
        /// Start proactive slowdown for upcoming curve
        /// Combines all environmental multipliers for proper speed calculation
        /// </summary>
        private void StartCurveSlowdown(float slowdownFactor, long currentTick)
        {
            if (!_autoDriveActive) return;

            try
            {
                // Calculate combined environmental multiplier (weather, road type, time of day)
                float combinedMultiplier = _roadTypeSpeedMultiplier * _weatherSpeedMultiplier * _timeSpeedMultiplier;

                // Apply combined multipliers to base speed, then apply curve slowdown
                float environmentalSpeed = _targetSpeed * combinedMultiplier;
                _originalSpeed = environmentalSpeed;
                _curveSlowdownSpeed = environmentalSpeed * slowdownFactor;
                _curveSlowdownActive = true;
                _curveSlowdownEndTick = currentTick + Constants.CURVE_SLOWDOWN_DURATION;

                // Apply reduced speed
                Ped player = Game.Player.Character;
                Function.Call(
                    _setCruiseSpeedHash,
                    player.Handle,
                    _curveSlowdownSpeed);

                if (Logger.IsDebugEnabled) Logger.Debug($"Curve slowdown: {_originalSpeed:F1} -> {_curveSlowdownSpeed:F1} m/s (combined mult: {combinedMultiplier:P0})");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StartCurveSlowdown");
            }
        }

        /// <summary>
        /// End curve slowdown and restore original speed
        /// </summary>
        private void EndCurveSlowdown()
        {
            if (!_curveSlowdownActive) return;

            _curveSlowdownActive = false;
            Logger.Debug($"Curve slowdown ended, speed restored");
        }

        /// <summary>
        /// Analyze curve characteristics for better prediction and handling
        /// Considers angle, estimated radius, and safe speed calculations
        /// </summary>
        private CurveInfo AnalyzeCurveCharacteristics(float vehicleHeading, float roadHeading, float distance, float currentSpeed)
        {
            float headingDiff = NormalizeAngleDiff(roadHeading - vehicleHeading);
            float absAngle = Math.Abs(headingDiff);

            // Classify curve severity based on turn angle
            CurveSeverity severity;
            if (absAngle < Constants.CURVE_HEADING_THRESHOLD)
                severity = CurveSeverity.None;
            else if (absAngle < 25f)
                severity = CurveSeverity.Gentle;
            else if (absAngle < 45f)
                severity = CurveSeverity.Moderate;
            else if (absAngle < 90f)
                severity = CurveSeverity.Sharp;
            else
                severity = CurveSeverity.Hairpin;

            if (severity == CurveSeverity.None)
                return new CurveInfo(severity, CurveDirection.Right, 0, 0, currentSpeed);

            // Estimate curve radius using geometry
            // For a given turn angle and distance, estimate the curve radius
            // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
            float curveRadius = distance / (float)Math.Tan(absAngle * Constants.DEG_TO_RAD * 0.5f);

            // Calculate safe speed using physics: v = sqrt(μ * g * r)
            // Using coefficient of friction estimates for different road conditions
            float frictionCoeff = GetRoadFrictionCoefficient();
            float safeSpeed = (float)Math.Sqrt(frictionCoeff * 9.81f * curveRadius);

            // Adjust for driving style
            safeSpeed *= GetCurveSpeedModifier();

            // Cap at reasonable maximum (don't go slower than walking speed for very sharp curves)
            safeSpeed = Math.Max(2f, Math.Min(safeSpeed, currentSpeed * 1.2f));

            CurveDirection direction = headingDiff > 0 ? CurveDirection.Right : CurveDirection.Left;

            return new CurveInfo(severity, direction, absAngle, curveRadius, safeSpeed);
        }

        /// <summary>
        /// Get road friction coefficient based on current conditions
        /// </summary>
        private float GetRoadFrictionCoefficient()
        {
            // Base friction for dry asphalt
            float friction = 0.8f;

            // Weather adjustments
            switch (_currentWeatherHash)
            {
                case Constants.WEATHER_RAIN:
                case Constants.WEATHER_CLEARING:
                    friction *= 0.7f; // Wet roads
                    break;
                case Constants.WEATHER_THUNDER:
                    friction *= 0.6f; // Heavy rain
                    break;
                case Constants.WEATHER_SNOW:
                case Constants.WEATHER_SNOWLIGHT:
                    friction *= 0.3f; // Snow/ice
                    break;
                case Constants.WEATHER_BLIZZARD:
                    friction *= 0.2f; // Blizzard conditions
                    break;
            }

            return friction;
        }

        /// <summary>
        /// Get speed modifier for curves based on driving style
        /// </summary>
        private float GetCurveSpeedModifier()
        {
            switch (_currentDrivingStyleMode)
            {
                case Constants.DRIVING_STYLE_MODE_CAUTIOUS:
                    return 0.8f; // Much more conservative
                case Constants.DRIVING_STYLE_MODE_NORMAL:
                    return 0.9f; // Slightly conservative
                case Constants.DRIVING_STYLE_MODE_FAST:
                    return 1.0f; // Standard physics
                case Constants.DRIVING_STYLE_MODE_RECKLESS:
                    return 1.1f; // Willing to take corners faster
                default:
                    return 0.9f;
            }
        }

        /// <summary>
        /// Generate appropriate curve announcement based on characteristics
        /// </summary>
        private string GenerateCurveAnnouncement(CurveInfo curveInfo, float distance)
        {
            string distanceText = FormatDistance(distance);
            string direction = curveInfo.Direction == CurveDirection.Left ? "left" : "right";

            switch (curveInfo.Severity)
            {
                case CurveSeverity.Gentle:
                    return $"Gentle curve {direction}, {distanceText}";
                case CurveSeverity.Moderate:
                    return $"Curve {direction} ahead, {distanceText}";
                case CurveSeverity.Sharp:
                    return $"Sharp curve {direction} ahead, {distanceText}";
                case CurveSeverity.Hairpin:
                    return $"Hairpin turn {direction} ahead, {distanceText}";
                default:
                    return $"Curve {direction} ahead, {distanceText}";
            }
        }

        /// <summary>
        /// Apply intelligent curve slowdown based on physics and curve characteristics
        /// </summary>
        private void ApplyIntelligentCurveSlowdown(CurveInfo curveInfo, float currentSpeed, long currentTick)
        {
            if (curveInfo.SafeSpeed >= currentSpeed * 0.9f)
                return; // No need to slow down significantly

            // Calculate slowdown factor based on speed difference
            float slowdownFactor = curveInfo.SafeSpeed / currentSpeed;
            slowdownFactor = Math.Max(0.3f, Math.Min(1.0f, slowdownFactor)); // Reasonable bounds

            StartCurveSlowdown(slowdownFactor, currentTick);
        }

        /// <summary>
        /// Track when entering and exiting intersections to announce turn direction
        /// </summary>
        private void UpdateIntersectionTracking(Vehicle vehicle, Vector3 position)
        {
            // Get road node properties at current position
            Function.Call(
                _getVehicleNodePropsHash,
                position.X, position.Y, position.Z,
                _density, _flags);

            int nodeFlags = _flags.GetResult<int>();
            bool atJunction = (nodeFlags & 128) != 0;

            if (atJunction && !_inIntersection)
            {
                // Entering intersection
                _inIntersection = true;
                _preIntersectionHeading = vehicle.Heading;
                _intersectionPosition = position;
            }
            else if (_inIntersection && !atJunction)
            {
                // Exited intersection - check turn direction
                _inIntersection = false;
                float headingChange = NormalizeAngleDiff(vehicle.Heading - _preIntersectionHeading);

                string direction;
                if (Math.Abs(headingChange) < Constants.TURN_HEADING_SLIGHT)
                {
                    direction = "Went straight";
                }
                else if (headingChange > 60f && headingChange < 120f)
                {
                    direction = "Turned right";
                }
                else if (headingChange < -60f && headingChange > -120f)
                {
                    direction = "Turned left";
                }
                else if (Math.Abs(headingChange) > Constants.TURN_HEADING_UTURN)
                {
                    direction = "Made U-turn";
                }
                else if (headingChange > 0)
                {
                    direction = "Bore right";
                }
                else
                {
                    direction = "Bore left";
                }

                _audio.Speak(direction);
            }
        }

        /// <summary>
        /// Format distance for speech in imperial units
        /// </summary>
        private string FormatDistance(float meters)
        {
            float feet = meters * Constants.METERS_TO_FEET;

            if (feet < 528) // Less than 0.1 miles
            {
                // Round to nearest 50 feet
                int roundedFeet = ((int)(feet / 50) + 1) * 50;
                return $"{roundedFeet} feet";
            }
            else if (feet < 1320) // 0.1 to 0.25 miles
            {
                return "quarter mile";
            }
            else if (feet < 2640) // 0.25 to 0.5 miles
            {
                return "half mile";
            }
            else
            {
                float miles = meters * Constants.METERS_TO_MILES;
                return $"{miles:F1} miles";
            }
        }

        /// <summary>
        /// Normalize angle difference to -180 to 180 range
        /// </summary>
        private float NormalizeAngleDiff(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        #region Advanced Driving Features

        /// <summary>
        /// Prioritized announcement - delegates to AnnouncementQueue for proper throttling and settings support
        /// </summary>
        private bool TryAnnounce(string message, int priority, long currentTick, string settingName = null)
        {
            // Delegate to AnnouncementQueue which handles priority, throttling, and settings
            return _announcementQueue.TryAnnounce(message, priority, currentTick, settingName);
        }

        /// <summary>
        /// Calculate effective cruise speed considering all modifiers
        /// </summary>
        private float GetEffectiveCruiseSpeed()
        {
            float speed = _targetSpeed;

            // Apply road type multiplier
            speed *= _roadTypeSpeedMultiplier;

            // Curve slowdown takes precedence
            if (_curveSlowdownActive)
            {
                speed = _curveSlowdownSpeed;
            }

            return speed;
        }

        /// <summary>
        /// Apply road type speed adjustment when road type changes
        /// </summary>
        public void ApplyRoadTypeSpeedAdjustment(int roadType, long currentTick)
        {
            if (!_autoDriveActive) return;
            if (roadType == _lastSpeedAdjustedRoadType) return;
            if (roadType < 0 || roadType >= Constants.ROAD_TYPE_SPEED_MULTIPLIERS.Length) return;

            float newMultiplier = Constants.GetRoadTypeSpeedMultiplier(roadType);

            // Only announce if multiplier changed significantly
            if (Math.Abs(newMultiplier - _roadTypeSpeedMultiplier) > 0.1f)
            {
                _roadTypeSpeedMultiplier = newMultiplier;
                _lastSpeedAdjustedRoadType = roadType;

                // Apply new speed (unless curve or arrival slowdown active)
                if (!_curveSlowdownActive && !_arrivalSlowdownActive)
                {
                    try
                    {
                        Ped player = Game.Player.Character;
                        // Use combined multipliers (weather, road type, time of day)
                        float combinedMultiplier = _roadTypeSpeedMultiplier * _weatherSpeedMultiplier * _timeSpeedMultiplier;
                        float adjustedSpeed = _targetSpeed * combinedMultiplier;
                        Function.Call(
                            _setCruiseSpeedHash,
                            player.Handle,  // Must use .Handle for native calls
                            adjustedSpeed);

                        int mph = (int)(adjustedSpeed * Constants.METERS_PER_SECOND_TO_MPH);
                        string roadName = Constants.GetRoadTypeName(roadType);
                        TryAnnounce($"Adjusting speed for {roadName}, {mph} miles per hour",
                            Constants.ANNOUNCE_PRIORITY_LOW, currentTick);

                        if (Logger.IsDebugEnabled) Logger.Debug($"Road type speed: {roadName} -> {adjustedSpeed:F1} m/s (combined: {combinedMultiplier:P0})");
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "ApplyRoadTypeSpeedAdjustment");
                    }
                }
            }
        }

        /// <summary>
        /// Check and announce traffic light state (stopped/proceeding)
        /// </summary>
        public void CheckTrafficLightState(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Cooldown check
            if (currentTick - _lastTrafficLightStateTick < Constants.TRAFFIC_LIGHT_STATE_COOLDOWN)
                return;

            float speed = vehicle.Speed;

            // Check if we just stopped
            if (!_stoppedAtLight && speed < Constants.TRAFFIC_LIGHT_STOP_SPEED)
            {
                // Check if there's a traffic light nearby
                if (IsNearTrafficLight(position))
                {
                    _stoppedAtLight = true;
                    _trafficLightStopPosition = position;
                    _lastTrafficLightStateTick = currentTick;
                    TryAnnounce("Stopping at traffic light", Constants.ANNOUNCE_PRIORITY_HIGH, currentTick);
                }
            }
            // Check if we started moving after being stopped at light
            else if (_stoppedAtLight && speed > Constants.TRAFFIC_LIGHT_STOP_SPEED * 3)
            {
                // Verify we were actually at a light and are now moving
                float distanceFromStop = (position - _trafficLightStopPosition).Length();
                if (distanceFromStop > 5f)
                {
                    _stoppedAtLight = false;
                    _lastTrafficLightStateTick = currentTick;
                    TryAnnounce("Proceeding through intersection", Constants.ANNOUNCE_PRIORITY_HIGH, currentTick);
                }
            }
        }

        /// <summary>
        /// Check if near a traffic light
        /// </summary>
        private bool IsNearTrafficLight(Vector3 position)
        {
            try
            {
                // Get road node properties
                Function.Call(
                    _getVehicleNodePropsHash,
                    position.X, position.Y, position.Z,
                    _density, _flags);

                int nodeFlags = _flags.GetResult<int>();
                return (nodeFlags & 256) != 0;  // Traffic light flag
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detect and announce U-turns
        /// </summary>
        public void CheckUturn(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Cooldown check
            if (currentTick - _lastUturnAnnounceTick < Constants.UTURN_ANNOUNCE_COOLDOWN)
                return;

            float currentHeading = vehicle.Heading;
            float distance = (position - _uturnTrackingPosition).Length();

            // Check if we've traveled enough distance to evaluate
            if (distance >= Constants.UTURN_DISTANCE_THRESHOLD)
            {
                float headingChange = Math.Abs(NormalizeAngleDiff(currentHeading - _uturnTrackingHeading));

                if (headingChange >= Constants.UTURN_HEADING_THRESHOLD)
                {
                    _lastUturnAnnounceTick = currentTick;
                    TryAnnounce("Making U-turn", Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                }

                // Reset tracking
                _uturnTrackingPosition = position;
                _uturnTrackingHeading = currentHeading;
            }
        }

        /// <summary>
        /// Initialize U-turn tracking when autodrive starts
        /// </summary>
        private void InitializeUturnTracking(Vehicle vehicle)
        {
            _uturnTrackingPosition = vehicle.Position;
            _uturnTrackingHeading = vehicle.Heading;
        }

        /// <summary>
        /// Check and announce hills/gradients
        /// </summary>
        public void CheckHillGradient(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Cooldown check
            if (currentTick - _lastHillAnnounceTick < Constants.HILL_ANNOUNCE_COOLDOWN)
                return;

            try
            {
                // Get vehicle pitch (negative = going uphill, positive = going downhill in GTA V)
                float pitch = vehicle.Rotation.X;

                // Check for significant gradient
                if (Math.Abs(pitch) >= Constants.HILL_STEEP_THRESHOLD)
                {
                    if (!_announcedCurrentHill || Math.Abs(pitch - _lastHillGradient) > 3f)
                    {
                        _announcedCurrentHill = true;
                        _lastHillGradient = pitch;
                        _lastHillAnnounceTick = currentTick;

                        string hillType = pitch < 0 ? "Steep uphill" : "Steep downhill";
                        TryAnnounce(hillType, Constants.ANNOUNCE_PRIORITY_HIGH, currentTick);
                    }
                }
                else if (Math.Abs(pitch) >= Constants.HILL_MODERATE_THRESHOLD)
                {
                    if (!_announcedCurrentHill || Math.Abs(pitch - _lastHillGradient) > 3f)
                    {
                        _announcedCurrentHill = true;
                        _lastHillGradient = pitch;
                        _lastHillAnnounceTick = currentTick;

                        string hillType = pitch < 0 ? "Uphill grade" : "Downhill grade";
                        TryAnnounce(hillType, Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                    }
                }
                else
                {
                    // Reset when on flat ground
                    if (_announcedCurrentHill && Math.Abs(pitch) < Constants.HILL_MODERATE_THRESHOLD - 1f)
                    {
                        _announcedCurrentHill = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckHillGradient");
            }
        }

        /// <summary>
        /// Check for granular arrival distance announcements
        /// </summary>
        private void CheckGranularArrivalAnnouncements(float distanceMeters, long currentTick)
        {
            int distanceFeet = (int)(distanceMeters * Constants.METERS_TO_FEET);

            // Check each milestone
            foreach (int milestone in Constants.ARRIVAL_ANNOUNCEMENT_DISTANCES)
            {
                // Announce when crossing a milestone (going from above to below)
                if (distanceFeet <= milestone && _lastAnnouncedArrivalDistance > milestone)
                {
                    _lastAnnouncedArrivalDistance = milestone;
                    TryAnnounce($"{milestone} feet to destination", Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                    return;  // Only one announcement per check
                }
            }

            // Track distance for next check
            if (distanceFeet > _lastAnnouncedArrivalDistance)
            {
                _lastAnnouncedArrivalDistance = distanceFeet;
            }
        }

        /// <summary>
        /// Reset all advanced driving feature states
        /// </summary>
        private void ResetAdvancedDrivingState()
        {
            _lastAnnouncedArrivalDistance = int.MaxValue;
            _inFinalApproach = false;
            _safeArrivalPosition = Vector3.Zero;
            _originalWaypointPos = Vector3.Zero;
            _roadTypeSpeedMultiplier = 1.0f;
            _lastSpeedAdjustedRoadType = -1;
            _stoppedAtLight = false;
            _lastTrafficLightStateTick = 0;
            _uturnTrackingPosition = Vector3.Zero;
            _uturnTrackingHeading = 0f;
            _lastUturnAnnounceTick = 0;
            _lastHillAnnounceTick = 0;
            _announcedCurrentHill = false;
            _lastHillGradient = 0f;

            // Reset weather state
            _weatherSpeedMultiplier = 1.0f;
            _weatherAnnounced = false;
            _lastWeatherCheckTick = 0;

            // Reset collision warning
            _lastVehicleAheadDistance = float.MaxValue;
            _lastCollisionWarningLevel = 0;
            _lastCollisionCheckTick = 0;
            _lastCollisionAnnounceTick = 0;

            // Reset time-of-day
            _timeSpeedMultiplier = 1.0f;
            _lastTimeCheckTick = 0;
            _headlightsOn = false;

            // Reset emergency vehicle
            _yieldingToEmergency = false;
            _emergencyYieldStartTick = 0;
            _lastEmergencyCheckTick = 0;

            // Reset ETA
            _lastAnnouncedETA = 0f;
            _lastETAAnnounceTick = 0;
            _speedSampleIndex = 0;
            _averageSpeed = 0f;
            if (_speedSamples != null)
            {
                Array.Clear(_speedSamples, 0, _speedSamples.Length);
            }

            // Reset pause state
            _pauseState = Constants.PAUSE_STATE_NONE;
            _pauseStartTick = 0;

            // Reset following state
            _lastFollowingState = 0;
            _lastFollowingCheckTick = 0;
            _lastFollowingAnnounceTick = 0;

            // Reset structure detection
            _currentStructureType = Constants.STRUCTURE_TYPE_NONE;
            _lastStructureCheckTick = 0;
            _lastStructureAnnounceTick = 0;
            _inStructure = false;

            // Reset lane change detection
            _laneTrackingPosition = Vector3.Zero;
            _laneTrackingHeading = 0f;
            _lastLaneCheckTick = 0;
            _lastLaneChangeAnnounceTick = 0;
            _laneChangeInProgress = false;

            // Reset overtaking detection
            _overtakeTracking?.Clear();
            _lastOvertakeCheckTick = 0;
            _lastOvertakeAnnounceTick = 0;
        }

        #endregion

        #region Environmental Awareness

        /// <summary>
        /// Check weather conditions and adjust speed accordingly
        /// </summary>
        public void CheckWeather(Vehicle vehicle, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Throttle checks
            if (currentTick - _lastWeatherCheckTick < Constants.TICK_INTERVAL_WEATHER_CHECK)
                return;

            _lastWeatherCheckTick = currentTick;

            try
            {
                // Get current weather hash
                int weatherHash = Function.Call<int>(_getPrevWeatherHash);

                if (weatherHash == _currentWeatherHash) return;

                _currentWeatherHash = weatherHash;
                float newMultiplier = GetWeatherSpeedMultiplier(weatherHash);

                // Only announce and adjust if multiplier changed significantly
                if (Math.Abs(newMultiplier - _weatherSpeedMultiplier) > 0.05f)
                {
                    _weatherSpeedMultiplier = newMultiplier;
                    ApplyEnvironmentalSpeedModifiers(currentTick);

                    string weatherName = GetWeatherName(weatherHash);
                    if (newMultiplier < 0.9f)
                    {
                        TryAnnounce($"{weatherName} conditions, reducing speed",
                            Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceWeather");
                    }
                    else if (_weatherAnnounced && newMultiplier >= 0.95f)
                    {
                        TryAnnounce("Weather clearing, resuming normal speed",
                            Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceWeather");
                    }
                    _weatherAnnounced = newMultiplier < 0.9f;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckWeather");
            }
        }

        /// <summary>
        /// Get speed multiplier for weather type
        /// </summary>
        private float GetWeatherSpeedMultiplier(int weatherHash)
        {
            // Using unchecked to handle potential overflow with hash comparison
            unchecked
            {
                if (weatherHash == Constants.WEATHER_CLEAR || weatherHash == Constants.WEATHER_EXTRASUNNY)
                    return Constants.WEATHER_SPEED_CLEAR;
                if (weatherHash == Constants.WEATHER_CLOUDS || weatherHash == Constants.WEATHER_CLEARING)
                    return Constants.WEATHER_SPEED_CLOUDS;
                if (weatherHash == Constants.WEATHER_OVERCAST || weatherHash == Constants.WEATHER_SMOG)
                    return Constants.WEATHER_SPEED_OVERCAST;
                if (weatherHash == Constants.WEATHER_RAIN)
                    return Constants.WEATHER_SPEED_RAIN;
                if (weatherHash == Constants.WEATHER_THUNDER)
                    return Constants.WEATHER_SPEED_THUNDER;
                if (weatherHash == Constants.WEATHER_FOGGY)
                    return Constants.WEATHER_SPEED_FOGGY;
                if (weatherHash == Constants.WEATHER_XMAS || weatherHash == Constants.WEATHER_SNOWLIGHT)
                    return Constants.WEATHER_SPEED_SNOW;
                if (weatherHash == Constants.WEATHER_BLIZZARD)
                    return Constants.WEATHER_SPEED_BLIZZARD;
            }
            return 1.0f;  // Default - unknown weather
        }

        /// <summary>
        /// Get readable weather name
        /// </summary>
        private string GetWeatherName(int weatherHash)
        {
            unchecked
            {
                if (weatherHash == Constants.WEATHER_CLEAR || weatherHash == Constants.WEATHER_EXTRASUNNY)
                    return "Clear";
                if (weatherHash == Constants.WEATHER_CLOUDS)
                    return "Cloudy";
                if (weatherHash == Constants.WEATHER_OVERCAST)
                    return "Overcast";
                if (weatherHash == Constants.WEATHER_RAIN)
                    return "Rainy";
                if (weatherHash == Constants.WEATHER_CLEARING)
                    return "Clearing";
                if (weatherHash == Constants.WEATHER_THUNDER)
                    return "Stormy";
                if (weatherHash == Constants.WEATHER_SMOG)
                    return "Smoggy";
                if (weatherHash == Constants.WEATHER_FOGGY)
                    return "Foggy";
                if (weatherHash == Constants.WEATHER_XMAS || weatherHash == Constants.WEATHER_SNOWLIGHT)
                    return "Snowy";
                if (weatherHash == Constants.WEATHER_BLIZZARD)
                    return "Blizzard";
            }
            return "Unknown";
        }

        /// <summary>
        /// Check time of day and adjust speed/headlights
        /// </summary>
        public void CheckTimeOfDay(Vehicle vehicle, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Throttle checks
            if (currentTick - _lastTimeCheckTick < Constants.TICK_INTERVAL_TIME_CHECK)
                return;

            _lastTimeCheckTick = currentTick;

            try
            {
                int hour = World.CurrentTimeOfDay.Hours;
                int newTimeOfDay;
                float newMultiplier;

                if (hour >= Constants.TIME_DAY_START && hour < Constants.TIME_DUSK_START)
                {
                    newTimeOfDay = 0;  // Day
                    newMultiplier = Constants.TIME_SPEED_DAY;
                }
                else if (hour >= Constants.TIME_DAWN_START && hour < Constants.TIME_DAY_START ||
                         hour >= Constants.TIME_DUSK_START && hour < Constants.TIME_NIGHT_START)
                {
                    newTimeOfDay = 1;  // Dawn/Dusk
                    newMultiplier = Constants.TIME_SPEED_DAWN_DUSK;
                }
                else
                {
                    newTimeOfDay = 2;  // Night
                    newMultiplier = Constants.TIME_SPEED_NIGHT;
                }

                // Update headlights
                bool shouldHaveHeadlights = newTimeOfDay >= 1;  // Dawn/dusk or night
                if (shouldHaveHeadlights != _headlightsOn)
                {
                    _headlightsOn = shouldHaveHeadlights;
                    // 0 = off, 1 = low, 2 = high
                    Function.Call(_setVehicleLightsHash, vehicle.Handle, shouldHaveHeadlights ? 2 : 0);
                }

                // Update speed multiplier if time changed
                if (newTimeOfDay != _lastTimeOfDay)
                {
                    _lastTimeOfDay = newTimeOfDay;

                    if (Math.Abs(newMultiplier - _timeSpeedMultiplier) > 0.05f)
                    {
                        _timeSpeedMultiplier = newMultiplier;
                        ApplyEnvironmentalSpeedModifiers(currentTick);

                        if (newTimeOfDay == 2)
                        {
                            TryAnnounce("Night driving, reducing speed", Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                        }
                        else if (newTimeOfDay == 0 && _timeSpeedMultiplier < 1.0f)
                        {
                            TryAnnounce("Daylight, resuming normal speed", Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckTimeOfDay");
            }
        }

        /// <summary>
        /// Apply combined environmental speed modifiers (weather + time + road type)
        /// </summary>
        private void ApplyEnvironmentalSpeedModifiers(long currentTick)
        {
            if (!_autoDriveActive) return;
            if (_curveSlowdownActive || _arrivalSlowdownActive || _pauseState != Constants.PAUSE_STATE_NONE) return;

            try
            {
                // Combine all modifiers
                float combinedMultiplier = _roadTypeSpeedMultiplier * _weatherSpeedMultiplier * _timeSpeedMultiplier;
                float adjustedSpeed = _targetSpeed * combinedMultiplier;

                Ped player = Game.Player.Character;
                Function.Call(
                    _setCruiseSpeedHash,
                    player.Handle,  // Must use .Handle for native calls
                    adjustedSpeed);

                if (Logger.IsDebugEnabled) Logger.Debug($"Environmental speed: road={_roadTypeSpeedMultiplier:P0}, weather={_weatherSpeedMultiplier:P0}, time={_timeSpeedMultiplier:P0}, final={adjustedSpeed:F1} m/s");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ApplyEnvironmentalSpeedModifiers");
            }
        }

        /// <summary>
        /// Check for vehicles ahead and warn of collision danger
        /// Uses TIME-TO-COLLISION (TTC) for speed-appropriate warnings
        /// </summary>
        public void CheckCollisionWarning(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Throttle checks
            if (currentTick - _lastCollisionCheckTick < Constants.TICK_INTERVAL_COLLISION_CHECK)
                return;

            _lastCollisionCheckTick = currentTick;

            try
            {
                float ourSpeed = vehicle.Speed;
                if (ourSpeed < 3f) return;  // Skip if barely moving

                // Get forward direction
                // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                float heading = vehicle.Heading;
                float radians = (90f - heading) * Constants.DEG_TO_RAD;
                Vector3 forward = new Vector3((float)Math.Cos(radians), (float)Math.Sin(radians), 0f);

                // Scan for vehicles ahead - use larger radius at higher speeds
                float scanDistance = Math.Max(Constants.COLLISION_SCAN_DISTANCE, ourSpeed * 5f);
                float closestDistance = float.MaxValue;
                float closestClosingSpeed = 0f;  // Relative speed (positive = getting closer)

                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(position, scanDistance);
                foreach (Vehicle v in nearbyVehicles)
                {
                    // Compare by Handle - SHVDN returns new wrapper objects each call
                    if (v.Handle == vehicle.Handle || !v.Exists()) continue;

                    Vector3 toVehicle = v.Position - position;
                    float distance = toVehicle.Length();

                    // Check if in front (within scan angle)
                    if (distance > 0.1f)  // Avoid division by zero
                    {
                        float dot = Vector3.Dot(Vector3.Normalize(toVehicle), forward);
                        // PERFORMANCE: Use pre-calculated RAD_TO_DEG constant
                        float angle = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, dot))) * Constants.RAD_TO_DEG;

                        if (angle <= Constants.COLLISION_SCAN_ANGLE && distance < closestDistance)
                        {
                            closestDistance = distance;

                            // Calculate closing speed (our speed - their forward speed component)
                            // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                            float theirSpeed = v.Speed;
                            float theirHeading = v.Heading;
                            float theirRadians = (90f - theirHeading) * Constants.DEG_TO_RAD;
                            Vector3 theirForward = new Vector3((float)Math.Cos(theirRadians), (float)Math.Sin(theirRadians), 0f);

                            // How much of their velocity is in our direction?
                            float theirSpeedInOurDirection = theirSpeed * Vector3.Dot(theirForward, forward);

                            // Closing speed = our speed - their speed in our direction
                            // Positive = we're catching up, negative = they're pulling away
                            closestClosingSpeed = ourSpeed - theirSpeedInOurDirection;
                        }
                    }
                }

                _lastVehicleAheadDistance = closestDistance;

                // Calculate Time-To-Collision (TTC)
                // TTC = distance / closing_speed (only if closing speed > 0)
                float timeToCollision = float.MaxValue;
                if (closestClosingSpeed > 0.5f && closestDistance < float.MaxValue)
                {
                    timeToCollision = closestDistance / closestClosingSpeed;
                }

                // Determine warning level based on BOTH TTC and minimum distances
                // At low speeds, use distance thresholds; at high speeds, use TTC
                int warningLevel = 0;

                // TTC-based warnings (more important at high speeds)
                if (timeToCollision <= Constants.COLLISION_TTC_IMMINENT)
                    warningLevel = 4;  // Emergency
                else if (timeToCollision <= Constants.COLLISION_TTC_CLOSE)
                    warningLevel = 3;  // Close
                else if (timeToCollision <= Constants.COLLISION_TTC_MEDIUM)
                    warningLevel = 2;  // Medium
                else if (timeToCollision <= Constants.COLLISION_TTC_FAR)
                    warningLevel = 1;  // Far

                // Also check minimum distance thresholds (important at low speeds)
                if (closestDistance <= Constants.COLLISION_WARNING_CLOSE && warningLevel < 3)
                    warningLevel = 3;
                else if (closestDistance <= Constants.COLLISION_WARNING_MEDIUM && warningLevel < 2)
                    warningLevel = 2;
                else if (closestDistance <= Constants.COLLISION_WARNING_FAR && warningLevel < 1)
                    warningLevel = 1;

                // Announce if warning level changed (escalating only) or emergency
                bool shouldAnnounce = (warningLevel > _lastCollisionWarningLevel) ||
                                      (warningLevel == 4); // Always announce emergency

                if (shouldAnnounce && currentTick - _lastCollisionAnnounceTick > Constants.COLLISION_WARNING_COOLDOWN)
                {
                    _lastCollisionAnnounceTick = currentTick;
                    _lastCollisionWarningLevel = warningLevel;

                    string warning;
                    int priority;
                    switch (warningLevel)
                    {
                        case 4:
                            warning = $"Collision imminent, {timeToCollision:F1} seconds";
                            priority = Constants.ANNOUNCE_PRIORITY_CRITICAL;
                            break;
                        case 3:
                            warning = "Vehicle close ahead";
                            priority = Constants.ANNOUNCE_PRIORITY_HIGH;
                            break;
                        case 2:
                            warning = "Vehicle ahead";
                            priority = Constants.ANNOUNCE_PRIORITY_MEDIUM;
                            break;
                        case 1:
                            warning = "Traffic ahead";
                            priority = Constants.ANNOUNCE_PRIORITY_LOW;
                            break;
                        default:
                            return;
                    }
                    TryAnnounce(warning, priority, currentTick);
                }
                else if (warningLevel < _lastCollisionWarningLevel)
                {
                    // Deescalated - just update, don't announce
                    _lastCollisionWarningLevel = warningLevel;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckCollisionWarning");
            }
        }

        /// <summary>
        /// Check for emergency vehicles with sirens and yield (except in reckless mode)
        /// </summary>
        public void CheckEmergencyVehicles(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // RECKLESS MODE: Skip emergency vehicle yielding
            if (_currentDrivingStyleMode == Constants.DRIVING_STYLE_MODE_RECKLESS)
            {
                if (_yieldingToEmergency)
                {
                    // Was yielding, now reckless - resume
                    _yieldingToEmergency = false;
                    ResumeFromYield(vehicle, currentTick);
                }
                return;
            }

            // Throttle checks
            if (currentTick - _lastEmergencyCheckTick < Constants.TICK_INTERVAL_EMERGENCY_CHECK)
                return;

            _lastEmergencyCheckTick = currentTick;

            try
            {
                // Check if currently yielding
                if (_yieldingToEmergency)
                {
                    // Check if yield time expired or emergency passed
                    if (currentTick - _emergencyYieldStartTick > Constants.EMERGENCY_YIELD_DURATION)
                    {
                        // Check if emergency vehicle is still nearby
                        bool stillNearby = IsEmergencyVehicleNearby(position);
                        if (!stillNearby)
                        {
                            _yieldingToEmergency = false;
                            ResumeFromYield(vehicle, currentTick);
                            TryAnnounce("Emergency vehicle passed, resuming",
                                Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                        }
                        else
                        {
                            // Reset yield timer if still nearby
                            _emergencyYieldStartTick = currentTick;
                        }
                    }
                    return;
                }

                // Scan for emergency vehicles with sirens on
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(position, Constants.EMERGENCY_DETECTION_RADIUS);

                // Get our forward direction for position analysis
                // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                float ourHeading = vehicle.Heading;
                float ourRadians = (90f - ourHeading) * Constants.DEG_TO_RAD;
                Vector3 ourForward = new Vector3((float)Math.Cos(ourRadians), (float)Math.Sin(ourRadians), 0f);

                foreach (Vehicle v in nearbyVehicles)
                {
                    // Compare by Handle - SHVDN returns new wrapper objects each call
                    if (v.Handle == vehicle.Handle || !v.Exists()) continue;

                    // Check if siren is on
                    bool sirenOn = Function.Call<bool>(_sirenAudioOnHash, v.Handle);
                    if (!sirenOn) continue;

                    // Found emergency vehicle with siren - determine direction
                    Vector3 toEmergency = v.Position - position;
                    float distance = toEmergency.Length();
                    float dot = distance > 0.1f ? Vector3.Dot(Vector3.Normalize(toEmergency), ourForward) : 0f;

                    // Determine if emergency vehicle is approaching from behind, in front, or side
                    string direction;
                    bool isBehind = dot < -0.3f;  // More than 90 degrees behind
                    bool isAhead = dot > 0.3f;   // More than 60 degrees ahead

                    if (isBehind)
                    {
                        // Check if emergency vehicle is moving toward us (closing in from behind)
                        float theirSpeed = v.Speed;
                        float ourSpeed = vehicle.Speed;

                        if (theirSpeed > ourSpeed + 5f)  // They're faster, likely approaching
                        {
                            direction = "approaching from behind";
                        }
                        else
                        {
                            // Behind but not closing - maybe we should continue?
                            // Still yield to be safe, but less urgently
                            direction = "behind";
                        }
                    }
                    else if (isAhead)
                    {
                        direction = "ahead";
                        // Emergency vehicle ahead - slow down but don't need to pull over as much
                    }
                    else
                    {
                        direction = "nearby";
                    }

                    _yieldingToEmergency = true;
                    _emergencyYieldStartTick = currentTick;
                    _emergencyVehiclePosition = v.Position;
                    _emergencyApproachingFromBehind = isBehind;

                    // Slow down and pull over
                    StartYieldToEmergency(vehicle, currentTick);
                    TryAnnounce($"Emergency vehicle {direction}, pulling over",
                        Constants.ANNOUNCE_PRIORITY_CRITICAL, currentTick);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckEmergencyVehicles");
            }
        }

        /// <summary>
        /// Check if an emergency vehicle with siren is still nearby.
        /// Uses pre-cached Hash to avoid per-call casting.
        /// </summary>
        private bool IsEmergencyVehicleNearby(Vector3 position)
        {
            try
            {
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(position, Constants.EMERGENCY_DETECTION_RADIUS);
                foreach (Vehicle v in nearbyVehicles)
                {
                    if (!v.Exists()) continue;
                    bool sirenOn = Function.Call<bool>(_sirenAudioOnHash, v.Handle);
                    if (sirenOn) return true;
                }
            }
            catch { /* Expected - vehicle entities may become invalid during iteration */ }
            return false;
        }

        /// <summary>
        /// Start yielding to emergency vehicle
        /// </summary>
        private void StartYieldToEmergency(Vehicle vehicle, long currentTick)
        {
            try
            {
                Ped player = Game.Player.Character;

                // Clear current task and slow down
                Function.Call(_clearPedTasksHash, player.Handle);

                // Apply brakes
                Function.Call(_setHandbrakeHash, vehicle.Handle, true);

                Logger.Info("Yielding to emergency vehicle");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StartYieldToEmergency");
            }
        }

        /// <summary>
        /// Resume driving after emergency vehicle passes
        /// </summary>
        private void ResumeFromYield(Vehicle vehicle, long currentTick)
        {
            try
            {
                Ped player = Game.Player.Character;

                // Release brakes
                Function.Call(_setHandbrakeHash, vehicle.Handle, false);

                // Resume driving task
                if (_wanderMode)
                {
                    IssueWanderTask(player, vehicle, _targetSpeed);
                }
                else
                {
                    // Use helper method for LONGRANGE support
                    IssueDriveToCoordTask(player, vehicle, _lastWaypointPos, _targetSpeed,
                        Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS);
                }

                _taskIssued = true;
                Logger.Info("Resumed driving after emergency yield");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ResumeFromYield");
            }
        }

        /// <summary>
        /// Check following distance using realistic time-based following (2-3 second rule)
        /// Real drivers maintain time gaps, not fixed distances
        /// </summary>
        public void CheckFollowingDistance(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Throttle checks
            if (currentTick - _lastFollowingCheckTick < Constants.TICK_INTERVAL_FOLLOWING_CHECK)
                return;

            _lastFollowingCheckTick = currentTick;

            float vehicleSpeed = vehicle.Speed;
            float followingTimeGap = float.MaxValue; // seconds

            // Only calculate time gap if we have a vehicle ahead and we're moving
            // Guard against float.MaxValue in _lastVehicleAheadDistance to prevent overflow
            if (_lastVehicleAheadDistance < Constants.FOLLOWING_CLEAR_ROAD &&
                _lastVehicleAheadDistance < float.MaxValue &&
                vehicleSpeed > 1f)
            {
                // Time gap = distance / relative speed
                // For simplicity, assume lead vehicle is going similar speed
                // In reality, this would need speed tracking of the vehicle ahead
                followingTimeGap = _lastVehicleAheadDistance / Math.Max(vehicleSpeed, 1f);
            }

            // Determine following state based on time gap (realistic 2-3 second rule)
            int followingState;
            if (_lastVehicleAheadDistance >= Constants.FOLLOWING_CLEAR_ROAD)
            {
                followingState = 0; // Clear road
                _followingTimeGap = float.MaxValue;
            }
            else
            {
                _followingTimeGap = followingTimeGap;

                // Time-based following states (2-3 second rule is safe)
                if (followingTimeGap >= 4.0f)
                    followingState = 0; // Very clear
                else if (followingTimeGap >= 3.0f)
                    followingState = 1; // Safe following (3+ seconds)
                else if (followingTimeGap >= 2.0f)
                    followingState = 2; // Normal following (2-3 seconds)
                else if (followingTimeGap >= 1.5f)
                    followingState = 3; // Close following (1.5-2 seconds)
                else
                    followingState = 4; // Dangerous (< 1.5 seconds)
            }

            // Adjust speed based on following state and driving style
            AdjustSpeedForFollowing(vehicle, followingState, vehicleSpeed);

            // Only announce state changes with cooldown
            if (followingState != _lastFollowingState &&
                currentTick - _lastFollowingAnnounceTick > Constants.FOLLOWING_ANNOUNCE_COOLDOWN)
            {
                _lastFollowingAnnounceTick = currentTick;
                _lastFollowingState = followingState;

                // Only announce notable states
                if (followingState == 0 && _lastFollowingState >= 3)
                {
                    TryAnnounce("Road clear ahead", Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                }
                else if (followingState == 3)
                {
                    TryAnnounce("Following too closely", Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                }
                else if (followingState == 4)
                {
                    TryAnnounce("Dangerously close", Constants.ANNOUNCE_PRIORITY_HIGH, currentTick);
                }
            }
            else
            {
                _lastFollowingState = followingState;
            }
        }

        /// <summary>
        /// Adjust vehicle speed based on following distance and driving style
        /// Implements realistic gradual speed control instead of sudden braking
        /// </summary>
        private void AdjustSpeedForFollowing(Vehicle vehicle, int followingState, float currentSpeed)
        {
            if (!_autoDriveActive || !_taskIssued) return;

            // Calculate target speed based on following state
            float targetSpeed = CalculateTargetSpeedForFollowing(followingState, currentSpeed);

            // Apply smooth speed transition to avoid jerky behavior
            ApplySmoothSpeedTransition(vehicle, targetSpeed, currentSpeed);
        }

        /// <summary>
        /// Calculate target speed based on following state and driving conditions
        /// Implements the "2-second rule" and other realistic driving behaviors
        /// </summary>
        private float CalculateTargetSpeedForFollowing(int followingState, float currentSpeed)
        {
            float baseTargetSpeed = _targetSpeed;

            // Apply road type and weather modifiers first
            baseTargetSpeed *= _roadTypeSpeedMultiplier * _weatherSpeedMultiplier * _timeSpeedMultiplier;

            // Adjust based on following state using time-based following rules
            switch (followingState)
            {
                case 0: // Clear road - no vehicle ahead
                    // Gradually return to full target speed
                    return Math.Min(baseTargetSpeed, currentSpeed + GetAccelerationRate());
                case 1: // Safe following (3+ seconds) - optimal gap
                    // Maintain speed or slight acceleration
                    return Math.Min(baseTargetSpeed, currentSpeed + (GetAccelerationRate() * 0.5f));
                case 2: // Normal following (2-3 seconds) - good gap
                    // Maintain current speed within reasonable bounds
                    return Math.Max(baseTargetSpeed * 0.9f, Math.Min(baseTargetSpeed, currentSpeed));
                case 3: // Close following (1.5-2 seconds) - too close
                    // Gradual deceleration to increase gap
                    return Math.Max(baseTargetSpeed * 0.75f, currentSpeed - GetDecelerationRate());
                case 4: // Dangerous following (< 1.5 seconds) - emergency
                    // Significant deceleration for safety
                    return Math.Max(Constants.AUTODRIVE_MIN_SPEED, currentSpeed - (GetDecelerationRate() * 2f));
            }

            return baseTargetSpeed;
        }

        /// <summary>
        /// Apply smooth speed transitions to avoid jerky behavior
        /// Uses acceleration/deceleration curves instead of instant speed changes
        /// </summary>
        private void ApplySmoothSpeedTransition(Vehicle vehicle, float targetSpeed, float currentSpeed)
        {
            // Calculate speed difference
            float speedDiff = targetSpeed - currentSpeed;

            // Only update if change is significant (> 0.5 m/s to avoid micro-adjustments)
            if (Math.Abs(speedDiff) < 0.5f) return;

            // Limit rate of change for smooth behavior
            float maxChangeRate = speedDiff > 0 ? GetAccelerationRate() : GetDecelerationRate();
            float actualChange = Math.Max(-maxChangeRate, Math.Min(maxChangeRate, speedDiff));

            float newSpeed = currentSpeed + actualChange;

            // Ensure within safe bounds
            newSpeed = Math.Max(Constants.AUTODRIVE_MIN_SPEED, Math.Min(Constants.AUTODRIVE_MAX_SPEED, newSpeed));

            // Update cruise speed
            // Use Game.Player.Character instead of vehicle.Driver to avoid null reference
            // when player exits vehicle during this call
            try
            {
                Ped player = Game.Player.Character;
                if (player != null && player.IsInVehicle())
                {
                    Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED, player.Handle, newSpeed);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ApplySmoothSpeedTransition");
            }
        }

        /// <summary>
        /// Get acceleration rate based on driving style (m/s²)
        /// Realistic acceleration curves prevent sudden speed changes
        /// </summary>
        private float GetAccelerationRate()
        {
            switch (_currentDrivingStyleMode)
            {
                case Constants.DRIVING_STYLE_MODE_CAUTIOUS:
                    return 1.0f; // Gentle acceleration (0-60 in ~17 seconds)
                case Constants.DRIVING_STYLE_MODE_NORMAL:
                    return 2.0f; // Moderate acceleration (0-60 in ~8.5 seconds)
                case Constants.DRIVING_STYLE_MODE_FAST:
                    return 3.0f; // Sporty acceleration (0-60 in ~5.5 seconds)
                case Constants.DRIVING_STYLE_MODE_RECKLESS:
                    return 4.0f; // Aggressive acceleration (0-60 in ~4 seconds)
                default:
                    return 2.0f;
            }
        }

        /// <summary>
        /// Get deceleration rate based on driving style (m/s²)
        /// More conservative deceleration for safety
        /// </summary>
        private float GetDecelerationRate()
        {
            switch (_currentDrivingStyleMode)
            {
                case Constants.DRIVING_STYLE_MODE_CAUTIOUS:
                    return 2.0f; // Gentle braking
                case Constants.DRIVING_STYLE_MODE_NORMAL:
                    return 3.0f; // Normal braking
                case Constants.DRIVING_STYLE_MODE_FAST:
                    return 4.0f; // Firm braking
                case Constants.DRIVING_STYLE_MODE_RECKLESS:
                    return 5.0f; // Hard braking (riskier)
                default:
                    return 3.0f;
            }
        }

        /// <summary>
        /// Get speed modifier based on driving style for following behavior
        /// </summary>
        private float GetFollowingStyleModifier()
        {
            switch (_currentDrivingStyleMode)
            {
                case Constants.DRIVING_STYLE_MODE_CAUTIOUS:
                    return 0.7f; // More conservative following
                case Constants.DRIVING_STYLE_MODE_NORMAL:
                    return 0.85f; // Balanced
                case Constants.DRIVING_STYLE_MODE_FAST:
                    return 0.95f; // Less conservative
                case Constants.DRIVING_STYLE_MODE_RECKLESS:
                    return 1.0f; // Aggressive following
                default:
                    return 0.85f;
            }
        }

        /// <summary>
        /// Check for tunnels and bridges
        /// </summary>
        public void CheckStructures(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Throttle checks
            if (currentTick - _lastStructureCheckTick < Constants.TICK_INTERVAL_STRUCTURE_CHECK)
                return;

            _lastStructureCheckTick = currentTick;

            try
            {
                int detectedType = Constants.STRUCTURE_TYPE_NONE;

                // Check for ceiling above (tunnel/overpass) - result not used but call determines if something is above
                Function.Call<bool>(
                    _getGroundZHash,
                    position.X, position.Y, position.Z + Constants.STRUCTURE_CHECK_HEIGHT,
                    new OutputArgument(),
                    false);

                // Check current road type for tunnel
                if (_currentRoadType == Constants.ROAD_TYPE_TUNNEL)
                {
                    detectedType = Constants.STRUCTURE_TYPE_TUNNEL;
                }
                else
                {
                    // Check for ground below (bridge check)
                    // Use pre-allocated OutputArgument to avoid per-tick allocations
                    bool hasBelowGround = Function.Call<bool>(
                        _getGroundZHash,
                        position.X, position.Y, position.Z - 2f,
                        _structureBelowArg,
                        false);

                    if (hasBelowGround)
                    {
                        float belowZ = _structureBelowArg.GetResult<float>();
                        // Guard against invalid float values from native
                        if (float.IsNaN(belowZ) || float.IsInfinity(belowZ))
                            return;

                        float heightAboveGround = position.Z - belowZ;

                        if (heightAboveGround > Constants.BRIDGE_MIN_HEIGHT_BELOW)
                        {
                            detectedType = Constants.STRUCTURE_TYPE_BRIDGE;
                        }
                    }
                }

                // Announce structure changes
                if (detectedType != _currentStructureType)
                {
                    bool wasInStructure = _inStructure;
                    _currentStructureType = detectedType;
                    _inStructure = detectedType != Constants.STRUCTURE_TYPE_NONE;

                    if (currentTick - _lastStructureAnnounceTick > Constants.STRUCTURE_ANNOUNCE_COOLDOWN)
                    {
                        _lastStructureAnnounceTick = currentTick;

                        if (_inStructure && !wasInStructure)
                        {
                            string structureName = GetStructureName(detectedType);
                            TryAnnounce($"Entering {structureName}", Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                        }
                        else if (!_inStructure && wasInStructure)
                        {
                            TryAnnounce("Exiting structure", Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckStructures");
            }
        }

        /// <summary>
        /// Get structure name for announcement
        /// </summary>
        private string GetStructureName(int structureType)
        {
            switch (structureType)
            {
                case Constants.STRUCTURE_TYPE_TUNNEL: return "tunnel";
                case Constants.STRUCTURE_TYPE_BRIDGE: return "bridge";
                case Constants.STRUCTURE_TYPE_OVERPASS: return "overpass";
                case Constants.STRUCTURE_TYPE_UNDERPASS: return "underpass";
                default: return "structure";
            }
        }

        /// <summary>
        /// Update and announce ETA to waypoint
        /// Uses GENERATE_DIRECTIONS_TO_COORD for accurate road distance estimation
        /// </summary>
        public void UpdateETA(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive || _wanderMode) return;

            // Update speed sample for averaging
            float currentSpeed = vehicle.Speed;
            _speedSamples[_speedSampleIndex] = currentSpeed;
            _speedSampleIndex = (_speedSampleIndex + 1) % Constants.ETA_SPEED_SAMPLES;

            // Calculate average speed
            float totalSpeed = 0f;
            int sampleCount = 0;
            for (int i = 0; i < Constants.ETA_SPEED_SAMPLES; i++)
            {
                if (_speedSamples[i] > 0)
                {
                    totalSpeed += _speedSamples[i];
                    sampleCount++;
                }
            }
            _averageSpeed = sampleCount > 0 ? totalSpeed / sampleCount : currentSpeed;

            // Throttle ETA announcements
            if (currentTick - _lastETAAnnounceTick < Constants.TICK_INTERVAL_ETA_UPDATE)
                return;

            // Calculate road distance using GENERATE_DIRECTIONS_TO_COORD
            // This gives actual driving distance, not straight-line
            float roadDistance = GetRoadDistanceToWaypoint(position);
            if (roadDistance < Constants.ETA_MIN_DISTANCE_FOR_ANNOUNCE)
                return;

            // Calculate ETA in seconds using road distance
            // Apply road factor (roads are typically 1.3-1.5x longer than straight line)
            float etaSeconds = _averageSpeed > 1f ? roadDistance / _averageSpeed : float.MaxValue;

            // Check if ETA changed significantly
            float etaChange = Math.Abs(etaSeconds - _lastAnnouncedETA);
            if (etaChange < Constants.ETA_ANNOUNCE_CHANGE_THRESHOLD && _lastAnnouncedETA > 0)
                return;

            _lastETAAnnounceTick = currentTick;
            _lastAnnouncedETA = etaSeconds;

            // Format and announce ETA
            string etaText = FormatETA(etaSeconds);
            TryAnnounce($"Estimated arrival in {etaText}", Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
        }

        /// <summary>
        /// Get estimated road distance to waypoint using GENERATE_DIRECTIONS_TO_COORD
        /// Falls back to straight-line distance with road factor if native fails
        /// </summary>
        private float GetRoadDistanceToWaypoint(Vector3 position)
        {
            try
            {
                // Use GENERATE_DIRECTIONS_TO_COORD to get road-aware distance estimate
                // Uses pre-allocated OutputArguments to avoid allocations
                int result = Function.Call<int>(
                    _generateDirectionsHash,
                    position.X, position.Y, position.Z,
                    _lastWaypointPos.X, _lastWaypointPos.Y, _lastWaypointPos.Z,
                    true,                   // p6: unknown, true seems standard
                    _roadDirectionArg1,     // direction - not needed but must be passed
                    _roadDirectionArg2,     // p8 - not needed but must be passed
                    _roadDistanceArg);

                if (result != 0) // 0 = failed, other values = success
                {
                    float roadDist = _roadDistanceArg.GetResult<float>();
                    if (roadDist > 0 && roadDist < Constants.ROAD_DISTANCE_SANITY_MAX) // Sanity check
                    {
                        return roadDist;
                    }
                }
            }
            catch (Exception ex)
            {
                if (Logger.IsDebugEnabled) Logger.Debug($"GENERATE_DIRECTIONS_TO_COORD failed: {ex.Message}");
            }

            // Fallback: straight-line distance with road factor (roads are ~1.4x longer)
            float straightLine = (_lastWaypointPos - position).Length();
            return straightLine * Constants.ROAD_DISTANCE_FACTOR;
        }

        /// <summary>
        /// Format ETA for speech
        /// </summary>
        private string FormatETA(float seconds)
        {
            if (seconds < 60)
            {
                return "less than a minute";
            }
            else if (seconds < 3600)
            {
                int minutes = (int)(seconds / 60);
                return minutes == 1 ? "1 minute" : $"{minutes} minutes";
            }
            else
            {
                int hours = (int)(seconds / 3600);
                int minutes = (int)((seconds % 3600) / 60);
                if (minutes == 0)
                    return hours == 1 ? "1 hour" : $"{hours} hours";
                return hours == 1 ? $"1 hour {minutes} minutes" : $"{hours} hours {minutes} minutes";
            }
        }

        #endregion

        #region Pause/Resume

        /// <summary>
        /// Pause AutoDrive (maintains state for resume)
        /// </summary>
        public void Pause()
        {
            if (!_autoDriveActive || _pauseState != Constants.PAUSE_STATE_NONE) return;

            try
            {
                Ped player = Game.Player.Character;
                Vehicle vehicle = player.CurrentVehicle;

                if (vehicle == null) return;

                _pauseState = Constants.PAUSE_STATE_PAUSED;
                _pauseStartTick = DateTime.Now.Ticks;
                _prePauseSpeed = _targetSpeed;
                _wasPausedWander = _wanderMode;

                // Clear driving task and apply brakes
                // Must use .Handle for native calls - SHVDN wrapper objects don't work directly
                Function.Call(_clearPedTasksHash, player.Handle);
                Function.Call(_setHandbrakeHash, vehicle.Handle, true);

                _audio.Speak("AutoDrive paused");
                Logger.Info("AutoDrive paused");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Pause");
            }
        }

        /// <summary>
        /// Resume AutoDrive from paused state
        /// </summary>
        public void Resume()
        {
            if (_pauseState != Constants.PAUSE_STATE_PAUSED) return;

            try
            {
                Ped player = Game.Player.Character;
                Vehicle vehicle = player.CurrentVehicle;

                if (vehicle == null || !player.IsInVehicle())
                {
                    _audio.Speak("Cannot resume, not in vehicle");
                    _pauseState = Constants.PAUSE_STATE_NONE;
                    return;
                }

                if (player.SeatIndex != VehicleSeat.Driver)
                {
                    _audio.Speak("Cannot resume, not in driver seat");
                    _pauseState = Constants.PAUSE_STATE_NONE;
                    return;
                }

                _pauseState = Constants.PAUSE_STATE_RESUMING;

                // Release brakes
                // Must use .Handle for native calls - SHVDN wrapper objects don't work directly
                Function.Call(_setHandbrakeHash, vehicle.Handle, false);

                // Re-issue driving task using optimized helpers
                if (_wasPausedWander)
                {
                    IssueWanderTask(player, vehicle, _targetSpeed);
                }
                else
                {
                    // Check if waypoint still exists
                    if (!Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
                    {
                        _audio.Speak("Waypoint removed, switching to wander mode");
                        _wanderMode = true;
                        IssueWanderTask(player, vehicle, _targetSpeed);
                    }
                    else
                    {
                        // Recalculate safe arrival position in case waypoint changed
                        Vector3 waypointPos = World.WaypointPosition;
                        _originalWaypointPos = waypointPos;
                        _safeArrivalPosition = GetSafeArrivalPosition(waypointPos);
                        _lastWaypointPos = _safeArrivalPosition;

                        // Use helper method for LONGRANGE support
                        IssueDriveToCoordTask(player, vehicle, _safeArrivalPosition, _targetSpeed,
                            Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS);
                    }
                }

                _taskIssued = true;
                _pauseState = Constants.PAUSE_STATE_NONE;

                _audio.Speak("AutoDrive resumed");
                Logger.Info("AutoDrive resumed");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Resume");
                _pauseState = Constants.PAUSE_STATE_NONE;
            }
        }

        /// <summary>
        /// Toggle pause/resume
        /// </summary>
        public void TogglePause()
        {
            if (_pauseState == Constants.PAUSE_STATE_PAUSED)
            {
                Resume();
            }
            else if (_pauseState == Constants.PAUSE_STATE_NONE && _autoDriveActive)
            {
                Pause();
            }
            else if (!_autoDriveActive)
            {
                _audio.Speak("AutoDrive is not active");
            }
        }

        #endregion

        #region Lane Change and Overtaking Detection

        /// <summary>
        /// Initialize lane tracking when autodrive starts
        /// </summary>
        private void InitializeLaneTracking(Vehicle vehicle)
        {
            _laneTrackingPosition = vehicle.Position;
            _laneTrackingHeading = vehicle.Heading;
            _laneChangeInProgress = false;
        }

        /// <summary>
        /// Check for lane changes based on lateral movement
        /// </summary>
        public void CheckLaneChange(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Throttle checks
            if (currentTick - _lastLaneCheckTick < Constants.TICK_INTERVAL_LANE_CHECK)
                return;

            _lastLaneCheckTick = currentTick;

            try
            {
                float speed = vehicle.Speed;

                // Skip if too slow
                if (speed < Constants.LANE_CHANGE_MIN_SPEED)
                {
                    // Reset tracking when slow
                    _laneTrackingPosition = position;
                    _laneTrackingHeading = vehicle.Heading;
                    return;
                }

                float currentHeading = vehicle.Heading;

                // Check if heading is relatively stable (not turning at intersection)
                float headingDiff = Math.Abs(NormalizeAngleDiff(currentHeading - _laneTrackingHeading));
                if (headingDiff > Constants.LANE_CHANGE_HEADING_TOLERANCE)
                {
                    // Turning, not lane changing - reset tracking
                    _laneTrackingPosition = position;
                    _laneTrackingHeading = currentHeading;
                    _laneChangeInProgress = false;
                    return;
                }

                // Calculate lateral movement (perpendicular to heading)
                // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                Vector3 movement = position - _laneTrackingPosition;
                float headingRad = (90f - _laneTrackingHeading) * Constants.DEG_TO_RAD;
                Vector3 forward = new Vector3((float)Math.Cos(headingRad), (float)Math.Sin(headingRad), 0f);
                Vector3 right = new Vector3((float)Math.Sin(headingRad), -(float)Math.Cos(headingRad), 0f);

                // Lateral distance (positive = right, negative = left)
                float lateralDistance = Vector3.Dot(movement, right);

                // Check for lane change threshold
                if (Math.Abs(lateralDistance) >= Constants.LANE_CHANGE_THRESHOLD)
                {
                    if (!_laneChangeInProgress &&
                        currentTick - _lastLaneChangeAnnounceTick > Constants.LANE_CHANGE_ANNOUNCE_COOLDOWN)
                    {
                        _laneChangeInProgress = true;
                        _lastLaneChangeAnnounceTick = currentTick;

                        string direction = lateralDistance > 0 ? "right" : "left";
                        TryAnnounce($"Changing lanes {direction}", Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                    }

                    // Reset tracking position after detecting lane change
                    _laneTrackingPosition = position;
                    _laneTrackingHeading = currentHeading;
                }
                else if (_laneChangeInProgress && Math.Abs(lateralDistance) < Constants.LANE_WIDTH * 0.3f)
                {
                    // Lane change complete (settled in new lane)
                    _laneChangeInProgress = false;
                }

                // Gradually update tracking position for continuous monitoring
                if (!_laneChangeInProgress)
                {
                    // Smooth update - blend toward current position
                    _laneTrackingPosition = Vector3.Lerp(_laneTrackingPosition, position, 0.1f);
                    _laneTrackingHeading = currentHeading;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckLaneChange");
            }
        }

        /// <summary>
        /// Check for overtaking maneuvers
        /// </summary>
        public void CheckOvertaking(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Throttle checks
            if (currentTick - _lastOvertakeCheckTick < Constants.TICK_INTERVAL_OVERTAKE_CHECK)
                return;

            _lastOvertakeCheckTick = currentTick;

            try
            {
                // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                float ourSpeed = vehicle.Speed;
                float ourHeading = vehicle.Heading;
                float headingRad = (90f - ourHeading) * Constants.DEG_TO_RAD;
                Vector3 forward = new Vector3((float)Math.Cos(headingRad), (float)Math.Sin(headingRad), 0f);
                Vector3 right = new Vector3((float)Math.Sin(headingRad), -(float)Math.Cos(headingRad), 0f);

                // Get nearby vehicles
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(position, Constants.OVERTAKE_DETECTION_RADIUS);

                // Track which vehicles are still visible (reuse pre-allocated HashSet)
                _visibleHandles.Clear();

                foreach (Vehicle v in nearbyVehicles)
                {
                    // Compare by Handle - SHVDN returns new wrapper objects each call
                    if (v.Handle == vehicle.Handle || !v.Exists()) continue;

                    int handle = v.Handle;
                    // Defensive: Limit _visibleHandles size to prevent unbounded growth
                    if (_visibleHandles.Count < 100)
                    {
                        _visibleHandles.Add(handle);
                    }

                    Vector3 toVehicle = v.Position - position;
                    float distance = toVehicle.Length();

                    // Calculate position relative to us
                    float forwardDist = Vector3.Dot(toVehicle, forward);  // positive = ahead
                    float lateralDist = Vector3.Dot(toVehicle, right);    // positive = right

                    // Determine relative position state
                    int newState;
                    if (forwardDist > Constants.OVERTAKE_SIDE_DISTANCE)
                    {
                        newState = 0;  // Ahead
                    }
                    else if (forwardDist < -Constants.OVERTAKE_BEHIND_DISTANCE)
                    {
                        newState = 2;  // Behind (we passed them)
                    }
                    else
                    {
                        newState = 1;  // Beside
                    }

                    // Check if we're tracking this vehicle
                    if (_overtakeTracking.TryGetValue(handle, out OvertakeTrackingInfo info))
                    {
                        // Check for state transition: ahead -> beside -> behind = overtake complete
                        if (info.State == 1 && newState == 2)
                        {
                            // Just completed overtaking this vehicle
                            float theirSpeed = v.Speed;

                            // Only announce if we were actually faster
                            if (ourSpeed > theirSpeed + Constants.OVERTAKE_MIN_SPEED_DIFF)
                            {
                                if (currentTick - _lastOvertakeAnnounceTick > Constants.OVERTAKE_ANNOUNCE_COOLDOWN)
                                {
                                    _lastOvertakeAnnounceTick = currentTick;

                                    // Determine which side we passed on
                                    string side = lateralDist > 0 ? "right" : "left";
                                    TryAnnounce($"Passed vehicle on {side}", Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                                }
                            }

                            // Remove from tracking (overtake complete)
                            _overtakeTracking.Remove(handle);
                            continue;
                        }

                        // Update state
                        info.State = newState;
                        _overtakeTracking[handle] = info;
                    }
                    else if (newState == 0 && _overtakeTracking.Count < Constants.OVERTAKE_TRACKING_MAX)
                    {
                        // New vehicle ahead - start tracking for potential overtake
                        float theirSpeed = v.Speed;

                        // Only track if we're faster (potential overtake)
                        if (ourSpeed > theirSpeed + Constants.OVERTAKE_MIN_SPEED_DIFF)
                        {
                            _overtakeTracking[handle] = new OvertakeTrackingInfo(handle, distance, currentTick);
                        }
                    }
                }

                // Clean up vehicles that are no longer visible or stale (reuse pre-allocated List)
                // Stale entries are removed to prevent unbounded dictionary growth
                _handleRemovalList.Clear();
                long staleThreshold = currentTick - 30_000_000; // 3 seconds staleness limit (reduced from 10s to prevent dictionary bloat)
                foreach (var kvp in _overtakeTracking)
                {
                    // Remove if no longer visible OR if entry is stale (tracking for too long)
                    if (!_visibleHandles.Contains(kvp.Key) || kvp.Value.FirstSeenTick < staleThreshold)
                    {
                        _handleRemovalList.Add(kvp.Key);
                    }
                }
                foreach (int handle in _handleRemovalList)
                {
                    _overtakeTracking.Remove(handle);
                }

                // Defensive: Trim excess capacity if collections grew too large
                // This prevents memory bloat from transient high-traffic situations
                if (_visibleHandles.Count > 50)
                {
                    _visibleHandles.Clear();
                    _visibleHandles.TrimExcess();
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckOvertaking");
            }
        }

        #endregion

        #region Waypoint Position Helpers

        /// <summary>
        /// Find a safe road position near the waypoint for driving
        /// This ensures we can actually drive to the destination
        /// Uses multiple strategies with increasing search radii for robustness
        /// </summary>
        private Vector3 GetSafeArrivalPosition(Vector3 waypointPos)
        {
            try
            {
                // Strategy 1: Try GET_CLOSEST_VEHICLE_NODE_WITH_HEADING for better road alignment
                // This native also provides heading, useful for approach direction
                // Uses pre-allocated OutputArguments to avoid allocations
                bool foundNode = Function.Call<bool>(
                    _getClosestNodeWithHeadingHash,
                    waypointPos.X, waypointPos.Y, waypointPos.Z,
                    _safeArrivalPosArg, _safeArrivalHeadingArg,
                    Constants.ROAD_NODE_TYPE_ALL,      // Node type: 1 = All road types (most inclusive)
                    Constants.ROAD_NODE_SEARCH_CONNECTION_DISTANCE,   // Search connection distance
                    0);     // Flags: 0 = default

                if (foundNode)
                {
                    Vector3 roadPos = _safeArrivalPosArg.GetResult<Vector3>();
                    float distanceToRoad = (roadPos - waypointPos).Length();

                    // Accept if within reasonable distance
                    if (distanceToRoad < Constants.SAFE_ARRIVAL_MAX_DISTANCE)
                    {
                        if (Logger.IsDebugEnabled) Logger.Debug($"Found road node {distanceToRoad:F1}m from waypoint (with heading)");
                        return roadPos;
                    }
                }

                // Strategy 2: Try the simpler GET_CLOSEST_VEHICLE_NODE
                // Sometimes works better for off-road waypoints
                bool foundSimpleNode = Function.Call<bool>(
                    _getClosestNodeHash,
                    waypointPos.X, waypointPos.Y, waypointPos.Z,
                    _safeArrivalPosArg,
                    Constants.ROAD_NODE_TYPE_ALL,      // Node type: 1 = Any
                    Constants.ROAD_NODE_SEARCH_CONNECTION_DISTANCE,   // p5: Search radius modifier
                    0);     // p6: Flags

                if (foundSimpleNode)
                {
                    Vector3 roadPos = _safeArrivalPosArg.GetResult<Vector3>();
                    float distanceToRoad = (roadPos - waypointPos).Length();
                    if (distanceToRoad < Constants.SAFE_ARRIVAL_MAX_DISTANCE)
                    {
                        if (Logger.IsDebugEnabled) Logger.Debug($"Found road node {distanceToRoad:F1}m from waypoint (simple)");
                        return roadPos;
                    }
                }

                // Strategy 3: Try GET_NTH_CLOSEST_VEHICLE_NODE for more options
                // Check the 2nd and 3rd closest nodes - sometimes closer to actual waypoint
                for (int n = 2; n <= 3; n++)
                {
                    bool foundNth = Function.Call<bool>(
                        _getNthClosestNodeHash,
                        waypointPos.X, waypointPos.Y, waypointPos.Z,
                        n,
                        _safeArrivalPosArg,
                        Constants.ROAD_NODE_TYPE_ALL,      // Node type
                        Constants.ROAD_NODE_SEARCH_CONNECTION_DISTANCE,   // p6
                        0);     // p7

                    if (foundNth)
                    {
                        Vector3 roadPos = _safeArrivalPosArg.GetResult<Vector3>();
                        float distanceToRoad = (roadPos - waypointPos).Length();
                        if (distanceToRoad < Constants.SAFE_ARRIVAL_NTH_NODE_MAX_DISTANCE) // Stricter for nth closest
                        {
                            if (Logger.IsDebugEnabled) Logger.Debug($"Found {n}th closest road node {distanceToRoad:F1}m from waypoint");
                            return roadPos;
                        }
                    }
                }

                // Strategy 4: Try getting a safe coord for ped (more flexible for off-road)
                bool foundSafe = Function.Call<bool>(
                    _getSafeCoordForPedHash,
                    waypointPos.X, waypointPos.Y, waypointPos.Z,
                    true,   // onGround
                    _safePedCoordArg,
                    Constants.SAFE_COORD_FLAGS);    // flags

                if (foundSafe)
                {
                    Vector3 safe = _safePedCoordArg.GetResult<Vector3>();
                    if (safe != Vector3.Zero && (safe - waypointPos).Length() < Constants.SAFE_ARRIVAL_SAFE_COORD_MAX_DISTANCE)
                    {
                        Logger.Debug($"Found safe coord for waypoint arrival");
                        return safe;
                    }
                }

                // Strategy 5: Try road side position for parking-friendly arrival
                bool foundRoadSide = Function.Call<bool>(
                    _getPointOnRoadSideHash,
                    waypointPos.X, waypointPos.Y, waypointPos.Z,
                    0,      // Side: 0 = right side of road
                    _roadSidePosArg);

                if (foundRoadSide)
                {
                    Vector3 roadSide = _roadSidePosArg.GetResult<Vector3>();
                    if (roadSide != Vector3.Zero && (roadSide - waypointPos).Length() < Constants.SAFE_ARRIVAL_ROAD_SIDE_MAX_DISTANCE)
                    {
                        Logger.Debug($"Found road side position for waypoint arrival");
                        return roadSide;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "GetSafeArrivalPosition");
            }

            // Fallback: return original waypoint position
            Logger.Debug("Using original waypoint position (no safe position found)");
            return waypointPos;
        }

        #endregion

        #region Road Type Detection

        /// <summary>
        /// Classify road type based on node flags, density, and additional heuristics
        /// Uses multiple signals for more reliable classification
        /// </summary>
        private int ClassifyRoadType(int flags, int density)
        {
            // Priority 1: Explicit road type flags (most reliable)
            if ((flags & Constants.ROAD_FLAG_TUNNEL) != 0)
                return Constants.ROAD_TYPE_TUNNEL;

            // Highway detection with multi-signal confirmation
            if ((flags & Constants.ROAD_FLAG_HIGHWAY) != 0)
            {
                // Confirm highway - highways have low density and no traffic lights
                if (density <= Constants.ROAD_DENSITY_SUBURBAN_MAX &&
                    (flags & Constants.ROAD_FLAG_TRAFFIC_LIGHT) == 0)
                {
                    return Constants.ROAD_TYPE_HIGHWAY;
                }
                // Highway flag but with traffic light = freeway on-ramp or city expressway
                // Still treat as highway for speed purposes
                return Constants.ROAD_TYPE_HIGHWAY;
            }

            // Priority 2: Off-road detection with density confirmation
            if ((flags & Constants.ROAD_FLAG_OFF_ROAD) != 0)
            {
                // Very low density + off-road = true dirt trail
                if (density <= 2)
                    return Constants.ROAD_TYPE_DIRT_TRAIL;
                // Off-road flag but higher density could be alley or parking lot
                return Constants.ROAD_TYPE_SUBURBAN;
            }

            // Priority 3: Traffic infrastructure signals
            if ((flags & Constants.ROAD_FLAG_TRAFFIC_LIGHT) != 0)
            {
                // Traffic light + high density = city street
                if (density > Constants.ROAD_DENSITY_SUBURBAN_MAX)
                    return Constants.ROAD_TYPE_CITY_STREET;
                // Traffic light + lower density = main suburban road
                return Constants.ROAD_TYPE_SUBURBAN;
            }

            // Priority 4: Junction/intersection analysis
            if ((flags & Constants.ROAD_FLAG_JUNCTION) != 0)
            {
                // Large junction + high density = city intersection
                if (density > Constants.ROAD_DENSITY_SUBURBAN_MAX)
                    return Constants.ROAD_TYPE_CITY_STREET;
            }

            // Priority 5: Density-based classification for unmarked roads
            if (density <= 1)
            {
                // Very low density with no flags = rural backroad
                return Constants.ROAD_TYPE_RURAL;
            }
            else if (density <= Constants.ROAD_DENSITY_RURAL_MAX)
            {
                // Low density = rural area
                return Constants.ROAD_TYPE_RURAL;
            }
            else if (density <= Constants.ROAD_DENSITY_SUBURBAN_MAX)
            {
                // Medium density = suburban
                return Constants.ROAD_TYPE_SUBURBAN;
            }

            // High density = city street (default for urban areas)
            return Constants.ROAD_TYPE_CITY_STREET;
        }

        /// <summary>
        /// Get road type at a given position - delegates to RoadTypeManager
        /// to ensure single source of truth and prevent state corruption
        /// </summary>
        public int GetRoadTypeAtPosition(Vector3 position)
        {
            // Delegate to RoadTypeManager to avoid duplicate OutputArgument state
            return _roadTypeManager?.GetRoadTypeAtPosition(position) ?? Constants.ROAD_TYPE_UNKNOWN;
        }

        /// <summary>
        /// Check for road type changes and announce if enabled
        /// Called from OnTick during AutoDrive
        /// </summary>
        public void CheckRoadTypeChange(Vector3 position, long currentTick, bool announceEnabled)
        {
            // Throttle checks
            if (currentTick - _lastRoadTypeCheckTick < Constants.TICK_INTERVAL_ROAD_TYPE_CHECK)
                return;

            _lastRoadTypeCheckTick = currentTick;

            int roadType = GetRoadTypeAtPosition(position);
            if (roadType == Constants.ROAD_TYPE_UNKNOWN)
                return;

            // Check if road type changed
            if (roadType != _currentRoadType)
            {
                int previousRoadType = _currentRoadType;
                _currentRoadType = roadType;

                // Apply speed adjustment for new road type
                ApplyRoadTypeSpeedAdjustment(roadType, currentTick);

                // Announce change if enabled and cooldown passed
                if (announceEnabled && roadType != _lastAnnouncedRoadType &&
                    currentTick - _lastRoadTypeAnnounceTick > Constants.ROAD_TYPE_ANNOUNCE_COOLDOWN)
                {
                    _lastRoadTypeAnnounceTick = currentTick;
                    _lastAnnouncedRoadType = roadType;

                    string roadName = Constants.GetRoadTypeName(roadType);
                    TryAnnounce($"Now on {roadName}", Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                }

                // Update seeking state if actively seeking
                if (_seekingRoad)
                {
                    UpdateSeekingState(roadType);
                }
            }
        }

        /// <summary>
        /// Announce current road type (for manual query)
        /// </summary>
        public void AnnounceCurrentRoadType()
        {
            Vector3 position = Game.Player.Character.Position;
            int roadType = GetRoadTypeAtPosition(position);

            if (roadType == Constants.ROAD_TYPE_UNKNOWN)
            {
                _audio.Speak("Unable to determine road type");
            }
            else
            {
                string roadName = Constants.GetRoadTypeName(roadType);
                _audio.Speak($"Currently on {roadName}");
            }
        }

        /// <summary>
        /// Check if current position is at a dead-end road and handle escape
        /// Uses road node flags to detect dead-ends proactively
        /// </summary>
        public void CheckDeadEnd(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive || !_wanderMode) return;

            // Throttle checks - check every 2 seconds
            if (currentTick - _lastDeadEndCheckTick < Constants.TICK_INTERVAL_DEAD_END_CHECK)
                return;

            _lastDeadEndCheckTick = currentTick;

            try
            {
                // Get road node properties at current position
                Function.Call(
                    _getVehicleNodePropsHash,
                    position.X, position.Y, position.Z,
                    _density, _flags);

                int nodeFlags = _flags.GetResult<int>();

                // Check if at a dead-end (flag bit 5 = 32)
                bool isDeadEnd = (nodeFlags & Constants.ROAD_FLAG_DEAD_END) != 0;

                if (isDeadEnd && !_inDeadEnd)
                {
                    // Just entered a dead-end
                    _inDeadEnd = true;
                    _deadEndEntryPosition = position;
                    _deadEndTurnCount = 0;
                    TryAnnounce("Approaching dead end, turning around", Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                    Logger.Info("Dead-end detected, initiating turnaround");

                    // Force a U-turn by starting recovery sequence
                    StartDeadEndTurnaround(vehicle, currentTick);
                }
                else if (!isDeadEnd && _inDeadEnd)
                {
                    // Left the dead-end
                    float distanceFromEntry = (position - _deadEndEntryPosition).Length();
                    if (distanceFromEntry > Constants.DEAD_END_ESCAPE_DISTANCE)
                    {
                        _inDeadEnd = false;
                        _deadEndTurnCount = 0;
                        Logger.Info("Successfully escaped dead-end");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckDeadEnd");
            }
        }

        /// <summary>
        /// Initiate turnaround maneuver when at a dead-end
        /// </summary>
        private void StartDeadEndTurnaround(Vehicle vehicle, long currentTick)
        {
            try
            {
                Ped player = Game.Player.Character;

                // Clear current task
                Function.Call(_clearPedTasksHash, player.Handle);

                // Perform a reverse and turn maneuver
                _deadEndTurnCount++;

                // Alternate turn direction on repeated attempts
                int action = (_deadEndTurnCount % 2 == 1) ?
                    Constants.TEMP_ACTION_REVERSE_LEFT :
                    Constants.TEMP_ACTION_REVERSE_RIGHT;

                Function.Call(
                    _taskVehicleTempActionHash,
                    player.Handle, vehicle.Handle, action, 2500);  // 2.5 second reverse

                // Re-issue wander task after turnaround completes
                // This will happen automatically when recovery is processed
                _recoveryState = Constants.RECOVERY_STATE_REVERSING;
                _recoveryStartTick = currentTick;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StartDeadEndTurnaround");
            }
        }

        /// <summary>
        /// Check if approaching a restricted area and reroute if needed
        /// Only active for Cautious and Normal driving styles
        /// </summary>
        public void CheckRestrictedArea(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_autoDriveActive) return;

            // Only check in cautious/normal modes where AvoidRestrictedAreas is active
            if (_currentDrivingStyleMode == Constants.DRIVING_STYLE_MODE_FAST ||
                _currentDrivingStyleMode == Constants.DRIVING_STYLE_MODE_RECKLESS)
                return;

            try
            {
                // Look ahead in driving direction
                // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                float heading = vehicle.Heading;
                float speed = vehicle.Speed;
                float lookahead = Math.Max(Constants.COLLISION_LOOKAHEAD_MIN, speed * Constants.COLLISION_LOOKAHEAD_TIME_FACTOR); // At least 30m, or 2 seconds ahead

                float radians = (90f - heading) * Constants.DEG_TO_RAD;
                Vector3 aheadPos = position + new Vector3(
                    (float)Math.Cos(radians) * lookahead,
                    (float)Math.Sin(radians) * lookahead,
                    0f);

                // Check if the area ahead is restricted
                bool isRestricted = IsPositionRestricted(aheadPos);

                if (isRestricted && _wanderMode)
                {
                    // Force a reroute by restarting wander
                    TryAnnounce("Restricted area ahead, rerouting", Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                    Logger.Info("Restricted area detected ahead, rerouting");

                    // Clear task and re-issue to force new route calculation
                    Ped player = Game.Player.Character;
                    Function.Call(_clearPedTasksHash, player.Handle);

                    IssueWanderTask(player, vehicle, _targetSpeed);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckRestrictedArea");
            }
        }

        /// <summary>
        /// Check if a position is in a restricted area (military base, airport, etc.)
        /// </summary>
        private bool IsPositionRestricted(Vector3 position)
        {
            try
            {
                // Get zone at position
                string zoneName = World.GetZoneLocalizedName(position);

                // Check for known restricted zone names (military, airport restricted areas)
                if (zoneName != null)
                {
                    string zone = zoneName.ToUpperInvariant();
                    if (zone.Contains("ZANCUD") || // Fort Zancudo
                        zone.Contains("HUMLAB") || // Humane Labs
                        zone.Contains("AIRP"))     // Airport restricted areas
                    {
                        return true;
                    }
                }

                // Additional check using road node flags for restricted roads
                Function.Call(
                    _getVehicleNodePropsHash,
                    position.X, position.Y, position.Z,
                    _density, _flags);

                int nodeFlags = _flags.GetResult<int>();
                int density = _density.GetResult<int>();

                // Check for specific flag combinations that indicate restricted areas
                // Off-road (1) + Dead-end (32) in low density often = restricted entrance
                if ((nodeFlags & Constants.ROAD_FLAG_OFF_ROAD) != 0 &&
                    (nodeFlags & Constants.ROAD_FLAG_DEAD_END) != 0 &&
                    density <= 2)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (Logger.IsDebugEnabled) Logger.Debug($"IsPositionRestricted check failed: {ex.Message}");
            }

            return false;
        }

        #endregion

        #region Road Seeking

        /// <summary>
        /// Start seeking a specific road type
        /// </summary>
        public void StartSeeking(int seekMode)
        {
            // Critical null check - player can be null during death/respawn
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                _audio.Speak("Cannot start seeking right now.");
                return;
            }

            Vehicle vehicle = player.CurrentVehicle;

            // Validation
            if (vehicle == null || !vehicle.Exists() || !player.IsInVehicle())
            {
                _audio.Speak("You must be in a vehicle to seek roads.");
                return;
            }

            if (player.SeatIndex != VehicleSeat.Driver)
            {
                _audio.Speak("You must be in the driver seat.");
                return;
            }

            // Bounds check seek mode
            if (seekMode < 0 || seekMode >= Constants.ROAD_SEEK_MODE_NAMES.Length)
            {
                _audio.Speak("Invalid seek mode.");
                return;
            }

            _seekMode = seekMode;
            _seekStartTick = DateTime.Now.Ticks; // Track when seeking started for timeout

            // FIX: Initialize seek state variables to prevent stale state from previous seeks
            _seekAttempts = 0;
            _lastSeekScanTick = 0;  // Allow immediate first scan
            _lastIssuedSeekTarget = Vector3.Zero;
            _lastIssuedTaskWasWander = false;

            // If seeking "Any Road", just start wander
            if (seekMode == Constants.ROAD_SEEK_MODE_ANY)
            {
                _seekingRoad = false;
                StartWander();
                return;
            }

            // Get current road type
            Vector3 position = player.Position;
            Logger.Info($"StartSeeking: mode={seekMode}, position=({position.X:F1}, {position.Y:F1}, {position.Z:F1})");

            int currentRoadType = GetRoadTypeAtPosition(position);
            int desiredRoadType = SeekModeToRoadType(seekMode);

            // Bounds check for road type array access
            if (desiredRoadType < 0 || desiredRoadType >= Constants.ROAD_TYPE_NAMES.Length)
            {
                _audio.Speak("Unknown road type.");
                return;
            }

            // Check if already on desired road type
            if (currentRoadType == desiredRoadType)
            {
                _seekingRoad = true;
                _onDesiredRoadType = true;

                string roadName = Constants.GetRoadTypeName(desiredRoadType);
                _audio.Speak($"Already on {roadName}. Staying on this road type.");

                // Start wander to stay on this road
                StartWanderInternal();
                return;
            }

            // Scan for road type
            if (Logger.IsDebugEnabled) Logger.Debug($"StartSeeking: Beginning scan for road type {desiredRoadType}");
            Vector3 foundPosition = Vector3.Zero;

            try
            {
                foundPosition = ScanForRoadType(position, desiredRoadType);
                if (Logger.IsDebugEnabled) Logger.Debug($"StartSeeking: Scan complete, found={foundPosition != Vector3.Zero}");
            }
            catch (Exception scanEx)
            {
                Logger.Exception(scanEx, "StartSeeking.ScanForRoadType");
                _audio.Speak("Road scan failed. Starting wander mode.");
                _seekingRoad = true;
                _onDesiredRoadType = false;
                _seekTargetPosition = Vector3.Zero;
                StartWanderInternal();
                return;
            }

            if (foundPosition != Vector3.Zero)
            {
                // Found road type - navigate to it
                _seekingRoad = true;
                _onDesiredRoadType = false;
                _seekTargetPosition = foundPosition;

                if (Logger.IsDebugEnabled) Logger.Debug($"StartSeeking: Navigating to found position ({foundPosition.X:F1}, {foundPosition.Y:F1}, {foundPosition.Z:F1})");
                NavigateToSeekTarget(player, vehicle, foundPosition);
            }
            else
            {
                // Not found - start wander and keep scanning
                _seekingRoad = true;
                _onDesiredRoadType = false;
                _seekTargetPosition = Vector3.Zero;

                string roadName = Constants.GetRoadSeekModeName(seekMode);
                _audio.Speak($"No {roadName} found nearby. Wandering until one is found.");

                Logger.Debug("StartSeeking: No road found, starting wander");
                StartWanderInternal();
            }
        }

        /// <summary>
        /// Convert seek mode to road type constant
        /// </summary>
        private int SeekModeToRoadType(int seekMode)
        {
            switch (seekMode)
            {
                case Constants.ROAD_SEEK_MODE_HIGHWAY:
                    return Constants.ROAD_TYPE_HIGHWAY;
                case Constants.ROAD_SEEK_MODE_CITY:
                    return Constants.ROAD_TYPE_CITY_STREET;
                case Constants.ROAD_SEEK_MODE_SUBURBAN:
                    return Constants.ROAD_TYPE_SUBURBAN;
                case Constants.ROAD_SEEK_MODE_RURAL:
                    return Constants.ROAD_TYPE_RURAL;
                case Constants.ROAD_SEEK_MODE_DIRT:
                    return Constants.ROAD_TYPE_DIRT_TRAIL;
                default:
                    return Constants.ROAD_TYPE_UNKNOWN;
            }
        }

        /// <summary>
        /// Scan surrounding area for a specific road type
        /// Returns position of closest matching road, or Vector3.Zero if not found
        /// </summary>
        private Vector3 ScanForRoadType(Vector3 origin, int desiredRoadType)
        {
            // Defensive: Validate origin position
            if (float.IsNaN(origin.X) || float.IsNaN(origin.Y) || float.IsNaN(origin.Z) ||
                float.IsInfinity(origin.X) || float.IsInfinity(origin.Y) || float.IsInfinity(origin.Z))
            {
                Logger.Warning("ScanForRoadType: invalid origin position");
                return Vector3.Zero;
            }

            // Defensive: Validate desired road type is in valid range
            if (desiredRoadType < 0 || desiredRoadType >= Constants.ROAD_TYPE_NAMES.Length)
            {
                Logger.Warning($"ScanForRoadType: invalid desiredRoadType {desiredRoadType}");
                return Vector3.Zero;
            }

            Vector3 closestPosition = Vector3.Zero;
            float closestDistance = float.MaxValue;
            int nativeCallCount = 0;
            const int MAX_NATIVE_CALLS_PER_SCAN = 48;  // Limit to prevent overwhelming the game engine

            try
            {
                // Radial scan: 12 angles x 6 distances = 72 samples max
                // Defensive: Prevent division by zero
                float sampleAngle = Constants.ROAD_SEEK_SAMPLE_ANGLE;
                float sampleDistance = Constants.ROAD_SEEK_SAMPLE_DISTANCE;
                if (sampleAngle <= 0f) sampleAngle = 30f;
                if (sampleDistance <= 0f) sampleDistance = 50f;

                int angleSteps = (int)(360f / sampleAngle);
                int distanceSteps = (int)(Constants.ROAD_SEEK_SCAN_RADIUS / sampleDistance);

                // Defensive: Limit iterations to prevent runaway loops (use >= for boundary)
                if (angleSteps <= 0 || angleSteps >= 36) angleSteps = 12;
                if (distanceSteps <= 0 || distanceSteps >= 20) distanceSteps = 6;

                if (Logger.IsDebugEnabled) Logger.Debug($"ScanForRoadType: Starting scan with {angleSteps} angles x {distanceSteps} distances");

                for (int d = 1; d <= distanceSteps; d++)
                {
                    float distance = d * Constants.ROAD_SEEK_SAMPLE_DISTANCE;

                    for (int a = 0; a < angleSteps; a++)
                    {
                        try
                        {
                            // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                            float angle = a * Constants.ROAD_SEEK_SAMPLE_ANGLE;
                            float radians = angle * Constants.DEG_TO_RAD;

                            // PERFORMANCE: Reuse pre-allocated vector instead of allocating new one
                            _scanSamplePos.X = origin.X + (float)Math.Cos(radians) * distance;
                            _scanSamplePos.Y = origin.Y + (float)Math.Sin(radians) * distance;
                            _scanSamplePos.Z = origin.Z;
                            Vector3 samplePos = _scanSamplePos;

                            // Skip if sample position is invalid
                            if (float.IsNaN(samplePos.X) || float.IsNaN(samplePos.Y))
                                continue;

                            // FIX: Limit native calls to prevent overwhelming the game engine
                            if (nativeCallCount >= MAX_NATIVE_CALLS_PER_SCAN)
                            {
                                if (Logger.IsDebugEnabled) Logger.Debug($"ScanForRoadType: Hit native call limit ({MAX_NATIVE_CALLS_PER_SCAN}), stopping scan early");
                                break;
                            }

                            // Get closest road node
                            nativeCallCount++;
                            bool found = Function.Call<bool>(
                                _getClosestNodeWithHeadingHash,
                                samplePos.X, samplePos.Y, samplePos.Z,
                                _seekNodePos, _seekNodeHeading, 1, 3f, 0f);

                            if (!found) continue;
                            nativeCallCount++;  // Count the GET_VEHICLE_NODE_PROPERTIES call too

                            Vector3 nodePos = _seekNodePos.GetResult<Vector3>();

                            // Validate node position
                            if (float.IsNaN(nodePos.X) || float.IsNaN(nodePos.Y) || float.IsNaN(nodePos.Z))
                                continue;

                            // Get road properties
                            Function.Call(
                                _getVehicleNodePropsHash,
                                nodePos.X, nodePos.Y, nodePos.Z,
                                _seekDensity, _seekFlags);

                            int nodeFlags = _seekFlags.GetResult<int>();
                            int nodeDensity = _seekDensity.GetResult<int>();

                            // Validate OutputArgument results - skip if obviously corrupted
                            // Flags should be reasonable bit flags (0-65535), density 0-15
                            if (nodeFlags < 0 || nodeFlags > 0xFFFF || nodeDensity < 0 || nodeDensity > 15)
                                continue;

                            int roadType = ClassifyRoadType(nodeFlags, nodeDensity);

                            // Validate road type result
                            if (roadType < 0 || roadType >= Constants.ROAD_TYPE_NAMES.Length)
                                continue;

                            if (roadType == desiredRoadType)
                            {
                                float distToNode = (nodePos - origin).Length();
                                if (!float.IsNaN(distToNode) && distToNode < closestDistance)
                                {
                                    closestDistance = distToNode;
                                    closestPosition = nodePos;
                                }
                            }
                        }
                        catch (Exception innerEx)
                        {
                            // Log but continue scanning other positions
                            if (Logger.IsDebugEnabled) Logger.Debug($"ScanForRoadType inner error at angle {a}: {innerEx.Message}");
                        }
                    }

                    // FIX: Also break outer loop when native call limit is hit
                    if (nativeCallCount >= MAX_NATIVE_CALLS_PER_SCAN)
                        break;
                }

                if (Logger.IsDebugEnabled) Logger.Debug($"ScanForRoadType: Completed with {nativeCallCount} native calls, found={closestPosition != Vector3.Zero}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ScanForRoadType");
            }

            return closestPosition;
        }

        /// <summary>
        /// Navigate to seek target position
        /// </summary>
        private void NavigateToSeekTarget(Ped player, Vehicle vehicle, Vector3 targetPos)
        {
            // Defensive: Validate entities before using them
            if (player == null || !player.Exists())
            {
                Logger.Warning("NavigateToSeekTarget: player is null or doesn't exist");
                return;
            }

            if (vehicle == null || !vehicle.Exists())
            {
                Logger.Warning("NavigateToSeekTarget: vehicle is null or doesn't exist");
                return;
            }

            // Defensive: Validate target position
            if (float.IsNaN(targetPos.X) || float.IsNaN(targetPos.Y) || float.IsNaN(targetPos.Z) ||
                float.IsInfinity(targetPos.X) || float.IsInfinity(targetPos.Y) || float.IsInfinity(targetPos.Z))
            {
                Logger.Warning("NavigateToSeekTarget: invalid target position");
                return;
            }

            // Defensive: Validate driving style mode is in range
            if (_currentDrivingStyleMode < 0 || _currentDrivingStyleMode >= Constants.DRIVING_STYLE_VALUES.Length)
            {
                Logger.Warning($"NavigateToSeekTarget: invalid driving style mode {_currentDrivingStyleMode}");
                return;
            }

            // Defensive: Validate seek mode for announcement
            if (_seekMode < 0 || _seekMode >= Constants.ROAD_SEEK_MODE_NAMES.Length)
            {
                Logger.Warning($"NavigateToSeekTarget: invalid seek mode {_seekMode}");
                return;
            }

            try
            {
                // Stop any existing task
                Function.Call(_clearPedTasksHash, player.Handle);

                // Issue drive task to target using helper (supports LONGRANGE for distant targets)
                IssueDriveToCoordTask(player, vehicle, targetPos, _targetSpeed,
                    Constants.ROAD_SEEK_ARRIVAL_THRESHOLD);

                _autoDriveActive = true;
                _wanderMode = false;
                _taskIssued = true;

                // FIX: Update task tracking state (this task is a drive-to-coord, not wander)
                _lastIssuedSeekTarget = targetPos;
                _lastIssuedTaskWasWander = false;

                // Announce navigation
                float distanceFeet = (targetPos - player.Position).Length() * Constants.METERS_TO_FEET;
                string roadName = Constants.GetRoadSeekModeName(_seekMode);
                _audio.Speak($"Navigating to {roadName}, {(int)distanceFeet} feet away");
                if (Logger.IsDebugEnabled) Logger.Debug($"NavigateToSeekTarget: Issued drive task to {roadName}, {distanceFeet:F0} feet away");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "NavigateToSeekTarget");
                _audio.Speak("Failed to navigate to road.");
            }
        }

        /// <summary>
        /// Internal wander start without duplicate announcements
        /// Idempotent - safe to call multiple times, only issues task if needed
        /// </summary>
        private void StartWanderInternal()
        {
            Ped player = Game.Player.Character;
            Vehicle vehicle = player.CurrentVehicle;

            if (vehicle == null) return;

            // FIX: Make idempotent - only issue wander task if not already wandering
            // This prevents task spam that causes crashes
            if (_wanderMode && _taskIssued && _lastIssuedTaskWasWander)
            {
                Logger.Debug("StartWanderInternal: Already wandering, skipping task reissue");
                return;
            }

            try
            {
                Function.Call(_clearPedTasksHash, player.Handle);

                // Use optimized cruise-style wander task
                IssueWanderTask(player, vehicle, _targetSpeed);

                _autoDriveActive = true;
                _wanderMode = true;
                _taskIssued = true;

                // FIX: Update task tracking state
                _lastIssuedTaskWasWander = true;
                _lastIssuedSeekTarget = Vector3.Zero;

                Logger.Debug("StartWanderInternal: Issued wander task");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StartWanderInternal");
            }
        }

        /// <summary>
        /// Update seeking state when road type changes
        /// </summary>
        private void UpdateSeekingState(int currentRoadType)
        {
            if (!_seekingRoad || _seekMode == Constants.ROAD_SEEK_MODE_ANY)
                return;

            int desiredRoadType = SeekModeToRoadType(_seekMode);

            // Bounds check before array access
            if (desiredRoadType < 0 || desiredRoadType >= Constants.ROAD_TYPE_NAMES.Length)
            {
                Logger.Warning($"UpdateSeekingState: invalid desiredRoadType {desiredRoadType}");
                return;
            }

            if (currentRoadType == desiredRoadType)
            {
                // Arrived on desired road type
                if (!_onDesiredRoadType)
                {
                    _onDesiredRoadType = true;
                    string roadName = Constants.GetRoadTypeName(desiredRoadType);
                    _audio.Speak($"Now on {roadName}. Staying on this road type.");

                    // Switch to wander to stay on this road
                    StartWanderInternal();
                }
            }
            else
            {
                // Left desired road type
                if (_onDesiredRoadType)
                {
                    _onDesiredRoadType = false;
                    string roadName = Constants.GetRoadTypeName(desiredRoadType);
                    _audio.Speak($"Left {roadName}. Searching for route back.");
                }
            }
        }

        /// <summary>
        /// Update road seeking - rescan and navigate if drifted off
        /// Called from OnTick during AutoDrive
        /// </summary>
        public void UpdateRoadSeeking(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_seekingRoad || _seekMode == Constants.ROAD_SEEK_MODE_ANY)
                return;

            // Defensive: Validate seek mode is in valid range
            if (_seekMode < 0 || _seekMode >= Constants.ROAD_SEEK_MODE_NAMES.Length)
            {
                Logger.Warning($"UpdateRoadSeeking: invalid seek mode {_seekMode}, stopping seek");
                StopSeeking();
                return;
            }

            // Defensive: Validate vehicle exists
            if (vehicle == null || !vehicle.Exists())
            {
                Logger.Warning("UpdateRoadSeeking: vehicle is null or doesn't exist");
                StopSeeking();
                return;
            }

            // Defensive: Validate position
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z))
            {
                Logger.Warning("UpdateRoadSeeking: invalid position");
                return;
            }

            // Defensive: Validate tick value
            if (currentTick < 0)
                return;

            // Throttle scans
            if (currentTick - _lastSeekScanTick < Constants.TICK_INTERVAL_ROAD_SEEK_SCAN)
                return;

            _lastSeekScanTick = currentTick;
            _seekAttempts++;

            // Timeout check - stop seeking after 10 minutes (600 seconds)
            const long SEEK_TIMEOUT_TICKS = 600L * 10_000_000L; // 10 minutes in ticks
            if (_seekStartTick > 0 && currentTick - _seekStartTick > SEEK_TIMEOUT_TICKS)
            {
                _audio.Speak("Road seeking timed out. Stopping search.");
                StopSeeking();
                return;
            }

            // Max attempts check - stop after 200 scans (about 10 minutes at 3-second intervals)
            const int MAX_SEEK_ATTEMPTS = 200;
            if (_seekAttempts > MAX_SEEK_ATTEMPTS)
            {
                _audio.Speak("Maximum search attempts reached. Stopping search.");
                StopSeeking();
                return;
            }

            // If on desired road type, keep wandering
            if (_onDesiredRoadType)
                return;

            try
            {
                // Check if we've arrived at the seek target position
                // This handles the case where the drive task completes before road type detection
                if (_seekTargetPosition != Vector3.Zero)
                {
                    float distanceToTarget = (position - _seekTargetPosition).Length();
                    if (!float.IsNaN(distanceToTarget) && distanceToTarget < Constants.ROAD_SEEK_ARRIVAL_THRESHOLD)
                    {
                        // Arrived at target - switch to wander mode to continue driving
                        int currentRoadType = GetRoadTypeAtPosition(position);
                        int desiredRoadType = SeekModeToRoadType(_seekMode);

                        // Validate road types before array access
                        if (desiredRoadType >= 0 && desiredRoadType < Constants.ROAD_TYPE_NAMES.Length)
                        {
                            if (currentRoadType == desiredRoadType)
                            {
                                // On the desired road type - stay here
                                _onDesiredRoadType = true;
                                string roadName = Constants.GetRoadTypeName(desiredRoadType);
                                _audio.Speak($"Arrived on {roadName}. Staying on this road type.");
                                StartWanderInternal();
                                return;
                            }
                        }

                        // Arrived at target but not on desired road type yet
                        // Clear target so we rescan for a better position
                        _seekTargetPosition = Vector3.Zero;
                    }
                }

                // Not on desired road - try to find and navigate to it
                int targetRoadType = SeekModeToRoadType(_seekMode);

                // Validate target road type
                if (targetRoadType < 0 || targetRoadType >= Constants.ROAD_TYPE_NAMES.Length)
                {
                    Logger.Warning($"UpdateRoadSeeking: invalid target road type {targetRoadType}");
                    return;
                }

                Vector3 foundPosition = ScanForRoadType(position, targetRoadType);

                if (foundPosition != Vector3.Zero)
                {
                    Ped player = Game.Player.Character;
                    if (player != null && player.Exists() && player.IsInVehicle() && player.SeatIndex == VehicleSeat.Driver)
                    {
                        // FIX: Only navigate if target changed significantly (> 20 meters) or no task issued yet
                        // This prevents task spam that causes crashes after ~1 minute of seeking
                        bool targetChanged = _lastIssuedSeekTarget == Vector3.Zero ||
                            (foundPosition - _lastIssuedSeekTarget).Length() > 20f;

                        bool needsNewTask = targetChanged || _lastIssuedTaskWasWander;

                        if (needsNewTask)
                        {
                            _seekTargetPosition = foundPosition;
                            NavigateToSeekTarget(player, vehicle, foundPosition);
                            _lastIssuedSeekTarget = foundPosition;
                            _lastIssuedTaskWasWander = false;
                            if (Logger.IsDebugEnabled) Logger.Debug($"UpdateRoadSeeking: Issued new drive task to target {(foundPosition - position).Length():F1}m away");
                        }
                        else
                        {
                            if (Logger.IsDebugEnabled) Logger.Debug($"UpdateRoadSeeking: Skipped task reissue (target unchanged, distance: {(foundPosition - _lastIssuedSeekTarget).Length():F1}m)");
                        }
                    }
                }
                else if (_seekTargetPosition == Vector3.Zero)
                {
                    // No target found and no current target - wander until we find one
                    // Only call if not already wandering (prevent task spam)
                    if (!_lastIssuedTaskWasWander || !_wanderMode || !_taskIssued)
                    {
                        StartWanderInternal();
                        _lastIssuedTaskWasWander = true;
                        _lastIssuedSeekTarget = Vector3.Zero;
                        Logger.Debug("UpdateRoadSeeking: Issued wander task (no target found)");
                    }
                    else
                    {
                        Logger.Debug("UpdateRoadSeeking: Skipped wander task reissue (already wandering)");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "UpdateRoadSeeking");
            }
        }

        /// <summary>
        /// Stop seeking and clear seek state
        /// </summary>
        public void StopSeeking()
        {
            _seekingRoad = false;
            _seekMode = Constants.ROAD_SEEK_MODE_ANY;
            _onDesiredRoadType = false;
            _seekTargetPosition = Vector3.Zero;
            _seekStartTick = 0;
            _seekAttempts = 0;

            // FIX: Reset task tracking state
            _lastIssuedSeekTarget = Vector3.Zero;
            _lastIssuedTaskWasWander = false;
        }

        #endregion

        #region Driving Styles

        /// <summary>
        /// Cycle to next driving style
        /// </summary>
        public void CycleDrivingStyle()
        {
            _currentDrivingStyleMode = (_currentDrivingStyleMode + 1) % 4;

            // Re-issue driving task with new style if currently driving
            // Research shows SET_DRIVE_TASK_DRIVING_STYLE alone may not update all flags
            // (especially pathfinding flags like StopAtTrafficLights, TakeShortestPath)
            if (_autoDriveActive)
            {
                ReissueTaskWithNewStyle();
            }

            string styleName = Constants.GetDrivingStyleName(_currentDrivingStyleMode);
            int styleValue = Constants.GetDrivingStyleValue(_currentDrivingStyleMode);
            _audio.Speak($"Driving style: {styleName}");
            Logger.Info($"CycleDrivingStyle: Changed to {styleName} (value={styleValue})");
        }

        /// <summary>
        /// Re-issue the current driving task with the new style.
        /// This is necessary because SET_DRIVE_TASK_DRIVING_STYLE may not update
        /// all flag-based behaviors (like traffic light stopping) mid-task.
        /// </summary>
        private void ReissueTaskWithNewStyle()
        {
            try
            {
                Ped player = Game.Player.Character;
                Vehicle vehicle = player?.CurrentVehicle;

                if (player == null || vehicle == null || !player.IsInVehicle())
                    return;

                int styleValue = Constants.GetDrivingStyleValue(_currentDrivingStyleMode);
                float ability = Constants.GetDrivingStyleAbility(_currentDrivingStyleMode);
                float aggressiveness = Constants.GetDrivingStyleAggressiveness(_currentDrivingStyleMode);
                float speedMultiplier = Constants.GetDrivingStyleSpeedMultiplier(_currentDrivingStyleMode);

                // Apply speed multiplier - THIS is the primary differentiator between styles
                float adjustedSpeed = _targetSpeed * speedMultiplier;

                if (Logger.IsDebugEnabled) Logger.Debug($"ReissueTaskWithNewStyle: style={styleValue}, speedMult={speedMultiplier}, adjustedSpeed={adjustedSpeed}, wanderMode={_wanderMode}");

                // Clear current task
                Function.Call(_clearPedTasksHash, player.Handle);

                if (_wanderMode)
                {
                    // Re-issue wander task with new style
                    Function.Call(
                        _taskVehicleDriveWanderHash,
                        player.Handle,
                        vehicle.Handle,
                        adjustedSpeed,
                        styleValue);
                }
                else if (_safeArrivalPosition != Vector3.Zero)
                {
                    // Re-issue waypoint task with new style
                    float distance = Vector3.Distance(player.Position, _safeArrivalPosition);
                    bool useLongRange = distance > Constants.AUTODRIVE_LONGRANGE_THRESHOLD;

                    if (useLongRange)
                    {
                        Function.Call(
                            _taskDriveToCoordLongrangeHash,
                            player.Handle,
                            vehicle.Handle,
                            _safeArrivalPosition.X,
                            _safeArrivalPosition.Y,
                            _safeArrivalPosition.Z,
                            adjustedSpeed,
                            styleValue,
                            Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS);
                    }
                    else
                    {
                        Function.Call(
                            _taskDriveToCoordHash,
                            player.Handle,
                            vehicle.Handle,
                            _safeArrivalPosition.X,
                            _safeArrivalPosition.Y,
                            _safeArrivalPosition.Z,
                            adjustedSpeed,
                            0,
                            vehicle.Model.Hash,
                            styleValue,
                            Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS,
                            0f);
                    }
                }

                // CRITICAL: Set cruise speed AFTER issuing task (reference: AutoDriveScript2.cs)
                Function.Call(_setCruiseSpeedHash, player.Handle, adjustedSpeed);

                // Apply ability and aggressiveness
                Function.Call(_setDriverAbilityHash, player.Handle, ability);
                Function.Call(_setDriverAggressivenessHash, player.Handle, aggressiveness);

                _taskIssued = true;
                Logger.Info($"ReissueTaskWithNewStyle: style={styleValue} ({Constants.GetDrivingStyleName(_currentDrivingStyleMode)}), speedMult={speedMultiplier:F1}, adjustedSpeed={adjustedSpeed:F1}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ReissueTaskWithNewStyle");
            }
        }

        /// <summary>
        /// Announce current driving style
        /// </summary>
        public void AnnounceDrivingStyle()
        {
            string styleName = Constants.GetDrivingStyleName(_currentDrivingStyleMode);
            _audio.Speak($"Current driving style: {styleName}");
        }

        /// <summary>
        /// Issue a drive-to-coordinate task, using LONGRANGE native for distant waypoints.
        /// VAutodrive research shows LONGRANGE has better pathfinding for long distances.
        /// </summary>
        /// <param name="player">The ped (player) to drive</param>
        /// <param name="vehicle">The vehicle to drive</param>
        /// <param name="destination">Target coordinates</param>
        /// <param name="speed">Driving speed in m/s</param>
        /// <param name="arrivalRadius">Distance at which to consider arrived</param>
        /// <param name="styleOverride">Optional style override (-1 to use current style)</param>
        private void IssueDriveToCoordTask(Ped player, Vehicle vehicle, Vector3 destination, float speed, float arrivalRadius, int styleOverride = -1)
        {
            if (player == null || vehicle == null) return;

            try
            {
                float distance = Vector3.Distance(player.Position, destination);
                bool useLongRange = distance > Constants.AUTODRIVE_LONGRANGE_THRESHOLD;

                // Use provided style or get from current mode
                int styleValue = styleOverride >= 0 ? styleOverride : Constants.GetDrivingStyleValue(_currentDrivingStyleMode);

                // For longrange, use optimized style for better pathfinding
                if (useLongRange && styleOverride < 0)
                {
                    styleValue = Constants.DRIVING_STYLE_LONGRANGE;
                }

                float ability = Constants.GetDrivingStyleAbility(_currentDrivingStyleMode);
                float aggressiveness = Constants.GetDrivingStyleAggressiveness(_currentDrivingStyleMode);

                if (Logger.IsDebugEnabled) Logger.Debug($"IssueDriveToCoordTask: dist={distance:F0}m, longRange={useLongRange}, style={styleValue}");

                if (useLongRange)
                {
                    // TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE: 8 params
                    // Better pathfinding for long distances (VAutodrive research)
                    Function.Call(
                        _taskDriveToCoordLongrangeHash,
                        player.Handle,
                        vehicle.Handle,
                        destination.X,
                        destination.Y,
                        destination.Z,
                        speed,
                        styleValue,
                        arrivalRadius);
                }
                else
                {
                    // TASK_VEHICLE_DRIVE_TO_COORD: 11 params (for shorter distances)
                    Function.Call(
                        _taskDriveToCoordHash,
                        player.Handle,
                        vehicle.Handle,
                        destination.X,
                        destination.Y,
                        destination.Z,
                        speed,
                        0,  // p6 - not used
                        vehicle.Model.Hash,
                        styleValue,
                        arrivalRadius,
                        0f);  // p10
                }

                // CRITICAL: Set cruise speed AFTER issuing task (reference: AutoDriveScript2.cs)
                Function.Call(_setCruiseSpeedHash, player.Handle, speed);

                // Set driver ability and aggressiveness
                Function.Call(_setDriverAbilityHash, player.Handle, ability);
                Function.Call(_setDriverAggressivenessHash, player.Handle, aggressiveness);

                if (Logger.IsDebugEnabled) Logger.Debug($"IssueDriveToCoordTask: Cruise speed set to {speed:F1}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "IssueDriveToCoordTask");
            }
        }

        /// <summary>
        /// Issue a wander/cruise task with optimized settings for smooth driving.
        /// Research shows: CRUISE style + ability 1.0 + aggressiveness 0.0 = smoothest AI driving.
        /// </summary>
        /// <param name="player">The ped (player) to drive</param>
        /// <param name="vehicle">The vehicle to drive</param>
        /// <param name="speed">Driving speed in m/s</param>
        private void IssueWanderTask(Ped player, Vehicle vehicle, float speed)
        {
            if (player == null || vehicle == null) return;

            try
            {
                // Use the user's selected driving style (respects CycleDrivingStyle choice)
                int styleValue = Constants.GetDrivingStyleValue(_currentDrivingStyleMode);
                float ability = Constants.GetDrivingStyleAbility(_currentDrivingStyleMode);
                float aggressiveness = Constants.GetDrivingStyleAggressiveness(_currentDrivingStyleMode);
                float speedMultiplier = Constants.GetDrivingStyleSpeedMultiplier(_currentDrivingStyleMode);

                // Apply speed multiplier - THIS is the primary differentiator between styles
                // Reference: AutoDriveScript2.cs uses 0.7f (Cautious), 1.0f (Normal), 1.2f (Aggressive)
                float adjustedSpeed = speed * speedMultiplier;

                Function.Call(
                    _taskVehicleDriveWanderHash,
                    player.Handle,
                    vehicle.Handle,
                    adjustedSpeed,
                    styleValue);

                // CRITICAL: Set cruise speed AFTER issuing task (reference: AutoDriveScript2.cs line 416)
                // This ensures the speed is applied to the active task
                Function.Call(_setCruiseSpeedHash, player.Handle, adjustedSpeed);

                Function.Call(_setDriverAbilityHash, player.Handle, ability);
                Function.Call(_setDriverAggressivenessHash, player.Handle, aggressiveness);

                Logger.Info($"IssueWanderTask: style={styleValue} ({Constants.GetDrivingStyleName(_currentDrivingStyleMode)}), speedMult={speedMultiplier:F1}, adjustedSpeed={adjustedSpeed:F1}, ability={ability:F1}, aggression={aggressiveness:F1}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "IssueWanderTask");
            }
        }

        /// <summary>
        /// Check if task needs re-issuing based on deviation from target
        /// Returns true only if vehicle has significantly deviated from path
        /// This prevents jerky movement from unnecessary task re-issues
        /// </summary>
        private bool NeedsTaskReissue(Vehicle vehicle, Vector3 targetPosition)
        {
            if (vehicle == null || !vehicle.Exists()) return false;

            Vector3 currentPos = vehicle.Position;
            Vector3 toTarget = targetPosition - currentPos;
            float distanceToTarget = toTarget.Length();

            // If very close to target, no need to re-issue
            if (distanceToTarget < Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS) return false;

            // Check if we've deviated significantly from the path to target
            // Calculate how far off course we are based on heading vs target direction
            // PERFORMANCE: Use pre-calculated RAD_TO_DEG constant
            float targetHeading = (float)(Math.Atan2(toTarget.Y, toTarget.X) * Constants.RAD_TO_DEG);
            float currentHeading = vehicle.Heading;

            // Normalize heading difference to -180 to 180
            float headingDiff = targetHeading - currentHeading;
            while (headingDiff > 180f) headingDiff -= 360f;
            while (headingDiff < -180f) headingDiff += 360f;

            // Ultra-precise re-issue logic (Grok optimization):
            // Only re-issue if ALL THREE conditions are met:
            // 1. Far from expected path (> TASK_DEVIATION_THRESHOLD)
            // 2. Heading significantly off (> 45°)
            // 3. Vehicle is slow/stopped (not just turning)
            // This prevents unnecessary task interruptions during normal navigation
            if (distanceToTarget > Constants.TASK_DEVIATION_THRESHOLD &&
                Math.Abs(headingDiff) > Constants.TASK_HEADING_DEVIATION_THRESHOLD)
            {
                float speed = vehicle.Speed;
                if (speed < Constants.STUCK_SPEED_THRESHOLD)
                {
                    if (Logger.IsDebugEnabled) Logger.Debug($"NeedsTaskReissue: dist={distanceToTarget:F0}m, heading={headingDiff:F0}°, speed={speed:F1}");
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Recovery System

        /// <summary>
        /// Check vehicle state for critical conditions (flipped, water, fire, damage)
        /// Returns true if a critical condition was detected and handled
        /// </summary>
        private bool CheckVehicleState(Vehicle vehicle, long currentTick)
        {
            // Throttle checks
            if (currentTick - _lastVehicleStateCheckTick < Constants.TICK_INTERVAL_RECOVERY_CHECK)
                return false;

            _lastVehicleStateCheckTick = currentTick;

            try
            {
                // Check if vehicle is flipped/upside down
                // Use vehicle's UpVector.Z - 1.0 = upright, 0 = on side, -1 = upside down
                float uprightValue = vehicle.UpVector.Z;

                if (uprightValue < Constants.VEHICLE_UPRIGHT_THRESHOLD)
                {
                    if (!_vehicleFlipped)
                    {
                        _vehicleFlipped = true;
                        _audio.Speak("Vehicle flipped. AutoDrive stopping.");
                        Stop(false);
                        return true;
                    }
                }
                else
                {
                    _vehicleFlipped = false;
                }

                // Check if vehicle is in water
                bool inWater = Function.Call<bool>(
                    _isEntityInWaterHash, vehicle.Handle);

                if (inWater)
                {
                    if (!_vehicleInWater)
                    {
                        _vehicleInWater = true;
                        _audio.Speak("Vehicle in water. AutoDrive stopping.");
                        Stop(false);
                        return true;
                    }
                }
                else
                {
                    _vehicleInWater = false;
                }

                // Check if vehicle is on fire
                if (vehicle.IsOnFire)
                {
                    if (!_vehicleOnFire)
                    {
                        _vehicleOnFire = true;
                        _audio.Speak("Vehicle on fire. AutoDrive stopping.");
                        Stop(false);
                        return true;
                    }
                }
                else
                {
                    _vehicleOnFire = false;
                }

                // Check vehicle health
                float health = vehicle.Health;
                float engineHealth = vehicle.EngineHealth;

                if (health < Constants.VEHICLE_UNDRIVEABLE_HEALTH || engineHealth < Constants.VEHICLE_UNDRIVEABLE_HEALTH)
                {
                    _audio.Speak("Vehicle destroyed. AutoDrive stopping.");
                    Stop(false);
                    return true;
                }

                if (health < Constants.VEHICLE_CRITICAL_HEALTH || engineHealth < Constants.VEHICLE_CRITICAL_HEALTH)
                {
                    if (!_vehicleCriticalDamage)
                    {
                        _vehicleCriticalDamage = true;
                        _audio.Speak("Warning: Vehicle critically damaged.");
                    }
                }
                else
                {
                    _vehicleCriticalDamage = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckVehicleState");
            }

            return false;
        }

        /// <summary>
        /// Check if the vehicle is stuck (not making progress)
        /// </summary>
        private void CheckIfStuck(Vehicle vehicle, Vector3 position, long currentTick)
        {
            // Throttle checks
            if (currentTick - _lastStuckCheckTick < Constants.TICK_INTERVAL_STUCK_CHECK)
                return;

            _lastStuckCheckTick = currentTick;

            // Skip if we recently recovered (cooldown)
            if (currentTick - _lastRecoveryTick < Constants.RECOVERY_COOLDOWN)
                return;

            try
            {
                float speed = vehicle.Speed;
                float heading = vehicle.Heading;

                // If moving at reasonable speed, not stuck
                if (speed > Constants.STUCK_SPEED_THRESHOLD * 2)
                {
                    _stuckCheckCount = 0;
                    _isStuck = false;
                    _lastStuckCheckPosition = position;
                    _lastStuckCheckHeading = heading;
                    return;
                }

                // Calculate movement since last check
                float movement = (_lastStuckCheckPosition - position).Length();
                float headingChange = Math.Abs(NormalizeAngleDiff(heading - _lastStuckCheckHeading));

                // Check if we're stuck (little movement and little heading change)
                if (movement < Constants.STUCK_MOVEMENT_THRESHOLD &&
                    headingChange < Constants.STUCK_HEADING_CHANGE_THRESHOLD &&
                    speed < Constants.STUCK_SPEED_THRESHOLD)
                {
                    _stuckCheckCount++;

                    if (_stuckCheckCount >= Constants.STUCK_CHECK_COUNT_THRESHOLD)
                    {
                        _isStuck = true;
                        Logger.Info($"Vehicle stuck detected: movement={movement:F1}m, speed={speed:F1}m/s, checks={_stuckCheckCount}");
                    }
                }
                else
                {
                    _stuckCheckCount = 0;
                    _isStuck = false;
                }

                _lastStuckCheckPosition = position;
                _lastStuckCheckHeading = heading;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CheckIfStuck");
            }
        }

        /// <summary>
        /// Check if we're making progress toward waypoint (timeout detection)
        /// </summary>
        private void CheckProgressTimeout(Vector3 position, long currentTick)
        {
            // Throttle checks
            if (currentTick - _lastProgressTick < Constants.TICK_INTERVAL_PROGRESS_CHECK)
                return;

            // Initialize on first check
            if (_lastProgressTick == 0)
            {
                _lastProgressTick = currentTick;
                _lastProgressDistance = (_lastWaypointPos - position).Length();
                return;
            }

            float currentDistance = (_lastWaypointPos - position).Length();

            // Check if we've made progress
            if (_lastProgressDistance - currentDistance >= Constants.PROGRESS_DISTANCE_THRESHOLD)
            {
                // Made progress, reset timer
                _lastProgressDistance = currentDistance;
                _lastProgressTick = currentTick;
                return;
            }

            // Check for timeout
            if (currentTick - _lastProgressTick > Constants.PROGRESS_TIMEOUT_TICKS)
            {
                Logger.Info($"Progress timeout: no progress toward waypoint for 30 seconds");
                _audio.Speak("Not making progress toward destination. Attempting to reroute.");

                // Reset progress tracking
                _lastProgressTick = currentTick;
                _lastProgressDistance = currentDistance;

                // Trigger recovery
                _isStuck = true;
            }
        }

        /// <summary>
        /// Start the recovery process with escalating strategies based on attempt number
        /// </summary>
        private void StartRecovery(Ped player, Vehicle vehicle, long currentTick)
        {
            if (_recoveryAttempts >= Constants.RECOVERY_MAX_ATTEMPTS)
            {
                _audio.Speak("Unable to recover after multiple attempts. AutoDrive stopping.");
                Stop(false);
                return;
            }

            _recoveryAttempts++;
            _recoveryStartTick = currentTick;

            // Determine recovery strategy based on attempt number
            int strategy = GetRecoveryStrategy(_recoveryAttempts);

            // Alternate turn direction each attempt
            _recoveryTurnDirection = (_recoveryAttempts % 2 == 1) ? 1 : -1;

            string strategyName = GetRecoveryStrategyName(strategy);
            _audio.Speak($"Attempting recovery, attempt {_recoveryAttempts}, {strategyName}");
            Logger.Info($"Starting recovery attempt {_recoveryAttempts}, strategy: {strategyName}, turn direction: {(_recoveryTurnDirection > 0 ? "right" : "left")}");

            try
            {
                // Clear current task
                Function.Call(_clearPedTasksHash, player.Handle);

                // Execute recovery based on strategy
                switch (strategy)
                {
                    case Constants.RECOVERY_STRATEGY_REVERSE_TURN:
                        // Standard reverse + turn
                        _recoveryState = Constants.RECOVERY_STATE_REVERSING;
                        int reverseAction = _recoveryTurnDirection > 0 ?
                            Constants.TEMP_ACTION_REVERSE_RIGHT :
                            Constants.TEMP_ACTION_REVERSE_LEFT;
                        Function.Call(
                            _taskVehicleTempActionHash,
                            player.Handle, vehicle.Handle, reverseAction, (int)(GetRecoveryReverseDuration() / 10000));
                        break;

                    case Constants.RECOVERY_STRATEGY_FORWARD_TURN:
                        // Try moving forward with opposite turn (might be stuck against something behind)
                        _recoveryState = Constants.RECOVERY_STATE_FORWARD;
                        int forwardAction = _recoveryTurnDirection > 0 ?
                            Constants.TEMP_ACTION_TURN_LEFT :  // Opposite direction
                            Constants.TEMP_ACTION_TURN_RIGHT;
                        Function.Call(
                            _taskVehicleTempActionHash,
                            player.Handle, vehicle.Handle, forwardAction, 2000);
                        break;

                    case Constants.RECOVERY_STRATEGY_THREE_POINT:
                        // Three-point turn: reverse, sharp turn, forward
                        _recoveryState = Constants.RECOVERY_STATE_REVERSING;
                        int threePointAction = _recoveryTurnDirection > 0 ?
                            Constants.TEMP_ACTION_REVERSE_LEFT :  // Start with opposite to create space
                            Constants.TEMP_ACTION_REVERSE_RIGHT;
                        Function.Call(
                            _taskVehicleTempActionHash,
                            player.Handle, vehicle.Handle, threePointAction, 3500);  // Longer reverse for three-point
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StartRecovery");
                _recoveryState = Constants.RECOVERY_STATE_FAILED;
            }
        }

        /// <summary>
        /// Get recovery strategy based on attempt number
        /// </summary>
        private int GetRecoveryStrategy(int attemptNumber)
        {
            if (attemptNumber <= 2)
                return Constants.RECOVERY_STRATEGY_REVERSE_TURN;
            else if (attemptNumber == 3)
                return Constants.RECOVERY_STRATEGY_FORWARD_TURN;
            else
                return Constants.RECOVERY_STRATEGY_THREE_POINT;
        }

        /// <summary>
        /// Get human-readable recovery strategy name
        /// </summary>
        private string GetRecoveryStrategyName(int strategy)
        {
            switch (strategy)
            {
                case Constants.RECOVERY_STRATEGY_REVERSE_TURN:
                    return "reverse and turn";
                case Constants.RECOVERY_STRATEGY_FORWARD_TURN:
                    return "forward maneuver";
                case Constants.RECOVERY_STRATEGY_THREE_POINT:
                    return "three-point turn";
                default:
                    return "recovery";
            }
        }

        /// <summary>
        /// Get reverse duration based on attempt number (escalating)
        /// </summary>
        private long GetRecoveryReverseDuration()
        {
            switch (_recoveryAttempts)
            {
                case 1:
                    return Constants.RECOVERY_REVERSE_DURATION_SHORT;
                case 2:
                    return Constants.RECOVERY_REVERSE_DURATION_MEDIUM;
                default:
                    return Constants.RECOVERY_REVERSE_DURATION_LONG;
            }
        }

        /// <summary>
        /// Update the recovery process (called each tick during recovery)
        /// Handles multiple recovery strategies with escalating complexity
        /// </summary>
        private void UpdateRecovery(Ped player, Vehicle vehicle, Vector3 position, long currentTick)
        {
            long elapsed = currentTick - _recoveryStartTick;
            int strategy = GetRecoveryStrategy(_recoveryAttempts);

            try
            {
                switch (_recoveryState)
                {
                    case Constants.RECOVERY_STATE_REVERSING:
                        // Use escalating reverse duration based on attempt
                        long reverseDuration = GetRecoveryReverseDuration();
                        if (elapsed > reverseDuration)
                        {
                            if (strategy == Constants.RECOVERY_STRATEGY_THREE_POINT)
                            {
                                // Three-point turn: after reversing, do sharp forward turn
                                _recoveryState = Constants.RECOVERY_STATE_THREE_POINT_TURN;
                                _recoveryStartTick = currentTick;

                                int turnAction = _recoveryTurnDirection > 0 ?
                                    Constants.TEMP_ACTION_TURN_RIGHT :
                                    Constants.TEMP_ACTION_TURN_LEFT;

                                Function.Call(
                                    _taskVehicleTempActionHash,
                                    player.Handle, vehicle.Handle, turnAction, 2500);  // Longer turn for three-point
                            }
                            else
                            {
                                // Standard: done reversing, start turning
                                _recoveryState = Constants.RECOVERY_STATE_TURNING;
                                _recoveryStartTick = currentTick;

                                int action = _recoveryTurnDirection > 0 ?
                                    Constants.TEMP_ACTION_TURN_RIGHT :
                                    Constants.TEMP_ACTION_TURN_LEFT;

                                Function.Call(
                                    _taskVehicleTempActionHash,
                                    player.Handle, vehicle.Handle, action, 1500);
                            }
                        }
                        break;

                    case Constants.RECOVERY_STATE_FORWARD:
                        // Forward maneuver strategy: move forward with turn
                        if (elapsed > 20_000_000)  // 2 seconds forward
                        {
                            // Done with forward maneuver, resume driving
                            _recoveryState = Constants.RECOVERY_STATE_RESUMING;
                            _recoveryStartTick = currentTick;
                            ResumeAfterRecovery(player, vehicle);
                        }
                        break;

                    case Constants.RECOVERY_STATE_THREE_POINT_TURN:
                        // Sharp turn phase of three-point turn
                        if (elapsed > 25_000_000)  // 2.5 seconds turn
                        {
                            // Done with three-point turn, resume driving
                            _recoveryState = Constants.RECOVERY_STATE_RESUMING;
                            _recoveryStartTick = currentTick;
                            ResumeAfterRecovery(player, vehicle);
                        }
                        break;

                    case Constants.RECOVERY_STATE_TURNING:
                        if (elapsed > Constants.RECOVERY_TURN_DURATION)
                        {
                            // Done turning, resume driving
                            _recoveryState = Constants.RECOVERY_STATE_RESUMING;
                            _recoveryStartTick = currentTick;
                            ResumeAfterRecovery(player, vehicle);
                        }
                        break;

                    case Constants.RECOVERY_STATE_RESUMING:
                        // Give time for the task to take effect
                        if (elapsed > 5_000_000)  // 0.5 seconds
                        {
                            // Recovery complete
                            _recoveryState = Constants.RECOVERY_STATE_NONE;
                            _isStuck = false;
                            _stuckCheckCount = 0;
                            _lastRecoveryTick = currentTick;
                            _lastStuckCheckPosition = position;

                            // Reset progress tracking
                            _lastProgressTick = currentTick;
                            if (!_wanderMode)
                            {
                                _lastProgressDistance = (_lastWaypointPos - position).Length();
                            }

                            _audio.Speak("Recovery complete, resuming");
                            Logger.Info("Recovery complete, resuming normal operation");
                        }
                        break;

                    case Constants.RECOVERY_STATE_FAILED:
                        _audio.Speak("Recovery failed. AutoDrive stopping.");
                        Stop(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "UpdateRecovery");
                _recoveryState = Constants.RECOVERY_STATE_FAILED;
            }
        }

        /// <summary>
        /// Resume driving after recovery
        /// </summary>
        private void ResumeAfterRecovery(Ped player, Vehicle vehicle)
        {
            try
            {
                // Clear temp action
                Function.Call(_clearPedTasksHash, player.Handle);

                if (_wanderMode)
                {
                    // Resume wander with optimized cruise-style driving
                    IssueWanderTask(player, vehicle, _targetSpeed);
                }
                else
                {
                    // Resume waypoint navigation using helper (supports LONGRANGE)
                    IssueDriveToCoordTask(player, vehicle, _lastWaypointPos, _targetSpeed,
                        Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS);
                }

                _taskIssued = true;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ResumeAfterRecovery");
                _recoveryState = Constants.RECOVERY_STATE_FAILED;
            }
        }

        /// <summary>
        /// Announce current recovery status
        /// </summary>
        public void AnnounceRecoveryStatus()
        {
            if (_recoveryState == Constants.RECOVERY_STATE_NONE)
            {
                if (_isStuck)
                {
                    _audio.Speak("Vehicle appears stuck, recovery pending");
                }
                else
                {
                    _audio.Speak("Vehicle operating normally");
                }
            }
            else
            {
                string state;
                switch (_recoveryState)
                {
                    case Constants.RECOVERY_STATE_REVERSING:
                        state = "reversing";
                        break;
                    case Constants.RECOVERY_STATE_TURNING:
                        state = "turning";
                        break;
                    case Constants.RECOVERY_STATE_RESUMING:
                        state = "resuming";
                        break;
                    default:
                        state = "unknown";
                        break;
                }
                _audio.Speak($"Recovery in progress, {state}, attempt {_recoveryAttempts} of {Constants.RECOVERY_MAX_ATTEMPTS}");
            }
        }

        #endregion
    }
}
