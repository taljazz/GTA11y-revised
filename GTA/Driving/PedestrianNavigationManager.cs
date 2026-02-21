using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// On-foot waypoint navigation system for blind players.
    /// Provides periodic spoken direction and distance announcements
    /// to guide a player walking toward their map waypoint.
    /// </summary>
    public class PedestrianNavigationManager
    {
        // PERFORMANCE: Pre-cached native hashes
        private static readonly Hash _isWaypointActiveHash = Hash.IS_WAYPOINT_ACTIVE;
        private static readonly Hash _getClosestNodeHash = (Hash)Constants.NATIVE_GET_CLOSEST_VEHICLE_NODE;

        // Dependencies
        private readonly AudioManager _audio;
        private readonly SettingsManager _settings;

        // Navigation state
        private bool _isActive;
        private Vector3 _waypointPos;
        private float _lastAnnouncedDistance;
        private long _lastDirectionAnnounceTick;
        private long _lastBeaconPulseTick;

        // Direction announcement interval (4 seconds in .NET ticks)
        private const long DIRECTION_ANNOUNCE_INTERVAL = 40_000_000;

        // Arrival threshold in meters
        private const float ARRIVAL_DISTANCE = 5f;

        // Distance milestone thresholds (meters)
        private const float MILESTONE_FAR = 500f;
        private const float MILESTONE_MEDIUM = 100f;
        private const float MILESTONE_CLOSE = 25f;

        // Distance milestone intervals (meters)
        private const float MILESTONE_INTERVAL_FAR = 100f;
        private const float MILESTONE_INTERVAL_MEDIUM = 50f;
        private const float MILESTONE_INTERVAL_CLOSE = 25f;
        private const float MILESTONE_INTERVAL_VERY_CLOSE = 5f;

        // Road crossing detection distance (meters)
        private const float ROAD_CROSSING_LOOKAHEAD = 15f;
        private long _lastRoadCrossingAnnounceTick;
        private const long ROAD_CROSSING_COOLDOWN = 100_000_000; // 10 seconds

        // Pre-allocated OutputArgument to avoid per-call allocation
        private readonly OutputArgument _roadNodeArg = new OutputArgument();

        // Beacon audio parameters
        private const float BEACON_FREQUENCY = 500f;
        private const float BEACON_GAIN = 1.0f;
        private const long BEACON_PULSE_FAR = 10_000_000;       // 1.0 second when far
        private const long BEACON_PULSE_MEDIUM = 7_000_000;     // 0.7 seconds
        private const long BEACON_PULSE_CLOSE = 4_000_000;      // 0.4 seconds
        private const long BEACON_PULSE_VERY_CLOSE = 2_000_000; // 0.2 seconds

        /// <summary>
        /// Whether pedestrian navigation is currently active.
        /// </summary>
        public bool IsActive => _isActive;

        public PedestrianNavigationManager(AudioManager audio, SettingsManager settings)
        {
            _audio = audio;
            _settings = settings;
        }

        /// <summary>
        /// Start pedestrian navigation toward the current map waypoint.
        /// </summary>
        public void StartNavigation()
        {
            // Check if a waypoint is set
            bool waypointActive = Function.Call<bool>(_isWaypointActiveHash);
            if (!waypointActive)
            {
                _audio.Speak("No waypoint set");
                return;
            }

            _waypointPos = World.WaypointPosition;
            _isActive = true;
            _lastAnnouncedDistance = float.MaxValue;
            _lastDirectionAnnounceTick = 0;
            _lastRoadCrossingAnnounceTick = 0;
            _lastBeaconPulseTick = 0;

            float distance = (_waypointPos - Game.Player.Character.Position).Length();
            string distText = FormatDistance(distance);
            _audio.Speak($"Pedestrian navigation started. Destination is {distText} away.");

            Logger.Info($"PedestrianNavigationManager: Started, distance={distance:F0}m, waypoint={_waypointPos}");
        }

        /// <summary>
        /// Stop pedestrian navigation.
        /// </summary>
        public void StopNavigation()
        {
            if (!_isActive) return;

            _isActive = false;
            _audio.StopBeacon();
            _audio.Speak("Pedestrian navigation stopped.");
            Logger.Info("PedestrianNavigationManager: Stopped");
        }

        /// <summary>
        /// Main update loop - call from OnTick when navigation is active.
        /// </summary>
        /// <param name="player">The player ped</param>
        /// <param name="playerPos">Current player position</param>
        /// <param name="currentTick">Current tick (DateTime.Now.Ticks)</param>
        public void Update(Ped player, Vector3 playerPos, long currentTick)
        {
            if (!_isActive) return;

            try
            {
                // Check if waypoint was removed
                bool waypointActive = Function.Call<bool>(_isWaypointActiveHash);
                if (!waypointActive)
                {
                    _isActive = false;
                    _audio.StopBeacon();
                    _audio.Speak("Waypoint removed. Navigation stopped.");
                    Logger.Info("PedestrianNavigationManager: Waypoint removed");
                    return;
                }

                // Detect if waypoint moved (use LengthSquared to avoid sqrt)
                Vector3 currentWaypoint = World.WaypointPosition;
                Vector3 waypointDelta = currentWaypoint - _waypointPos;
                if (waypointDelta.LengthSquared() > Constants.WAYPOINT_MOVED_THRESHOLD * Constants.WAYPOINT_MOVED_THRESHOLD)
                {
                    _waypointPos = currentWaypoint;
                    _lastAnnouncedDistance = float.MaxValue;
                    float newDist = (_waypointPos - playerPos).Length();
                    _audio.Speak($"Waypoint moved. New destination is {FormatDistance(newDist)} away.");
                    if (Logger.IsDebugEnabled) Logger.Debug($"PedestrianNavigationManager: Waypoint moved to {_waypointPos}");
                    return;
                }

                float distance = (_waypointPos - playerPos).Length();

                // Check for arrival
                if (distance < ARRIVAL_DISTANCE)
                {
                    _isActive = false;
                    _audio.StopBeacon();
                    _audio.Speak("Arrived at destination.");
                    Logger.Info("PedestrianNavigationManager: Arrived");
                    return;
                }

                // Directional beacon pulse (panned audio)
                UpdateBeacon(player, playerPos, distance, currentTick);

                // Distance milestone announcements
                CheckDistanceMilestones(distance, playerPos, currentTick);

                // Periodic direction announcements
                if (currentTick - _lastDirectionAnnounceTick > DIRECTION_ANNOUNCE_INTERVAL)
                {
                    _lastDirectionAnnounceTick = currentTick;
                    AnnounceDirection(player, playerPos, distance);
                }

                // Road crossing detection
                if (currentTick - _lastRoadCrossingAnnounceTick > ROAD_CROSSING_COOLDOWN)
                {
                    CheckRoadCrossing(player, playerPos, currentTick);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PedestrianNavigationManager.Update");
            }
        }

        /// <summary>
        /// Announce the relative direction and distance to the waypoint.
        /// Uses the player's current heading vs. bearing to waypoint.
        /// </summary>
        private void AnnounceDirection(Ped player, Vector3 playerPos, float distance)
        {
            float bearing = CalculateBearing(playerPos, _waypointPos);
            float playerHeading = player.Heading;

            // Relative angle: how far the player needs to turn
            // GTA heading: 0=North, increases clockwise
            // Bearing: same convention from CalculateBearing
            float diff = bearing - playerHeading;

            // Normalize to -180..180
            if (diff > 180f) diff -= 360f;
            if (diff < -180f) diff += 360f;

            string direction = GetRelativeDirection(diff);
            string distText = FormatDistance(distance);

            _audio.Speak($"{direction}, {distText}");
        }

        /// <summary>
        /// Calculate compass bearing from one point to another.
        /// Returns degrees 0-360 where 0=North, 90=East (in GTA coordinate space).
        /// </summary>
        private static float CalculateBearing(Vector3 from, Vector3 to)
        {
            // GTA coordinate system: X increases East, Y increases North
            // atan2(deltaX, deltaY) gives bearing from North, clockwise
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float bearing = (float)Math.Atan2(dx, dy) * Constants.RAD_TO_DEG;
            if (bearing < 0f) bearing += 360f;
            return bearing;
        }

        /// <summary>
        /// Convert a relative angle (-180 to 180) to a human-readable direction string.
        /// Positive = target is to the right, negative = to the left.
        /// </summary>
        private static string GetRelativeDirection(float diff)
        {
            float absDiff = Math.Abs(diff);

            if (absDiff < 15f)
                return "Straight ahead";
            if (absDiff < 45f)
                return diff > 0 ? "Slightly right" : "Slightly left";
            if (absDiff < 135f)
                return diff > 0 ? "Turn right" : "Turn left";

            // 135-180 degrees off
            return "Turn around";
        }

        /// <summary>
        /// Check and announce distance milestones based on adaptive intervals.
        /// </summary>
        private void CheckDistanceMilestones(float distance, Vector3 playerPos, long currentTick)
        {
            float interval;
            if (distance > MILESTONE_FAR)
                interval = MILESTONE_INTERVAL_FAR;
            else if (distance > MILESTONE_MEDIUM)
                interval = MILESTONE_INTERVAL_MEDIUM;
            else if (distance > MILESTONE_CLOSE)
                interval = MILESTONE_INTERVAL_CLOSE;
            else
                interval = MILESTONE_INTERVAL_VERY_CLOSE;

            // Calculate which milestone bucket we're in
            float currentMilestone = (float)Math.Floor(distance / interval) * interval;
            float lastMilestone = (float)Math.Floor(_lastAnnouncedDistance / interval) * interval;

            // Announce when we cross into a new milestone (getting closer)
            if (currentMilestone < lastMilestone && _lastAnnouncedDistance < float.MaxValue)
            {
                string bearing = SpatialCalculator.GetDirectionTo(playerPos, _waypointPos);
                string distText = FormatDistance(distance);
                _audio.Speak($"{distText}, {bearing}");
                _lastDirectionAnnounceTick = currentTick; // Reset direction timer to avoid double-speak
            }

            _lastAnnouncedDistance = distance;
        }

        /// <summary>
        /// Check if the player is approaching a road crossing in the direction of the waypoint.
        /// Uses GET_CLOSEST_VEHICLE_NODE to detect nearby road nodes.
        /// </summary>
        private void CheckRoadCrossing(Ped player, Vector3 playerPos, long currentTick)
        {
            try
            {
                // Project a point ahead of the player in the direction they're walking
                float headingRad = player.Heading * Constants.DEG_TO_RAD;
                Vector3 lookAhead = new Vector3(
                    playerPos.X + (float)Math.Sin(headingRad) * ROAD_CROSSING_LOOKAHEAD,
                    playerPos.Y + (float)Math.Cos(headingRad) * ROAD_CROSSING_LOOKAHEAD,
                    playerPos.Z
                );

                bool foundNode = Function.Call<bool>(
                    _getClosestNodeHash,
                    lookAhead.X, lookAhead.Y, lookAhead.Z,
                    _roadNodeArg,
                    Constants.NODE_FLAG_ACTIVE_NODES_ONLY,
                    Constants.ROAD_NODE_SEARCH_CONNECTION_DISTANCE,
                    0);

                if (foundNode)
                {
                    Vector3 roadNode = _roadNodeArg.GetResult<Vector3>();
                    float distToRoad = (roadNode - playerPos).Length();

                    // Road node is close and roughly ahead of the player
                    if (distToRoad < ROAD_CROSSING_LOOKAHEAD && distToRoad > 3f)
                    {
                        _lastRoadCrossingAnnounceTick = currentTick;
                        _audio.Speak("Road ahead");
                        if (Logger.IsDebugEnabled) Logger.Debug($"PedestrianNavigationManager: Road detected {distToRoad:F0}m ahead");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PedestrianNavigationManager.CheckRoadCrossing");
            }
        }

        /// <summary>
        /// Play a directional audio beacon that pans left/right based on
        /// the relative angle to the waypoint. Pulses faster as the player gets closer.
        /// </summary>
        private void UpdateBeacon(Ped player, Vector3 playerPos, float distance, long currentTick)
        {
            // Determine pulse rate based on distance
            long pulseInterval;
            if (distance > MILESTONE_FAR)
                pulseInterval = BEACON_PULSE_FAR;
            else if (distance > MILESTONE_MEDIUM)
                pulseInterval = BEACON_PULSE_MEDIUM;
            else if (distance > MILESTONE_CLOSE)
                pulseInterval = BEACON_PULSE_CLOSE;
            else
                pulseInterval = BEACON_PULSE_VERY_CLOSE;

            if (currentTick - _lastBeaconPulseTick < pulseInterval)
                return;

            _lastBeaconPulseTick = currentTick;

            // Calculate relative angle for panning
            float bearing = CalculateBearing(playerPos, _waypointPos);
            float playerHeading = player.Heading;
            float diff = bearing - playerHeading;
            if (diff > 180f) diff -= 360f;
            if (diff < -180f) diff += 360f;

            // Convert angle to pan: -1 (left) to +1 (right)
            // Dead zone of 5 degrees = centered
            float absDiff = Math.Abs(diff);
            float pan;
            if (absDiff < Constants.BEACON_PAN_DEAD_ZONE)
            {
                pan = 0f;
            }
            else if (absDiff > Constants.BEACON_PAN_MAX_ANGLE)
            {
                pan = diff > 0 ? 1f : -1f;
            }
            else
            {
                pan = (absDiff - Constants.BEACON_PAN_DEAD_ZONE) * Constants.BEACON_PAN_RANGE_INV;
                if (diff < 0) pan = -pan;
            }

            // Reduce gain when the destination is behind the player (>120 degrees off)
            float gainMult = absDiff > 120f ? Constants.BEACON_BEHIND_GAIN_FACTOR : BEACON_GAIN;

            _audio.PlayBeaconPulse(pan, BEACON_FREQUENCY, gainMult);
        }

        /// <summary>
        /// Format a distance in meters to a human-readable string using imperial units.
        /// </summary>
        private static string FormatDistance(float distanceMeters)
        {
            float miles = distanceMeters * Constants.METERS_TO_MILES;

            if (miles < 0.1f)
            {
                int feet = (int)(distanceMeters * Constants.METERS_TO_FEET);
                return $"{feet} feet";
            }

            return $"{miles:F1} miles";
        }
    }
}
