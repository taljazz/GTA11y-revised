# ScriptHookVDotNet v3.4.0 Research Summary

**Research Date:** January 2026
**Purpose:** Document SHVDN 3.4.0 APIs used in GTA11Y accessibility mod

---

## 1. New APIs in v3.4.0 (Compared to v3.0.2)

### v3.4.0 Release (January 17, 2024)

**Interior and Building Systems (NEW)**
- `InteriorProxy` class - Wrapper for interior native functions
- `AnimatedBuilding` class
- `Building` class
- `InteriorInstance` class
- `World` class methods for these types: count/capacity properties, handle retrieval, `World.GetClosest()` overloads

**Entity Enhancements**
- `Entity.FragmentGroupCount` - Number of fragment groups
- `Entity.IsFragmentObject` - Whether entity is a fragment object
- `Entity.DetachFragmentPart()` - Detach fragment parts
- `EntityBone.FragmentGroupIndex` - Fragment group index for bones
- `World.EntityColliderCount` - Global collider count
- `Entity.EntityColliderCapacity` - Entity's collider capacity

**Ped Perception Properties**
- `Ped.SeeingRange` - Visual detection range
- `Ped.HearingRange` - Audio detection range
- `Ped.VisualFieldMinAngle` / `Ped.VisualFieldMaxAngle`
- `Ped.VisualFieldMinElevationAngle` / `Ped.VisualFieldMaxElevationAngle`
- `Ped.VisualFieldPeripheralRange`
- `Ped.VisualFieldCenterAngle`

**Death Record Tracking**
- `Ped.CauseOfDeath` - Weapon/method that killed the ped
- `Ped.TimeOfDeath` - When the ped died
- `Ped.ClearKillerRecord()` - Clear death records

**Projectile System**
- `Projectile` class
- `Projectile.FromHandle()` - Get projectile from handle
- `Projectile.OwnerEntity` - Entity that fired the projectile

**Utility Additions**
- `Game.FindPattern()` - Memory pattern searching
- `Quaternion.LookRotation()` - Rotation calculations

**Enum Updates**
- New entries in `PedHash`, `VehicleHash`, `WeaponHash`, `WeaponComponentHash`, `RadioStation`, `BlipSprite`, `ExplosionType`

**Bug Fixes**
- Fixed `Euphoria` helper class `Stop()` and `Start()` methods
- Fixed `Vehicle.PassengerCount` for game v1.0.2545.0+
- Fixed `WeaponCollection.Give()` weapon selection behavior

---

## 2. Vehicle Modification APIs - Complete Documentation

### VehicleModCollection Class

**Access:** `vehicle.Mods`

#### Core Methods

| Method | Description |
|--------|-------------|
| `InstallModKit()` | **REQUIRED** before applying any mods. Calls `SET_VEHICLE_MOD_KIT` native. |
| `Contains(VehicleModType type)` | Check if mod type is available for this vehicle |
| `ToArray()` | Get array of all installed modifications |
| `RequestAdditionTextFile(int timeout = 1000)` | Request localization text for mod names |
| `GetLocalizedWheelTypeName(VehicleWheelType type)` | Get display name for wheel type |

#### Indexers

```csharp
// Standard mods (multiple levels)
VehicleMod mod = vehicle.Mods[VehicleModType.Engine];

// Toggle mods (on/off)
VehicleToggleMod toggleMod = vehicle.Mods[VehicleToggleModType.Turbo];
```

#### Color Properties

| Property | Type | Description |
|----------|------|-------------|
| `PrimaryColor` | `VehicleColor` | Preset primary color |
| `SecondaryColor` | `VehicleColor` | Preset secondary color |
| `RimColor` | `VehicleColor` | Wheel rim color |
| `PearlescentColor` | `VehicleColor` | Pearlescent effect color |
| `TrimColor` | `VehicleColor` | Interior trim (v1.0.505.2+) |
| `DashboardColor` | `VehicleColor` | Dashboard color (v1.0.505.2+) |
| `CustomPrimaryColor` | `Color` | RGB custom primary |
| `CustomSecondaryColor` | `Color` | RGB custom secondary |
| `IsPrimaryColorCustom` | `bool` | Is custom RGB set |
| `IsSecondaryColorCustom` | `bool` | Is custom RGB set |
| `ColorCombination` | `int` | Get/set color combo index |
| `ColorCombinationCount` | `int` | Available combinations |

**Color Management Methods:**
- `ClearCustomPrimaryColor()` - Remove custom primary
- `ClearCustomSecondaryColor()` - Remove custom secondary

#### Wheel Properties

| Property | Type | Description |
|----------|------|-------------|
| `WheelType` | `VehicleWheelType` | Current wheel style category |
| `AllowedWheelTypes` | `VehicleWheelType[]` | Compatible wheel types |
| `LocalizedWheelTypeName` | `string` | Display name of current type |

#### Visual Effects

| Property | Type | Description |
|----------|------|-------------|
| `TireSmokeColor` | `Color` | Tire smoke RGB color |
| `NeonLightsColor` | `Color` | Underglow neon RGB |
| `WindowTint` | `VehicleWindowTint` | Window tint level |

**Neon Methods:**
- `HasNeonLight(VehicleNeonLight light)` - Check if position available
- `HasNeonLights` - Any neon positions available
- `IsNeonLightsOn(VehicleNeonLight light)` - Is specific position on
- `SetNeonLightsOn(VehicleNeonLight light, bool on)` - Toggle position

#### Livery Properties

| Property | Type | Description |
|----------|------|-------------|
| `Livery` | `int` | Current livery index |
| `LiveryCount` | `int` | Available liveries |
| `LocalizedLiveryName` | `string` | Display name |

#### License Plate Properties

| Property | Type | Description |
|----------|------|-------------|
| `LicensePlate` | `string` | Plate text (up to 8 chars) |
| `LicensePlateType` | `LicensePlateType` | Plate type category |
| `LicensePlateStyle` | `LicensePlateStyle` | Visual style |

---

### VehicleMod Class

**Access:** `vehicle.Mods[VehicleModType.Engine]`

| Property/Method | Type | Description |
|-----------------|------|-------------|
| `Vehicle` | `Vehicle` | Owner vehicle |
| `Type` | `VehicleModType` | Mod slot type |
| `Count` | `int` | Available options (0 = not supported) |
| `Index` | `int` | Current mod level (-1 = stock) |
| `Variation` | `bool` | Variation flag for current mod |
| `LocalizedName` | `string` | Display name of installed mod |
| `LocalizedTypeName` | `string` | Display name of mod category |
| `Remove()` | void | Uninstall mod (return to stock) |

---

### VehicleModType Enum (50 values)

| Value | Name | Description |
|-------|------|-------------|
| 0 | `Spoiler` | Rear spoiler |
| 1 | `FrontBumper` | Front bumper |
| 2 | `RearBumper` | Rear bumper |
| 3 | `SideSkirt` | Side skirts |
| 4 | `Exhaust` | Exhaust system |
| 5 | `Frame` | Roll cage/chassis |
| 6 | `Grille` | Front grille |
| 7 | `Hood` | Hood/bonnet |
| 8 | `LeftFender` | Left fender/wing |
| 9 | `RightFender` | Right fender/wing |
| 10 | `Roof` | Roof |
| 11 | `Engine` | Engine upgrade |
| 12 | `Brakes` | Brake upgrade |
| 13 | `Transmission` | Transmission upgrade |
| 14 | `Horns` | Horn sound |
| 15 | `Suspension` | Suspension lowering |
| 16 | `Armor` | Armor protection |
| 17 | `Nitrous` | (Unused in vanilla) |
| 18 | `Turbo` | Turbo tuning |
| 19 | `Subwoofer` | (Unused in vanilla) |
| 20 | `SmokeColor` | Tire smoke color |
| 21 | `Hydraulics` | Hydraulics |
| 22 | `XenonHeadlights` | Xenon lights |
| 23 | `FrontWheel` | Front wheels |
| 24 | `BackWheel` | Rear wheels (motorcycles) |
| 25-49 | Various | Interior mods, Benny's upgrades, etc. |

