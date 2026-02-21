using GTA.Math;

namespace GrandTheftAccessibility.Data
{
    /// <summary>
    /// Represents a teleport destination with name and coordinates
    /// </summary>
    public struct TeleportLocation
    {
        public string Name { get; }
        public Vector3 Coords { get; }
        public string Category { get; }

        public TeleportLocation(string name, float x, float y, float z, string category)
        {
            Name = name;
            Coords = new Vector3(x, y, z);
            Category = category;
        }
    }

    /// <summary>
    /// Represents a GPS waypoint destination
    /// </summary>
    public struct WaypointDestination
    {
        public string Name { get; }
        public Vector3 Coords { get; }

        public WaypointDestination(string name, float x, float y, float z)
        {
            Name = name;
            Coords = new Vector3(x, y, z);
        }
    }

    /// <summary>
    /// Centralized location data for teleport and waypoint menus.
    /// All coordinates are organized by category for easy navigation.
    /// </summary>
    public static class LocationData
    {
        #region Teleport Location Categories

        public static readonly string[] TeleportCategories = new string[]
        {
            "Character Houses",
            "Airports and Runways",
            "Sniping Vantage Points",
            "Military and Restricted",
            "Landmarks",
            "Blaine County",
            "Coastal and Beaches",
            "Remote Areas",
            "Emergency Services"
        };

        #endregion

        #region Teleport Locations - Character Houses

        public static readonly TeleportLocation[] CharacterHouses = new TeleportLocation[]
        {
            new TeleportLocation("Michael's House", -852.4f, 160.0f, 65.6f, "Character Houses"),
            new TeleportLocation("Franklin's House", 7.9f, 548.1f, 175.5f, "Character Houses"),
            new TeleportLocation("Trevor's Trailer", 1985.7f, 3812.2f, 32.2f, "Character Houses"),
            new TeleportLocation("Floyd's Apartment", -1157.2f, -1520.7f, 10.6f, "Character Houses"),
            new TeleportLocation("Lester's House", 1273.9f, -1719.3f, 54.8f, "Character Houses")
        };

        #endregion

        #region Teleport Locations - Airports and Runways

