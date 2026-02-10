using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Recovery states for stuck vehicle handling
    /// </summary>
    public enum RecoveryState
    {
        None = 0,
        Reversing = 1,
        Turning = 2,
        Resuming = 3,
        Failed = 4,
        Forward = 5,
        ThreePointTurn = 6
    }

    /// <summary>
    /// Recovery strategies with escalating complexity
    /// </summary>
    public enum RecoveryStrategy
    {
        ReverseTurn = 1,
        ForwardTurn = 2,
        ThreePoint = 3
    }

    /// <summary>
    /// Manages stuck detection and recovery attempts for AutoDrive.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class RecoveryManager
    {
        // PERFORMANCE: Pre-cached Hash values to avoid repeated casting
        private static readonly Hash _isEntityInWaterHash = (Hash)Constants.NATIVE_IS_ENTITY_IN_WATER;
        private static readonly Hash _clearPedTasksHash = (Hash)Constants.NATIVE_CLEAR_PED_TASKS;
        private static readonly Hash _taskVehicleTempActionHash = (Hash)Constants.NATIVE_TASK_VEHICLE_TEMP_ACTION;

        // Stuck detection
        private Vector3 _lastStuckCheckPosition;
        private float _lastStuckCheckHeading;
        private long _lastStuckCheckTick;
        private int _stuckCheckCount;
        private bool _isStuck;

        // Recovery state
        private RecoveryState _recoveryState = RecoveryState.None;
        private long _recoveryStartTick;
        private int _recoveryAttempts;
        private long _lastRecoveryTick;
        private int _recoveryTurnDirection;  // 1 = right, -1 = left

        // Progress tracking (for waypoint timeout)
        private float _lastProgressDistance;
        private long _lastProgressTick;

        // Vehicle state
        private bool _vehicleFlipped;
        private bool _vehicleInWater;
        private bool _vehicleOnFire;
        private bool _vehicleCriticalDamage;
        private long _lastVehicleStateCheckTick;

        /// <summary>
        /// Whether the vehicle is currently stuck
        /// </summary>
        public bool IsStuck => _isStuck;

        /// <summary>
        /// Whether recovery is in progress
        /// </summary>
        public bool IsRecovering => _recoveryState != RecoveryState.None;

        /// <summary>
        /// Current recovery state
        /// </summary>
        public RecoveryState CurrentState => _recoveryState;

        /// <summary>
        /// Number of recovery attempts made
        /// </summary>
        public int RecoveryAttempts => _recoveryAttempts;

        /// <summary>
        /// Whether vehicle is in a critical state (flipped, water, fire)
        /// </summary>
        public bool IsVehicleCritical => _vehicleFlipped || _vehicleInWater || _vehicleOnFire;

        /// <summary>
        /// Check vehicle state for critical conditions (flipped, water, fire, damage).
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="criticalMessage">Output: message describing critical condition</param>
        /// <returns>True if a critical condition was detected</returns>
        public bool CheckVehicleState(Vehicle vehicle, long currentTick, out string criticalMessage)
        {
            criticalMessage = null;

            // Defensive: Validate vehicle parameter
            if (vehicle == null || !vehicle.Exists())
                return false;

            // Defensive: Guard against invalid tick values
            if (currentTick < 0)
                return false;

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
                        criticalMessage = "Vehicle flipped. AutoDrive stopping.";
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
                        criticalMessage = "Vehicle in water. AutoDrive stopping.";
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
                        criticalMessage = "Vehicle on fire. AutoDrive stopping.";
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
                    criticalMessage = "Vehicle destroyed. AutoDrive stopping.";
                    return true;
                }

                if (health < Constants.VEHICLE_CRITICAL_HEALTH || engineHealth < Constants.VEHICLE_CRITICAL_HEALTH)
                {
                    if (!_vehicleCriticalDamage)
                    {
                        _vehicleCriticalDamage = true;
                        criticalMessage = "Warning: Vehicle critically damaged.";
                        // Don't return true - just a warning, continue driving
                    }
                }
                else
                {
                    _vehicleCriticalDamage = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RecoveryManager.CheckVehicleState");
            }

            return false;
        }

        /// <summary>
        /// Check if the vehicle is stuck (not making progress).
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="position">Current position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <returns>True if vehicle was just detected as stuck</returns>
        public bool CheckIfStuck(Vehicle vehicle, Vector3 position, long currentTick)
        {
            // Defensive: Validate vehicle parameter
            if (vehicle == null || !vehicle.Exists())
                return false;

            // Defensive: Validate position (guard against NaN/Infinity)
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
                float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
                return false;

            // Defensive: Guard against invalid tick values
            if (currentTick < 0)
                return false;

            // Throttle checks
            if (currentTick - _lastStuckCheckTick < Constants.TICK_INTERVAL_STUCK_CHECK)
                return false;

            _lastStuckCheckTick = currentTick;

            // Skip if we recently recovered (cooldown)
            if (currentTick - _lastRecoveryTick < Constants.RECOVERY_COOLDOWN)
                return false;

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
                    return false;
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
                        return true;
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
                Logger.Exception(ex, "RecoveryManager.CheckIfStuck");
            }

            return false;
        }

        /// <summary>
        /// Check if we're making progress toward waypoint (timeout detection).
        /// </summary>
        /// <param name="position">Current position</param>
        /// <param name="waypointPos">Waypoint position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <returns>True if progress timeout was triggered</returns>
        public bool CheckProgressTimeout(Vector3 position, Vector3 waypointPos, long currentTick)
        {
            // Defensive: Validate positions (guard against NaN/Infinity)
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
                float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
                return false;

            if (float.IsNaN(waypointPos.X) || float.IsNaN(waypointPos.Y) || float.IsNaN(waypointPos.Z) ||
                float.IsInfinity(waypointPos.X) || float.IsInfinity(waypointPos.Y) || float.IsInfinity(waypointPos.Z))
                return false;

            // Defensive: Guard against invalid tick values
            if (currentTick < 0)
                return false;

            // Throttle checks
            if (currentTick - _lastProgressTick < Constants.TICK_INTERVAL_PROGRESS_CHECK)
                return false;

            // Initialize on first check
            if (_lastProgressTick == 0)
            {
                _lastProgressTick = currentTick;
                _lastProgressDistance = (waypointPos - position).Length();
                return false;
            }

            float currentDistance = (waypointPos - position).Length();

            // Check if we've made progress
            if (_lastProgressDistance - currentDistance >= Constants.PROGRESS_DISTANCE_THRESHOLD)
            {
                // Made progress, reset timer
                _lastProgressDistance = currentDistance;
                _lastProgressTick = currentTick;
                return false;
            }

            // Check for timeout
            if (currentTick - _lastProgressTick > Constants.PROGRESS_TIMEOUT_TICKS)
            {
                Logger.Info($"Progress timeout: no progress toward waypoint for 30 seconds");
                _isStuck = true;

                // Reset progress tracking
                _lastProgressTick = currentTick;
                _lastProgressDistance = currentDistance;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Start the recovery process with escalating strategies.
        /// </summary>
        /// <param name="player">Player ped</param>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="message">Output: announcement message</param>
        /// <returns>True if recovery started, false if max attempts reached</returns>
        public bool StartRecovery(Ped player, Vehicle vehicle, long currentTick, out string message)
        {
            message = null;

            // Defensive: Validate player and vehicle parameters
            if (player == null || !player.Exists())
            {
                message = "Invalid player. AutoDrive stopping.";
                _recoveryState = RecoveryState.Failed;
                return false;
            }

            if (vehicle == null || !vehicle.Exists())
            {
                message = "Invalid vehicle. AutoDrive stopping.";
                _recoveryState = RecoveryState.Failed;
                return false;
            }

            if (_recoveryAttempts >= Constants.RECOVERY_MAX_ATTEMPTS)
            {
                message = "Unable to recover after multiple attempts. AutoDrive stopping.";
                _recoveryState = RecoveryState.Failed;
                return false;
            }

            _recoveryAttempts++;
            _recoveryStartTick = currentTick;

            // Determine recovery strategy based on attempt number
            RecoveryStrategy strategy = GetRecoveryStrategy(_recoveryAttempts);

            // Alternate turn direction each attempt
            _recoveryTurnDirection = (_recoveryAttempts % 2 == 1) ? 1 : -1;

            string strategyName = GetRecoveryStrategyName(strategy);
            message = $"Attempting recovery, attempt {_recoveryAttempts}, {strategyName}";
            Logger.Info($"Starting recovery attempt {_recoveryAttempts}, strategy: {strategyName}, turn direction: {(_recoveryTurnDirection > 0 ? "right" : "left")}");

            try
            {
                // Clear current task
                Function.Call(_clearPedTasksHash, player.Handle);

                // Execute recovery based on strategy
                switch (strategy)
                {
                    case RecoveryStrategy.ReverseTurn:
                        // Standard reverse + turn
                        _recoveryState = RecoveryState.Reversing;
                        int reverseAction = _recoveryTurnDirection > 0 ?
                            Constants.TEMP_ACTION_REVERSE_RIGHT :
                            Constants.TEMP_ACTION_REVERSE_LEFT;
                        Function.Call(
                            _taskVehicleTempActionHash,
                            player.Handle, vehicle.Handle, reverseAction, (int)(GetRecoveryReverseDuration() / 10000));
                        break;

                    case RecoveryStrategy.ForwardTurn:
                        // Try moving forward with opposite turn (might be stuck against something behind)
                        _recoveryState = RecoveryState.Forward;
                        int forwardAction = _recoveryTurnDirection > 0 ?
                            Constants.TEMP_ACTION_TURN_LEFT :  // Opposite direction
                            Constants.TEMP_ACTION_TURN_RIGHT;
                        Function.Call(
                            _taskVehicleTempActionHash,
                            player.Handle, vehicle.Handle, forwardAction, 2000);
                        break;

                    case RecoveryStrategy.ThreePoint:
                        // Three-point turn: reverse, sharp turn, forward
                        _recoveryState = RecoveryState.Reversing;
                        int threePointAction = _recoveryTurnDirection > 0 ?
                            Constants.TEMP_ACTION_REVERSE_LEFT :  // Start with opposite to create space
                            Constants.TEMP_ACTION_REVERSE_RIGHT;
                        Function.Call(
                            _taskVehicleTempActionHash,
                            player.Handle, vehicle.Handle, threePointAction, 3500);  // Longer reverse for three-point
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RecoveryManager.StartRecovery");
                _recoveryState = RecoveryState.Failed;
                return false;
            }
        }

        /// <summary>
        /// Update the recovery process (called each tick during recovery).
        /// </summary>
        /// <param name="player">Player ped</param>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="position">Current position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="resumeCallback">Callback to resume driving after recovery</param>
        /// <param name="message">Output: announcement message if any</param>
        /// <returns>True if recovery completed or failed</returns>
        public bool UpdateRecovery(Ped player, Vehicle vehicle, Vector3 position, long currentTick,
            Action<Ped, Vehicle> resumeCallback, out string message)
        {
            message = null;

            // Defensive: Validate player and vehicle parameters
            if (player == null || !player.Exists() || vehicle == null || !vehicle.Exists())
            {
                message = "Entity no longer valid. Recovery stopping.";
                _recoveryState = RecoveryState.Failed;
                return true;
            }

            long elapsed = currentTick - _recoveryStartTick;
            RecoveryStrategy strategy = GetRecoveryStrategy(_recoveryAttempts);

            try
            {
                switch (_recoveryState)
                {
                    case RecoveryState.Reversing:
                        // Use escalating reverse duration based on attempt
                        long reverseDuration = GetRecoveryReverseDuration();
                        if (elapsed > reverseDuration)
                        {
                            if (strategy == RecoveryStrategy.ThreePoint)
                            {
                                // Three-point turn: after reversing, do sharp forward turn
                                _recoveryState = RecoveryState.ThreePointTurn;
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
                                _recoveryState = RecoveryState.Turning;
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

                    case RecoveryState.Forward:
                        // Forward maneuver strategy: move forward with turn
                        if (elapsed > 20_000_000)  // 2 seconds forward
                        {
                            // Done with forward maneuver, resume driving
                            _recoveryState = RecoveryState.Resuming;
                            _recoveryStartTick = currentTick;
                            resumeCallback?.Invoke(player, vehicle);
                        }
                        break;

                    case RecoveryState.ThreePointTurn:
                        // Sharp turn phase of three-point turn
                        if (elapsed > 25_000_000)  // 2.5 seconds turn
                        {
                            // Done with three-point turn, resume driving
                            _recoveryState = RecoveryState.Resuming;
                            _recoveryStartTick = currentTick;
                            resumeCallback?.Invoke(player, vehicle);
                        }
                        break;

                    case RecoveryState.Turning:
                        if (elapsed > Constants.RECOVERY_TURN_DURATION)
                        {
                            // Done turning, resume driving
                            _recoveryState = RecoveryState.Resuming;
                            _recoveryStartTick = currentTick;
                            resumeCallback?.Invoke(player, vehicle);
                        }
                        break;

                    case RecoveryState.Resuming:
                        // Give time for the task to take effect
                        if (elapsed > 5_000_000)  // 0.5 seconds
                        {
                            // Recovery complete
                            _recoveryState = RecoveryState.None;
                            _isStuck = false;
                            _stuckCheckCount = 0;
                            _lastRecoveryTick = currentTick;
                            _lastStuckCheckPosition = position;

                            // Reset progress tracking
                            _lastProgressTick = currentTick;

                            message = "Recovery complete, resuming";
                            Logger.Info("Recovery complete, resuming normal operation");
                            return true;
                        }
                        break;

                    case RecoveryState.Failed:
                        message = "Recovery failed. AutoDrive stopping.";
                        return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RecoveryManager.UpdateRecovery");
                _recoveryState = RecoveryState.Failed;
            }

            return false;
        }

        /// <summary>
        /// Get recovery strategy based on attempt number
        /// </summary>
        private RecoveryStrategy GetRecoveryStrategy(int attemptNumber)
        {
            if (attemptNumber <= 2)
                return RecoveryStrategy.ReverseTurn;
            else if (attemptNumber == 3)
                return RecoveryStrategy.ForwardTurn;
            else
                return RecoveryStrategy.ThreePoint;
        }

        /// <summary>
        /// Get human-readable recovery strategy name
        /// </summary>
        private string GetRecoveryStrategyName(RecoveryStrategy strategy)
        {
            switch (strategy)
            {
                case RecoveryStrategy.ReverseTurn:
                    return "reverse and turn";
                case RecoveryStrategy.ForwardTurn:
                    return "forward maneuver";
                case RecoveryStrategy.ThreePoint:
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
        /// Get current recovery status message
        /// </summary>
        public string GetStatusMessage()
        {
            if (_recoveryState == RecoveryState.None)
            {
                if (_isStuck)
                {
                    return "Vehicle appears stuck, recovery pending";
                }
                else
                {
                    return "Vehicle operating normally";
                }
            }
            else
            {
                string state;
                switch (_recoveryState)
                {
                    case RecoveryState.Reversing:
                        state = "reversing";
                        break;
                    case RecoveryState.Turning:
                        state = "turning";
                        break;
                    case RecoveryState.Resuming:
                        state = "resuming";
                        break;
                    case RecoveryState.Forward:
                        state = "forward maneuver";
                        break;
                    case RecoveryState.ThreePointTurn:
                        state = "three-point turn";
                        break;
                    default:
                        state = "unknown";
                        break;
                }
                return $"Recovery in progress, {state}, attempt {_recoveryAttempts} of {Constants.RECOVERY_MAX_ATTEMPTS}";
            }
        }

        /// <summary>
        /// Update progress tracking after recovery
        /// </summary>
        public void UpdateProgressTracking(Vector3 position, Vector3 waypointPos, long currentTick)
        {
            _lastProgressTick = currentTick;
            _lastProgressDistance = (waypointPos - position).Length();
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

        /// <summary>
        /// Reset all recovery state
        /// </summary>
        public void Reset()
        {
            _lastStuckCheckPosition = Vector3.Zero;
            _lastStuckCheckHeading = 0f;
            _lastStuckCheckTick = 0;
            _stuckCheckCount = 0;
            _isStuck = false;

            _recoveryState = RecoveryState.None;
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
    }
}
