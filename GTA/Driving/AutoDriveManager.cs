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
        private readonly AnnouncementQueue _announcementQueue;

        // Centralized speed control - all speed-affecting systems set modifiers/caps here
        private readonly SpeedArbiter _speedArbiter;

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

        // Curve slowdown state (synced from RoadFeatureDetector for announcements)
        private bool _curveSlowdownActive;

        // Arrival slowdown state
        private bool _arrivalSlowdownActive;

        // Final approach for precise waypoint arrival
        private Vector3 _safeArrivalPosition;      // Road-safe position near waypoint
        private Vector3 _originalWaypointPos;      // Original waypoint for reference

        // Deferred restart flag (prevents crash when changing waypoint during active drive)
        private bool _pendingRestart;

        // Deferred start flag (prevents crash when switching modes in same frame)
        private bool _pendingWaypointStart;

        // Road type speed adjustment
        private float _roadTypeSpeedMultiplier = 1.0f;

        // Traffic light state tracking
        private bool _stoppedAtLight;
        private long _lastTrafficLightStateTick;
        private Vector3 _trafficLightStopPosition;

        // ===== SPEED MODIFIER STATE (synced from extracted managers) =====

        // Emergency vehicle yield state (synced from EmergencyVehicleHandler)
        private bool _yieldingToEmergency;

        // Speed change announcements
        private float _lastAnnouncedEffectiveSpeed;
        private long _lastSpeedAnnounceTick;
        private bool _wasCurveSlowdownActive;  // Track curve transition for announcements

        // Pause/Resume capability
        private int _pauseState;
        private long _pauseStartTick;
        private float _prePauseSpeed;
        private bool _wasPausedWander;  // Track mode before pause

        // Following distance, structure detection, lane change, and overtaking
        // are now handled by TrafficAwarenessManager and StructureDetector

        // Road type tracking
        private int _currentRoadType;

        // Road seeking state
        private int _seekMode;
        private bool _seekingRoad;
        private Vector3 _seekTargetPosition;
        private long _lastSeekScanTick;
        private long _seekStartTick;  // Track when seeking started for timeout
        private int _seekAttempts;    // Track number of scan attempts
        private bool _onDesiredRoadType;

        // Task spam prevention - track last issued task
        private Vector3 _lastIssuedSeekTarget;  // Last target we issued a drive task for
        private bool _lastIssuedTaskWasWander;  // True if last task was wander, false if drive-to-coord

        // Driving style
        private int _currentDrivingStyleMode = Constants.DRIVING_STYLE_MODE_NORMAL;


        // ===== RECOVERY SYSTEM STATE =====

        // Stuck detection
        private Vector3 _lastStuckCheckPosition;
        private float _lastStuckCheckHeading;
        private long _lastStuckCheckTick;
        private int _stuckCheckCount;              // Consecutive stuck checks
        private bool _isStuck;

        // Cooperative recovery — drives to escape node behind vehicle, then re-issues original task
        private bool _recoveryActive;
        private Vector3 _recoveryEscapeTarget;
        private int _recoveryAttemptCount;
        private long _recoveryStartTick;
        private long _recoveryLastCompleteTick;
        private bool _recoveryWasWanderMode;
        private Vector3 _recoveryOriginalTarget;   // saves _lastWaypointPos before recovery


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
        private static readonly Hash _isWaypointActiveHash = Hash.IS_WAYPOINT_ACTIVE;
        private static readonly Hash _setCruiseSpeedHash = (Hash)Constants.NATIVE_SET_DRIVE_TASK_CRUISE_SPEED;
        private static readonly Hash _clearPedTasksHash = (Hash)Constants.NATIVE_CLEAR_PED_TASKS;
        private static readonly Hash _clearPedTasksImmediatelyHash = (Hash)Constants.NATIVE_CLEAR_PED_TASKS_IMMEDIATELY;
        private static readonly Hash _setHandbrakeHash = (Hash)Constants.NATIVE_SET_VEHICLE_HANDBRAKE;
        private static readonly Hash _getVehicleNodePropsHash = (Hash)Constants.NATIVE_GET_VEHICLE_NODE_PROPERTIES;
        private static readonly Hash _taskVehicleDriveWanderHash = (Hash)Constants.NATIVE_TASK_VEHICLE_DRIVE_WANDER;
        private static readonly Hash _setDriverAbilityHash = (Hash)Constants.NATIVE_SET_DRIVER_ABILITY;
        private static readonly Hash _setDriverAggressivenessHash = (Hash)Constants.NATIVE_SET_DRIVER_AGGRESSIVENESS;
        private static readonly Hash _taskDriveToCoordLongrangeHash = (Hash)Constants.NATIVE_TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE;
        private static readonly Hash _taskDriveToCoordHash = (Hash)Constants.NATIVE_TASK_VEHICLE_DRIVE_TO_COORD;
        private static readonly Hash _getClosestNodeHash = (Hash)Constants.NATIVE_GET_CLOSEST_VEHICLE_NODE;
        private static readonly Hash _getNthClosestNodeHash = (Hash)Constants.NATIVE_GET_NTH_CLOSEST_VEHICLE_NODE;
        private static readonly Hash _getNthClosestNodeWithHeadingHash = (Hash)Constants.NATIVE_GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING;
        private static readonly Hash _isEntityInWaterHash = (Hash)Constants.NATIVE_IS_ENTITY_IN_WATER;

        // Pre-allocated OutputArguments to avoid allocations
        private readonly OutputArgument _nodePos = new OutputArgument();
        private readonly OutputArgument _density = new OutputArgument();
        private readonly OutputArgument _flags = new OutputArgument();

        // Pre-allocated for seeking scans (GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING + GET_VEHICLE_NODE_PROPERTIES)
        private readonly OutputArgument _seekNodePos = new OutputArgument();
        private readonly OutputArgument _seekNodeHeading = new OutputArgument();
        private readonly OutputArgument _seekLaneCount = new OutputArgument();
        private readonly OutputArgument _seekDensity = new OutputArgument();
        private readonly OutputArgument _seekFlags = new OutputArgument();

        public bool IsActive => _autoDriveActive;
        public bool IsSeeking => _seekingRoad;
        public int CurrentRoadType => _currentRoadType;
        public int SeekMode => _seekMode;
        public int CurrentDrivingStyleMode => _currentDrivingStyleMode;
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

            // Centralized speed control
            _speedArbiter = new SpeedArbiter(Constants.AUTODRIVE_DEFAULT_SPEED);

            // Initialize extracted components (order matters - some depend on others)
            _weatherManager = new WeatherManager();
            _announcementQueue = new AnnouncementQueue(audio, settings);  // FIX: Pass settings for announcement toggles
            _collisionDetector = new CollisionDetector();

            // Initialize new extracted managers
            _navigationManager = new NavigationManager(audio, _announcementQueue);
            _roadFeatureDetector = new RoadFeatureDetector(audio, _announcementQueue, _weatherManager);
            _trafficAwarenessManager = new TrafficAwarenessManager(audio, _announcementQueue);
            _environmentalManager = new EnvironmentalManager(_announcementQueue);
            _emergencyVehicleHandler = new EmergencyVehicleHandler(audio, _announcementQueue);
            _structureDetector = new StructureDetector(audio, _announcementQueue);
            _roadTypeManager = new RoadTypeManager(audio, _announcementQueue);
            _etaCalculator = new ETACalculator(audio, _announcementQueue);
        }

        /// <summary>
        /// Reset all stuck detection and vehicle state tracking
        /// </summary>
        private void ResetRecoveryState()
        {
            _lastStuckCheckPosition = Vector3.Zero;
            _lastStuckCheckHeading = 0f;
            _lastStuckCheckTick = 0;
            _stuckCheckCount = 0;
            _isStuck = false;

            _lastProgressDistance = float.MaxValue;
            _lastProgressTick = 0;

            _vehicleFlipped = false;
            _vehicleInWater = false;
            _vehicleOnFire = false;
            _vehicleCriticalDamage = false;
            _lastVehicleStateCheckTick = 0;

            _recoveryActive = false;
            _recoveryEscapeTarget = Vector3.Zero;
            _recoveryAttemptCount = 0;
            _recoveryStartTick = 0;
            _recoveryLastCompleteTick = 0;
            _recoveryWasWanderMode = false;
            _recoveryOriginalTarget = Vector3.Zero;
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
                float speedMultiplier = Constants.GetDrivingStyleSpeedMultiplier(_currentDrivingStyleMode);

                // Apply speed multiplier on initial start (not just on re-issue)
                float adjustedSpeed = _targetSpeed * speedMultiplier;

                if (Logger.IsDebugEnabled) Logger.Debug($"StartWander: Using style={styleValue} ({Constants.GetDrivingStyleName(_currentDrivingStyleMode)}), speedMult={speedMultiplier}, adjustedSpeed={adjustedSpeed}, ability={ability}, aggression={aggressiveness}");

                // Issue drive task ONCE - this is the key to smooth driving!
                // Must use .Handle for native calls - SHVDN wrapper objects don't work directly
                Function.Call(
                    _taskVehicleDriveWanderHash,
                    player.Handle,
                    vehicle.Handle,
                    adjustedSpeed,
                    styleValue);

                // Set cruise speed AFTER issuing task (VAutodrive pattern)
                Function.Call(_setCruiseSpeedHash, player.Handle, adjustedSpeed);

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
            if (!Function.Call<bool>(_isWaypointActiveHash))
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
            if (!Function.Call<bool>(_isWaypointActiveHash))
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

                // Get current driving style settings (using safe bounds-checked accessors)
                int styleValue = Constants.GetDrivingStyleValue(_currentDrivingStyleMode);
                float ability = Constants.GetDrivingStyleAbility(_currentDrivingStyleMode);
                float aggressiveness = Constants.GetDrivingStyleAggressiveness(_currentDrivingStyleMode);
                float speedMultiplier = Constants.GetDrivingStyleSpeedMultiplier(_currentDrivingStyleMode);

                // CRITICAL: Validate all parameters before passing to native function
                // Invalid parameters can cause game engine crashes
                int vehicleModelHash = vehicle.Model.Hash;

                // Validate speed is within reasonable bounds
                if (_targetSpeed <= 0 || _targetSpeed > 200f || float.IsNaN(_targetSpeed) || float.IsInfinity(_targetSpeed))
                {
                    Logger.Error($"Invalid target speed: {_targetSpeed}, resetting to default");
                    _targetSpeed = Constants.AUTODRIVE_DEFAULT_SPEED;
                }

                // Apply speed multiplier on initial start (not just on re-issue)
                float adjustedSpeed = _targetSpeed * speedMultiplier;

                // Calculate distance to waypoint to determine which native to use
                float distanceToWaypoint = Vector3.Distance(player.Position, safePosition);
                bool useLongRange = distanceToWaypoint > Constants.AUTODRIVE_LONGRANGE_THRESHOLD;

                // Always use the user's chosen style - no longrange override
                // VAutodrive research: the style flags should be consistent regardless of distance
                // LONGRANGE native handles pathfinding differently, but the style flags should match
                // the user's preference for traffic behavior, light-stopping, etc.

                // Log parameters before issuing task
                Logger.Info($"StartWaypointInternal: Distance={distanceToWaypoint:F0}m, UseLongRange={useLongRange}");
                Logger.Info($"  Player Handle: {player.Handle}");
                Logger.Info($"  Vehicle Handle: {vehicle.Handle}, Model Hash: {vehicleModelHash}");
                Logger.Info($"  Destination: X={safePosition.X:F2}, Y={safePosition.Y:F2}, Z={safePosition.Z:F2}");
                Logger.Info($"  Speed: {adjustedSpeed:F2} m/s (base={_targetSpeed:F2}, mult={speedMultiplier:F1}), Style: {styleValue}, Stop Range: {Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS}");

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
                            adjustedSpeed,
                            styleValue,
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
                            adjustedSpeed,
                            0,  // p6 - not used
                            vehicleModelHash,
                            styleValue,
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

                // Set cruise speed AFTER issuing task (VAutodrive pattern)
                Function.Call(_setCruiseSpeedHash, player.Handle, adjustedSpeed);

                // Set driver ability based on style
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

                // Set aggressiveness based on style
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

                string bearing = SpatialCalculator.GetDirectionTo(vehicle.Position, _originalWaypointPos);

                if (distanceMiles < 0.1f)
                {
                    int feet = (int)(_lastDistanceToWaypoint * Constants.METERS_TO_FEET);
                    _audio.Speak($"AutoDrive started, navigating to waypoint {feet} feet away to the {bearing} at {mph} miles per hour");
                }
                else
                {
                    _audio.Speak($"AutoDrive started, navigating to waypoint {distanceMiles:F1} miles away to the {bearing} at {mph} miles per hour");
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
                    // Use CLEAR_PED_TASKS_IMMEDIATELY to force-stop the driving task
                    // CLEAR_PED_TASKS only fades out gracefully, so the AI keeps driving
                    Function.Call(_clearPedTasksImmediatelyHash, player.Handle);

                    // Apply handbrake so the vehicle actually stops instead of coasting
                    Vehicle vehicle = player.CurrentVehicle;
                    if (vehicle != null && vehicle.Exists())
                    {
                        Function.Call(_setHandbrakeHash, vehicle.Handle, true);
                    }
                }
                catch { /* Expected during stop - player state may be invalid */ }
            }

            _autoDriveActive = false;
            _taskIssued = false;

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

            // Clear advanced driving states
            ResetAdvancedDrivingState();

            // Stop collision proximity beep
            _audio.StopCollisionBeep();

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
            _speedArbiter.MarkDirty(); // Force immediate speed apply on next Update tick

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
            _speedArbiter.MarkDirty(); // Force immediate speed apply on next Update tick

            int mph = (int)(_targetSpeed * Constants.METERS_PER_SECOND_TO_MPH);
            _audio.Speak($"Speed: {mph} miles per hour");
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

            if (_recoveryActive)
            {
                _audio.Speak($"Recovery in progress, attempt {_recoveryAttemptCount}");
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

            if (_wanderMode)
            {
                _audio.Speak($"AutoDrive active, {mode}, {styleName} style{seekInfo}, at {mph} miles per hour");
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
                    _audio.Speak($"AutoDrive active, {mode}, {styleName} style{seekInfo}, {feet} feet to destination, {mph} miles per hour");
                }
                else
                {
                    _audio.Speak($"AutoDrive active, {mode}, {styleName} style{seekInfo}, {distanceMiles:F1} miles to destination, {mph} miles per hour");
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

            // === VEHICLE STATE & STUCK DETECTION ===

            // Check vehicle state (flipped, water, fire, damage)
            if (CheckVehicleState(vehicle, currentTick))
            {
                // Critical vehicle state - already handled
                return;
            }

            // If cooperative recovery is active, check progress toward escape node
            if (UpdateRecovery(vehicle, position, currentTick))
            {
                _speedArbiter.ApplySpeed(player);
                return;
            }

            // Check if stuck — triggers cooperative recovery
            CheckIfStuck(vehicle, position, currentTick);

            // Check progress timeout (waypoint mode only) — triggers cooperative recovery
            if (!_wanderMode)
            {
                CheckProgressTimeout(vehicle, position, currentTick);
            }

            // === ADVANCED DRIVING FEATURES (using extracted managers) ===

            // Check traffic light state (still inline - consider extracting to RoadFeatureDetector)
            CheckTrafficLightState(vehicle, position, currentTick);

            // Check for U-turns (delegated to StructureDetector)
            _structureDetector.CheckUturn(vehicle, position, currentTick);

            // Check hill/gradient (delegated to StructureDetector)
            _structureDetector.CheckHillGradient(vehicle, position, currentTick);

            // === ENVIRONMENTAL AWARENESS (using extracted managers) ===

            // Check weather conditions (delegated to WeatherManager)
            {
                string weatherName;
                bool shouldAnnounce;
                if (_weatherManager.Update(currentTick, out weatherName, out shouldAnnounce))
                {
                    if (shouldAnnounce && weatherName != null)
                    {
                        float mult = _weatherManager.SpeedMultiplier;
                        int pct = (int)(mult * 100f);
                        _announcementQueue.TryAnnounce($"Weather: {weatherName}, speed adjusted to {pct}%",
                            Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                    }
                    else if (weatherName != null)
                    {
                        _announcementQueue.TryAnnounce($"Weather clearing, {weatherName}",
                            Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                    }
                }

                // Reckless mode: no speed reductions — AI handles everything at full cruise speed.
                // Other styles: weather affects speed (rain, snow, fog reduce traction).
                bool isReckless = _currentDrivingStyleMode == Constants.DRIVING_STYLE_MODE_RECKLESS;
                _speedArbiter.SetWeatherMultiplier(isReckless ? 1.0f : _weatherManager.SpeedMultiplier);
            }

            // Check time of day (delegated to EnvironmentalManager)
            _environmentalManager.CheckTimeOfDay(vehicle, currentTick);
            // Reckless mode: no night-time speed reduction
            {
                bool isReckless = _currentDrivingStyleMode == Constants.DRIVING_STYLE_MODE_RECKLESS;
                _speedArbiter.SetTimeMultiplier(isReckless ? 1.0f : _environmentalManager.TimeSpeedMultiplier);
            }

            // Check for vehicles ahead (collision warning - delegated to CollisionDetector)
            {
                string warningMessage;
                int warningPriority;
                if (_collisionDetector.CheckCollisionWarning(vehicle, position, currentTick,
                    out warningMessage, out warningPriority))
                {
                    if (warningMessage != null)
                    {
                        _announcementQueue.TryAnnounce(warningMessage, warningPriority, currentTick, "announceCollision");
                    }
                }

                // Collision proximity beep removed — too annoying over extended driving.
                // Speech announcements above still provide collision warnings.

                // Feed vehicle-ahead distance to TrafficAwarenessManager for following-distance logic
                _trafficAwarenessManager.SetVehicleAheadDistance(_collisionDetector.VehicleAheadDistance);
            }

            // Check following distance (delegated to TrafficAwarenessManager)
            // Detection and announcements only — AI handles braking/swerving via driving flags:
            //   Cautious/Normal: DF_STOP_FOR_CARS makes the AI brake for traffic natively.
            //   Aggressive/Reckless: SwerveAroundAllVehicles makes the AI dodge traffic.
            _trafficAwarenessManager.CheckFollowingDistance(vehicle, currentTick);

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

            // Sync curve slowdown state from detector (for announcements only)
            bool wasCurve = _wasCurveSlowdownActive;
            _curveSlowdownActive = _roadFeatureDetector.IsCurveSlowdownActive;
            _wasCurveSlowdownActive = _curveSlowdownActive;

            // No curve speed cap — the GTA V AI handles curve deceleration at engine level
            // via vehicleaihandlinginfo.meta AICurvePoints. SET_DRIVER_ABILITY controls
            // how smoothly it handles curves (1.0=best, 0.7=sloppy for Reckless).

            // Announce curve transitions for accessibility (no speed override)
            if (_curveSlowdownActive && !wasCurve)
            {
                _announcementQueue.TryAnnounce("Curve ahead",
                    Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick);
                _lastSpeedAnnounceTick = currentTick;
            }

            // === ROAD TYPE MANAGEMENT (using extracted managers) ===

            // Check road type changes (delegated to RoadTypeManager)
            _roadTypeManager.CheckRoadTypeChange(position, currentTick, true);
            _currentRoadType = _roadTypeManager.CurrentRoadType;

            // Sync road type speed multiplier to arbiter
            // Reckless: no speed reductions — clamp multiplier to >= 1.0 so highways still boost
            // but city/dirt roads don't slow down.
            _roadTypeSpeedMultiplier = _roadTypeManager.RoadTypeSpeedMultiplier;
            {
                bool isReckless = _currentDrivingStyleMode == Constants.DRIVING_STYLE_MODE_RECKLESS;
                float effectiveRoadMult = isReckless ? Math.Max(1.0f, _roadTypeSpeedMultiplier) : _roadTypeSpeedMultiplier;
                _speedArbiter.SetRoadTypeMultiplier(effectiveRoadMult);
            }

            // Check for dead-ends (wander mode only) — triggers cooperative recovery
            if (_wanderMode && !_recoveryActive)
            {
                _roadTypeManager.CheckDeadEnd(vehicle, position, currentTick, _wanderMode, HandleDeadEndRecovery);
                _roadTypeManager.CheckRestrictedArea(vehicle, position, currentTick,
                    _currentDrivingStyleMode, _wanderMode, _targetSpeed);
            }

            // === NORMAL OPERATION ===

            // Waypoint mode: check for arrival and distance updates (delegated to NavigationManager)
            if (!_wanderMode)
            {
                bool shouldStop, shouldRestart;
                bool stillNavigating = _navigationManager.UpdateProgress(position, currentTick,
                    out shouldStop, out shouldRestart);

                // Sync arrival slowdown state and set arrival cap on arbiter
                _arrivalSlowdownActive = _navigationManager.IsArrivalSlowdownActive;
                if (_arrivalSlowdownActive)
                    _speedArbiter.SetArrivalCap(_navigationManager.ArrivalSlowdownSpeed);
                else
                    _speedArbiter.ClearArrivalCap();

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

                // Check if task needs re-issuing due to deviation from path
                if (!_recoveryActive && NeedsTaskReissue(vehicle, _safeArrivalPosition))
                {
                    Ped reissuePlayer = Game.Player.Character;
                    if (reissuePlayer != null && reissuePlayer.IsInVehicle())
                    {
                        IssueDriveToCoordTask(reissuePlayer, vehicle, _safeArrivalPosition, _targetSpeed,
                            Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS);
                        _taskIssued = true;
                        Logger.Info("Task re-issued due to path deviation");
                    }
                }
            }

            // === CENTRALIZED SPEED CONTROL ===
            // All speed-affecting systems have now set their multipliers and caps.
            // Apply the final computed speed via a single SET_DRIVE_TASK_CRUISE_SPEED call.
            _speedArbiter.SetBaseSpeed(_targetSpeed);
            _speedArbiter.SetStyleMultiplier(Constants.GetDrivingStyleSpeedMultiplier(_currentDrivingStyleMode));
            Ped speedPlayer = Game.Player.Character;
            _speedArbiter.ApplySpeed(speedPlayer);

            // Catch-all speed change announcement for combined modifier changes
            // Skip during curve/arrival slowdown (those have dedicated context announcements)
            if (!_curveSlowdownActive && !_arrivalSlowdownActive)
            {
                float effectiveSpeed = _speedArbiter.CurrentEffectiveSpeed;
                if (Math.Abs(effectiveSpeed - _lastAnnouncedEffectiveSpeed) > Constants.SPEED_ANNOUNCE_CHANGE_THRESHOLD &&
                    currentTick - _lastSpeedAnnounceTick > Constants.SPEED_ANNOUNCE_COOLDOWN)
                {
                    int mph = (int)(effectiveSpeed * Constants.METERS_PER_SECOND_TO_MPH);
                    _announcementQueue.TryAnnounce($"{mph} miles per hour",
                        Constants.ANNOUNCE_PRIORITY_LOW, currentTick);
                    _lastAnnouncedEffectiveSpeed = effectiveSpeed;
                    _lastSpeedAnnounceTick = currentTick;
                }
            }
        }

        /// <summary>
        /// Check and announce road features - delegates to RoadFeatureDetector
        /// </summary>
        public void CheckRoadFeatures(Vehicle vehicle, Vector3 position, long currentTick)
        {
            _roadFeatureDetector.Update(vehicle, position, currentTick, _targetSpeed,
                _currentDrivingStyleMode, _autoDriveActive);
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


        // ApplyRoadTypeSpeedAdjustment removed — SpeedArbiter handles road type multiplier
        // via SetRoadTypeMultiplier() in the Update() loop (line ~1138).

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
        /// Reset all advanced driving feature states
        /// </summary>
        private void ResetAdvancedDrivingState()
        {
            _safeArrivalPosition = Vector3.Zero;
            _originalWaypointPos = Vector3.Zero;
            _roadTypeSpeedMultiplier = 1.0f;
            _stoppedAtLight = false;
            _lastTrafficLightStateTick = 0;

            // Reset speed change announcement tracking
            _lastAnnouncedEffectiveSpeed = 0f;
            _lastSpeedAnnounceTick = 0;
            _wasCurveSlowdownActive = false;

            // Reset emergency vehicle yield state
            _yieldingToEmergency = false;

            // Reset pause state
            _pauseState = Constants.PAUSE_STATE_NONE;
            _pauseStartTick = 0;
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
                    if (!Function.Call<bool>(_isWaypointActiveHash))
                    {
                        _audio.Speak("Waypoint removed, switching to wander mode");
                        _wanderMode = true;
                        IssueWanderTask(player, vehicle, _targetSpeed);
                    }
                    else
                    {
                        // Recalculate safe arrival position in case waypoint changed
                        // CRITICAL: Use InitializeWaypoint to sync NavigationManager's internal state
                        // (arrival detection, distance tracking, waypoint-moved detection)
                        Vector3 waypointPos = World.WaypointPosition;
                        _navigationManager.InitializeWaypoint(waypointPos, player.Position);
                        _originalWaypointPos = _navigationManager.OriginalWaypointPos;
                        _safeArrivalPosition = _navigationManager.SafeArrivalPosition;
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

        #region Road Type Detection

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
        /// Check for road type changes and announce if enabled - delegates to RoadTypeManager
        /// </summary>
        public void CheckRoadTypeChange(Vector3 position, long currentTick, bool announceEnabled)
        {
            bool changed = _roadTypeManager.CheckRoadTypeChange(position, currentTick, announceEnabled);
            _currentRoadType = _roadTypeManager.CurrentRoadType;

            if (changed && _seekingRoad)
            {
                UpdateSeekingState(_currentRoadType);
            }
        }

        /// <summary>
        /// Announce current road type (for manual query) - delegates to RoadTypeManager
        /// </summary>
        public void AnnounceCurrentRoadType()
        {
            _roadTypeManager.AnnounceCurrentRoadType();
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
        /// Scan for a specific road type by iterating through the N closest road nodes.
        /// Uses GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING (returns position, heading, lane count)
        /// + GET_VEHICLE_NODE_PROPERTIES (returns flags, density) for classification.
        /// Much more efficient than radial sampling — every call hits an actual road node.
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

            try
            {
                int maxNodes = Constants.ROAD_SEEK_MAX_NODES;

                if (Logger.IsDebugEnabled) Logger.Debug($"ScanForRoadType: Scanning {maxNodes} closest nodes for type {desiredRoadType}");

                for (int n = 1; n <= maxNodes; n++)
                {
                    try
                    {
                        // GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING:
                        // (x, y, z, nthClosest, outPos, outHeading, outTotalLanes, nodeFlags, zMeasureMult, zTolerance)
                        // nodeFlags=0 = active road nodes only (not switched-off parking lots/alleys)
                        bool found = Function.Call<bool>(
                            _getNthClosestNodeWithHeadingHash,
                            origin.X, origin.Y, origin.Z,
                            n,
                            _seekNodePos, _seekNodeHeading, _seekLaneCount,
                            Constants.NODE_FLAG_ACTIVE_NODES_ONLY,
                            3f, 0f);

                        if (!found) break;  // No more nodes available

                        Vector3 nodePos = _seekNodePos.GetResult<Vector3>();
                        int laneCount = _seekLaneCount.GetResult<int>();

                        // Validate node position
                        if (float.IsNaN(nodePos.X) || float.IsNaN(nodePos.Y) || float.IsNaN(nodePos.Z))
                            continue;

                        // Get road properties (flags + density)
                        Function.Call(
                            _getVehicleNodePropsHash,
                            nodePos.X, nodePos.Y, nodePos.Z,
                            _seekDensity, _seekFlags);

                        int nodeFlags = _seekFlags.GetResult<int>();
                        int nodeDensity = _seekDensity.GetResult<int>();

                        // Validate OutputArgument results
                        if (nodeFlags < 0 || nodeFlags > 0xFFFF || nodeDensity < 0 || nodeDensity > 15)
                            continue;

                        // Delegate to RoadTypeManager for single source of truth
                        int roadType = _roadTypeManager.ClassifyRoadType(nodeFlags, nodeDensity, laneCount);

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
                        if (Logger.IsDebugEnabled) Logger.Debug($"ScanForRoadType inner error at node {n}: {innerEx.Message}");
                    }
                }

                if (Logger.IsDebugEnabled) Logger.Debug($"ScanForRoadType: Scanned up to {maxNodes} nodes, found={closestPosition != Vector3.Zero}");
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
                    // Read from NavigationManager for authoritative position
                    Vector3 safePos = _navigationManager.SafeArrivalPosition != Vector3.Zero
                        ? _navigationManager.SafeArrivalPosition
                        : _safeArrivalPosition;
                    float distance = Vector3.Distance(player.Position, safePos);
                    bool useLongRange = distance > Constants.AUTODRIVE_LONGRANGE_THRESHOLD;

                    if (useLongRange)
                    {
                        Function.Call(
                            _taskDriveToCoordLongrangeHash,
                            player.Handle,
                            vehicle.Handle,
                            safePos.X,
                            safePos.Y,
                            safePos.Z,
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
                            safePos.X,
                            safePos.Y,
                            safePos.Z,
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

                // Use provided style or get from current mode - always respect user's choice
                int styleValue = styleOverride >= 0 ? styleOverride : Constants.GetDrivingStyleValue(_currentDrivingStyleMode);
                float ability = Constants.GetDrivingStyleAbility(_currentDrivingStyleMode);
                float aggressiveness = Constants.GetDrivingStyleAggressiveness(_currentDrivingStyleMode);
                float speedMultiplier = Constants.GetDrivingStyleSpeedMultiplier(_currentDrivingStyleMode);

                // Apply speed multiplier to get style-adjusted speed
                float adjustedSpeed = speed * speedMultiplier;

                if (Logger.IsDebugEnabled) Logger.Debug($"IssueDriveToCoordTask: dist={distance:F0}m, longRange={useLongRange}, style={styleValue}, speedMult={speedMultiplier:F1}, adjustedSpeed={adjustedSpeed:F1}");

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
                        adjustedSpeed,
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
                        adjustedSpeed,
                        0,  // p6 - not used
                        vehicle.Model.Hash,
                        styleValue,
                        arrivalRadius,
                        0f);  // p10
                }

                // CRITICAL: Set cruise speed AFTER issuing task (VAutodrive pattern)
                Function.Call(_setCruiseSpeedHash, player.Handle, adjustedSpeed);

                // Set driver ability and aggressiveness
                Function.Call(_setDriverAbilityHash, player.Handle, ability);
                Function.Call(_setDriverAggressivenessHash, player.Handle, aggressiveness);

                if (Logger.IsDebugEnabled) Logger.Debug($"IssueDriveToCoordTask: Cruise speed set to {adjustedSpeed:F1}");
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
                float headingChange = Math.Abs(RoadFeatureDetector.NormalizeAngleDiff(heading - _lastStuckCheckHeading));

                // Check if we're stuck (little movement and little heading change)
                if (movement < Constants.STUCK_MOVEMENT_THRESHOLD &&
                    headingChange < Constants.STUCK_HEADING_CHANGE_THRESHOLD &&
                    speed < Constants.STUCK_SPEED_THRESHOLD)
                {
                    _stuckCheckCount++;

                    if (_stuckCheckCount >= Constants.STUCK_CHECK_COUNT_THRESHOLD && !_isStuck)
                    {
                        _isStuck = true;
                        Logger.Info($"Vehicle stuck detected: movement={movement:F1}m, speed={speed:F1}m/s, checks={_stuckCheckCount}");
                        StartCooperativeRecovery(vehicle, position, currentTick);
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
        private void CheckProgressTimeout(Vehicle vehicle, Vector3 position, long currentTick)
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

                // Reset progress tracking so we don't spam
                _lastProgressTick = currentTick;
                _lastProgressDistance = currentDistance;

                // Trigger cooperative recovery if not already recovering
                if (!_recoveryActive)
                {
                    StartCooperativeRecovery(vehicle, position, currentTick);
                }
            }
        }

        // ===== COOPERATIVE RECOVERY =====

        /// <summary>
        /// Find a valid road node behind the vehicle for cooperative escape.
        /// Escalates search distance with each recovery attempt.
        /// </summary>
        private Vector3 FindEscapeNode(Vehicle vehicle, Vector3 position, int attemptNumber)
        {
            float heading = vehicle.Heading;
            float behindHeading = heading + 180f;
            if (behindHeading >= 360f) behindHeading -= 360f;

            // Escalate distance: attempt 1 = 30m, attempt 2 = 60m, etc.
            float searchDistance = Math.Min(
                Constants.RECOVERY_ESCAPE_BASE_DISTANCE + (attemptNumber - 1) * Constants.RECOVERY_ESCAPE_DISTANCE_INCREMENT,
                Constants.RECOVERY_ESCAPE_MAX_DISTANCE);

            float radians = (90f - behindHeading) * Constants.DEG_TO_RAD;
            float behindX = position.X + (float)Math.Cos(radians) * searchDistance;
            float behindY = position.Y + (float)Math.Sin(radians) * searchDistance;

            try
            {
                // Strategy 1: GET_CLOSEST_VEHICLE_NODE at projected-behind position
                bool found = Function.Call<bool>(
                    _getClosestNodeHash,
                    behindX, behindY, position.Z,
                    _nodePos,
                    Constants.ROAD_NODE_TYPE_ALL,
                    Constants.ROAD_NODE_SEARCH_CONNECTION_DISTANCE,
                    0);

                if (found)
                {
                    Vector3 nodePos = _nodePos.GetResult<Vector3>();
                    if (!float.IsNaN(nodePos.X) && IsNodeBehindVehicle(heading, position, nodePos))
                        return nodePos;
                }

                // Strategy 2: Scan Nth closest nodes to current position, pick first behind
                for (int n = 1; n <= Constants.RECOVERY_NODE_SCAN_COUNT; n++)
                {
                    bool foundNth = Function.Call<bool>(
                        _getNthClosestNodeHash,
                        position.X, position.Y, position.Z,
                        n, _nodePos,
                        Constants.ROAD_NODE_TYPE_ALL,
                        Constants.ROAD_NODE_SEARCH_CONNECTION_DISTANCE,
                        0);

                    if (foundNth)
                    {
                        Vector3 nodePos = _nodePos.GetResult<Vector3>();
                        if (!float.IsNaN(nodePos.X) && IsNodeBehindVehicle(heading, position, nodePos))
                            return nodePos;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "FindEscapeNode");
            }

            // Strategy 3: Return projected position directly — AI will find nearest driveable point
            return new Vector3(behindX, behindY, position.Z);
        }

        /// <summary>
        /// Check if a node is behind the vehicle using dot product (negative = behind).
        /// </summary>
        private static bool IsNodeBehindVehicle(float vehicleHeading, Vector3 vehiclePos, Vector3 nodePos)
        {
            float radians = (90f - vehicleHeading) * Constants.DEG_TO_RAD;
            float forwardX = (float)Math.Cos(radians);
            float forwardY = (float)Math.Sin(radians);

            float toNodeX = nodePos.X - vehiclePos.X;
            float toNodeY = nodePos.Y - vehiclePos.Y;

            return (forwardX * toNodeX + forwardY * toNodeY) < 0;
        }

        /// <summary>
        /// Start cooperative recovery: find escape node behind vehicle and drive to it.
        /// Does NOT fight the AI — clears task and issues a new drive-to-coord.
        /// </summary>
        private void StartCooperativeRecovery(Vehicle vehicle, Vector3 position, long currentTick)
        {
            // Check cooldown
            if (currentTick - _recoveryLastCompleteTick < Constants.RECOVERY_COOLDOWN)
                return;

            // Check max attempts
            _recoveryAttemptCount++;
            if (_recoveryAttemptCount > Constants.RECOVERY_MAX_ATTEMPTS)
            {
                _announcementQueue.AnnounceImmediate(
                    $"Recovery failed after {Constants.RECOVERY_MAX_ATTEMPTS} attempts. Stopping AutoDrive.");
                Logger.Warning($"Recovery exhausted: {Constants.RECOVERY_MAX_ATTEMPTS} attempts, stopping");
                Stop(false);
                return;
            }

            // Find escape node behind the vehicle
            Vector3 escapeNode = FindEscapeNode(vehicle, position, _recoveryAttemptCount);

            // Validate escape node
            if (float.IsNaN(escapeNode.X) || float.IsNaN(escapeNode.Y) || float.IsNaN(escapeNode.Z))
            {
                Logger.Warning("StartCooperativeRecovery: escape node is NaN");
                return;
            }

            float escapeDistance = (escapeNode - position).Length();
            if (float.IsNaN(escapeDistance) || escapeDistance < 5f)
            {
                Logger.Warning($"StartCooperativeRecovery: escape node too close ({escapeDistance:F1}m)");
                return;
            }

            // Save original state before recovery
            if (!_recoveryActive)
            {
                _recoveryWasWanderMode = _wanderMode;
                _recoveryOriginalTarget = _lastWaypointPos;
            }

            // Clear current task and drive to escape node
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            try
            {
                Function.Call(_clearPedTasksHash, player.Handle);

                IssueDriveToCoordTask(player, vehicle, escapeNode, Constants.RECOVERY_ESCAPE_SPEED,
                    Constants.RECOVERY_ESCAPE_ARRIVAL_RADIUS);

                _recoveryActive = true;
                _recoveryEscapeTarget = escapeNode;
                _recoveryStartTick = currentTick;
                _taskIssued = true;

                _announcementQueue.AnnounceImmediate($"Attempting recovery, attempt {_recoveryAttemptCount}");
                Logger.Info($"Cooperative recovery started: attempt={_recoveryAttemptCount}, distance={escapeDistance:F1}m");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StartCooperativeRecovery");
                _recoveryActive = false;
            }
        }

        /// <summary>
        /// Update recovery state: check if we reached escape node or timed out.
        /// Returns true if recovery is in progress (caller should skip normal processing).
        /// </summary>
        private bool UpdateRecovery(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (!_recoveryActive) return false;

            // Check if we reached the escape node
            float distanceToEscape = (position - _recoveryEscapeTarget).Length();
            if (!float.IsNaN(distanceToEscape) && distanceToEscape < Constants.RECOVERY_ESCAPE_ARRIVAL_RADIUS)
            {
                CompleteRecovery(vehicle, position, currentTick);
                return true;
            }

            // Check for timeout
            if (currentTick - _recoveryStartTick > Constants.RECOVERY_ESCAPE_TIMEOUT)
            {
                Logger.Info($"Recovery escape timeout, distance remaining: {distanceToEscape:F1}m");

                if (vehicle.Speed > Constants.STUCK_SPEED_THRESHOLD)
                {
                    // Moving but didn't reach target — count as partial success
                    CompleteRecovery(vehicle, position, currentTick);
                }
                else
                {
                    // Still stuck — escalate to next attempt
                    _recoveryActive = false;
                    _isStuck = true;
                    _stuckCheckCount = Constants.STUCK_CHECK_COUNT_THRESHOLD;
                    Logger.Info("Recovery timeout with no movement, will escalate");
                }
                return true;
            }

            return true;
        }

        /// <summary>
        /// Complete recovery by re-issuing the original driving task.
        /// </summary>
        private void CompleteRecovery(Vehicle vehicle, Vector3 position, long currentTick)
        {
            _recoveryActive = false;
            _recoveryLastCompleteTick = currentTick;

            // Reset stuck detection to fresh baseline
            _isStuck = false;
            _stuckCheckCount = 0;
            _lastStuckCheckPosition = position;
            _lastStuckCheckHeading = vehicle.Heading;
            _lastStuckCheckTick = currentTick;

            // Reset progress tracking
            _lastProgressTick = currentTick;
            if (!_recoveryWasWanderMode)
                _lastProgressDistance = (_lastWaypointPos - position).Length();

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
                return;

            try
            {
                Function.Call(_clearPedTasksHash, player.Handle);

                if (_recoveryWasWanderMode)
                {
                    IssueWanderTask(player, vehicle, _targetSpeed);
                    _wanderMode = true;
                    _lastIssuedTaskWasWander = true;
                }
                else
                {
                    // Check if waypoint still exists
                    if (Function.Call<bool>(_isWaypointActiveHash))
                    {
                        IssueDriveToCoordTask(player, vehicle, _recoveryOriginalTarget, _targetSpeed,
                            Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS);
                        _wanderMode = false;
                        _lastIssuedTaskWasWander = false;
                    }
                    else
                    {
                        // Waypoint removed during recovery
                        IssueWanderTask(player, vehicle, _targetSpeed);
                        _wanderMode = true;
                        _lastIssuedTaskWasWander = true;
                        _announcementQueue.AnnounceImmediate("Waypoint removed during recovery. Wandering.");
                    }
                }

                _taskIssued = true;
                _speedArbiter.MarkDirty();
                _recoveryAttemptCount = 0;

                _announcementQueue.AnnounceImmediate("Recovery successful, resuming");
                Logger.Info("Recovery completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CompleteRecovery");
            }
        }

        /// <summary>
        /// Callback from RoadTypeManager when dead-end is detected.
        /// Triggers cooperative recovery instead of TASK_VEHICLE_TEMP_ACTION.
        /// </summary>
        private void HandleDeadEndRecovery(Vehicle vehicle, long currentTick)
        {
            if (_recoveryActive) return;

            Vector3 position = vehicle.Position;
            Logger.Info("Dead-end recovery initiated via cooperative approach");
            StartCooperativeRecovery(vehicle, position, currentTick);
        }

        #endregion
    }
}