**Interior/Benny's Mods (25-49):**
- `PlateHolder`, `VanityPlates`, `TrimDesign`, `Ornaments`
- `Dashboard`, `DialDesign`, `DoorSpeaker`, `Seats`
- `SteeringWheels`, `ColumnShifterLevers`, `Plaques`
- `Speakers`, `Trunk`, `Hydraulics`, `EngineBlock`
- `AirFilter`, `Struts`, `ArchCover`, `Aerials`
- `Trim`, `Tank`, `Windows`, `Livery`

---

### VehicleToggleModType Enum

| Value | Name | Description |
|-------|------|-------------|
| 18 | `Turbo` | Turbo on/off |
| 20 | `TireSmoke` | Tire smoke enabled |
| 22 | `XenonHeadlights` | Xenon lights on/off |

**Usage:**
```csharp
// Check if installed
bool hasTurbo = vehicle.Mods[VehicleToggleModType.Turbo].IsInstalled;

// Install/uninstall
vehicle.Mods[VehicleToggleModType.Turbo].IsInstalled = true;
```

---

### VehicleWheelType Enum (13 values)

| Value | Name | Localization Key |
|-------|------|------------------|
| 0 | `Sport` | CMOD_WHE1_0 |
| 1 | `Muscle` | CMOD_WHE1_1 |
| 2 | `Lowrider` | CMOD_WHE1_2 |
| 3 | `SUV` | CMOD_WHE1_3 |
| 4 | `Offroad` | CMOD_WHE1_4 |
| 5 | `Tuner` | CMOD_WHE1_5 |
| 6 | `BikeWheels` | CMOD_WHE1_6 |
| 7 | `HighEnd` | CMOD_WHE1_7 |
| 8 | `BennysOriginals` | CMOD_WHE1_8 |
| 9 | `BennysBespoke` | CMOD_WHE1_9 |
| 10 | `OpenWheel` | CMOD_WHE1_10 |
| 11 | `Street` | CMOD_WHE1_11 |
| 12 | `Track` | CMOD_WHE1_12 |

---

### VehicleNeonLight Enum

| Value | Name |
|-------|------|
| 0 | `Left` |
| 1 | `Right` |
| 2 | `Front` |
| 3 | `Back` |

---

### VehicleWindowTint Enum

| Value | Name |
|-------|------|
| 0 | `None` |
| 1 | `PureBlack` |
| 2 | `DarkSmoke` |
| 3 | `LightSmoke` |
| 4 | `Stock` |
| 5 | `Limo` |
| 6 | `Green` |
| -1 | `Invalid` (v3.0.4+) |

---

### LicensePlateStyle Enum

| Value | Name |
|-------|------|
| 0 | `BlueOnWhite2` |
| 1 | `YellowOnBlack` |
| 2 | `YellowOnBlue` |
| 3 | `BlueOnWhite1` |
| 4 | `BlueOnWhite3` |
| 5 | `NorthYankton` |

---

## 3. How to Use InstallModKit()

**Critical:** You MUST call `InstallModKit()` before applying ANY modifications. Without this, all mod changes will silently fail.

```csharp
// Correct usage
Vehicle vehicle = World.CreateVehicle(VehicleHash.Zentorno, position);
vehicle.Mods.InstallModKit();  // REQUIRED!

// Now you can apply mods
vehicle.Mods[VehicleModType.Engine].Index = 3;      // EMS Level 4
vehicle.Mods[VehicleModType.Brakes].Index = 2;      // Race Brakes
vehicle.Mods[VehicleModType.Transmission].Index = 2; // Race Transmission
vehicle.Mods[VehicleToggleModType.Turbo].IsInstalled = true;

// Set wheel type before wheel mod
vehicle.Mods.WheelType = VehicleWheelType.HighEnd;
vehicle.Mods[VehicleModType.FrontWheel].Index = 5;

// Colors
vehicle.Mods.PrimaryColor = VehicleColor.MetallicRed;
vehicle.Mods.CustomSecondaryColor = Color.FromArgb(255, 0, 128);

// Neons
vehicle.Mods.NeonLightsColor = Color.Blue;
vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, true);
vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, true);
vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, true);
vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, true);
```

---

## 4. Breaking Changes (v3.0.2 to v3.4.0)

**Policy:** SHVDN makes breaking changes only in new major versions, except for performance improvements that may break few scripts.

**No breaking changes between v3.0.2 and v3.4.0** - all changes are additive:
- New classes, properties, and methods added
- Enum values added (not removed)
- Bug fixes that correct incorrect behavior

**Compatibility Notes:**
- Scripts compiled against v3.0.2 should work with v3.4.0
- v3.6.0 has compatibility issues with game v1.0.3258.0+ (use nightly.89+ instead)

---

## 5. Best Practices

### Performance Tips

1. **Use Latest API Version**
   - Name files like `script.3.cs` to use v3 API
   - v3 has better performance and more features

2. **Efficient Native Calls**
   - Use provided `Native.Function.Call` overloads (0-16 parameters)
   - These are optimized for performance

3. **Vehicle Property Access**
   - Use `Vehicle.IsHelicopter`, `Vehicle.IsPlane`, etc. directly
   - Faster than `Vehicle.Model.IsHelicopter`

4. **Tick Event Efficiency**
   - Minimize work in Tick event
   - Use throttling for expensive operations
   - Cache frequently accessed values

5. **Entity Pooling**
   - `World.GetNearbyVehicles/Peds/Props()` creates new arrays
   - Cache results when possible

### Common Pitfalls

1. **Forgetting InstallModKit()**
   ```csharp
   // WRONG - mods won't apply
   vehicle.Mods[VehicleModType.Engine].Index = 3;

   // CORRECT
   vehicle.Mods.InstallModKit();
   vehicle.Mods[VehicleModType.Engine].Index = 3;
   ```

2. **Entity Pool Full**
   - `World.CreateVehicle()` returns `null` when pool is full
   - Always check for null returns

