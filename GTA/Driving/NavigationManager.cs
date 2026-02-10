using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages waypoint navigation, arrival detection, and final approach logic.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class NavigationManager
    {
        // PERFORMANCE: Pre-cached Hash values to avoid repeated casting
        private static readonly Hash _setCruiseSpeedHash = (Hash)Constants.NATIVE_SET_DRIVE_TASK_CRUISE_SPEED;
        private static readonly Hash _getClosestNodeWithHeadingHash = (Hash)Constants.NATIVE_GET_CLOSEST_VEHICLE_NODE_WITH_HEADING;
        private static readonly Hash _getClosestNodeHash = (Hash)Constants.NATIVE_GET_CLOSEST_VEHICLE_NODE;
        private static readonly Hash _getNthClosestNodeHash = (Hash)Constants.NATIVE_GET_NTH_CLOSEST_VEHICLE_NODE;
        private static readonly Hash _getSafeCoordForPedHash = (Hash)Constants.NATIVE_GET_SAFE_COORD_FOR_PED;
        private static readonly Hash _getPointOnRoadSideHash = (Hash)Constants.NATIVE_GET_POINT_ON_ROAD_SIDE;

        private readonly AudioManager _audio;
        private readonly AnnouncementQueue _announcementQueue;

        // Waypoint tracking
        private Vector3 _lastWaypointPos;
        private float _lastDistanceToWaypoint;

        // Final approach state
        private bool _inFinalApproach;
        private Vector3 _safeArrivalPosition;
        private Vector3 _originalWaypointPos;

        // Arrival slowdown state
        private bool _arrivalSlowdownActive;
        private float _lastArrivalSpeed;
        private int _lastAnnouncedArrivalDistance;

        // Pre-allocated OutputArguments to avoid allocations
        private readonly OutputArgument _safeArrivalPosArg = new OutputArgument();
        private readonly OutputArgument _safeArrivalHeadingArg = new OutputArgument();
        private readonly OutputArgument _safePedCoordArg = new OutputArgument();
        private readonly OutputArgument _roadSidePosArg = new OutputArgument();

        /// <summary>
        /// Whether final approach is active
        /// </summary>
        public bool IsInFinalApproach => _inFinalApproach;

        /// <summary>
        /// Whether arrival slowdown is active
        /// </summary>
        public bool IsArrivalSlowdownActive => _arrivalSlowdownActive;

        /// <summary>
        /// Safe arrival position on road
        /// </summary>
        public Vector3 SafeArrivalPosition => _safeArrivalPosition;

        /// <summary>
        /// Original waypoint position
        /// </summary>
        public Vector3 OriginalWaypointPos => _originalWaypointPos;

        /// <summary>
        /// Last tracked waypoint position
        /// </summary>
        public Vector3 LastWaypointPos => _lastWaypointPos;

        /// <summary>
        /// Last distance to waypoint
        /// </summary>
        public float LastDistanceToWaypoint => _lastDistanceToWaypoint;

        public NavigationManager(AudioManager audio, AnnouncementQueue announcementQueue)
        {
            _audio = audio;
            _announcementQueue = announcementQueue;
            Reset();
        }

        /// <summary>
        /// Reset all navigation state
        /// </summary>
        public void Reset()
        {
            _lastWaypointPos = Vector3.Zero;
            _lastDistanceToWaypoint = float.MaxValue;
            _inFinalApproach = false;
            _safeArrivalPosition = Vector3.Zero;
            _originalWaypointPos = Vector3.Zero;
            _arrivalSlowdownActive = false;
            _lastArrivalSpeed = 0f;
            _lastAnnouncedArrivalDistance = int.MaxValue;
        }

        /// <summary>
        /// Initialize navigation to a waypoint
        /// </summary>
        /// <param name="waypointPos">The original waypoint position</param>
        /// <param name="playerPos">Current player position</param>
        public void InitializeWaypoint(Vector3 waypointPos, Vector3 playerPos)
        {
            Logger.Info($"NavigationManager.InitializeWaypoint: waypoint={waypointPos}, player={playerPos}");
            _originalWaypointPos = waypointPos;

            Logger.Info("NavigationManager.InitializeWaypoint: Calling GetSafeArrivalPosition");
            _safeArrivalPosition = GetSafeArrivalPosition(waypointPos);
            Logger.Info($"NavigationManager.InitializeWaypoint: safePosition={_safeArrivalPosition}");

            _inFinalApproach = false;
            _lastWaypointPos = _safeArrivalPosition;
            _lastDistanceToWaypoint = (_safeArrivalPosition - playerPos).Length();
            _lastAnnouncedArrivalDistance = (int)(_lastDistanceToWaypoint * Constants.METERS_TO_FEET);
            Logger.Info($"NavigationManager.InitializeWaypoint: Complete, distance={_lastDistanceToWaypoint:F1}m");
        }

        /// <summary>
        /// Update waypoint progress and check for arrival.
        /// Returns true if arrived at destination.
        /// </summary>
        /// <param name="position">Current vehicle position</param>
        /// <param name="targetSpeed">Current target speed for restoration</param>
        /// <param name="curveSlowdownActive">Whether curve slowdown is active</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="shouldStop">Output: true if AutoDrive should stop</param>
        /// <param name="shouldRestart">Output: true if navigation should restart</param>
        /// <returns>True if still navigating, false if arrived or stopped</returns>
        public bool UpdateProgress(Vector3 position, float targetSpeed, bool curveSlowdownActive,
            long currentTick, out bool shouldStop, out bool shouldRestart)
        {
            shouldStop = false;
            shouldRestart = false;

            // Check if waypoint was removed
            if (!Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
            {
                _announcementQueue.TryAnnounce("Waypoint removed. AutoDrive stopping.",
                    Constants.ANNOUNCE_PRIORITY_CRITICAL, currentTick, "announceNavigation");
                EndArrivalSlowdown(targetSpeed, curveSlowdownActive, false);
                shouldStop = true;
                return false;
            }

            // Check if waypoint moved (compare with original position)
            Vector3 currentWaypoint = World.WaypointPosition;
            if ((currentWaypoint - _originalWaypointPos).Length() > Constants.WAYPOINT_MOVED_THRESHOLD)
            {
                _announcementQueue.TryAnnounce("Waypoint moved. Recalculating route.",
                    Constants.ANNOUNCE_PRIORITY_HIGH, currentTick, "announceNavigation");
                _inFinalApproach = false;
                EndArrivalSlowdown(targetSpeed, curveSlowdownActive, false);
                shouldRestart = true;
                return false;
            }

            // Check distance to both safe position and original waypoint
            float distanceToSafe = (_safeArrivalPosition - position).Length();
            float distanceToOriginal = (_originalWaypointPos - position).Length();
            float distance = Math.Min(distanceToSafe, distanceToOriginal);

            // === FINAL APPROACH PHASE ===
            if (!_inFinalApproach && distance < Constants.AUTODRIVE_FINAL_APPROACH_DISTANCE)
            {
                _inFinalApproach = true;
                if (Logger.IsDebugEnabled) Logger.Debug($"Entering final approach at {distance:F0}m");

                Ped player = Game.Player.Character;
                Function.Call(
                    _setCruiseSpeedHash,
                    player.Handle,
                    Constants.AUTODRIVE_FINAL_APPROACH_SPEED);
            }

            // Check for arrival
            float arrivalRadius = _inFinalApproach ?
                Constants.AUTODRIVE_PRECISE_ARRIVAL_RADIUS :
                Constants.AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS;

            if (distance < arrivalRadius)
            {
                _announcementQueue.TryAnnounce("You have arrived at your destination.",
                    Constants.ANNOUNCE_PRIORITY_CRITICAL, currentTick, "announceNavigation");
                _inFinalApproach = false;
                EndArrivalSlowdown(targetSpeed, curveSlowdownActive, false);
                shouldStop = true;
                return false;
            }

            // === SMOOTH ARRIVAL DECELERATION ===
            if (distance <= Constants.ARRIVAL_SLOWDOWN_DISTANCE)
            {
                float targetArrivalSpeed = Math.Max(
                    Constants.ARRIVAL_FINAL_SPEED,
                    distance * Constants.ARRIVAL_SPEED_FACTOR);

                if (!_arrivalSlowdownActive || Math.Abs(targetArrivalSpeed - _lastArrivalSpeed) > 1f)
                {
                    if (!_arrivalSlowdownActive)
                    {
                        _arrivalSlowdownActive = true;
                        _announcementQueue.TryAnnounce("Approaching destination",
                            Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceNavigation");
                        if (Logger.IsDebugEnabled) Logger.Debug($"Arrival slowdown started at {distance:F0}m");
                    }

                    _lastArrivalSpeed = targetArrivalSpeed;
                    ApplyArrivalSpeed(targetArrivalSpeed);
                }
            }
            else if (_arrivalSlowdownActive)
            {
                EndArrivalSlowdown(targetSpeed, curveSlowdownActive, true);
            }

            // Announce distance milestones
            float distanceMiles = distance * Constants.METERS_TO_MILES;
            float lastMiles = _lastDistanceToWaypoint * Constants.METERS_TO_MILES;

            if (distanceMiles < 0.5f)
            {
                CheckGranularArrivalAnnouncements(distance, currentTick);
            }
            else if ((int)(lastMiles * 2) > (int)(distanceMiles * 2))
            {
                _announcementQueue.TryAnnounce($"{distanceMiles:F1} miles to destination",
                    Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceNavigation");
            }

            _lastDistanceToWaypoint = distance;
            return true;
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
                Logger.Exception(ex, "NavigationManager.ApplyArrivalSpeed");
            }
        }

        /// <summary>
        /// End arrival slowdown and restore normal speed
        /// </summary>
        public void EndArrivalSlowdown(float targetSpeed, bool curveSlowdownActive, bool stillDriving)
        {
            if (!_arrivalSlowdownActive) return;

            _arrivalSlowdownActive = false;
            _lastArrivalSpeed = 0f;

            if (stillDriving && !curveSlowdownActive)
            {
                try
                {
                    Ped player = Game.Player.Character;
                    Function.Call(
                        _setCruiseSpeedHash,
                        player.Handle,
                        targetSpeed);

                    if (Logger.IsDebugEnabled) Logger.Debug($"Arrival slowdown ended, restored to {targetSpeed:F1} m/s");
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "NavigationManager.EndArrivalSlowdown");
                }
            }
        }

        /// <summary>
        /// Check for granular arrival distance announcements
        /// </summary>
        private void CheckGranularArrivalAnnouncements(float distanceMeters, long currentTick)
        {
            int distanceFeet = (int)(distanceMeters * Constants.METERS_TO_FEET);

            foreach (int milestone in Constants.ARRIVAL_ANNOUNCEMENT_DISTANCES)
            {
                if (distanceFeet <= milestone && _lastAnnouncedArrivalDistance > milestone)
                {
                    _lastAnnouncedArrivalDistance = milestone;
                    _announcementQueue.TryAnnounce($"{milestone} feet to destination",
                        Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceNavigation");
                    return;
                }
            }

            if (distanceFeet > _lastAnnouncedArrivalDistance)
            {
                _lastAnnouncedArrivalDistance = distanceFeet;
            }
        }

        /// <summary>
        /// Find a safe road position near the waypoint for driving
        /// </summary>
        public Vector3 GetSafeArrivalPosition(Vector3 waypointPos)
        {
            try
            {
                // Strategy 1: GET_CLOSEST_VEHICLE_NODE_WITH_HEADING
                bool foundNode = Function.Call<bool>(
                    _getClosestNodeWithHeadingHash,
                    waypointPos.X, waypointPos.Y, waypointPos.Z,
                    _safeArrivalPosArg, _safeArrivalHeadingArg,
                    Constants.ROAD_NODE_TYPE_ALL,
                    Constants.ROAD_NODE_SEARCH_CONNECTION_DISTANCE,
                    0);

                if (foundNode)
                {
                    Vector3 roadPos = _safeArrivalPosArg.GetResult<Vector3>();
                    float distanceToRoad = (roadPos - waypointPos).Length();
                    if (distanceToRoad < Constants.SAFE_ARRIVAL_MAX_DISTANCE)
                    {
                        if (Logger.IsDebugEnabled) Logger.Debug($"Found road node {distanceToRoad:F1}m from waypoint (with heading)");
                        return roadPos;
                    }
                }

                // Strategy 2: GET_CLOSEST_VEHICLE_NODE
                bool foundSimpleNode = Function.Call<bool>(
                    _getClosestNodeHash,
                    waypointPos.X, waypointPos.Y, waypointPos.Z,
                    _safeArrivalPosArg,
                    Constants.ROAD_NODE_TYPE_ALL,
                    Constants.ROAD_NODE_SEARCH_CONNECTION_DISTANCE,
                    0);

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

                // Strategy 3: GET_NTH_CLOSEST_VEHICLE_NODE
                for (int n = 2; n <= 3; n++)
                {
                    bool foundNth = Function.Call<bool>(
                        _getNthClosestNodeHash,
                        waypointPos.X, waypointPos.Y, waypointPos.Z,
                        n,
                        _safeArrivalPosArg,
                        Constants.ROAD_NODE_TYPE_ALL,
                        Constants.ROAD_NODE_SEARCH_CONNECTION_DISTANCE,
                        0);

                    if (foundNth)
                    {
                        Vector3 roadPos = _safeArrivalPosArg.GetResult<Vector3>();
                        float distanceToRoad = (roadPos - waypointPos).Length();
                        if (distanceToRoad < Constants.SAFE_ARRIVAL_NTH_NODE_MAX_DISTANCE)
                        {
                            if (Logger.IsDebugEnabled) Logger.Debug($"Found {n}th closest road node {distanceToRoad:F1}m from waypoint");
                            return roadPos;
                        }
                    }
                }

                // Strategy 4: GET_SAFE_COORD_FOR_PED
                bool foundSafe = Function.Call<bool>(
                    _getSafeCoordForPedHash,
                    waypointPos.X, waypointPos.Y, waypointPos.Z,
                    true,
                    _safePedCoordArg,
                    Constants.SAFE_COORD_FLAGS);

                if (foundSafe)
                {
                    Vector3 safe = _safePedCoordArg.GetResult<Vector3>();
                    if (safe != Vector3.Zero && (safe - waypointPos).Length() < Constants.SAFE_ARRIVAL_SAFE_COORD_MAX_DISTANCE)
                    {
                        Logger.Debug($"Found safe coord for waypoint arrival");
                        return safe;
                    }
                }

                // Strategy 5: GET_POINT_ON_ROAD_SIDE
                bool foundRoadSide = Function.Call<bool>(
                    _getPointOnRoadSideHash,
                    waypointPos.X, waypointPos.Y, waypointPos.Z,
                    0,
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
                Logger.Exception(ex, "NavigationManager.GetSafeArrivalPosition");
            }

            Logger.Debug("Using original waypoint position (no safe position found)");
            return waypointPos;
        }

        /// <summary>
        /// Format distance for announcements
        /// </summary>
        public static string FormatDistance(float distanceMeters)
        {
            float distanceMiles = distanceMeters * Constants.METERS_TO_MILES;

            if (distanceMiles < 0.1f)
            {
                int feet = (int)(distanceMeters * Constants.METERS_TO_FEET);
                return $"{feet} feet";
            }
            else
            {
                return $"{distanceMiles:F1} miles";
            }
        }
    }
}
