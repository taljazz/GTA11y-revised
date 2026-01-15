# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GTA11Y is a deprecated accessibility mod for Grand Theft Auto V designed to help blind gamers play the game. It's a C# ScriptHookVDotNet plugin that provides audio cues, screen reader integration, and numpad-based navigation controls.

**Status:** Deprecated, preserved for historical/educational purposes. **Optimized:** December 2025 with performance improvements and bug fixes. **Enhanced:** January 2026 with aircraft accessibility features and stability improvements.

## Quick Reference

| Aspect | Details |
|--------|---------|
| **Target Framework** | .NET Framework 4.8 |
| **Language Version** | C# 7.3 |
| **Platform** | x64 only |
| **ScriptHookVDotNet** | 3.4.0.0 (upgraded January 2026) |
| **Assembly Name** | GrandTheftAccessibility.dll |
| **Namespace** | GrandTheftAccessibility |

## Build Commands

```bash
# Build Debug configuration (preferred method)
dotnet build "GTA\GTA11Y.csproj" -c Debug

# Build Release configuration
dotnet build "GTA\GTA11Y.csproj" -c Release
```

See `GTA/dotnet.md` for detailed build instructions.

**Output Location:** `GTA\bin\x64\Debug\` or `GTA\bin\x64\Release\`

## Architecture Overview

**⚠️ IMPORTANT:** This codebase was heavily optimized in December 2025. See additional documentation:
- `OPTIMIZATION_SUMMARY.md` - Complete optimization details, before/after comparisons
- `SCRIPTHOOK_COMPATIBILITY.md` - ScriptHookVDotNet 3.0.2.0 compatibility verification
- `MIGRATION_GUIDE.md` - Code migration examples from original → optimized
- `backups/GTA11Y_Original_Backup.cs` - Original 1500-line monolithic code (preserved)

### File Structure

```
GTA/
├── GTA11Y.cs                    # Main script (600+ lines, down from 1500)
├── Constants.cs                 # Centralized constants (50+ named constants)
├── AudioManager.cs              # Audio output (Tolk + NAudio + stereo panning)
├── SettingsManager.cs           # JSON settings (bool + int for multi-value)
├── EntityScanner.cs             # Entity detection with pooling
├── SpatialCalculator.cs         # Distance/direction calculations (GTA V coords)
├── Logger.cs                    # Async file logging with error throttling
├── HashManager.cs               # Centralized hash loading (~210 lines, NEW - Jan 2026)
├── AutoDriveManager.cs          # Autonomous driving system (~3200 lines)
├── AutoFlyManager.cs            # Aircraft autopilot & autoland system (~800 lines)
├── WeatherManager.cs            # Weather detection & speed multipliers (~170 lines, NEW)
├── CollisionDetector.cs         # TTC-based collision warnings (~230 lines, NEW)
├── CurveAnalyzer.cs             # Physics-based curve speed (~260 lines, NEW)
├── CurveTypes.cs                # CurveInfo struct, CurveSeverity enum (~60 lines, NEW)
├── RecoveryManager.cs           # Stuck detection & recovery (~420 lines, NEW)
├── AnnouncementQueue.cs         # Priority announcement throttling (~140 lines, NEW)
├── Location.cs                  # Teleport destinations (legacy)
├── VehicleSpawn.cs              # Vehicle spawn data
├── SavedVehicle.cs              # Vehicle save data model
├── VehicleSaveManager.cs        # JSON persistence for saved vehicles
├── Result.cs                    # Entity search results (pooled)
├── setting.cs                   # Legacy setting model
├── dotnet.md                    # Build instructions
├── backups/
│   └── GTA11Y_Original_Backup.cs  # Original 1500-line monolithic code
├── Data/
│   └── LocationData.cs          # Centralized location data (~375 lines, NEW)
├── Menus/
│   ├── IMenuState.cs           # Menu interface (with submenu support)
│   ├── MenuManager.cs          # Menu coordinator (10 menus)
│   ├── LocationMenu.cs         # Teleport menu with categories (76 locations, 9 categories)
│   ├── WaypointMenu.cs         # GPS waypoint menu (85 locations)
│   ├── AutoDriveMenu.cs        # AutoDrive control menu
│   ├── AircraftLandingMenu.cs  # Aircraft landing destinations with navigation/autofly
│   ├── AutoFlyMenu.cs          # AutoFly control menu
│   ├── VehicleSpawnMenu.cs     # Vehicle spawn (with category filter + class announcement)
│   ├── VehicleCategoryMenu.cs  # Vehicle categories menu (24 categories)
│   ├── VehicleModMenu.cs       # Vehicle tuning/mods menu
│   ├── VehicleModMenuProxy.cs  # Proxy for mod menu (Handle-based vehicle caching)
│   ├── VehicleSaveLoadMenu.cs  # Vehicle save/load slots (10 slots)
│   ├── FunctionsMenu.cs        # Chaos functions + Mark Waypoint
│   └── SettingsMenu.cs         # Settings toggles (bool + int)
└── GTA11Y.csproj               # Project file (updated with new files)
```

### Core Components

#### GTA11Y.cs - Main Script Class
- **Lines:** ~600 (optimized from 1500)
- **Inherits:** `GTA.Script`
- **Event Handlers:** `OnTick`, `OnKeyDown`, `OnKeyUp`
- **Pattern:** Delegates to manager classes
- **Optimization:** Heavily throttled tick event (70% CPU reduction)

#### Manager Classes (Separation of Concerns)

**AudioManager.cs**
- Manages Tolk (screen reader) and NAudio (spatial audio)
- Implements IDisposable for proper cleanup
- Pre-initializes audio resources (no allocations during gameplay)
- **Tolk resilience:** Auto-reconnects if screen reader disconnects
- **Stereo panning:** Roll indicator uses `StereoToMonoSampleProvider` + `PanningSampleProvider`
- **Optimized indicators:** Pre-created sample providers, `Init()` called only once (not per-frame)
- Methods: `Speak()`, `PlayPedTargetSound()`, `PlayAltitudeIndicator()`, `PlayAircraftPitchIndicator()`, `PlayAircraftRollIndicator()`, `UpdateAircraftIndicators()`

**SettingsManager.cs**
- JSON persistence to `Documents/Rockstar Games/GTA V/ModSettings/gta11ySettings.json`
- Uses **bool** (true/false) for toggles, **int** for multi-value settings (e.g., altitudeMode)
- Auto-repair corrupted settings files
- 21 settings with display names
- `GetIntSetting()` / `SetIntSetting()` for multi-value options

**EntityScanner.cs**
- Scans nearby vehicles, peds, props, doors
- **Object pooling:** Pre-allocated 50 Result objects (90% allocation reduction)
- **StringBuilder:** Eliminates string allocations in formatting
- **TryGetValue:** Single dictionary lookup instead of ContainsKey + []

**SpatialCalculator.cs**
- Static utility class (no instantiation)
- Direction calculations (8-slice compass: N, NE, E, SE, S, SW, W, NW)
- Distance calculations (horizontal XY + vertical Z)
- Heading conversions (degrees → compass names)
- **GTA V coordinate fix:** Handles mirrored coordinate system (90° = West, not East)

**Logger.cs** (NEW - January 2026)
- File-based logging to `Documents/Rockstar Games/GTA V/ModSettings/gta11y.log`
- **Async writing:** Background thread with queue (non-blocking)
- **Error throttling:** Suppresses repeated identical errors for 5 seconds
- **Fresh start:** Log file deleted on each session to avoid confusion
- Log levels: Debug, Info, Warning, Error
- Default MinLevel is Info (set to Debug only when debugging)

**Constants.cs**
- All magic numbers centralized (50+ named constants)
- Tick intervals, search radii, conversion factors
- Aircraft type constants and thresholds
- VTOL and Blimp vehicle hash sets for O(1) lookup
- Weather hash constants for speed adjustments
- Announcement priority levels and cooldowns
- Recovery system constants and thresholds
- Curve detection and slowdown parameters
- Easy performance tuning (adjust intervals in one place)

**HashManager.cs** (NEW - January 2026)
- Static centralized hash loading (replaces duplication in EntityScanner/GTA11Y)
- Thread-safe singleton with lazy initialization
- Uses int keys (not strings) for zero-allocation lookups
- `TryGetName(int hash, out string name)` - primary lookup API
- `ContainsHash(int hash)` - existence check
- Graceful failure with empty dictionary fallback
- Parse error limiting (stops after 100 errors)

**AutoDriveManager.cs** (NEW - January 2026)
- Comprehensive autonomous driving system (~3200 lines, refactored January 2026)
- **Navigation modes:** Waypoint, Wander, Road Type Seeking
- **Driving styles:** Normal, Cautious, Aggressive, Reckless
- **Environmental awareness:** Delegates to WeatherManager
- **Safety features:** Delegates to CollisionDetector
- **Curve handling:** Delegates to CurveAnalyzer
- **Recovery system:** Delegates to RecoveryManager
- **Announcements:** Delegates to AnnouncementQueue
- **Pre-allocated collections:** Zero per-tick allocations
- Methods: `StartWaypoint()`, `StartWander()`, `Update()`, `Stop()`, `Pause()`, `Resume()`

**WeatherManager.cs** (NEW - January 2026)
- Weather detection and speed multiplier calculation
- Road friction coefficient for physics-based curve speeds
- Speed multipliers: Clear (1.0x), Rain (0.8x), Thunder (0.7x), Snow (0.6x), Blizzard (0.5x)
- `GetRoadFrictionCoefficient()` - weather-dependent friction for curve speed calculation

**CollisionDetector.cs** (NEW - January 2026)
- Time-To-Collision (TTC) based collision warnings
- Realistic time-based following distance (2-3 second rule)
- Warning levels: None, Far, Medium, Close, Imminent
- `CheckCollisionWarning()` - returns warning message and priority
- `CheckFollowingDistance()` - returns following state (0-4)

**CurveAnalyzer.cs** (NEW - January 2026)
- Physics-based safe speed calculation: v = sqrt(mu * g * r)
- Curve severity classification: None, Gentle, Moderate, Sharp, Hairpin
- Pre-allocated OutputArguments for road node queries
- `AnalyzeCurve()` - returns CurveInfo with severity, direction, safe speed
- `DetectCurveAhead()` - scans road ahead for curves

**RecoveryManager.cs** (NEW - January 2026)
- Stuck detection and multi-stage recovery
- Vehicle state monitoring (flipped, water, fire, critical damage)
- Recovery strategies: ReverseTurn, ForwardTurn, ThreePointTurn
- Progress timeout detection (no waypoint progress for 30 seconds)
- `CheckIfStuck()`, `StartRecovery()`, `UpdateRecovery()`

**AnnouncementQueue.cs** (NEW - January 2026)
- Priority-based announcement throttling
- Cooldowns: Critical (0.5s), High (2s), Medium (3s), Low (5s)
- `TryAnnounce()` - announce with throttling
- `AnnounceImmediate()` - bypass throttling for critical messages

**AutoFlyManager.cs** (NEW - January 2026)
- Aircraft autopilot and autoland system (~800 lines)
- **Flight modes:** Cruise (maintain heading/altitude), Waypoint (fly to GPS), Destination (autoland)
- **Aircraft type handling:** Fixed-wing, Helicopter, VTOL (hover/plane modes), Blimp
- **Phase state machine:** CRUISE → APPROACH → FINAL → TOUCHDOWN → LANDED
- **Blimp special handling:** Navigation only, announces "manual landing required"
- **Landing tasks:** `TASK_PLANE_LAND` for fixed-wing, `TASK_HELI_MISSION` with land flag for helicopters
- **Landing gear:** Auto-deploys for fixed-wing on final approach
- **Speed limits:** Aircraft type-specific (blimps: 8-25 m/s, helicopters: 30 m/s, planes: 50 m/s)
- **Announcement priority queue:** Distance milestones, approach guidance, phase transitions
- Methods: `StartCruise()`, `StartWaypoint()`, `StartDestination()`, `Update()`, `Stop()`, `Pause()`, `Resume()`

#### Menu System (State Pattern with Hierarchical Submenus)

**IMenuState** interface defines:
- `NavigatePrevious()` / `NavigateNext()` - Menu navigation
- `GetCurrentItemText()` - Speech output
- `ExecuteSelection()` - Menu action
- `HasActiveSubmenu` - Returns true if in submenu (NEW)
- `ExitSubmenu()` - Exit current submenu (NEW)

**Menu Order (10 menus):**
1. **LocationMenu** - 76 teleport locations in 9 categories (with submenu navigation)
2. **WaypointMenu** - 85 GPS waypoint destinations for driving (NEW - January 2026)
3. **AutoDriveMenu** - Autonomous driving controls (NEW - January 2026)
4. **AircraftLandingMenu** - 50+ aircraft landing destinations with AutoFly integration (NEW)
5. **AutoFlyMenu** - Aircraft autopilot controls (cruise, waypoint, altitude/speed) (NEW - January 2026)
6. **VehicleCategoryMenu** - Vehicle spawn by category (24 categories) → submenu per category
7. **VehicleModMenuProxy** - Vehicle modifications (only when in vehicle)
8. **VehicleSaveLoadMenu** - Save/Load/Clear vehicle slots (10 slots)
9. **FunctionsMenu** - Chaos functions + Mark Waypoint to Mission Objective
10. **SettingsMenu** - Toggle all 21 settings

**MenuManager** coordinates menu hierarchy, state transitions, and submenu navigation.

### Key Architecture Patterns

#### 1. Tick Event Throttling (Performance Critical)

**Original:** Everything ran every frame (60 FPS = 60 ops/sec)
**Optimized:** Operations run at appropriate intervals

```csharp
private void OnTick(object sender, EventArgs e)
{
    if (Game.IsLoading) return;

    long currentTick = DateTime.Now.Ticks;

    // Altitude - throttled to 0.1s (not every frame!)
    if (_settings.GetSetting("altitudeIndicator") &&
        currentTick - _lastAltitudeTick > Constants.TICK_INTERVAL_ALTITUDE)
    {
        _lastAltitudeTick = currentTick;
        // Only update if changed significantly
        if (Math.Abs(altitude - _lastAltitude) > Constants.HEIGHT_CHANGE_THRESHOLD)
        {
            _lastAltitude = altitude;
            _audio.PlayAltitudeIndicator(altitude);
        }
    }

    // Per-frame operations (must run every tick)
    ApplyCheatSettings(player, currentVehicle);
}
```

**Throttle Intervals:**
| Operation | Interval | Original |
|-----------|----------|----------|
| Vehicle speed | 2.5s | Every frame |
| Target lock | 0.2s | Every frame |
| Street check | 0.5s | Every frame |
| Zone check | 0.5s | Every frame |
| Altitude | 0.1s | Every frame |
| Pitch | 0.05s | Every frame |

#### 2. Object Pooling (Memory Optimization)

```csharp
// EntityScanner.cs
private readonly List<Result> _resultPool;  // Pre-allocated

