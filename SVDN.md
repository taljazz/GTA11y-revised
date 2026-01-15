# ScriptHookVDotNet Version

## Current Version
**ScriptHookVDotNet 3.4.0.0**

## Version History for GTA11Y

| Date | Version | Notes |
|------|---------|-------|
| January 2026 | 3.4.0.0 | Upgraded from 3.0.2.0 for vehicle modification APIs |
| December 2025 | 3.0.2.0 | Initial optimization |

## Why 3.4.0.0?

### Features Used
- `vehicle.Mods.InstallModKit()` - Required before applying mods
- `vehicle.Mods[VehicleModType.X].Index` - Get/set mod level
- `vehicle.Mods[VehicleToggleModType.X].IsInstalled` - Toggle mods
- `vehicle.Mods.PrimaryColor`, `SecondaryColor`, `CustomPrimaryColor`
- `vehicle.Mods.WheelType`, `WindowTint`
- `vehicle.Mods.SetNeonLightsOn()`, `NeonLightsColor`
- `vehicle.Mods.LicensePlate`, `LicensePlateStyle`

### Native Function Calls
All native function calls use `Function.Call()` with explicit parameter types:
- Entity parameters: Always use `.Handle` (int)
- `iFlightHeight` and `iMinHeightAboveTerrain` in `TASK_PLANE_MISSION`: Cast to `(int)`
- Floats: Pass as float literals or variables

## Native Functions Used

### Flight Tasks
| Native | Hash | Parameters |
|--------|------|------------|
| `TASK_PLANE_MISSION` | `0x23703CD154E83B88` | 14 params - ped, vehicle, targetVehicle, targetPed, x, y, z, missionType, speed, targetReachedDist, heading, **iFlightHeight (INT)**, **iMinHeightAboveTerrain (INT)**, bPrecise |
| `TASK_HELI_MISSION` | `0xDAD029E187A2BEB4` | 15 params - ped, vehicle, vehicleTarget, pedTarget, x, y, z, missionType, speed, radius, heading, height, minHeight, slowDist, missionFlags |
| `TASK_PLANE_LAND` | `0xBF19721FA34D32C0` | 8 params - pilot, plane, runwayStartX, runwayStartY, runwayStartZ, runwayEndX, runwayEndY, runwayEndZ |

### Ground Vehicle Drive Tasks
| Native | Hash | Parameters |
|--------|------|------------|
| `TASK_VEHICLE_DRIVE_TO_COORD` | `0xE2A2AA2F659D77A7` | 11 params - ped (Handle), vehicle (Handle), x, y, z, speed, p6 (unused), modelHash, drivingStyle, stopRange, p10 |
| `TASK_VEHICLE_DRIVE_WANDER` | `0x480142959D337D00` | 4 params - ped (Handle), vehicle (Handle), speed, drivingStyle |
| `TASK_VEHICLE_TEMP_ACTION` | `0xC429DCEEB339E129` | 4 params - driver (Handle), vehicle (Handle), action (int), duration (ms) |
| `SET_DRIVE_TASK_CRUISE_SPEED` | `0x5C9B84BD7D31D908` | 2 params - ped (Handle), speed (float) |
| `SET_DRIVE_TASK_DRIVING_STYLE` | `0xDACE1BE37D88AF67` | 2 params - ped (Handle), drivingStyle (int) |
| `SET_DRIVER_ABILITY` | `0xB195CD0A4F5B4D82` | 2 params - driver (Handle), ability (float 0.0-1.0) |
| `SET_DRIVER_AGGRESSIVENESS` | `0xA731F608CA104E3C` | 2 params - driver (Handle), aggressiveness (float 0.0-1.0) |

### Vehicle State Natives
| Native | Hash | Parameters |
|--------|------|------------|
| `IS_VEHICLE_IN_WATER` | `0x5E0B7AF4` | 1 param - vehicle (Handle) |
| `IS_VEHICLE_SIREN_AUDIO_ON` | `0x88EB7F24` | 1 param - vehicle (Handle) |
| `SET_VEHICLE_HANDBRAKE` | `0x6E2AA6A4` | 2 params - vehicle (Handle), toggle (bool) |
| `SET_VEHICLE_LIGHTS` | `0x34E710FF` | 2 params - vehicle (Handle), state (int: 0=off, 2=on) |
| `CLEAR_PED_TASKS` | `0xE1EF3C1B` | 1 param - ped (Handle) |

