using System;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages time of day detection, headlights, and environmental speed modifiers.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class EnvironmentalManager
    {
        // PERFORMANCE: Pre-cached Hash values to avoid repeated casting
        private static readonly Hash _setVehicleLightsHash = (Hash)Constants.NATIVE_SET_VEHICLE_LIGHTS;
        private static readonly Hash _setCruiseSpeedHash = (Hash)Constants.NATIVE_SET_DRIVE_TASK_CRUISE_SPEED;

        private readonly AudioManager _audio;
        private readonly AnnouncementQueue _announcementQueue;
        private readonly WeatherManager _weatherManager;

        // Time of day state
        private int _lastTimeOfDay;  // 0=day, 1=dawn/dusk, 2=night
        private float _timeSpeedMultiplier = 1.0f;
        private long _lastTimeCheckTick;
        private bool _headlightsOn;

        /// <summary>
        /// Current time of day state (0=day, 1=dawn/dusk, 2=night)
        /// </summary>
        public int TimeOfDay => _lastTimeOfDay;

        /// <summary>
        /// Current time-based speed multiplier
        /// </summary>
        public float TimeSpeedMultiplier => _timeSpeedMultiplier;

        /// <summary>
        /// Whether headlights are currently on
        /// </summary>
        public bool HeadlightsOn => _headlightsOn;

        public EnvironmentalManager(AudioManager audio, AnnouncementQueue announcementQueue, WeatherManager weatherManager)
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
            _lastTimeOfDay = 0;
            _timeSpeedMultiplier = 1.0f;
            _lastTimeCheckTick = 0;
            _headlightsOn = false;
        }

        /// <summary>
        /// Check time of day and adjust headlights
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="currentTick">Current game tick</param>
        /// <returns>True if time changed, false otherwise</returns>
        public bool CheckTimeOfDay(Vehicle vehicle, long currentTick)
        {
            if (currentTick - _lastTimeCheckTick < Constants.TICK_INTERVAL_TIME_CHECK)
                return false;

            _lastTimeCheckTick = currentTick;

            try
            {
                int hour = World.CurrentTimeOfDay.Hours;
                int newTimeOfDay;
                float newMultiplier;

                if (hour >= Constants.TIME_DAY_START && hour < Constants.TIME_DUSK_START)
                {
                    newTimeOfDay = 0;  // Day
                    newMultiplier = Constants.TIME_SPEED_DAY;
                }
                else if (hour >= Constants.TIME_DAWN_START && hour < Constants.TIME_DAY_START ||
                         hour >= Constants.TIME_DUSK_START && hour < Constants.TIME_NIGHT_START)
                {
                    newTimeOfDay = 1;  // Dawn/Dusk
                    newMultiplier = Constants.TIME_SPEED_DAWN_DUSK;
                }
                else
                {
                    newTimeOfDay = 2;  // Night
                    newMultiplier = Constants.TIME_SPEED_NIGHT;
                }

                // Update headlights
                UpdateHeadlights(vehicle, newTimeOfDay >= 1);

                // Check if time changed
                if (newTimeOfDay != _lastTimeOfDay)
                {
                    _lastTimeOfDay = newTimeOfDay;

                    if (Math.Abs(newMultiplier - _timeSpeedMultiplier) > 0.05f)
                    {
                        _timeSpeedMultiplier = newMultiplier;

                        if (newTimeOfDay == 2)
                        {
                            _announcementQueue.TryAnnounce("Night driving, reducing speed",
                                Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceWeather");
                        }
                        else if (newTimeOfDay == 0 && _timeSpeedMultiplier < 1.0f)
                        {
                            _announcementQueue.TryAnnounce("Daylight, resuming normal speed",
                                Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceWeather");
                        }

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "EnvironmentalManager.CheckTimeOfDay");
            }

            return false;
        }

        /// <summary>
        /// Update vehicle headlights based on time of day
        /// </summary>
        private void UpdateHeadlights(Vehicle vehicle, bool shouldHaveHeadlights)
        {
            if (vehicle == null || !vehicle.Exists())
                return;

            if (shouldHaveHeadlights != _headlightsOn)
            {
                _headlightsOn = shouldHaveHeadlights;
                // 0 = off, 1 = low, 2 = high
                try
                {
                    Function.Call(_setVehicleLightsHash,
                        vehicle.Handle, shouldHaveHeadlights ? 2 : 0);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "EnvironmentalManager.UpdateHeadlights");
                }
            }
        }

        /// <summary>
        /// Calculate the combined environmental speed multiplier
        /// </summary>
        /// <param name="roadTypeSpeedMultiplier">Road type multiplier from RoadTypeManager</param>
        /// <returns>Combined multiplier (road * weather * time)</returns>
        public float GetCombinedSpeedMultiplier(float roadTypeSpeedMultiplier)
        {
            float weatherMultiplier = _weatherManager?.SpeedMultiplier ?? 1.0f;
            return roadTypeSpeedMultiplier * weatherMultiplier * _timeSpeedMultiplier;
        }

        /// <summary>
        /// Apply combined environmental speed modifiers to driving task
        /// </summary>
        /// <param name="targetSpeed">Base target speed</param>
        /// <param name="roadTypeSpeedMultiplier">Road type multiplier</param>
        /// <param name="curveSlowdownActive">Whether curve slowdown is active</param>
        /// <param name="arrivalSlowdownActive">Whether arrival slowdown is active</param>
        /// <param name="pauseState">Current pause state</param>
        public void ApplyEnvironmentalSpeedModifiers(float targetSpeed, float roadTypeSpeedMultiplier,
            bool curveSlowdownActive, bool arrivalSlowdownActive, int pauseState)
        {
            if (curveSlowdownActive || arrivalSlowdownActive || pauseState != Constants.PAUSE_STATE_NONE)
                return;

            try
            {
                float combinedMultiplier = GetCombinedSpeedMultiplier(roadTypeSpeedMultiplier);
                float adjustedSpeed = targetSpeed * combinedMultiplier;

                Ped player = Game.Player.Character;
                if (player != null && player.IsInVehicle())
                {
                    Function.Call(
                        _setCruiseSpeedHash,
                        player.Handle,
                        adjustedSpeed);

                    if (Logger.IsDebugEnabled) Logger.Debug($"Environmental speed: road={roadTypeSpeedMultiplier:P0}, " +
                        $"weather={_weatherManager?.SpeedMultiplier ?? 1.0f:P0}, " +
                        $"time={_timeSpeedMultiplier:P0}, final={adjustedSpeed:F1} m/s");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "EnvironmentalManager.ApplyEnvironmentalSpeedModifiers");
            }
        }

        /// <summary>
        /// Get effective cruise speed considering all modifiers
        /// </summary>
        /// <param name="targetSpeed">Base target speed</param>
        /// <param name="roadTypeSpeedMultiplier">Road type multiplier</param>
        /// <param name="curveSlowdownActive">Whether curve slowdown is active</param>
        /// <param name="curveSlowdownSpeed">Speed during curve slowdown</param>
        /// <returns>Effective speed to use</returns>
        public float GetEffectiveCruiseSpeed(float targetSpeed, float roadTypeSpeedMultiplier,
            bool curveSlowdownActive, float curveSlowdownSpeed)
        {
            float speed = targetSpeed;

            // Apply combined environmental modifiers
            speed *= GetCombinedSpeedMultiplier(roadTypeSpeedMultiplier);

            // Curve slowdown takes precedence
            if (curveSlowdownActive)
            {
                speed = curveSlowdownSpeed;
            }

            return speed;
        }

        /// <summary>
        /// Get time of day description for announcements
        /// </summary>
        public string GetTimeOfDayDescription()
        {
            switch (_lastTimeOfDay)
            {
                case 0: return "Day";
                case 1: return "Dawn/Dusk";
                case 2: return "Night";
                default: return "Unknown";
            }
        }
    }
}
