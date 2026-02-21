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

        private readonly AnnouncementQueue _announcementQueue;

        // Time of day state
        private int _lastTimeOfDay;  // 0=day, 1=dawn/dusk, 2=night
        private float _timeSpeedMultiplier = 1.0f;
        private long _lastTimeCheckTick;
        private bool _headlightsOn;

        /// <summary>
        /// Current time-based speed multiplier
        /// </summary>
        public float TimeSpeedMultiplier => _timeSpeedMultiplier;

        public EnvironmentalManager(AnnouncementQueue announcementQueue)
        {
            _announcementQueue = announcementQueue;
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
                                Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceTimeOfDay");
                        }
                        else if (newTimeOfDay == 0 && _timeSpeedMultiplier < 1.0f)
                        {
                            _announcementQueue.TryAnnounce("Daylight, resuming normal speed",
                                Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceTimeOfDay");
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

    }
}
