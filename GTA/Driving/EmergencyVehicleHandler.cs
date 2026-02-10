using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Handles detection and yielding to emergency vehicles.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class EmergencyVehicleHandler
    {
        private readonly AudioManager _audio;
        private readonly AnnouncementQueue _announcementQueue;

        // PERFORMANCE: Pre-cached Hash values
        private static readonly Hash _sirenAudioOnHash = (Hash)Constants.NATIVE_IS_VEHICLE_SIREN_AUDIO_ON;
        private static readonly Hash _clearPedTasksHash = (Hash)Constants.NATIVE_CLEAR_PED_TASKS;
        private static readonly Hash _setHandbrakeHash = (Hash)Constants.NATIVE_SET_VEHICLE_HANDBRAKE;

        // Emergency vehicle state
        private bool _yieldingToEmergency;
        private long _emergencyYieldStartTick;
        private long _lastEmergencyCheckTick;
        private Vector3 _emergencyVehiclePosition;
        private bool _emergencyApproachingFromBehind;

        /// <summary>
        /// Whether currently yielding to an emergency vehicle
        /// </summary>
        public bool IsYieldingToEmergency => _yieldingToEmergency;

        /// <summary>
        /// Whether the emergency vehicle is approaching from behind
        /// </summary>
        public bool EmergencyApproachingFromBehind => _emergencyApproachingFromBehind;

        /// <summary>
        /// Position of the detected emergency vehicle
        /// </summary>
        public Vector3 EmergencyVehiclePosition => _emergencyVehiclePosition;

        public EmergencyVehicleHandler(AudioManager audio, AnnouncementQueue announcementQueue)
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
            _yieldingToEmergency = false;
            _emergencyYieldStartTick = 0;
            _lastEmergencyCheckTick = 0;
            _emergencyVehiclePosition = Vector3.Zero;
            _emergencyApproachingFromBehind = false;
        }

        /// <summary>
        /// Check for emergency vehicles with sirens and yield
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="position">Current position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="drivingStyleMode">Current driving style mode</param>
        /// <param name="onResume">Callback to resume driving when emergency passes</param>
        /// <returns>True if yielding, false otherwise</returns>
        public bool CheckEmergencyVehicles(Vehicle vehicle, Vector3 position, long currentTick,
            int drivingStyleMode, Action<Vehicle, long> onResume)
        {
            if (vehicle == null || !vehicle.Exists())
                return _yieldingToEmergency;

            // RECKLESS MODE: Skip emergency vehicle yielding
            if (drivingStyleMode == Constants.DRIVING_STYLE_MODE_RECKLESS)
            {
                if (_yieldingToEmergency)
                {
                    // Was yielding, now reckless - resume
                    _yieldingToEmergency = false;
                    onResume?.Invoke(vehicle, currentTick);
                }
                return false;
            }

            // Throttle checks
            if (currentTick - _lastEmergencyCheckTick < Constants.TICK_INTERVAL_EMERGENCY_CHECK)
                return _yieldingToEmergency;

            _lastEmergencyCheckTick = currentTick;

            try
            {
                // Check if currently yielding
                if (_yieldingToEmergency)
                {
                    // Check if yield time expired or emergency passed
                    if (currentTick - _emergencyYieldStartTick > Constants.EMERGENCY_YIELD_DURATION)
                    {
                        // Check if emergency vehicle is still nearby
                        bool stillNearby = IsEmergencyVehicleNearby(position);
                        if (!stillNearby)
                        {
                            _yieldingToEmergency = false;
                            onResume?.Invoke(vehicle, currentTick);
                            _announcementQueue.TryAnnounce("Emergency vehicle passed, resuming",
                                Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceEmergencyVehicles");
                        }
                        else
                        {
                            // Reset yield timer if still nearby
                            _emergencyYieldStartTick = currentTick;
                        }
                    }
                    return true;
                }

                // Scan for emergency vehicles with sirens on
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(position, Constants.EMERGENCY_DETECTION_RADIUS);

                // Get our forward direction for position analysis
                // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
                float ourHeading = vehicle.Heading;
                float ourRadians = (90f - ourHeading) * Constants.DEG_TO_RAD;
                Vector3 ourForward = new Vector3((float)Math.Cos(ourRadians), (float)Math.Sin(ourRadians), 0f);

                foreach (Vehicle v in nearbyVehicles)
                {
                    // Compare by Handle - SHVDN returns new wrapper objects each call
                    if (v.Handle == vehicle.Handle || !v.Exists()) continue;

                    // Check if siren is on
                    bool sirenOn = Function.Call<bool>(_sirenAudioOnHash, v.Handle);
                    if (!sirenOn) continue;

                    // Found emergency vehicle with siren - determine direction
                    Vector3 toEmergency = v.Position - position;
                    float distance = toEmergency.Length();
                    float dot = distance > 0.1f ? Vector3.Dot(Vector3.Normalize(toEmergency), ourForward) : 0f;

                    // Determine if emergency vehicle is approaching from behind, in front, or side
                    string direction;
                    bool isBehind = dot < -0.3f;  // More than 90 degrees behind
                    bool isAhead = dot > 0.3f;   // More than 60 degrees ahead

                    if (isBehind)
                    {
                        // Check if emergency vehicle is moving toward us (closing in from behind)
                        float theirSpeed = v.Speed;
                        float ourSpeed = vehicle.Speed;

                        if (theirSpeed > ourSpeed + 5f)  // They're faster, likely approaching
                        {
                            direction = "approaching from behind";
                        }
                        else
                        {
                            // Behind but not closing - maybe we should continue?
                            // Still yield to be safe, but less urgently
                            direction = "behind";
                        }
                    }
                    else if (isAhead)
                    {
                        direction = "ahead";
                        // Emergency vehicle ahead - slow down but don't need to pull over as much
                    }
                    else
                    {
                        direction = "nearby";
                    }

                    _yieldingToEmergency = true;
                    _emergencyYieldStartTick = currentTick;
                    _emergencyVehiclePosition = v.Position;
                    _emergencyApproachingFromBehind = isBehind;

                    // Slow down and pull over
                    StartYieldToEmergency(vehicle);
                    _announcementQueue.TryAnnounce($"Emergency vehicle {direction}, pulling over",
                        Constants.ANNOUNCE_PRIORITY_CRITICAL, currentTick, "announceEmergencyVehicles");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "EmergencyVehicleHandler.CheckEmergencyVehicles");
            }

            return false;
        }

        /// <summary>
        /// Check if an emergency vehicle with siren is still nearby
        /// </summary>
        public bool IsEmergencyVehicleNearby(Vector3 position)
        {
            try
            {
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(position, Constants.EMERGENCY_DETECTION_RADIUS);
                foreach (Vehicle v in nearbyVehicles)
                {
                    if (!v.Exists()) continue;
                    bool sirenOn = Function.Call<bool>(_sirenAudioOnHash, v.Handle);
                    if (sirenOn) return true;
                }
            }
            catch (Exception ex)
            {
                if (Logger.IsDebugEnabled) Logger.Debug($"IsEmergencyVehicleNearby check failed: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Start yielding to emergency vehicle by stopping
        /// </summary>
        private void StartYieldToEmergency(Vehicle vehicle)
        {
            try
            {
                Ped player = Game.Player.Character;

                // Clear current task and slow down
                Function.Call(_clearPedTasksHash, player.Handle);

                // Apply brakes
                Function.Call(_setHandbrakeHash, vehicle.Handle, true);

                Logger.Info("Yielding to emergency vehicle");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "EmergencyVehicleHandler.StartYieldToEmergency");
            }
        }

        /// <summary>
        /// Release the handbrake after yielding
        /// </summary>
        public void ReleaseHandbrake(Vehicle vehicle)
        {
            try
            {
                Function.Call(_setHandbrakeHash, vehicle.Handle, false);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "EmergencyVehicleHandler.ReleaseHandbrake");
            }
        }

        /// <summary>
        /// Manually end yielding state (e.g., when stopping AutoDrive)
        /// </summary>
        public void EndYield(Vehicle vehicle)
        {
            if (_yieldingToEmergency)
            {
                _yieldingToEmergency = false;
                if (vehicle != null && vehicle.Exists())
                {
                    ReleaseHandbrake(vehicle);
                }
            }
        }
    }
}
