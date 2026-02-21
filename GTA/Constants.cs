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

        // PERFORMANCE: Pre-calculated math constants to avoid repeated calculations in hot paths
        public const float DEG_TO_RAD = (float)(Math.PI / 180.0);   // ~0.01745329f
        public const float RAD_TO_DEG = (float)(180.0 / Math.PI);   // ~57.29578f
        public const float PI_FLOAT = (float)Math.PI;               // ~3.14159265f
        public const float TWO_PI = (float)(Math.PI * 2.0);         // ~6.28318531f

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

        // Landing beacon audio (3D audio beacon for aircraft navigation)
        public const float BEACON_GAIN = 0.08f;
        public const float BEACON_MIN_GAIN = BEACON_GAIN * 0.1f;   // Floor gain when behind
        public const float BEACON_BASE_FREQUENCY = 400f;           // Hz at destination altitude
        public const float BEACON_MIN_FREQUENCY = 150f;            // Hz when very high above
        public const float BEACON_MAX_FREQUENCY = 800f;            // Hz when below destination
        public const float BEACON_ALTITUDE_SCALE = 0.5f;           // Hz reduction per foot above destination
        public const float BEACON_PULSE_DURATION = 0.06f;          // seconds per pulse (60ms beep)
        public const long BEACON_PULSE_DURATION_TICKS = (long)(BEACON_PULSE_DURATION * 10_000_000);

        // Beacon pulse rate (pre-calculated as ticks to avoid per-pulse float→long conversion)
        public const long BEACON_PULSE_FAR_TICKS = (long)(1.5f * 10_000_000);      // >2 miles
        public const long BEACON_PULSE_MEDIUM_TICKS = (long)(0.8f * 10_000_000);   // 1-2 miles
        public const long BEACON_PULSE_NEAR_TICKS = (long)(0.4f * 10_000_000);     // 0.25-1 mile
        public const long BEACON_PULSE_CLOSE_TICKS = (long)(0.15f * 10_000_000);   // <0.25 mile
        public const long BEACON_PULSE_OVERHEAD_TICKS = (long)(0.06f * 10_000_000); // directly over

        // Collision proximity beep (AutoDrive)
        public const double COLLISION_BEEP_GAIN = 0.08;
        public const double COLLISION_BEEP_DURATION_SECONDS = 0.05;
        public const double COLLISION_BEEP_FREQ_FAR = 400.0;
        public const double COLLISION_BEEP_FREQ_MEDIUM = 600.0;
        public const double COLLISION_BEEP_FREQ_CLOSE = 900.0;
        public const double COLLISION_BEEP_FREQ_IMMINENT = 1200.0;

        // Beacon distance thresholds (meters) - squared to avoid sqrt in distance check
        public const float BEACON_OVERHEAD_DISTANCE_SQ = 150f * 150f;   // ~500 feet
        public const float BEACON_CLOSE_DISTANCE_SQ = 400f * 400f;      // ~0.25 miles
        public const float BEACON_NEAR_DISTANCE_SQ = 1600f * 1600f;     // ~1 mile
        public const float BEACON_MEDIUM_DISTANCE_SQ = 3200f * 3200f;   // ~2 miles

        // Beacon pan parameters
        public const float BEACON_PAN_DEAD_ZONE = 5f;              // degrees - center tolerance
        public const float BEACON_PAN_MAX_ANGLE = 90f;             // degrees - full pan at 90+ off heading
        public const float BEACON_PAN_RANGE_INV = 1f / (BEACON_PAN_MAX_ANGLE - BEACON_PAN_DEAD_ZONE); // pre-calculated
        public const float BEACON_BEHIND_GAIN_FACTOR = 0.3f;       // reduce volume when behind (>120 degrees off)

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

        // Native function hashes for VTOL nozzle position control
        public const ulong NATIVE_GET_VEHICLE_FLIGHT_NOZZLE_POSITION = 0xDA62027C8BDB326E;
        public const ulong NATIVE_SET_VEHICLE_FLIGHT_NOZZLE_POSITION = 0x30D779DE7C4F6DD3;  // Set VTOL nozzle angle (0=plane, 1=hover)

        // Vehicle save/load
        public const int VEHICLE_SAVE_SLOT_COUNT = 10;
        public const string SAVED_VEHICLES_FILE_NAME = "gta11ySavedVehicles.json";

        // Vehicle mod menu display names - expanded to include all known mod types
        // Standard mods: 0-49, Colors: 66-67, Extended/DLC mods probed dynamically
        public static readonly Dictionary<int, string> MOD_TYPE_NAMES = new Dictionary<int, string>
        {
            // Body/Exterior (0-10)
            { 0, "Spoiler" },
            { 1, "Front Bumper" },
            { 2, "Rear Bumper" },
            { 3, "Side Skirt" },
            { 4, "Exhaust" },
            { 5, "Frame" },
            { 6, "Grille" },
            { 7, "Hood" },
            { 8, "Left Fender" },
            { 9, "Right Fender" },
            { 10, "Roof" },

            // Performance (11-18)
            { 11, "Engine" },
            { 12, "Brakes" },
            { 13, "Transmission" },
            { 14, "Horn" },
            { 15, "Suspension" },
            { 16, "Armor" },
            { 17, "Nitrous" },
            { 18, "Turbo" },

            // Audio/Visual (19-22)
            { 19, "Subwoofer" },
            { 20, "Tire Smoke" },
            { 21, "Hydraulics" },
            { 22, "Xenon Lights" },

            // Wheels (23-24)
            { 23, "Front Wheels" },
            { 24, "Rear Wheels" },

            // Plates (25-26)
            { 25, "Plate Holder" },
            { 26, "Vanity Plate" },

            // Interior/Benny's (27-45)
            { 27, "Trim" },
            { 28, "Ornaments" },
            { 29, "Dashboard" },
            { 30, "Dial Design" },
            { 31, "Door Speaker" },
            { 32, "Seats" },
            { 33, "Steering Wheel" },
            { 34, "Shift Lever" },
            { 35, "Plaques" },
            { 36, "Speakers" },
            { 37, "Trunk" },
            { 38, "Hydraulics" },
            { 39, "Engine Block" },
            { 40, "Air Filter" },
            { 41, "Strut Bar" },
            { 42, "Arch Cover" },
            { 43, "Antenna" },
            { 44, "Exterior Parts" },
            { 45, "Tank" },

            // Doors/Windows (46-47)
            { 46, "Left Door" },
            { 47, "Right Door" },

            // Livery (48)
            { 48, "Livery" },

            // Light Bar (49)
            { 49, "Light Bar" }

            // NOTE: Weaponized vehicle mods (Primary Weapon, Secondary Weapon, Countermeasures, etc.)
            // are NOT accessible through GET_NUM_VEHICLE_MODS/SET_VEHICLE_MOD natives.
            // They require the in-game Vehicle Workshop (Facility, MOC, Avenger) to modify.
            // The standard vehicle mod type system only goes up to index 48 (Livery) + 49 (Light Bar).
        };

        // All valid mod type indices (0-49)
        // Note: 17-22 are toggle mods (Nitrous, Turbo, Subwoofer, TyreSmoke, Hydraulics, XenonLights)
        // which are handled separately via VehicleToggleModType
        public static readonly int[] ALL_MOD_TYPES = new int[]
        {
            // Body/Exterior
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
            // Performance
            11, 12, 13, 14, 15, 16,
            // Note: 17-22 are handled as toggle mods, not through GET_NUM_VEHICLE_MODS
            // Wheels
            23, 24,
            // Plates
            25, 26,
            // Interior/Benny's
            27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45,
            // Doors/Windows
            46, 47,
            // Livery/Light Bar
            48, 49
        };

        // Performance mod types (commonly used)
        // Note: 17 (Nitrous) and 18 (Turbo) are toggle mods handled separately
        public static readonly int[] PERFORMANCE_MOD_TYPES = { 11, 12, 13, 15, 16 };

        // Body mod types
        public static readonly int[] BODY_MOD_TYPES = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 46, 47 };

        // Interior/Benny's mod types
        public static readonly int[] INTERIOR_MOD_TYPES = { 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45 };

        // Horn names for horn mod type (14)
        public static readonly string[] HORN_NAMES = new string[]
        {
            "Stock Horn",
            "Jazz Horn Loop",
            "Jazz Horn",
            "Sad Trombone",
            "Classical Horn Loop 1",
            "Classical Horn Loop 2",
            "Classical Horn Loop 3",
            "Classical Horn 1",
            "Classical Horn 2",
            "Classical Horn 3",
            "Classical Horn 4",
            "Classical Horn 5",
            "Classical Horn 6",
            "Classical Horn 7",
            "Scale - Do",
            "Scale - Re",
            "Scale - Mi",
            "Scale - Fa",
            "Scale - Sol",
            "Scale - La",
            "Scale - Ti",
            "Scale - Do (High)",
            "Liberty City Loop",
            "Liberty City",
            "Star Spangled Banner Loop",
            "Star Spangled Banner",
            "Shave and Haircut Loop",
            "Shave and Haircut",
            "Vice City Loop",
            "Vice City",
            "Lowrider Horn Loop",
            "Lowrider Horn",
            "San Andreas Loop",
            "San Andreas",
            "Revelation",
            "Revelation Loop"
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
        public const float AUTODRIVE_LONGRANGE_THRESHOLD = 500f;           // meters - use LONGRANGE native above this distance

        // Native for finding safe road position near waypoint
        public const ulong NATIVE_GET_CLOSEST_VEHICLE_NODE = 0x240A18690AE96513;
        public const ulong NATIVE_GET_NTH_CLOSEST_VEHICLE_NODE = 0xE50E52416CCF948B;  // For alternate road nodes
        public const ulong NATIVE_GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING = 0x80CA6A8B6C094CC4;  // Returns position + heading + lane count
        public const ulong NATIVE_GET_SAFE_COORD_FOR_PED = 0xB61C8E878A4199CA;
        public const ulong NATIVE_GET_POINT_ON_ROAD_SIDE = 0x16F46FB18C8009E4;

        // ===== DRIVING STYLES =====

        // Driving style mode indices
        public const int DRIVING_STYLE_MODE_CAUTIOUS = 0;
        public const int DRIVING_STYLE_MODE_NORMAL = 1;
        public const int DRIVING_STYLE_MODE_AGGRESSIVE = 2;
        public const int DRIVING_STYLE_MODE_RECKLESS = 3;

        // ===== DRIVING STYLE FLAGS (VehicleDrivingFlags from SHVDN v3.6.0) =====
        // Complete flag reference:
        //   Bit 0  (1):          StopForVehicles - stop before hitting vehicles
        //   Bit 1  (2):          StopForPeds - stop before hitting pedestrians
        //   Bit 2  (4):          SwerveAroundAllVehicles - steer around all vehicles
        //   Bit 3  (8):          SteerAroundStationaryVehicles - steer around parked vehicles
        //   Bit 4  (16):         SteerAroundPeds - steer around pedestrians
        //   Bit 5  (32):         SteerAroundObjects - steer around obstacles
        //   Bit 6  (64):         DontSteerAroundPlayerPed - don't avoid player
        //   Bit 7  (128):        StopAtTrafficLights - obey traffic signals
        //   Bit 8  (256):        GoOffRoadWhenAvoiding / UseBlinkers
        //   Bit 9  (512):        AllowGoingWrongWay - use wrong lane when stuck
        //   Bit 10 (1024):       Reverse
        //   Bit 11 (2048):       UseWanderFallbackInsteadOfStraightLine
        //   Bit 14 (16384):      AdjustCruiseSpeedBasedOnRoadSpeed
        //   Bit 18 (262144):     UseShortCutLinks - take shortest path
        //   Bit 19 (524288):     ChangeLanesAroundObstructions
        //   Bit 21 (2097152):    UseSwitchedOffNodes
        //   Bit 25 (33554432):   UseStringPullingAtJunctions - smooth junction paths
        //   Bit 29 (536870912):  TryToAvoidHighways
        //   Bit 30 (1073741824): ForceJoinInRoadDirection
        //   Bit 31 (2147483648): StopAtDestination
        //
        // VAutodrive research: Uses GTA's built-in DrivingStyle presets + SET_DRIVER_ABILITY
        // + SET_DRIVER_AGGRESSIVENESS as the primary behavior differentiators.
        // Key insight: Each style should have DISTINCT flag values for noticeably different behavior.

        // Cautious: Maximum safety - stops for everything, obeys all laws
        // = StopForVehicles(1) + StopForPeds(2) + SwerveAroundAllVehicles(4) +
        //   SteerAroundStationaryVehicles(8) + SteerAroundPeds(16) + SteerAroundObjects(32) +
        //   StopAtTrafficLights(128) +
        //   AdjustCruiseSpeedBasedOnRoadSpeed(16384) + UseShortCutLinks(262144) +
        //   ChangeLanesAroundObstructions(524288) +
        //   AvoidAdverseConditions(67108864) + ForceJoinInRoadDirection(1073741824)
        // Removed: GoOffRoadWhenAvoiding(256) - cautious drivers stay on road
        // Removed: DontTerminateTaskWhenAchieved(2147483648) - causes circling at destination
        // Added: AvoidAdverseConditions(67108864) - avoids hazards
        public const int DRIVING_STYLE_CAUTIOUS = unchecked((int)(
            1u + 2u + 4u + 8u + 16u + 32u + 128u +
            16384u + 262144u + 524288u +
            67108864u + 1073741824u));

        // Normal: Balanced driving - stops for vehicles and peds, obeys lights, smooth junctions
        // Based on SHVDN DrivingModeStopForVehicles (786603) but with ForceJoinRoadDirection
        // = StopForVehicles(1) + StopForPeds(2) + SwerveAroundAllVehicles(4) +
        //   SteerAroundStationaryVehicles(8) + SteerAroundPeds(16) + SteerAroundObjects(32) +
        //   StopAtTrafficLights(128) +
        //   AdjustCruiseSpeedBasedOnRoadSpeed(16384) + UseShortCutLinks(262144) +
        //   ChangeLanesAroundObstructions(524288) +
        //   UseStringPullingAtJunctions(33554432) + ForceJoinInRoadDirection(1073741824)
        // Added: SwerveAroundAllVehicles(4), SteerAroundPeds(16) - complete obstacle avoidance
        public const int DRIVING_STYLE_NORMAL = unchecked((int)(
            1u + 2u + 4u + 8u + 16u + 32u + 128u +
            16384u + 262144u + 524288u +
            33554432u + 1073741824u));

        // Aggressive: Ignores lights, swerves around traffic, allows wrong way, overtakes
        // Distinct from Normal - more assertive, skips traffic lights, takes shortcuts
        // = StopForVehicles(1) + SwerveAroundAllVehicles(4) + SteerAroundObjects(32) +
        //   AllowGoingWrongWay(512) +
        //   AdjustCruiseSpeedBasedOnRoadSpeed(16384) + UseShortCutLinks(262144) +
        //   ChangeLanesAroundObstructions(524288) + UseSwitchedOffNodes(2097152) +
        //   UseStringPullingAtJunctions(33554432) + ForceJoinInRoadDirection(1073741824)
        public const int DRIVING_STYLE_AGGRESSIVE = unchecked((int)(
            1u + 4u + 32u + 512u +
            16384u + 262144u + 524288u + 2097152u +
            33554432u + 1073741824u));

        // Reckless: Swerves around traffic without braking, dodges peds/objects, takes any path
        // Based on SHVDN DrivingModeAvoidVehiclesReckless — "doesn't use brakes AT ALL for steering"
        // = SwerveAroundAllVehicles(4) + SteerAroundPeds(16) + SteerAroundObjects(32) +
        //   AllowGoingWrongWay(512) + UseShortCutLinks(262144) +
        //   ChangeLanesAroundObstructions(524288) + UseSwitchedOffNodes(2097152) +
        //   ForceJoinInRoadDirection(1073741824)
        // No speed limits, no stopping for cars, no traffic lights. AI handles all speed management.
        public const int DRIVING_STYLE_RECKLESS = unchecked((int)(
            4u + 16u + 32u + 512u +
            262144u + 524288u + 2097152u + 1073741824u));

        // Driving style values array (indexed by mode)
        public static readonly int[] DRIVING_STYLE_VALUES = new int[]
        {
            DRIVING_STYLE_CAUTIOUS,    // 0 - Cautious
            DRIVING_STYLE_NORMAL,      // 1 - Normal
            DRIVING_STYLE_AGGRESSIVE,  // 2 - Aggressive
            DRIVING_STYLE_RECKLESS     // 3 - Reckless
        };

        // Speed multipliers per style (applied to target speed)
        // VAutodrive research: speed + driving flags + ability/aggressiveness together
        // create distinct driving personalities
        public static readonly float[] DRIVING_STYLE_SPEED_MULTIPLIERS = new float[]
        {
            0.7f,   // Cautious - 70% of target speed (slower, safer)
            1.0f,   // Normal - 100% of target speed
            1.2f,   // Aggressive - 120% of target speed (faster, more assertive)
            1.5f    // Reckless - 150% of target speed (all-out speed, AI handles curves/traffic)
        };

        // Driver ability per style (0.0 - 1.0)
        // VAutodrive research: SET_DRIVER_ABILITY controls curve handling quality
        // 1.0 = best vehicle control, smoother turns, better lane keeping
        public static readonly float[] DRIVING_STYLE_ABILITIES = new float[]
        {
            1.0f,   // Cautious - maximum control for safe driving
            1.0f,   // Normal - maximum control
            0.9f,   // Aggressive - slightly less precise at higher speeds
            0.7f    // Reckless - noticeably less control for chaotic feel
        };

        // Driver aggressiveness per style (0.0 - 1.0)
        // VAutodrive research: SET_DRIVER_AGGRESSIVENESS controls traffic interaction
        // Higher = more lane changes, closer following, more assertive overtaking
        public static readonly float[] DRIVING_STYLE_AGGRESSIVENESS = new float[]
        {
            0.0f,   // Cautious - completely passive, no lane changes
            0.3f,   // Normal - mild assertiveness, occasional lane changes
            0.7f,   // Aggressive - frequent overtaking and lane changes
            1.0f    // Reckless - maximum aggression, constant lane changes
        };

        // Display names for menu
        public static readonly string[] DRIVING_STYLE_NAMES = new string[]
        {
            "Cautious",      // Maximum safety, obey traffic, stop at lights
            "Normal",        // Balanced driving, obey lights, smooth driving
            "Aggressive",    // Ignore traffic lights, overtake, allow wrong way
            "Reckless"       // Ram through vehicles, ignore everything
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
        public const float CURVE_SLOWDOWN_DISTANCE_BASE = 100f;           // Base distance to start slowing
        public const float CURVE_SLOWDOWN_DISTANCE_SPEED_FACTOR = 4.0f;   // Multiply speed by this for lookahead
        public const float CURVE_SLOWDOWN_DISTANCE_MIN = 50f;             // Minimum slowdown distance
        public const float CURVE_SLOWDOWN_DISTANCE_MAX = 200f;            // Maximum slowdown distance
        public const long CURVE_SLOWDOWN_MAX_DURATION = 80_000_000;       // 8 second safety timeout (condition-based end is primary)
        public const float CURVE_REALIGN_LOOKAHEAD = 15f;                  // Meters ahead to check road alignment for curve end

        // Smooth arrival deceleration (waypoint mode)
        public const float ARRIVAL_SLOWDOWN_DISTANCE = 100f;              // Start slowing 100m from destination
        public const float ARRIVAL_FINAL_SPEED = 5f;                      // Final approach speed (m/s, ~11 mph)
        public const float ARRIVAL_SPEED_FACTOR = 0.3f;                   // Speed = distance * factor (gradual slowdown)

        // Native function hashes for AutoDrive
        public const ulong NATIVE_TASK_VEHICLE_DRIVE_WANDER = 0x480142959D337D00;
        public const ulong NATIVE_TASK_VEHICLE_DRIVE_TO_COORD = 0xE2A2AA2F659D77A7;
        public const ulong NATIVE_TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE = 0x158BB33F920D360C;  // Better pathfinding for long distances
        public const ulong NATIVE_SET_DRIVE_TASK_CRUISE_SPEED = 0x5C9B84BD7D31D908;
        public const ulong NATIVE_SET_DRIVER_ABILITY = 0xB195FFA8042FC5C3;
        public const ulong NATIVE_SET_DRIVER_AGGRESSIVENESS = 0xA731F608CA104E3C;
        public const ulong NATIVE_GET_CLOSEST_VEHICLE_NODE_WITH_HEADING = 0xFF071FB798B803B0;
        public const ulong NATIVE_GET_VEHICLE_NODE_PROPERTIES = 0x0568566ACBB5DEDC;
        public const ulong NATIVE_GENERATE_DIRECTIONS_TO_COORD = 0xF90125F1F79ECDF8;
        public const ulong NATIVE_CLEAR_PED_TASKS = 0xE1EF3C1216AFF2CD;
        public const ulong NATIVE_CLEAR_PED_TASKS_IMMEDIATELY = 0xAAA34F8A7CB32098;

        // ===== ROAD TYPE DETECTION =====

        // Road node flags (from GET_VEHICLE_NODE_PROPERTIES - eVehicleNodeProperties)
        public const int ROAD_FLAG_OFF_ROAD = 1;           // Bit 0: Dirt roads, trails, alleys
        public const int ROAD_FLAG_ON_PLAYERS_ROAD = 2;    // Bit 1: On player's current road
        public const int ROAD_FLAG_NO_BIG_VEHICLES = 4;    // Bit 2: Narrow road, no large vehicles
        public const int ROAD_FLAG_SWITCHED_OFF = 8;       // Bit 3: Disabled/inactive node
        public const int ROAD_FLAG_TUNNEL = 16;            // Bit 4: Tunnel or interior
        public const int ROAD_FLAG_DEAD_END = 32;          // Bit 5: Dead end road
        public const int ROAD_FLAG_HIGHWAY = 64;           // Bit 6: Highway/freeway
        public const int ROAD_FLAG_JUNCTION = 128;         // Bit 7: Intersection
        public const int ROAD_FLAG_TRAFFIC_LIGHT = 256;    // Bit 8: Has traffic light
        public const int ROAD_FLAG_GIVE_WAY = 512;         // Bit 9: Yield sign
        public const int ROAD_FLAG_WATER = 1024;           // Bit 10: Water route node

        // eGetClosestNodeFlags - used with GET_CLOSEST_VEHICLE_NODE_WITH_HEADING etc.
        // NOTE: These are NOT the same as eVehicleNodeProperties above
        public const int NODE_FLAG_INCLUDE_SWITCHED_OFF = 1;   // Include disabled/inactive nodes
        public const int NODE_FLAG_INCLUDE_BOAT_NODES = 2;     // Include boat/water nodes
        public const int NODE_FLAG_IGNORE_SLIPLANES = 4;       // Skip slip lanes
        public const int NODE_FLAG_ACTIVE_NODES_ONLY = 0;      // Default: only active road nodes

        // Lane count thresholds for road classification
        public const int ROAD_LANES_HIGHWAY_MIN = 3;           // 3+ lanes = likely highway
        public const int ROAD_LANES_CITY_MIN = 2;              // 2+ lanes = city/main road

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
        // Note: DF_ADJUST_CRUISE_SPEED_BASED_ON_ROAD_SPEED (flag 16384) in driving styles
        // already tells the AI to adjust speed for road types. These multipliers are kept
        // minimal to avoid fighting the AI's built-in road speed adjustment.
        // Only dirt road retains a significant reduction since the AI flag doesn't handle it well.
        public static readonly float[] ROAD_TYPE_SPEED_MULTIPLIERS = new float[]
        {
            1.0f,   // 0 - Unknown: let AI decide
            1.0f,   // 1 - Highway: AI handles via flag 16384
            1.0f,   // 2 - City street: AI handles via flag 16384
            1.0f,   // 3 - Suburban: AI handles via flag 16384
            1.0f,   // 4 - Rural: AI handles via flag 16384
            0.6f,   // 5 - Dirt road: AI flag doesn't slow enough for unpaved
            1.0f    // 6 - Tunnel: AI handles normally
        };

        // Road type to driving style mapping (dynamic style switching)
        // Indexed by ROAD_TYPE_* values, returns DRIVING_STYLE_MODE_* values
        // Highway → Fast (overtaking), City → Cautious (pedestrians), others → Normal
        public static readonly int[] ROAD_TYPE_DRIVING_STYLES = new int[]
        {
            DRIVING_STYLE_MODE_NORMAL,    // 0 - Unknown: normal driving
            DRIVING_STYLE_MODE_AGGRESSIVE,      // 1 - Highway: faster, more overtaking
            DRIVING_STYLE_MODE_CAUTIOUS,  // 2 - City street: careful of pedestrians
            DRIVING_STYLE_MODE_NORMAL,    // 3 - Suburban: normal driving
            DRIVING_STYLE_MODE_NORMAL,    // 4 - Rural: normal driving
            DRIVING_STYLE_MODE_CAUTIOUS,  // 5 - Dirt road: careful of terrain
            DRIVING_STYLE_MODE_NORMAL     // 6 - Tunnel: normal driving
        };

        /// <summary>
        /// Get suggested driving style for a road type (for dynamic style switching)
        /// </summary>
        public static int GetSuggestedDrivingStyle(int roadType)
        {
            if (roadType >= 0 && roadType < ROAD_TYPE_DRIVING_STYLES.Length)
                return ROAD_TYPE_DRIVING_STYLES[roadType];
            return DRIVING_STYLE_MODE_NORMAL;
        }

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

        // Waypoint bearing announcements (standalone, between milestones)
        public const long BEARING_ANNOUNCE_INTERVAL = 300_000_000;  // 30 seconds

        // Speed change announcements (catch-all for combined multiplier changes)
        public const float SPEED_ANNOUNCE_CHANGE_THRESHOLD = 2.2f;  // m/s (~5 mph)
        public const long SPEED_ANNOUNCE_COOLDOWN = 150_000_000;    // 15 seconds

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
        public const int ROAD_SEEK_MAX_NODES = 30;                       // Check 30 closest road nodes per scan
        public const float ROAD_SEEK_ARRIVAL_THRESHOLD = 30f;            // Within 30m = arrived

        // ===== AUTODRIVE RECOVERY SYSTEM =====

        // Recovery check intervals
        public const long TICK_INTERVAL_RECOVERY_CHECK = 5_000_000;       // 0.5s between recovery checks
        public const long TICK_INTERVAL_STUCK_CHECK = 10_000_000;         // 1.0s between stuck checks
        public const long TICK_INTERVAL_PROGRESS_CHECK = 20_000_000;      // 2.0s between progress checks

        // Stuck detection thresholds (optimized per Grok recommendations - 5s threshold)
        public const float STUCK_MOVEMENT_THRESHOLD = 2f;                 // Less than 2m movement = potentially stuck
        public const float STUCK_SPEED_THRESHOLD = 1f;                    // Less than 1 m/s = very slow/stopped
        public const int STUCK_CHECK_COUNT_THRESHOLD = 5;                 // 5 consecutive checks = stuck (5 seconds - increased from 3)
        public const float STUCK_HEADING_CHANGE_THRESHOLD = 5f;           // Heading change < 5° while stuck = truly stuck

        // Flight stuck detection (Grok optimization) - for aerial recovery
        public const int FLIGHT_STUCK_THRESHOLD = 50;                     // 50 checks at 0.2s = ~10 seconds not progressing

        // Task re-issue thresholds (optimized to reduce jerky movement)
        public const float TASK_DEVIATION_THRESHOLD = 10f;                // Only re-issue task if deviated >10m from path
        public const float TASK_HEADING_DEVIATION_THRESHOLD = 45f;        // Only re-issue if heading differs >45° from target

        // Progress timeout (waypoint mode)
        public const long PROGRESS_TIMEOUT_TICKS = 300_000_000;           // 30 seconds without progress = timeout
        public const float PROGRESS_DISTANCE_THRESHOLD = 10f;             // Must get 10m closer within timeout

        // Vehicle state thresholds
        public const float VEHICLE_UPRIGHT_THRESHOLD = 0.5f;              // Dot product with up vector (< 0.5 = flipped)
        public const float VEHICLE_CRITICAL_HEALTH = 300f;                // Health below this = critical damage
        public const float VEHICLE_UNDRIVEABLE_HEALTH = 100f;             // Health below this = undriveable

        // Native function hashes for vehicle control
        public const ulong NATIVE_IS_ENTITY_IN_WATER = 0xCFB0A0D8EDD145A3;
        // NOTE: Use vehicle.UpVector.Z for upright detection (no native needed)
        public const ulong NATIVE_SET_VEHICLE_ON_GROUND_PROPERLY = 0x49733E92263139D1;
        public const ulong NATIVE_SET_VEHICLE_FORWARD_SPEED = 0xAB54A438726D25D5;
        // Hard speed ceiling the AI will never exceed (cooperative with AI driving task)
        public const ulong NATIVE_SET_DRIVE_TASK_MAX_CRUISE_SPEED = 0x404A5AA9B9F0B746;

        // Dead-end detection
        public const long TICK_INTERVAL_DEAD_END_CHECK = 20_000_000;      // 2 seconds between checks
        public const float DEAD_END_ESCAPE_DISTANCE = 50f;                // meters from entry to consider escaped
        // Note: ROAD_FLAG_DEAD_END is defined in Road Type Detection section

        // Cooperative recovery system — re-issues driving task to escape node behind vehicle
        public const float RECOVERY_ESCAPE_BASE_DISTANCE = 30f;           // meters behind vehicle (attempt 1)
        public const float RECOVERY_ESCAPE_DISTANCE_INCREMENT = 30f;      // +30m per attempt (2=60m, 3=90m...)
        public const float RECOVERY_ESCAPE_MAX_DISTANCE = 150f;           // max search distance
        public const float RECOVERY_ESCAPE_ARRIVAL_RADIUS = 12f;          // generous arrival threshold
        public const float RECOVERY_ESCAPE_SPEED = 8f;                    // m/s (~18 mph) cautious escape
        public const long RECOVERY_ESCAPE_TIMEOUT = 150_000_000;          // 15 seconds to reach escape node
        public const long RECOVERY_COOLDOWN = 50_000_000;                 // 5s cooldown after success
        public const int RECOVERY_MAX_ATTEMPTS = 5;                       // stop after 5 failures
        public const int RECOVERY_NODE_SCAN_COUNT = 5;                    // scan 5 nearest nodes to find one behind

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
        public const long TICK_INTERVAL_OVERTAKE_CHECK = 10_000_000;  // 1.0 seconds
        public const long OVERTAKE_ANNOUNCE_COOLDOWN = 50_000_000;   // 5 seconds between announcements
        public const int OVERTAKE_TRACKING_MAX = 5;                // max vehicles to track for overtaking

        // ===== END AUTODRIVE SYSTEM =====

        // ===== AIRCRAFT DATA =====

        // Default runway length for landing destination calculations (used by AircraftLandingMenu)
        public const float DEFAULT_RUNWAY_LENGTH = 800f;         // meters (~2625 feet)

        // ===== END AIRCRAFT DATA =====

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
        // IMPORTANT: This value is eGetClosestNodeFlags, NOT a "road type".
        // Value 1 = GCNF_INCLUDE_SWITCHED_OFF_NODES (includes parking lots, alleys, disabled nodes).
        // Use NODE_FLAG_ACTIVE_NODES_ONLY (0) if you only want active road nodes.
        public const int ROAD_NODE_TYPE_ALL = 1;                          // eGetClosestNodeFlags: includes switched-off nodes
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
        /// Safely get driving style speed multiplier by mode index with bounds checking.
        /// This is the PRIMARY differentiator between styles (reference: AutoDriveScript2.cs)
        /// </summary>
        public static float GetDrivingStyleSpeedMultiplier(int modeIndex)
        {
            if (modeIndex >= 0 && modeIndex < DRIVING_STYLE_SPEED_MULTIPLIERS.Length)
                return DRIVING_STYLE_SPEED_MULTIPLIERS[modeIndex];
            return DRIVING_STYLE_SPEED_MULTIPLIERS[DRIVING_STYLE_MODE_NORMAL];
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
        /// Get mod type display name by mod type index.
        /// Returns the name from MOD_TYPE_NAMES dictionary or a formatted fallback.
        /// </summary>
        public static string GetModTypeName(int modType)
        {
            if (MOD_TYPE_NAMES.TryGetValue(modType, out string name))
                return name;
            return $"Mod Type {modType}";
        }

        /// <summary>
        /// Get horn name by horn index.
        /// </summary>
        public static string GetHornName(int hornIndex)
        {
            if (hornIndex >= 0 && hornIndex < HORN_NAMES.Length)
                return HORN_NAMES[hornIndex];
            return $"Horn {hornIndex + 1}";
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

        // ===== TURRET CREW SYSTEM =====

        // Turret crew announcement modes
        public const int TURRET_ANNOUNCE_OFF = 0;
        public const int TURRET_ANNOUNCE_FIRING_ONLY = 1;
        public const int TURRET_ANNOUNCE_APPROACHING_ONLY = 2;
        public const int TURRET_ANNOUNCE_BOTH = 3;

        // Turret announcement mode display names
        public static readonly string[] TURRET_ANNOUNCE_MODE_NAMES = new string[]
        {
            "Off",
            "Firing Only",
            "Enemy Approaching Only",
            "Both"
        };

        /// <summary>
        /// Safely get turret announcement mode name by index with bounds checking.
        /// </summary>
        public static string GetTurretAnnouncementModeName(int mode)
        {
            if (mode >= 0 && mode < TURRET_ANNOUNCE_MODE_NAMES.Length)
                return TURRET_ANNOUNCE_MODE_NAMES[mode];
            return "Unknown";
        }

        // Vehicle hash to turret seat index mapping
        // Maps vehicle model hashes to arrays of seat indices that have turrets
        public static readonly Dictionary<int, int[]> TURRET_VEHICLE_SEATS = new Dictionary<int, int[]>
        {
            { 562680400, new int[] { 0, 1, 2 } },      // APC - driver and two gunner seats
            { -1860900134, new int[] { 5 } },          // Insurgent Pick-Up (insurgent3) - rear turret
            { -1831682906, new int[] { 0, 1, 2 } },    // Barrage - three turret positions
            { 837858166, new int[] { 1, 2 } },         // Technical - two gunner seats
            { -1600252419, new int[] { 0, 1, 2 } },    // Valkyrie - pilot and two door gunners
            { -1435527158, new int[] { 0, 1, 2 } },    // Khanjali tank - three turret seats
            { -32236122, new int[] { 1 } },            // HalfTrack - left rear seat turret
            { -212993243, new int[] { 1, 2 } },        // Barrage variant - .50 cal and grenade launcher
            { 1181327175, new int[] { 0 } },           // Akula - passenger minigun
            { -42959138, new int[] { 0 } },            // Hunter - passenger minigun
            { -2020647301, new int[] { 1 } },          // Menacer - rear turret
            { 408970549, new int[] { 0, 1, 2 } },      // Avenger - multiple turret positions
            { 1033245328, new int[] { 2 } },           // Insurgent (weaponized variant)
            { -1970118790, new int[] { 0 } },          // Savage - front gunner
            { -1660661558, new int[] { 0 } },          // Savage (alt hash)
            { -1216765807, new int[] { 0 } },          // Hunter helicopter
            { 788747387, new int[] { 0 } },            // Buzzard - pilot weapons
            { -339587598, new int[] { 1, 2 } },        // Annihilator - door gunners
            { -1628917549, new int[] { 1, 2 } }        // Annihilator2 - door gunners
        };

        // Turret engagement parameters
        public const float TURRET_MIN_ENGAGEMENT_RANGE = 25f;   // meters - dead zone to prevent self-damage from splash
        public const float TURRET_FULL_AUTO_RANGE = 80f;        // meters - full auto engagement (expanded from 50)
        public const float TURRET_AIM_RANGE = 150f;             // meters - aim at targets within this range
        public const float TURRET_CREW_DAMAGE_THRESHOLD = 0.5f; // 50% health threshold for damage warning

        // Turret engagement range squared (avoid sqrt in distance comparisons)
        public const float TURRET_MIN_ENGAGEMENT_RANGE_SQ = TURRET_MIN_ENGAGEMENT_RANGE * TURRET_MIN_ENGAGEMENT_RANGE;
        public const float TURRET_FULL_AUTO_RANGE_SQ = TURRET_FULL_AUTO_RANGE * TURRET_FULL_AUTO_RANGE;
        public const float TURRET_AIM_RANGE_SQ = TURRET_AIM_RANGE * TURRET_AIM_RANGE;

        // Turret target priority scores (higher = more threatening)
        public const int TURRET_PRIORITY_ATTACKING = 100;       // Currently shooting at player
        public const int TURRET_PRIORITY_ARMED_VEHICLE = 75;    // In hostile vehicle with weapons
        public const int TURRET_PRIORITY_ARMED_ON_FOOT = 50;    // Armed hostile on foot
        public const int TURRET_PRIORITY_UNARMED = 25;          // Hostile but unarmed

        // Turret combat tuning
        public const int TURRET_CREW_ACCURACY = 75;             // 0-100 accuracy scale
        public const int TURRET_COMBAT_ABILITY_PROFESSIONAL = 2; // SET_PED_COMBAT_ABILITY level
        public const int TURRET_COMBAT_RANGE_FAR = 2;           // SET_PED_COMBAT_RANGE level

        // Turret tick intervals
        public const long TICK_INTERVAL_TURRET_UPDATE = 500_000;          // 0.05s - turret AI update
        public const long TICK_INTERVAL_TURRET_ANNOUNCE = 50_000_000;     // 5s - announcement cooldown

        // Turret crew states
        public const int TURRET_STATE_IDLE = 0;
        public const int TURRET_STATE_AIMING = 1;
        public const int TURRET_STATE_FIGHTING = 2;

        // Natives for turret crew behavior
        public const ulong NATIVE_SET_PED_FIRING_PATTERN = 0x9AC577F5A12AD8A9;
        public const ulong NATIVE_TASK_VEHICLE_SHOOT_AT_COORD = 0x5190796ED39C9B6D;
        public const ulong NATIVE_IS_TURRET_SEAT = 0xE33FFA906CE74880;
        public const ulong NATIVE_TASK_COMBAT_HATED_TARGETS = 0x2BBA30B854534A0C;
        public const ulong NATIVE_HAS_ENTITY_BEEN_DAMAGED_BY = 0xC86D67D52A707CF8;
        public const ulong NATIVE_IS_PED_IN_COMBAT = 0x4859F1FC66A6278E;
        public const ulong NATIVE_SET_PED_COMBAT_ABILITY = 0xC7622C0D36B2FDA8;
        public const ulong NATIVE_SET_PED_COMBAT_RANGE = 0x3C606747B23E497B;

        // Firing patterns (uint values)
        public const uint FIRING_PATTERN_FULL_AUTO = 0xC6EE6B4C;
        public const uint FIRING_PATTERN_DEFAULT = 0;

        // ===== END TURRET CREW SYSTEM =====

        // ===== GTA ONLINE FEATURES IN SINGLE PLAYER =====

        // Native to enable MP maps in single player (ON_ENTER_MP)
        // This activates multiplayer map content (interiors, DLC locations) in single player
        public const ulong NATIVE_ON_ENTER_MP = 0x0888C3502DBBEEF5;

        // Native to disable MP maps (ON_ENTER_SP) - returns to standard single player map
        public const ulong NATIVE_ON_ENTER_SP = 0xD7C10C4A637992C9;

        // DLC check native - used to verify if specific DLC content is available
        // IS_DLC_PRESENT(dlcHash) - returns true if the specified DLC is present
        public const ulong NATIVE_IS_DLC_PRESENT = 0x812595A0644CE1DE;

        // Set this player as MP player for certain game systems
        // _SET_PLAYER_MODEL_IS_MP_PLAYER(bool toggle)
        public const ulong NATIVE_SET_PLAYER_MODEL_IS_MP_PLAYER = 0xFFFA0DE1DFEB4E72;

        // Enable/disable freemode property manager (for MP properties)
        // _ENABLE_FREEMODE_PROPERTY_MANAGER(bool toggle)
        public const ulong NATIVE_ENABLE_FREEMODE_PROPERTY_MANAGER = 0xC505036A35AFD01B;

        // Set instance priority mode (affects MP content loading)
        // SET_INSTANCE_PRIORITY_MODE(int mode) - 0=normal, 1=priority
        public const ulong NATIVE_SET_INSTANCE_PRIORITY_MODE = 0x35A3CD97B2C0A6D2;

        // Request IPL (Interior Proxy List) - loads specific interiors/exteriors
        public const ulong NATIVE_REQUEST_IPL = 0x41B4893843BBDB74;

        // Remove IPL - unloads specific interiors/exteriors
        public const ulong NATIVE_REMOVE_IPL = 0xEE6C5AD3ECE0A82D;

        // Check if IPL is active
        public const ulong NATIVE_IS_IPL_ACTIVE = 0x88A741E44A2B3495;

        // Get interior at coordinates
        public const ulong NATIVE_GET_INTERIOR_AT_COORDS = 0xB0F7F8663821D9C3;

        // Enable interior prop set
        public const ulong NATIVE_ENABLE_INTERIOR_PROP = 0x55E86AF2712B36A1;

        // Disable interior prop set
        public const ulong NATIVE_DISABLE_INTERIOR_PROP = 0x420BD37289EEE162;

        // Refresh interior (after changing props)
        public const ulong NATIVE_REFRESH_INTERIOR = 0x41F37C3F5D25F3AA;

        // ===== END GTA ONLINE FEATURES =====

        // ===== END CONSTANTS =====
    }
}