3. **Wrong Project Type**
   - Use "Class Library (.NET Framework)"
   - NOT "Class Library" (that's for .NET 5+/Core)

4. **Version Mismatch**
   - ASI file and DLL files must be from same version
   - Don't mix versions

5. **Vehicle Handle Comparison**
   - SHVDN returns new wrapper objects each call
   - Compare by `vehicle.Handle` (int), not object reference

6. **Using Script.Wait() Excessively**
   - Can cause game hangs
   - Consider using tick-based timing instead

---

## 6. Native Function Wrappers

### v3.4.0 Native Wrappers

Most new APIs in v3.4.0 wrap existing natives:

| API | Native Function |
|-----|-----------------|
| `Entity.FragmentGroupCount` | `GET_ENTITY_FRAGMENT_GROUP_COUNT` |
| `Entity.IsFragmentObject` | Memory access |
| `Entity.DetachFragmentPart()` | `DETACH_FRAGMENT_PART_FROM_ENTITY` |
| `Ped.SeeingRange` | `SET_PED_SEEING_RANGE` / Memory |
| `Ped.HearingRange` | `SET_PED_HEARING_RANGE` / Memory |
| `Ped.CauseOfDeath` | `GET_PED_CAUSE_OF_DEATH` |
| `Ped.TimeOfDeath` | `GET_PED_TIME_OF_DEATH` |
| `Game.FindPattern()` | Memory pattern search |

### Vehicle Mod Natives (Already Present)

| SHVDN API | Native Function |
|-----------|-----------------|
| `InstallModKit()` | `SET_VEHICLE_MOD_KIT(veh, 0)` |
| `Mods[type].Index` getter | `GET_VEHICLE_MOD(veh, type)` |
| `Mods[type].Index` setter | `SET_VEHICLE_MOD(veh, type, index, variation)` |
| `Mods[type].Count` | `GET_NUM_VEHICLE_MODS(veh, type)` |
| `Mods[type].Remove()` | `REMOVE_VEHICLE_MOD(veh, type)` |
| `WheelType` getter | `GET_VEHICLE_WHEEL_TYPE(veh)` |
| `WheelType` setter | `SET_VEHICLE_WHEEL_TYPE(veh, type)` |
| `SetNeonLightsOn()` | `SET_VEHICLE_NEON_ENABLED(veh, index, toggle)` |
| `IsNeonLightsOn()` | `IS_VEHICLE_NEON_ENABLED(veh, index)` |
| `NeonLightsColor` | `SET/GET_VEHICLE_NEON_COLOUR` |

---

## 7. Navigation and Pathfinding APIs

### Overview

GTA V has a comprehensive pathfinding system built on the PATHFIND namespace of native functions. These APIs enable vehicle navigation, pedestrian pathfinding, and GPS waypoint management. The system uses a network of vehicle nodes (road waypoints) and pedestrian navmesh polygons.

---

### Vehicle Node Functions

#### GET_CLOSEST_VEHICLE_NODE

**Hash:** `0x240A18690AE96513`

```c
BOOL GET_CLOSEST_VEHICLE_NODE(float x, float y, float z, Vector3* outPosition, int nodeFlags, float zMeasureMult, float zTolerance);
```

| Parameter | Type | Description |
|-----------|------|-------------|
| x, y, z | float | Search origin coordinates |
| outPosition | Vector3* | Output: Found node coordinates |
| nodeFlags | int | Filters which nodes to find (see enum below) |
| zMeasureMult | float | Weight for Z-axis difference in distance calculation |
| zTolerance | float | Z distance before `zMeasureMult` applies |

**Node Flags (eGetClosestNodeFlags):**

| Flag | Value | Description |
|------|-------|-------------|
| GCNF_INCLUDE_SWITCHED_OFF_NODES | 1 | Include disabled nodes |
| GCNF_INCLUDE_BOAT_NODES | 2 | Include water/boat nodes |
| GCNF_IGNORE_SLIPLANES | 4 | Ignore slip lanes |
| GCNF_IGNORE_SWITCHED_OFF_DEADENDS | 8 | Ignore disabled dead-ends |
| GCNF_GET_HEADING | 256 | Also return heading (use WITH_HEADING variant) |
| GCNF_FAVOUR_FACING | 512 | Favor nodes facing search direction |

**C# Usage:**
```csharp
OutputArgument outPos = new OutputArgument();
bool found = Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE,
    x, y, z, outPos, 1, 3.0f, 0f);
if (found)
{
    Vector3 nodePos = outPos.GetResult<Vector3>();
}
```

---

#### GET_CLOSEST_VEHICLE_NODE_WITH_HEADING

**Hash:** `0xFF071FB798B803B0`

```c
BOOL GET_CLOSEST_VEHICLE_NODE_WITH_HEADING(float x, float y, float z, Vector3* outPosition, float* outHeading, int nodeFlags, float zMeasureMult, float zTolerance);
```

Same as `GET_CLOSEST_VEHICLE_NODE` but also returns the road heading at that node.

**C# Usage:**
```csharp
OutputArgument outPos = new OutputArgument();
OutputArgument outHeading = new OutputArgument();
bool found = Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
    x, y, z, outPos, outHeading, 1, 3.0f, 0f);
if (found)
{
    Vector3 nodePos = outPos.GetResult<Vector3>();
    float heading = outHeading.GetResult<float>();
}
```

---

#### GET_NTH_CLOSEST_VEHICLE_NODE

**Hash:** `0xE50E52416CCF948B`

```c
BOOL GET_NTH_CLOSEST_VEHICLE_NODE(float x, float y, float z, int nthClosest, Vector3* outPosition, int nodeFlags, float zMeasureMult, float zTolerance);
```

Returns the Nth closest vehicle node instead of just the closest. Useful for finding alternative routes or backup positions.

---

#### GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING

**Hash:** `0x80CA6A8B6C094CC4`

```c
BOOL GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING(float x, float y, float z, int nthClosest, Vector3* outPosition, float* outHeading, int* totalLanes, int nodeFlags, float zMeasureMult, float zTolerance);
```

Extended version that also returns total lane count at the node.

---

#### GET_NTH_CLOSEST_VEHICLE_NODE_FAVOUR_DIRECTION

**Hash:** `0x45905BE8654AE067`

```c
BOOL GET_NTH_CLOSEST_VEHICLE_NODE_FAVOUR_DIRECTION(float x, float y, float z, float desiredX, float desiredY, float desiredZ, int nthClosest, Vector3* outPosition, float* outHeading, int nodeFlags, float zMeasureMult, float zTolerance);
```

Finds nodes that favor a specific direction of travel. Useful for finding road nodes in the direction the player wants to go.

---

#### GET_CLOSEST_MAJOR_VEHICLE_NODE

**Hash:** `0x2EABE3B06F58C1BE`

```c
BOOL GET_CLOSEST_MAJOR_VEHICLE_NODE(float x, float y, float z, Vector3* outPosition, float zMeasureMult, int zTolerance);
```

Finds the closest major vehicle node (main roads only). Equivalent to `GET_CLOSEST_VEHICLE_NODE` with `GCNF_INCLUDE_SWITCHED_OFF_NODES` flag.

---

#### GET_VEHICLE_NODE_PROPERTIES

**Hash:** `0x0568566ACBB5DEDC`

```c
BOOL GET_VEHICLE_NODE_PROPERTIES(float x, float y, float z, int* density, int* flags);
```

Gets traffic density (0-15) and property flags for the closest node.

**Vehicle Node Properties Enum (eVehicleNodeProperties):**

| Flag | Value | Description |
|------|-------|-------------|
| OFF_ROAD | 1 << 0 (1) | Node is off-road (dirt roads, alleys, parking lots) |
| ON_PLAYERS_ROAD | 1 << 1 (2) | Node is on the player's current road |
| NO_BIG_VEHICLES | 1 << 2 (4) | Large vehicles cannot use this node |
| SWITCHED_OFF | 1 << 3 (8) | Node is disabled |
| TUNNEL_OR_INTERIOR | 1 << 4 (16) | Node is in a tunnel or interior |
| LEADS_TO_DEAD_END | 1 << 5 (32) | Node leads to a dead end |
| HIGHWAY | 1 << 6 (64) | Node is on a highway/freeway |
| JUNCTION | 1 << 7 (128) | Node is at a junction/intersection |
| TRAFFIC_LIGHT | 1 << 8 (256) | Node has traffic lights |
| GIVE_WAY | 1 << 9 (512) | Node requires giving way |
| WATER | 1 << 10 (1024) | Node is a water/boat node |

**C# Usage for Road Type Detection:**
```csharp
OutputArgument outDensity = new OutputArgument();
OutputArgument outFlags = new OutputArgument();
bool found = Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES,
    x, y, z, outDensity, outFlags);

if (found)
{
    int density = outDensity.GetResult<int>();  // 0-15, higher = busier
    int flags = outFlags.GetResult<int>();

    bool isHighway = (flags & 64) != 0;   // HIGHWAY flag
    bool isOffRoad = (flags & 1) != 0;    // OFF_ROAD flag
    bool hasTrafficLight = (flags & 256) != 0;
    bool isJunction = (flags & 128) != 0;
}
```

---

#### GET_VEHICLE_NODE_IS_SWITCHED_OFF

**Hash:** `0x4F5070AA58F69279`

```c
BOOL GET_VEHICLE_NODE_IS_SWITCHED_OFF(int nodeID);
```

Returns true when the node is an "off-road" type (alleys, dirt roads, parking lots). Also known as `_GET_IS_SLOW_ROAD_FLAG`.

---

#### GET_VEHICLE_NODE_POSITION

**Hash:** `0x703123E5E7D429C2`

```c
void GET_VEHICLE_NODE_POSITION(int nodeId, Vector3* outPosition);
```

Gets the position of a vehicle node by its ID.

**WARNING:** Calling this with an invalid node ID will crash the game!

---

### Road Detection Functions

#### GET_CLOSEST_ROAD

**Hash:** `0x132F52BBA570FE92`

```c
BOOL GET_CLOSEST_ROAD(float x, float y, float z, float minimumEdgeLength, int minimumLaneCount, Vector3* srcNode, Vector3* targetNode, int* laneCountForward, int* laneCountBackward, float* width, BOOL onlyMajorRoads);
```

Finds an edge (road segment connecting two nodes) that satisfies the specified criteria.

| Parameter | Type | Description |
|-----------|------|-------------|
| x, y, z | float | Search origin |
| minimumEdgeLength | float | Minimum road segment length |
| minimumLaneCount | int | Minimum total lanes |
| srcNode | Vector3* | Output: Start node of road segment |
| targetNode | Vector3* | Output: End node of road segment |
| laneCountForward | int* | Output: Lanes in forward direction |
| laneCountBackward | int* | Output: Lanes in backward direction |
| width | float* | Output: Gap width between directions |
| onlyMajorRoads | BOOL | Only search major roads |

**Use Case:** Determine road direction, lane count, and road width for lane-keeping or lane-changing logic.

---

#### GET_ROAD_BOUNDARY_USING_HEADING

```c
void GET_ROAD_BOUNDARY_USING_HEADING(float x, float y, float z, float heading, Vector3* outPosition);
```

Gets the road boundary position in a specified heading direction. Useful for finding road edges.

---

#### GET_POINT_ON_ROAD_SIDE

**Hash:** `0x16F46FB18C8009E4` (also known as `_GET_POINT_ON_ROAD_SIDE`)

```c
void _GET_POINT_ON_ROAD_SIDE(float x, float y, float z, int p3, Vector3* outPosition);
```

Finds a point on the side of the road near the specified coordinates.

---

### Safe Pedestrian Coordinates

#### GET_SAFE_COORD_FOR_PED

**Hash:** `0xB61C8E878A4199CA`

```c
BOOL GET_SAFE_COORD_FOR_PED(float x, float y, float z, BOOL onlyOnPavement, Vector3* outPosition, int flags);
```

Finds a safe navigation position for pedestrians on the navmesh near the specified coordinates.

| Parameter | Type | Description |
|-----------|------|-------------|
| x, y, z | float | Search origin |
| onlyOnPavement | BOOL | Shorthand for GSC_FLAG_ONLY_PAVEMENT |
| outPosition | Vector3* | Output: Safe position |
| flags | int | Search behavior flags (see below) |

**Safe Position Flags (eSafePositionFlags):**

| Flag | Value | Description |
|------|-------|-------------|
| GSC_FLAG_ONLY_PAVEMENT | 1 | Only navmesh polygons marked as pavement |
| GSC_FLAG_NOT_ISOLATED | 2 | Exclude isolated navmesh regions |
| GSC_FLAG_NOT_INTERIOR | 4 | Exclude interior-generated polygons |
| GSC_FLAG_NOT_WATER | 8 | Exclude water-marked polygons |
| GSC_FLAG_ONLY_NETWORK_SPAWN | 16 | Only network spawn candidate positions |
| GSC_FLAG_USE_FLOOD_FILL | 32 | Use flood-fill search from start position |

**C# Usage:**
```csharp
OutputArgument outPos = new OutputArgument();
bool found = Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED,
    x, y, z,
    true,    // onlyOnPavement
    outPos,
    0);      // no additional flags

if (found)
{
    Vector3 safePos = outPos.GetResult<Vector3>();
}
```

---

### Ground Detection

#### GET_GROUND_Z_FOR_3D_COORD

**Hash:** `0xC906A7DAB05C8D2B`

```c
BOOL GET_GROUND_Z_FOR_3D_COORD(float x, float y, float z, float* groundZ, BOOL includeWater);
```

Gets the ground elevation at specified coordinates.

| Parameter | Type | Description |
|-----------|------|-------------|
| x, y, z | float | Position to check (z should be above ground) |
| groundZ | float* | Output: Ground Z coordinate |
| includeWater | BOOL | Consider water surface as ground |

**Important Limitations:**
- Only works within render distance of the client
- Returns false if coordinates are too far away or not loaded
- If position is below ground, outputs zero

**C# Usage:**
```csharp
OutputArgument groundZ = new OutputArgument();
bool found = Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD,
    x, y, 1000f,  // Start from high up
    groundZ,
    false);       // Don't include water

if (found)
{
    float ground = groundZ.GetResult<float>();
}
```

---

#### GET_GROUND_Z_AND_NORMAL_FOR_3D_COORD

**Hash:** `0x8BDC7BFC57A81E76`

```c
BOOL GET_GROUND_Z_AND_NORMAL_FOR_3D_COORD(float x, float y, float z, float* groundZ, Vector3* normal);
```

Same as above but also returns the surface normal vector, useful for determining slope angle and direction.

**C# Usage:**
```csharp
OutputArgument groundZ = new OutputArgument();
OutputArgument normal = new OutputArgument();
bool found = Function.Call<bool>(Hash.GET_GROUND_Z_AND_NORMAL_FOR_3D_COORD,
    x, y, 1000f, groundZ, normal);

if (found)
{
    float ground = groundZ.GetResult<float>();
    Vector3 surfaceNormal = normal.GetResult<Vector3>();

    // Calculate slope angle
    float slopeAngle = (float)Math.Acos(surfaceNormal.Z) * 180f / (float)Math.PI;
}
```

---

#### GET_GROUND_Z_EXCLUDING_OBJECTS_FOR_3D_COORD

**Hash:** `0x9E82F0F362881B29`

```c
BOOL GET_GROUND_Z_EXCLUDING_OBJECTS_FOR_3D_COORD(float x, float y, float z, float* groundZ, BOOL includeWater);
```

Same as `GET_GROUND_Z_FOR_3D_COORD` but ignores objects (props) at the position. Returns terrain/building ground only.

---

### GPS and Waypoint Functions

#### SET_NEW_WAYPOINT

**Hash:** `0xFE43368D2AA4F2FC`

```c
void SET_NEW_WAYPOINT(float x, float y);
```

Sets a new GPS waypoint on the map at the specified X, Y coordinates.

**C# Usage:**
```csharp
Function.Call(Hash.SET_NEW_WAYPOINT, x, y);
// Play confirmation sound
GTA.Audio.PlaySoundFrontend("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");
```

---

#### IS_WAYPOINT_ACTIVE

**Hash:** `0x202B1BBFC6AB5EE4`

```c
BOOL IS_WAYPOINT_ACTIVE();
```

Returns true if a waypoint is currently set on the map.

**C# Usage:**
```csharp
bool hasWaypoint = Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE);
```

---

#### GET_FIRST_BLIP_INFO_ID

**Hash:** `0x1BEDE233E6CD2A1F`

```c
int GET_FIRST_BLIP_INFO_ID(int blipSprite);
```

Gets the handle of the first blip with the specified sprite type. For waypoints, use sprite type 8.

**Common Blip Sprites:**
| Value | Description |
|-------|-------------|
| 8 | Waypoint (player-set marker) |
| 1 | Destination/objective |
| 66 | Friend |

---

#### GET_BLIP_COORDS

**Hash:** `0x586AFE3FF72D996E`

```c
Vector3 GET_BLIP_COORDS(int blip);
```

Gets the world coordinates of a blip.

**Note:** The Z coordinate may be 0.0f if the game cannot determine ground height at that location.

---

#### DOES_BLIP_EXIST

**Hash:** `0xA6DB27D19ECBB7DA`

```c
BOOL DOES_BLIP_EXIST(int blip);
```

Checks if a blip handle is valid.

---

#### Complete Waypoint Position Retrieval (C#):

```csharp
// Method 1: Using natives directly
bool hasWaypoint = Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE);
if (hasWaypoint)
{
    int waypointBlip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, 8);
    if (Function.Call<bool>(Hash.DOES_BLIP_EXIST, waypointBlip))
    {
        Vector3 waypointPos = Function.Call<Vector3>(Hash.GET_BLIP_COORDS, waypointBlip);

        // Get ground Z if needed (Z may be 0)
        if (waypointPos.Z == 0f)
        {
            OutputArgument groundZ = new OutputArgument();
            Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD,
                waypointPos.X, waypointPos.Y, 1000f, groundZ, false);
            waypointPos.Z = groundZ.GetResult<float>();
        }
    }
}

// Method 2: Using SHVDN's World.WaypointPosition
Vector3 waypointPos = World.WaypointPosition;
if (waypointPos != Vector3.Zero)
{
    // Waypoint is set and position is available
}
```

---

### GPS Route Functions

#### GENERATE_DIRECTIONS_TO_COORD

**Hash:** `0xF90125F1F79ECDF8`

```c
int GENERATE_DIRECTIONS_TO_COORD(float x, float y, float z, BOOL p3, int* direction, float* vehicle, float* distToNxJunction);
```

Generates GPS directions to a coordinate and returns turn direction information.

| Parameter | Type | Description |
|-----------|------|-------------|
| x, y, z | float | Destination coordinates |
| p3 | BOOL | Unknown (usually false) |
| direction | int* | Output: Turn direction (1=left, 2=right, 4=straight) |
| vehicle | float* | Unknown output |
| distToNxJunction | float* | Output: Distance to next junction |

---

#### CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS

**Hash:** `0xADD95C7005C4A197`

```c
float CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS(float x1, float y1, float z1, float x2, float y2, float z2);
```

Calculates the driving distance between two points along the road network.

**Important Notes:**
- Returns 100000.0 for very long distances or when path nodes aren't loaded
- Returns distance in game units (1 unit = 1 meter)
- May not match GPS display if GPS is set to kilometers

**C# Usage:**
```csharp
float travelDistance = Function.Call<float>(Hash.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS,
    startX, startY, startZ, endX, endY, endZ);

if (travelDistance < 100000f)
{
    // Valid distance in meters
    float miles = travelDistance * 0.000621371f;
}
```

---

#### GET_DISTANCE_BETWEEN_COORDS

**Hash:** `0xF1B760881820C952`

```c
float GET_DISTANCE_BETWEEN_COORDS(float x1, float y1, float z1, float x2, float y2, float z2, BOOL useZ);
```

Calculates straight-line distance between two points. If `useZ` is false, only calculates 2D (XY plane) distance.

---

### GPS Multi-Route Functions

#### START_GPS_MULTI_ROUTE

**Hash:** `0x3D3D15AF7BCAAF83`

```c
void START_GPS_MULTI_ROUTE(int routeColor, BOOL routeAlpha, BOOL displayOnFoot);
```

Starts a new GPS multi-route (custom GPS path with multiple waypoints).

---

#### ADD_POINT_TO_GPS_MULTI_ROUTE

**Hash:** `0xA905192A6781C41B`

```c
void ADD_POINT_TO_GPS_MULTI_ROUTE(float x, float y, float z);
```

Adds a point to the current GPS multi-route.

---

#### SET_GPS_MULTI_ROUTE_RENDER

**Hash:** `0x3DDA37128DD1ACA8`

```c
void SET_GPS_MULTI_ROUTE_RENDER(BOOL toggle);
```

Enables or disables rendering of the GPS multi-route.

---

#### CLEAR_GPS_MULTI_ROUTE

**Hash:** `0x67EEDEA1B9BAFD94`

```c
void CLEAR_GPS_MULTI_ROUTE();
```

Clears the current GPS multi-route.

---

### Node Type Reference

The `nodeType` parameter in various GET_*_VEHICLE_NODE functions follows a pattern:

| Value | Type | Description |
|-------|------|-------------|
| 0, 4, 8, 12... | Asphalt Only | Only paved/asphalt roads |
| 1, 5, 9, 13... | Any Road | All road types including paths |
| 2, 6, 10, 14... | Under Map | (Invalid positions) |
| 3, 7, 11, 15... | Water | Boat/water nodes |

**Common Values:**
- `0` = Paved roads only (best for cars)
- `1` = Any road/path (includes dirt roads)
- `3` = Water (for boats)

---

### SHVDN World Class Helpers

ScriptHookVDotNet provides convenience properties in the `World` class:

| Property | Type | Description |
|----------|------|-------------|
| `World.WaypointPosition` | `Vector3` | Current waypoint position (or Vector3.Zero if none) |
| `World.WaypointBlip` | `Blip` | Current waypoint blip (or null if none) |

**Usage:**
```csharp
// Get waypoint
Vector3 waypointPos = World.WaypointPosition;
if (waypointPos != Vector3.Zero)
{
    // Waypoint is set
}

// Set waypoint
World.WaypointPosition = new Vector3(x, y, 0);
```

---

### Practical Examples

#### Finding a Safe Arrival Position

```csharp
/// <summary>
/// Finds a road-safe position near the target for vehicle arrival
/// </summary>
private Vector3 GetSafeArrivalPosition(Vector3 targetPos)
{
    // Try GET_CLOSEST_VEHICLE_NODE first
    OutputArgument outPos = new OutputArgument();
    if (Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE,
        targetPos.X, targetPos.Y, targetPos.Z,
        outPos, 1, 3.0f, 0f))
    {
        return outPos.GetResult<Vector3>();
    }

    // Fallback to GET_SAFE_COORD_FOR_PED
    if (Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED,
        targetPos.X, targetPos.Y, targetPos.Z,
        true, outPos, 0))
    {
        return outPos.GetResult<Vector3>();
    }

    // Last resort: return original position
    return targetPos;
}
```

#### Detecting Highway vs City Road

```csharp
/// <summary>
/// Determines if the vehicle is on a highway
/// </summary>
private bool IsOnHighway(Vector3 position)
{
    OutputArgument density = new OutputArgument();
    OutputArgument flags = new OutputArgument();

    if (Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES,
        position.X, position.Y, position.Z,
        density, flags))
    {
        int nodeFlags = flags.GetResult<int>();
        return (nodeFlags & 64) != 0;  // HIGHWAY flag
    }
    return false;
}
```

#### Getting Distance to Waypoint via Roads

```csharp
/// <summary>
/// Gets driving distance to waypoint in miles
/// </summary>
private float GetDrivingDistanceToWaypoint(Vector3 playerPos)
{
    Vector3 waypointPos = World.WaypointPosition;
    if (waypointPos == Vector3.Zero)
        return -1f;

    float meters = Function.Call<float>(Hash.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS,
        playerPos.X, playerPos.Y, playerPos.Z,
        waypointPos.X, waypointPos.Y, waypointPos.Z);

    if (meters >= 100000f)
        return -1f;  // Path not found or too far

    return meters * 0.000621371f;  // Convert to miles
}
```

---

### Sources

- [FiveM Native Reference - PATHFIND](https://docs.fivem.net/natives/)
- [citizenfx/natives - GetSafeCoordForPed.md](https://github.com/citizenfx/natives/blob/master/PATHFIND/GetSafeCoordForPed.md)
- [citizenfx/natives - GetGroundZFor_3dCoord.md](https://github.com/citizenfx/natives/blob/master/MISC/GetGroundZFor_3dCoord.md)
- [FiveM - GET_CLOSEST_VEHICLE_NODE](https://docs.fivem.net/natives/?_0x240A18690AE96513)
- [FiveM - GET_VEHICLE_NODE_PROPERTIES](https://docs.fivem.net/natives/?_0x0568566ACBB5DEDC)
- [FiveM - CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS](https://docs.fivem.net/natives/?_0xADD95C7005C4A197)
- [GTAForums - Pathfind Node Types](https://gtaforums.com/topic/843561-pathfind-node-types/)
- [GTAForums - GET_CLOSEST_VEHICLE_NODE_WITH_HEADING](https://gtaforums.com/topic/828863-help-with-get_closest_vehicle_node_with_heading)
- [ScriptHookVDotNet - World.cs](https://github.com/scripthookvdotnet/scripthookvdotnet/blob/main/source/scripting_v3/GTA/World.cs)
- [RAGE Multiplayer Wiki - getVehicleNodeProperties](https://wiki.rage.mp/wiki/Pathfind::getVehicleNodeProperties)

---

## Sources

- [ScriptHookVDotNet GitHub Releases](https://github.com/scripthookvdotnet/scripthookvdotnet/releases)
- [ScriptHookVDotNet v3.4.0 Release](https://github.com/scripthookvdotnet/scripthookvdotnet/releases/tag/v3.4.0)
- [ScriptHookVDotNet Wiki - How Tos](https://github.com/scripthookvdotnet/scripthookvdotnet/wiki/How-Tos)
- [VehicleModCollection.cs Source](https://github.com/scripthookvdotnet/scripthookvdotnet/blob/main/source/scripting_v3/GTA/Entities/Vehicles/VehicleModCollection.cs)
- [VehicleMod.cs Source](https://github.com/scripthookvdotnet/scripthookvdotnet/blob/main/source/scripting_v3/GTA/Entities/Vehicles/VehicleMod.cs)
- [ScriptHookVDotNet Documentation](https://scripthookvdotnet.github.io/)
- [ScriptHookVDotNet FAQ](https://scripthookvdotnet.github.io/faq/)
- [RageCoop VehicleModType Documentation](https://docs.ragecoop.com/SHVDN/GTA.VehicleModType.html)
- [FiveM Natives - SET_VEHICLE_MOD](https://docs.fivem.net/natives/?_0x6AF0636DDEDCB6DD=)
- [FiveM Natives - GET_VEHICLE_WHEEL_TYPE](https://docs.fivem.net/natives/?_0xB3ED1BFB4BE636DC=)

---

## 8. World State and Vehicle APIs

**Research Date:** January 2026
**Purpose:** Document weather, time, vehicle state, and entity relationship natives for environmental awareness in GTA11Y.

---

### Weather System Natives

#### GET_CURR_WEATHER_STATE / _GET_WEATHER_TYPE_TRANSITION

Gets the current weather transition state between two weather types.

```c
void _GET_WEATHER_TYPE_TRANSITION(Hash* weatherType1, Hash* weatherType2, float* percentWeather2);
```

**Parameters (out):**
- `weatherType1` - Hash of the previous/current weather type
- `weatherType2` - Hash of the next/transitioning weather type
- `percentWeather2` - Transition progress (0.0 = fully weatherType1, 1.0 = fully weatherType2)

**Native Hash:** `0xF3BBE884A14BB413`

---

#### GET_PREV_WEATHER_TYPE_HASH_NAME

Returns the previous weather type hash.

```c
Hash GET_PREV_WEATHER_TYPE_HASH_NAME();
```

**Native Hash:** `0x564B884A05EC45A3` (alternate: `0xA8171E9E`)
**Legacy Name:** `_GET_PREV_WEATHER_TYPE`

---

#### GET_NEXT_WEATHER_TYPE_HASH_NAME

Returns the upcoming/next weather type hash.

```c
Hash GET_NEXT_WEATHER_TYPE_HASH_NAME();
```

**Native Hash:** `0x711327CD09C8F162`

---

#### SET_WEATHER_TYPE_NOW

Immediately changes the weather to the specified type.

```c
void SET_WEATHER_TYPE_NOW(char* weatherType);
```

**Note:** Not supported in networked sessions. Use `SET_OVERRIDE_WEATHER` or `SET_WEATHER_TYPE_NOW_PERSIST` instead.

---

#### Weather Type Strings

| String | Description |
|--------|-------------|
| `CLEAR` | Clear sky |
| `EXTRASUNNY` | Very sunny |
| `CLOUDS` | Cloudy |
| `OVERCAST` | Overcast/gray |
| `RAIN` | Raining |
| `CLEARING` | Clearing after rain |
| `THUNDER` | Thunderstorm |
| `SMOG` | Smoggy |
| `FOGGY` | Foggy |
| `XMAS` | Christmas snow |
| `SNOW` | Snow |
| `SNOWLIGHT` | Light snow |
| `BLIZZARD` | Blizzard |
| `HALLOWEEN` | Halloween special |
| `NEUTRAL` | Neutral weather |

---

#### Weather Type Enum (Numeric Values)

| Value | Name | Description |
|-------|------|-------------|
| 0 | `ExtraSunny` | Very sunny |
| 1 | `Clear` | Clear |
| 2 | `Clouds` | Cloudy |
| 3 | `Smog` | Smoggy |
| 4 | `Foggy` | Fog |
| 5 | `Overcast` | Overcast |
| 6 | `Rain` | Rain |
| 7 | `Thunder` | Thunder/lightning |
| 8 | `LightRain` | Light rain |
| 9 | `SmoggyLightRain` | Smog with rain |
| 10 | `VeryLightSnow` | Very light snow |
| 11 | `WindyLightSnow` | Windy light snow |
| 12 | `LightSnow` | Light snow |
| 13 | `Christmas` | Christmas weather |
| 14 | `Halloween` | Halloween weather |

**Converting to Hash:** Use `Game.GenerateHash("WEATHER_NAME")` or the joaat (Jenkins One At A Time) hash function.

---

### Time of Day Natives

#### GET_CLOCK_HOURS

Gets the current in-game hour (0-23).

```c
int GET_CLOCK_HOURS();
```

**Returns:** Hour value 0-23

---

#### GET_CLOCK_MINUTES

Gets the current in-game minute (0-59).

```c
int GET_CLOCK_MINUTES();
```

**Returns:** Minute value 0-59

---

#### GET_CLOCK_SECONDS

Gets the current in-game second (0-59).

```c
int GET_CLOCK_SECONDS();
```

**Returns:** Second value 0-59

---

#### Time System Information

- GTA V time runs at 30x real-time by default
- 2 real seconds = 1 GTA V minute
- Full day cycle takes approximately 48 real-time minutes
- Time range: 00:00:00 to 23:59:59

---

#### NETWORK_OVERRIDE_CLOCK_TIME

Overrides the game clock time for the local player.

```c
void NETWORK_OVERRIDE_CLOCK_TIME(int hours, int minutes, int seconds);
```

---

### Vehicle State Natives

#### GET_VEHICLE_FLIGHT_NOZZLE_POSITION

Gets the VTOL nozzle position for aircraft like Hydra, Tula, Avenger.

```c
float GET_VEHICLE_FLIGHT_NOZZLE_POSITION(Vehicle vehicle);
```

**Returns:**
- `0.0` = Nozzles pointing backward (jet flight mode)
- `1.0` = Nozzles pointing downward (VTOL/hover mode)
- Values in between indicate partial transition

**VTOL-Capable Aircraft:**
- Hydra
- Tula
- Avenger
- Raiju

**Usage for VTOL Detection:**
```csharp
float nozzlePos = Function.Call<float>(Hash.GET_VEHICLE_FLIGHT_NOZZLE_POSITION, vehicle);
bool isHoverMode = nozzlePos > 0.5f;  // In hover/VTOL mode
bool isPlaneMode = nozzlePos <= 0.5f; // In standard jet flight mode
```

---

#### IS_VEHICLE_ON_ALL_WHEELS

Checks if all wheels of a vehicle are touching the ground.

```c
BOOL IS_VEHICLE_ON_ALL_WHEELS(Vehicle vehicle);
```

**Returns:** `true` if all wheels are grounded, `false` if any wheel is airborne.

**Use Cases:**
- Detecting if vehicle landed after a jump
- Checking if vehicle is airborne
- Validating vehicle state for physics effects

**SHVDN Property:** `vehicle.IsOnAllWheels`

---

#### SET_VEHICLE_ON_GROUND_PROPERLY

Places the vehicle on the ground with all wheels touching.

```c
BOOL SET_VEHICLE_ON_GROUND_PROPERLY(Vehicle vehicle, float p1);
```

**Parameters:**
- `vehicle` - Vehicle to place
- `p1` - Unknown, typically use 5.0f

---

#### GET_VEHICLE_CLASS

Returns the vehicle class category.

```c
int GET_VEHICLE_CLASS(Vehicle vehicle);
```

**Native Hash:** `0x29439776AAA00A62` (alternate: `0xC025338E`)

**Vehicle Class IDs:**

| ID | Class | Description |
|----|-------|-------------|
| 0 | Compacts | Compact cars |
| 1 | Sedans | Sedan cars |
| 2 | SUVs | Sport utility vehicles |
| 3 | Coupes | Coupe cars |
| 4 | Muscle | Muscle cars |
| 5 | Sports Classics | Classic sports cars |
| 6 | Sports | Sports cars |
| 7 | Super | Super cars |
| 8 | Motorcycles | Motorcycles |
| 9 | Off-road | Off-road vehicles |
| 10 | Industrial | Industrial vehicles |
| 11 | Utility | Utility vehicles |
| 12 | Vans | Vans |
| 13 | Cycles | Bicycles |
| 14 | Boats | Boats |
| 15 | Helicopters | Helicopters |
| 16 | Planes | Airplanes |
| 17 | Service | Service vehicles |
| 18 | Emergency | Emergency vehicles |
| 19 | Military | Military vehicles |
| 20 | Commercial | Commercial vehicles |
| 21 | Trains | Trains |
| 22 | Open Wheel | Open wheel/F1 cars |

**Getting Localized Class Name:**
```csharp
int vehicleClass = Function.Call<int>(Hash.GET_VEHICLE_CLASS, vehicle);
string className = Game.GetGXTEntry($"VEH_CLASS_{vehicleClass}");
```

---

#### GET_VEHICLE_CLASS_FROM_NAME

Gets vehicle class from model hash (without spawning).

```c
int GET_VEHICLE_CLASS_FROM_NAME(Hash modelHash);
```

**Native Hash:** `0x461FC3EB507C7CB5`

---

### Entity Relationship Natives

#### GET_ENTITY_SPEED

Gets the speed of an entity in meters per second.

```c
float GET_ENTITY_SPEED(Entity entity);
```

**Returns:** Speed in m/s

**Conversions:**
- To km/h: multiply by 3.6
- To mph: multiply by 2.236936

**SHVDN Property:** `entity.Speed`

---

#### GET_ENTITY_HEADING

Gets the heading/yaw of an entity in degrees.

```c
float GET_ENTITY_HEADING(Entity entity);
```

**Returns:** Heading in degrees (0-360)
- 0 degrees = North
- 90 degrees = West (GTA V uses mirrored coordinate system)
- 180 degrees = South
- 270 degrees = East

**SHVDN Property:** `entity.Heading`

---

#### GET_ENTITY_VELOCITY

Gets the velocity vector of an entity.

```c
Vector3 GET_ENTITY_VELOCITY(Entity entity);
```

**Returns:** `Vector3` with X, Y, Z velocity components in m/s

**SHVDN Property:** `entity.Velocity`

---

#### GET_ENTITY_SPEED_VECTOR

Gets the speed vector, optionally relative to entity orientation.

```c
Vector3 GET_ENTITY_SPEED_VECTOR(Entity entity, BOOL relative);
```

**Parameters:**
- `entity` - Entity to query
- `relative` - If `true`, returns speed relative to entity's facing direction

**Relative Vector Interpretation:**
- `+Y` = Moving forward
- `-Y` = Moving backward (reverse)
- `+X` = Moving right
- `-X` = Moving left

**Use Case:** Detecting if vehicle is reversing:
```csharp
Vector3 speedVec = Function.Call<Vector3>(Hash.GET_ENTITY_SPEED_VECTOR, vehicle, true);
bool isReversing = speedVec.Y < -1.0f;  // Moving backward faster than 1 m/s
```

---

### Ped/Vehicle Relationship Natives

#### IS_PED_IN_ANY_VEHICLE

Checks if a ped is in any vehicle.

```c
BOOL IS_PED_IN_ANY_VEHICLE(Ped ped, BOOL atGetIn);
```

**Parameters:**
- `ped` - The ped to check
- `atGetIn` - Timing flag:
  - `false` = Returns true only when ped is seated and door closing
  - `true` = Returns true as soon as ped starts entering (after door opens)

**SHVDN:** `ped.IsInVehicle()`

---

#### IS_PED_IN_VEHICLE

Checks if a ped is in a specific vehicle.

```c
BOOL IS_PED_IN_VEHICLE(Ped ped, Vehicle vehicle, BOOL atGetIn);
```

**Parameters:**
- `ped` - The ped to check
- `vehicle` - The specific vehicle to check
- `atGetIn` - Same timing flag as above

**Example (Submersible):**
- `atGetIn = false`: Returns true only after ped descends into seat
- `atGetIn = true`: Returns true as soon as hatch opens

---

### Driving Task Natives

#### TASK_VEHICLE_DRIVE_TO_COORD

Commands a ped to drive to specific coordinates.

```c
void TASK_VEHICLE_DRIVE_TO_COORD(
    Ped ped, Vehicle vehicle,
    float x, float y, float z,
    float speed, int p6, Hash vehicleModel,
    int drivingStyle, float stopRange, float p10
);
```

**Parameters:**
- `ped` - The driver ped
- `vehicle` - The vehicle to drive
- `x, y, z` - Destination coordinates
- `speed` - Target speed in m/s
- `p6` - Unknown (float, possibly radius)
- `vehicleModel` - Hash of vehicle model (for pathfinding)
- `drivingStyle` - Combined driving style flags (see below)
- `stopRange` - Distance from destination to stop
- `p10` - Unknown (works as bool true)

---

#### SET_DRIVE_TASK_CRUISE_SPEED

Sets/updates the driving speed for an active driving task.

```c
void SET_DRIVE_TASK_CRUISE_SPEED(Ped ped, float speed);
```

**Parameters:**
- `ped` - The driver ped
- `speed` - New target speed in m/s

---

#### SET_DRIVE_TASK_DRIVING_STYLE

Sets/updates the driving style for an active driving task.

```c
void SET_DRIVE_TASK_DRIVING_STYLE(Ped ped, int drivingStyle);
```

**Native Hash:** `0xDACE1BE37D88AF67`

---

#### SET_DRIVER_ABILITY

Sets the overall driving ability of a ped.

```c
void SET_DRIVER_ABILITY(Ped ped, float ability);
```

**Parameters:**
- `ped` - The driver ped
- `ability` - Value 0.0 to 1.0 (0 = poor, 1 = perfect)

---

#### SET_DRIVER_AGGRESSIVENESS

Sets how aggressively a ped drives.

```c
void SET_DRIVER_AGGRESSIVENESS(Ped ped, float aggressiveness);
```

**Parameters:**
- `ped` - The driver ped
- `aggressiveness` - Value 0.0 to 1.0 (0 = calm, 1 = aggressive)

---

### Driving Style Flags

Driving styles are 32-bit flags combined into a single integer. Each flag enables/disables specific AI driving behaviors.

#### Individual Flag Values

| Decimal | Hex | Binary | Description |
|---------|-----|--------|-------------|
| 1 | 0x1 | 00000001 | Stop before vehicles |
| 2 | 0x2 | 00000010 | Stop before pedestrians |
| 4 | 0x4 | 00000100 | Avoid vehicles |
| 8 | 0x8 | 00001000 | Avoid empty vehicles |
| 16 | 0x10 | 00010000 | Avoid pedestrians |
| 32 | 0x20 | 00100000 | Avoid objects |
| 64 | 0x40 | 01000000 | Unknown |
| 128 | 0x80 | 10000000 | Stop at traffic lights |
| 256 | 0x100 | 100000000 | Use blinkers |
| 512 | 0x200 | 1000000000 | Allow wrong-way driving |
| 1024 | 0x400 | 10000000000 | Reverse gear |
| 2048 | 0x800 | 100000000000 | Unknown (possibly left overtake) |
| 4096 | 0x1000 | 1000000000000 | Unknown (possibly right overtake) |
| 262144 | 0x40000 | 1000000000000000000 | Take shortest path (uses dirt roads) |
| 4194304 | 0x400000 | 10000000000000000000000 | Ignore roads (local pathing ~200m) |
| 16777216 | 0x1000000 | 1000000000000000000000000 | Ignore all pathing (straight line) |
| 536870912 | 0x20000000 | 100000000000000000000000000000 | Avoid highways when possible |

---

#### Predefined Driving Styles

| Value | Name | Description |
|-------|------|-------------|
| 5 | Sometimes Overtake | Stop before vehicles + Avoid vehicles |
| 6 | Avoid Traffic Extremely | Stop before peds + Avoid vehicles |
| 786468 | Avoid Traffic | Avoid vehicles + Avoid objects |
| 786603 | Normal | Stop before vehicles/peds + Stop at lights |
| 1074528293 | Rushed | Fast driving with some safety |
| 2883621 | Ignore Lights | Normal but ignores traffic lights |

---

#### Combining Flags

```csharp
// Create custom driving style
int drivingStyle = 0;
drivingStyle |= 1;    // Stop before vehicles
drivingStyle |= 4;    // Avoid vehicles
drivingStyle |= 16;   // Avoid pedestrians
drivingStyle |= 128;  // Stop at traffic lights

// Or add decimal values
int drivingStyle = 1 + 4 + 16 + 128;  // = 149
```

---

### SHVDN Helper Properties for Vehicle State

| SHVDN Property | Native Equivalent | Description |
|----------------|-------------------|-------------|
| `vehicle.Speed` | `GET_ENTITY_SPEED` | Speed in m/s |
| `vehicle.Heading` | `GET_ENTITY_HEADING` | Heading in degrees |
| `vehicle.Velocity` | `GET_ENTITY_VELOCITY` | Velocity vector |
| `vehicle.IsOnAllWheels` | `IS_VEHICLE_ON_ALL_WHEELS` | All wheels grounded |
| `vehicle.ClassType` | `GET_VEHICLE_CLASS` | Vehicle class enum |
| `vehicle.IsHelicopter` | Checks class | Is helicopter type |
| `vehicle.IsPlane` | Checks class | Is plane type |
| `vehicle.IsBike` | Checks class | Is motorcycle |
| `vehicle.IsBicycle` | Checks class | Is bicycle |
| `vehicle.IsBoat` | Checks class | Is boat type |
| `vehicle.UpVector.Z` | Entity rotation | Upright indicator (1=upright, -1=flipped) |
| `ped.IsInVehicle()` | `IS_PED_IN_ANY_VEHICLE` | Ped in any vehicle |
| `ped.CurrentVehicle` | `GET_VEHICLE_PED_IS_IN` | Current vehicle reference |

---

### Sources - World State and Vehicle APIs

- [FiveM Native Reference](https://docs.fivem.net/natives/)
- [GET_VEHICLE_CLASS Native](https://docs.fivem.net/natives/?_0x29439776AAA00A62=)
- [GET_PREV_WEATHER_TYPE_HASH_NAME](https://docs.fivem.net/natives/?_0x564B884A05EC45A3=)
- [GET_NEXT_WEATHER_TYPE_HASH_NAME](https://docs.fivem.net/natives/?_0x711327CD09C8F162=)
- [_GET_WEATHER_TYPE_TRANSITION](https://docs.fivem.net/natives/?_0xF3BBE884A14BB413=)
- [SET_WEATHER_TYPE_NOW](https://docs.fivem.net/natives/?_0x29B487C359E19889=)
- [alt:V Weather Reference](https://docs.altv.mp/gta/articles/references/weather.html)
- [alt:V Time and Date Reference](https://docs.altv.mp/gta/articles/references/time-and-date.html)
- [citizenfx/natives - GetEntitySpeed](https://github.com/citizenfx/natives/blob/master/ENTITY/GetEntitySpeed.md)
- [citizenfx/natives - GetEntityHeading](https://github.com/citizenfx/natives/blob/master/ENTITY/GetEntityHeading.md)
- [citizenfx/natives - GetEntityVelocity](https://github.com/citizenfx/natives/blob/master/ENTITY/GetEntityVelocity.md)
- [citizenfx/natives - GetEntitySpeedVector](https://github.com/citizenfx/natives/blob/master/ENTITY/GetEntitySpeedVector.md)
- [citizenfx/natives - IsVehicleOnAllWheels](https://github.com/citizenfx/natives/blob/master/VEHICLE/IsVehicleOnAllWheels.md)
- [citizenfx/natives - GetClockMinutes](https://github.com/citizenfx/natives/blob/master/CLOCK/GetClockMinutes.md)
- [GTAForums - Driving Styles Guide](https://gtaforums.com/topic/822314-guide-driving-styles/)
- [Vespura Driving Style Calculator](https://vespura.com/fivem/drivingstyle/)
- [SET_DRIVE_TASK_DRIVING_STYLE](https://docs.fivem.net/natives/?_0xDACE1BE37D88AF67=)
- [GTAMods Wiki - SET_DRIVE_TASK_CRUISE_SPEED](https://gtamods.com/wiki/SET_DRIVE_TASK_CRUISE_SPEED)
- [Aircraft Nozzle Precise Control Mod](https://www.gta5-mods.com/scripts/aircraft-nozzle-precise-control)
- [GTABASE - VTOL Vehicle List](https://www.gtabase.com/grand-theft-auto-v/guides/list-of-vehicles-in-gta-5-gta-online-by-feature?feature=vtol)
- [SHVDN Vehicle Class Reference](https://nitanmarcel.github.io/shvdn-docs.github.io/class_g_t_a_1_1_vehicle.html)
- [SHVDN Ped Class Reference](https://nitanmarcel.github.io/shvdn-docs.github.io/class_g_t_a_1_1_ped.html)
