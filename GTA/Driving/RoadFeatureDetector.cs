using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Detects and announces road features including curves, intersections, and traffic lights.
    /// Handles curve slowdown for safe driving.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class RoadFeatureDetector
    {
        private readonly AudioManager _audio;
        private readonly AnnouncementQueue _announcementQueue;
        private readonly WeatherManager _weatherManager;

        // Curve detection state
        private long _lastCurveAnnounceTick;
        private long _lastIntersectionAnnounceTick;
        private long _lastTrafficLightAnnounceTick;

        // Curve slowdown state
        private bool _curveSlowdownActive;
        private long _curveSlowdownEndTick;
        private float _originalSpeed;
        private float _curveSlowdownSpeed;

        // Intersection tracking
        private bool _inIntersection;
        private float _preIntersectionHeading;
        private Vector3 _intersectionPosition;

        // Traffic light state (reserved for future use)
        private Vector3 _trafficLightStopPosition;

        // Pre-allocated OutputArguments
        private readonly OutputArgument _nodePos = new OutputArgument();
        private readonly OutputArgument _nodeHeading = new OutputArgument();
        private readonly OutputArgument _density = new OutputArgument();
        private readonly OutputArgument _flags = new OutputArgument();

        /// <summary>
        /// Whether curve slowdown is currently active
        /// </summary>
        public bool IsCurveSlowdownActive => _curveSlowdownActive;

        /// <summary>
        /// Current curve slowdown speed
        /// </summary>
        public float CurveSlowdownSpeed => _curveSlowdownSpeed;

        /// <summary>
        /// Whether currently in an intersection
        /// </summary>
        public bool IsInIntersection => _inIntersection;

        public RoadFeatureDetector(AudioManager audio, AnnouncementQueue announcementQueue, WeatherManager weatherManager)
        {
            _audio = audio;
            _announcementQueue = announcementQueue;
            _weatherManager = weatherManager;
            Reset();
        }

        /// <summary>
        /// Reset all state
        /// </summary>
        public void Reset()
        {
            _lastCurveAnnounceTick = 0;
            _lastIntersectionAnnounceTick = 0;
            _lastTrafficLightAnnounceTick = 0;
            _curveSlowdownActive = false;
            _curveSlowdownEndTick = 0;
            _originalSpeed = 0;
            _curveSlowdownSpeed = 0;
            _inIntersection = false;
            _preIntersectionHeading = 0;
            _intersectionPosition = Vector3.Zero;
            _trafficLightStopPosition = Vector3.Zero;
        }

        /// <summary>
        /// Check and announce road features
        /// </summary>
        public void Update(Vehicle vehicle, Vector3 position, long currentTick,
            float targetSpeed, int drivingStyleMode, bool autoDriveActive)
        {
            if (vehicle == null || !vehicle.Exists())
                return;

            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
                float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
                return;

            float speed = vehicle.Speed;
            if (speed < Constants.ROAD_FEATURE_MIN_SPEED) return;

            // Check if curve slowdown should expire
            if (_curveSlowdownActive && currentTick > _curveSlowdownEndTick)
            {
                EndCurveSlowdown();
            }

            float vehicleHeading = vehicle.Heading;

            // Dynamic lookahead based on speed
            float lookaheadDistance = Math.Min(
                Constants.ROAD_LOOKAHEAD_MAX,
                Math.Max(Constants.ROAD_LOOKAHEAD_MIN, speed * Constants.ROAD_LOOKAHEAD_SPEED_FACTOR));

            long cooldown = CalculateSpeedScaledCooldown(speed);

            // Look ahead at multiple distances
            for (float distance = Constants.ROAD_SAMPLE_INTERVAL; distance <= lookaheadDistance; distance += Constants.ROAD_SAMPLE_INTERVAL)
            {
                float radians = (90f - vehicleHeading) * (float)Math.PI / 180f;
                Vector3 lookAheadPos = position + new Vector3(
                    (float)Math.Cos(radians) * distance,
                    (float)Math.Sin(radians) * distance,
                    0f);

                bool found = Function.Call<bool>(
                    (Hash)Constants.NATIVE_GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                    lookAheadPos.X, lookAheadPos.Y, lookAheadPos.Z,
                    _nodePos, _nodeHeading, 1, 3f, 0f);

                if (!found) continue;

                float roadHeading = _nodeHeading.GetResult<float>();

                CurveInfo curveInfo = AnalyzeCurveCharacteristics(vehicleHeading, roadHeading, distance, speed, drivingStyleMode);

                if (curveInfo.Severity != CurveSeverity.None)
                {
                    if (currentTick - _lastCurveAnnounceTick > cooldown)
                    {
                        _lastCurveAnnounceTick = currentTick;
                        string announcement = GenerateCurveAnnouncement(curveInfo, distance);
                        _announcementQueue.TryAnnounce(announcement, Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "roadFeatureAnnouncements");

                        float curveSlowdownDistance = CalculateCurveSlowdownDistance(speed, curveInfo.Severity);

                        if (autoDriveActive && distance <= curveSlowdownDistance && !_curveSlowdownActive)
                        {
                            ApplyIntelligentCurveSlowdown(curveInfo, speed, targetSpeed, currentTick);
                        }

                        return;
                    }
                }

                Vector3 nodePosition = _nodePos.GetResult<Vector3>();
                Function.Call(
                    (Hash)Constants.NATIVE_GET_VEHICLE_NODE_PROPERTIES,
                    nodePosition.X, nodePosition.Y, nodePosition.Z,
                    _density, _flags);

                int nodeFlags = _flags.GetResult<int>();

                bool hasTrafficLight = (nodeFlags & 256) != 0;
                if (hasTrafficLight && currentTick - _lastTrafficLightAnnounceTick > cooldown)
                {
                    _lastTrafficLightAnnounceTick = currentTick;
                    string distanceText = FormatDistance(distance);
                    _announcementQueue.TryAnnounce($"Traffic light ahead, {distanceText}",
                        Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceTrafficLights");
                    return;
                }

                bool isJunction = (nodeFlags & 128) != 0;
                if (isJunction && !hasTrafficLight && currentTick - _lastIntersectionAnnounceTick > cooldown)
                {
                    _lastIntersectionAnnounceTick = currentTick;
                    string distanceText = FormatDistance(distance);
                    _announcementQueue.TryAnnounce($"Intersection ahead, {distanceText}",
                        Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "roadFeatureAnnouncements");
                    return;
                }
            }

            UpdateIntersectionTracking(vehicle, position, currentTick);
        }

        /// <summary>
        /// Update intersection tracking for turn announcements
        /// </summary>
        private void UpdateIntersectionTracking(Vehicle vehicle, Vector3 position, long currentTick)
        {
            Function.Call(
                (Hash)Constants.NATIVE_GET_VEHICLE_NODE_PROPERTIES,
                position.X, position.Y, position.Z,
                _density, _flags);

            int nodeFlags = _flags.GetResult<int>();
            bool atJunction = (nodeFlags & 128) != 0;

            if (atJunction && !_inIntersection)
            {
                _inIntersection = true;
                _preIntersectionHeading = vehicle.Heading;
                _intersectionPosition = position;
            }
            else if (_inIntersection && !atJunction)
            {
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

                _announcementQueue.TryAnnounce(direction, Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "roadFeatureAnnouncements");
            }
        }

        private long CalculateSpeedScaledCooldown(float speed)
        {
            if (speed >= Constants.ROAD_FEATURE_COOLDOWN_SPEED_THRESHOLD)
            {
                return Constants.ROAD_FEATURE_COOLDOWN_MIN;
            }

            float speedRatio = speed / Constants.ROAD_FEATURE_COOLDOWN_SPEED_THRESHOLD;
            long cooldownRange = Constants.ROAD_FEATURE_COOLDOWN_MAX - Constants.ROAD_FEATURE_COOLDOWN_MIN;
            return Constants.ROAD_FEATURE_COOLDOWN_MAX - (long)(cooldownRange * speedRatio);
        }

        private float CalculateCurveSlowdownDistance(float speed, CurveSeverity severity)
        {
            float baseDistance = Constants.CURVE_SLOWDOWN_DISTANCE_BASE;
            float speedBasedDistance = speed * Constants.CURVE_SLOWDOWN_DISTANCE_SPEED_FACTOR;

            float severityMultiplier = 1.0f;
            switch (severity)
            {
                case CurveSeverity.Gentle:
                    severityMultiplier = 0.8f;
                    break;
                case CurveSeverity.Moderate:
                    severityMultiplier = 1.0f;
                    break;
                case CurveSeverity.Sharp:
                    severityMultiplier = 1.3f;
                    break;
                case CurveSeverity.Hairpin:
                    severityMultiplier = 1.6f;
                    break;
            }

            float calculatedDistance = Math.Max(baseDistance, speedBasedDistance) * severityMultiplier;

            return Math.Max(
                Constants.CURVE_SLOWDOWN_DISTANCE_MIN,
                Math.Min(Constants.CURVE_SLOWDOWN_DISTANCE_MAX, calculatedDistance));
        }

        private void StartCurveSlowdown(float slowdownFactor, float targetSpeed, long currentTick)
        {
            try
            {
                _originalSpeed = targetSpeed;
                _curveSlowdownSpeed = targetSpeed * slowdownFactor;
                _curveSlowdownActive = true;
                _curveSlowdownEndTick = currentTick + Constants.CURVE_SLOWDOWN_DURATION;

                Ped player = Game.Player.Character;
                Function.Call(
                    (Hash)Constants.NATIVE_SET_DRIVE_TASK_CRUISE_SPEED,
                    player.Handle,
                    _curveSlowdownSpeed);

                Logger.Debug($"Curve slowdown: {_originalSpeed:F1} -> {_curveSlowdownSpeed:F1} m/s");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RoadFeatureDetector.StartCurveSlowdown");
            }
        }

        /// <summary>
        /// End curve slowdown
        /// </summary>
        public void EndCurveSlowdown()
        {
            if (!_curveSlowdownActive) return;
            _curveSlowdownActive = false;
            Logger.Debug($"Curve slowdown ended");
        }

        private CurveInfo AnalyzeCurveCharacteristics(float vehicleHeading, float roadHeading,
            float distance, float currentSpeed, int drivingStyleMode)
        {
            float headingDiff = NormalizeAngleDiff(roadHeading - vehicleHeading);
            float absAngle = Math.Abs(headingDiff);

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

            float curveRadius = distance / (float)Math.Tan(absAngle * Math.PI / 180f / 2f);
            float frictionCoeff = _weatherManager?.GetRoadFrictionCoefficient() ?? 0.8f;
            float safeSpeed = (float)Math.Sqrt(frictionCoeff * 9.81f * curveRadius);

            safeSpeed *= GetCurveSpeedModifier(drivingStyleMode);
            safeSpeed = Math.Max(2f, Math.Min(safeSpeed, currentSpeed * 1.2f));

            CurveDirection direction = headingDiff > 0 ? CurveDirection.Right : CurveDirection.Left;

            return new CurveInfo(severity, direction, absAngle, curveRadius, safeSpeed);
        }

        private float GetCurveSpeedModifier(int drivingStyleMode)
        {
            switch (drivingStyleMode)
            {
                case Constants.DRIVING_STYLE_MODE_CAUTIOUS:
                    return 0.8f;
                case Constants.DRIVING_STYLE_MODE_NORMAL:
                    return 0.9f;
                case Constants.DRIVING_STYLE_MODE_FAST:
                    return 1.0f;
                case Constants.DRIVING_STYLE_MODE_RECKLESS:
                    return 1.1f;
                default:
                    return 0.9f;
            }
        }

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

        private void ApplyIntelligentCurveSlowdown(CurveInfo curveInfo, float currentSpeed, float targetSpeed, long currentTick)
        {
            if (curveInfo.SafeSpeed >= currentSpeed * 0.9f)
                return;

            float slowdownFactor = curveInfo.SafeSpeed / currentSpeed;
            slowdownFactor = Math.Max(0.3f, Math.Min(1.0f, slowdownFactor));

            StartCurveSlowdown(slowdownFactor, targetSpeed, currentTick);
        }

        /// <summary>
        /// Format distance for speech in imperial units
        /// </summary>
        public static string FormatDistance(float meters)
        {
            float feet = meters * Constants.METERS_TO_FEET;

            if (feet < 528)
            {
                int roundedFeet = ((int)(feet / 50) + 1) * 50;
                return $"{roundedFeet} feet";
            }
            else if (feet < 1320)
            {
                return "quarter mile";
            }
            else if (feet < 2640)
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
        public static float NormalizeAngleDiff(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
