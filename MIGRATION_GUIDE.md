# Migration Guide: Original → Optimized Code

This guide helps you understand the differences between the original and optimized versions.

## Quick Reference

| Original | Optimized | Reason |
|----------|-----------|--------|
| `getSetting("godMode") == 1` | `_settings.GetSetting("godMode")` | Proper bool instead of int |
| `Tolk.Speak()` | `_audio.Speak()` | Centralized in AudioManager |
| String concatenation in loops | `StringBuilder` | Eliminates allocations |
| `hashes.ContainsKey(k); hashes[k]` | `hashes.TryGetValue(k, out v)` | Single lookup |
| Magic numbers (50, 2.5, etc.) | `Constants.NEARBY_ENTITY_RADIUS` | Named constants |
| Everything in `onTick` | Throttled with tick intervals | Performance |
| Monolithic 1500-line class | Manager classes + menus | Separation of concerns |

## File Structure Changes

### New Files Created

```
GTA/
├── Constants.cs                 # All magic numbers
├── AudioManager.cs              # Audio output
├── SettingsManager.cs           # Settings persistence
├── EntityScanner.cs             # Entity detection
├── SpatialCalculator.cs         # Math utilities
├── Menus/
│   ├── IMenuState.cs           # Menu interface
│   ├── MenuManager.cs          # Menu coordinator
│   ├── LocationMenu.cs         # Teleport menu
│   ├── VehicleSpawnMenu.cs     # Vehicle spawn menu
│   ├── FunctionsMenu.cs        # Functions menu
│   └── SettingsMenu.cs         # Settings menu
└── GTA11Y.cs                   # Main class (optimized)
```

### Modified Files

- `GTA11Y.cs` - Completely rewritten, optimized
- `GTA11Y.csproj` - Updated to include new files

### Backup Files

- `GTA11Y_Original_Backup.cs` - Original code preserved

## Code Migration Examples

### Settings Access

**Before:**
```csharp
private int getSetting(string id)
{
    int result = -1;
    for (int i = 0; i < settingsMenu.Count; i++)
    {
        if (settingsMenu[i].id == id)
            result = settingsMenu[i].value;
    }
    return result;
}

// Usage:
if (getSetting("godMode") == 1)
{
    Game.Player.IsInvincible = true;
}
```

**After:**
```csharp
// SettingsManager handles this internally with Dictionary
bool godMode = _settings.GetSetting("godMode");
Game.Player.IsInvincible = godMode;
```

### Audio Output

**Before:**
```csharp
Tolk.Speak("Message");

out1.Stop();
tped.Position = 0;
out1.Play();
```

**After:**
```csharp
_audio.Speak("Message");

_audio.PlayPedTargetSound();
```

### Entity Scanning

**Before:**
```csharp
Vehicle[] vehicles = World.GetNearbyVehicles(Game.Player.Character.Position, 50);
string status;
List<Result> results = new List<Result>();

foreach (Vehicle vehicle in vehicles)
{
    // ... validation ...
    string name = (status + " " + vehicle.LocalizedName);
    double xyDistance = Math.Round(World.GetDistance(...) - Math.Abs(...), 1);
    double zDistance = Math.Round(vehicle.Position.Z - ..., 1);
    string direction = getDir(calculate_x_y_angle(...));
    Result result = new Result(name, xyDistance, zDistance, direction);
    results.Add(result);
}

Tolk.Speak(listToString(results, "Nearest Vehicles: "));
```

**After:**
```csharp
bool onScreenOnly = _settings.GetSetting("onscreen");
string result = _scanner.ScanNearbyVehicles(
    Game.Player.Character.Position,
    Game.Player.Character.CurrentVehicle,
    onScreenOnly
);
_audio.Speak(result);
```

### Menu Navigation

**Before:**
```csharp
// 100+ lines of switch statements and nested ifs
if (e.KeyCode == Keys.NumPad1 && !keyState[1])
{
    keyState[1] = true;
    if (mainMenuIndex == 0)
    {
        if (locationMenuIndex > 0)
        {
            locationMenuIndex--;
            Tolk.Speak(locations[locationMenuIndex].name);
        }
        else
        {
            locationMenuIndex = locations.Count - 1;
            Tolk.Speak(locations[locationMenuIndex].name);
        }
    }
    // ... 100+ more lines for other menus
}
```

