using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Analyzes road curves and calculates safe speeds.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class CurveAnalyzer
    {
        private readonly WeatherManager _weatherManager;

        // Pre-allocated OutputArguments to avoid per-call allocations
        private readonly OutputArgument _nodePos = new OutputArgument();
        private readonly OutputArgument _nodeHeading = new OutputArgument();
        private readonly OutputArgument _density = new OutputArgument();
        private readonly OutputArgument _flags = new OutputArgument();

        // Curve slowdown state
        private bool _curveSlowdownActive;
        private long _curveSlowdownEndTick;
        private float _originalSpeed;
        private float _curveSlowdownSpeed;

        /// <summary>
        /// Whether curve slowdown is currently active
        /// </summary>
        public bool IsCurveSlowdownActive => _curveSlowdownActive;

        /// <summary>
        /// Current curve slowdown speed (valid only if IsCurveSlowdownActive)
        /// </summary>
        public float CurveSlowdownSpeed => _curveSlowdownSpeed;

        public CurveAnalyzer(WeatherManager weatherManager)
        {
            _weatherManager = weatherManager;
        }

        /// <summary>
        /// Analyze curve characteristics for better prediction and handling.
        /// Considers angle, estimated radius, and safe speed calculations.
        /// </summary>
        /// <param name="vehicleHeading">Current vehicle heading</param>
        /// <param name="roadHeading">Road heading at lookahead position</param>
        /// <param name="distance">Distance to the curve</param>
        /// <param name="currentSpeed">Current vehicle speed</param>
        /// <param name="drivingStyleMode">Current driving style mode for speed adjustment</param>
        /// <returns>CurveInfo with severity, direction, and safe speed</returns>
        public CurveInfo AnalyzeCurve(float vehicleHeading, float roadHeading, float distance,
            float currentSpeed, int drivingStyleMode)
        {
            // Defensive: Validate float parameters (guard against NaN/Infinity)
            if (float.IsNaN(vehicleHeading) || float.IsInfinity(vehicleHeading) ||
                float.IsNaN(roadHeading) || float.IsInfinity(roadHeading) ||
                float.IsNaN(distance) || float.IsInfinity(distance) ||
                float.IsNaN(currentSpeed) || float.IsInfinity(currentSpeed))
            {
                return new CurveInfo(CurveSeverity.None, CurveDirection.Right, 0, 0, currentSpeed);
            }

            // Defensive: Ensure distance is positive
            if (distance <= 0)
            {
                return new CurveInfo(CurveSeverity.None, CurveDirection.Right, 0, 0, currentSpeed);
            }

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
            float curveRadius = distance / (float)Math.Tan(absAngle * Math.PI / 180f / 2f);

            // Calculate safe speed using physics: v = sqrt(mu * g * r)
            // Using coefficient of friction estimates for different road conditions
            float frictionCoeff = _weatherManager?.GetRoadFrictionCoefficient() ?? 0.8f;
            float safeSpeed = (float)Math.Sqrt(frictionCoeff * 9.81f * curveRadius);

            // Adjust for driving style
            safeSpeed *= GetCurveSpeedModifier(drivingStyleMode);

            // Cap at reasonable maximum (don't go slower than walking speed for very sharp curves)
            safeSpeed = Math.Max(2f, Math.Min(safeSpeed, currentSpeed * 1.2f));

            CurveDirection direction = headingDiff > 0 ? CurveDirection.Right : CurveDirection.Left;

            return new CurveInfo(severity, direction, absAngle, curveRadius, safeSpeed);
        }

        /// <summary>
        /// Scan road ahead and detect curves.
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="position">Current position</param>
        /// <param name="drivingStyleMode">Current driving style mode</param>
        /// <param name="curveInfo">Output: detected curve info</param>
        /// <param name="distance">Output: distance to curve</param>
        /// <returns>True if a curve was detected</returns>
        public bool DetectCurveAhead(Vehicle vehicle, Vector3 position, int drivingStyleMode,
            out CurveInfo curveInfo, out float distance)
        {
            curveInfo = default;
            distance = 0;

            // Defensive: Validate vehicle parameter
            if (vehicle == null || !vehicle.Exists())
                return false;

            // Defensive: Validate position (guard against NaN/Infinity)
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
                float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
                return false;

            try
            {
                // Defensive: Check vehicle state
                if (vehicle.IsDead)
                    return false;

                float speed = vehicle.Speed;
                if (speed < Constants.ROAD_FEATURE_MIN_SPEED)
                    return false;

                float vehicleHeading = vehicle.Heading;

                // Dynamic lookahead based on speed: faster = look further ahead
                float lookaheadDistance = Math.Min(
                    Constants.ROAD_LOOKAHEAD_MAX,
                    Math.Max(Constants.ROAD_LOOKAHEAD_MIN, speed * Constants.ROAD_LOOKAHEAD_SPEED_FACTOR));

                // Look ahead at multiple distances and check for curves
                for (float dist = Constants.ROAD_SAMPLE_INTERVAL; dist <= lookaheadDistance; dist += Constants.ROAD_SAMPLE_INTERVAL)
                {
                    // Calculate look-ahead position
                    float radians = (90f - vehicleHeading) * (float)Math.PI / 180f;
                    Vector3 lookAheadPos = position + new Vector3(
                        (float)Math.Cos(radians) * dist,
                        (float)Math.Sin(radians) * dist,
                        0f);

                    // Get road node at look-ahead position
                    bool found = Function.Call<bool>(
                        (Hash)Constants.NATIVE_GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                        lookAheadPos.X, lookAheadPos.Y, lookAheadPos.Z,
                        _nodePos, _nodeHeading, 1, 3f, 0f);

                    if (!found) continue;

                    float roadHeading = _nodeHeading.GetResult<float>();

                    // Analyze curve at this position
                    CurveInfo info = AnalyzeCurve(vehicleHeading, roadHeading, dist, speed, drivingStyleMode);

                    if (info.Severity != CurveSeverity.None)
                    {
                        curveInfo = info;
                        distance = dist;
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CurveAnalyzer.DetectCurveAhead");
                return false;
            }
        }

        /// <summary>
        /// Calculate speed-dependent slowdown distance for curves.
        /// At higher speeds, we need more distance to safely decelerate.
        /// </summary>
        public float CalculateSlowdownDistance(float speed, CurveSeverity severity)
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
        /// Start curve slowdown
        /// </summary>
        /// <param name="targetSpeed">Current target speed</param>
        /// <param name="curveInfo">Curve information</param>
        /// <param name="currentSpeed">Current vehicle speed</param>
        /// <param name="currentTick">Current game tick</param>
        /// <returns>The slowdown speed to apply, or -1 if no slowdown needed</returns>
        public float StartSlowdown(float targetSpeed, CurveInfo curveInfo, float currentSpeed, long currentTick)
        {
            if (curveInfo.SafeSpeed >= currentSpeed * 0.9f)
                return -1f; // No need to slow down significantly

            // Calculate slowdown factor based on speed difference
            float slowdownFactor = curveInfo.SafeSpeed / currentSpeed;
            slowdownFactor = Math.Max(0.3f, Math.Min(1.0f, slowdownFactor)); // Reasonable bounds

            _originalSpeed = targetSpeed;
            _curveSlowdownSpeed = targetSpeed * slowdownFactor;
            _curveSlowdownActive = true;
            _curveSlowdownEndTick = currentTick + Constants.CURVE_SLOWDOWN_DURATION;

            Logger.Debug($"Curve slowdown: {_originalSpeed:F1} -> {_curveSlowdownSpeed:F1} m/s");

            return _curveSlowdownSpeed;
        }

        /// <summary>
        /// Check if curve slowdown should end
        /// </summary>
        /// <param name="currentTick">Current game tick</param>
        /// <returns>True if slowdown ended and speed should be restored</returns>
        public bool CheckSlowdownExpired(long currentTick)
        {
            if (_curveSlowdownActive && currentTick > _curveSlowdownEndTick)
            {
                _curveSlowdownActive = false;
                Logger.Debug("Curve slowdown ended, speed restored");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Generate appropriate curve announcement based on characteristics
        /// </summary>
        public string GenerateAnnouncement(CurveInfo curveInfo, float distance)
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
        /// Get speed modifier for curves based on driving style
        /// </summary>
        private float GetCurveSpeedModifier(int drivingStyleMode)
        {
            switch (drivingStyleMode)
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
        /// Normalize angle difference to -180 to 180 range
        /// </summary>
        private float NormalizeAngleDiff(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
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
        /// Reset all curve analyzer state
        /// </summary>
        public void Reset()
        {
            _curveSlowdownActive = false;
            _curveSlowdownEndTick = 0;
            _originalSpeed = 0;
            _curveSlowdownSpeed = 0;
        }
    }
}
