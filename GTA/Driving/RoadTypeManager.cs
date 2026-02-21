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
        private static readonly Hash _clearPedTasksHash = (Hash)Constants.NATIVE_CLEAR_PED_TASKS;
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
            _deadEndEntryPosition = Vector3.Zero;
        }

        /// <summary>
        /// Classify road type based on node flags and density (no lane count available)
        /// </summary>
        public int ClassifyRoadType(int flags, int density)
        {
            return ClassifyRoadType(flags, density, -1);
        }

        /// <summary>
        /// Classify road type based on node flags, density, and lane count.
        /// Lane count from GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING improves
        /// highway detection (3+ lanes without flags still likely = highway).
        /// </summary>
        /// <param name="flags">eVehicleNodeProperties flags from GET_VEHICLE_NODE_PROPERTIES</param>
        /// <param name="density">Traffic density 0-15 from GET_VEHICLE_NODE_PROPERTIES</param>
        /// <param name="laneCount">Total lanes from GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING, or -1 if unavailable</param>
        public int ClassifyRoadType(int flags, int density, int laneCount)
        {
            // Skip water route nodes
            if ((flags & Constants.ROAD_FLAG_WATER) != 0)
                return Constants.ROAD_TYPE_UNKNOWN;

            // Skip switched-off nodes (parking lots, disabled paths)
            if ((flags & Constants.ROAD_FLAG_SWITCHED_OFF) != 0)
                return Constants.ROAD_TYPE_UNKNOWN;

            // Priority 1: Explicit road type flags (most reliable)
            if ((flags & Constants.ROAD_FLAG_TUNNEL) != 0)
                return Constants.ROAD_TYPE_TUNNEL;

            // Highway detection: flag + optional lane count confirmation
            if ((flags & Constants.ROAD_FLAG_HIGHWAY) != 0)
                return Constants.ROAD_TYPE_HIGHWAY;

            // Lane-count highway detection: no highway flag but 3+ lanes and no traffic lights
            // This catches unmarked highway segments that GTA doesn't flag
            if (laneCount >= Constants.ROAD_LANES_HIGHWAY_MIN &&
                (flags & Constants.ROAD_FLAG_TRAFFIC_LIGHT) == 0 &&
                density <= Constants.ROAD_DENSITY_SUBURBAN_MAX)
            {
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
                return Constants.ROAD_TYPE_RURAL;
            else if (density <= Constants.ROAD_DENSITY_RURAL_MAX)
                return Constants.ROAD_TYPE_RURAL;
            else if (density <= Constants.ROAD_DENSITY_SUBURBAN_MAX)
                return Constants.ROAD_TYPE_SUBURBAN;

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
        /// Update cached speed multiplier for the new road type.
        /// Actual speed application is handled by SpeedArbiter via RoadTypeSpeedMultiplier property.
        /// </summary>
        private bool UpdateRoadTypeMultiplier(int roadType)
        {
            if (roadType == _lastSpeedAdjustedRoadType) return false;
            if (roadType < 0 || roadType >= Constants.ROAD_TYPE_SPEED_MULTIPLIERS.Length) return false;

            float newMultiplier = Constants.GetRoadTypeSpeedMultiplier(roadType);

            if (Math.Abs(newMultiplier - _roadTypeSpeedMultiplier) > 0.1f)
            {
                _roadTypeSpeedMultiplier = newMultiplier;
                _lastSpeedAdjustedRoadType = roadType;

                if (Logger.IsDebugEnabled) Logger.Debug($"Road type multiplier: {Constants.GetRoadTypeName(roadType)} -> {_roadTypeSpeedMultiplier:P0}");

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
        /// <returns>True if road type changed</returns>
        public bool CheckRoadTypeChange(Vector3 position, long currentTick, bool announceEnabled)
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

                // Update speed multiplier for new road type
                UpdateRoadTypeMultiplier(roadType);

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
        /// <param name="onDeadEndDetected">Callback when dead-end is detected</param>
        /// <returns>True if at dead-end</returns>
        public bool CheckDeadEnd(Vehicle vehicle, Vector3 position, long currentTick,
            bool wanderMode, Action<Vehicle, long> onDeadEndDetected)
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
                    // Just entered a dead-end — delegate recovery to AutoDriveManager
                    _inDeadEnd = true;
                    _deadEndEntryPosition = position;
                    _announcementQueue.TryAnnounce("Approaching dead end, attempting recovery",
                        Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceRoadType");
                    Logger.Info("Dead-end detected, requesting cooperative recovery");

                    onDeadEndDetected?.Invoke(vehicle, currentTick);
                    return true;
                }
                else if (!isDeadEnd && _inDeadEnd)
                {
                    // Left the dead-end
                    float distanceFromEntry = (position - _deadEndEntryPosition).Length();
                    if (!float.IsNaN(distanceFromEntry) && distanceFromEntry > Constants.DEAD_END_ESCAPE_DISTANCE)
                    {
                        _inDeadEnd = false;
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
            if (drivingStyleMode == Constants.DRIVING_STYLE_MODE_AGGRESSIVE ||
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
