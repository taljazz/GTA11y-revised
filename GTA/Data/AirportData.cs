using System;
using System.Collections.Generic;
using GTA.Math;

namespace GrandTheftAccessibility.Data
{
    /// <summary>
    /// Represents an airport with runways, parking positions, and taxi routes.
    /// Provides comprehensive ground navigation data for accessibility.
    /// </summary>
    public class Airport
    {
        public string Name { get; }
        public string ICAOCode { get; }  // e.g., "LSIA" for Los Santos International
        public Vector3 CenterPosition { get; }
        public float Radius { get; }  // meters - defines airport boundary
        public List<Runway> Runways { get; }
        public List<ParkingPosition> ParkingPositions { get; }
        public List<TaxiwaySegment> Taxiways { get; }

        public Airport(string name, string icaoCode, Vector3 center, float radius)
        {
            Name = name;
            ICAOCode = icaoCode;
            CenterPosition = center;
            Radius = radius;
            Runways = new List<Runway>();
            ParkingPositions = new List<ParkingPosition>();
            Taxiways = new List<TaxiwaySegment>();
        }

        /// <summary>
        /// Check if a position is within this airport's boundary
        /// </summary>
        public bool ContainsPosition(Vector3 position)
        {
            return Vector3.Distance(CenterPosition, position) <= Radius;
        }

        /// <summary>
        /// Find the nearest runway to a given position
        /// </summary>
        public Runway FindNearestRunway(Vector3 position)
        {
            Runway nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var runway in Runways)
            {
                float distance = Vector3.Distance(position, runway.ThresholdPosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = runway;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find the nearest parking position to a given position
        /// </summary>
        public ParkingPosition FindNearestParking(Vector3 position)
        {
            ParkingPosition nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var parking in ParkingPositions)
            {
                float distance = Vector3.Distance(position, parking.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = parking;
                }
            }

            return nearest;
        }
    }

    /// <summary>
    /// Represents a runway with threshold, end position, and approach data.
    /// </summary>
    public class Runway
    {
        public string Name { get; }              // e.g., "Runway 03L"
        public int Number { get; }               // e.g., 3 for Runway 03
        public string Designator { get; }        // e.g., "L", "R", "C", or ""
        public Vector3 ThresholdPosition { get; }
        public Vector3 EndPosition { get; }
        public float Heading { get; }            // Magnetic heading in degrees
        public float Length { get; }             // meters
        public float Width { get; }              // meters
        public float Elevation { get; }          // Ground elevation in meters

        // Approach procedure data
        public ApproachProcedure ILSApproach { get; set; }

        public Runway(string name, Vector3 threshold, float heading, float length, float width = 45f)
        {
            Name = name;
            ThresholdPosition = threshold;
            Heading = heading;
            Length = length;
            Width = width;
            Elevation = threshold.Z;

            // Parse runway number from name (e.g., "03L" -> 3, "L")
            ParseRunwayDesignation(name, out int number, out string designator);
            Number = number;
            Designator = designator;

            // Calculate end position
            // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
            float radians = heading * Constants.DEG_TO_RAD;
            EndPosition = threshold + new Vector3(
                (float)Math.Sin(radians) * length,
                (float)Math.Cos(radians) * length,
                0f);
        }

        private void ParseRunwayDesignation(string name, out int number, out string designator)
        {
            // Extract number and designator from names like "Runway 03L", "03", "12R"
            string cleaned = name.Replace("Runway ", "").Replace("RWY ", "").Trim();
            designator = "";
            number = 0;

            if (string.IsNullOrEmpty(cleaned)) return;

            // Check for L/R/C suffix
            char lastChar = cleaned[cleaned.Length - 1];
            if (lastChar == 'L' || lastChar == 'R' || lastChar == 'C')
            {
                designator = lastChar.ToString();
                cleaned = cleaned.Substring(0, cleaned.Length - 1);
            }

            int.TryParse(cleaned, out number);
        }

        /// <summary>
        /// Get the reciprocal runway number (opposite direction)
        /// </summary>
        public int GetReciprocalNumber()
        {
            int reciprocal = Number + 18;
            if (reciprocal > 36) reciprocal -= 36;
            return reciprocal;
        }
    }

