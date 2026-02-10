using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages road type detection, dead-ends, and restricted areas.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class RoadTypeManager
    {
        // PERFORMANCE: Pre-cached Hash values to avoid repeated casting
        private static readonly Hash _getVehicleNodePropsHash = (Hash)Constants.NATIVE_GET_VEHICLE_NODE_PROPERTIES;
        private static readonly Hash _setCruiseSpeedHash = (Hash)Constants.NATIVE_SET_DRIVE_TASK_CRUISE_SPEED;
        private static readonly Hash _clearPedTasksHash = (Hash)Constants.NATIVE_CLEAR_PED_TASKS;
        private static readonly Hash _taskVehicleTempActionHash = (Hash)Constants.NATIVE_TASK_VEHICLE_TEMP_ACTION;
        private static readonly Hash _taskVehicleDriveWanderHash = (Hash)Constants.NATIVE_TASK_VEHICLE_DRIVE_WANDER;

        private readonly AudioManager _audio;
        private readonly AnnouncementQueue _announcementQueue;

        // Road type state
        private float _roadTypeSpeedMultiplier = 1.0f;
        private int _lastSpeedAdjustedRoadType = -1;
        private int _currentRoadType;
        private int _lastAnnouncedRoadType;
        private long _lastRoadTypeAnnounceTick;
        private long _lastRoadTypeCheckTick;

        // Dead-end state
        private bool _inDeadEnd;
        private long _lastDeadEndCheckTick;
        private int _deadEndTurnCount;
        private Vector3 _deadEndEntryPosition;

        // Pre-allocated OutputArguments
        private readonly OutputArgument _density = new OutputArgument();
        private readonly OutputArgument _flags = new OutputArgument();

        /// <summary>
        /// Current road type speed multiplier
        /// </summary>
        public float RoadTypeSpeedMultiplier => _roadTypeSpeedMultiplier;

        /// <summary>
        /// Current road type
        /// </summary>
        public int CurrentRoadType => _currentRoadType;

        /// <summary>
        /// Whether currently in a dead-end
        /// </summary>
        public bool IsInDeadEnd => _inDeadEnd;

        public RoadTypeManager(AudioManager audio, AnnouncementQueue announcementQueue)
        {
            _audio = audio;
            _announcementQueue = announcementQueue;
            Reset();
        }

        /// <summary>
        /// Reset all state
        /// </summary>
        public void Reset()
        {
            _roadTypeSpeedMultiplier = 1.0f;
            _lastSpeedAdjustedRoadType = -1;
            _currentRoadType = Constants.ROAD_TYPE_UNKNOWN;
            _lastAnnouncedRoadType = -1;
            _lastRoadTypeAnnounceTick = 0;
            _lastRoadTypeCheckTick = 0;

            _inDeadEnd = false;
            _lastDeadEndCheckTick = 0;
            _deadEndTurnCount = 0;
            _deadEndEntryPosition = Vector3.Zero;
        }

        /// <summary>
        /// Classify road type based on node flags, density, and additional heuristics
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
        /// Get road type at a given position
        /// </summary>
        public int GetRoadTypeAtPosition(Vector3 position)
        {
            try
            {
                // Get road node properties
                Function.Call(
                    _getVehicleNodePropsHash,
                    position.X, position.Y, position.Z,
                    _density, _flags);

                int nodeFlags = _flags.GetResult<int>();
                int nodeDensity = _density.GetResult<int>();

                return ClassifyRoadType(nodeFlags, nodeDensity);
            }
            catch
            {
                return Constants.ROAD_TYPE_UNKNOWN;
            }
        }

        /// <summary>
        /// Apply road type speed adjustment when road type changes
        /// </summary>
        /// <param name="roadType">New road type</param>
        /// <param name="targetSpeed">Current target speed</param>
        /// <param name="curveSlowdownActive">Whether curve slowdown is active</param>
        /// <param name="arrivalSlowdownActive">Whether arrival slowdown is active</param>
        /// <returns>True if speed was adjusted</returns>
        public bool ApplyRoadTypeSpeedAdjustment(int roadType, float targetSpeed,
            bool curveSlowdownActive, bool arrivalSlowdownActive)
        {
            if (roadType == _lastSpeedAdjustedRoadType) return false;
            if (roadType < 0 || roadType >= Constants.ROAD_TYPE_SPEED_MULTIPLIERS.Length) return false;

            float newMultiplier = Constants.GetRoadTypeSpeedMultiplier(roadType);

            // Only apply if multiplier changed significantly
            if (Math.Abs(newMultiplier - _roadTypeSpeedMultiplier) > 0.1f)
            {
                _roadTypeSpeedMultiplier = newMultiplier;
                _lastSpeedAdjustedRoadType = roadType;

                // Apply new speed (unless curve or arrival slowdown active)
                if (!curveSlowdownActive && !arrivalSlowdownActive)
                {
                    try
                    {
                        Ped player = Game.Player.Character;
                        if (player != null && player.IsInVehicle())
                        {
                            float adjustedSpeed = targetSpeed * _roadTypeSpeedMultiplier;
                            Function.Call(
                                _setCruiseSpeedHash,
                                player.Handle,
                                adjustedSpeed);

                            string roadName = Constants.GetRoadTypeName(roadType);
                            if (Logger.IsDebugEnabled) Logger.Debug($"Road type speed: {roadName} -> {adjustedSpeed:F1} m/s ({_roadTypeSpeedMultiplier:P0})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "RoadTypeManager.ApplyRoadTypeSpeedAdjustment");
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Check for road type changes and announce if enabled
        /// </summary>
        /// <param name="position">Current position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="announceEnabled">Whether to announce changes</param>
        /// <param name="targetSpeed">Current target speed</param>
        /// <param name="curveSlowdownActive">Whether curve slowdown is active</param>
        /// <param name="arrivalSlowdownActive">Whether arrival slowdown is active</param>
        /// <returns>True if road type changed</returns>
        public bool CheckRoadTypeChange(Vector3 position, long currentTick, bool announceEnabled,
            float targetSpeed, bool curveSlowdownActive, bool arrivalSlowdownActive)
        {
            // Defensive: Validate tick value
            if (currentTick < 0)
                return false;

            // Throttle checks
            if (currentTick - _lastRoadTypeCheckTick < Constants.TICK_INTERVAL_ROAD_TYPE_CHECK)
                return false;

            _lastRoadTypeCheckTick = currentTick;

            // Defensive: Validate position
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z))
                return false;

            int roadType = GetRoadTypeAtPosition(position);
            if (roadType == Constants.ROAD_TYPE_UNKNOWN)
                return false;

            // Defensive: Validate road type is in valid range before array access
            if (roadType < 0 || roadType >= Constants.ROAD_TYPE_NAMES.Length)
            {
                Logger.Warning($"CheckRoadTypeChange: invalid road type {roadType}");
                return false;
            }

            // Check if road type changed
            if (roadType != _currentRoadType)
            {
                int previousRoadType = _currentRoadType;
                _currentRoadType = roadType;

                // Apply speed adjustment for new road type
                ApplyRoadTypeSpeedAdjustment(roadType, targetSpeed, curveSlowdownActive, arrivalSlowdownActive);

                // Announce change if enabled and cooldown passed
                if (announceEnabled && roadType != _lastAnnouncedRoadType &&
                    currentTick - _lastRoadTypeAnnounceTick > Constants.ROAD_TYPE_ANNOUNCE_COOLDOWN)
                {
                    _lastRoadTypeAnnounceTick = currentTick;
                    _lastAnnouncedRoadType = roadType;

                    string roadName = Constants.GetRoadTypeName(roadType);
                    _announcementQueue.TryAnnounce($"Now on {roadName}",
                        Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceRoadType");
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Announce current road type (for manual query)
        /// </summary>
        public void AnnounceCurrentRoadType()
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists())
                {
                    _audio.Speak("Unable to determine road type");
                    return;
                }

                Vector3 position = player.Position;
                int roadType = GetRoadTypeAtPosition(position);

                if (roadType == Constants.ROAD_TYPE_UNKNOWN)
                {
                    _audio.Speak("Unable to determine road type");
                }
                else if (roadType >= 0 && roadType < Constants.ROAD_TYPE_NAMES.Length)
                {
                    string roadName = Constants.GetRoadTypeName(roadType);
                    _audio.Speak($"Currently on {roadName}");
                }
                else
                {
                    _audio.Speak("Unable to determine road type");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RoadTypeManager.AnnounceCurrentRoadType");
                _audio.Speak("Unable to determine road type");
            }
        }

        /// <summary>
        /// Check if current position is at a dead-end road and handle escape
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="position">Current position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="wanderMode">Whether in wander mode</param>
        /// <param name="onTurnaround">Callback when turnaround is needed</param>
        /// <returns>True if at dead-end</returns>
        public bool CheckDeadEnd(Vehicle vehicle, Vector3 position, long currentTick,
            bool wanderMode, Action<Vehicle, long, int> onTurnaround)
        {
            if (!wanderMode) return false;

            // Defensive: Validate vehicle
            if (vehicle == null || !vehicle.Exists())
                return false;

            // Defensive: Validate position
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z))
                return false;

            // Defensive: Validate tick value
            if (currentTick < 0)
                return _inDeadEnd;

            // Throttle checks
            if (currentTick - _lastDeadEndCheckTick < Constants.TICK_INTERVAL_DEAD_END_CHECK)
                return _inDeadEnd;

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
                    _announcementQueue.TryAnnounce("Approaching dead end, turning around",
                        Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceRoadType");
                    Logger.Info("Dead-end detected, initiating turnaround");

                    // Request turnaround
                    StartDeadEndTurnaround(vehicle, currentTick, onTurnaround);
                    return true;
                }
                else if (!isDeadEnd && _inDeadEnd)
                {
                    // Left the dead-end
                    float distanceFromEntry = (position - _deadEndEntryPosition).Length();
                    if (!float.IsNaN(distanceFromEntry) && distanceFromEntry > Constants.DEAD_END_ESCAPE_DISTANCE)
                    {
                        _inDeadEnd = false;
                        _deadEndTurnCount = 0;
                        Logger.Info("Successfully escaped dead-end");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RoadTypeManager.CheckDeadEnd");
            }

            return _inDeadEnd;
        }

        /// <summary>
        /// Initiate turnaround maneuver when at a dead-end
        /// </summary>
        private void StartDeadEndTurnaround(Vehicle vehicle, long currentTick,
            Action<Vehicle, long, int> onTurnaround)
        {
            // Defensive: Validate vehicle
            if (vehicle == null || !vehicle.Exists())
            {
                Logger.Warning("StartDeadEndTurnaround: vehicle is null or doesn't exist");
                return;
            }

            try
            {
                Ped player = Game.Player.Character;

                // Defensive: Validate player
                if (player == null || !player.Exists())
                {
                    Logger.Warning("StartDeadEndTurnaround: player is null or doesn't exist");
                    return;
                }

                // Clear current task
                Function.Call(_clearPedTasksHash, player.Handle);

                // Perform a reverse and turn maneuver
                _deadEndTurnCount++;

                // Defensive: Prevent runaway turn count
                if (_deadEndTurnCount > 100)
                {
                    Logger.Warning("StartDeadEndTurnaround: excessive turn count, resetting");
                    _deadEndTurnCount = 1;
                }

                // Alternate turn direction on repeated attempts
                int action = (_deadEndTurnCount % 2 == 1) ?
                    Constants.TEMP_ACTION_REVERSE_LEFT :
                    Constants.TEMP_ACTION_REVERSE_RIGHT;

                Function.Call(
                    _taskVehicleTempActionHash,
                    player.Handle, vehicle.Handle, action, 2500);  // 2.5 second reverse

                // Notify parent about turnaround for recovery state
                onTurnaround?.Invoke(vehicle, currentTick, _deadEndTurnCount);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RoadTypeManager.StartDeadEndTurnaround");
            }
        }

        /// <summary>
        /// Check if approaching a restricted area and reroute if needed
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="position">Current position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="drivingStyleMode">Current driving style mode</param>
        /// <param name="wanderMode">Whether in wander mode</param>
        /// <param name="targetSpeed">Current target speed</param>
        /// <returns>True if restricted area detected</returns>
        public bool CheckRestrictedArea(Vehicle vehicle, Vector3 position, long currentTick,
            int drivingStyleMode, bool wanderMode, float targetSpeed)
        {
            // Only check in cautious/normal modes where AvoidRestrictedAreas is active
            if (drivingStyleMode == Constants.DRIVING_STYLE_MODE_FAST ||
                drivingStyleMode == Constants.DRIVING_STYLE_MODE_RECKLESS)
                return false;

            try
            {
                // Look ahead in driving direction
                float heading = vehicle.Heading;
                float speed = vehicle.Speed;
                float lookahead = Math.Max(Constants.COLLISION_LOOKAHEAD_MIN,
                    speed * Constants.COLLISION_LOOKAHEAD_TIME_FACTOR);

                // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                float radians = (90f - heading) * Constants.DEG_TO_RAD;
                Vector3 aheadPos = position + new Vector3(
                    (float)Math.Cos(radians) * lookahead,
                    (float)Math.Sin(radians) * lookahead,
                    0f);

                // Check if the area ahead is restricted
                bool isRestricted = IsPositionRestricted(aheadPos);

                if (isRestricted && wanderMode)
                {
                    // Force a reroute by restarting wander
                    _announcementQueue.TryAnnounce("Restricted area ahead, rerouting",
                        Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceRoadType");
                    Logger.Info("Restricted area detected ahead, rerouting");

                    // Clear task and re-issue to force new route calculation
                    Ped player = Game.Player.Character;
                    Function.Call(_clearPedTasksHash, player.Handle);

                    int styleValue = Constants.GetDrivingStyleValue(drivingStyleMode);
                    Function.Call(
                        _taskVehicleDriveWanderHash,
                        player.Handle, vehicle.Handle, targetSpeed, styleValue);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RoadTypeManager.CheckRestrictedArea");
            }

            return false;
        }

        /// <summary>
        /// Check if a position is in a restricted area (military base, airport, etc.)
        /// </summary>
        public bool IsPositionRestricted(Vector3 position)
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
    }
}