public EntityScanner()
{
    _resultPool = new List<Result>(50);
    for (int i = 0; i < 50; i++)
    {
        _resultPool.Add(new Result("", 0, 0, ""));  // Create once
    }
}

private Result GetPooledResult(...)
{
    if (_poolIndex < _resultPool.Count)
    {
        Result r = _resultPool[_poolIndex++];
        r.name = name;  // Reuse existing object
        // ... update fields
        return r;
    }
    return new Result(...);  // Pool exhausted, create new
}
```

**Impact:** Eliminates ~3600 allocations per minute during scanning

#### 3. StringBuilder for String Operations

```csharp
// Before (allocates new string each iteration):
text = text + i.xyDistance + " meters " + i.direction + ", ";

// After (reuses buffer):
_resultBuilder.Clear();
_resultBuilder.Append(i.xyDistance).Append(" meters ").Append(i.direction).Append(", ");
```

#### 4. Cached State (Avoid Redundant Checks)

```csharp
// Cache frequently accessed values
private string _currentWeaponHash;
private string _currentStreet;
private string _currentZone;
private float _lastAltitude;
private float _lastPitch;

// Only announce on change
if (weaponHash != _currentWeaponHash)
{
    _currentWeaponHash = weaponHash;
    _audio.Speak(weaponHash);
}
```

### Controls (Numpad-Based)

| Key | Function |
|-----|----------|
| **NumPad0** | Current location/time info |
| **Ctrl+NumPad0** | Current heading/direction (moved from Decimal) |
| **NumPad1** | Previous submenu item |
| **Ctrl+NumPad1** | Previous item (fast-scroll -25 in vehicle menu) |
| **NumPad2** | Select/activate current item (enter submenu) |
| **Ctrl+NumPad2** | Toggle accessibility keys on/off |
| **NumPad3** | Next submenu item |
| **Ctrl+NumPad3** | Next item (fast-scroll +25 in vehicle menu) |
| **NumPad4** | List nearby vehicles |
| **NumPad5** | List nearby doors/gates |
| **NumPad6** | List nearby pedestrians |
| **NumPad7** | Previous main menu |
| **NumPad8** | List nearby objects |
| **NumPad9** | Next main menu |
| **Decimal (.)** | **Exit submenu / Back** (when in submenu), or heading (when not) |
| **Ctrl+Decimal** | Current time with minutes |

### Audio Feedback System

**Screen Reader (Tolk):**
- Text-to-speech announcements
- Location, vehicle names, menu items
- Managed by AudioManager

**Spatial Audio (NAudio):**
1. **tped.wav** - Pedestrian target lock
2. **tvehicle.wav** - Vehicle target lock
3. **tprop.wav** - Destructible prop target lock
4. **Altitude Indicator** - Triangle wave, frequency varies with height (120 + height*40 Hz)
5. **Pitch Indicator** - Square wave, frequency varies with aim angle (600 + pitch*6 Hz)
6. **Aircraft Pitch Indicator** - Sine wave, center channel, frequency varies with nose angle
7. **Aircraft Roll Indicator** - Sawtooth wave, stereo panned left/right based on bank angle
8. **Lane-Keeping Indicator** - Sine wave, stereo panned left/right based on lane position
9. **Collision Warning** - Square wave beep, higher frequency when closer to vehicle ahead

### Settings System

**Storage:** `%USERPROFILE%\Documents\Rockstar Games\GTA V\ModSettings\gta11ySettings.json`

**Settings (boolean + integer):**

| Setting ID | Display Name | Type | Default |
|------------|--------------|------|---------|
| announceHeadings | Heading Change Announcements | bool | true |
| announceZones | Street and Zone Change Announcements | bool | true |
| announceTime | Time of Day Announcements | bool | true |
| altitudeMode | Altitude Indicator Mode | int | 1 |
| targetPitchIndicator | Audible Targeting Pitch Indicator | bool | true |
| radioOff | Always Disable vehicle radios | bool | false |
| warpInsideVehicle | Teleport player inside newly spawned vehicles | bool | false |
| onscreen | Announce only visible nearby items | bool | false |
| speed | Announce current vehicle speed | bool | false |
| godMode | God Mode | bool | false |
| policeIgnore | Police Ignore Player | bool | false |
| vehicleGodMode | Make Current vehicle indestructible | bool | false |
| infiniteAmmo | Unlimited Ammo | bool | false |
| neverWanted | Wanted Level Never Increases | bool | false |
| superJump | Super Jump | bool | false |
| runFaster | Run Faster | bool | false |
| swimFaster | Fast Swimming | bool | false |
| explosiveAmmo | Explosive Ammo | bool | false |
| fireAmmo | Fire Ammo | bool | false |
| explosiveMelee | Explosive Melee | bool | false |
| aircraftAttitude | Aircraft Attitude Indicator | bool | false |

**Altitude Mode Values (altitudeMode):**
- `0` = Off
- `1` = Normal (tone-based, frequency varies with height)
- `2` = Aircraft (spoken altitude in feet, fine/coarse intervals)

**Auto-Repair:** If JSON is corrupted, file is deleted and recreated with defaults.

## ScriptHookVDotNet 3.0.2.0 Compatibility

**Version:** ScriptHookVDotNet3 v3.0.2.0 (verified compatible)
**Release:** [GitHub v3.0.2](https://github.com/scripthookvdotnet/scripthookvdotnet/releases/tag/v3.0.2)

### Compatibility Fixes Applied

**1. Audio API Namespace (5 instances)**
```csharp
// Wrong:
Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");

// Correct:
GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
```

**2. C# Language Version (2 instances)**
```csharp
// Wrong (C# 8.0 - incompatible):
return slice switch { 0 => "north", _ => "unknown" };

// Correct (C# 7.3):
switch (slice) { case 0: return "north"; default: return "unknown"; }
```

### All APIs Verified Compatible ✅

- `GTA.Script`, `Game.Player`, `Game.IsLoading`
- `World.GetNearbyVehicles/Peds/Props()`, `World.CreateVehicle()`
- `GameplayCamera`, `GameplayCamera.IsAimCamActive`, `GameplayCamera.RelativePitch`
- `GTA.Audio.PlaySoundFrontend()`
- Entity types: `Ped`, `Vehicle`, `Prop`, `Entity`
- Enums: `WeaponHash`, `VehicleHash`, `VehicleSeat`, `EntityType`
- Event handlers: `Tick`, `KeyDown`, `KeyUp`

### Why v3.0.2 Matters

**Bug Fixes in 3.0.2:**
1. INI settings save bug ("//0" appended)
2. **Keyboard modifier key fix** ← **Critical for this mod!**
3. Subdirectory script loading restored

**This mod relies on modifier keys:**
- Ctrl+NumPad1/3: Fast-scroll vehicle menu (±25 items)
- Ctrl+NumPad2: Toggle accessibility keys
- Ctrl detection via `_controlHeld` boolean

See `SCRIPTHOOK_COMPATIBILITY.md` for complete verification.

## Performance Optimizations

### Estimated Improvements

- **CPU Usage:** 5-10x reduction during gameplay
- **Garbage Collections:** 70% fewer
- **Memory Allocations:** 90% reduction per frame
- **Frame Pacing:** More consistent (better responsiveness)

### Key Optimizations

**1. Tick Throttling**
- Vehicle speed: Every 2.5s (was every frame)
- Street/zone: Every 0.5s (was every frame)
- Result: 70% less work per frame

**2. Object Pooling**
- 50 Result objects pre-allocated
- Reused every scan (no GC pressure)
- Result: 90% fewer allocations

**3. StringBuilder Usage**
- Entity scan results use reusable buffer
- Eliminates string concatenation allocations
- Result: Zero string allocations in scan formatting

**4. Dictionary Optimization**
```csharp
// Before: 2 lookups
if (hashes.ContainsKey(key))
    if (hashes[key] != "player_one")

