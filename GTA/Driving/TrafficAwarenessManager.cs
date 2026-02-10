using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages traffic awareness including lane changes, overtaking, and following distance.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class TrafficAwarenessManager
    {
        private readonly AudioManager _audio;
        private readonly AnnouncementQueue _announcementQueue;

        // Lane tracking state
        private Vector3 _laneTrackingPosition;
        private float _laneTrackingHeading;
        private long _lastLaneCheckTick;
        private long _lastLaneChangeAnnounceTick;
        private bool _laneChangeInProgress;

        // Overtaking tracking
        private Dictionary<int, OvertakeTrackingInfo> _overtakeTracking;
        private long _lastOvertakeCheckTick;
        private long _lastOvertakeAnnounceTick;

        // Pre-allocated collections
        private readonly HashSet<int> _visibleHandles = new HashSet<int>();
        private readonly List<int> _handleRemovalList = new List<int>();

        // PERFORMANCE: Pre-allocated vectors to avoid per-frame allocations
        private Vector3 _cachedForward;
        private Vector3 _cachedRight;

        // Following distance state
        private float _followingTimeGap = float.MaxValue;
        private int _lastFollowingState;
        private long _lastFollowingCheckTick;
        private long _lastFollowingAnnounceTick;

        // External state references (passed in via Update)
        private float _lastVehicleAheadDistance = float.MaxValue;

        /// <summary>
        /// Current following time gap in seconds
        /// </summary>
        public float FollowingTimeGap => _followingTimeGap;

        /// <summary>
        /// Current following state (0=clear, 1=safe, 2=normal, 3=close, 4=dangerous)
        /// </summary>
        public int FollowingState => _lastFollowingState;

        /// <summary>
        /// Whether a lane change is currently in progress
        /// </summary>
        public bool IsLaneChangeInProgress => _laneChangeInProgress;

        public TrafficAwarenessManager(AudioManager audio, AnnouncementQueue announcementQueue)
        {
            _audio = audio;
            _announcementQueue = announcementQueue;
            _overtakeTracking = new Dictionary<int, OvertakeTrackingInfo>(Constants.OVERTAKE_TRACKING_MAX);
            Reset();
        }

        /// <summary>
        /// Reset all state
        /// </summary>
        public void Reset()
        {
            _laneTrackingPosition = Vector3.Zero;
            _laneTrackingHeading = 0f;
            _lastLaneCheckTick = 0;
            _lastLaneChangeAnnounceTick = 0;
            _laneChangeInProgress = false;

            _overtakeTracking.Clear();
            _lastOvertakeCheckTick = 0;
            _lastOvertakeAnnounceTick = 0;

            _followingTimeGap = float.MaxValue;
            _lastFollowingState = 0;
            _lastFollowingCheckTick = 0;
            _lastFollowingAnnounceTick = 0;
            _lastVehicleAheadDistance = float.MaxValue;
        }

        /// <summary>
        /// Initialize lane tracking
        /// </summary>
        public void InitializeLaneTracking(Vehicle vehicle)
        {
            _laneTrackingPosition = vehicle.Position;
            _laneTrackingHeading = vehicle.Heading;
            _laneChangeInProgress = false;
        }

        /// <summary>
        /// Set the current distance to vehicle ahead (from CollisionDetector)
        /// </summary>
        public void SetVehicleAheadDistance(float distance)
        {
            _lastVehicleAheadDistance = distance;
        }

        /// <summary>
        /// Check for lane changes based on lateral movement
        /// </summary>
        public void CheckLaneChange(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (currentTick - _lastLaneCheckTick < Constants.TICK_INTERVAL_LANE_CHECK)
                return;

            _lastLaneCheckTick = currentTick;

            try
            {
                float speed = vehicle.Speed;

                if (speed < Constants.LANE_CHANGE_MIN_SPEED)
                {
                    _laneTrackingPosition = position;
                    _laneTrackingHeading = vehicle.Heading;
                    return;
                }

                float currentHeading = vehicle.Heading;
                float headingDiff = Math.Abs(RoadFeatureDetector.NormalizeAngleDiff(currentHeading - _laneTrackingHeading));

                if (headingDiff > Constants.LANE_CHANGE_HEADING_TOLERANCE)
                {
                    _laneTrackingPosition = position;
                    _laneTrackingHeading = currentHeading;
                    _laneChangeInProgress = false;
                    return;
                }

                Vector3 movement = position - _laneTrackingPosition;

                // OPTIMIZED: Use pre-calculated DEG_TO_RAD constant and reuse vector
                float headingRad = (90f - _laneTrackingHeading) * Constants.DEG_TO_RAD;
                _cachedRight.X = (float)Math.Sin(headingRad);
                _cachedRight.Y = -(float)Math.Cos(headingRad);
                _cachedRight.Z = 0f;

                float lateralDistance = Vector3.Dot(movement, _cachedRight);

                if (Math.Abs(lateralDistance) >= Constants.LANE_CHANGE_THRESHOLD)
                {
                    if (!_laneChangeInProgress &&
                        currentTick - _lastLaneChangeAnnounceTick > Constants.LANE_CHANGE_ANNOUNCE_COOLDOWN)
                    {
                        _laneChangeInProgress = true;
                        _lastLaneChangeAnnounceTick = currentTick;

                        string direction = lateralDistance > 0 ? "right" : "left";
                        _announcementQueue.TryAnnounce($"Changing lanes {direction}",
                            Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceTrafficAwareness");
                    }

                    _laneTrackingPosition = position;
                    _laneTrackingHeading = currentHeading;
                }
                else if (_laneChangeInProgress && Math.Abs(lateralDistance) < Constants.LANE_WIDTH * 0.3f)
                {
                    _laneChangeInProgress = false;
                }

                if (!_laneChangeInProgress)
                {
                    _laneTrackingPosition = Vector3.Lerp(_laneTrackingPosition, position, 0.1f);
                    _laneTrackingHeading = currentHeading;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TrafficAwarenessManager.CheckLaneChange");
            }
        }

        /// <summary>
        /// Check for overtaking maneuvers
        /// </summary>
        public void CheckOvertaking(Vehicle vehicle, Vector3 position, long currentTick)
        {
            // Defensive: Validate tick value
            if (currentTick < 0)
                return;

            if (currentTick - _lastOvertakeCheckTick < Constants.TICK_INTERVAL_OVERTAKE_CHECK)
                return;

            _lastOvertakeCheckTick = currentTick;

            // Defensive: Validate vehicle
            if (vehicle == null || !vehicle.Exists())
                return;

            // Defensive: Validate position
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z))
                return;

            try
            {
                float ourSpeed = vehicle.Speed;
                float ourHeading = vehicle.Heading;

                // Defensive: Validate speed and heading
                if (float.IsNaN(ourSpeed) || float.IsNaN(ourHeading))
                    return;

                // OPTIMIZED: Use pre-calculated DEG_TO_RAD constant and reuse cached vectors
                float headingRad = (90f - ourHeading) * Constants.DEG_TO_RAD;
                float cosH = (float)Math.Cos(headingRad);
                float sinH = (float)Math.Sin(headingRad);

                _cachedForward.X = cosH;
                _cachedForward.Y = sinH;
                _cachedForward.Z = 0f;

                _cachedRight.X = sinH;
                _cachedRight.Y = -cosH;
                _cachedRight.Z = 0f;

                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(position, Constants.OVERTAKE_DETECTION_RADIUS);

                // Defensive: Check for null array
                if (nearbyVehicles == null)
                    return;

                _visibleHandles.Clear();

                // Defensive: Limit the number of vehicles to process to prevent runaway loops
                int vehicleLimit = Math.Min(nearbyVehicles.Length, 50);

                for (int i = 0; i < vehicleLimit; i++)
                {
                    Vehicle v = nearbyVehicles[i];
                    if (v == null || v.Handle == vehicle.Handle || !v.Exists()) continue;

                    int handle = v.Handle;

                    // Defensive: Limit _visibleHandles size to prevent unbounded growth
                    if (_visibleHandles.Count < 100)
                    {
                        _visibleHandles.Add(handle);
                    }

                    Vector3 toVehicle = v.Position - position;
                    float distance = toVehicle.Length();

                    // Skip if distance is invalid
                    if (float.IsNaN(distance))
                        continue;

                    // OPTIMIZED: Use cached vectors instead of allocating new ones
                    float forwardDist = Vector3.Dot(toVehicle, _cachedForward);
                    float lateralDist = Vector3.Dot(toVehicle, _cachedRight);

                    int newState;
                    if (forwardDist > Constants.OVERTAKE_SIDE_DISTANCE)
                        newState = 0; // Ahead
                    else if (forwardDist < -Constants.OVERTAKE_BEHIND_DISTANCE)
                        newState = 2; // Behind
                    else
                        newState = 1; // Beside

                    if (_overtakeTracking.TryGetValue(handle, out OvertakeTrackingInfo info))
                    {
                        if (info.State == 1 && newState == 2)
                        {
                            float theirSpeed = v.Speed;

                            if (!float.IsNaN(theirSpeed) && ourSpeed > theirSpeed + Constants.OVERTAKE_MIN_SPEED_DIFF)
                            {
                                if (currentTick - _lastOvertakeAnnounceTick > Constants.OVERTAKE_ANNOUNCE_COOLDOWN)
                                {
                                    _lastOvertakeAnnounceTick = currentTick;
                                    string side = lateralDist > 0 ? "right" : "left";
                                    _announcementQueue.TryAnnounce($"Passed vehicle on {side}",
                                        Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceTrafficAwareness");
                                }
                            }

                            _overtakeTracking.Remove(handle);
                            continue;
                        }

                        info.State = newState;
                        _overtakeTracking[handle] = info;
                    }
                    else if (newState == 0 && _overtakeTracking.Count < Constants.OVERTAKE_TRACKING_MAX)
                    {
                        float theirSpeed = v.Speed;
                        if (!float.IsNaN(theirSpeed) && ourSpeed > theirSpeed + Constants.OVERTAKE_MIN_SPEED_DIFF)
                        {
                            _overtakeTracking[handle] = new OvertakeTrackingInfo(handle, distance, currentTick);
                        }
                    }
                }

                // Cleanup stale entries
                _handleRemovalList.Clear();
                long staleThreshold = currentTick - 100_000_000;
                foreach (var kvp in _overtakeTracking)
                {
                    if (!_visibleHandles.Contains(kvp.Key) || kvp.Value.FirstSeenTick < staleThreshold)
                    {
                        _handleRemovalList.Add(kvp.Key);
                    }
                }
                foreach (int handle in _handleRemovalList)
                {
                    _overtakeTracking.Remove(handle);
                }

                // Defensive: Clear _visibleHandles to free memory if it grew too large
                if (_visibleHandles.Count > 50)
                {
                    _visibleHandles.Clear();
                    _visibleHandles.TrimExcess();
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TrafficAwarenessManager.CheckOvertaking");
            }
        }

        /// <summary>
        /// Check following distance using 2-3 second rule
        /// </summary>
        public void CheckFollowingDistance(Vehicle vehicle, long currentTick, float targetSpeed,
            float roadTypeSpeedMultiplier, float weatherSpeedMultiplier, float timeSpeedMultiplier,
            int drivingStyleMode, bool taskIssued)
        {
            if (currentTick - _lastFollowingCheckTick < Constants.TICK_INTERVAL_FOLLOWING_CHECK)
                return;

            _lastFollowingCheckTick = currentTick;

            float vehicleSpeed = vehicle.Speed;
            float followingTimeGap = float.MaxValue;

            if (_lastVehicleAheadDistance < Constants.FOLLOWING_CLEAR_ROAD &&
                _lastVehicleAheadDistance < float.MaxValue &&
                vehicleSpeed > 1f)
            {
                followingTimeGap = _lastVehicleAheadDistance / Math.Max(vehicleSpeed, 1f);
            }

            int followingState;
            if (_lastVehicleAheadDistance >= Constants.FOLLOWING_CLEAR_ROAD)
            {
                followingState = 0;
                _followingTimeGap = float.MaxValue;
            }
            else
            {
                _followingTimeGap = followingTimeGap;

                if (followingTimeGap >= 4.0f)
                    followingState = 0;
                else if (followingTimeGap >= 3.0f)
                    followingState = 1;
                else if (followingTimeGap >= 2.0f)
                    followingState = 2;
                else if (followingTimeGap >= 1.5f)
                    followingState = 3;
                else
                    followingState = 4;
            }

            if (taskIssued)
            {
                AdjustSpeedForFollowing(vehicle, followingState, vehicleSpeed, targetSpeed,
                    roadTypeSpeedMultiplier, weatherSpeedMultiplier, timeSpeedMultiplier, drivingStyleMode);
            }

            if (followingState != _lastFollowingState &&
                currentTick - _lastFollowingAnnounceTick > Constants.FOLLOWING_ANNOUNCE_COOLDOWN)
            {
                _lastFollowingAnnounceTick = currentTick;

                int previousState = _lastFollowingState;
                _lastFollowingState = followingState;

                if (followingState == 0 && previousState >= 3)
                {
                    _announcementQueue.TryAnnounce("Road clear ahead", Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceTrafficAwareness");
                }
                else if (followingState == 3)
                {
                    _announcementQueue.TryAnnounce("Following too closely", Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceTrafficAwareness");
                }
                else if (followingState == 4)
                {
                    _announcementQueue.TryAnnounce("Dangerously close", Constants.ANNOUNCE_PRIORITY_HIGH, currentTick, "announceTrafficAwareness");
                }
            }
            else
            {
                _lastFollowingState = followingState;
            }
        }

        private void AdjustSpeedForFollowing(Vehicle vehicle, int followingState, float currentSpeed,
            float targetSpeed, float roadTypeMultiplier, float weatherMultiplier, float timeMultiplier,
            int drivingStyleMode)
        {
            float adjustedTargetSpeed = CalculateTargetSpeedForFollowing(followingState, currentSpeed,
                targetSpeed, roadTypeMultiplier, weatherMultiplier, timeMultiplier, drivingStyleMode);

            ApplySmoothSpeedTransition(vehicle, adjustedTargetSpeed, currentSpeed, drivingStyleMode);
        }

        private float CalculateTargetSpeedForFollowing(int followingState, float currentSpeed,
            float targetSpeed, float roadTypeMultiplier, float weatherMultiplier, float timeMultiplier,
            int drivingStyleMode)
        {
            float baseTargetSpeed = targetSpeed * roadTypeMultiplier * weatherMultiplier * timeMultiplier;

            switch (followingState)
            {
                case 0:
                    return Math.Min(baseTargetSpeed, currentSpeed + GetAccelerationRate(drivingStyleMode));
                case 1:
                    return Math.Min(baseTargetSpeed, currentSpeed + (GetAccelerationRate(drivingStyleMode) * 0.5f));
                case 2:
                    return Math.Max(baseTargetSpeed * 0.9f, Math.Min(baseTargetSpeed, currentSpeed));
                case 3:
                    return Math.Max(baseTargetSpeed * 0.75f, currentSpeed - GetDecelerationRate(drivingStyleMode));
                case 4:
                    return Math.Max(Constants.AUTODRIVE_MIN_SPEED, currentSpeed - (GetDecelerationRate(drivingStyleMode) * 2f));
            }

            return baseTargetSpeed;
        }

        private void ApplySmoothSpeedTransition(Vehicle vehicle, float targetSpeed, float currentSpeed, int drivingStyleMode)
        {
            float speedDiff = targetSpeed - currentSpeed;

            if (Math.Abs(speedDiff) < 0.5f) return;

            float maxChangeRate = speedDiff > 0 ? GetAccelerationRate(drivingStyleMode) : GetDecelerationRate(drivingStyleMode);
            float actualChange = Math.Max(-maxChangeRate, Math.Min(maxChangeRate, speedDiff));

            float newSpeed = currentSpeed + actualChange;
            newSpeed = Math.Max(Constants.AUTODRIVE_MIN_SPEED, Math.Min(Constants.AUTODRIVE_MAX_SPEED, newSpeed));

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
                Logger.Exception(ex, "TrafficAwarenessManager.ApplySmoothSpeedTransition");
            }
        }

        private float GetAccelerationRate(int drivingStyleMode)
        {
            switch (drivingStyleMode)
            {
                case Constants.DRIVING_STYLE_MODE_CAUTIOUS: return 1.0f;
                case Constants.DRIVING_STYLE_MODE_NORMAL: return 2.0f;
                case Constants.DRIVING_STYLE_MODE_FAST: return 3.0f;
                case Constants.DRIVING_STYLE_MODE_RECKLESS: return 4.0f;
                default: return 2.0f;
            }
        }

        private float GetDecelerationRate(int drivingStyleMode)
        {
            switch (drivingStyleMode)
            {
                case Constants.DRIVING_STYLE_MODE_CAUTIOUS: return 2.0f;
                case Constants.DRIVING_STYLE_MODE_NORMAL: return 3.0f;
                case Constants.DRIVING_STYLE_MODE_FAST: return 4.0f;
                case Constants.DRIVING_STYLE_MODE_RECKLESS: return 5.0f;
                default: return 3.0f;
            }
        }
    }
}
