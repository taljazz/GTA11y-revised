using System;
using System.Collections.Generic;
using System.Text;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Provides audio alternatives to the minimap/radar by announcing nearby blips,
    /// mission objectives, and police presence via TTS.
    /// </summary>
    public class BlipManager
    {
        // PERFORMANCE: Pre-cached Hash values to avoid repeated casting
        private static readonly Hash _getFirstBlipInfoId = Hash.GET_FIRST_BLIP_INFO_ID;
        private static readonly Hash _getNextBlipInfoId = Hash.GET_NEXT_BLIP_INFO_ID;
        private static readonly Hash _doesBlipExist = Hash.DOES_BLIP_EXIST;
        private static readonly Hash _getBlipInfoIdCoord = Hash.GET_BLIP_INFO_ID_COORD;
        private static readonly Hash _isWaypointActive = Hash.IS_WAYPOINT_ACTIVE;

        private readonly AudioManager _audio;
        private readonly SettingsManager _settings;
        private readonly StringBuilder _resultBuilder;

        // Reusable list for blip scan results to avoid per-call allocation
        private readonly List<BlipResult> _blipResults;

        // Police awareness state
        private int _lastWantedLevel;

        // Throttle for police announcements
        private long _lastPoliceAnnounceTick;
        private const long POLICE_ANNOUNCE_INTERVAL = 50_000_000; // 5 seconds

        // Maximum nearby blips to announce
        private const int MAX_NEARBY_BLIPS = 5;
        private const float NEARBY_BLIP_RADIUS = 500f;

        // Common blip sprite IDs mapped to friendly names
        private static readonly Dictionary<int, string> BlipSpriteNames = new Dictionary<int, string>
        {
            { 1,   "destination" },
            { 2,   "destination" },
            { 3,   "destination" },
            { 6,   "police station" },
            { 38,  "destination flag" },
            { 40,  "helipad" },
            { 60,  "Ammu-Nation" },
            { 61,  "barber" },
            { 63,  "hospital" },
            { 72,  "store" },
            { 73,  "golf" },
            { 75,  "clothes store" },
            { 78,  "cinema" },
            { 84,  "tattoo parlor" },
            { 89,  "bar" },
            { 90,  "mission" },
            { 93,  "triathlon" },
            { 106, "LS Customs" },
            { 108, "flight school" },
            { 109, "property" },
            { 110, "Franklin" },
            { 111, "Trevor" },
            { 112, "Michael" },
            { 143, "objective" },
            { 164, "mission" },
            { 225, "pickup" },
            { 227, "dropoff" },
            { 280, "mission" },
            { 304, "taxi destination" },
            { 309, "mission marker" },
            { 357, "car meet" },
            { 380, "mission area" },
            { 417, "mission marker" },
            { 478, "mission destination" },
            { 480, "mission pickup" },
        };

        // Sprite IDs to scan for nearby blips (points of interest)
        private static readonly int[] NearbyBlipSprites = new int[]
        {
            1, 2, 3, 6, 38, 40, 60, 61, 63, 72, 73, 75, 78, 84, 89,
            90, 93, 106, 108, 109, 110, 111, 112, 143, 164, 225, 227,
            280, 304, 309, 357, 380, 417, 478, 480
        };

        // Mission-specific sprite IDs (subset for mission blip tracking)
        private static readonly int[] MissionBlipSprites = new int[]
        {
            1, 2, 3, 38, 40, 90, 143, 225, 227, 280, 304, 309, 380, 417, 478, 480
        };

        public BlipManager(AudioManager audio, SettingsManager settings)
        {
            _audio = audio;
            _settings = settings;
            _resultBuilder = new StringBuilder(512);
            _blipResults = new List<BlipResult>(32);
            _lastWantedLevel = 0;
            _lastPoliceAnnounceTick = 0;
        }

        /// <summary>
        /// Announce nearby blips sorted by distance (top 5 within 500m)
        /// </summary>
        public void AnnounceNearbyBlips(Vector3 playerPos)
        {
            if (!_settings.GetSetting("announceBlips"))
                return;

            _blipResults.Clear();

            try
            {
                foreach (int sprite in NearbyBlipSprites)
                {
                    int blipHandle = Function.Call<int>(_getFirstBlipInfoId, sprite);

                    while (Function.Call<bool>(_doesBlipExist, blipHandle))
                    {
                        Vector3 blipPos = Function.Call<Vector3>(_getBlipInfoIdCoord, blipHandle);
                        double distance = SpatialCalculator.GetHorizontalDistance(playerPos, blipPos);

                        if (distance <= NEARBY_BLIP_RADIUS && distance > 5.0)
                        {
                            string direction = SpatialCalculator.GetDirectionTo(playerPos, blipPos);
                            string name = GetBlipName(sprite);
                            _blipResults.Add(new BlipResult(name, distance, direction));
                        }

                        blipHandle = Function.Call<int>(_getNextBlipInfoId, sprite);
                    }
                }

                // Sort by distance
                _blipResults.Sort((a, b) => a.Distance.CompareTo(b.Distance));

                // Build announcement
                _resultBuilder.Clear();

                if (_blipResults.Count == 0)
                {
                    _audio.Speak("No blips nearby.");
                    return;
                }

                int count = Math.Min(_blipResults.Count, MAX_NEARBY_BLIPS);
                _resultBuilder.Append("Nearby: ");

                for (int i = 0; i < count; i++)
                {
                    BlipResult result = _blipResults[i];
                    _resultBuilder.Append(result.Name)
                        .Append(", ")
                        .Append(Math.Round(result.Distance))
                        .Append(" meters ")
                        .Append(result.Direction)
                        .Append(". ");
                }

                _audio.Speak(_resultBuilder.ToString());
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AnnounceNearbyBlips");
            }
        }

        /// <summary>
        /// Find and announce the closest active mission blip
        /// </summary>
        public void AnnounceMissionBlip(Vector3 playerPos)
        {
            try
            {
                float closestDistance = float.MaxValue;
                Vector3 closestPos = Vector3.Zero;
                int closestSprite = -1;
                bool found = false;

                // Hoist waypoint check outside loop - state doesn't change during scan
                bool hasWaypoint = Function.Call<bool>(_isWaypointActive);
                Vector3 waypointPos = Vector3.Zero;
                if (hasWaypoint)
                {
                    Blip waypoint = World.WaypointBlip;
                    if (waypoint != null)
                        waypointPos = waypoint.Position;
                    else
                        hasWaypoint = false;
                }

                foreach (int sprite in MissionBlipSprites)
                {
                    int blipHandle = Function.Call<int>(_getFirstBlipInfoId, sprite);

                    while (Function.Call<bool>(_doesBlipExist, blipHandle))
                    {
                        Vector3 blipPos = Function.Call<Vector3>(_getBlipInfoIdCoord, blipHandle);

                        // Skip if this is the player's own waypoint
                        if (hasWaypoint)
                        {
                            float waypointDist = (blipPos - waypointPos).Length();
                            if (waypointDist < 5f)
                            {
                                blipHandle = Function.Call<int>(_getNextBlipInfoId, sprite);
                                continue;
                            }
                        }

                        float distance = (blipPos - playerPos).Length();

                        if (distance < closestDistance && distance > 10f)
                        {
                            closestDistance = distance;
                            closestPos = blipPos;
                            closestSprite = sprite;
                            found = true;
                        }

                        blipHandle = Function.Call<int>(_getNextBlipInfoId, sprite);
                    }
                }

                if (found)
                {
                    string direction = SpatialCalculator.GetDirectionTo(playerPos, closestPos);
                    string name = GetBlipName(closestSprite);
                    float distanceMiles = closestDistance * Constants.METERS_TO_MILES;

                    _resultBuilder.Clear();
                    _resultBuilder.Append("Mission objective: ").Append(name).Append(", ");

                    if (distanceMiles < 0.1f)
                    {
                        int feet = (int)(closestDistance * Constants.METERS_TO_FEET);
                        _resultBuilder.Append(feet).Append(" feet ");
                    }
                    else
                    {
                        _resultBuilder.Append(Math.Round(distanceMiles, 1)).Append(" miles ");
                    }

                    _resultBuilder.Append(direction);
                    _audio.Speak(_resultBuilder.ToString());
                }
                else
                {
                    _audio.Speak("No active mission objective.");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AnnounceMissionBlip");
            }
        }

        /// <summary>
        /// Passive update: announce wanted level changes
        /// </summary>
        public void Update(long currentTick)
        {
            try
            {
                int wantedLevel = Game.Player.WantedLevel;

                if (wantedLevel != _lastWantedLevel)
                {
                    // Throttle rapid changes
                    if (currentTick - _lastPoliceAnnounceTick < POLICE_ANNOUNCE_INTERVAL)
                    {
                        _lastWantedLevel = wantedLevel;
                        return;
                    }

                    if (wantedLevel > 0 && _lastWantedLevel == 0)
                    {
                        _audio.Speak($"Wanted: {wantedLevel} star{(wantedLevel > 1 ? "s" : "")}");
                    }
                    else if (wantedLevel == 0 && _lastWantedLevel > 0)
                    {
                        _audio.Speak("Wanted level cleared.");
                    }
                    else if (wantedLevel > _lastWantedLevel)
                    {
                        _audio.Speak($"Wanted level increased to {wantedLevel} stars.");
                    }
                    else if (wantedLevel < _lastWantedLevel)
                    {
                        _audio.Speak($"Wanted level decreased to {wantedLevel} star{(wantedLevel > 1 ? "s" : "")}.");
                    }

                    _lastWantedLevel = wantedLevel;
                    _lastPoliceAnnounceTick = currentTick;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "BlipManager.Update");
            }
        }

        /// <summary>
        /// Get a friendly name for a blip sprite ID
        /// </summary>
        private static string GetBlipName(int spriteId)
        {
            if (BlipSpriteNames.TryGetValue(spriteId, out string name))
                return name;
            return "point of interest";
        }

        /// <summary>
        /// Lightweight struct for sorting blip results without heap allocation
        /// </summary>
        private struct BlipResult
        {
            public readonly string Name;
            public readonly double Distance;
            public readonly string Direction;

            public BlipResult(string name, double distance, string direction)
            {
                Name = name;
                Distance = distance;
                Direction = direction;
            }
        }
    }
}
