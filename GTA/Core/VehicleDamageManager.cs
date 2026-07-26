using System;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Monitors vehicle damage state (engine, body, tires, fire) and announces
    /// significant changes via TTS. Resets tracking when the player changes vehicles.
    /// Engine/body health: 0-1000 scale (1000 = perfect).
    /// Tire burst detection via IS_VEHICLE_TYRE_BURST for wheel indices 0-3.
    /// Derives from MonitorBase&lt;Vehicle&gt; which supplies throttling, the enabled
    /// setting check, and exception handling.
    /// </summary>
    public class VehicleDamageManager : MonitorBase<Vehicle>
    {
        #region Constants

        private const long UPDATE_INTERVAL = 1_000;   // 1 second

        // Cached native hash for tire burst check
        private static readonly Hash _isTireBurstHash = Hash.IS_VEHICLE_TYRE_BURST;

        // Tire name lookup by wheel index
        private static readonly string[] TireNames = { "Front left", "Front right", "Rear left", "Rear right" };

        #endregion

        #region Fields

        // Vehicle tracking - reset when vehicle changes
        private int _lastVehicleHandle;

        // Previous state for change detection
        private int _lastEngineThreshold;
        private int _lastBodyThreshold;
        private bool _wasOnFire;

        // Tire burst tracking (indices 0-3: FL, FR, RL, RR)
        private bool _tireBurst0;
        private bool _tireBurst1;
        private bool _tireBurst2;
        private bool _tireBurst3;

        #endregion

        #region Construction

        public VehicleDamageManager(AudioManager audio, SettingsManager settings)
            : base(audio, settings)
        {
            _lastVehicleHandle = 0;
            ResetTracking();
        }

        #endregion

        #region MonitorBase Overrides

        protected override long UpdateIntervalMs => UPDATE_INTERVAL;

        protected override string EnabledSettingKey => "announceVehicleDamage";

        protected override bool ValidateSubject(Vehicle vehicle)
        {
            return vehicle != null && vehicle.Exists();
        }

        protected override void OnUpdate(Vehicle vehicle, long currentTick)
        {
            // Reset tracking when vehicle changes
            int vehicleHandle = vehicle.Handle;
            if (vehicleHandle != _lastVehicleHandle)
            {
                ResetTracking();
                _lastVehicleHandle = vehicleHandle;
                return; // Skip first tick for new vehicle to establish baseline
            }

            CheckEngineHealth(vehicle);
            CheckBodyHealth(vehicle);
            CheckTires(vehicle);
            CheckFire(vehicle);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Speak a full vehicle health summary on demand.
        /// </summary>
        public void AnnounceStatus(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return;

            try
            {
                float engineHealth = vehicle.EngineHealth;
                float bodyHealth = vehicle.BodyHealth;
                bool onFire = vehicle.IsOnFire;

                string engineStatus = GetEngineStatusText(engineHealth);
                string bodyStatus = GetBodyStatusText(bodyHealth);

                string status = $"Engine {engineStatus}, Body {bodyStatus}";

                // Count burst tires
                int burstCount = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (Function.Call<bool>(_isTireBurstHash, vehicle, i, false))
                        burstCount++;
                }

                if (burstCount > 0)
                    status += $", {burstCount} tire{(burstCount > 1 ? "s" : "")} burst";

                if (onFire)
                    status += ", Vehicle on fire";

                Audio.Speak(status, true);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "VehicleDamageManager.AnnounceStatus");
            }
        }

        #endregion

        #region Damage Checks

        /// <summary>
        /// Check engine health and announce threshold crossings.
        /// Thresholds: 500 (damaged), 300 (badly damaged), 100 (critical), 0 (dead).
        /// </summary>
        private void CheckEngineHealth(Vehicle vehicle)
        {
            float engineHealth = vehicle.EngineHealth;
            int threshold = GetEngineThreshold(engineHealth);

            if (threshold < _lastEngineThreshold)
            {
                string message = GetEngineThresholdMessage(engineHealth);
                if (message != null)
                    Audio.Speak(message, true);
            }

            _lastEngineThreshold = threshold;
        }

        /// <summary>
        /// Check body health and announce threshold crossings.
        /// Same scale as engine: 500, 300, 100, 0.
        /// </summary>
        private void CheckBodyHealth(Vehicle vehicle)
        {
            float bodyHealth = vehicle.BodyHealth;
            int threshold = GetBodyThreshold(bodyHealth);

            if (threshold < _lastBodyThreshold)
            {
                string message = GetBodyThresholdMessage(bodyHealth);
                if (message != null)
                    Audio.Speak(message, true);
            }

            _lastBodyThreshold = threshold;
        }

        /// <summary>
        /// Check each tire and announce newly burst tires.
        /// Wheel indices: 0=FL, 1=FR, 2=RL, 3=RR.
        /// </summary>
        private void CheckTires(Vehicle vehicle)
        {
            CheckSingleTire(vehicle, 0, ref _tireBurst0);
            CheckSingleTire(vehicle, 1, ref _tireBurst1);
            CheckSingleTire(vehicle, 2, ref _tireBurst2);
            CheckSingleTire(vehicle, 3, ref _tireBurst3);
        }

        /// <summary>
        /// Check a single tire by wheel index, announce if newly burst.
        /// </summary>
        private void CheckSingleTire(Vehicle vehicle, int wheelIndex, ref bool wasBurst)
        {
            // Check for any burst (not just completely flat)
            bool isBurst = Function.Call<bool>(_isTireBurstHash, vehicle, wheelIndex, false);

            if (isBurst && !wasBurst)
            {
                string tireName = wheelIndex < TireNames.Length ? TireNames[wheelIndex] : $"Tire {wheelIndex}";
                Audio.Speak($"{tireName} tire burst", true);
            }

            wasBurst = isBurst;
        }

        /// <summary>
        /// Check if vehicle is on fire and announce the change.
        /// </summary>
        private void CheckFire(Vehicle vehicle)
        {
            bool onFire = vehicle.IsOnFire;

            if (onFire && !_wasOnFire)
                Audio.Speak("Vehicle on fire!", true);

            _wasOnFire = onFire;
        }

        /// <summary>
        /// Reset all damage tracking state (called on vehicle change).
        /// </summary>
        private void ResetTracking()
        {
            _lastEngineThreshold = 1000;
            _lastBodyThreshold = 1000;
            _wasOnFire = false;
            _tireBurst0 = false;
            _tireBurst1 = false;
            _tireBurst2 = false;
            _tireBurst3 = false;
        }

        #endregion

        #region Threshold Helpers

        /// <summary>
        /// Get threshold bucket for engine health.
        /// </summary>
        private static int GetEngineThreshold(float health)
        {
            if (health <= 0f) return 0;
            if (health < 100f) return 100;
            if (health < 300f) return 300;
            if (health < 500f) return 500;
            return 1000;
        }

        /// <summary>
        /// Get TTS message for engine health threshold crossing.
        /// </summary>
        private static string GetEngineThresholdMessage(float health)
        {
            if (health <= 0f) return "Engine dead";
            if (health < 100f) return "Engine critical";
            if (health < 300f) return "Engine badly damaged";
            if (health < 500f) return "Engine damaged";
            return null;
        }

        /// <summary>
        /// Get threshold bucket for body health.
        /// </summary>
        private static int GetBodyThreshold(float health)
        {
            if (health <= 0f) return 0;
            if (health < 100f) return 100;
            if (health < 300f) return 300;
            if (health < 500f) return 500;
            return 1000;
        }

        /// <summary>
        /// Get TTS message for body health threshold crossing.
        /// </summary>
        private static string GetBodyThresholdMessage(float health)
        {
            if (health <= 0f) return "Body destroyed";
            if (health < 100f) return "Body critical";
            if (health < 300f) return "Body badly damaged";
            if (health < 500f) return "Body damaged";
            return null;
        }

        /// <summary>
        /// Get a human-readable status string for engine health.
        /// </summary>
        private static string GetEngineStatusText(float health)
        {
            if (health <= 0f) return "dead";
            if (health < 100f) return "critical";
            if (health < 300f) return "badly damaged";
            if (health < 500f) return "damaged";
            if (health < 900f) return "minor damage";
            return "perfect";
        }

        /// <summary>
        /// Get a human-readable status string for body health.
        /// </summary>
        private static string GetBodyStatusText(float health)
        {
            if (health <= 0f) return "destroyed";
            if (health < 100f) return "critical";
            if (health < 300f) return "badly damaged";
            if (health < 500f) return "damaged";
            if (health < 900f) return "minor damage";
            return "perfect";
        }

        #endregion
    }
}
