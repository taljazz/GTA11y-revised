using System;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Centralized speed control for AutoDrive. All speed-affecting systems set their
    /// desired modifier or speed cap on the arbiter, and a single ApplySpeed() call
    /// per tick computes and applies the final speed via SET_DRIVE_TASK_CRUISE_SPEED.
    ///
    /// This eliminates the "last writer wins" problem where multiple systems independently
    /// called SET_DRIVE_TASK_CRUISE_SPEED and the final speed depended on execution order.
    ///
    /// Final speed = min(arrivalCap, baseSpeed * styleMultiplier * roadTypeMultiplier
    ///                                        * weatherMultiplier * timeMultiplier)
    /// Clamped to [MIN_SPEED, AUTODRIVE_MAX_SPEED]
    ///
    /// Curve and following speed caps removed — the GTA V AI handles curves at engine level
    /// (vehicleaihandlinginfo.meta AICurvePoints) and traffic via driving flags.
    /// BRING_VEHICLE_TO_HALT provides physics-level emergency braking as a safety net.
    ///
    /// Uses BOTH SET_DRIVE_TASK_CRUISE_SPEED (target) and SET_DRIVE_TASK_MAX_CRUISE_SPEED (ceiling).
    /// The max cruise speed ensures the AI cooperatively respects the arrival cap
    /// without needing velocity overrides that fight the AI.
    /// </summary>
    internal class SpeedArbiter
    {
        // Pre-cached hashes for native calls
        private static readonly Hash _setCruiseSpeedHash = (Hash)Constants.NATIVE_SET_DRIVE_TASK_CRUISE_SPEED;
        private static readonly Hash _setMaxCruiseSpeedHash = (Hash)Constants.NATIVE_SET_DRIVE_TASK_MAX_CRUISE_SPEED;

        // Base target speed set by the user (m/s)
        private float _baseTargetSpeed;

        // Multipliers (1.0 = no effect)
        private float _styleMultiplier = 1.0f;
        private float _roadTypeMultiplier = 1.0f;
        private float _weatherMultiplier = 1.0f;
        private float _timeMultiplier = 1.0f;

        // Speed caps (float.MaxValue = no cap)
        private float _arrivalCap = float.MaxValue;

        // Minimum speed floor (prevent stalling)
        private const float MIN_SPEED = 2.0f;

        // Track last applied speed to avoid redundant native calls
        private float _lastAppliedSpeed;
        private const float SPEED_CHANGE_THRESHOLD = 0.5f;

        // Track whether any value changed since last apply
        private bool _dirty = true;

        /// <summary>
        /// The final computed speed from the last ApplySpeed() call
        /// </summary>
        public float CurrentEffectiveSpeed { get; private set; }

        /// <summary>
        /// The base target speed before any modifiers
        /// </summary>
        public float BaseTargetSpeed => _baseTargetSpeed;

        public SpeedArbiter(float initialSpeed)
        {
            _baseTargetSpeed = initialSpeed;
            CurrentEffectiveSpeed = initialSpeed;
        }

        /// <summary>
        /// Set the base target speed (from user's speed setting)
        /// </summary>
        public void SetBaseSpeed(float speed)
        {
            if (Math.Abs(_baseTargetSpeed - speed) > 0.01f)
            {
                _baseTargetSpeed = speed;
                _dirty = true;
            }
        }

        /// <summary>
        /// Set the driving style speed multiplier
        /// </summary>
        public void SetStyleMultiplier(float multiplier)
        {
            if (Math.Abs(_styleMultiplier - multiplier) > 0.001f)
            {
                _styleMultiplier = multiplier;
                _dirty = true;
            }
        }

        /// <summary>
        /// Set the road type speed multiplier
        /// </summary>
        public void SetRoadTypeMultiplier(float multiplier)
        {
            if (Math.Abs(_roadTypeMultiplier - multiplier) > 0.001f)
            {
                _roadTypeMultiplier = multiplier;
                _dirty = true;
            }
        }

        /// <summary>
        /// Set the weather speed multiplier
        /// </summary>
        public void SetWeatherMultiplier(float multiplier)
        {
            if (Math.Abs(_weatherMultiplier - multiplier) > 0.001f)
            {
                _weatherMultiplier = multiplier;
                _dirty = true;
            }
        }

        /// <summary>
        /// Set the time-of-day speed multiplier
        /// </summary>
        public void SetTimeMultiplier(float multiplier)
        {
            if (Math.Abs(_timeMultiplier - multiplier) > 0.001f)
            {
                _timeMultiplier = multiplier;
                _dirty = true;
            }
        }

        /// <summary>
        /// Set the arrival slowdown speed cap (float.MaxValue to clear)
        /// </summary>
        public void SetArrivalCap(float cap)
        {
            if (Math.Abs(_arrivalCap - cap) > 0.01f)
            {
                _arrivalCap = cap;
                _dirty = true;
            }
        }

        /// <summary>
        /// Clear the arrival speed cap
        /// </summary>
        public void ClearArrivalCap()
        {
            SetArrivalCap(float.MaxValue);
        }

        /// <summary>
        /// Calculate the effective speed without applying it.
        /// Used when issuing new driving tasks that need the current effective speed.
        /// </summary>
        public float CalculateEffectiveSpeed()
        {
            // Base speed with all multipliers
            float modifiedSpeed = _baseTargetSpeed * _styleMultiplier * _roadTypeMultiplier
                                  * _weatherMultiplier * _timeMultiplier;

            // Apply arrival cap (the only speed cap — AI handles curves and traffic natively)
            float cappedSpeed = modifiedSpeed;
            if (_arrivalCap < cappedSpeed) cappedSpeed = _arrivalCap;

            // Enforce minimum speed
            if (cappedSpeed < MIN_SPEED) cappedSpeed = MIN_SPEED;

            // Enforce maximum speed cap
            if (cappedSpeed > Constants.AUTODRIVE_MAX_SPEED) cappedSpeed = Constants.AUTODRIVE_MAX_SPEED;

            return cappedSpeed;
        }

        /// <summary>
        /// Compute and apply the final speed via SET_DRIVE_TASK_CRUISE_SPEED.
        /// Call this ONCE per tick from the Update loop.
        /// Returns the applied speed.
        /// </summary>
        public float ApplySpeed(Ped player)
        {
            float effectiveSpeed = CalculateEffectiveSpeed();
            CurrentEffectiveSpeed = effectiveSpeed;

            // Only call native if speed actually changed significantly
            if (_dirty || Math.Abs(effectiveSpeed - _lastAppliedSpeed) > SPEED_CHANGE_THRESHOLD)
            {
                try
                {
                    if (player != null && player.Exists() && player.IsInVehicle())
                    {
                        // Set target speed (AI accelerates/decelerates toward this)
                        Function.Call(_setCruiseSpeedHash, player.Handle, effectiveSpeed);

                        // Set max cruise speed ceiling (AI will NEVER exceed this).
                        // Small headroom above effective speed lets the AI drive naturally
                        // while respecting the arrival cap.
                        float maxSpeed = effectiveSpeed * 1.05f;
                        if (maxSpeed > Constants.AUTODRIVE_MAX_SPEED) maxSpeed = Constants.AUTODRIVE_MAX_SPEED;
                        Function.Call(_setMaxCruiseSpeedHash, player.Handle, maxSpeed);

                        _lastAppliedSpeed = effectiveSpeed;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "SpeedArbiter.ApplySpeed");
                }
                _dirty = false;
            }

            return effectiveSpeed;
        }

        /// <summary>
        /// Force an immediate speed apply on next call (e.g., after task re-issue)
        /// </summary>
        public void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>
        /// Reset all multipliers and caps to defaults
        /// </summary>
        public void Reset()
        {
            _styleMultiplier = 1.0f;
            _roadTypeMultiplier = 1.0f;
            _weatherMultiplier = 1.0f;
            _timeMultiplier = 1.0f;
            _arrivalCap = float.MaxValue;
            _lastAppliedSpeed = 0f;
            _dirty = true;
        }
    }
}