// After: 1 lookup
if (hashes.TryGetValue(key, out string name) && name != "player_one")
```

**5. Cached State**
- Weapon hash, street, zone cached
- Only update/announce on change
- Result: No redundant API calls

### Tuning Performance

Edit `Constants.cs` to adjust throttling:

```csharp
// More responsive (higher CPU):
public const long TICK_INTERVAL_VEHICLE_SPEED = 10_000_000;  // 1.0s

// Less responsive (lower CPU):
public const long TICK_INTERVAL_VEHICLE_SPEED = 50_000_000;  // 5.0s
```

**Note:** 10,000 ticks = 1 millisecond

## Bug Fixes (from Original Code)

All fixed in optimized version:

1. ✅ **Line 323:** Duplicate `InfiniteAmmo = true` assignment
2. ✅ **Lines 413-418:** Duplicate ped targeting check in `onTick`
3. ✅ **Line 1391:** Malformed path `"/Rockstar Games / GTA V / ModSettings/"` (spaces!)
4. ✅ **Lines 615/618:** Door/gate filter logic always true: `(Contains("door") == false || !Contains("gate") == false)`
5. ✅ **Direction mapping:** `getDir()` had overlapping/incorrect ranges
6. ✅ **Typos:** exsplosive → explosive, unlimitted → unlimited, indestructable → indestructible

## January 2026 Enhancements

### Aircraft Accessibility Features

**3-Way Altitude Mode:**
- **Off (0):** No altitude feedback
- **Normal (1):** Tone-based indicator, frequency varies with height above ground
- **Aircraft (2):** Spoken altitude in feet with smart intervals:
  - Below 500ft: Announce every 50ft
  - Above 500ft: Announce every 500ft

**Aircraft Attitude Indicator:**
When in an aircraft with altitude mode enabled, provides audio feedback for:

1. **Pitch (nose up/down):**
   - Sine wave in center channel
   - Higher frequency = nose up, lower = nose down
   - Pulse rate varies with angle (steeper = faster pulses)

2. **Roll (bank left/right):**
   - Sawtooth wave with stereo panning
   - Sound pans to the direction the aircraft is tilting
   - Uses `StereoToMonoSampleProvider` + `PanningSampleProvider` (NAudio requires mono input for panning)

3. **Inverted/Upright Announcements (fixed-wing only):**
   - Announces "inverted" when roll exceeds 90°
   - Announces "upright" when returning to normal

**Aircraft Type Detection:**

| Type | Detection Method | Thresholds |
|------|------------------|------------|
| Fixed-wing | Default for planes | 5°/15°/30° (level/slight/moderate) |
| Helicopter | `VehicleClass.Helicopters` | 3°/10°/20° (tighter) |
| Blimp | Model hash in `BLIMP_VEHICLE_HASHES` | 2°/5°/10° (tightest) |
| VTOL (hover) | Nozzle position > 0.5 | Uses helicopter thresholds |
| VTOL (plane) | Nozzle position ≤ 0.5 | Uses fixed-wing thresholds |

**VTOL Detection:**
```csharp
// Uses native function to detect Hydra/Avenger mode
float nozzlePosition = Function.Call<float>(
    (Hash)Constants.NATIVE_GET_VEHICLE_FLIGHT_NOZZLE_POSITION, vehicle);
// > 0.5 = hover mode, ≤ 0.5 = plane mode
```

### Direction Fix (GTA V Coordinate System)

GTA V uses a mirrored/counterclockwise coordinate system:
- **Standard compass:** 90° = East
- **GTA V:** 90° = West

Fixed in `SpatialCalculator.cs` to correctly report directions.

### Teleport Menu (LocationMenu) Reorganization (NEW - January 2026)

The teleport menu has been reorganized from a flat list into 9 categories with 76 total locations:

**Location Categories (76 total):**

| Category | Count | Examples |
|----------|-------|----------|
| Character Houses | 5 | Michael, Franklin, Trevor, Floyd, Lester |
| Airports and Runways | 11 | LSIA Runway 03/21/12/30, Sandy Shores, McKenzie, Fort Zancudo |
| Sniping Vantage Points | 16 | Maze Bank Tower Roof, FIB/IAA Building Roof, Mile High Club, Galileo Observatory |
| Military and Restricted | 6 | Fort Zancudo (Main/ATC/Hangar), Humane Labs, NOOSE HQ |
| Landmarks | 8 | Diamond Casino, Del Perro Pier, Playboy Mansion, Pacific Standard Bank |
| Blaine County | 9 | Sandy Shores, Grapeseed, Paleto Bay, Altruist Camp, Hippy Camp |
| Coastal and Beaches | 7 | Vespucci Beach, Del Perro Beach, Chumash, Paleto Beach |
| Remote Areas | 7 | Far North San Andreas, Chiliad Wilderness, Raton Canyon |
| Emergency Services | 7 | Police Stations, Hospitals, Fire Station |

**Runway Locations (for aircraft spawning):**
- LSIA has 4 runway ends (03/21/12/30) plus terminal and center field
- Sandy Shores, McKenzie, Grapeseed airstrips
- Fort Zancudo runway (north/south ends)

**Sniping Vantage Points:**
- High-rise rooftops: Maze Bank Tower (326m), FIB (262m), IAA (206m), Mile High Club (243m)
- Natural elevations: Galileo Observatory, Vinewood Sign, Mt Chiliad Summit, Mt Gordo
- Industrial structures: Sandy Shores Water Tower, Crane, Land Act Dam

**Navigation Flow:**
```
NumPad7/9      → Navigate to "Teleport to location" menu
NumPad1/3      → Navigate between categories
NumPad2        → Enter category (submenu)
  NumPad1/3    → Navigate locations (±1)
  Ctrl+NumPad1/3 → Fast-scroll (±10)
  NumPad2      → Teleport to location
  Decimal      → Back to category list
```

**Teleport System (Robust Implementation - January 2026):**

The teleportation system uses `SET_ENTITY_COORDS_NO_OFFSET` - the same native used by the Native Trainer, proven to be the most reliable teleportation method in GTA V.

**Implementation (`LocationMenu.TeleportToLocation()`):**

```csharp
// Use SET_ENTITY_COORDS_NO_OFFSET - the most reliable teleport method
// Parameters: entity, x, y, z, keepTasks, keepIK, doWarp
Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET,
    entityToTeleport.Handle,
    destination.X,
    destination.Y,
    destination.Z,
    false,  // keepTasks - clear tasks
    false,  // keepIK - reset IK
    true);  // doWarp - instant warp, clear contacts
```

**Why SET_ENTITY_COORDS_NO_OFFSET:**

| Parameter | Value | Effect |
|-----------|-------|--------|
| `keepTasks` | `false` | Clears active tasks (parachuting, climbing, etc.) |
| `keepIK` | `false` | Resets inverse kinematics (animations) |
| `doWarp` | `true` | Instant warp, clears contacts, creates space |

**Key Design Decisions:**

1. **NO `Script.Wait()` calls** - `Script.Wait()` blocks the entire script thread and causes game hangs. All teleportation is non-blocking.

2. **Entity existence check** - Uses `DOES_ENTITY_EXIST` to verify the player entity is valid before teleporting (critical after death/respawn).

3. **Vehicle handling** - If player is in a vehicle, teleports the vehicle (not the player) using `player.CurrentVehicle`.

4. **Velocity clearing** - Calls `SET_ENTITY_VELOCITY` with zeros to prevent continued movement after teleport.

5. **Vehicle ground placement** - Calls `SET_VEHICLE_ON_GROUND_PROPERLY` to ensure vehicles land correctly.

**Why NOT Other Approaches:**

| Approach | Problem |
|----------|---------|
| `START_PLAYER_TELEPORT` | 100-frame timeout for Z-finding, doesn't work reliably in all states |
| `Script.Wait()` loops | Blocks game, causes hangs and freezes |
| `SET_ENTITY_COORDS` | Less reliable, doesn't clear contacts properly |
| Complex state machines | Over-engineered, same issues as waiting |

**Fallback Chain:**

1. Primary: `SET_ENTITY_COORDS_NO_OFFSET` with native call
2. Fallback: `entity.Position = destination` (SHVDN property setter)

**Sources & References:**

- [LambdaMenu teleportation.cpp](https://github.com/citizenfx/project-lambdamenu/blob/master/LambdaMenu/teleportation.cpp) - FiveM's teleport implementation
- [Native Trainer script.cpp](https://github.com/croced/GTA-V-Improved-Trainer/blob/master/samples/NativeTrainer/script.cpp) - Alexander Blade's implementation
- [SET_ENTITY_COORDS_NO_OFFSET docs](https://github.com/citizenfx/natives/blob/master/ENTITY/SetEntityCoordsNoOffset.md) - Native function documentation
- [ScriptHookVDotNet Script.Wait issue](https://github.com/crosire/scripthookvdotnet/issues/30) - Why Script.Wait() causes problems

### Tolk (Screen Reader) Resilience

`AudioManager.cs` now includes:
- Auto-reconnection after screen reader disconnects
- Failure counting with reconnect after 3 failures
- 5-second cooldown between reconnect attempts
- `CheckTolkHealth()` method called periodically from OnTick

### ApplyCheatSettings Optimization

Write-only properties (`Player.IgnoredByPolice`, `Weapon.InfiniteAmmo`) cannot be read to check current state. Fixed by:
- Adding cached state fields (`_cachedPoliceIgnore`, `_cachedInfiniteAmmo`)
- Only setting properties when the setting value changes

### Logger Error Throttling

Prevents log flooding from repeated identical errors:
- Tracks last log time per error signature
- Suppresses identical errors for 5 seconds
- Reports suppression count when error is finally logged

### Initialization Hardening

Fixed "Failed Initialization" crashes caused by:

**1. Game.Player Access in Constructor (GTA11Y.cs)**
- Problem: Accessing `Game.Player.Character.Weapons.Current` in constructor before game loads
- Fix: Initialize `_currentWeaponHash` to empty string, set on first tick instead

**2. Audio File Loading (AudioManager.cs)**
- Problem: Missing audio files caused exceptions during initialization
- Fix: Added `File.Exists()` checks and try-catch blocks
- Missing files now log a warning instead of crashing

**3. Null Checks in Audio Methods**
- Added null checks to `PlayPedTargetSound()`, `PlayVehicleTargetSound()`, `PlayPropTargetSound()`
- Methods safely return if audio files weren't loaded

### Vehicle Speed Setting Fix

- `speed` setting was not being checked before announcing vehicle speed
- Fixed in GTA11Y.cs to check `_settings.GetSetting("speed")` before speaking

### Removed autoAim Setting

- Removed unused `autoAim` setting (no feature implemented for it)
- Removed from `DefaultSettings` and `SettingDisplayNames` in SettingsManager.cs

### GPS Waypoint Menu (NEW - January 2026)

New menu for setting GPS waypoints to drive to predefined destinations:

**Features:**
- Sets GPS waypoint marker on the map for driving navigation
- 85 locations organized by category for easy navigation
- Plays standard GTA waypoint confirmation sound
- GPS route appears on minimap automatically
- Fast-scroll with Ctrl+NumPad1/3 (jumps 10 locations)
- Displays current position: "X of 85: Location Name"

**Location Categories (85 total):**

| Category | Count | Examples |
|----------|-------|----------|
| LS Customs / Garages | 5 | Burton, La Mesa, Airport, Harmony, Beaker's |
| Freeways - Los Santos | 5 | Del Perro, La Puerta, Olympic, Elysian |
| Freeways - Blaine County | 6 | Senora, Route 68, Great Ocean Highway |
| Airports | 4 | LSIA, McKenzie, Sandy Shores |
| Piers & Docks | 5 | Del Perro Pier, Merryweather, Cargo Ship |
| Gas Stations | 6 | Downtown LS, Paleto, Sandy Shores, Procopio |
| Main Character Houses | 5 | Michael, Franklin, Trevor, Floyd, Lester |
| Landmarks | 10 | Maze Bank, Vinewood Sign, Casino, Observatory |
| Military & Restricted | 4 | Fort Zancudo, NOOSE, Humane Labs |
| Emergency Services | 3 | Police Station, Hospital, Coroner |
| Blaine County | 13 | Mt Chiliad, Altruist Camp, Quarry, Wind Farm |
| Coastal Drives | 6 | Pacific Bluffs, Playboy Mansion, Mt Gordo |
| Banks | 2 | Pacific Standard, Blaine County Savings |
| Entertainment | 5 | Strip Club, Vinewood Bowl, Maze Bank Arena |
| Industrial | 4 | Power Station, Sawmill, Meth Lab |
| Extreme North/South | 3 | Far North, Chiliad Wilderness, Calafia Bridge |
| Neighborhoods | 5 | Little Seoul, Mirror Park, Epsilon Building |
| Boats & Yachts | 2 | Yacht, Aircraft Carrier |

**Implementation:**
```csharp
// Set GPS waypoint at X, Y coordinates
Function.Call(Hash.SET_NEW_WAYPOINT, destination.X, destination.Y);

