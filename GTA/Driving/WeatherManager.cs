using System;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages weather detection and provides speed multipliers for driving conditions.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class WeatherManager
    {
        private int _currentWeatherHash;
        private float _weatherSpeedMultiplier = 1.0f;
        private long _lastWeatherCheckTick;
        private bool _weatherAnnounced;

        /// <summary>
        /// Current weather hash value
        /// </summary>
        public int CurrentWeatherHash => _currentWeatherHash;

        /// <summary>
        /// Current speed multiplier based on weather conditions
        /// </summary>
        public float SpeedMultiplier => _weatherSpeedMultiplier;

        /// <summary>
        /// Whether a weather change has been announced
        /// </summary>
        public bool WeatherAnnounced => _weatherAnnounced;

        /// <summary>
        /// Check weather conditions and update speed multiplier.
        /// Returns true if weather changed significantly.
        /// </summary>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="weatherName">Output: human-readable weather name if changed</param>
        /// <returns>True if weather changed and announcement may be needed</returns>
        public bool Update(long currentTick, out string weatherName, out bool shouldAnnounce)
        {
            weatherName = null;
            shouldAnnounce = false;

            // Guard against invalid tick values
            if (currentTick < 0)
                return false;

            // Throttle checks
            if (currentTick - _lastWeatherCheckTick < Constants.TICK_INTERVAL_WEATHER_CHECK)
                return false;

            _lastWeatherCheckTick = currentTick;

            try
            {
                // Get current weather hash - wrapped in try/catch as native calls can fail
                int weatherHash = Function.Call<int>((Hash)Constants.NATIVE_GET_PREV_WEATHER_TYPE_HASH_NAME);

                if (weatherHash == _currentWeatherHash)
                    return false;

                _currentWeatherHash = weatherHash;
                float newMultiplier = GetWeatherSpeedMultiplier(weatherHash);

                // Only announce and adjust if multiplier changed significantly
                if (Math.Abs(newMultiplier - _weatherSpeedMultiplier) > 0.05f)
                {
                    _weatherSpeedMultiplier = newMultiplier;
                    weatherName = GetWeatherName(weatherHash);

                    if (newMultiplier < 0.9f)
                    {
                        shouldAnnounce = true;
                        _weatherAnnounced = true;
                    }
                    else if (_weatherAnnounced && newMultiplier >= 0.95f)
                    {
                        shouldAnnounce = true;
                        _weatherAnnounced = false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeatherManager.Update");
            }

            return false;
        }

        /// <summary>
        /// Get speed multiplier for weather type
        /// </summary>
        public float GetWeatherSpeedMultiplier(int weatherHash)
        {
            // Using unchecked to handle potential overflow with hash comparison
            unchecked
            {
                if (weatherHash == Constants.WEATHER_CLEAR || weatherHash == Constants.WEATHER_EXTRASUNNY)
                    return Constants.WEATHER_SPEED_CLEAR;
                if (weatherHash == Constants.WEATHER_CLOUDS || weatherHash == Constants.WEATHER_CLEARING)
                    return Constants.WEATHER_SPEED_CLOUDS;
                if (weatherHash == Constants.WEATHER_OVERCAST || weatherHash == Constants.WEATHER_SMOG)
                    return Constants.WEATHER_SPEED_OVERCAST;
                if (weatherHash == Constants.WEATHER_RAIN)
                    return Constants.WEATHER_SPEED_RAIN;
                if (weatherHash == Constants.WEATHER_THUNDER)
                    return Constants.WEATHER_SPEED_THUNDER;
                if (weatherHash == Constants.WEATHER_FOGGY)
                    return Constants.WEATHER_SPEED_FOGGY;
                if (weatherHash == Constants.WEATHER_XMAS || weatherHash == Constants.WEATHER_SNOWLIGHT)
                    return Constants.WEATHER_SPEED_SNOW;
                if (weatherHash == Constants.WEATHER_BLIZZARD)
                    return Constants.WEATHER_SPEED_BLIZZARD;
            }
            return 1.0f;  // Default - unknown weather
        }

        /// <summary>
        /// Get readable weather name
        /// </summary>
        public string GetWeatherName(int weatherHash)
        {
            unchecked
            {
                if (weatherHash == Constants.WEATHER_CLEAR || weatherHash == Constants.WEATHER_EXTRASUNNY)
                    return "Clear";
                if (weatherHash == Constants.WEATHER_CLOUDS)
                    return "Cloudy";
                if (weatherHash == Constants.WEATHER_OVERCAST)
                    return "Overcast";
                if (weatherHash == Constants.WEATHER_RAIN)
                    return "Rainy";
                if (weatherHash == Constants.WEATHER_CLEARING)
                    return "Clearing";
                if (weatherHash == Constants.WEATHER_THUNDER)
                    return "Stormy";
                if (weatherHash == Constants.WEATHER_SMOG)
                    return "Smoggy";
                if (weatherHash == Constants.WEATHER_FOGGY)
                    return "Foggy";
                if (weatherHash == Constants.WEATHER_XMAS || weatherHash == Constants.WEATHER_SNOWLIGHT)
                    return "Snowy";
                if (weatherHash == Constants.WEATHER_BLIZZARD)
                    return "Blizzard";
            }
            return "Unknown";
        }

        /// <summary>
        /// Get current weather name
        /// </summary>
        public string GetCurrentWeatherName()
        {
            return GetWeatherName(_currentWeatherHash);
        }

        /// <summary>
        /// Get road friction coefficient based on current weather conditions.
        /// Used for calculating safe speeds in curves.
        /// </summary>
        public float GetRoadFrictionCoefficient()
        {
            // Base friction for dry asphalt
            float friction = 0.8f;

            // Weather adjustments
            unchecked
            {
                if (_currentWeatherHash == Constants.WEATHER_RAIN ||
                    _currentWeatherHash == Constants.WEATHER_CLEARING)
                {
                    friction *= 0.7f; // Wet roads
                }
                else if (_currentWeatherHash == Constants.WEATHER_THUNDER)
                {
                    friction *= 0.6f; // Heavy rain
                }
                else if (_currentWeatherHash == Constants.WEATHER_SNOW ||
                         _currentWeatherHash == Constants.WEATHER_SNOWLIGHT)
                {
                    friction *= 0.3f; // Snow/ice
                }
                else if (_currentWeatherHash == Constants.WEATHER_BLIZZARD)
                {
                    friction *= 0.2f; // Blizzard conditions
                }
            }

            return friction;
        }

        /// <summary>
        /// Reset all weather state
        /// </summary>
        public void Reset()
        {
            _currentWeatherHash = 0;
            _weatherSpeedMultiplier = 1.0f;
            _lastWeatherCheckTick = 0;
            _weatherAnnounced = false;
        }
    }
}
