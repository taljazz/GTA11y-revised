using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Calculates and announces ETA to waypoint.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class ETACalculator
    {
        // PERFORMANCE: Pre-cached Hash value to avoid repeated casting
        private static readonly Hash _generateDirectionsHash = (Hash)Constants.NATIVE_GENERATE_DIRECTIONS_TO_COORD;

        private readonly AudioManager _audio;
        private readonly AnnouncementQueue _announcementQueue;

        // ETA tracking
        private float _lastAnnouncedETA;  // seconds
        private long _lastETAAnnounceTick;
        private float[] _speedSamples;
        private int _speedSampleIndex;
        private int _validSampleCount;     // OPTIMIZED: Track valid samples to avoid iterating whole array
        private float _runningSpeedTotal;  // OPTIMIZED: Running total for O(1) average calculation
        private float _averageSpeed;

        // Pre-allocated OutputArguments
        private readonly OutputArgument _roadDistanceArg = new OutputArgument();
        private readonly OutputArgument _roadDirectionArg1 = new OutputArgument();
        private readonly OutputArgument _roadDirectionArg2 = new OutputArgument();

        /// <summary>
        /// Current average speed used for ETA calculation
        /// </summary>
        public float AverageSpeed => _averageSpeed;

        /// <summary>
        /// Last announced ETA in seconds
        /// </summary>
        public float LastAnnouncedETA => _lastAnnouncedETA;

        public ETACalculator(AudioManager audio, AnnouncementQueue announcementQueue)
        {
            _audio = audio;
            _announcementQueue = announcementQueue;
            _speedSamples = new float[Constants.ETA_SPEED_SAMPLES];
            Reset();
        }

        /// <summary>
        /// Reset all state
        /// </summary>
        public void Reset()
        {
            _lastAnnouncedETA = 0f;
            _lastETAAnnounceTick = 0;
            _speedSampleIndex = 0;
            _validSampleCount = 0;
            _runningSpeedTotal = 0f;
            _averageSpeed = 0f;
            if (_speedSamples != null)
            {
                Array.Clear(_speedSamples, 0, _speedSamples.Length);
            }
        }

        /// <summary>
        /// Update and announce ETA to waypoint
        /// Uses GENERATE_DIRECTIONS_TO_COORD for accurate road distance estimation
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="position">Current position</param>
        /// <param name="waypointPos">Waypoint position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="wanderMode">Whether in wander mode (no ETA in wander)</param>
        public void UpdateETA(Vehicle vehicle, Vector3 position, Vector3 waypointPos,
            long currentTick, bool wanderMode)
        {
            if (vehicle == null || !vehicle.Exists())
                return;

            if (wanderMode) return;  // No ETA in wander mode

            // OPTIMIZED: Update speed sample using running total (O(1) instead of O(n))
            float currentSpeed = vehicle.Speed;

            // Subtract old value from running total before overwriting
            float oldValue = _speedSamples[_speedSampleIndex];
            if (oldValue > 0)
            {
                _runningSpeedTotal -= oldValue;
            }
            else if (_validSampleCount < Constants.ETA_SPEED_SAMPLES)
            {
                // This is a new slot being filled
                _validSampleCount++;
            }

            // Add new value
            _speedSamples[_speedSampleIndex] = currentSpeed;
            _runningSpeedTotal += currentSpeed;
            _speedSampleIndex = (_speedSampleIndex + 1) % Constants.ETA_SPEED_SAMPLES;

            // Calculate average speed - O(1) using running total
            _averageSpeed = _validSampleCount > 0 ? _runningSpeedTotal / _validSampleCount : currentSpeed;

            // Throttle ETA announcements
            if (currentTick - _lastETAAnnounceTick < Constants.TICK_INTERVAL_ETA_UPDATE)
                return;

            // Calculate road distance using GENERATE_DIRECTIONS_TO_COORD
            float roadDistance = GetRoadDistanceToWaypoint(position, waypointPos);
            if (roadDistance < Constants.ETA_MIN_DISTANCE_FOR_ANNOUNCE)
                return;

            // Calculate ETA in seconds using road distance
            float etaSeconds = _averageSpeed > 1f ? roadDistance / _averageSpeed : float.MaxValue;

            // Check if ETA changed significantly
            float etaChange = Math.Abs(etaSeconds - _lastAnnouncedETA);
            if (etaChange < Constants.ETA_ANNOUNCE_CHANGE_THRESHOLD && _lastAnnouncedETA > 0)
                return;

            _lastETAAnnounceTick = currentTick;
            _lastAnnouncedETA = etaSeconds;

            // Format and announce ETA
            string etaText = FormatETA(etaSeconds);
            _announcementQueue.TryAnnounce($"Estimated arrival in {etaText}",
                Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceNavigation");
        }

        /// <summary>
        /// Get estimated road distance to waypoint using GENERATE_DIRECTIONS_TO_COORD
        /// Falls back to straight-line distance with road factor if native fails
        /// </summary>
        public float GetRoadDistanceToWaypoint(Vector3 position, Vector3 waypointPos)
        {
            try
            {
                // Use GENERATE_DIRECTIONS_TO_COORD to get road-aware distance estimate
                int result = Function.Call<int>(
                    _generateDirectionsHash,
                    position.X, position.Y, position.Z,
                    waypointPos.X, waypointPos.Y, waypointPos.Z,
                    true,                   // p6: unknown, true seems standard
                    _roadDirectionArg1,     // direction - not needed but must be passed
                    _roadDirectionArg2,     // p8 - not needed but must be passed
                    _roadDistanceArg);

                if (result != 0) // 0 = failed, other values = success
                {
                    float roadDist = _roadDistanceArg.GetResult<float>();
                    if (roadDist > 0 && roadDist < Constants.ROAD_DISTANCE_SANITY_MAX)
                    {
                        return roadDist;
                    }
                }
            }
            catch (Exception ex)
            {
                if (Logger.IsDebugEnabled) Logger.Debug($"GENERATE_DIRECTIONS_TO_COORD failed: {ex.Message}");
            }

            // Fallback: straight-line distance with road factor (roads are ~1.4x longer)
            float straightLine = (waypointPos - position).Length();
            return straightLine * Constants.ROAD_DISTANCE_FACTOR;
        }

        /// <summary>
        /// Format ETA for speech
        /// </summary>
        public static string FormatETA(float seconds)
        {
            if (seconds < 60)
            {
                return "less than a minute";
            }
            else if (seconds < 3600)
            {
                int minutes = (int)(seconds / 60);
                return minutes == 1 ? "1 minute" : $"{minutes} minutes";
            }
            else
            {
                int hours = (int)(seconds / 3600);
                int minutes = (int)((seconds % 3600) / 60);
                if (minutes == 0)
                    return hours == 1 ? "1 hour" : $"{hours} hours";
                return hours == 1 ? $"1 hour {minutes} minutes" : $"{hours} hours {minutes} minutes";
            }
        }

    }
}