// Play confirmation sound
GTA.Audio.PlaySoundFrontend("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");
```

**Navigation Flow:**
```
NumPad7/9      → Navigate to "Set GPS Waypoint" menu
NumPad1/3      → Navigate between destinations (±1)
Ctrl+NumPad1/3 → Fast-scroll (±10 locations)
NumPad2        → Set waypoint (GPS route appears on minimap)
```

**File:** `Menus/WaypointMenu.cs`

### Aircraft Landing Menu (NEW - January 2026)

New menu specifically for aircraft, providing landing destinations with in-flight navigation guidance:

**Features:**
- 50+ landing destinations (airports, helipads, flat areas)
- Sets GPS waypoint and activates voice navigation
- In-flight announcements with distance, direction, turn guidance
- Altitude guidance when approaching destination
- Runway heading information for airports

**Landing Destination Categories:**

| Category | Count | Examples |
|----------|-------|----------|
| Major Airports | 10 | LSIA (4 runway ends), Sandy Shores, McKenzie, Fort Zancudo |
| Hospital Helipads | 4 | Central LS, Pillbox Hill, Mount Zonah, Sandy Shores Medical |
| Police Helipads | 3 | LSPD HQ, Vespucci, Mission Row |
| Government/Official | 4 | FIB Building, IAA Building, City Hall, NOOSE HQ |
| Corporate Buildings | 5 | Maze Bank Tower, Arcadius, Lombank West |
| Military/Special | 4 | Fort Zancudo helipads, Aircraft Carrier, Humane Labs |
| Mountain/Remote | 6 | Mt Chiliad Summit, Altruist Camp, Observatory |
| Beaches/Flat Areas | 4 | Vespucci Beach, Del Perro Beach, Paleto Beach |
| Emergency Roads | 3 | Great Ocean Highway, Route 68, Senora Freeway |

**In-Flight Navigation:**

When you select a destination, the menu activates navigation mode with periodic voice announcements:

```
"Navigation active to LSIA Runway 3 West, southwest, 2.3 miles, runway heading 93 degrees"
```

**Announcement Intervals (based on distance):**
- > 5 miles: Every 2 miles
- 1-5 miles: Every half mile
- < 1 mile: Every quarter mile

**Navigation Announcements Include:**
- Distance (miles, quarter/half mile, or feet when close)
- Direction (north, southwest, etc.)
- Turn guidance when off-course: "turn right 45 degrees"
- Altitude guidance when close: "descend 500 feet"
- Runway heading for airports: "align runway 93"

**Implementation:**
```csharp
// AircraftLandingMenu.cs
public void UpdateNavigation(Vehicle aircraft, Vector3 position, long currentTick)
{
    // Called from OnTick when in aircraft
    // Provides periodic voice navigation updates
    // Handles distance-based announcement intervals
    // Calculates heading difference for turn guidance
}
```

**Navigation Flow:**
```
NumPad7/9      → Navigate to "Aircraft Landing" menu
NumPad1/3      → Browse destinations (±1)
Ctrl+NumPad1/3 → Fast-scroll (±10)
NumPad2        → Activate navigation (sets waypoint + voice guidance)
```

**File:** `Menus/AircraftLandingMenu.cs`

### AutoFly & AutoLand System (NEW - January 2026)

Full aircraft autopilot system integrated with AircraftLandingMenu for automatic flight and landing:

**Flight Modes:**

| Mode | Description | Behavior |
|------|-------------|----------|
| **Cruise** | Maintain current state | Holds altitude and heading, adjustable speed |
| **Waypoint** | Fly to GPS marker | Navigates to waypoint, then circles |
| **Destination** | Fly and land | Full autopilot to AircraftLandingMenu destination with autoland |

**Aircraft Type Handling:**

| Type | Detection | Navigation Task | Landing Task |
|------|-----------|-----------------|--------------|
| Fixed-wing | Default for planes | `TASK_PLANE_MISSION` | `TASK_PLANE_LAND` (runway start/end) |
| Helicopter | `VehicleClass.Helicopters` | `TASK_HELI_MISSION` | `TASK_HELI_MISSION` + `LAND_ON_ARRIVAL` flag |
| VTOL (hover) | Nozzle position > 0.5 | `TASK_HELI_MISSION` | `TASK_HELI_MISSION` + `LAND_ON_ARRIVAL` flag |
| VTOL (plane) | Nozzle position ≤ 0.5 | `TASK_PLANE_MISSION` | `TASK_PLANE_LAND` |
| Blimp | Model hash in `BLIMP_VEHICLE_HASHES` | `TASK_PLANE_MISSION` | **No autoland** - circles and announces manual landing required |

**Blimp Special Handling:**

Blimps cannot autoland due to their flight characteristics:
- Use slower default speed (15 m/s vs 50 m/s for planes)
- Speed limits enforced (8-25 m/s range)
- When reaching destination, circles overhead
- Announces: "Arrived at [destination], circling. Blimps require manual landing"
- Player must take manual control for landing

**Flight Phase State Machine (Destination Mode):**

```
CRUISE (en route)
    │
    │ < 2 miles from destination
    ▼
APPROACH (aligning, speed reduced)
    │
    │ < 0.5 miles from destination
    ▼
FINAL (gear down for fixed-wing, slow approach)
    │
    ├─────────────────────┬─────────────────────┐
    ▼                     ▼                     ▼
TOUCHDOWN (Fixed-wing)  TOUCHDOWN (Helicopter)  CIRCLE (Blimp)
    │                     │                     │
    ▼                     ▼                     │
TAXIING                 LANDED                  └─→ Manual control
    │
    ▼
LANDED
```

**AutoFly Menu Options:**

```
AutoFly Menu:
├── Start Cruise Mode (maintain altitude/heading)
├── Fly to GPS Waypoint (requires active waypoint)
├── Increase Altitude (+500 feet)
├── Decrease Altitude (-500 feet)
├── Increase Speed (+10 mph)
├── Decrease Speed (-10 mph)
├── Pause/Resume AutoFly
├── Current Status (announces mode, phase, distance)
└── Stop AutoFly
```

**Key Constants (Constants.cs):**

```csharp
// AutoFly native function hashes
public const ulong NATIVE_TASK_PLANE_MISSION = 0x23703CD154E83B88;
public const ulong NATIVE_TASK_HELI_MISSION = 0xDAD029E187A2BEB4;
public const ulong NATIVE_TASK_PLANE_LAND = 0xBF19721FA34D32C0;
public const ulong NATIVE_CONTROL_LANDING_GEAR = 0xCFB0019F3B5B85A2;

// Flight modes
public const int FLIGHT_MODE_CRUISE = 1;
public const int FLIGHT_MODE_WAYPOINT = 2;
public const int FLIGHT_MODE_DESTINATION = 3;

// Flight phases
public const int PHASE_CRUISE = 1;
public const int PHASE_APPROACH = 2;
public const int PHASE_FINAL = 3;
public const int PHASE_TOUCHDOWN = 4;
public const int PHASE_LANDED = 6;

// Speed parameters (m/s)
public const float AUTOFLY_DEFAULT_SPEED = 50f;      // Fixed-wing
public const float AUTOFLY_HELI_DEFAULT_SPEED = 30f; // Helicopters
public const float AUTOFLY_BLIMP_DEFAULT_SPEED = 15f; // Blimps (~34 mph)
public const float AUTOFLY_BLIMP_MAX_SPEED = 25f;    // Blimp max (~56 mph)
public const float AUTOFLY_BLIMP_MIN_SPEED = 8f;     // Blimp min (~18 mph)

// Phase distances
public const float AUTOFLY_APPROACH_DISTANCE = 3200f; // 2 miles
public const float AUTOFLY_FINAL_DISTANCE = 800f;     // 0.5 miles
```

**LandingDestination Extension:**

The `LandingDestination` class was extended with runway endpoint calculation for `TASK_PLANE_LAND`:

```csharp
internal class LandingDestination
{
    public Vector3 Position { get; }           // Runway start/touchdown point
    public Vector3 RunwayEndPosition { get; }  // Calculated from position + heading + 800m
    public float RunwayHeading { get; }        // Runway direction (-1 for helipads)
    public bool IsHelipad { get; }

