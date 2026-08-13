using System;
using System.Collections.Generic;
using System.IO;
using GTA;
using GTA.Math;
using GTA.Native;
using Newtonsoft.Json;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Sweeps the whole map and asks the game where every road is and what kind
    /// of road it is.
    ///
    /// The road spots in the teleport menu began as map knowledge typed from
    /// memory, which is guesswork wearing a coordinate's clothing. The game
    /// holds the real answer: every drivable road is a chain of vehicle nodes,
    /// each carrying a density and a property bitfield that says outright
    /// whether it is a highway, a tunnel, off-road, or has traffic lights. That
    /// is the same data the road-type announcements already read - this just
    /// reads it everywhere instead of under the car.
    ///
    /// Three natives make a full sweep possible:
    ///   LOAD_ALL_PATH_NODES streams in the whole node graph, so distant roads
    ///     answer instead of returning "not loaded" (which is why this cannot
    ///     be done by simply driving around).
    ///   GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING snaps a probe to a real node
    ///     and hands back its heading and lane count.
    ///   GET_VEHICLE_NODE_PROPERTIES gives that node's density and flags.
    ///
    /// The results are then NAMED by the game as well - GET_STREET_NAME_AT_COORD
    /// and the zone name turn a bare coordinate into "Route 68, Harmony". So
    /// the output is not merely verified, it is self-describing.
    ///
    /// The sweep is spread across frames on a probe budget: a whole-map survey
    /// is tens of thousands of native calls and doing it in one frame would
    /// hang the game. Progress is spoken, because a progress bar is no use here.
    /// </summary>
    public class RoadSurveyor
    {
        #region Types

        /// <summary>One road node as the game describes it.</summary>
        private class SurveyedNode
        {
            public Vector3 Position;
            public float Heading;
            public int Lanes;
            public int Density;
            public int Flags;
            public RoadType Type;

            // Named at discovery rather than at the end. Naming tens of
            // thousands of nodes in the final frame would hang the game; doing
            // it here spreads the cost over the frame-budgeted sweep, and each
            // node is only ever discovered once.
            public string Street;
            public string Zone;
        }

        /// <summary>A named spot, ready to become a teleport destination.</summary>
        private class NamedSpot
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("street")]
            public string Street { get; set; }

            [JsonProperty("zone")]
            public string Zone { get; set; }

            [JsonProperty("x")]
            public float X { get; set; }

            [JsonProperty("y")]
            public float Y { get; set; }

            [JsonProperty("z")]
            public float Z { get; set; }

            [JsonProperty("heading")]
            public float Heading { get; set; }

            [JsonProperty("lanes")]
            public int Lanes { get; set; }

            [JsonProperty("density")]
            public int Density { get; set; }

            [JsonProperty("flags")]
            public int Flags { get; set; }
        }

        /// <summary>A road long enough to drive end to end.</summary>
        private class NamedRoute
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("roadType")]
            public string RoadType { get; set; }

            [JsonProperty("nodeCount")]
            public int NodeCount { get; set; }

            [JsonProperty("lengthMeters")]
            public float LengthMeters { get; set; }

            [JsonProperty("startX")]
            public float StartX { get; set; }

            [JsonProperty("startY")]
            public float StartY { get; set; }

            [JsonProperty("startZ")]
            public float StartZ { get; set; }

            [JsonProperty("startHeading")]
            public float StartHeading { get; set; }

            [JsonProperty("endX")]
            public float EndX { get; set; }

            [JsonProperty("endY")]
            public float EndY { get; set; }

            [JsonProperty("endZ")]
            public float EndZ { get; set; }
        }

        /// <summary>The whole survey, as written to disk.</summary>
        private class SurveyFile
        {
            [JsonProperty("gridSpacing")]
            public float GridSpacing { get; set; }

            [JsonProperty("totalNodesFound")]
            public int TotalNodesFound { get; set; }

            [JsonProperty("countsByType")]
            public Dictionary<string, int> CountsByType { get; set; }

            [JsonProperty("spotsByType")]
            public Dictionary<string, List<NamedSpot>> SpotsByType { get; set; }

            [JsonProperty("routes")]
            public List<NamedRoute> Routes { get; set; }
        }

        private enum Phase
        {
            Idle,
            LoadingNodes,
            Sweeping,
            Finishing
        }

        #endregion

        #region Fields

        private readonly AudioManager _audio;
        private readonly RoadTypeManager _roadTypes;

        private Phase _phase = Phase.Idle;
        private long _phaseStartTick;

        // Sweep cursor
        private float _cursorX;
        private float _cursorY;
        private int _probesDone;
        private int _probesTotal;
        private int _lastAnnouncedPercent;

        // Results, keyed by a coarse cell so the same node found from several
        // probes is only kept once
        private readonly Dictionary<long, SurveyedNode> _found = new Dictionary<long, SurveyedNode>();

        // Reused so the sweep does not allocate per probe
        private readonly OutputArgument _outNodePos = new OutputArgument();
        private readonly OutputArgument _outHeading = new OutputArgument();
        private readonly OutputArgument _outLanes = new OutputArgument();
        private readonly OutputArgument _outDensity = new OutputArgument();
        private readonly OutputArgument _outFlags = new OutputArgument();

        private readonly string _savePath;

        #endregion

        #region Properties

        public bool IsRunning => _phase != Phase.Idle;

        #endregion

        #region Construction

        public RoadSurveyor(AudioManager audio, RoadTypeManager roadTypes)
        {
            _audio = audio;
            _roadTypes = roadTypes;

            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string folder = Path.Combine(documents, Constants.SETTINGS_FOLDER_PATH.TrimStart('/'));

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                _savePath = Path.Combine(folder, Constants.SURVEY_FILE_NAME);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RoadSurveyor constructor");
            }
        }

        #endregion

        #region Public API

        /// <summary>Start the survey, or cancel one already running.</summary>
        public void Toggle()
        {
            if (IsRunning)
            {
                Cancel("Survey cancelled.");
                return;
            }

            Start();
        }

        /// <summary>
        /// Called every tick. Does nothing unless a survey is running.
        /// </summary>
        public void Update(long currentTick)
        {
            if (_phase == Phase.Idle)
                return;

            try
            {
                switch (_phase)
                {
                    case Phase.LoadingNodes:
                        UpdateLoading(currentTick);
                        break;

                    case Phase.Sweeping:
                        UpdateSweep();
                        break;

                    case Phase.Finishing:
                        Finish();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RoadSurveyor.Update");
                Cancel("The survey failed. The log has the details.");
            }
        }

        #endregion

        #region Sweep

        private void Start()
        {
            _found.Clear();
            _cursorX = Constants.SURVEY_MIN_X;
            _cursorY = Constants.SURVEY_MIN_Y;
            _probesDone = 0;
            _lastAnnouncedPercent = 0;

            int columns = (int)((Constants.SURVEY_MAX_X - Constants.SURVEY_MIN_X) / Constants.SURVEY_GRID_SPACING) + 1;
            int rows = (int)((Constants.SURVEY_MAX_Y - Constants.SURVEY_MIN_Y) / Constants.SURVEY_GRID_SPACING) + 1;
            _probesTotal = columns * rows;

            // Ask for the entire node graph. Without this only nodes near the
            // player answer, and the survey would describe wherever you happen
            // to be standing rather than the map.
            Function.Call(Hash.LOAD_ALL_PATH_NODES, true);

            _phase = Phase.LoadingNodes;
            _phaseStartTick = Game.GameTime;

            Logger.Info($"SURVEY|start|probes={_probesTotal}|spacing={Constants.SURVEY_GRID_SPACING}");
            _audio.Speak("Road survey started. Loading the map's road data, please wait.", true);
        }

        private void UpdateLoading(long currentTick)
        {
            bool loaded = Function.Call<bool>(Hash.LOAD_ALL_PATH_NODES, true);

            if (loaded)
            {
                _phase = Phase.Sweeping;
                Logger.Info($"SURVEY|nodes-loaded|waited={currentTick - _phaseStartTick}ms");
                _audio.Speak("Road data loaded. Surveying the map now.", true);
                return;
            }

            if (currentTick - _phaseStartTick > Constants.SURVEY_LOAD_TIMEOUT_MS)
            {
                // Sweep anyway - partial coverage beats none, and the log will
                // show a low node count if this mattered
                _phase = Phase.Sweeping;
                Logger.Warning("SURVEY|nodes-load-timeout|sweeping with whatever is streamed");
                _audio.Speak("Road data took too long to load. Surveying what is available.", true);
            }
        }

        private void UpdateSweep()
        {
            for (int probe = 0; probe < Constants.SURVEY_PROBES_PER_TICK; probe++)
            {
                if (_cursorY > Constants.SURVEY_MAX_Y)
                {
                    _phase = Phase.Finishing;
                    return;
                }

                ProbeAt(_cursorX, _cursorY);
                _probesDone++;

                _cursorX += Constants.SURVEY_GRID_SPACING;
                if (_cursorX > Constants.SURVEY_MAX_X)
                {
                    _cursorX = Constants.SURVEY_MIN_X;
                    _cursorY += Constants.SURVEY_GRID_SPACING;
                }
            }

            AnnounceProgress();
        }

        /// <summary>
        /// Ask the game for the nearest node or two at this spot, and record
        /// what kind of road each one belongs to.
        /// </summary>
        private void ProbeAt(float x, float y)
        {
            for (int nth = 1; nth <= Constants.SURVEY_NODES_PER_POINT; nth++)
            {
                // Z is nominal: zMeasureMult of 0 tells the search to ignore
                // height entirely, which is what we want from a flat grid
                bool gotNode = Function.Call<bool>(
                    Hash.GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                    x, y, 0f,
                    nth,
                    _outNodePos, _outHeading, _outLanes,
                    Constants.SURVEY_NODE_SEARCH_FLAGS,
                    0f,     // zMeasureMult - ignore height
                    0f);    // zTolerance

                if (!gotNode)
                    continue;

                Vector3 nodePos = _outNodePos.GetResult<Vector3>();

                // Only keep nodes the probe actually reached. Beyond this the
                // search is returning the nearest node to an empty part of the
                // map, which would smear one node across a whole region.
                if (Math.Abs(nodePos.X - x) > Constants.SURVEY_GRID_SPACING ||
                    Math.Abs(nodePos.Y - y) > Constants.SURVEY_GRID_SPACING)
                    continue;

                long key = CellKey(nodePos);
                if (_found.ContainsKey(key))
                    continue;

                bool gotProps = Function.Call<bool>(
                    Hash.GET_VEHICLE_NODE_PROPERTIES,
                    nodePos.X, nodePos.Y, nodePos.Z,
                    _outDensity, _outFlags);

                if (!gotProps)
                    continue;

                int density = _outDensity.GetResult<int>();
                int flags = _outFlags.GetResult<int>();
                int lanes = _outLanes.GetResult<int>();

                RoadType type = _roadTypes.ClassifyRoadType(flags, density, lanes);
                if (type == RoadType.Unknown)
                    continue;

                _found[key] = new SurveyedNode
                {
                    Position = nodePos,
                    Heading = _outHeading.GetResult<float>(),
                    Lanes = lanes,
                    Density = density,
                    Flags = flags,
                    Type = type,
                    Street = SafeStreetName(nodePos),
                    Zone = SafeZoneName(nodePos)
                };
            }
        }

        /// <summary>
        /// A grid cell id for deduplication. Nodes found from neighbouring
        /// probes land on the same cell and are recorded once.
        /// </summary>
        private static long CellKey(Vector3 position)
        {
            long cx = (long)Math.Floor(position.X / Constants.SURVEY_DEDUPE_RADIUS);
            long cy = (long)Math.Floor(position.Y / Constants.SURVEY_DEDUPE_RADIUS);
            return (cx << 24) ^ cy;
        }

        private void AnnounceProgress()
        {
            int percent = _probesTotal > 0 ? (_probesDone * 100) / _probesTotal : 0;
            if (percent < _lastAnnouncedPercent + 25)
                return;

            _lastAnnouncedPercent = percent - (percent % 25);
            if (_lastAnnouncedPercent <= 0 || _lastAnnouncedPercent >= 100)
                return;

            _audio.Speak($"Survey {_lastAnnouncedPercent} percent, {_found.Count} road points so far.", true);
        }

        #endregion

        #region Results

        private void Finish()
        {
            // Release the node graph - holding the whole map's nodes costs
            // memory the game would rather spend on what is around the player
            Function.Call(Hash.LOAD_ALL_PATH_NODES, false);
            _phase = Phase.Idle;

            try
            {
                var byType = new Dictionary<RoadType, List<SurveyedNode>>();
                foreach (SurveyedNode node in _found.Values)
                {
                    List<SurveyedNode> list;
                    if (!byType.TryGetValue(node.Type, out list))
                    {
                        list = new List<SurveyedNode>();
                        byType[node.Type] = list;
                    }
                    list.Add(node);
                }

                var file = new SurveyFile
                {
                    GridSpacing = Constants.SURVEY_GRID_SPACING,
                    TotalNodesFound = _found.Count,
                    CountsByType = new Dictionary<string, int>(),
                    SpotsByType = new Dictionary<string, List<NamedSpot>>(),
                    Routes = new List<NamedRoute>()
                };

                foreach (var pair in byType)
                {
                    string typeName = pair.Key.ToString();
                    file.CountsByType[typeName] = pair.Value.Count;
                    file.SpotsByType[typeName] = PickSpread(pair.Value);
                    file.Routes.AddRange(FindRoutes(pair.Key, pair.Value));
                }

                string json = JsonConvert.SerializeObject(file, Formatting.Indented);
                File.WriteAllText(_savePath, json);

                var summary = new List<string>();
                foreach (var pair in file.CountsByType)
                {
                    summary.Add($"{pair.Key}={pair.Value}");
                    Logger.Info($"SURVEY|type|{pair.Key}|nodes={pair.Value}" +
                                $"|picked={file.SpotsByType[pair.Key].Count}");
                }

                Logger.Info($"SURVEY|done|nodes={_found.Count}|routes={file.Routes.Count}" +
                            $"|{string.Join(",", summary.ToArray())}|file={_savePath}");

                _audio.Speak($"Survey complete. {_found.Count} road points found, " +
                             $"{file.Routes.Count} drivable roads identified. " +
                             "The results are saved next to the log.", true);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RoadSurveyor.Finish");
                _audio.Speak("The survey finished but could not be saved. The log has the details.", true);
            }
            finally
            {
                _found.Clear();
            }
        }

        /// <summary>
        /// Pick spots spread across the map rather than the first N found,
        /// which would all sit in one corner because the sweep runs in order.
        /// Denser nodes come first so the pick favours a real road over a stub.
        /// </summary>
        private List<NamedSpot> PickSpread(List<SurveyedNode> candidates)
        {
            var picked = new List<NamedSpot>();
            var pickedPositions = new List<Vector3>();

            candidates.Sort((a, b) => b.Density.CompareTo(a.Density));

            foreach (SurveyedNode node in candidates)
            {
                if (picked.Count >= Constants.SURVEY_PICKS_PER_TYPE)
                    break;

                bool tooClose = false;
                foreach (Vector3 already in pickedPositions)
                {
                    if (already.DistanceTo(node.Position) < Constants.SURVEY_PICK_MIN_SEPARATION)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                    continue;

                picked.Add(Describe(node));
                pickedPositions.Add(node.Position);
            }

            return picked;
        }

        /// <summary>
        /// Group a road type's nodes by street name and keep the streets long
        /// enough to be worth driving. The two furthest-apart nodes on a street
        /// become its endpoints, which is exactly the shape the Road Trips menu
        /// wants: a start and an end on the SAME road, so the game's own router
        /// keeps the car on it.
        /// </summary>
        private List<NamedRoute> FindRoutes(RoadType type, List<SurveyedNode> nodes)
        {
            var byStreet = new Dictionary<string, List<SurveyedNode>>();

            foreach (SurveyedNode node in nodes)
            {
                // Named during the sweep, so this costs nothing here
                if (string.IsNullOrEmpty(node.Street))
                    continue;

                List<SurveyedNode> list;
                if (!byStreet.TryGetValue(node.Street, out list))
                {
                    list = new List<SurveyedNode>();
                    byStreet[node.Street] = list;
                }
                list.Add(node);
            }

            var routes = new List<NamedRoute>();

            foreach (var pair in byStreet)
            {
                if (pair.Value.Count < Constants.SURVEY_ROUTE_MIN_NODES)
                    continue;

                SurveyedNode start;
                SurveyedNode end;
                float best = FindEndpoints(pair.Value, out start, out end);

                if (start == null || best < Constants.SURVEY_ROUTE_MIN_LENGTH)
                    continue;

                routes.Add(new NamedRoute
                {
                    Name = pair.Key,
                    RoadType = type.ToString(),
                    NodeCount = pair.Value.Count,
                    LengthMeters = best,
                    StartX = start.Position.X,
                    StartY = start.Position.Y,
                    StartZ = start.Position.Z,
                    StartHeading = HeadingTowards(start.Position, end.Position),
                    EndX = end.Position.X,
                    EndY = end.Position.Y,
                    EndZ = end.Position.Z
                });
            }

            // Longest first, then keep the best few - a road type has hundreds
            // of named streets and only the long ones make a drive
            routes.Sort((a, b) => b.LengthMeters.CompareTo(a.LengthMeters));
            if (routes.Count > Constants.SURVEY_ROUTES_PER_TYPE)
                routes.RemoveRange(Constants.SURVEY_ROUTES_PER_TYPE,
                                   routes.Count - Constants.SURVEY_ROUTES_PER_TYPE);

            return routes;
        }

        /// <summary>
        /// The two ends of a road, found in two linear passes: the node
        /// furthest from the average position, then the node furthest from
        /// that one. Comparing every node against every other would be exact
        /// but quadratic, and a long street holds thousands of nodes - that is
        /// millions of comparisons in the frame that writes the file. For a
        /// road, which is essentially a line, these two passes land on the
        /// same answer.
        /// </summary>
        private static float FindEndpoints(List<SurveyedNode> nodes,
                                           out SurveyedNode start,
                                           out SurveyedNode end)
        {
            start = null;
            end = null;

            if (nodes == null || nodes.Count < 2)
                return 0f;

            Vector3 centre = Vector3.Zero;
            foreach (SurveyedNode node in nodes)
                centre += node.Position;
            centre /= nodes.Count;

            float furthest = -1f;
            foreach (SurveyedNode node in nodes)
            {
                float distance = centre.DistanceTo(node.Position);
                if (distance <= furthest)
                    continue;

                furthest = distance;
                start = node;
            }

            if (start == null)
                return 0f;

            float span = 0f;
            foreach (SurveyedNode node in nodes)
            {
                float distance = start.Position.DistanceTo(node.Position);
                if (distance <= span)
                    continue;

                span = distance;
                end = node;
            }

            return end == null ? 0f : span;
        }

        /// <summary>Name a spot the way the game names it.</summary>
        private NamedSpot Describe(SurveyedNode node)
        {
            string street = node.Street;
            string zone = node.Zone;

            string name;
            if (!string.IsNullOrEmpty(street) && !string.IsNullOrEmpty(zone))
                name = $"{street}, {zone}";
            else if (!string.IsNullOrEmpty(street))
                name = street;
            else if (!string.IsNullOrEmpty(zone))
                name = zone;
            else
                name = $"{node.Position.X:F0}, {node.Position.Y:F0}";

            return new NamedSpot
            {
                Name = name,
                Street = street,
                Zone = zone,
                X = node.Position.X,
                Y = node.Position.Y,
                Z = node.Position.Z,
                Heading = node.Heading,
                Lanes = node.Lanes,
                Density = node.Density,
                Flags = node.Flags
            };
        }

        /// <summary>
        /// The heading that points from one place to the other, in GTA's
        /// convention: counterclockwise, 0 north, 90 west.
        /// </summary>
        private static float HeadingTowards(Vector3 from, Vector3 to)
        {
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;

            double degrees = Math.Atan2(-dx, dy) * (180.0 / Math.PI);
            if (degrees < 0)
                degrees += 360.0;

            return (float)degrees;
        }

        private static string SafeStreetName(Vector3 position)
        {
            try
            {
                string name = World.GetStreetName(position);
                return string.IsNullOrEmpty(name) ? null : name.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static string SafeZoneName(Vector3 position)
        {
            try
            {
                string name = World.GetZoneLocalizedName(position);
                return string.IsNullOrEmpty(name) ? null : name.Trim();
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Helpers

        private void Cancel(string message)
        {
            if (_phase != Phase.Idle)
            {
                try { Function.Call(Hash.LOAD_ALL_PATH_NODES, false); }
                catch { }
            }

            _phase = Phase.Idle;
            _found.Clear();
            Logger.Info("SURVEY|cancelled");

            if (!string.IsNullOrEmpty(message))
                _audio.Speak(message, true);
        }

        /// <summary>Whether a survey has already been saved.</summary>
        public bool HasSavedSurvey()
        {
            try { return !string.IsNullOrEmpty(_savePath) && File.Exists(_savePath); }
            catch { return false; }
        }

        /// <summary>A spoken summary of the saved survey, or null if there is none.</summary>
        public string DescribeSavedSurvey()
        {
            try
            {
                if (!HasSavedSurvey())
                    return null;

                string json = File.ReadAllText(_savePath);
                SurveyFile file = JsonConvert.DeserializeObject<SurveyFile>(json);
                if (file == null || file.CountsByType == null)
                    return null;

                var parts = new List<string>();
                foreach (var pair in file.CountsByType)
                    parts.Add($"{pair.Value} {Constants.GetRoadTypeSpokenName(pair.Key)}");

                return $"{file.TotalNodesFound} road points: {string.Join(", ", parts.ToArray())}.";
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RoadSurveyor.DescribeSavedSurvey");
                return null;
            }
        }

        #endregion
    }
}
