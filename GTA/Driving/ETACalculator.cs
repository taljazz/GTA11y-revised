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
        #region Fields

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

        #endregion

        #region Properties

        /// <summary>
        /// Current average speed used for ETA calculation
        /// </summary>
        public float AverageSpeed => _averageSpeed;

        /// <summary>
        /// Last announced ETA in seconds
        /// </summary>
        public float LastAnnouncedETA => _lastAnnouncedETA;

        #endregion

        #region Construction

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

        #endregion

        #region ETA Calculation

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
        /// Estimated road distance to the waypoint.
        ///
        /// This used to call GENERATE_DIRECTIONS_TO_COORD, and that call was
        /// THE CRASH that took the game down repeatedly. Two things were wrong
        /// with it, and the first is fatal:
        ///
        /// The native takes SEVEN arguments - a destination x, y, z, an int of
        /// flags, and then three OUTPUT POINTERS. This passed TEN: a source
        /// position AND a destination, then true, then the three outputs. So
        /// everything landed one slot too far along. The game took the
        /// destination's Y and Z - ordinary coordinate floats - as the
        /// addresses of the direction and street-name outputs, and the literal
        /// 1 from `true` as the address of the distance output, then wrote
        /// through all three. Writing to addresses made of map coordinates is
        /// memory corruption, which is why the crash wandered: sometimes those
        /// addresses were unmapped and the game died on the spot, sometimes
        /// they were writable and it died minutes later somewhere unrelated.
        ///
        /// The second problem makes the call pointless even done correctly.
        /// Rockstar's own header says the float it returns is fApproxDistance,
        /// "in the case of a junction being identified" - the distance to the
        /// NEXT JUNCTION, not the distance along the road to the destination.
        /// It was never the number this method wanted.
        ///
        /// So the estimate below is not a fallback any more, it is the answer:
        /// straight-line distance scaled by how much longer roads run than the
        /// crow flies. Roughly right, costs nothing, and cannot corrupt memory.
        /// </summary>
        public float GetRoadDistanceToWaypoint(Vector3 position, Vector3 waypointPos)
        {
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

        #endregion
    }
}