    /// <summary>
    /// Represents an ILS or visual approach procedure with waypoints.
    /// </summary>
    public class ApproachProcedure
    {
        public string Name { get; }              // e.g., "ILS 03L"
        public Runway Runway { get; }
        public List<ApproachWaypoint> Waypoints { get; }
        public float GlideslopeAngle { get; }    // degrees (typically 3.0)
        public float LocalizerWidth { get; }     // degrees (typically 5.0)

        public ApproachProcedure(string name, Runway runway, float glideslopeAngle = 3.0f)
        {
            Name = name;
            Runway = runway;
            Waypoints = new List<ApproachWaypoint>();
            GlideslopeAngle = glideslopeAngle;
            LocalizerWidth = 5.0f;
        }

        /// <summary>
        /// Generate standard approach waypoints based on runway data
        /// </summary>
        public void GenerateStandardApproach()
        {
            Waypoints.Clear();

            // Calculate approach heading (opposite of runway heading)
            // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
            float approachHeading = (Runway.Heading + 180f) % 360f;
            float radians = approachHeading * Constants.DEG_TO_RAD;
            float groundZ = Runway.Elevation;

            // IAF - Initial Approach Fix (10nm / ~18.5km out, 4000ft AGL)
            Vector3 iafPos = Runway.ThresholdPosition + new Vector3(
                (float)Math.Sin(radians) * 18500f,
                (float)Math.Cos(radians) * 18500f,
                groundZ + 1220f);  // 4000 feet
            Waypoints.Add(new ApproachWaypoint("IAF", iafPos, ApproachWaypointType.InitialApproachFix, 1220f, 250f));

            // IF - Intermediate Fix (5nm / ~9.2km out, 2500ft AGL)
            Vector3 ifPos = Runway.ThresholdPosition + new Vector3(
                (float)Math.Sin(radians) * 9200f,
                (float)Math.Cos(radians) * 9200f,
                groundZ + 760f);  // 2500 feet
            Waypoints.Add(new ApproachWaypoint("IF", ifPos, ApproachWaypointType.IntermediateFix, 760f, 200f));

            // FAF - Final Approach Fix (glideslope intercept, ~5nm / 9.2km, but we use 3nm for GTA scale)
            Vector3 fafPos = Runway.ThresholdPosition + new Vector3(
                (float)Math.Sin(radians) * 5500f,
                (float)Math.Cos(radians) * 5500f,
                groundZ + 450f);  // 1500 feet
            Waypoints.Add(new ApproachWaypoint("FAF", fafPos, ApproachWaypointType.FinalApproachFix, 450f, 160f));

            // MAP - Missed Approach Point (0.5nm before threshold)
            Vector3 mapPos = Runway.ThresholdPosition + new Vector3(
                (float)Math.Sin(radians) * 900f,
                (float)Math.Cos(radians) * 900f,
                groundZ + 60f);  // 200 feet
            Waypoints.Add(new ApproachWaypoint("MAP", mapPos, ApproachWaypointType.MissedApproachPoint, 60f, 140f));

            // Threshold
            Waypoints.Add(new ApproachWaypoint("THR", Runway.ThresholdPosition, ApproachWaypointType.Threshold, 0f, 130f));
        }
    }

    /// <summary>
    /// Types of approach waypoints
    /// </summary>
    public enum ApproachWaypointType
    {
        InitialApproachFix,
        IntermediateFix,
        FinalApproachFix,
        MissedApproachPoint,
        Threshold,
        Custom
    }

    /// <summary>
    /// Represents a waypoint in an approach procedure
    /// </summary>
    public class ApproachWaypoint
    {
        public string Name { get; }
        public Vector3 Position { get; }
        public ApproachWaypointType Type { get; }
        public float AltitudeAGL { get; }        // meters above ground level
        public float TargetSpeed { get; }        // knots

        public ApproachWaypoint(string name, Vector3 position, ApproachWaypointType type, float altitudeAGL, float targetSpeedKnots)
        {
            Name = name;
            Position = position;
            Type = type;
            AltitudeAGL = altitudeAGL;
            TargetSpeed = targetSpeedKnots * 0.514444f;  // Convert knots to m/s
        }
    }