**After:**
```csharp
case Keys.NumPad1 when !_keyStates[1]:
    _keyStates[1] = true;
    Audio.PlaySoundFrontend("NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
    _menu.NavigatePreviousItem(_controlHeld);
    _audio.Speak(_menu.GetCurrentItemText());
    break;
```

### Spatial Calculations

**Before:**
```csharp
private double calculate_x_y_angle(double x1, double y1, double x2, double y2, double deg)
{
    double x = x1 - x2;
    double y = y2 - y1;
    double rad = 0;
    if (x == 0 || y == 0)
    {
        rad = Math.Atan(0);
    }
    else
    {
        rad = Math.Atan(y / x);
    }
    // ... 30 more lines ...
}
```

**After:**
```csharp
// Static utility class
double angle = SpatialCalculator.CalculateAngle(x1, y1, x2, y2);
string direction = SpatialCalculator.GetDirectionTo(fromPos, toPos);
```

## Tick Event Optimization

### Before: Everything Every Frame

```csharp
private void onTick(object sender, EventArgs e)
{
    if (!Game.IsLoading)
    {
        // Altitude check - EVERY FRAME
        if (Game.Player.Character.HeightAboveGround - z > 1f || ...)
        {
            z = Game.Player.Character.HeightAboveGround;
            if (getSetting("altitudeIndicator") == 1)
            {
                // Play sound
            }
        }

        // Street check - EVERY FRAME
        if (street != World.GetStreetName(Game.Player.Character.Position))
        {
            street = World.GetStreetName(Game.Player.Character.Position);
            // Announce
        }

        // Vehicle speed - EVERY FRAME
        if (DateTime.Now.Ticks - drivingTicks > 25000000 && ...)
        {
            // Announce speed
        }

        // ... 400+ more lines of per-frame work
    }
}
```

### After: Throttled and Optimized

```csharp
private void OnTick(object sender, EventArgs e)
{
    if (Game.IsLoading) return;

    long currentTick = DateTime.Now.Ticks;

    // Altitude - throttled to 0.1s
    if (_settings.GetSetting("altitudeIndicator") &&
        currentTick - _lastAltitudeTick > Constants.TICK_INTERVAL_ALTITUDE)
    {
        _lastAltitudeTick = currentTick;
        float altitude = player.HeightAboveGround;

        if (Math.Abs(altitude - _lastAltitude) > Constants.HEIGHT_CHANGE_THRESHOLD)
        {
            _lastAltitude = altitude;
            _audio.PlayAltitudeIndicator(altitude);
        }
    }

    // Street check - throttled to 0.5s
    if (currentTick - _lastStreetCheckTick > Constants.TICK_INTERVAL_STREET_CHECK)
    {
        _lastStreetCheckTick = currentTick;
        UpdateStreetAnnouncement(playerPos);
    }

    // Apply cheat settings (must run every frame)
    ApplyCheatSettings(player, currentVehicle);
}
```

## Settings System Migration

### Before: Integer-based

```csharp
class Setting
{
    public string id;
    public string displayName;
    public int value; // 0 or 1
}

Dictionary<string, int> settings = new Dictionary<string, int>();
```

### After: Boolean-based

```csharp
Dictionary<string, bool> _settings = new Dictionary<string, bool>();

public bool GetSetting(string id)
{
    return _settings.TryGetValue(id, out bool value) && value;
}

public bool ToggleSetting(string id)
{
    if (_settings.ContainsKey(id))
    {
        _settings[id] = !_settings[id];
        return _settings[id];
    }
    return false;
}
```

**Settings files are forward compatible** - old JSON files with int values will be automatically converted.

## Object Pooling Pattern

### Before: Create on Every Scan

```csharp
// In NumPad4 handler (runs every keypress)
List<Result> results = new List<Result>();

foreach (Vehicle vehicle in vehicles)
{
    Result result = new Result(name, xyDistance, zDistance, direction); // NEW ALLOCATION
    results.Add(result);
}
```