    // Constructor calculates runway end:
    // RunwayEndPosition = Position + (heading direction × 800 meters)
}
```

**Integration with AircraftLandingMenu:**

When selecting a destination from AircraftLandingMenu while in an aircraft:
1. If AutoFlyManager is available → Launches AutoFly with `StartDestination()`
2. If not in aircraft or no AutoFlyManager → Falls back to navigation-only mode (waypoint + voice guidance)

**Implementation Files:**
- `AutoFlyManager.cs` - Core autopilot logic (~800 lines)
- `Menus/AutoFlyMenu.cs` - Menu interface (~250 lines)
- `Menus/AircraftLandingMenu.cs` - Extended with AutoFly integration

### AutoDrive System (NEW - January 2026)

Comprehensive autonomous driving system with environmental awareness, safety features, and intelligent navigation:

**Core Features:**
- **Waypoint Navigation:** Drive to GPS waypoint with distance tracking and arrival detection
- **Wander Mode:** Random exploration with optional road type preference
- **Road Type Seeking:** Find and stay on specific road types (freeway, highway, street)
- **Multiple Driving Styles:** Normal, Cautious, Aggressive, Reckless (affects speed, behavior)
- **Pause/Resume:** Pause driving and resume from current position
- **Speed Control:** Adjustable target speed with environmental modifiers

**Driving Styles:**

| Style | Speed Modifier | Behavior |
|-------|----------------|----------|
| Normal | 1.0x | Standard traffic laws, stops for obstacles |
| Cautious | 0.7x | Slower, more careful, wider following distance |
| Aggressive | 1.3x | Faster, closer following, quicker lane changes |
| Reckless | 1.5x | Ignores traffic, no emergency vehicle yielding |

**Environmental Awareness:**

| Feature | Description | Speed Impact |
|---------|-------------|--------------|
| Weather Detection | Rain, thunder, snow, fog, clearing | 0.6x - 1.0x |
| Time-of-Day | Night driving (21:00-05:00) | 0.85x |
| Road Type | Freeway vs city street vs dirt road | Variable |
| Hill/Gradient | Uphill/downhill detection | Announced |
| Tunnel/Bridge | Structure entry/exit announcements | Announced |

**Weather Conditions Detected:**
- Clear, Clouds, Overcast (no speed change)
- Rain, Drizzle (0.8x speed)
- Thunder, Lightning (0.7x speed)
- Snow, Snowlight, Blizzard (0.6x speed)
- Fog, Foggy (0.7x speed)
- Clearing (0.9x speed)

**Safety Features:**

1. **Collision Warning System:**
   - Detects vehicles ahead in driving path
   - Distance-based warning (close/very close/imminent)
   - Audio beep with frequency based on distance
   - Automatic speed reduction when too close

2. **Following Distance:**
   - Maintains safe distance from vehicle ahead
   - Distance varies by driving style
   - Automatic speed matching when following

3. **Traffic Light Detection:**
   - Announces traffic lights ahead
   - Distance-based announcements

4. **Curve Detection & Slowdown:**
   - Detects sharp curves and gentle curves
   - Automatic speed reduction through curves
   - Direction announcement (left/right)

5. **Emergency Vehicle Awareness:**
   - Detects nearby emergency vehicles with sirens
   - Yields to emergency vehicles (except in Reckless mode)
   - Announces "Emergency vehicle nearby"

**Traffic Awareness:**

1. **Lane Change Announcements:**
   - Detects lateral movement across lanes
   - Announces "Changing lanes left/right"
   - Throttled to prevent spam

2. **Overtaking Announcements:**
   - Tracks vehicles being passed
   - Announces "Passed vehicle on left/right"
   - Uses pre-allocated tracking dictionary

3. **U-Turn Detection:**
   - Detects 180° heading changes
   - Announces "U-turn detected"

**Navigation Announcements:**

| Type | Trigger | Example |
|------|---------|---------|
| Distance Milestones | Every 0.5 miles (far), 0.1 miles (near) | "2.5 miles to destination" |
| Final Approach | < 1000 feet | "500 feet to destination" |
| Arrival | < 50 feet | "Arrived at destination" |
| Road Type Change | When road type changes | "Now on freeway" |
| ETA | Periodically | "Estimated arrival in 3 minutes" |

**Arrival Announcements (Granular):**
- 1000, 750, 500, 400, 300, 250, 200, 150, 100, 75, 50 feet milestones
- Final approach slowdown at 50m for precise arrival
- Safe arrival position finding (road node detection)

**Recovery System:**

When the vehicle gets stuck, AutoDrive automatically attempts recovery:

1. **Stuck Detection:**
   - Monitors vehicle movement and speed
   - Detects when progress stops (< 0.5m movement)
   - Progress timeout (no waypoint progress for 30 seconds)

2. **Recovery Stages:**
   - Stage 1: Reverse briefly
   - Stage 2: Turn while reversing
   - Stage 3: Forward with turn
   - Stage 4: Resume navigation

3. **Recovery Limits:**
   - Maximum 5 recovery attempts
   - Announces "Attempting recovery, attempt X"
   - Stops after max attempts with failure announcement

**Road Type Seeking:**

Allows finding and staying on specific road types:

| Road Type | Description |
|-----------|-------------|
| Freeway | Major highways (high speed) |
| Highway | Secondary highways |
| Main Road | Primary city streets |
| Street | Standard roads |
| Dirt Road | Unpaved roads |

**Seek Modes:**
- **Find:** Navigate to nearest road of type, then stay on it
- **Stay:** Already on road type, maintain it
- **Seeking:** Looking for road type, wandering until found

**Implementation Files:**
- `AutoDriveManager.cs` - Core logic (~3200 lines)
- `Menus/AutoDriveMenu.cs` - Menu interface

**Menu Options:**
```
AutoDrive Menu:
├── Drive to Waypoint (requires active waypoint)
├── Wander (random driving)
├── Seek Road Type → Freeway, Highway, Main Road, Street, Dirt Road
├── Change Driving Style → Normal, Cautious, Aggressive, Reckless
├── Pause/Resume
├── Increase Speed
├── Decrease Speed
├── Status (current state announcement)
└── Stop
```

**Navigation Flow:**
```
NumPad7/9      → Navigate to "AutoDrive" menu
NumPad1/3      → Navigate menu options
NumPad2        → Execute option
  - "Drive to Waypoint" → Starts navigation
  - "Wander" → Starts random driving
  - "Seek Road Type" → Opens submenu
  - "Stop" → Stops AutoDrive
Decimal        → Back (from submenus)
```

**Key Constants (Constants.cs):**

```csharp
// Core AutoDrive
public const float AUTODRIVE_BASE_SPEED = 15f;           // m/s (~34 mph)
public const float AUTODRIVE_WAYPOINT_ARRIVAL_RADIUS = 10f;
public const float AUTODRIVE_FINAL_APPROACH_DISTANCE = 50f;
public const float AUTODRIVE_PRECISE_ARRIVAL_RADIUS = 6f;

// Collision Warning
public const float COLLISION_WARNING_DISTANCE = 30f;     // meters
public const float COLLISION_CLOSE_DISTANCE = 15f;
public const float COLLISION_IMMINENT_DISTANCE = 8f;

// Following Distance
public const float FOLLOWING_DISTANCE_NORMAL = 15f;      // meters
public const float FOLLOWING_DISTANCE_CAUTIOUS = 25f;
public const float FOLLOWING_DISTANCE_AGGRESSIVE = 8f;

// Weather Speed Multipliers
public const float WEATHER_SPEED_RAIN = 0.8f;
public const float WEATHER_SPEED_THUNDER = 0.7f;
public const float WEATHER_SPEED_SNOW = 0.6f;
public const float WEATHER_SPEED_FOG = 0.7f;

// Recovery
public const int RECOVERY_MAX_ATTEMPTS = 5;
public const float RECOVERY_STUCK_THRESHOLD = 0.5f;      // meters
```

**Announcement Priority Queue:**

AutoDrive uses a priority system to prevent announcement spam:

| Priority | Type | Cooldown |
|----------|------|----------|
| Critical | Collision imminent, Arrival | 0s (immediate) |
| High | Emergency vehicle, Recovery | 2s |
| Medium | Curves, Traffic lights, Milestones | 3s |
| Low | Lane changes, Overtaking, ETA | 5s |

**Performance Optimizations:**

1. **Pre-allocated Collections:**
   ```csharp
   private readonly HashSet<int> _visibleHandles = new HashSet<int>();
   private readonly List<int> _handleRemovalList = new List<int>();
   private readonly Dictionary<int, OvertakeTrackingInfo> _overtakeTracking;
   ```

2. **Throttled Updates:**
   - Main update: 0.2s interval
   - Road type check: 1.0s interval
   - Road seeking: 3.0s interval
   - Weather check: 5.0s interval
   - ETA announcement: 30s interval

3. **Safe Waypoint Arrival:**
   ```csharp
   // Finds road-safe position near waypoint for mission recognition
   private Vector3 GetSafeArrivalPosition(Vector3 waypointPos)
   {
       // Try GET_CLOSEST_VEHICLE_NODE first
       // Fallback to GET_SAFE_COORD_FOR_PED
       // Returns closest drivable road position
   }
   ```

**Native Functions Used:**

| Native | Purpose |
|--------|---------|
| `TASK_VEHICLE_DRIVE_TO_COORD` | Drive to specific coordinates |
| `TASK_VEHICLE_DRIVE_WANDER` | Random wandering |
| `SET_DRIVE_TASK_CRUISE_SPEED` | Adjust driving speed |
| `GET_CLOSEST_VEHICLE_NODE` | Find nearest road |
| `GET_SAFE_COORD_FOR_PED` | Find safe arrival position |
| `GET_CURR_WEATHER_STATE` | Detect current weather |
| `IS_VEHICLE_SIREN_ON` | Detect emergency vehicles |

### Mark Waypoint to Mission Objective (NEW - January 2026)

New function in the Functions menu that finds and marks mission objectives:

**Usage:**
- Navigate to Functions menu → "Mark Waypoint to Mission Objective"
- Scans for mission-related blips (taxi destinations, objectives, pickups)
- Sets GPS waypoint to the nearest mission objective
- Announces distance in feet or miles

**Mission Blip Types Detected:**
- Standard destination markers (taxi, delivery)
- Mission objectives and pickups
- Helipad markers
- Yellow mission circles

**Implementation:**
```csharp
// FunctionsMenu.cs - Static cached array to avoid allocation
private static readonly int[] MissionBlipSprites = new int[]
{
    1, 2, 3, 38, 40, 90, 143, 225, 227, 280, 304, 309, 380, 417, 478, 480
};

// Iterates through blip types, finds closest, sets waypoint
Function.Call(Hash.SET_NEW_WAYPOINT, closestBlipPos.X, closestBlipPos.Y);
```

### Vehicle Class Announcement in Weaponized Category (NEW - January 2026)

When browsing the Weaponized vehicles category, the vehicle class is now announced after the vehicle name:

**Examples:**
- "Akula, Helicopter"
- "Deluxo, Sports"
- "Oppressor, Motorcycle"
- "Khanjali, Military"
- "Hydra, Plane"

**Implementation:**
- `VehicleSpawn` class extended with `vehicleClassName` field
- `VehicleSpawnMenu` looks up class using `GET_VEHICLE_CLASS_FROM_NAME` native
- `Constants.VEHICLE_CLASS_NAMES` array maps class indices to readable names
- Only applies to special categories (Weaponized) where vehicle types are mixed

### Vehicle Category Menu (NEW - January 2026)

Replaced flat vehicle spawn menu with hierarchical category-based menu:

**24 Vehicle Categories (23 VehicleClass + 1 Special):**

1. **Weaponized** (Special) - Armed vehicles with guns/missiles
2. Super Cars, Sports Cars, Sports Classics, Muscle Cars
3. Coupes, Sedans, Compacts, SUVs, Off-Road
4. Motorcycles, Cycles (Bicycles), Vans
5. Commercial, Industrial, Service, Utility
6. Emergency, Military, Planes, Helicopters
7. Boats, Open Wheel, Trains

**Weaponized Vehicles Category:**

Special category listing all vehicles with built-in weapons (90+ vehicles):

| Type | Examples |
|------|----------|
| **Aircraft** | Akula, Hydra, Lazer, Savage, Hunter, B11, Avenger, Buzzard, Valkyrie |
| **Cars** | Deluxo, Vigilante, Scramjet, Toreador, Stromberg, Nightshark, Ruiner 2000 |
| **Trucks/Military** | APC, Khanjali, Insurgent, Halftrack, Chernobog, Terrorbyte, Technical |
| **Motorcycles** | Oppressor, Oppressor Mk II, Deathbike variants |
| **Boats** | Weaponized Dinghy, Patrol Boat |

- Uses name-based filtering via `Constants.WEAPONIZED_VEHICLE_NAMES` HashSet
- Placed first in category list for easy access
- Includes vehicles from Gunrunning, Doomsday, and other DLC updates

**Navigation Flow:**
```
NumPad7/9 → Navigate to "Spawn Vehicle" menu
NumPad1/3 → Navigate between categories (Weaponized is first)
NumPad2   → Enter category (shows vehicles in that category)
  NumPad1/3 → Navigate vehicles within category
  NumPad2   → Spawn selected vehicle
  Decimal   → Back to category list