    /// <summary>
    /// Represents a parking/gate position at an airport
    /// </summary>
    public class ParkingPosition
    {
        public string Name { get; }              // e.g., "Gate A1", "Hangar 2"
        public Vector3 Position { get; }
        public float Heading { get; }            // Parking heading
        public ParkingType Type { get; }
        public float Radius { get; }             // Size of parking spot

        public ParkingPosition(string name, Vector3 position, float heading, ParkingType type = ParkingType.Gate)
        {
            Name = name;
            Position = position;
            Heading = heading;
            Type = type;
            Radius = type == ParkingType.Hangar ? 30f : 15f;
        }
    }

    /// <summary>
    /// Types of parking positions
    /// </summary>
    public enum ParkingType
    {
        Gate,
        Ramp,
        Hangar,
        FBO,  // Fixed Base Operator
        Cargo,
        Military
    }

    /// <summary>
    /// Represents a segment of taxiway connecting two points
    /// </summary>
    public class TaxiwaySegment
    {
        public string Name { get; }              // e.g., "Alpha", "Bravo"
        public Vector3 StartPosition { get; }
        public Vector3 EndPosition { get; }
        public float Width { get; }              // meters
        public bool IsBidirectional { get; }

        public TaxiwaySegment(string name, Vector3 start, Vector3 end, float width = 23f, bool bidirectional = true)
        {
            Name = name;
            StartPosition = start;
            EndPosition = end;
            Width = width;
            IsBidirectional = bidirectional;
        }

        /// <summary>
        /// Get the midpoint of this taxiway segment
        /// </summary>
        public Vector3 GetMidpoint()
        {
            return (StartPosition + EndPosition) / 2f;
        }
    }

    /// <summary>
    /// Static class containing all airport data for GTA V.
    /// Provides predefined taxi routes, runways, and parking for major airports.
    /// </summary>
    public static class AirportDatabase
    {
        private static List<Airport> _airports;
        private static bool _initialized = false;

        /// <summary>
        /// Get all airports in the database
        /// </summary>
        public static List<Airport> GetAllAirports()
        {
            EnsureInitialized();
            return _airports;
        }

        /// <summary>
        /// Find the airport containing a given position
        /// </summary>
        public static Airport FindAirportAtPosition(Vector3 position)
        {
            EnsureInitialized();

            foreach (var airport in _airports)
            {
                if (airport.ContainsPosition(position))
                    return airport;
            }

            return null;
        }