### After: Reuse Pooled Objects

```csharp
// EntityScanner.cs
private readonly List<Result> _resultPool;

public EntityScanner()
{
    _resultPool = new List<Result>(50);
    for (int i = 0; i < 50; i++)
    {
        _resultPool.Add(new Result("", 0, 0, ""));
    }
}

private Result GetPooledResult(string name, double xyDistance, double zDistance, string direction)
{
    if (_poolIndex < _resultPool.Count)
    {
        Result r = _resultPool[_poolIndex++];
        r.name = name;
        r.xyDistance = xyDistance;
        r.zDistance = zDistance;
        r.direction = direction;
        r.totalDistance = xyDistance + Math.Abs(zDistance);
        return r;
    }
    return new Result(name, xyDistance, zDistance, direction);
}
```

## Constants Migration

### Before: Magic Numbers Everywhere

```csharp
if (DateTime.Now.Ticks - drivingTicks > 25000000) // What does this mean?
{
    double speedMph = Game.Player.Character.CurrentVehicle.Speed * 2.23694; // ???
    Tolk.Speak("" + Math.Round(speedMph) + " mph");
}

Vehicle[] vehicles = World.GetNearbyVehicles(pos, 50); // Why 50?
```

### After: Named Constants

```csharp
if (currentTick - _lastVehicleSpeedTick > Constants.TICK_INTERVAL_VEHICLE_SPEED)
{
    double speedMph = currentVehicle.Speed * Constants.METERS_PER_SECOND_TO_MPH;
    _audio.Speak($"{Math.Round(speedMph)} mph");
}

Vehicle[] vehicles = World.GetNearbyVehicles(pos, Constants.NEARBY_ENTITY_RADIUS);
```

## Testing Your Changes

1. **Build the project:**
   ```bash
   msbuild GTA11Y.csproj /p:Configuration=Release /p:Platform=x64
   ```

2. **Compare output size:**
   - Original DLL should be similar size
   - Performance difference will be in runtime, not file size

3. **Test all features:**
   - NumPad 0-9 and Decimal key
   - All menu navigation
   - Entity scanning
   - Audio cues
   - Settings persistence

4. **Profile performance:**
   - Use dotMemory or PerfView to check allocations
   - Monitor FPS with mod enabled
   - Check GC collections (should be much fewer)

## Common Pitfalls

1. **Don't forget to initialize managers:**
   ```csharp
   _settings = new SettingsManager();  // Load settings first
   _audio = new AudioManager();        // Then audio
   _scanner = new EntityScanner();     // Then scanner
   ```

2. **Dispose AudioManager properly:**
   ```csharp
   // In destructor or cleanup
   _audio?.Dispose();
   ```

3. **Settings are now bool, not int:**
   ```csharp
   // WRONG:
   if (_settings.GetSetting("godMode") == 1)

   // RIGHT:
   if (_settings.GetSetting("godMode"))
   ```

4. **Use throttled operations correctly:**
   ```csharp
   // Always check interval before running expensive ops
   if (currentTick - _lastCheckTick > Constants.INTERVAL)
   {
       _lastCheckTick = currentTick;
       // Do expensive work
   }
   ```

## Performance Tuning

To adjust throttling intervals, edit `Constants.cs`:

```csharp
public const long TICK_INTERVAL_VEHICLE_SPEED = 25_000_000;  // 2.5 seconds

// Make it faster (more responsive but more CPU):
public const long TICK_INTERVAL_VEHICLE_SPEED = 10_000_000;  // 1.0 second

// Make it slower (less responsive but less CPU):
public const long TICK_INTERVAL_VEHICLE_SPEED = 50_000_000;  // 5.0 seconds
```

Remember: 10,000 ticks = 1 millisecond

## Reverting to Original Code

If you need to revert:

1. Rename `GTA11Y_Original_Backup.cs` to `GTA11Y.cs`
2. Remove new files from `.csproj`
3. Rebuild

The original code is fully functional and preserved.