```

**Implementation:**
- `VehicleCategoryMenu.cs` - Category list with submenus, supports special categories
- `VehicleSpawnMenu.cs` - Supports both `VehicleClass` filter and `HashSet<string>` name filter
- `VehicleCategory` class has `IsSpecial` flag for non-VehicleClass categories
- Uses `Hash.GET_VEHICLE_CLASS_FROM_NAME` to classify standard vehicles

### Vehicle Modification Menu (NEW - January 2026)

New menu for modifying vehicles while in-game (accessible only when in a vehicle):

**Mod Categories Available:**
1. **Performance:** Engine, Transmission, Brakes, Suspension, Armor, Turbo
2. **Appearance:** Spoiler, Front/Rear Bumper, Side Skirt, Exhaust, Grille, Hood, Roof, Left Fender/Wing, Right Fender/Wing
3. **Wheels:** Front Wheels, Rear Wheels
4. **Wheel Type:** Change wheel style category (see below)
5. **Toggle Mods:** Turbo (on/off), Xenon Headlights (on/off)
6. **Neon Lights:** Left, Right, Front, Back, All On/Off
7. **Livery:** Vehicle liveries (if available)

**Fender/Wing Terminology:**

In GTA V, "fender" and "wing" refer to the **same body panel** - different terminology for the same car part:
- **Fender** = American English term
- **Wing** = British English term

There are only 2 mod slots for these panels:
| Index | Name | Description |
|-------|------|-------------|
| 8 | Left Fender/Wing | Left side body panel |
| 9 | Right Fender/Wing | Right side body panel |

The menu uses "Fender/Wing" to accommodate users familiar with either term.

**Wheel Type Selection:**

The Wheel Type category lets you change the style of wheels available for your vehicle. Each type has different wheel designs suited for that vehicle style.

| Index | Wheel Type | Description |
|-------|------------|-------------|
| 0 | Sport | Sports car wheels |
| 1 | Muscle | Muscle car wheels |
| 2 | Lowrider | Lowrider style |
| 3 | SUV | SUV/truck wheels |
| 4 | Offroad | Off-road wheels |
| 5 | Tuner | Tuner/import wheels |
| 6 | Bike Wheels | Motorcycle wheels |
| 7 | High End | High-end luxury |
| 8 | Bennys Originals | Benny's Originals |
| 9 | Bennys Bespoke | Benny's Bespoke |
| 10 | Open Wheel | Open wheel (F1 style) |
| 11 | Street | Street wheels |
| 12 | Track | Track/racing wheels |

- Available for all wheeled vehicles (excludes boats, helicopters, planes)
- No "stock" option - navigation wraps from Track back to Sport
- Wheel type names stored in `Constants.WHEEL_TYPE_NAMES`

**Navigation Flow:**
```
NumPad7/9 → Navigate to "Vehicle Mods" menu
  (Shows "Not in vehicle" if player is on foot)
NumPad1/3 → Navigate mod categories
NumPad2   → Enter category
  NumPad1/3 → Navigate mod options (Stock, Level 1, Level 2, etc.)
  NumPad2   → Apply mod instantly
  Decimal   → Back to category list
```

**Implementation:**
- `VehicleModMenu.cs` - Main mod menu with categories
- `VehicleModMenuProxy.cs` - Wrapper that handles "not in vehicle" state
- Special mod type constants: `MOD_TYPE_NEONS = -2`, `MOD_TYPE_WHEEL_TYPE = -3`
- Uses SHVDN 3.4.0 vehicle modification APIs:
  ```csharp
  vehicle.Mods.InstallModKit();  // Required first!
  vehicle.Mods[VehicleModType.Engine].Index = level;
  vehicle.Mods[VehicleToggleModType.Turbo].IsInstalled = true;
  vehicle.Mods.WheelType = VehicleWheelType.Sport;  // Set wheel type
  ```

### Vehicle Save/Load System (NEW - January 2026)

Save and restore custom vehicle configurations with 10 fixed slots:

**Features:**
- Save current vehicle with all mods, colors, neons, license plate
- Load saved vehicle (spawns with all saved modifications)
- Clear saved slots

**Saved Data (per slot):**
- Vehicle model hash
- All mod indices (Engine, Transmission, etc.)
- Primary/Secondary colors (preset and custom RGB)
- Pearlescent and rim colors
- Wheel type
- Window tint
- Neon lights (positions and color)
- License plate text and style
- Toggle mods (Turbo, Xenon, Tire Smoke)

**Storage Location:**
`Documents/Rockstar Games/GTA V/ModSettings/gta11ySavedVehicles.json`

**Navigation Flow:**
```
NumPad7/9 → Navigate to "Vehicle Save/Load" menu
NumPad1/3 → Navigate: "Save Current Vehicle", "Load Saved Vehicle", "Clear Slot"
NumPad2   → Enter submenu
  NumPad1/3 → Navigate slots 1-10
            (Shows "Slot 3: Zentorno, Engine Level 4, Turbo" or "Slot 3: Empty")
  NumPad2   → Execute (save/load/clear)
  Decimal   → Back
```

**Implementation:**
- `SavedVehicle.cs` - Data model for saved configurations
- `VehicleSaveManager.cs` - JSON persistence (10 slots)
- `VehicleSaveLoadMenu.cs` - Menu UI for save/load/clear operations

### ScriptHookVDotNet Upgrade (3.0.2 → 3.4.0)

Upgraded to SHVDN 3.4.0 for better vehicle modification APIs:

**New APIs Used:**
- `vehicle.Mods.InstallModKit()` - Required before applying mods
- `vehicle.Mods[VehicleModType.X].Index` - Get/set mod level
- `vehicle.Mods[VehicleToggleModType.X].IsInstalled` - Toggle mods
- `vehicle.Mods.PrimaryColor`, `SecondaryColor`, `CustomPrimaryColor`
- `vehicle.Mods.WheelType`, `WindowTint`
- `vehicle.Mods.SetNeonLightsOn()`, `NeonLightsColor`
- `vehicle.Mods.LicensePlate`, `LicensePlateStyle`

**Backward Compatibility:**
- All existing APIs remain compatible
- No breaking changes from 3.0.2

### Critical Performance Fixes (January 2026)

Two major performance issues were identified and fixed that caused hangs/crashes after extended use:

#### 1. VehicleModMenuProxy Vehicle Comparison Fix

**Problem:** The proxy compared vehicles using object reference (`_lastVehicle == currentVehicle`), but SHVDN returns a **new wrapper object** each time `CurrentVehicle` is accessed. This caused:
- A new `VehicleModMenu` created on **every menu interaction**
- Each creation called `InstallModKit()` + 15+ native calls to check mod counts
- Memory pressure and eventual hangs

**Fix (VehicleModMenuProxy.cs):**
```csharp
// BEFORE - Always fails, creates new menu every time:
if (_lastVehicle != null && _lastVehicle == currentVehicle && _modMenu != null)

// AFTER - Compare by Handle (integer), works correctly:
private int _lastVehicleHandle;
int currentHandle = currentVehicle.Handle;
if (_lastVehicleHandle == currentHandle && _modMenu != null)
```

**Additional optimization:** Removed `UpdateModMenu()` calls from property getters (`HasActiveSubmenu`, `GetMenuName`, `ExitSubmenu`) - these just query existing state and don't need vehicle checks.

#### 2. Aircraft Attitude Indicator Audio Leak Fix

**Problem:** The aircraft pitch/roll indicators were creating **new audio objects on every call** (20 times/second):

```csharp
// BEFORE - Called 20x/sec, each creating 2+ new objects that were never disposed:
public void PlayAircraftRollIndicator(float rollDegrees)
{
    var signalSample = _aircraftRollGenerator.Take(...);  // NEW OffsetSampleProvider
    var monoSignal = new StereoToMonoSampleProvider(...); // NEW object
    var panner = new PanningSampleProvider(...);          // NEW object
    _aircraftRollOut.Init(panner);  // Allocates internal buffers each time!
    _aircraftRollOut.Play();
}
```

At 20 calls/second × 40+ objects = **2,400+ leaked objects per minute**. After 10 minutes of flying: **24,000+ objects** never garbage collected, exhausting Windows audio resources and causing "Failed Initialization" crashes.

**Fix (AudioManager.cs):**
```csharp
// Pre-create sample providers ONCE in constructor:
var monoSignal = new StereoToMonoSampleProvider(_aircraftRollGenerator);
_aircraftRollPanner = new PanningSampleProvider(monoSignal);

// AFTER - Reuse pre-created providers, only update properties:
public void PlayAircraftRollIndicator(float rollDegrees)
{
    _aircraftRollOut.Stop();

    // Just update pan value - no new objects!
    float pan = Math.Max(-1f, Math.Min(1f, rollDegrees / 60f));
    _aircraftRollPanner.Pan = pan;

    // Only init ONCE on first use:
    if (!_aircraftRollInitialized)
    {
        _aircraftRollOut.Init(_aircraftRollPanner);
        _aircraftRollInitialized = true;
    }

    _aircraftRollOut.Play();
    _aircraftRollStopTick = DateTime.Now.Ticks + (long)(duration * 10_000_000);
}
```

**New method added:** `UpdateAircraftIndicators()` - called every frame from `OnTick` to handle timer-based audio stop (very lightweight, just checks timestamps).

**Impact:**
- **Before:** 40+ allocations per second (leaked)
- **After:** 0 allocations per second (reusing same objects)
- No more resource exhaustion or crashes during extended flight

#### 3. Weapon Hash ToString() Per-Tick Allocation Fix

**Problem:** Weapon change detection called `ToString()` on the weapon hash **every single tick**, creating a new string object 60 times per second:

```csharp
// BEFORE - Creates new string EVERY TICK (60+ allocations/second):
string weaponHash = player.Weapons.Current.Hash.ToString();
if (weaponHash != _currentWeaponHash)
```

**Fix (GTA11Y.cs):**
```csharp
// AFTER - Compare enum directly, only ToString() when weapon actually changes:
private WeaponHash _currentWeaponHash;  // Store enum, not string