        /// <summary>
        /// Find the nearest airport to a given position
        /// </summary>
        public static Airport FindNearestAirport(Vector3 position)
        {
            EnsureInitialized();

            Airport nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var airport in _airports)
            {
                float distance = Vector3.Distance(position, airport.CenterPosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = airport;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find the nearest runway across all airports
        /// </summary>
        public static Runway FindNearestRunway(Vector3 position, out Airport airport)
        {
            EnsureInitialized();

            airport = null;
            Runway nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var ap in _airports)
            {
                foreach (var runway in ap.Runways)
                {
                    float distance = Vector3.Distance(position, runway.ThresholdPosition);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = runway;
                        airport = ap;
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get a taxi route from parking to runway at a specific airport using A* pathfinding.
        /// </summary>
        public static List<Vector3> GetTaxiRouteToRunway(Airport airport, Vector3 currentPosition, Runway targetRunway)
        {
            var route = new List<Vector3>();

            if (airport == null || targetRunway == null)
                return route;

            // Build the taxiway graph and find path using A*
            var path = FindTaxiPath(airport, currentPosition, targetRunway.ThresholdPosition);

            if (path.Count > 0)
            {
                route.AddRange(path);
            }

            // Add runway hold short position (50m before threshold)
            // PERFORMANCE: Use pre-calculated DEG_TO_RAD constant
            float holdHeading = (targetRunway.Heading + 180f) % 360f;
            float holdRadians = holdHeading * Constants.DEG_TO_RAD;
            Vector3 holdShort = targetRunway.ThresholdPosition + new Vector3(
                (float)Math.Sin(holdRadians) * 50f,
                (float)Math.Cos(holdRadians) * 50f,
                0f);

            // Only add hold short if it's not too close to last waypoint
            if (route.Count == 0 || Vector3.Distance(route[route.Count - 1], holdShort) > 20f)
            {
                route.Add(holdShort);
            }

            // Add runway threshold
            route.Add(targetRunway.ThresholdPosition);

            return route;
        }

        /// <summary>
        /// A* pathfinding through the taxiway network.
        /// </summary>
        private static List<Vector3> FindTaxiPath(Airport airport, Vector3 startPos, Vector3 endPos)
        {
            const float NODE_MERGE_DISTANCE = 30f;  // Merge nodes within this distance
            const float CONNECTION_DISTANCE = 50f;  // Max distance to connect to taxiway network

            // Build graph of all taxiway nodes
            var allNodes = new Dictionary<int, Vector3>();
            var nodeConnections = new Dictionary<int, List<int>>();
            int nodeIndex = 0;

            // Add all taxiway endpoints as nodes
            foreach (var taxiway in airport.Taxiways)
            {
                int startIdx = FindOrAddNode(allNodes, taxiway.StartPosition, NODE_MERGE_DISTANCE, ref nodeIndex);
                int endIdx = FindOrAddNode(allNodes, taxiway.EndPosition, NODE_MERGE_DISTANCE, ref nodeIndex);

                // Add bidirectional connections
                if (!nodeConnections.ContainsKey(startIdx))
                    nodeConnections[startIdx] = new List<int>();
                if (!nodeConnections.ContainsKey(endIdx))
                    nodeConnections[endIdx] = new List<int>();

                if (!nodeConnections[startIdx].Contains(endIdx))
                    nodeConnections[startIdx].Add(endIdx);
                if (taxiway.IsBidirectional && !nodeConnections[endIdx].Contains(startIdx))
                    nodeConnections[endIdx].Add(startIdx);
            }

            // Find nearest node to start position
            int startNodeIdx = FindNearestNode(allNodes, startPos, CONNECTION_DISTANCE);
            if (startNodeIdx < 0)
            {
                // No taxiway nearby - try to find any node within larger radius
                startNodeIdx = FindNearestNode(allNodes, startPos, CONNECTION_DISTANCE * 3);
                if (startNodeIdx < 0)
                    return new List<Vector3>();
            }

            // Find nearest node to end position (runway threshold)
            int endNodeIdx = FindNearestNode(allNodes, endPos, CONNECTION_DISTANCE * 2);
            if (endNodeIdx < 0)
            {
                // Runway threshold not near taxiway - find closest
                endNodeIdx = FindNearestNode(allNodes, endPos, float.MaxValue);
                if (endNodeIdx < 0)
                    return new List<Vector3>();
            }

            // A* algorithm
            var openSet = new SortedSet<(float fScore, int node)>(Comparer<(float, int)>.Create((a, b) =>
            {
                int cmp = a.Item1.CompareTo(b.Item1);
                return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
            }));

            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, float>();
            var fScore = new Dictionary<int, float>();

            foreach (var node in allNodes.Keys)
            {
                gScore[node] = float.MaxValue;
                fScore[node] = float.MaxValue;
            }

            gScore[startNodeIdx] = 0f;
            fScore[startNodeIdx] = Heuristic(allNodes[startNodeIdx], endPos);
            openSet.Add((fScore[startNodeIdx], startNodeIdx));

            while (openSet.Count > 0)
            {
                var (_, current) = openSet.Min;
                openSet.Remove(openSet.Min);

                if (current == endNodeIdx)
                {
                    // Reconstruct path
                    return ReconstructPath(cameFrom, current, allNodes, startPos);
                }

                if (!nodeConnections.ContainsKey(current))
                    continue;

                foreach (int neighbor in nodeConnections[current])
                {
                    float tentativeGScore = gScore[current] + Vector3.Distance(allNodes[current], allNodes[neighbor]);

                    if (tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + Heuristic(allNodes[neighbor], endPos);

                        // Remove old entry if exists and add new
                        openSet.RemoveWhere(x => x.node == neighbor);
                        openSet.Add((fScore[neighbor], neighbor));
                    }
                }
            }

            // No path found - return empty
            return new List<Vector3>();
        }

        private static int FindOrAddNode(Dictionary<int, Vector3> nodes, Vector3 position, float mergeDistance, ref int nextIndex)
        {
            // Check if a node already exists near this position
            foreach (var kvp in nodes)
            {
                if (Vector3.Distance(kvp.Value, position) < mergeDistance)
                    return kvp.Key;
            }

            // Add new node
            int idx = nextIndex++;
            nodes[idx] = position;
            return idx;
        }

        private static int FindNearestNode(Dictionary<int, Vector3> nodes, Vector3 position, float maxDistance)
        {
            int nearest = -1;
            float nearestDist = maxDistance;

            foreach (var kvp in nodes)
            {
                float dist = Vector3.Distance(kvp.Value, position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = kvp.Key;
                }
            }

            return nearest;
        }

        private static float Heuristic(Vector3 a, Vector3 b)
        {
            // Euclidean distance heuristic
            return Vector3.Distance(a, b);
        }

        private static List<Vector3> ReconstructPath(Dictionary<int, int> cameFrom, int current, Dictionary<int, Vector3> nodes, Vector3 startPos)
        {
            var path = new List<Vector3>();
            path.Add(nodes[current]);

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(nodes[current]);
            }

            path.Reverse();

            // If first waypoint is far from start, add an intermediate point
            if (path.Count > 0 && Vector3.Distance(startPos, path[0]) > 50f)
            {
                // Add a point partway to help guide the aircraft
                Vector3 intermediatePoint = startPos + (path[0] - startPos) * 0.5f;
                path.Insert(0, intermediatePoint);
            }

            return path;
        }

        /// <summary>
        /// Get a taxi route from runway to parking using A* pathfinding.
        /// </summary>
        public static List<Vector3> GetTaxiRouteToParking(Airport airport, Vector3 currentPosition, ParkingPosition targetParking)
        {
            var route = new List<Vector3>();

            if (airport == null || targetParking == null)
                return route;

            // Build the taxiway graph and find path using A*
            var path = FindTaxiPath(airport, currentPosition, targetParking.Position);

            if (path.Count > 0)
            {
                route.AddRange(path);
            }

            // Add final parking position if not already included
            if (route.Count == 0 || Vector3.Distance(route[route.Count - 1], targetParking.Position) > 20f)
            {
                route.Add(targetParking.Position);
            }

            return route;
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;

            _airports = new List<Airport>();

            // Initialize all airports
            InitializeLSIA();
            InitializeSandyShores();
            InitializeMcKenzieField();
            InitializeFortZancudo();

            _initialized = true;
        }

        private static void InitializeLSIA()
        {
            // Los Santos International Airport
            var lsia = new Airport("Los Santos International", "LSIA",
                new Vector3(-1350f, -2800f, 14f), 1500f);

            // Runways
            // Runway 03/21 (main runway, roughly 93°/273°)
            var rwy03 = new Runway("03", new Vector3(-1336f, -2434f, 13.9f), 93f, 800f);
            var rwy21 = new Runway("21", new Vector3(-942f, -2988f, 13.9f), 273f, 800f);

            // Runway 12/30 (cross runway, roughly 183°/3°)
            var rwy12 = new Runway("12", new Vector3(-1850f, -2978f, 13.9f), 183f, 800f);
            var rwy30 = new Runway("30", new Vector3(-1218f, -2563f, 13.9f), 3f, 800f);

            // Generate approach procedures
            rwy03.ILSApproach = new ApproachProcedure("ILS 03", rwy03);
            rwy03.ILSApproach.GenerateStandardApproach();
            rwy21.ILSApproach = new ApproachProcedure("ILS 21", rwy21);
            rwy21.ILSApproach.GenerateStandardApproach();
            rwy12.ILSApproach = new ApproachProcedure("ILS 12", rwy12);
            rwy12.ILSApproach.GenerateStandardApproach();
            rwy30.ILSApproach = new ApproachProcedure("ILS 30", rwy30);
            rwy30.ILSApproach.GenerateStandardApproach();

            lsia.Runways.Add(rwy03);
            lsia.Runways.Add(rwy21);
            lsia.Runways.Add(rwy12);
            lsia.Runways.Add(rwy30);

            // Parking positions (terminals, hangars)
            lsia.ParkingPositions.Add(new ParkingPosition("Terminal Gate A1", new Vector3(-1037f, -2962f, 14f), 180f, ParkingType.Gate));
            lsia.ParkingPositions.Add(new ParkingPosition("Terminal Gate A2", new Vector3(-1067f, -2962f, 14f), 180f, ParkingType.Gate));
            lsia.ParkingPositions.Add(new ParkingPosition("Terminal Gate A3", new Vector3(-1097f, -2962f, 14f), 180f, ParkingType.Gate));
            lsia.ParkingPositions.Add(new ParkingPosition("Terminal Gate B1", new Vector3(-1200f, -2890f, 14f), 90f, ParkingType.Gate));
            lsia.ParkingPositions.Add(new ParkingPosition("Terminal Gate B2", new Vector3(-1200f, -2920f, 14f), 90f, ParkingType.Gate));
            lsia.ParkingPositions.Add(new ParkingPosition("Cargo Ramp 1", new Vector3(-1550f, -2730f, 14f), 270f, ParkingType.Cargo));
            lsia.ParkingPositions.Add(new ParkingPosition("Cargo Ramp 2", new Vector3(-1550f, -2780f, 14f), 270f, ParkingType.Cargo));
            lsia.ParkingPositions.Add(new ParkingPosition("FBO Hangar", new Vector3(-1250f, -3050f, 14f), 0f, ParkingType.FBO));
            lsia.ParkingPositions.Add(new ParkingPosition("Private Hangar 1", new Vector3(-1150f, -3100f, 14f), 0f, ParkingType.Hangar));
            lsia.ParkingPositions.Add(new ParkingPosition("Private Hangar 2", new Vector3(-1080f, -3100f, 14f), 0f, ParkingType.Hangar));

            // Taxiways
            // Main taxiway parallel to 03/21 (Alpha runs along the south side of the airport)
            // Split Alpha into smaller connected segments to ensure proper graph connectivity
            lsia.Taxiways.Add(new TaxiwaySegment("Alpha", new Vector3(-1150f, -2500f, 14f), new Vector3(-1100f, -2600f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Alpha 2", new Vector3(-1100f, -2600f, 14f), new Vector3(-1050f, -2700f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Alpha 3", new Vector3(-1050f, -2700f, 14f), new Vector3(-1000f, -2800f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Alpha 4", new Vector3(-1000f, -2800f, 14f), new Vector3(-1000f, -2900f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Alpha 5", new Vector3(-1000f, -2900f, 14f), new Vector3(-1037f, -2962f, 14f)));

            // Connecting taxiways (with proper junction points)
            lsia.Taxiways.Add(new TaxiwaySegment("Bravo", new Vector3(-1100f, -2600f, 14f), new Vector3(-1200f, -2550f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Bravo 2", new Vector3(-1200f, -2550f, 14f), new Vector3(-1270f, -2490f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Bravo 3", new Vector3(-1270f, -2490f, 14f), new Vector3(-1336f, -2434f, 14f)));

            lsia.Taxiways.Add(new TaxiwaySegment("Charlie", new Vector3(-1000f, -2800f, 14f), new Vector3(-970f, -2880f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Charlie 2", new Vector3(-970f, -2880f, 14f), new Vector3(-942f, -2988f, 14f)));

            // Cross runway taxiways
            lsia.Taxiways.Add(new TaxiwaySegment("Delta", new Vector3(-1200f, -2890f, 14f), new Vector3(-1350f, -2800f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Delta 2", new Vector3(-1350f, -2800f, 14f), new Vector3(-1500f, -2850f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Delta 3", new Vector3(-1500f, -2850f, 14f), new Vector3(-1700f, -2920f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Delta 4", new Vector3(-1700f, -2920f, 14f), new Vector3(-1850f, -2978f, 14f)));

            lsia.Taxiways.Add(new TaxiwaySegment("Echo", new Vector3(-1200f, -2550f, 14f), new Vector3(-1218f, -2563f, 14f)));

            // Terminal connectors
            lsia.Taxiways.Add(new TaxiwaySegment("Terminal A", new Vector3(-1037f, -2962f, 14f), new Vector3(-1097f, -2962f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Terminal A2", new Vector3(-1097f, -2962f, 14f), new Vector3(-1150f, -2950f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Terminal B", new Vector3(-1200f, -2890f, 14f), new Vector3(-1200f, -2920f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Terminal B2", new Vector3(-1150f, -2950f, 14f), new Vector3(-1200f, -2890f, 14f)));

            // === Private Hangar taxiway connections ===
            // South Apron taxiway (connects hangars to main taxiway network)
            // Private Hangar 1 (-1150, -3100) connects via South Apron to Terminal A
            lsia.Taxiways.Add(new TaxiwaySegment("South Apron 1", new Vector3(-1150f, -3100f, 14f), new Vector3(-1150f, -3050f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("South Apron 2", new Vector3(-1150f, -3050f, 14f), new Vector3(-1100f, -3000f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("South Apron 3", new Vector3(-1100f, -3000f, 14f), new Vector3(-1037f, -2962f, 14f)));

            // Private Hangar 2 (-1080, -3100) connects to South Apron
            lsia.Taxiways.Add(new TaxiwaySegment("South Apron 4", new Vector3(-1080f, -3100f, 14f), new Vector3(-1100f, -3050f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("South Apron 5", new Vector3(-1100f, -3050f, 14f), new Vector3(-1100f, -3000f, 14f)));

            // FBO Hangar (-1250, -3050) connects to Terminal B area
            lsia.Taxiways.Add(new TaxiwaySegment("FBO Connector", new Vector3(-1250f, -3050f, 14f), new Vector3(-1200f, -3000f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("FBO Connector 2", new Vector3(-1200f, -3000f, 14f), new Vector3(-1150f, -2950f, 14f)));
            // FBO Connector 3 connects to Terminal A2's endpoint
            lsia.Taxiways.Add(new TaxiwaySegment("FBO Connector 3", new Vector3(-1150f, -2950f, 14f), new Vector3(-1097f, -2962f, 14f)));

            // Devin Weston Hangar (-1355, -3059) connection
            lsia.Taxiways.Add(new TaxiwaySegment("DW Hangar", new Vector3(-1355f, -3059f, 14f), new Vector3(-1300f, -3000f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("DW Hangar 2", new Vector3(-1300f, -3000f, 14f), new Vector3(-1250f, -2950f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("DW Hangar 3", new Vector3(-1250f, -2950f, 14f), new Vector3(-1200f, -2890f, 14f)));

            // Golf - connects Terminal A to Runway 21 threshold
            lsia.Taxiways.Add(new TaxiwaySegment("Golf 1", new Vector3(-1037f, -2962f, 14f), new Vector3(-990f, -2975f, 14f)));
            lsia.Taxiways.Add(new TaxiwaySegment("Golf 2", new Vector3(-990f, -2975f, 14f), new Vector3(-942f, -2988f, 14f)));

            _airports.Add(lsia);
        }

        private static void InitializeSandyShores()
        {
            // Sandy Shores Airfield
            var sandy = new Airport("Sandy Shores Airfield", "KSSA",
                new Vector3(1650f, 3200f, 41f), 500f);

            // Runway 12/30 (roughly 118°/298°)
            var rwy12 = new Runway("12", new Vector3(1747f, 3273f, 41.1f), 118f, 600f);
            var rwy30 = new Runway("30", new Vector3(1395f, 3130f, 40.4f), 298f, 600f);

            rwy12.ILSApproach = new ApproachProcedure("VIS 12", rwy12);
            rwy12.ILSApproach.GenerateStandardApproach();
            rwy30.ILSApproach = new ApproachProcedure("VIS 30", rwy30);
            rwy30.ILSApproach.GenerateStandardApproach();

            sandy.Runways.Add(rwy12);
            sandy.Runways.Add(rwy30);

            // Parking
            sandy.ParkingPositions.Add(new ParkingPosition("Main Hangar", new Vector3(1770f, 3239f, 42f), 0f, ParkingType.Hangar));
            sandy.ParkingPositions.Add(new ParkingPosition("Ramp North", new Vector3(1700f, 3300f, 41f), 180f, ParkingType.Ramp));
            sandy.ParkingPositions.Add(new ParkingPosition("Ramp South", new Vector3(1450f, 3150f, 40f), 0f, ParkingType.Ramp));

            // Taxiways
            sandy.Taxiways.Add(new TaxiwaySegment("Alpha", new Vector3(1770f, 3239f, 42f), new Vector3(1747f, 3273f, 41f)));
            sandy.Taxiways.Add(new TaxiwaySegment("Bravo", new Vector3(1450f, 3150f, 40f), new Vector3(1395f, 3130f, 40f)));

            _airports.Add(sandy);
        }

        private static void InitializeMcKenzieField()
        {
            // McKenzie Field (Grapeseed)
            var mckenzie = new Airport("McKenzie Field", "KMCK",
                new Vector3(2070f, 4780f, 41f), 400f);

            // Runway 10/28 (roughly 100°/280°)
            var rwy10 = new Runway("10", new Vector3(2134f, 4801f, 41.2f), 100f, 500f);
            var rwy28 = new Runway("28", new Vector3(2012f, 4750f, 40.5f), 280f, 500f);

            rwy10.ILSApproach = new ApproachProcedure("VIS 10", rwy10);
            rwy10.ILSApproach.GenerateStandardApproach();
            rwy28.ILSApproach = new ApproachProcedure("VIS 28", rwy28);
            rwy28.ILSApproach.GenerateStandardApproach();

            mckenzie.Runways.Add(rwy10);
            mckenzie.Runways.Add(rwy28);

            // Parking
            mckenzie.ParkingPositions.Add(new ParkingPosition("Hangar", new Vector3(2100f, 4720f, 41f), 90f, ParkingType.Hangar));
            mckenzie.ParkingPositions.Add(new ParkingPosition("Grass Ramp", new Vector3(2050f, 4780f, 41f), 180f, ParkingType.Ramp));

            // Taxiways
            mckenzie.Taxiways.Add(new TaxiwaySegment("Alpha", new Vector3(2100f, 4720f, 41f), new Vector3(2134f, 4801f, 41f)));

            _airports.Add(mckenzie);
        }

        private static void InitializeFortZancudo()
        {
            // Fort Zancudo Military Airbase
            var zancudo = new Airport("Fort Zancudo", "KNKX",
                new Vector3(-2350f, 3060f, 33f), 800f);

            // Runway 12/30 (roughly 117°/297°)
            var rwy12 = new Runway("12", new Vector3(-2259f, 3102f, 32.8f), 117f, 900f);
            var rwy30 = new Runway("30", new Vector3(-2454f, 3015f, 32.8f), 297f, 900f);

            rwy12.ILSApproach = new ApproachProcedure("ILS 12", rwy12);
            rwy12.ILSApproach.GenerateStandardApproach();
            rwy30.ILSApproach = new ApproachProcedure("ILS 30", rwy30);
            rwy30.ILSApproach.GenerateStandardApproach();

            zancudo.Runways.Add(rwy12);
            zancudo.Runways.Add(rwy30);

            // Parking (military)
            zancudo.ParkingPositions.Add(new ParkingPosition("Hangar 1", new Vector3(-2100f, 3150f, 33f), 270f, ParkingType.Military));
            zancudo.ParkingPositions.Add(new ParkingPosition("Hangar 2", new Vector3(-2100f, 3200f, 33f), 270f, ParkingType.Military));
            zancudo.ParkingPositions.Add(new ParkingPosition("Flight Line", new Vector3(-2200f, 3150f, 33f), 180f, ParkingType.Military));
            zancudo.ParkingPositions.Add(new ParkingPosition("Helipad Main", new Vector3(-2148f, 3176f, 33f), 0f, ParkingType.Ramp));
            zancudo.ParkingPositions.Add(new ParkingPosition("Control Tower Pad", new Vector3(-2358f, 3249f, 101.5f), 0f, ParkingType.Ramp));

            // Taxiways
            zancudo.Taxiways.Add(new TaxiwaySegment("Alpha", new Vector3(-2100f, 3150f, 33f), new Vector3(-2259f, 3102f, 33f)));
            zancudo.Taxiways.Add(new TaxiwaySegment("Bravo", new Vector3(-2200f, 3150f, 33f), new Vector3(-2350f, 3060f, 33f)));
            zancudo.Taxiways.Add(new TaxiwaySegment("Charlie", new Vector3(-2350f, 3060f, 33f), new Vector3(-2454f, 3015f, 33f)));

            _airports.Add(zancudo);
        }
    }
}