        public static readonly TeleportLocation[] AirportsAndRunways = new TeleportLocation[]
        {
            // LSIA - Los Santos International Airport - Runways
            new TeleportLocation("LSIA Runway 03 South End", -1285.0f, -3359.0f, 13.9f, "Airports and Runways"),
            new TeleportLocation("LSIA Runway 21 North End", -1497.0f, -2598.0f, 13.9f, "Airports and Runways"),
            new TeleportLocation("LSIA Runway 12 West End", -1850.0f, -3103.0f, 13.9f, "Airports and Runways"),
            new TeleportLocation("LSIA Runway 30 East End", -942.0f, -2950.0f, 13.9f, "Airports and Runways"),
            new TeleportLocation("LSIA Main Terminal", -1034.6f, -2733.6f, 13.8f, "Airports and Runways"),
            new TeleportLocation("LSIA Center Field", -1336.0f, -3044.0f, 13.9f, "Airports and Runways"),
            // LSIA - Aircraft Parking (for jets/fixed-wing)
            new TeleportLocation("LSIA Terminal Gate A1", -1037.0f, -2962.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA Terminal Gate A2", -1067.0f, -2962.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA Terminal Gate A3", -1097.0f, -2962.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA Terminal Gate B1", -1200.0f, -2890.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA Terminal Gate B2", -1200.0f, -2920.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA Cargo Ramp 1", -1550.0f, -2730.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA Cargo Ramp 2", -1550.0f, -2780.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA FBO Hangar", -1250.0f, -3050.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA Private Hangar 1", -1150.0f, -3100.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA Private Hangar 2", -1080.0f, -3100.0f, 14.0f, "Airports and Runways"),
            new TeleportLocation("LSIA Devin Weston Hangar", -1355.0f, -3059.0f, 14.0f, "Airports and Runways"),
            // Sandy Shores Airfield
            new TeleportLocation("Sandy Shores Airfield Runway", 1747.0f, 3273.7f, 41.1f, "Airports and Runways"),
            new TeleportLocation("Sandy Shores Main Hangar", 1770.0f, 3239.0f, 42.0f, "Airports and Runways"),
            new TeleportLocation("Sandy Shores Ramp North", 1700.0f, 3300.0f, 41.0f, "Airports and Runways"),
            new TeleportLocation("Sandy Shores Ramp South", 1450.0f, 3150.0f, 40.0f, "Airports and Runways"),
            // McKenzie Field (Grapeseed)
            new TeleportLocation("McKenzie Airfield Runway", 2121.7f, 4796.3f, 41.1f, "Airports and Runways"),
            new TeleportLocation("McKenzie Hangar", 2100.0f, 4720.0f, 41.0f, "Airports and Runways"),
            new TeleportLocation("McKenzie Grass Ramp", 2050.0f, 4780.0f, 41.0f, "Airports and Runways"),
            new TeleportLocation("Grapeseed Airstrip", 2132.0f, 4805.0f, 41.2f, "Airports and Runways"),
            // Fort Zancudo Military Airbase (best for jets like Lazer)
            new TeleportLocation("Fort Zancudo Runway South", -2285.0f, 3162.0f, 32.8f, "Airports and Runways"),
            new TeleportLocation("Fort Zancudo Runway North", -1813.0f, 3086.0f, 32.8f, "Airports and Runways"),
            new TeleportLocation("Fort Zancudo Hangar 1", -2100.0f, 3150.0f, 33.0f, "Airports and Runways"),
            new TeleportLocation("Fort Zancudo Hangar 2", -2100.0f, 3200.0f, 33.0f, "Airports and Runways"),
            new TeleportLocation("Fort Zancudo Flight Line", -2200.0f, 3150.0f, 33.0f, "Airports and Runways"),
            new TeleportLocation("Fort Zancudo Jet Spawn Area", -2454.0f, 3015.0f, 32.8f, "Airports and Runways"),
            new TeleportLocation("Fort Zancudo Helipad Main", -2148.0f, 3176.0f, 33.0f, "Airports and Runways"),
            new TeleportLocation("Fort Zancudo Control Tower Pad", -2358.0f, 3249.0f, 101.5f, "Airports and Runways")
        };

        #endregion

        #region Teleport Locations - Sniping Vantage Points

        public static readonly TeleportLocation[] SnipingVantagePoints = new TeleportLocation[]
        {
            // Tallest buildings in Los Santos
            new TeleportLocation("Maze Bank Tower Roof", -75.015f, -818.215f, 326.175f, "Sniping Vantage Points"),
            new TeleportLocation("FIB Building Roof", 136.0f, -749.0f, 262.0f, "Sniping Vantage Points"),
            new TeleportLocation("IAA Building Roof", 117.220f, -620.938f, 206.047f, "Sniping Vantage Points"),
            new TeleportLocation("Mile High Club Construction", -69.858f, -801.687f, 243.386f, "Sniping Vantage Points"),
            new TeleportLocation("Arcadius Tower Roof", -141.29f, -621.15f, 168.82f, "Sniping Vantage Points"),
            new TeleportLocation("Lombank West Roof", -1581.55f, -558.96f, 108.52f, "Sniping Vantage Points"),
            // Natural elevated positions
            new TeleportLocation("Galileo Observatory Dome", -425.517f, 1123.620f, 325.854f, "Sniping Vantage Points"),
            new TeleportLocation("Vinewood Sign Top", 711.362f, 1198.134f, 348.526f, "Sniping Vantage Points"),
            new TeleportLocation("Mt Chiliad Summit", 425.4f, 5614.3f, 766.5f, "Sniping Vantage Points"),
            new TeleportLocation("Mt Gordo Lighthouse Area", 3430.155f, 5174.196f, 41.287f, "Sniping Vantage Points"),
            // Industrial and utility structures
            new TeleportLocation("Sandy Shores Water Tower", 1394.481f, 3609.423f, 39.023f, "Sniping Vantage Points"),
            new TeleportLocation("Sandy Shores Crane", 1051.209f, 2280.452f, 89.727f, "Sniping Vantage Points"),
            new TeleportLocation("El Burro Heights Overlook", 1384.0f, -2057.1f, 52.0f, "Sniping Vantage Points"),
            new TeleportLocation("Land Act Dam Overlook", 1660.37f, -13.97f, 170.62f, "Sniping Vantage Points"),
            // Urban vantage points
            new TeleportLocation("Paleto Bay Lighthouse", -243.89f, 6574.23f, 10.58f, "Sniping Vantage Points"),
            new TeleportLocation("NOOSE Headquarters Roof", 2535.14f, -383.76f, 92.99f, "Sniping Vantage Points")
        };

        #endregion

        #region Teleport Locations - Military and Restricted

        public static readonly TeleportLocation[] MilitaryAndRestricted = new TeleportLocation[]
        {
            new TeleportLocation("Fort Zancudo Main Gate", -2047.4f, 3132.1f, 32.8f, "Military and Restricted"),
            new TeleportLocation("Fort Zancudo ATC Tower", -2344.373f, 3267.498f, 32.811f, "Military and Restricted"),
            new TeleportLocation("Fort Zancudo Hangar", -1843.0f, 2984.0f, 32.8f, "Military and Restricted"),
            new TeleportLocation("Humane Labs Entrance", 3619.7f, 3731.5f, 28.7f, "Military and Restricted"),
            new TeleportLocation("NOOSE Headquarters", 2522.98f, -384.436f, 92.9928f, "Military and Restricted"),
            new TeleportLocation("LS Government Facility", 481.0f, -998.0f, 30.7f, "Military and Restricted")
        };

        #endregion

        #region Teleport Locations - Landmarks

        public static readonly TeleportLocation[] Landmarks = new TeleportLocation[]
        {
            new TeleportLocation("Diamond Casino", 925.329f, 46.152f, 80.908f, "Landmarks"),
            new TeleportLocation("Del Perro Ferris Wheel", -1670.7f, -1125.0f, 13.0f, "Landmarks"),
            new TeleportLocation("Del Perro Pier Entrance", -1850.12f, -1231.82f, 13.02f, "Landmarks"),
            new TeleportLocation("Playboy Mansion", -1475.234f, 167.088f, 55.841f, "Landmarks"),
            new TeleportLocation("Kortz Center", -2243.81f, 264.76f, 174.62f, "Landmarks"),
            new TeleportLocation("Vanilla Unicorn Club", 96.17191f, -1290.668f, 29.26874f, "Landmarks"),
            new TeleportLocation("Union Depository", 2.6f, -667.1f, 16.1f, "Landmarks"),
            new TeleportLocation("Pacific Standard Bank", 235.0f, 216.0f, 106.3f, "Landmarks")
        };

        #endregion

        #region Teleport Locations - Blaine County

        public static readonly TeleportLocation[] BlaineCounty = new TeleportLocation[]
        {
            new TeleportLocation("Sandy Shores Town Center", 1847.0f, 3694.0f, 34.3f, "Blaine County"),
            new TeleportLocation("Grapeseed Main Street", 1702.0f, 4920.0f, 42.1f, "Blaine County"),
            new TeleportLocation("Paleto Bay Town Center", -379.53f, 6118.32f, 31.85f, "Blaine County"),
            new TeleportLocation("Altruist Cult Camp", -1170.841f, 4926.646f, 224.295f, "Blaine County"),
            new TeleportLocation("Hippy Camp", 2476.712f, 3789.645f, 41.226f, "Blaine County"),
            new TeleportLocation("Ron Alternates Wind Farm", 2354.0f, 1830.3f, 101.1f, "Blaine County"),
            new TeleportLocation("Blaine County Savings Bank", -109.299f, 6464.035f, 31.627f, "Blaine County"),
            new TeleportLocation("Beaker's Garage", 116.3748f, 6621.362f, 31.6078f, "Blaine County"),
            new TeleportLocation("Quarry", 2950.55f, 2774.16f, 42.34f, "Blaine County")
        };

        #endregion

        #region Teleport Locations - Coastal and Beaches

        public static readonly TeleportLocation[] CoastalAndBeaches = new TeleportLocation[]
        {
            new TeleportLocation("Vespucci Beach", -1334.0f, -1545.0f, 4.4f, "Coastal and Beaches"),
            new TeleportLocation("Del Perro Beach", -1508.0f, -944.0f, 10.2f, "Coastal and Beaches"),
            new TeleportLocation("Chumash Beach", -3192.6f, 1100.0f, 20.2f, "Coastal and Beaches"),
            new TeleportLocation("Paleto Beach", -274.52f, 6635.83f, 7.39f, "Coastal and Beaches"),
            new TeleportLocation("Elysian Island Port", 338.2f, -2715.9f, 38.5f, "Coastal and Beaches"),
            new TeleportLocation("Jetsam Terminal", 760.4f, -2943.2f, 5.8f, "Coastal and Beaches"),
            new TeleportLocation("Pacific Bluffs", -2183.0f, -424.0f, 13.1f, "Coastal and Beaches")
        };

        #endregion

        #region Teleport Locations - Remote Areas

        public static readonly TeleportLocation[] RemoteAreas = new TeleportLocation[]
        {
            new TeleportLocation("Far North San Andreas", 24.775f, 7644.102f, 19.055f, "Remote Areas"),
            new TeleportLocation("Chiliad Mountain State Wilderness", 2994.917f, 2774.16f, 42.33663f, "Remote Areas"),
            new TeleportLocation("Raton Canyon", -524.0f, 4193.0f, 193.7f, "Remote Areas"),
            new TeleportLocation("Tongva Valley", -1871.0f, 2062.0f, 140.7f, "Remote Areas"),
            new TeleportLocation("Cassidy Creek", -421.0f, 4434.0f, 42.3f, "Remote Areas"),
            new TeleportLocation("Lago Zancudo Swamp", -1340.0f, 2593.0f, 1.8f, "Remote Areas"),
            new TeleportLocation("Tataviam Mountains", 1312.0f, 2168.0f, 101.9f, "Remote Areas")
        };

        #endregion

        #region Teleport Locations - Emergency Services

        public static readonly TeleportLocation[] EmergencyServices = new TeleportLocation[]
        {
            new TeleportLocation("Mission Row Police Station", 436.491f, -982.172f, 30.699f, "Emergency Services"),
            new TeleportLocation("Vinewood Police Station", 638.55f, 1.01f, 82.79f, "Emergency Services"),
            new TeleportLocation("Paleto Bay Sheriff", -437.12f, 6020.56f, 31.49f, "Emergency Services"),
            new TeleportLocation("Sandy Shores Sheriff", 1853.18f, 3686.63f, 34.27f, "Emergency Services"),
            new TeleportLocation("Central LS Medical Center", 297.69f, -584.61f, 43.26f, "Emergency Services"),
            new TeleportLocation("Pillbox Hill Hospital", 360.97f, -585.21f, 28.83f, "Emergency Services"),
            new TeleportLocation("Davis Fire Station", 199.83f, -1643.38f, 29.8f, "Emergency Services")
        };

        #endregion

        #region All Teleport Locations Combined

        /// <summary>
        /// Returns all teleport locations for a given category index
        /// </summary>
        internal static TeleportLocation[] GetTeleportLocationsByCategory(int categoryIndex)
        {
            switch (categoryIndex)
            {
                case 0: return CharacterHouses;
                case 1: return AirportsAndRunways;
                case 2: return SnipingVantagePoints;
                case 3: return MilitaryAndRestricted;
                case 4: return Landmarks;
                case 5: return BlaineCounty;
                case 6: return CoastalAndBeaches;
                case 7: return RemoteAreas;
                case 8: return EmergencyServices;
                default: return CharacterHouses;
            }
        }

        #endregion

        #region Waypoint Destinations

        public static readonly WaypointDestination[] WaypointDestinations = new WaypointDestination[]
        {
            // ===== LS CUSTOMS / GARAGES =====
            new WaypointDestination("LS Customs - Burton", -365.425f, -131.809f, 37.873f),
            new WaypointDestination("LS Customs - La Mesa", 731.5f, -1088.8f, 22.2f),
            new WaypointDestination("LS Customs - Airport", -1135.0f, -1987.0f, 13.2f),
            new WaypointDestination("LS Customs - Harmony (Route 68)", 1175.0f, 2640.0f, 37.8f),
            new WaypointDestination("Beaker's Garage - Paleto", 116.3748f, 6621.362f, 31.6078f),

            // ===== FREEWAYS - LOS SANTOS =====
            new WaypointDestination("Del Perro Freeway - West LS", -1250.0f, -900.0f, 12.0f),
            new WaypointDestination("La Puerta Freeway Bridge", -543.932f, -2225.543f, 122.366f),
            new WaypointDestination("Olympic Freeway - Downtown", -75.0f, -818.0f, 40.0f),
            new WaypointDestination("LS Freeway - Elysian Island", 338.2f, -2715.9f, 38.5f),
            new WaypointDestination("Elysian Fields Freeway", 760.4f, -2943.2f, 5.8f),

            // ===== FREEWAYS - BLAINE COUNTY =====
            new WaypointDestination("Senora Freeway - South Entry", 1384.0f, -2057.1f, 52.0f),
            new WaypointDestination("Senora Freeway - Sandy Shores", 1920.0f, 3780.0f, 32.0f),
            new WaypointDestination("Route 68 - Harmony", 1200.0f, 2650.0f, 37.5f),
            new WaypointDestination("Route 68 - West End", -1630.0f, 2100.0f, 62.0f),
            new WaypointDestination("Great Ocean Highway - Chumash", -3192.6f, 1100.0f, 20.2f),
            new WaypointDestination("Great Ocean Highway - Paleto", -275.522f, 6635.835f, 7.425f),

            // ===== AIRPORTS =====
            new WaypointDestination("LSIA - Main Entrance", -1034.6f, -2733.6f, 13.8f),
            new WaypointDestination("LSIA - Runway", -1336.0f, -3044.0f, 13.9f),
            new WaypointDestination("McKenzie Airfield", 2121.7f, 4796.3f, 41.1f),
            new WaypointDestination("Sandy Shores Airfield", 1747.0f, 3273.7f, 41.1f),

            // ===== PIERS & DOCKS =====
            new WaypointDestination("Del Perro Pier", -1850.127f, -1231.751f, 13.017f),
            new WaypointDestination("Chumash Historic Pier", -3426.683f, 967.738f, 8.347f),
            new WaypointDestination("Paleto Bay Pier", -243.89f, 6574.23f, 10.58f),
            new WaypointDestination("Merryweather Dock", 486.417f, -3339.692f, 6.07f),
            new WaypointDestination("Cargo Ship Dock", 899.678f, -2882.191f, 19.013f),

            // ===== GAS STATIONS =====
            new WaypointDestination("Gas Station - Downtown LS", 265.0f, -1261.0f, 29.3f),
            new WaypointDestination("Gas Station - Mirror Park", 1208.0f, -1402.0f, 35.2f),
            new WaypointDestination("Gas Station - Paleto Bay", 180.0f, 6602.0f, 31.9f),
            new WaypointDestination("Gas Station - Sandy Shores", 1961.0f, 3740.0f, 32.3f),
            new WaypointDestination("Gas Station - Grapeseed", 1687.0f, 4929.0f, 42.1f),
            new WaypointDestination("Procopio Truck Stop", -2555.0f, 2334.0f, 33.1f),

            // ===== MAIN CHARACTER HOUSES =====
            new WaypointDestination("Michael's House", -852.4f, 160.0f, 65.6f),
            new WaypointDestination("Franklin's House - Vinewood", 7.9f, 548.1f, 175.5f),
            new WaypointDestination("Trevor's Trailer", 1985.7f, 3812.2f, 32.2f),
            new WaypointDestination("Floyd's Apartment", -1150.703f, -1520.713f, 10.633f),
            new WaypointDestination("Lester's House", 1273.898f, -1719.304f, 54.771f),

            // ===== LANDMARKS & POINTS OF INTEREST =====
            new WaypointDestination("Maze Bank Tower", -75.015f, -818.215f, 326.176f),
            new WaypointDestination("Vinewood Sign", 711.362f, 1198.134f, 348.526f),
            new WaypointDestination("Galileo Observatory", -438.804f, 1076.097f, 352.411f),
            new WaypointDestination("Casino", 925.329f, 46.152f, 80.908f),
            new WaypointDestination("Ferris Wheel", -1670.7f, -1125.0f, 13.0f),
            new WaypointDestination("Kortz Center", -2243.810f, 264.048f, 174.615f),
            new WaypointDestination("University of San Andreas", -1696.866f, 142.747f, 64.372f),
            new WaypointDestination("Los Santos Golf Club", -1336.715f, 59.051f, 55.246f),
            new WaypointDestination("Oriental Theater", 293.089f, 180.466f, 104.301f),
            new WaypointDestination("Richman Hotel", -1330.911f, 340.871f, 64.078f),

            // ===== MILITARY & RESTRICTED =====
            new WaypointDestination("Fort Zancudo - Main Gate", -2047.4f, 3132.1f, 32.8f),
            new WaypointDestination("Fort Zancudo - ATC", -2344.373f, 3267.498f, 32.811f),
            new WaypointDestination("NOOSE Headquarters", 2535.243f, -383.799f, 92.993f),
            new WaypointDestination("Humane Labs Entrance", 3619.749f, 3731.5f, 28.690f),

            // ===== EMERGENCY SERVICES =====
            new WaypointDestination("Police Station - Mission Row", 436.491f, -982.172f, 30.699f),
            new WaypointDestination("Hospital - Pillbox Hill", 356.822f, -590.151f, 43.315f),
            new WaypointDestination("Coroner Office", 243.351f, -1376.014f, 39.534f),

            // ===== BLAINE COUNTY DESTINATIONS =====
            new WaypointDestination("Mount Chiliad Peak", 450.718f, 5566.614f, 806.183f),
            new WaypointDestination("Altruist Cult Camp", -1170.841f, 4926.646f, 224.295f),
            new WaypointDestination("Hippy Camp", 2476.712f, 3789.645f, 41.226f),
            new WaypointDestination("Stab City", 126.975f, 3714.419f, 46.827f),
            new WaypointDestination("Weed Farm", 2208.777f, 5578.235f, 53.735f),
            new WaypointDestination("O'Neil Ranch", 2441.216f, 4968.585f, 51.707f),
            new WaypointDestination("Quarry", 2954.196f, 2783.410f, 41.004f),
            new WaypointDestination("Wind Farm", 2354.0f, 1830.3f, 101.1f),
            new WaypointDestination("Satellite Dishes", 2062.123f, 2942.055f, 47.431f),
            new WaypointDestination("Rebel Radio", 736.153f, 2583.143f, 79.634f),
            new WaypointDestination("Sandy Shores Crane", 1051.209f, 2280.452f, 89.727f),
            new WaypointDestination("Airplane Graveyard", 2395.096f, 3049.616f, 60.053f),
            new WaypointDestination("Land Act Dam", 1660.369f, -12.013f, 170.020f),

            // ===== COASTAL DRIVES =====
            new WaypointDestination("Pacific Bluffs Country Club", -3022.222f, 39.968f, 13.611f),
            new WaypointDestination("Playboy Mansion", -1475.234f, 167.088f, 55.841f),
            new WaypointDestination("Beach Skatepark", -1374.881f, -1398.835f, 6.141f),
            new WaypointDestination("Marlowe Vineyards", -1868.971f, 2095.674f, 139.115f),
            new WaypointDestination("Mount Gordo", 2877.633f, 5911.078f, 369.624f),
            new WaypointDestination("El Gordo Lighthouse", 3430.155f, 5174.196f, 41.280f),

            // ===== BANKS =====
            new WaypointDestination("Pacific Standard Bank", 235.046f, 216.434f, 106.287f),
            new WaypointDestination("Blaine County Savings Bank", -109.299f, 6464.035f, 31.627f),

            // ===== ENTERTAINMENT =====
            new WaypointDestination("Strip Club", 96.17191f, -1290.668f, 29.26874f),
            new WaypointDestination("Vanilla Unicorn Parking", 126.135f, -1278.583f, 29.270f),
            new WaypointDestination("Vinewood Bowl", 686.245f, 577.950f, 130.461f),
            new WaypointDestination("Sisyphus Theater", 205.316f, 1167.378f, 227.005f),
            new WaypointDestination("Maze Bank Arena", -324.300f, -1968.545f, 67.002f),

            // ===== INDUSTRIAL =====
            new WaypointDestination("Power Station Chimney", 2732.931f, 1577.540f, 83.671f),
            new WaypointDestination("Paleto Sawmill", -549.467f, 5308.221f, 114.146f),
            new WaypointDestination("Slaughterhouse", -80.557f, 6220.006f, 31.090f),
            new WaypointDestination("Trevor's Meth Lab", 1391.773f, 3608.716f, 38.942f),

            // ===== EXTREME NORTH/SOUTH =====
            new WaypointDestination("Far North San Andreas", 24.775f, 7644.102f, 19.055f),
            new WaypointDestination("Chiliad Mountain Wilderness", 2994.917f, 2774.16f, 42.33663f),
            new WaypointDestination("Calafia Train Bridge", -517.869f, 4425.284f, 89.795f),

            // ===== NEIGHBORHOODS =====
            new WaypointDestination("Little Seoul", -889.655f, -853.499f, 20.566f),
            new WaypointDestination("Little Portola", -635.463f, -242.402f, 38.175f),
            new WaypointDestination("Mirror Park", 1070.206f, -711.958f, 58.483f),
            new WaypointDestination("Epsilon Building", -695.025f, 82.955f, 55.855f),
            new WaypointDestination("Vinewood Cemetery", -1659.0f, -128.399f, 59.954f),

            // ===== BOATS & YACHTS =====
            new WaypointDestination("Yacht", -2023.661f, -1038.038f, 5.577f),
            new WaypointDestination("Aircraft Carrier", 3069.330f, -4704.220f, 15.043f)
        };

        #endregion
    }
}