WeaponHash weaponHash = player.Weapons.Current.Hash;
if (weaponHash != _currentWeaponHash)
{
    _currentWeaponHash = weaponHash;
    _audio.Speak(weaponHash.ToString());  // Only allocate on change
}
```

**Impact:** Eliminated 60 string allocations per second during normal gameplay.

#### 4. Altitude/Pitch Indicator Audio Leak Fix

**Problem:** Same issue as aircraft indicators - `PlayAltitudeIndicator()` and `PlayPitchIndicator()` called `Init()` with `.Take()` every time, creating new `OffsetSampleProvider` objects:

```csharp
// BEFORE - Creates new objects every call:
_altitudeOut.Init(_altitudeGenerator.Take(TimeSpan.FromSeconds(...)));
_altitudeOut.Play();
```

**Fix (AudioManager.cs):**
```csharp
// Pre-configure generator in constructor:
_altitudeGenerator = new SignalGenerator
{
    Gain = Constants.ALTITUDE_GAIN,
    Frequency = Constants.ALTITUDE_BASE_FREQUENCY,
    Type = SignalGeneratorType.Triangle
};

// AFTER - Init once, just update frequency:
public void PlayAltitudeIndicator(float heightAboveGround)
{
    _altitudeOut.Stop();
    _altitudeGenerator.Frequency = Constants.ALTITUDE_BASE_FREQUENCY +
        (heightAboveGround * Constants.ALTITUDE_FREQUENCY_MULTIPLIER);

    if (!_altitudeInitialized)
    {
        _altitudeOut.Init(_altitudeGenerator);
        _altitudeInitialized = true;
    }

    _altitudeOut.Play();
    _altitudeStopTick = DateTime.Now.Ticks + (long)(duration * 10_000_000);
}
```

Same fix applied to `PlayPitchIndicator()`. Timer-based stop handled by `UpdateAircraftIndicators()`.

#### 5. EntityScanner Vehicle Reference Comparison Fix

**Problem:** When scanning nearby vehicles, the player's current vehicle was filtered using object reference comparison:

```csharp
// BEFORE - May fail due to SHVDN wrapper objects:
if (vehicle == currentVehicle)
    continue;
```

**Fix (EntityScanner.cs):**
```csharp
// AFTER - Compare by Handle (integer):
if (currentVehicle != null && vehicle.Handle == currentVehicle.Handle)
    continue;
```

#### 6. FunctionsMenu Vehicle Reference Comparison Fix

**Problem:** The "Explode Nearby Vehicles" function compared the player's vehicle using object reference:

```csharp
// BEFORE:
if (Game.Player.Character.CurrentVehicle == v)
```

**Fix (FunctionsMenu.cs):**
```csharp
// AFTER - Compare by Handle:
Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;
if (playerVehicle != null && playerVehicle.Handle == v.Handle)
```

#### Performance Fix Summary

| Fix | Component | Issue | Allocations Eliminated |
|-----|-----------|-------|------------------------|
| 1 | VehicleModMenuProxy | Object reference comparison | Menu recreation per interaction |
| 2 | Aircraft Indicators | New objects per call (20/sec) | 40+/second |
| 3 | Weapon Detection | ToString() per tick | 60/second |
| 4 | Altitude/Pitch Indicators | Init()+Take() per call | 20+/second |
| 5 | EntityScanner | Vehicle reference comparison | Potential filter failures |
| 6 | FunctionsMenu | Vehicle reference comparison | Potential god mode bypass |
| 7 | AutoDriveManager | Non-existent native (upright check) | Crash on wander mode |

**Total Impact:**
- **Before:** 120+ allocations/second during active gameplay
- **After:** 0 allocations/second in steady state
- No more hangs or "Failed Initialization" crashes during extended play
- Correct vehicle filtering in all scan/comparison operations

#### 7. AutoDrive Vehicle Upright Detection Fix

**Problem:** The `CheckVehicleState()` function in AutoDriveManager used a non-existent native:
```csharp
// BEFORE - Native 0x1F9EBB9DD58F4BDA doesn't exist, causes crash:
float uprightValue = Function.Call<float>(
    (Hash)Constants.NATIVE_GET_ENTITY_UPRIGHT_VALUE, vehicle);
```

**Error:** `FATAL: Can't find native 0x1F9EBB9DD58F4BDA`

**Fix:** Use SHVDN's built-in `vehicle.UpVector.Z` property instead:
```csharp
// AFTER - Use vehicle's UpVector.Z (1.0 = upright, 0 = side, -1 = upside down):
float uprightValue = vehicle.UpVector.Z;
```

**Why This Works:**
- `vehicle.UpVector` returns the vehicle's local up direction in world space
- The Z component indicates how upright the vehicle is:
  - `1.0` = fully upright (roof pointing up)
  - `0.0` = on its side (roof pointing horizontally)
  - `-1.0` = upside down (roof pointing down)
- The existing threshold of `0.5` still works correctly

## January 2026 Refactoring Summary

### Architectural Refactoring

Major code reorganization to improve maintainability, testability, and performance through separation of concerns.

#### New Manager Classes Extracted from AutoDriveManager

**HashManager.cs (~210 lines)**
- Centralized entity hash loading (previously duplicated in EntityScanner and GTA11Y)
- Thread-safe singleton pattern with lazy initialization
- Uses int keys instead of string to avoid ToString() allocations
- Graceful failure handling with empty dictionary fallback
- `TryGetName(int hash, out string name)` - primary lookup method
- `ContainsHash(int hash)` - existence check

**WeatherManager.cs (~170 lines)**
- Weather detection and speed multiplier calculation
- Extracted from AutoDriveManager's weather-related code
- Road friction coefficient calculation for curve speed
- Weather change announcements with throttling
- Speed multipliers: Clear (1.0x), Rain (0.8x), Thunder (0.7x), Snow (0.6x), Blizzard (0.5x)

**CollisionDetector.cs (~230 lines)**
- Time-To-Collision (TTC) based collision warnings
- Realistic time-based following distance (2-3 second rule)
- Replaced fixed distance thresholds with speed-relative calculations
- Warning levels: None, Far, Medium, Close, Imminent
- Handle-based vehicle comparison for nearby vehicle scanning

**CurveAnalyzer.cs (~260 lines)**
- Physics-based safe speed calculation for curves
- Curve severity classification: None, Gentle, Moderate, Sharp, Hairpin
- Road lookahead scanning with pre-allocated OutputArguments
- Weather-aware friction coefficient integration
- Speed-dependent slowdown distance calculation

**CurveTypes.cs (~60 lines)**
- `CurveSeverity` enum (None, Gentle, Moderate, Sharp, Hairpin)
- `CurveDirection` enum (Left, Right)
- `CurveInfo` struct with Severity, Direction, Angle, Radius, SafeSpeed

**RecoveryManager.cs (~420 lines)**
- Stuck detection and multi-stage recovery system
- Vehicle state monitoring (flipped, in water, on fire, critical damage)
- Progress timeout detection (no waypoint progress for 30 seconds)
- Recovery strategies: ReverseTurn, ForwardTurn, ThreePointTurn
- Escalating recovery attempts with alternating turn directions

**AnnouncementQueue.cs (~140 lines)**
- Priority-based announcement throttling
- Prevents announcement spam during AutoDrive
- Priority levels: Critical (0.5s), High (2s), Medium (3s), Low (5s)
- Global cooldown to prevent rapid-fire announcements

#### LocationData.cs (~375 lines)

Centralized location data for teleport and waypoint menus:

**Structure:**
- `TeleportLocation` struct (Name, Coords, Category)
- `WaypointDestination` struct (Name, Coords)
- Static arrays organized by category
- `GetTeleportLocationsByCategory(int index)` accessor method

**Teleport Categories (76 locations):**
1. Character Houses (5)
2. Airports and Runways (11)
3. Sniping Vantage Points (16)
4. Military and Restricted (6)
5. Landmarks (8)
6. Blaine County (9)
7. Coastal and Beaches (7)
8. Remote Areas (7)
9. Emergency Services (7)

**Waypoint Destinations (85 locations):**
- Organized in sections: LS Customs, Freeways, Airports, Piers, Gas Stations, Character Houses, Landmarks, Military, Emergency, Blaine County, Coastal, Banks, Entertainment, Industrial, Extreme Areas, Neighborhoods, Boats

### Performance Optimizations (January 2026)

#### 1. Handle-Based Entity Comparison

**Problem:** SHVDN returns new wrapper objects each time `CurrentVehicle` or similar properties are accessed. Object reference comparison (`vehicle1 == vehicle2`) always fails.

**Solution:** Compare by Handle (int) instead:
```csharp
// BEFORE - Always fails:
if (_lastVehicle == currentVehicle)

// AFTER - Works correctly:
if (_lastVehicleHandle == currentVehicle.Handle)
```

**Files Fixed:**
- VehicleModMenuProxy.cs
- EntityScanner.cs
- FunctionsMenu.cs
- CollisionDetector.cs

#### 2. WeaponHash Enum Comparison

**Problem:** `weaponHash.ToString()` called every tick (60 allocations/second)

**Solution:** Store and compare enum values directly:
```csharp
// BEFORE:
private string _currentWeaponHash;
string weaponHash = player.Weapons.Current.Hash.ToString();

// AFTER:
private WeaponHash _currentWeaponHash;
WeaponHash weaponHash = player.Weapons.Current.Hash;
```

#### 3. Pre-Allocated OutputArguments

**Problem:** Native calls with OutputArgument created new objects each call

**Solution:** Pre-allocate and reuse:
```csharp
// In constructor:
private readonly OutputArgument _nodePos = new OutputArgument();
private readonly OutputArgument _nodeHeading = new OutputArgument();

// In method - reuse same objects:
Function.Call<bool>((Hash)NATIVE, x, y, z, _nodePos, _nodeHeading, ...);
```

**Files Using:** CurveAnalyzer.cs, AutoDriveManager.cs

#### 4. NAudio Provider Reuse

**Problem:** Audio indicators created new `WaveOutEvent.Init()` calls with new sample providers each tick

**Solution:** Pre-create providers once, only update properties:
```csharp
// Constructor - create once:
var monoSignal = new StereoToMonoSampleProvider(_aircraftRollGenerator);
_aircraftRollPanner = new PanningSampleProvider(monoSignal);

// Method - just update pan value:
_aircraftRollPanner.Pan = pan;  // No new objects!
```

### Defensive Coding Patterns

All manager classes implement comprehensive defensive patterns:

#### Entity Validation
```csharp
// Always check entity exists and is valid
if (vehicle == null || !vehicle.Exists())
    return false;

// Check state before accessing properties
if (vehicle.IsDead)
    return false;
```

#### Float Guard Patterns
```csharp
// Guard against NaN and Infinity
if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
    float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
    return false;
```

#### Tick Value Guards
```csharp
// Protect against invalid tick values
if (currentTick < 0)
    return false;
```

#### Try-Catch on Native Calls
```csharp
try
{
    int weatherHash = Function.Call<int>((Hash)Constants.NATIVE_GET_PREV_WEATHER_TYPE_HASH_NAME);
    // ...
}
catch (Exception ex)
{
    Logger.Exception(ex, "WeatherManager.Update");
}
```

#### Bounds Checking
```csharp
// Clamp values to valid ranges
if (priority < Constants.ANNOUNCE_PRIORITY_CRITICAL)
    priority = Constants.ANNOUNCE_PRIORITY_CRITICAL;
else if (priority > Constants.ANNOUNCE_PRIORITY_LOW)
    priority = Constants.ANNOUNCE_PRIORITY_LOW;
```

