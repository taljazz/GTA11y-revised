using System;
using System.Collections.Generic;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Centralized constants for the GTA11Y mod to avoid magic numbers
    /// </summary>
    public static class Constants
    {
        // Tick intervals (in .NET ticks, 10,000 ticks = 1ms)
        public const long TICK_INTERVAL_VEHICLE_SPEED = 25_000_000;      // 2.5 seconds
        public const long TICK_INTERVAL_TARGET = 2_000_000;              // 0.2 seconds
        public const long TICK_INTERVAL_STREET_CHECK = 5_000_000;        // 0.5 seconds
        public const long TICK_INTERVAL_ZONE_CHECK = 5_000_000;          // 0.5 seconds
        public const long TICK_INTERVAL_ALTITUDE = 1_000_000;            // 0.1 seconds
        public const long TICK_INTERVAL_PITCH = 500_000;                 // 0.05 seconds
        public const long TICK_INTERVAL_TOLK_HEALTH = 100_000_000;       // 10 seconds

        // Search radii
        public const float NEARBY_ENTITY_RADIUS = 50f;
        public const float NEARBY_VEHICLE_EXPLODE_RADIUS = 100f;
        public const float NEARBY_PED_COMBAT_RADIUS = 5000f;

        // Menu navigation
        public const int VEHICLE_SPAWN_FAST_SCROLL_AMOUNT = 25;

        // Conversion factors
        public const double METERS_PER_SECOND_TO_MPH = 2.23694;

        // Altitude indicator audio
        public const double ALTITUDE_GAIN = 0.1;
        public const double ALTITUDE_BASE_FREQUENCY = 120.0;
        public const double ALTITUDE_FREQUENCY_MULTIPLIER = 40.0;
        public const double ALTITUDE_DURATION_SECONDS = 0.075;

        // Pitch indicator audio
        public const double PITCH_GAIN = 0.08;
        public const double PITCH_BASE_FREQUENCY = 600.0;
        public const double PITCH_FREQUENCY_MULTIPLIER = 6.0;
        public const double PITCH_DURATION_SECONDS = 0.025;

        // Height change threshold (meters)
        public const float HEIGHT_CHANGE_THRESHOLD = 1f;

        // Pitch change threshold (degrees)
        public const float PITCH_CHANGE_THRESHOLD = 1f;

        // Aircraft altitude mode thresholds
        public const float AIRCRAFT_ALTITUDE_THRESHOLD = 500f;     // feet - switch from fine to coarse
        public const float AIRCRAFT_ALTITUDE_FINE_INTERVAL = 50f;  // feet - announce every 50ft below threshold
        public const float AIRCRAFT_ALTITUDE_COARSE_INTERVAL = 500f; // feet - announce every 500ft above threshold
        public const float METERS_TO_FEET = 3.28084f;
        public const float METERS_TO_MILES = 0.000621371f;         // Conversion factor

        // Altitude mode values
        public const int ALTITUDE_MODE_OFF = 0;
        public const int ALTITUDE_MODE_NORMAL = 1;
        public const int ALTITUDE_MODE_AIRCRAFT = 2;

        // Aircraft attitude indicator - base check interval
        public const long TICK_INTERVAL_AIRCRAFT_ATTITUDE = 500_000;    // 0.05 seconds (check frequently)

        // Aircraft type classification
        public const int AIRCRAFT_TYPE_FIXED_WING = 0;
        public const int AIRCRAFT_TYPE_HELICOPTER = 1;
        public const int AIRCRAFT_TYPE_BLIMP = 2;
        public const int AIRCRAFT_TYPE_VTOL_HOVER = 3;   // VTOL in hover mode (like heli)
        public const int AIRCRAFT_TYPE_VTOL_PLANE = 4;   // VTOL in plane mode (like fixed-wing)

        // VTOL mode threshold (nozzle position)
        public const float VTOL_HOVER_THRESHOLD = 0.5f;  // Above 0.5 = hover mode

        // Fixed-wing attitude thresholds (degrees)
        public const float FIXED_WING_ANGLE_LEVEL = 5f;       // 0-5° = level (silent)
        public const float FIXED_WING_ANGLE_SLIGHT = 15f;     // 5-15° = slight tilt
        public const float FIXED_WING_ANGLE_MODERATE = 30f;   // 15-30° = moderate tilt
        // Above 30° = steep tilt

        // Helicopter attitude thresholds (tighter - more sensitive)
        public const float HELI_ANGLE_LEVEL = 3f;             // 0-3° = level (silent)
        public const float HELI_ANGLE_SLIGHT = 10f;           // 3-10° = slight tilt
        public const float HELI_ANGLE_MODERATE = 20f;         // 10-20° = moderate tilt
        // Above 20° = steep/dangerous

        // Blimp attitude thresholds (tightest - very sensitive)
        public const float BLIMP_ANGLE_LEVEL = 2f;            // 0-2° = level (silent)
        public const float BLIMP_ANGLE_SLIGHT = 5f;           // 2-5° = slight tilt
        public const float BLIMP_ANGLE_MODERATE = 10f;        // 5-10° = moderate tilt
        // Above 10° = steep for a blimp

        // Inverted detection threshold (fixed-wing only)
        public const float INVERTED_ROLL_THRESHOLD = 90f;     // Roll > 90° or < -90° = inverted

        // Aircraft attitude pulse intervals (ticks) - varies with angle
        public const long AIRCRAFT_PULSE_SILENT = long.MaxValue;        // No pulse (level)
        public const long AIRCRAFT_PULSE_SLOW = 5_000_000;              // 0.5 seconds (slight)
        public const long AIRCRAFT_PULSE_MEDIUM = 2_500_000;            // 0.25 seconds (moderate)
        public const long AIRCRAFT_PULSE_RAPID = 1_000_000;             // 0.1 seconds (steep)

        // Aircraft pitch audio (center channel)
        public const double AIRCRAFT_PITCH_GAIN = 0.08;
        public const double AIRCRAFT_PITCH_BASE_FREQUENCY = 400.0;      // Hz at level
        public const double AIRCRAFT_PITCH_FREQUENCY_MULTIPLIER = 8.0;  // Hz per degree

        // Aircraft roll audio (stereo panned)
        public const double AIRCRAFT_ROLL_GAIN = 0.06;
        public const double AIRCRAFT_ROLL_FREQUENCY = 300.0;            // Hz (constant)
        public const double AIRCRAFT_ROLL_DURATION_SECONDS = 0.08;

        // Compass directions (degrees)
        public const double NORTH = 0;
        public const double NORTH_NORTHEAST = 22.5;
        public const double NORTHEAST = 45;
        public const double EAST_NORTHEAST = 67.5;
        public const double EAST = 90;
        public const double EAST_SOUTHEAST = 112.5;
        public const double SOUTHEAST = 135;
        public const double SOUTH_SOUTHEAST = 157.5;
        public const double SOUTH = 180;
        public const double SOUTH_SOUTHWEST = 202.5;
        public const double SOUTHWEST = 225;
        public const double WEST_SOUTHWEST = 247.5;
        public const double WEST = 270;
        public const double WEST_NORTHWEST = 292.5;
        public const double NORTHWEST = 315;
        public const double NORTH_NORTHWEST = 337.5;

        // Heading slices (8 directions)
        public const int HEADING_SLICE_COUNT = 8;
        public const double HEADING_SLICE_DEGREES = 45.0;

        // File paths
        public const string HASH_FILE_PATH = "scripts/hashes.txt";
        public const string AUDIO_TPED_PATH = "scripts/tped.wav";
        public const string AUDIO_TVEHICLE_PATH = "scripts/tvehicle.wav";
        public const string AUDIO_TPROP_PATH = "scripts/tprop.wav";
        public const string SETTINGS_FILE_NAME = "gta11ySettings.json";
        public const string SETTINGS_FOLDER_PATH = "/Rockstar Games/GTA V/ModSettings";

        // Player model names (to exclude from NPC lists)
        public static readonly string[] PLAYER_MODELS = { "player_zero", "player_one", "player_two" };

        // VTOL-capable vehicle model hashes (use HashSet for O(1) lookup)
        public static readonly HashSet<int> VTOL_VEHICLE_HASHES = new HashSet<int>
        {
            970385471,    // Hydra
            -2007026063,  // Avenger
            -1600252419,  // Avenger2
            1043222410,   // Tula
            -1858699125   // Starling (has some VTOL capability)
        };

        // Blimp vehicle model hashes (use HashSet for O(1) lookup)
        public static readonly HashSet<int> BLIMP_VEHICLE_HASHES = new HashSet<int>
        {
            -150975354,   // Blimp
            -613725916,   // Blimp2
            -1093422249   // Blimp3
        };

        // Native function hash for VTOL nozzle position (not in all SHVDN versions)
        public const ulong NATIVE_GET_VEHICLE_FLIGHT_NOZZLE_POSITION = 0xDA62027C8BDB326E;

        // Vehicle save/load
        public const int VEHICLE_SAVE_SLOT_COUNT = 10;
        public const string SAVED_VEHICLES_FILE_NAME = "gta11ySavedVehicles.json";

        // Vehicle mod menu display names
        public static readonly Dictionary<int, string> MOD_TYPE_NAMES = new Dictionary<int, string>
        {
            { 0, "Spoiler" },
            { 1, "Front Bumper" },
            { 2, "Rear Bumper" },
            { 3, "Side Skirt" },
            { 4, "Exhaust" },
            { 5, "Frame" },
            { 6, "Grille" },
            { 7, "Hood" },
            { 8, "Left Fender/Wing" },
            { 9, "Right Fender/Wing" },
            { 10, "Roof" },
            { 11, "Engine" },
            { 12, "Brakes" },
            { 13, "Transmission" },
            { 14, "Horn" },
            { 15, "Suspension" },
            { 16, "Armor" },
            { 18, "Turbo" },
            { 22, "Xenon Headlights" },
            { 23, "Front Wheels" },
            { 24, "Rear Wheels" },
            { 25, "Plate Holder" },
            { 26, "Vanity Plates" },
            { 27, "Trim" },
            { 28, "Ornaments" },
            { 29, "Dashboard" },
            { 30, "Dial" },
            { 31, "Door Speaker" },
            { 32, "Seats" },
            { 33, "Steering Wheel" },
            { 34, "Shifter Lever" },
            { 35, "Plaques" },
            { 36, "Speakers" },
            { 37, "Trunk" },
            { 38, "Hydraulics" },
            { 39, "Engine Block" },
            { 40, "Air Filter" },
            { 41, "Struts" },
            { 42, "Arch Cover" },
            { 43, "Aerials" },
            { 44, "Trim Design" },
            { 45, "Tank" },
            { 46, "Windows" },
            { 48, "Livery" }
        };

        // Wheel type names (index matches VehicleWheelType enum value)
        // VehicleWheelType: Sport=0, Muscle=1, Lowrider=2, SUV=3, Offroad=4, Tuner=5,
        // BikeWheels=6, HighEnd=7, BennysOriginals=8, BennysBespoke=9, OpenWheel=10, Street=11, Track=12
        public static readonly string[] WHEEL_TYPE_NAMES = new string[]
        {
            "Sport",            // 0
            "Muscle",           // 1
            "Lowrider",         // 2
            "SUV",              // 3
            "Offroad",          // 4
            "Tuner",            // 5
            "Bike Wheels",      // 6
            "High End",         // 7
            "Bennys Originals", // 8
            "Bennys Bespoke",   // 9
            "Open Wheel",       // 10
            "Street",           // 11
            "Track"             // 12
        };

        // Special mod type values for custom categories
        public const int MOD_TYPE_NEONS = -2;
        public const int MOD_TYPE_WHEEL_TYPE = -3;
        public const int WHEEL_TYPE_COUNT = 13;  // 0-12

        // Weaponized vehicle model names (lowercase for case-insensitive matching)
        // These vehicles have built-in weapons like missiles, guns, or turrets
        public static readonly HashSet<string> WEAPONIZED_VEHICLE_NAMES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Aircraft
            "akula", "annihilator", "annihilator2", "avenger", "avenger2", "b11",
            "bombushka", "buzzard", "buzzard2", "hunter", "hydra", "lazer",
            "mogul", "molotok", "nokota", "pyro", "rogue", "savage", "seabreeze",
            "seasparrow", "seasparrow2", "seasparrow3", "starling", "strikeforce",
            "tula", "valkyrie", "valkyrie2", "volatol", "alkonost", "raiju",

            // Cars & Ground Vehicles
            "apc", "ardent", "barrage", "bruiser", "bruiser2", "bruiser3",
            "brutus", "brutus2", "brutus3", "cerberus", "cerberus2", "cerberus3",
            "chernobog", "deluxo", "dominator4", "dominator5", "dominator6",
            "dune3", "dune4", "dune5", "halftrack", "imperator", "imperator2", "imperator3",
            "insurgent", "insurgent2", "insurgent3", "issi4", "issi5", "issi6",
            "jb7002", "khanjali", "menacer", "minitank", "monster3", "monster4", "monster5",
            "nightshark", "rcbandito", "revolter", "rhino", "ruiner2", "ruiner3",
            "sasquatch", "sasquatch2", "scarab", "scarab2", "scarab3", "scramjet",
            "slamvan4", "slamvan5", "slamvan6", "speedo4", "stromberg", "tampa3",
            "technical", "technical2", "technical3", "terrorbyte", "thruster",
            "toreador", "trailersmall2", "vigilante", "viseris", "zr380", "zr3802", "zr3803",
            "paragon2", "patriot3", "deity", "buffalo4", "champion", "jubilee", "granger2",
            "ignus2", "weaponized", "conada", "dinghy5",

            // Motorcycles
            "deathbike", "deathbike2", "deathbike3", "oppressor", "oppressor2",

            // Boats
            "dinghy5", "patrolboat", "seabreeze",

            // Other
            "turretlimo", "pounder2", "mule4", "boxville5", "speedo4", "rcv"
        };

        // ===== AUTODRIVE SYSTEM =====

        // AutoDrive tick intervals
        public const long TICK_INTERVAL_AUTODRIVE_UPDATE = 2_000_000;     // 0.2s for navigation updates
        public const long TICK_INTERVAL_ROAD_FEATURE = 5_000_000;         // 0.5s for road feature checks

        // AutoDrive parameters
        public const float AUTODRIVE_DEFAULT_SPEED = 15f;                 // m/s (~33 mph)
        public const float AUTODRIVE_MIN_SPEED = 5f;                      // m/s (~11 mph)
        public const float AUTODRIVE_MAX_SPEED = 40f;                     // m/s (~89 mph)
        public const float AUTODRIVE_SPEED_INCREMENT = 2f;                // m/s per key press
        public const float AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS = 10f;        // meters - tighter for mission markers
        public const float AUTODRIVE_FINAL_APPROACH_DISTANCE = 50f;        // meters - start final approach
        public const float AUTODRIVE_FINAL_APPROACH_SPEED = 8f;            // m/s (~18 mph) for precise arrival
        public const float AUTODRIVE_PRECISE_ARRIVAL_RADIUS = 6f;          // meters - very close to destination

        // Native for finding safe road position near waypoint
        public const ulong NATIVE_GET_CLOSEST_VEHICLE_NODE = 0x240A18690AE96513;
        public const ulong NATIVE_GET_NTH_CLOSEST_VEHICLE_NODE = 0xE50E52416CCF948B;  // For alternate road nodes
        public const ulong NATIVE_GET_SAFE_COORD_FOR_PED = 0xB61C8E878A4199CA;
        public const ulong NATIVE_GET_POINT_ON_ROAD_SIDE = 0x16F46FB18C8009E4;

        // ===== DRIVING STYLES =====

        // Driving style mode indices
        public const int DRIVING_STYLE_MODE_CAUTIOUS = 0;
        public const int DRIVING_STYLE_MODE_NORMAL = 1;
        public const int DRIVING_STYLE_MODE_FAST = 2;
        public const int DRIVING_STYLE_MODE_RECKLESS = 3;

        // Driving style flag values (optimized for human-like driving)
        // Flag breakdown:
        //   StopForVehicles (1), StopForPeds (2), SwerveAroundAllVehicles (4)
        //   SteerAroundStationaryVehicles (8), SteerAroundPeds (16), SteerAroundObjects (32)
        //   StopAtTrafficLights (128), GoOffRoadWhenAvoiding (256), AllowGoingWrongWay (512)
        //   Reverse (1024), UseWanderFallbackInsteadOfStraightLine (2048) - cruise randomly if pathfinding fails
        //   AvoidRestrictedAreas (4096), AdjustCruiseSpeedBasedOnRoadSpeed (16384)
        //   UseShortCutLinks (262144), ChangeLanesAroundObstructions (524288)
        //   UseStringPullingAtJunctions (33554432) - smoother, more natural turns
        //   ForceJoinInRoadDirection (1073741824) - join roads in correct lane direction
        //
        // Cautious: All safety flags + pathfinding fallback + avoid restricted + smooth turns + correct lane join
        public const int DRIVING_STYLE_CAUTIOUS = 1108105407;   // 34357439 + 2048 + 4096 + 1073741824
        // Normal: Balanced safety + pathfinding fallback + avoid restricted + smooth turns + correct lane join
        public const int DRIVING_STYLE_NORMAL = 1108105403;     // 34357435 + 2048 + 4096 + 1073741824
        // Fast: Ignore lights, pathfinding fallback + smooth turns + correct lane join (no restricted area check)
        public const int DRIVING_STYLE_FAST = 1108086565;       // 34342693 + 2048 + 1073741824
        // Reckless: Ram through + pathfinding fallback + smooth turns + correct lane join
        // Does NOT include StopForVehicles, SwerveAroundAllVehicles, or AvoidRestrictedAreas
        public const int DRIVING_STYLE_RECKLESS = 1108086304;   // 34342432 + 2048 + 1073741824

        // Legacy constant (kept for compatibility)
        public const int DRIVING_STYLE_RUSHED = 1074528293;

        // Driving style values array (indexed by mode)
        public static readonly int[] DRIVING_STYLE_VALUES = new int[]
        {
            DRIVING_STYLE_CAUTIOUS,   // 0 - Cautious
            DRIVING_STYLE_NORMAL,     // 1 - Normal
            DRIVING_STYLE_FAST,       // 2 - Fast
            DRIVING_STYLE_RECKLESS    // 3 - Reckless
        };

        // Driver ability per style (0.0 - 1.0)
        // Lower values = more human-like imperfection (slight steering variation, less perfect lane centering)
        // Research shows ability 1.0 = "too perfect", feels robotic
        // Values below create natural variation while still being competent
        public static readonly float[] DRIVING_STYLE_ABILITIES = new float[]
        {
            0.5f,   // Cautious - hesitant, less confident (like nervous driver)
            0.7f,   // Normal - average driver skill, some imperfection
            0.85f,  // Fast - skilled but not perfect
            0.95f   // Reckless - high skill (needs precision for ramming)
        };

        // Driver aggressiveness per style (0.0 - 1.0)
        // Higher values = more assertive lane changes, closer following, quicker reactions
        public static readonly float[] DRIVING_STYLE_AGGRESSIVENESS = new float[]
        {
            0.1f,   // Cautious - very passive, gives way often
            0.4f,   // Normal - balanced, typical urban driver
            0.65f,  // Fast - pushier, less patient
            0.9f    // Reckless - very aggressive, minimal hesitation
        };

        // Display names for menu
        public static readonly string[] DRIVING_STYLE_NAMES = new string[]
        {
            "Cautious",      // Maximum safety, obey traffic
            "Normal",        // Balanced driving
            "Fast",          // Ignore traffic lights, still avoid collisions
            "Reckless"       // Ram through vehicles, for tanks and heavy vehicles
        };

        // Native for changing driving style mid-task (without re-issuing)
        public const ulong NATIVE_SET_DRIVE_TASK_DRIVING_STYLE = 0xDACE1BE37D88AF67;

        // Road feature detection - Dynamic lookahead based on speed
        public const float ROAD_LOOKAHEAD_MIN = 50f;                      // Minimum lookahead 50m (at slow speeds)
        public const float ROAD_LOOKAHEAD_MAX = 200f;                     // Maximum lookahead 200m (at high speeds)
        public const float ROAD_LOOKAHEAD_SPEED_FACTOR = 4f;              // Lookahead = speed * factor (clamped to min/max)
        public const float ROAD_SAMPLE_INTERVAL = 20f;                    // Sample every 20m
        public const float CURVE_HEADING_THRESHOLD = 25f;                 // >25 degree = curve
        public const float SHARP_CURVE_THRESHOLD = 45f;                   // >45 degree = sharp curve

        // Speed-scaled announcement cooldowns
        public const long ROAD_FEATURE_COOLDOWN_MIN = 20_000_000;         // 2 seconds at high speed
        public const long ROAD_FEATURE_COOLDOWN_MAX = 50_000_000;         // 5 seconds at low speed
        public const float ROAD_FEATURE_COOLDOWN_SPEED_THRESHOLD = 20f;   // Speed (m/s) for minimum cooldown
        public const float ROAD_FEATURE_MIN_SPEED = 5f;                   // m/s - skip when slow

        // Proactive curve slowdown - speed-dependent distances
        // At 40 m/s (90 mph), need 4+ seconds to safely slow down
        public const float CURVE_SLOWDOWN_FACTOR = 0.6f;                  // Reduce to 60% speed for curves
        public const float SHARP_CURVE_SLOWDOWN_FACTOR = 0.4f;            // Reduce to 40% speed for sharp curves
        public const float CURVE_SLOWDOWN_DISTANCE = 80f;                 // Start slowing this far before curve (legacy)
        public const float CURVE_SLOWDOWN_DISTANCE_BASE = 100f;           // Base distance to start slowing
        public const float CURVE_SLOWDOWN_DISTANCE_SPEED_FACTOR = 4.0f;   // Multiply speed by this for lookahead
        public const float CURVE_SLOWDOWN_DISTANCE_MIN = 50f;             // Minimum slowdown distance
        public const float CURVE_SLOWDOWN_DISTANCE_MAX = 200f;            // Maximum slowdown distance
        public const long CURVE_SLOWDOWN_DURATION = 40_000_000;           // 4 seconds of reduced speed

        // Smooth arrival deceleration (waypoint mode)
        public const float ARRIVAL_SLOWDOWN_DISTANCE = 100f;              // Start slowing 100m from destination
        public const float ARRIVAL_FINAL_SPEED = 5f;                      // Final approach speed (m/s, ~11 mph)
        public const float ARRIVAL_SPEED_FACTOR = 0.3f;                   // Speed = distance * factor (gradual slowdown)

        // Native function hashes for AutoDrive
        public const ulong NATIVE_TASK_VEHICLE_DRIVE_WANDER = 0x480142959D337D00;
        public const ulong NATIVE_TASK_VEHICLE_DRIVE_TO_COORD = 0xE2A2AA2F659D77A7;
        public const ulong NATIVE_SET_DRIVE_TASK_CRUISE_SPEED = 0x5C9B84BD7D31D908;
        public const ulong NATIVE_SET_DRIVER_ABILITY = 0xB195FFA8042FC5C3;
        public const ulong NATIVE_SET_DRIVER_AGGRESSIVENESS = 0xA731F608CA104E3C;
        public const ulong NATIVE_GET_CLOSEST_VEHICLE_NODE_WITH_HEADING = 0xFF071FB798B803B0;
        public const ulong NATIVE_GET_VEHICLE_NODE_PROPERTIES = 0x0568566ACBB5DEDC;
        public const ulong NATIVE_GENERATE_DIRECTIONS_TO_COORD = 0xF90125F1F79ECDF8;
        public const ulong NATIVE_CLEAR_PED_TASKS = 0xE1EF3C1216AFF2CD;

        // ===== ROAD TYPE DETECTION =====

        // Road node flags (from GET_VEHICLE_NODE_PROPERTIES)
        public const int ROAD_FLAG_OFF_ROAD = 1;           // Bit 0: Dirt roads, trails, alleys
        public const int ROAD_FLAG_TUNNEL = 16;            // Bit 4: Tunnel or interior
        public const int ROAD_FLAG_DEAD_END = 32;          // Bit 5: Dead end road
        public const int ROAD_FLAG_HIGHWAY = 64;           // Bit 6: Highway/freeway
        public const int ROAD_FLAG_JUNCTION = 128;         // Bit 7: Intersection
        public const int ROAD_FLAG_TRAFFIC_LIGHT = 256;    // Bit 8: Has traffic light
        public const int ROAD_FLAG_GIVE_WAY = 512;         // Bit 9: Yield sign

        // Road density thresholds (0-15 from GET_VEHICLE_NODE_PROPERTIES)
        public const int ROAD_DENSITY_RURAL_MAX = 3;       // 0-3 = Rural/empty
        public const int ROAD_DENSITY_SUBURBAN_MAX = 7;    // 4-7 = Suburban/light traffic

        // Road type classification values
        public const int ROAD_TYPE_UNKNOWN = 0;
        public const int ROAD_TYPE_HIGHWAY = 1;
        public const int ROAD_TYPE_CITY_STREET = 2;
        public const int ROAD_TYPE_SUBURBAN = 3;
        public const int ROAD_TYPE_RURAL = 4;
        public const int ROAD_TYPE_DIRT_TRAIL = 5;
        public const int ROAD_TYPE_TUNNEL = 6;

        // Road type display names (indexed by ROAD_TYPE_* values)
        public static readonly string[] ROAD_TYPE_NAMES = new string[]
        {
            "unknown road",    // 0
            "highway",         // 1
            "city street",     // 2
            "suburban road",   // 3
            "rural road",      // 4
            "dirt road",       // 5
            "tunnel"           // 6
        };

        // Road type detection tick interval
        public const long TICK_INTERVAL_ROAD_TYPE_CHECK = 10_000_000;    // 1.0s
        public const long ROAD_TYPE_ANNOUNCE_COOLDOWN = 100_000_000;     // 10s between same type announcements

        // Road type speed multipliers (indexed by ROAD_TYPE_* values)
        // Multiplied by target speed to get appropriate speed for road type
        public static readonly float[] ROAD_TYPE_SPEED_MULTIPLIERS = new float[]
        {
            0.8f,   // 0 - Unknown: cautious
            1.0f,   // 1 - Highway: full speed
            0.65f,  // 2 - City street: slower for pedestrians/traffic
            0.8f,   // 3 - Suburban: moderate
            0.85f,  // 4 - Rural: slightly slower
            0.5f,   // 5 - Dirt road: much slower
            0.7f    // 6 - Tunnel: cautious
        };

        // Granular arrival distance announcements (in feet)
        public static readonly int[] ARRIVAL_ANNOUNCEMENT_DISTANCES = new int[]
        {
            500, 200, 100, 50
        };

        // Traffic light state detection
        public const float TRAFFIC_LIGHT_STOP_SPEED = 0.5f;              // m/s - considered stopped
        public const float TRAFFIC_LIGHT_DETECTION_RADIUS = 30f;         // meters to check for lights
        public const long TRAFFIC_LIGHT_STATE_COOLDOWN = 30_000_000;     // 3 seconds between state announcements

        // U-turn detection
        public const float UTURN_HEADING_THRESHOLD = 150f;               // degrees - heading change for U-turn
        public const float UTURN_DISTANCE_THRESHOLD = 30f;               // meters - distance over which to measure
        public const long UTURN_ANNOUNCE_COOLDOWN = 50_000_000;          // 5 seconds between U-turn announcements

        // Hill/gradient detection
        public const float HILL_STEEP_THRESHOLD = 8f;                    // degrees - steep hill
        public const float HILL_MODERATE_THRESHOLD = 4f;                 // degrees - moderate hill
        public const float HILL_DETECTION_DISTANCE = 50f;                // meters ahead to check
        public const long HILL_ANNOUNCE_COOLDOWN = 50_000_000;           // 5 seconds between hill announcements

        // Announcement priority levels (higher = more important, but lower number = higher priority)
        public const int ANNOUNCE_PRIORITY_CRITICAL = 0;     // Recovery, arrival, errors
        public const int ANNOUNCE_PRIORITY_HIGH = 1;         // Traffic lights, hills
        public const int ANNOUNCE_PRIORITY_MEDIUM = 2;       // Curves, intersections
        public const int ANNOUNCE_PRIORITY_LOW = 3;          // Distance updates, road type
        public const long ANNOUNCE_MIN_GAP = 5_000_000;      // 0.5 seconds minimum between announcements

        // Per-priority cooldown durations (ticks)
        public const long ANNOUNCE_COOLDOWN_CRITICAL = 5_000_000;    // 0.5 seconds
        public const long ANNOUNCE_COOLDOWN_HIGH = 20_000_000;       // 2 seconds
        public const long ANNOUNCE_COOLDOWN_MEDIUM = 30_000_000;     // 3 seconds
        public const long ANNOUNCE_COOLDOWN_LOW = 50_000_000;        // 5 seconds
        public const long ANNOUNCE_GLOBAL_COOLDOWN = 5_000_000;      // 0.5 second minimum between any announcements

        // ===== ROAD SEEKING =====

        // Seek modes
        public const int ROAD_SEEK_MODE_ANY = 0;
        public const int ROAD_SEEK_MODE_HIGHWAY = 1;
        public const int ROAD_SEEK_MODE_CITY = 2;
        public const int ROAD_SEEK_MODE_SUBURBAN = 3;
        public const int ROAD_SEEK_MODE_RURAL = 4;
        public const int ROAD_SEEK_MODE_DIRT = 5;

        // Seek mode display names
        public static readonly string[] ROAD_SEEK_MODE_NAMES = new string[]
        {
            "Any Road",
            "Highway",
            "City Street",
            "Suburban Road",
            "Rural Road",
            "Dirt Road"
        };

        // Road seeking parameters
        public const long TICK_INTERVAL_ROAD_SEEK_SCAN = 30_000_000;     // 3.0s between scans
        public const float ROAD_SEEK_SCAN_RADIUS = 300f;                 // Search radius in meters
        public const float ROAD_SEEK_SAMPLE_ANGLE = 30f;                 // Sample every 30 degrees
        public const float ROAD_SEEK_SAMPLE_DISTANCE = 50f;              // Sample every 50m outward
        public const int ROAD_SEEK_MAX_SAMPLES = 72;                     // 12 angles * 6 distances
        public const float ROAD_SEEK_ARRIVAL_THRESHOLD = 30f;            // Within 30m = arrived

        // ===== AUTODRIVE RECOVERY SYSTEM =====

        // Recovery check intervals
        public const long TICK_INTERVAL_RECOVERY_CHECK = 5_000_000;       // 0.5s between recovery checks
        public const long TICK_INTERVAL_STUCK_CHECK = 10_000_000;         // 1.0s between stuck checks
        public const long TICK_INTERVAL_PROGRESS_CHECK = 20_000_000;      // 2.0s between progress checks

        // Stuck detection thresholds
        public const float STUCK_MOVEMENT_THRESHOLD = 2f;                 // Less than 2m movement = potentially stuck
        public const float STUCK_SPEED_THRESHOLD = 1f;                    // Less than 1 m/s = very slow/stopped
        public const int STUCK_CHECK_COUNT_THRESHOLD = 3;                 // 3 consecutive checks = stuck (3 seconds)
        public const float STUCK_HEADING_CHANGE_THRESHOLD = 5f;           // Heading change < 5° while stuck = truly stuck

        // Progress timeout (waypoint mode)
        public const long PROGRESS_TIMEOUT_TICKS = 300_000_000;           // 30 seconds without progress = timeout
        public const float PROGRESS_DISTANCE_THRESHOLD = 10f;             // Must get 10m closer within timeout

        // Vehicle state thresholds
        public const float VEHICLE_UPRIGHT_THRESHOLD = 0.5f;              // Dot product with up vector (< 0.5 = flipped)
        public const float VEHICLE_CRITICAL_HEALTH = 300f;                // Health below this = critical damage
        public const float VEHICLE_UNDRIVEABLE_HEALTH = 100f;             // Health below this = undriveable

        // Recovery action parameters - multi-stage with escalation
        public const float RECOVERY_REVERSE_SPEED = 5f;                   // m/s when reversing
        public const float RECOVERY_REVERSE_DISTANCE = 10f;               // meters to reverse
        public const long RECOVERY_REVERSE_DURATION = 20_000_000;         // 2 seconds of reversing (legacy)
        public const long RECOVERY_REVERSE_DURATION_SHORT = 15_000_000;   // 1.5 seconds (first attempt)
        public const long RECOVERY_REVERSE_DURATION_MEDIUM = 25_000_000;  // 2.5 seconds (second attempt)
        public const long RECOVERY_REVERSE_DURATION_LONG = 35_000_000;    // 3.5 seconds (third+ attempt)
        public const long RECOVERY_TURN_DURATION = 15_000_000;            // 1.5 seconds of turning
        public const float RECOVERY_TURN_ANGLE = 45f;                     // degrees to turn during recovery
        public const int RECOVERY_MAX_ATTEMPTS = 5;                       // max recovery attempts before giving up (increased)
        public const long RECOVERY_COOLDOWN = 30_000_000;                 // 3 seconds between recovery attempts (reduced)

        // Recovery escalation - try different strategies based on attempt number
        public const int RECOVERY_STRATEGY_REVERSE_TURN = 1;              // Attempt 1-2: reverse + turn
        public const int RECOVERY_STRATEGY_FORWARD_TURN = 2;              // Attempt 3: forward + turn opposite
        public const int RECOVERY_STRATEGY_THREE_POINT = 3;               // Attempt 4-5: three-point turn

        // Recovery states
        public const int RECOVERY_STATE_NONE = 0;
        public const int RECOVERY_STATE_REVERSING = 1;
        public const int RECOVERY_STATE_TURNING = 2;
        public const int RECOVERY_STATE_RESUMING = 3;
        public const int RECOVERY_STATE_FAILED = 4;
        public const int RECOVERY_STATE_FORWARD = 5;           // Forward maneuver state
        public const int RECOVERY_STATE_THREE_POINT_TURN = 6;  // Sharp turn phase of three-point

        // Native function hashes for recovery
        public const ulong NATIVE_TASK_VEHICLE_TEMP_ACTION = 0xC429DCEEB339E129;
        public const ulong NATIVE_IS_VEHICLE_STUCK_ON_ROOF = 0xB497F06B288DCFDF;
        public const ulong NATIVE_IS_ENTITY_IN_WATER = 0xCFB0A0D8EDD145A3;  // Correct hash for IS_ENTITY_IN_WATER
        // NOTE: Use vehicle.UpVector.Z for upright detection (no native needed)
        public const ulong NATIVE_SET_VEHICLE_ON_GROUND_PROPERLY = 0x49733E92263139D1;
        public const ulong NATIVE_SET_VEHICLE_FORWARD_SPEED = 0xAB54A438726D25D5;

        // Temp action values for TASK_VEHICLE_TEMP_ACTION
        public const int TEMP_ACTION_REVERSE = 3;                         // Reverse
        public const int TEMP_ACTION_TURN_LEFT = 7;                       // Turn hard left
        public const int TEMP_ACTION_TURN_RIGHT = 8;                      // Turn hard right
        public const int TEMP_ACTION_REVERSE_LEFT = 13;                   // Reverse + turn left
        public const int TEMP_ACTION_REVERSE_RIGHT = 14;                  // Reverse + turn right

        // Dead-end detection and handling
        public const long TICK_INTERVAL_DEAD_END_CHECK = 20_000_000;      // 2 seconds between checks
        public const float DEAD_END_ESCAPE_DISTANCE = 50f;                // meters from entry to consider escaped
        // Note: ROAD_FLAG_DEAD_END is defined in Road Type Detection section

        // ===== WEATHER-BASED DRIVING =====

        // Weather types (from GET_PREV_WEATHER_TYPE_HASH_NAME / GET_NEXT_WEATHER_TYPE_HASH_NAME)
        // Hash values for weather conditions (unchecked to allow negative values from hex)
        public const int WEATHER_CLEAR = unchecked((int)0x36A83D84);           // CLEAR
        public const int WEATHER_EXTRASUNNY = unchecked((int)0x97AA0A79);      // EXTRASUNNY
        public const int WEATHER_CLOUDS = unchecked((int)0x30FDAF5C);          // CLOUDS
        public const int WEATHER_OVERCAST = unchecked((int)0xBB898D2D);        // OVERCAST
        public const int WEATHER_RAIN = unchecked((int)0x54A69840);            // RAIN
        public const int WEATHER_CLEARING = unchecked((int)0x6DB1A50D);        // CLEARING
        public const int WEATHER_THUNDER = unchecked((int)0xB677829F);         // THUNDER
        public const int WEATHER_SMOG = unchecked((int)0x10DCF4B5);            // SMOG
        public const int WEATHER_FOGGY = unchecked((int)0xD61BDE01);           // FOGGY
        public const int WEATHER_XMAS = unchecked((int)0xAAC9C895);            // XMAS (snow)
        public const int WEATHER_SNOW = unchecked((int)0x2B402288);            // SNOW
        public const int WEATHER_SNOWLIGHT = unchecked((int)0x23FB812B);       // SNOWLIGHT
        public const int WEATHER_BLIZZARD = unchecked((int)0x27EA2814);        // BLIZZARD

        // Weather speed multipliers (applied on top of road type multiplier)
        // Based on real-world stopping distance increases on wet/icy roads
        public const float WEATHER_SPEED_CLEAR = 1.0f;         // Full speed in clear weather
        public const float WEATHER_SPEED_CLOUDS = 1.0f;        // Normal in cloudy
        public const float WEATHER_SPEED_OVERCAST = 0.95f;     // Slightly slower
        public const float WEATHER_SPEED_RAIN = 0.70f;         // Slower in rain (wet roads increase stopping distance 2x)
        public const float WEATHER_SPEED_THUNDER = 0.60f;      // Even slower in storms (reduced visibility + wet)
        public const float WEATHER_SPEED_FOGGY = 0.55f;        // Slow in fog (visibility severely impaired)
        public const float WEATHER_SPEED_SNOW = 0.50f;         // Very slow in snow (stopping distance 3-4x normal)
        public const float WEATHER_SPEED_BLIZZARD = 0.35f;     // Crawl in blizzard (near-zero visibility + ice)

        // Weather check interval
        public const long TICK_INTERVAL_WEATHER_CHECK = 50_000_000;  // 5 seconds

        // Native for weather
        public const ulong NATIVE_GET_PREV_WEATHER_TYPE_HASH_NAME = 0x564B884A05EC45A3;
        public const ulong NATIVE_GET_NEXT_WEATHER_TYPE_HASH_NAME = 0x711327CD09C8F162;

        // ===== COLLISION WARNING SYSTEM =====

        // Collision detection thresholds - TIME-BASED for safety
        // At highway speeds (40 m/s = 90 mph), fixed distances are dangerous
        // These thresholds are now TIME TO COLLISION (TTC) in seconds
        public const float COLLISION_TTC_IMMINENT = 1.5f;      // seconds - emergency braking needed
        public const float COLLISION_TTC_CLOSE = 3.0f;         // seconds - immediate attention
        public const float COLLISION_TTC_MEDIUM = 5.0f;        // seconds - prepare to slow
        public const float COLLISION_TTC_FAR = 8.0f;           // seconds - awareness

        // Legacy distance thresholds (used as minimum bounds)
        public const float COLLISION_SCAN_DISTANCE = 150f;     // meters ahead to scan (increased for high speed)
        public const float COLLISION_WARNING_CLOSE = 15f;      // meters - minimum imminent threshold
        public const float COLLISION_WARNING_MEDIUM = 30f;     // meters - minimum close threshold
        public const float COLLISION_WARNING_FAR = 50f;        // meters - minimum far threshold
        public const float COLLISION_SAFE_DISTANCE = 60f;      // meters - all clear

        // Collision scan parameters
        public const float COLLISION_SCAN_ANGLE = 30f;         // degrees - cone in front
        public const long TICK_INTERVAL_COLLISION_CHECK = 3_000_000;  // 0.3 seconds (faster for safety)
        public const long COLLISION_WARNING_COOLDOWN = 15_000_000;    // 1.5 seconds between same warning

        // Following distance (time-based, in seconds at current speed)
        // Based on real-world "2-second rule" (3+ seconds in adverse conditions)
        public const float FOLLOWING_DISTANCE_SAFE = 3.0f;     // 3 second rule (safe)
        public const float FOLLOWING_DISTANCE_NORMAL = 2.0f;   // 2 second rule (acceptable)
        public const float FOLLOWING_DISTANCE_CLOSE = 1.5f;    // Too close
        public const float FOLLOWING_DISTANCE_TAILGATING = 0.8f; // Dangerous

        // Native for raycast
        public const ulong NATIVE_GET_CLOSEST_VEHICLE_IN_DIRECTION = 0x2E2ADB65E51A3B78;
        public const ulong NATIVE_START_SHAPE_TEST_LOS_PROBE = 0x7EE9F5D83DD4F90E;
        public const ulong NATIVE_GET_SHAPE_TEST_RESULT = 0x3D87450E15D98694;

        // ===== TIME-OF-DAY AWARENESS =====

        // Time periods (24-hour format)
        public const int TIME_DAWN_START = 5;                  // 5:00 AM
        public const int TIME_DAY_START = 7;                   // 7:00 AM
        public const int TIME_DUSK_START = 18;                 // 6:00 PM
        public const int TIME_NIGHT_START = 20;                // 8:00 PM

        // Time-based speed adjustments
        public const float TIME_SPEED_DAY = 1.0f;              // Full speed during day
        public const float TIME_SPEED_DAWN_DUSK = 0.9f;        // Slightly slower at dawn/dusk
        public const float TIME_SPEED_NIGHT = 0.8f;            // Slower at night (visibility)

        // Time check interval
        public const long TICK_INTERVAL_TIME_CHECK = 100_000_000;  // 10 seconds

        // Headlight natives
        public const ulong NATIVE_SET_VEHICLE_LIGHTS = 0x34E710FF01247C5A;
        public const ulong NATIVE_SET_VEHICLE_FULLBEAM = 0x8B7FD87F0DDB421E;

        // ===== EMERGENCY VEHICLE AWARENESS =====

        // Emergency vehicle detection
        public const float EMERGENCY_DETECTION_RADIUS = 100f;  // meters to detect sirens
        public const float EMERGENCY_YIELD_DISTANCE = 30f;     // meters to start yielding
        public const long TICK_INTERVAL_EMERGENCY_CHECK = 10_000_000;  // 1 second
        public const long EMERGENCY_YIELD_DURATION = 50_000_000;  // 5 seconds of yielding

        // Emergency vehicle blip types
        public const int BLIP_POLICE = 60;
        public const int BLIP_AMBULANCE = 61;
        public const int BLIP_FIRE = 68;

        // Native for siren check
        public const ulong NATIVE_IS_VEHICLE_SIREN_ON = 0x4C9BF537BE2634B2;
        public const ulong NATIVE_IS_VEHICLE_SIREN_AUDIO_ON = 0xB5CC40FBCB586380;

        // ===== ETA ANNOUNCEMENTS =====

        // ETA calculation
        public const long TICK_INTERVAL_ETA_UPDATE = 300_000_000;  // 30 seconds
        public const float ETA_ANNOUNCE_CHANGE_THRESHOLD = 60f;    // Announce if ETA changes by 1+ minute
        public const float ETA_MIN_DISTANCE_FOR_ANNOUNCE = 500f;   // meters - don't announce ETA under this

        // Road distance estimation factor
        // Roads are typically 1.3-1.5x longer than straight-line distance
        // Used as fallback when GENERATE_DIRECTIONS_TO_COORD fails
        public const float ROAD_DISTANCE_FACTOR = 1.4f;

        // ETA smoothing (average speed over time for more accurate prediction)
        public const int ETA_SPEED_SAMPLES = 10;                   // Number of speed samples to average

        // ===== PAUSE/RESUME CAPABILITY =====

        // Pause states
        public const int PAUSE_STATE_NONE = 0;
        public const int PAUSE_STATE_PAUSED = 1;
        public const int PAUSE_STATE_RESUMING = 2;

        // Pause behavior
        public const float PAUSE_BRAKE_FORCE = 1.0f;               // Full brakes when pausing
        public const long PAUSE_RESUME_DELAY = 10_000_000;         // 1 second delay before resuming

        // Native for braking
        public const ulong NATIVE_SET_VEHICLE_BRAKE = 0xE1E4E9B7FC841C5C;
        public const ulong NATIVE_SET_VEHICLE_HANDBRAKE = 0x684785568EF26A22;

        // ===== FOLLOWING DISTANCE FEEDBACK =====

        // Following distance thresholds (in meters)
        public const float FOLLOWING_CLEAR_ROAD = 80f;             // No vehicle ahead
        public const float FOLLOWING_COMFORTABLE = 40f;            // Good distance
        public const float FOLLOWING_CLOSE = 20f;                  // Getting close
        public const float FOLLOWING_TOO_CLOSE = 10f;              // Too close
        public const float FOLLOWING_DANGEROUS = 5f;               // Dangerous

        // Following distance announcements
        public const long TICK_INTERVAL_FOLLOWING_CHECK = 20_000_000;  // 2 seconds
        public const long FOLLOWING_ANNOUNCE_COOLDOWN = 100_000_000;   // 10 seconds between same state

        // ===== TUNNEL/BRIDGE DETECTION =====

        // Structure detection
        public const float STRUCTURE_CHECK_HEIGHT = 10f;           // meters above to check for ceiling
        public const float BRIDGE_MIN_HEIGHT_BELOW = 5f;           // meters of clearance below = bridge
        public const long TICK_INTERVAL_STRUCTURE_CHECK = 20_000_000;  // 2 seconds
        public const long STRUCTURE_ANNOUNCE_COOLDOWN = 50_000_000;    // 5 seconds

        // Structure types
        public const int STRUCTURE_TYPE_NONE = 0;
        public const int STRUCTURE_TYPE_TUNNEL = 1;
        public const int STRUCTURE_TYPE_BRIDGE = 2;
        public const int STRUCTURE_TYPE_OVERPASS = 3;              // Under an overpass
        public const int STRUCTURE_TYPE_UNDERPASS = 4;             // Going under something

        // Natives for probe/raycast
        public const ulong NATIVE_GET_GROUND_Z_FOR_3D_COORD = 0xC906A7DAB05C8D2B;
        public const ulong NATIVE_START_EXPENSIVE_SYNCHRONOUS_SHAPE_TEST_LOS_PROBE = 0x377906D8A31E5586;

        // ===== LANE CHANGE AND OVERTAKING =====

        // Lane change detection
        public const float LANE_WIDTH = 3.5f;                      // meters - typical lane width
        public const float LANE_CHANGE_THRESHOLD = 2.5f;           // meters lateral movement = lane change
        public const float LANE_CHANGE_MIN_SPEED = 8f;             // m/s - minimum speed to detect lane changes
        public const long TICK_INTERVAL_LANE_CHECK = 5_000_000;    // 0.5 seconds
        public const long LANE_CHANGE_ANNOUNCE_COOLDOWN = 30_000_000;  // 3 seconds between announcements
        public const float LANE_CHANGE_HEADING_TOLERANCE = 15f;    // degrees - must maintain similar heading

        // Overtaking detection
        public const float OVERTAKE_DETECTION_RADIUS = 30f;        // meters - scan radius for vehicles
        public const float OVERTAKE_SIDE_DISTANCE = 8f;            // meters - how far to the side to consider "beside"
        public const float OVERTAKE_BEHIND_DISTANCE = 15f;         // meters - how far behind = "passed"
        public const float OVERTAKE_MIN_SPEED_DIFF = 5f;           // m/s - must be going faster than overtaken vehicle
        public const long TICK_INTERVAL_OVERTAKE_CHECK = 5_000_000;  // 0.5 seconds
        public const long OVERTAKE_ANNOUNCE_COOLDOWN = 50_000_000;   // 5 seconds between announcements
        public const int OVERTAKE_TRACKING_MAX = 5;                // max vehicles to track for overtaking

        // ===== END AUTODRIVE SYSTEM =====

        // ===== AUTOFLY SYSTEM (Aircraft Autopilot & AutoLand) =====

        // AutoFly native function hashes
        public const ulong NATIVE_TASK_PLANE_MISSION = 0x23703CD154E83B88;
        public const ulong NATIVE_TASK_HELI_MISSION = 0xDAD029E187A2BEB4;
        public const ulong NATIVE_TASK_PLANE_LAND = 0xBF19721FA34D32C0;
        public const ulong NATIVE_CONTROL_LANDING_GEAR = 0xCFC8BE9A5E1FE575;
        public const ulong NATIVE_GET_LANDING_GEAR_STATE = 0x9B0F3DCA3DB0F4CD;
        public const ulong NATIVE_IS_VEHICLE_ON_ALL_WHEELS = 0x1F9FB66F3A3842D2;

        // Aircraft mission types (eVehicleMission)
        public const int MISSION_NONE = 0;
        public const int MISSION_CRUISE = 1;
        public const int MISSION_GOTO = 4;
        public const int MISSION_STOP = 5;
        public const int MISSION_CIRCLE = 9;
        public const int MISSION_LAND = 19;

        // Helicopter mission flags (eHeliMissionFlags)
        public const int HELI_FLAG_NONE = 0;
        public const int HELI_FLAG_ATTAIN_REQUESTED_ORIENTATION = 1;
        public const int HELI_FLAG_DONT_MODIFY_ORIENTATION = 2;
        public const int HELI_FLAG_DONT_MODIFY_PITCH = 4;
        public const int HELI_FLAG_DONT_MODIFY_THROTTLE = 8;
        public const int HELI_FLAG_DONT_MODIFY_ROLL = 16;
        public const int HELI_FLAG_LAND_ON_ARRIVAL = 32;
        public const int HELI_FLAG_DONT_DO_AVOIDANCE = 64;
        public const int HELI_FLAG_START_ENGINE_IMMEDIATELY = 128;
        public const int HELI_FLAG_MAINTAIN_HEIGHT_ABOVE_TERRAIN = 4096;
        // Combined flags for common operations
        public const int HELI_FLAG_LAND_NO_AVOIDANCE = 96;  // LandOnArrival + DontDoAvoidance

        // Landing gear states
        public const int LANDING_GEAR_DEPLOYED = 0;
        public const int LANDING_GEAR_CLOSING = 1;
        public const int LANDING_GEAR_OPENING = 2;
        public const int LANDING_GEAR_RETRACTED = 3;
        public const int LANDING_GEAR_BROKEN = 5;

        // AutoFly tick intervals
        public const long TICK_INTERVAL_AUTOFLY_UPDATE = 2_000_000;           // 0.2s main update
        public const long TICK_INTERVAL_AUTOFLY_APPROACH = 5_000_000;         // 0.5s approach checks
        public const long TICK_INTERVAL_AUTOFLY_DISTANCE = 10_000_000;        // 1.0s distance announcements

        // Flight modes
        public const int FLIGHT_MODE_NONE = 0;
        public const int FLIGHT_MODE_CRUISE = 1;        // Maintain altitude/heading
        public const int FLIGHT_MODE_WAYPOINT = 2;      // Fly to GPS waypoint, then circle
        public const int FLIGHT_MODE_DESTINATION = 3;   // Fly to destination and autoland

        // Flight mode display names
        public static readonly string[] FLIGHT_MODE_NAMES = new string[]
        {
            "Inactive",
            "Cruise",
            "Waypoint",
            "Destination"
        };

        // Flight phases (for DESTINATION mode state machine)
        public const int PHASE_INACTIVE = 0;
        public const int PHASE_CRUISE = 1;        // En route to destination
        public const int PHASE_APPROACH = 2;      // Within approach distance, aligning
        public const int PHASE_FINAL = 3;         // Final approach, gear down
        public const int PHASE_TOUCHDOWN = 4;     // Landing task issued
        public const int PHASE_TAXIING = 5;       // Fixed-wing taxiing after landing
        public const int PHASE_LANDED = 6;        // Flight complete

        // Flight phase display names
        public static readonly string[] FLIGHT_PHASE_NAMES = new string[]
        {
            "Inactive",
            "En route",
            "Approach",
            "Final approach",
            "Landing",
            "Taxiing",
            "Landed"
        };

        // AutoFly speed parameters (m/s)
        public const float AUTOFLY_DEFAULT_SPEED = 50f;       // ~112 mph / 97 knots
        public const float AUTOFLY_MIN_SPEED = 20f;           // ~45 mph / 39 knots
        public const float AUTOFLY_MAX_SPEED = 100f;          // ~224 mph / 194 knots
        public const float AUTOFLY_SPEED_INCREMENT = 5f;      // m/s per key press (~11 mph)
        public const float AUTOFLY_APPROACH_SPEED = 40f;      // ~90 mph for approach phase
        public const float AUTOFLY_FINAL_SPEED = 30f;         // ~67 mph for final approach
        public const float AUTOFLY_HELI_DEFAULT_SPEED = 30f;  // Helicopters fly slower
        public const float AUTOFLY_BLIMP_DEFAULT_SPEED = 15f; // Blimps are very slow (~34 mph)
        public const float AUTOFLY_BLIMP_MAX_SPEED = 25f;     // Blimp max speed (~56 mph)
        public const float AUTOFLY_BLIMP_MIN_SPEED = 8f;      // Blimp min speed (~18 mph)

        // AutoFly altitude parameters (meters)
        public const float AUTOFLY_DEFAULT_ALTITUDE = 500f;   // ~1640 feet
        public const float AUTOFLY_MIN_ALTITUDE = 100f;       // ~328 feet
        public const float AUTOFLY_MAX_ALTITUDE = 3000f;      // ~10,000 feet
        public const float AUTOFLY_ALTITUDE_INCREMENT = 152f; // 500 feet in meters
        public const float AUTOFLY_MIN_TERRAIN_CLEARANCE = 50f; // meters above ground

        // Phase transition distances (meters)
        public const float AUTOFLY_APPROACH_DISTANCE = 3200f;    // 2 miles - start approach phase
        public const float AUTOFLY_FINAL_DISTANCE = 800f;        // 0.5 miles - start final approach
        public const float AUTOFLY_TOUCHDOWN_DISTANCE = 100f;    // Issue landing task
        public const float AUTOFLY_ARRIVAL_RADIUS = 30f;         // meters - consider landed

        // Landing gear deployment
        public const float AUTOFLY_GEAR_DEPLOY_ALTITUDE = 152f;  // 500 feet AGL in meters
        public const float AUTOFLY_GEAR_DEPLOY_DISTANCE = 1600f; // 1 mile from destination

        // Runway parameters
        public const float DEFAULT_RUNWAY_LENGTH = 800f;         // meters (~2625 feet)
        public const float RUNWAY_ALIGNMENT_TOLERANCE = 30f;     // degrees off runway heading

        // Helicopter landing parameters
        public const float HELI_LANDING_SPEED = 10f;             // m/s for final descent
        public const float HELI_LANDING_RADIUS = 15f;            // meters - precision landing
        public const float HELI_APPROACH_HEIGHT = 50f;           // meters above landing zone

        // Cruise mode parameters
        public const float CRUISE_HEADING_TOLERANCE = 5f;        // degrees - acceptable deviation
        public const float CRUISE_ALTITUDE_TOLERANCE = 20f;      // meters - acceptable deviation

        // Distance announcement thresholds (in feet)
        public static readonly int[] AUTOFLY_DISTANCE_MILESTONES_FEET = new int[]
        {
            2000, 1500, 1000, 500, 200, 100
        };

        // Distance announcement thresholds (in miles)
        public static readonly float[] AUTOFLY_DISTANCE_MILESTONES_MILES = new float[]
        {
            5f, 4f, 3f, 2.5f, 2f, 1.5f, 1f, 0.5f
        };

        // Turn guidance thresholds
        public const float AUTOFLY_TURN_GUIDANCE_THRESHOLD = 30f;   // degrees off-course to announce
        public const float AUTOFLY_ALTITUDE_GUIDANCE_THRESHOLD = 50f; // meters altitude diff to announce

        // Pause states (reuse from AutoDrive)
        // PAUSE_STATE_NONE = 0, PAUSE_STATE_PAUSED = 1, PAUSE_STATE_RESUMING = 2

        // ===== END AUTOFLY SYSTEM =====

        // Vehicle class names (indexed by VehicleClass enum value)
        public static readonly string[] VEHICLE_CLASS_NAMES = new string[]
        {
            "Compact",       // 0 - Compacts
            "Sedan",         // 1 - Sedans
            "SUV",           // 2 - SUVs
            "Coupe",         // 3 - Coupes
            "Muscle",        // 4 - Muscle
            "Sports Classic", // 5 - SportsClassics
            "Sports",        // 6 - Sports
            "Super",         // 7 - Super
            "Motorcycle",    // 8 - Motorcycles
            "Off-Road",      // 9 - OffRoad
            "Industrial",    // 10 - Industrial
            "Utility",       // 11 - Utility
            "Van",           // 12 - Vans
            "Bicycle",       // 13 - Cycles
            "Boat",          // 14 - Boats
            "Helicopter",    // 15 - Helicopters
            "Plane",         // 16 - Planes
            "Service",       // 17 - Service
            "Emergency",     // 18 - Emergency
            "Military",      // 19 - Military
            "Commercial",    // 20 - Commercial
            "Train",         // 21 - Trains
            "Open Wheel"     // 22 - OpenWheel
        };

        // Common vehicle colors for quick selection
        public static readonly string[] VEHICLE_COLOR_NAMES = new string[]
        {
            "Metallic Black",
            "Metallic Graphite Black",
            "Metallic Black Steel",
            "Metallic Dark Silver",
            "Metallic Silver",
            "Metallic Blue Silver",
            "Metallic Steel Gray",
            "Metallic Shadow Silver",
            "Metallic Stone Silver",
            "Metallic Midnight Silver",
            "Metallic Red",
            "Metallic Torino Red",
            "Metallic Formula Red",
            "Metallic Blaze Red",
            "Metallic Graceful Red",
            "Metallic Garnet Red",
            "Metallic Desert Red",
            "Metallic Cabernet Red",
            "Metallic Candy Red",
            "Metallic Sunrise Orange",
            "Metallic Classic Gold",
            "Metallic Orange",
            "Metallic Dark Green",
            "Metallic Racing Green",
            "Metallic Sea Green",
            "Metallic Olive Green",
            "Metallic Green",
            "Metallic Gasoline Blue Green",
            "Metallic Midnight Blue",
            "Metallic Dark Blue",
            "Metallic Saxony Blue",
            "Metallic Blue",
            "Metallic Mariner Blue",
            "Metallic Harbor Blue",
            "Metallic Diamond Blue",
            "Metallic Surf Blue",
            "Metallic Nautical Blue",
            "Metallic Bright Blue",
            "Metallic Purple Blue",
            "Metallic Spinnaker Blue",
            "Metallic Ultra Blue",
            "Metallic Purple",
            "Metallic Dark Yellow",
            "Metallic Race Yellow",
            "Metallic Bronze",
            "Metallic Yellow Bird",
            "Metallic Lime",
            "Metallic Champagne",
            "Metallic Cream",
            "Metallic White",
            "Chrome"
        };

        // ===== SAFE ARRIVAL POSITION CALCULATION =====

        // Search radius/distance thresholds for finding safe road position near waypoint
        public const float SAFE_ARRIVAL_MAX_DISTANCE = 150f;              // meters - max acceptable distance from waypoint
        public const float SAFE_ARRIVAL_NTH_NODE_MAX_DISTANCE = 100f;     // meters - stricter for 2nd/3rd closest nodes
        public const float SAFE_ARRIVAL_SAFE_COORD_MAX_DISTANCE = 200f;   // meters - max for safe coord fallback
        public const float SAFE_ARRIVAL_ROAD_SIDE_MAX_DISTANCE = 150f;    // meters - max for road side position

        // Native call parameters for road node lookup
        public const float ROAD_NODE_SEARCH_CONNECTION_DISTANCE = 3.0f;   // meters - connection distance for node search
        public const int ROAD_NODE_TYPE_ALL = 1;                          // Node type: 1 = All road types
        public const int SAFE_COORD_FLAGS = 16;                           // Flags for GET_SAFE_COORD_FOR_PED

        // Road distance sanity check
        public const float ROAD_DISTANCE_SANITY_MAX = 50000f;             // meters - max sensible road distance for ETA

        // Waypoint moved threshold
        public const float WAYPOINT_MOVED_THRESHOLD = 50f;                // meters - waypoint movement to trigger recalc

        // Turn heading change thresholds for navigation
        public const float TURN_HEADING_SLIGHT = 20f;                     // degrees - slight direction change
        public const float TURN_HEADING_UTURN = 150f;                     // degrees - U-turn detected

        // Collision lookahead minimum
        public const float COLLISION_LOOKAHEAD_MIN = 30f;                 // meters - minimum lookahead distance
        public const float COLLISION_LOOKAHEAD_TIME_FACTOR = 2f;          // seconds ahead to scan

        // ===== AUTOFLY ADDITIONAL CONSTANTS =====

        // Default ground height fallback
        public const float AUTOFLY_DEFAULT_GROUND_HEIGHT = 50f;           // meters - fallback ground height

        // Helicopter landing approach parameters
        public const float AUTOFLY_HELI_APPROACH_SPEED = 10f;             // m/s - slow approach speed
        public const float AUTOFLY_DESCEND_AGL = 300f;                    // meters - ~1000 feet AGL for descend
        public const float AUTOFLY_DESCEND_OFFSET = 500f;                 // meters - offset for descend target

        // Task reach distances for plane mission
        public const float AUTOFLY_TASK_REACH_DISTANCE = 100f;            // meters - targetReachedDist parameter

        // Circling altitudes
        public const float AUTOFLY_CIRCLE_APPROACH_AGL = 450f;            // meters - ~1500 feet AGL
        public const float AUTOFLY_CIRCLE_FINAL_AGL = 150f;               // meters - ~500 feet AGL
        public const float AUTOFLY_CIRCLE_ARRIVED_AGL = 100f;             // meters - ~300 feet AGL for circling

        // Task radius parameters
        public const float AUTOFLY_HELI_TASK_RADIUS = 50f;                // meters - helicopter task radius
        public const float AUTOFLY_HELI_CIRCLE_RADIUS = 100f;             // meters - helicopter circle radius
        public const float AUTOFLY_PLANE_CIRCLE_RADIUS = 200f;            // meters - plane circle radius (larger)
        public const float AUTOFLY_CRUISE_FAR_DISTANCE = 10000f;          // meters - cruise target distance (10km)

        // Touchdown detection
        public const float AUTOFLY_TOUCHDOWN_SPEED = 20f;                 // m/s - ~45 mph acceptable touchdown speed
        public const int AUTOFLY_TOUCHDOWN_STABLE_COUNT_PLANE = 3;        // consecutive checks for plane
        public const int AUTOFLY_TOUCHDOWN_STABLE_COUNT_HELI = 5;         // consecutive checks for helicopter (settles slower)
        public const float AUTOFLY_TOUCHDOWN_HEIGHT_PLANE = 3f;           // meters - height threshold for plane
        public const float AUTOFLY_TOUCHDOWN_HEIGHT_HELI = 2f;            // meters - height threshold for helicopter
        public const float AUTOFLY_TOUCHDOWN_HEIGHT_WHEELS = 0.5f;        // meters - fallback if wheels check fails

        // Terrain clearance and flight path planning
        public const float AUTOFLY_DEFAULT_TERRAIN_CLEARANCE = 300f;      // meters - default safe clearance
        public const float AUTOFLY_TERRAIN_SAMPLE_START = 200f;           // meters - start terrain sampling
        public const float AUTOFLY_TERRAIN_SAMPLE_INTERVAL = 400f;        // meters - sample interval
        public const float AUTOFLY_TERRAIN_SAMPLE_MAX = 2000f;            // meters - max terrain sample distance
        public const float AUTOFLY_TERRAIN_WARNING_THRESHOLD = 100f;      // meters - terrain warning threshold

        // Altitude calculations
        public const float AUTOFLY_CRUISE_ALTITUDE_OFFSET = 500f;         // meters - cruise altitude above base
        public const float AUTOFLY_APPROACH_ALTITUDE_OFFSET = 200f;       // meters - approach altitude above base
        public const float AUTOFLY_FINAL_ALTITUDE_OFFSET = 100f;          // meters - final altitude above base
        public const float AUTOFLY_DEFAULT_ALTITUDE_OFFSET = 300f;        // meters - default altitude above base
        public const float AUTOFLY_MIN_SAFE_ALTITUDE_AGL = 300f;          // meters - ~1000 feet AGL minimum

        // Altitude factor calculation
        public const float AUTOFLY_ALTITUDE_FACTOR_MIN = 0.8f;            // minimum altitude factor
        public const float AUTOFLY_ALTITUDE_FACTOR_REFERENCE = 10000f;    // meters - reference altitude for factor
        public const float AUTOFLY_ALTITUDE_FACTOR_RATE = 0.2f;           // rate of factor decrease
        public const float AUTOFLY_ACCEL_ALTITUDE_FACTOR_MIN = 0.6f;      // minimum for acceleration
        public const float AUTOFLY_ACCEL_ALTITUDE_FACTOR_RATE = 0.4f;     // rate for acceleration factor

        // Minimum safe speed for approach
        public const float AUTOFLY_APPROACH_MIN_SPEED = 15f;              // m/s - minimum safe approach speed

        // ===== SAFE ARRAY ACCESS HELPER METHODS =====
        // These methods provide bounds-checked access to constant arrays to prevent IndexOutOfRangeException

        /// <summary>
        /// Safely get driving style value by mode index with bounds checking.
        /// </summary>
        public static int GetDrivingStyleValue(int modeIndex)
        {
            if (modeIndex >= 0 && modeIndex < DRIVING_STYLE_VALUES.Length)
                return DRIVING_STYLE_VALUES[modeIndex];
            return DRIVING_STYLE_VALUES[DRIVING_STYLE_MODE_NORMAL]; // Default to normal
        }

        /// <summary>
        /// Safely get driving style ability by mode index with bounds checking.
        /// </summary>
        public static float GetDrivingStyleAbility(int modeIndex)
        {
            if (modeIndex >= 0 && modeIndex < DRIVING_STYLE_ABILITIES.Length)
                return DRIVING_STYLE_ABILITIES[modeIndex];
            return DRIVING_STYLE_ABILITIES[DRIVING_STYLE_MODE_NORMAL];
        }

        /// <summary>
        /// Safely get driving style aggressiveness by mode index with bounds checking.
        /// </summary>
        public static float GetDrivingStyleAggressiveness(int modeIndex)
        {
            if (modeIndex >= 0 && modeIndex < DRIVING_STYLE_AGGRESSIVENESS.Length)
                return DRIVING_STYLE_AGGRESSIVENESS[modeIndex];
            return DRIVING_STYLE_AGGRESSIVENESS[DRIVING_STYLE_MODE_NORMAL];
        }

        /// <summary>
        /// Safely get driving style name by mode index with bounds checking.
        /// </summary>
        public static string GetDrivingStyleName(int modeIndex)
        {
            if (modeIndex >= 0 && modeIndex < DRIVING_STYLE_NAMES.Length)
                return DRIVING_STYLE_NAMES[modeIndex];
            return "Unknown";
        }

        /// <summary>
        /// Safely get road type name by road type index with bounds checking.
        /// </summary>
        public static string GetRoadTypeName(int roadType)
        {
            if (roadType >= 0 && roadType < ROAD_TYPE_NAMES.Length)
                return ROAD_TYPE_NAMES[roadType];
            return "unknown road";
        }

        /// <summary>
        /// Safely get road type speed multiplier by road type index with bounds checking.
        /// </summary>
        public static float GetRoadTypeSpeedMultiplier(int roadType)
        {
            if (roadType >= 0 && roadType < ROAD_TYPE_SPEED_MULTIPLIERS.Length)
                return ROAD_TYPE_SPEED_MULTIPLIERS[roadType];
            return 0.8f; // Default to cautious multiplier for unknown
        }

        /// <summary>
        /// Safely get road seek mode name by seek mode index with bounds checking.
        /// </summary>
        public static string GetRoadSeekModeName(int seekMode)
        {
            if (seekMode >= 0 && seekMode < ROAD_SEEK_MODE_NAMES.Length)
                return ROAD_SEEK_MODE_NAMES[seekMode];
            return "Unknown";
        }

        /// <summary>
        /// Safely get wheel type name by wheel type index with bounds checking.
        /// </summary>
        public static string GetWheelTypeName(int wheelType)
        {
            if (wheelType >= 0 && wheelType < WHEEL_TYPE_NAMES.Length)
                return WHEEL_TYPE_NAMES[wheelType];
            return "Unknown";
        }

        /// <summary>
        /// Safely get flight mode name by mode index with bounds checking.
        /// </summary>
        public static string GetFlightModeName(int flightMode)
        {
            if (flightMode >= 0 && flightMode < FLIGHT_MODE_NAMES.Length)
                return FLIGHT_MODE_NAMES[flightMode];
            return "Unknown";
        }

        /// <summary>
        /// Safely get flight phase name by phase index with bounds checking.
        /// </summary>
        public static string GetFlightPhaseName(int phase)
        {
            if (phase >= 0 && phase < FLIGHT_PHASE_NAMES.Length)
                return FLIGHT_PHASE_NAMES[phase];
            return "Unknown";
        }

        /// <summary>
        /// Validate driving style mode index is within valid range.
        /// </summary>
        public static bool IsValidDrivingStyleMode(int modeIndex)
        {
            return modeIndex >= 0 && modeIndex < DRIVING_STYLE_VALUES.Length;
        }

        /// <summary>
        /// Validate road type index is within valid range.
        /// </summary>
        public static bool IsValidRoadType(int roadType)
        {
            return roadType >= 0 && roadType < ROAD_TYPE_NAMES.Length;
        }

        /// <summary>
        /// Validate road seek mode index is within valid range.
        /// </summary>
        public static bool IsValidRoadSeekMode(int seekMode)
        {
            return seekMode >= 0 && seekMode < ROAD_SEEK_MODE_NAMES.Length;
        }

        // ===== END CONSTANTS =====
    }
}
