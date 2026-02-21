using System;
using GTA;
using GTA.Math;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Collision warning level enumeration
    /// </summary>
    public enum CollisionWarningLevel
    {
        None = 0,
        Far = 1,
        Medium = 2,
        Close = 3,
        Imminent = 4
    }

    /// <summary>
    /// Detects vehicles ahead and provides collision warnings.
    /// Uses Time-To-Collision (TTC) for speed-appropriate warnings.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class CollisionDetector
    {
        private float _lastVehicleAheadDistance = float.MaxValue;
        private int _lastCollisionWarningLevel;
        private long _lastCollisionCheckTick;
        private long _lastCollisionAnnounceTick;
        // PERFORMANCE: Pre-allocated vectors to avoid per-frame allocations
        private Vector3 _forwardVector;
        private Vector3 _theirForwardVector;

        /// <summary>
        /// Distance to the closest vehicle ahead
        /// </summary>
        public float VehicleAheadDistance => _lastVehicleAheadDistance;

        /// <summary>
        /// Current collision warning level
        /// </summary>
        public CollisionWarningLevel WarningLevel => (CollisionWarningLevel)_lastCollisionWarningLevel;

        /// <summary>
        /// Whether a collision is imminent (requires immediate action)
        /// </summary>
        public bool IsCollisionImminent => _lastCollisionWarningLevel >= 4;

        /// <summary>
        /// Check for vehicles ahead and calculate collision warning level.
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="position">Current position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="warningMessage">Output: warning message if changed</param>
        /// <param name="priority">Output: announcement priority</param>
        /// <returns>True if warning level changed and announcement may be needed</returns>
        public bool CheckCollisionWarning(Vehicle vehicle, Vector3 position, long currentTick,
            out string warningMessage, out int priority)
        {
            warningMessage = null;
            priority = 0;

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
            if (currentTick - _lastCollisionCheckTick < Constants.TICK_INTERVAL_COLLISION_CHECK)
                return false;

            _lastCollisionCheckTick = currentTick;

            try
            {
                // Defensive: Check vehicle state before accessing properties
                if (vehicle.IsDead)
                    return false;

                float ourSpeed = vehicle.Speed;
                if (ourSpeed < 3f)
                {
                    _lastVehicleAheadDistance = float.MaxValue;
                    return false;  // Skip if barely moving
                }

                // Get forward direction
                // PERFORMANCE: Reuse pre-allocated vector, use pre-calculated DEG_TO_RAD constant
                float heading = vehicle.Heading;
                float radians = (90f - heading) * Constants.DEG_TO_RAD;
                _forwardVector.X = (float)Math.Cos(radians);
                _forwardVector.Y = (float)Math.Sin(radians);
                _forwardVector.Z = 0f;

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
                        float dot = Vector3.Dot(Vector3.Normalize(toVehicle), _forwardVector);
                        // PERFORMANCE: Use pre-calculated RAD_TO_DEG constant
                        float angle = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, dot))) * Constants.RAD_TO_DEG;

                        if (angle <= Constants.COLLISION_SCAN_ANGLE && distance < closestDistance)
                        {
                            closestDistance = distance;

                            // Calculate closing speed (our speed - their forward speed component)
                            float theirSpeed = v.Speed;
                            float theirHeading = v.Heading;
                            // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                            float theirRadians = (90f - theirHeading) * Constants.DEG_TO_RAD;
                            // PERFORMANCE: Reuse pre-allocated vector instead of allocating new one
                            _theirForwardVector.X = (float)Math.Cos(theirRadians);
                            _theirForwardVector.Y = (float)Math.Sin(theirRadians);
                            _theirForwardVector.Z = 0f;

                            // How much of their velocity is in our direction?
                            float theirSpeedInOurDirection = theirSpeed * Vector3.Dot(_theirForwardVector, _forwardVector);

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

                    switch (warningLevel)
                    {
                        case 4:
                            warningMessage = $"Collision imminent, {timeToCollision:F1} seconds";
                            priority = Constants.ANNOUNCE_PRIORITY_CRITICAL;
                            break;
                        case 3:
                            warningMessage = "Vehicle close ahead";
                            priority = Constants.ANNOUNCE_PRIORITY_HIGH;
                            break;
                        case 2:
                            warningMessage = "Vehicle ahead";
                            priority = Constants.ANNOUNCE_PRIORITY_MEDIUM;
                            break;
                        case 1:
                            warningMessage = "Traffic ahead";
                            priority = Constants.ANNOUNCE_PRIORITY_LOW;
                            break;
                    }
                    return warningMessage != null;
                }
                else if (warningLevel < _lastCollisionWarningLevel)
                {
                    // Deescalated - just update, don't announce
                    _lastCollisionWarningLevel = warningLevel;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CollisionDetector.CheckCollisionWarning");
            }

            return false;
        }

        /// <summary>
        /// Reset all collision detection state
        /// </summary>
        public void Reset()
        {
            _lastVehicleAheadDistance = float.MaxValue;
            _lastCollisionWarningLevel = 0;
            _lastCollisionCheckTick = 0;
            _lastCollisionAnnounceTick = 0;
        }
    }
}