#### Graceful Degradation
```csharp
// Return defaults instead of crashing
public static Dictionary<int, string> Hashes
{
    get
    {
        EnsureInitialized();
        return _hashes ?? new Dictionary<int, string>();  // Never return null
    }
}
```

### File-by-File Defensive Changes

| File | Defensive Additions |
|------|---------------------|
| GTA11Y.cs | Game.IsLoading check, player existence validation, tick overflow protection |
| AudioManager.cs | Null audio provider checks, Tolk health monitoring, file existence checks |
| EntityScanner.cs | Handle-based vehicle comparison, pool bounds checking |
| SettingsManager.cs | JSON parse error handling, auto-repair corrupted settings |
| HashManager.cs | Thread-safe initialization, file existence check, parse error limits |
| Logger.cs | Queue overflow protection, error throttling, disposal safety |
| AutoDriveManager.cs | All entity validations, NaN guards, recovery state machine |
| AutoFlyManager.cs | Aircraft type validation, phase state guards, landing task safety |
| WeatherManager.cs | Native call wrapping, hash comparison overflow handling |
| CollisionDetector.cs | Vector normalization guards, division by zero protection |
| CurveAnalyzer.cs | Negative distance guard, minimum speed floor |
| RecoveryManager.cs | Entity validity on every tick, recovery state validation |
| AnnouncementQueue.cs | Null message check, audio manager validation |
| All menu files | Index bounds checking, null audio manager guards |

## January 2027 Improvements

### 1. Realistic Time-Based Following Distance (2-3 Second Rule)

**Optimization:** Implemented the "2-second rule" used by real drivers instead of fixed distance thresholds.

**Before:**
```csharp
// Fixed distance thresholds (not realistic)
if (_followingDistance >= Constants.FOLLOWING_COMFORTABLE) // 40m
    followingState = 1; // Comfortable
```

**After:**
```csharp
// Time-based following (realistic 2-3 second rule)
float followingTimeGap = _followingDistance / currentSpeed;
if (followingTimeGap >= 3.0f)
    followingState = 1; // Safe following (3+ seconds)
else if (followingTimeGap >= 2.0f)
    followingState = 2; // Normal following (2-3 seconds)
else if (followingTimeGap >= 1.5f)
    followingState = 3; // Close following (1.5-2 seconds)
```

**Impact:**
- Much more realistic driving behavior matching real-world driver expectations
- Accounts for speed variations (slow traffic vs highway)
- Prevents unsafe following distances at high speeds
- Reduces abrupt speed changes through better anticipation

### 2. Gradual Speed Control with Acceleration Curves

**Optimization:** Replaced instant speed changes with realistic acceleration/deceleration curves.

**Before:**
```csharp
// Instant speed change (jarring)
_targetSpeed = newSpeed;
UpdateFlightSpeed(); // Immediate jump
```

**After:**
```csharp
// Gradual speed change with physics-based curves
private void ApplySmoothSpeedTransition(Vehicle vehicle, float targetSpeed, float currentSpeed)
{
    float speedDiff = targetSpeed - currentSpeed;
    float maxChangeRate = speedDiff > 0 ? GetAccelerationRate() : GetDecelerationRate();
    float actualChange = Math.Max(-maxChangeRate, Math.Min(maxChangeRate, speedDiff));
    float newSpeed = currentSpeed + actualChange;

    // Apply with bounds checking
    newSpeed = Math.Max(minSpeed, Math.Min(maxSpeed, newSpeed));
    _targetSpeed = newSpeed;
    UpdateFlightSpeed();
}
```

**Impact:**
- Eliminates jarring speed transitions that feel unnatural
- Implements realistic acceleration curves (gentle for cautious driving, aggressive for reckless)
- Reduces wear and tear on virtual physics engine
- More predictable and human-like driving behavior

### 3. Enhanced Curve Detection and Physics-Based Slowdown

**Optimization:** Advanced curve analysis with severity classification and physics-based safe speeds.

**Before:**
```csharp
// Simple angle threshold
if (Math.Abs(headingDiff) > Constants.CURVE_HEADING_THRESHOLD)
    StartCurveSlowdown(0.8f); // Fixed 20% reduction
```

**After:**
```csharp
// Physics-based curve analysis
private CurveInfo AnalyzeCurveCharacteristics(float vehicleHeading, float roadHeading, float distance, float speed)
{
    float absAngle = Math.Abs(NormalizeAngleDiff(roadHeading - vehicleHeading));

    // Classify severity
    CurveSeverity severity = absAngle > 90f ? CurveSeverity.Hairpin :
                            absAngle > 45f ? CurveSeverity.Sharp :
                            absAngle > 25f ? CurveSeverity.Moderate :
                            absAngle > 10f ? CurveSeverity.Gentle : CurveSeverity.None;

    // Calculate safe speed using physics: v = sqrt(μ × g × r)
    float curveRadius = distance / (float)Math.Tan(absAngle * Math.PI / 180f / 2f);
    float frictionCoeff = GetRoadFrictionCoefficient(); // Weather-dependent
    float safeSpeed = (float)Math.Sqrt(frictionCoeff * 9.81f * curveRadius);

    return new CurveInfo(severity, direction, absAngle, curveRadius, safeSpeed);
}
```

**Impact:**
- Much more accurate curve speed recommendations
- Accounts for road conditions (wet, icy, etc.)
- Different handling for different curve severities
- Prevents unrealistic cornering speeds

**Status:** ✅ **All improvements implemented and compiling**

## External Dependencies

### Runtime Requirements (scripts/ folder)

**DLLs:**
- `ScriptHookV.dll` (native Script Hook V)
- `ScriptHookVDotNet3.dll` (v3.4.0.0 - upgraded January 2026)
- `NAudio.dll` (1.10.0)
- `TolkDotNet.dll` (screen reader library)
- `Newtonsoft.Json.dll` (12.0.3)
- `GrandTheftAccessibility.dll` (compiled mod)

**Data Files:**
- `hashes.txt` (1.5MB - entity name mappings)
- `tped.wav`, `tvehicle.wav`, `tprop.wav` (audio cues, in External Resources folder)

### NuGet Packages

```xml
<package id="Newtonsoft.Json" version="12.0.3" targetFramework="net48" />
<package id="NAudio" version="1.10.0" targetFramework="net48" />
```

### System Requirements

- **GTA V:** Story Mode only (SHVDN doesn't work in GTA:Online)
- **.NET Framework:** 4.8 runtime installed
- **Platform:** Windows x64
- **ScriptHookV:** Latest for current GTA V build
- **ScriptHookVDotNet3:** v3.4.0 or later (required for vehicle mod features)

## Development Guidelines

### C# Version Constraints

**Target:** C# 7.3 (per `.csproj`)

**Allowed:**
- ✅ Pattern matching in switch (`case X when Y:`)
- ✅ Tuples, deconstruction
- ✅ Ref returns and locals
- ✅ Expression-bodied members
- ✅ Throw expressions

**Not Allowed:**
- ❌ Switch expressions (`x switch { ... }`) - C# 8.0
- ❌ Nullable reference types - C# 8.0
- ❌ Using declarations - C# 8.0
- ❌ Static local functions - C# 8.0
- ❌ Records - C# 9.0
- ❌ Init-only properties - C# 9.0

### Code Organization

- Use `#region` sections: Fields, Constructors, Methods
- Namespace: `GrandTheftAccessibility`
- PascalCase for public members
- `_camelCase` for private fields
- Clear XML doc comments on public APIs

### Performance Best Practices

1. **Minimize Tick Work:** Only run essential code every frame
2. **Throttle Expensive Operations:** Use tick intervals
3. **Cache Frequently Used Values:** Avoid redundant lookups
4. **Pool Objects:** Reuse instead of allocate
5. **Use StringBuilder:** For string building in loops
6. **TryGetValue:** For dictionary lookups
7. **Avoid LINQ in Tick:** Use for loops instead (less allocations)
8. **Compare SHVDN entities by Handle:** `entity.Handle` (int), not object reference - SHVDN returns new wrapper objects each call
9. **Pre-create NAudio providers:** Call `WaveOutEvent.Init()` once, reuse by updating properties (Frequency, Pan, etc.)

### Debugging

**Test in GTA V Story Mode:**
1. Copy output DLL to `GTAV/scripts/`
2. Launch GTA V
3. Press **F2** to reload scripts (after changes) - configurable in `ScriptHookVDotNet.ini`
4. Check `GTAV/ScriptHookVDotNet.log` for errors

**ScriptHookVDotNet.ini Keys:**
- `ReloadKey=F2` - Reload all scripts
- `ConsoleKey=F4` - Open debug console

**Common Issues:**
- Scripts not loading: Check ScriptHookV compatibility with GTA V build
- Keyboard not responding: v3.0.2 fixes this (upgrade if needed)
- Performance issues: Check tick throttling intervals in Constants.cs

## Installation

### For Development

1. Clone repository
2. Open `GTA\GTA11Y.sln` in Visual Studio
3. Restore NuGet packages
4. Build (x64 platform)
5. Copy output DLL + dependencies to `GTAV/scripts/`

### For End Users

1. Install Script Hook V
2. Install ScriptHookVDotNet3 (v3.0.2+)
3. Copy mod DLL + dependencies to `GTAV/scripts/`
4. Copy `hashes.txt` and audio files to `GTAV/scripts/`
5. Launch GTA V Story Mode

## Additional Documentation

- **OPTIMIZATION_SUMMARY.md** - Before/after comparisons, performance metrics
- **SCRIPTHOOK_COMPATIBILITY.md** - Full API compatibility verification
- **MIGRATION_GUIDE.md** - Code examples, migration patterns
- **README.md** (in parent folder) - Original project description

## Sources & References

- [ScriptHookVDotNet v3.0.2 Release](https://github.com/scripthookvdotnet/scripthookvdotnet/releases/tag/v3.0.2)
- [ScriptHookVDotNet GitHub](https://github.com/scripthookvdotnet/scripthookvdotnet)
- [ScriptHookVDotNet Documentation](https://scripthookvdotnet.github.io/)
- [GTA.Audio API Source](https://github.com/scripthookvdotnet/scripthookvdotnet/blob/main/source/scripting_v2/GTA/Audio.cs)
- [Script Hook V .Net Enhanced](https://www.gta5-mods.com/tools/script-hook-v-net-enhanced)
- [GTA5 Modding Best Practices](https://forums.gta5-mods.com/topic/30271/getting-up-to-speed-with-scripting-for-gta-v)

---

**Last Updated:** January 2026
**Optimized By:** Claude Code with research-based GTA5 modding best practices
**Enhanced By:** Claude Code - Aircraft accessibility, aircraft landing navigation, AutoFly & AutoLand system, AutoDrive system (weather/collision/lane awareness), stability improvements, logging, vehicle category menu, vehicle mod menu, vehicle save/load system, GPS waypoint menu
**January 2026 Refactoring:** Major architectural refactoring - extracted WeatherManager, CollisionDetector, CurveAnalyzer, RecoveryManager, AnnouncementQueue, HashManager, LocationData; comprehensive defensive coding patterns
**January 2027 Improvements:** Realistic time-based following distance (2-3 second rule), gradual speed control with acceleration curves, enhanced curve detection with severity classification
**ScriptHookVDotNet Version:** 3.4.0.0 (upgraded January 2026)