### Landing Gear
| Native | Hash | Parameters |
|--------|------|------------|
| `CONTROL_LANDING_GEAR` | `0xCFB0019F3B5B85A2` | 2 params - vehicle (Handle), state (int: 0=up, 1=down) |

## Mission Types (eVehicleMission)

| Value | Name | Description |
|-------|------|-------------|
| 1 | MISSION_CRUISE | Maintain heading and altitude |
| 4 | MISSION_GOTO | Navigate to coordinates |
| 9 | MISSION_CIRCLE | Circle around coordinates |
| 19 | MISSION_LAND | Land at coordinates |

## Helicopter Mission Flags (eHeliMissionFlags)

| Value | Name | Description |
|-------|------|-------------|
| 0 | HELI_FLAG_NONE | No flags |
| 1 | ATTAIN_REQUESTED_ORIENTATION | Match requested heading |
| 2 | DONT_MODIFY_ORIENTATION | Keep current orientation |
| 4 | DONT_MODIFY_PITCH | Keep current pitch |
| 8 | DONT_MODIFY_THROTTLE | Keep current throttle |
| 16 | DONT_MODIFY_ROLL | Keep current roll |
| 32 | LAND_ON_ARRIVAL | Land when reaching destination |
| 64 | DONT_DO_AVOIDANCE | Skip obstacle avoidance (faster landing) |
| 128 | START_ENGINE_IMMEDIATELY | Start engine right away |
| 4096 | MAINTAIN_HEIGHT_ABOVE_TERRAIN | Terrain following |

### Common Combined Flags
- `96` = LAND_ON_ARRIVAL (32) + DONT_DO_AVOIDANCE (64) - Standard helicopter landing
- `4224` = MAINTAIN_HEIGHT_ABOVE_TERRAIN (4096) + START_ENGINE_IMMEDIATELY (128) - Cruise with terrain following

## Driving Style Flags (eVehicleDrivingFlags)

Common predefined styles:
| Value | Name | Description |
|-------|------|-------------|
| 786603 | DrivingModeStopForVehicles | Stop for traffic, obey lights |
| 786469 | DrivingModeAvoidVehicles | Swerve around obstacles |
| 786468 | DrivingModeAvoidVehiclesReckless | Aggressive swerving |
| 786597 | DrivingModeAvoidVehiclesObeyLights | Balanced safety |

Key flags (combine with bitwise OR):
| Flag | Value | Description |
|------|-------|-------------|
| StopForVehicles | 1 | Stop for other vehicles |
| StopForPeds | 2 | Stop for pedestrians |
| SwerveAroundAllVehicles | 4 | Swerve around all vehicles |
| StopAtTrafficLights | 128 | Obey traffic lights |
| GoOffRoadWhenAvoiding | 256 | Go off-road to avoid |
| AllowGoingWrongWay | 512 | Allow wrong-way driving |

## Temp Actions (eTempAction)

| Code | Name | Description |
|------|------|-------------|
| 1 | WAIT | Wait in place |
| 3 | BRAKE_REVERSE | Brake and reverse |
| 4 | HANDBRAKE_TURN_LEFT | Handbrake turn left |
| 5 | HANDBRAKE_TURN_RIGHT | Handbrake turn right |
| 6 | HANDBRAKE_UNTIL_ENDS | Handbrake until time expires |
| 7 | TURN_LEFT | Turn left |
| 8 | TURN_RIGHT | Turn right |
| 9 | ACCELERATE | Accelerate forward |
| 13 | TURN_LEFT_GO_REVERSE | Turn left while reversing |
| 14 | TURN_RIGHT_GO_REVERSE | Turn right while reversing |
| 22 | GO_IN_REVERSE | Reverse straight |
| 30 | BURNOUT | Perform burnout |
| 31 | REV_ENGINE | Rev engine |

**Warning:** Actions 15-18 (plane actions) will crash if used outside aircraft.

## Backward Compatibility
All existing SHVDN 3.0.2.0 APIs remain compatible in 3.4.0.0. No breaking changes.

## References
- [ScriptHookVDotNet GitHub](https://github.com/scripthookvdotnet/scripthookvdotnet)
- [FiveM Native Reference](https://docs.fivem.net/natives/)
- [GTAMods Wiki](https://gtamods.com/wiki/)
