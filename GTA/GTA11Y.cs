using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GrandTheftAccessibility.Menus;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Main mod class - heavily optimized for performance
    /// Key optimizations:
    /// - Throttled tick events (not everything runs every frame)
    /// - Manager classes for separation of concerns
    /// - Object pooling and StringBuilder to reduce allocations
    /// - Proper boolean settings instead of int
    /// - State pattern for menus
    /// - Bug fixes from original code
    /// </summary>
    public class GTA11Y : Script
    {
        // Manager classes
        private readonly AudioManager _audio;
        private readonly SettingsManager _settings;
        private readonly EntityScanner _scanner;
        private readonly MenuManager _menu;
        private readonly HealthArmorManager _healthArmor;
        private readonly VehicleDamageManager _vehicleDamage;
        private readonly CombatAssistManager _combat;
        private readonly BlipManager _blips;
        private readonly GameStateManager _gameState;

        // Cached state (to avoid repeated lookups)
        private WeaponHash _currentWeaponHash;  // Use enum directly, not string (avoids ToString() allocation)
        private string _currentStreet;
        private string _currentZone;

        private float _lastAltitude;
        private float _lastPitch;
        private bool _timeAnnounced;

        // Aircraft mode tracking
        private float _lastAnnouncedAltitudeFeet;  // For aircraft spoken altitude
        private float _lastAircraftPitch;           // Vehicle pitch angle
        private float _lastAircraftRoll;            // Vehicle roll angle
        private long _lastAircraftPitchPulseTick;   // Last time pitch pulse played
        private long _lastAircraftRollPulseTick;    // Last time roll pulse played
        private bool _wasInverted;                  // Track inverted state for announcements

        // Heading tracking (8-slice compass)
        private readonly bool[] _headingSlices;

        // Key state tracking (prevents key repeat)
        private readonly bool[] _keyStates;
        private bool _controlHeld;
        private bool _keysDisabled;

        // Cached cheat states (for write-only properties)
        private bool _cachedPoliceIgnore;
        private bool _cachedInfiniteAmmo;
        private bool _cachedRadioOff;
        private int _lastVehicleHandleForRadio;  // Track which vehicle we set radio for

        // PERFORMANCE: Cached per-frame cheat settings (avoid dictionary lookups every frame)
        private bool _cachedExplosiveAmmo;
        private bool _cachedFireAmmo;
        private bool _cachedExplosiveMelee;
        private bool _cachedSuperJump;
        private bool _cachedRunFaster;
        private bool _cachedSwimFaster;
        private long _lastCheatSettingsRefreshTick;

        // GTA Online features tracking
        private bool _cachedEnableMPMaps;
        private bool _mpMapsCurrentlyEnabled;  // Track actual state to detect changes

        // Tick throttling (avoid running expensive operations every frame)
        private long _lastVehicleSpeedTick;
        private long _lastTargetingTick;
        private long _lastStreetCheckTick;
        private long _lastZoneCheckTick;
        private long _lastAltitudeTick;
        private long _lastPitchTick;
        private long _lastTolkHealthCheckTick;
        private long _lastAircraftAttitudeTick;
        private long _lastAutoDriveTick;
        private long _lastRoadFeatureTick;

        public GTA11Y()
        {
            // Initialize logging first
            Logger.Initialize();
            Logger.Info("GTA11Y constructor starting...");

            try
            {
                // Initialize managers
                Logger.Debug("Initializing SettingsManager...");
                _settings = new SettingsManager();

                Logger.Debug("Initializing AudioManager...");
                _audio = new AudioManager();

                Logger.Debug("Initializing EntityScanner...");
                _scanner = new EntityScanner();

                Logger.Debug("Initializing MenuManager...");
                _menu = new MenuManager(_settings, _audio);

                // Initialize new accessibility managers
                _healthArmor = new HealthArmorManager(_audio, _settings);
                _vehicleDamage = new VehicleDamageManager(_audio, _settings);
                _combat = new CombatAssistManager(_audio, _settings);
                _blips = new BlipManager(_audio, _settings);
                _gameState = new GameStateManager(_audio);

                // Initialize state tracking
                _headingSlices = new bool[Constants.HEADING_SLICE_COUNT];
                _keyStates = new bool[20];
                _currentWeaponHash = WeaponHash.Unarmed;  // Will be set on first tick (don't access Game.Player in constructor)
                _keysDisabled = false;

                // Register event handlers
                Tick += OnTick;
                KeyDown += OnKeyDown;
                KeyUp += OnKeyUp;
                Aborted += OnAborted;

                Logger.Info("GTA11Y initialized successfully");

                // Log session diagnostics for troubleshooting
                Logger.LogSessionInfo("1.0.0", "3.4.0.0");
                Logger.LogSettings(_settings.AllBoolSettings, _settings.AllIntSettings);

                // Announce mod ready
                _audio.Speak("Mod Ready");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "GTA11Y Constructor");
                throw;  // Re-throw to let SHVDN handle it
            }
        }

        /// <summary>
        /// Main tick event - highly optimized to minimize per-frame work
        /// </summary>
        private void OnTick(object sender, EventArgs e)
        {
            // Guard: Skip if game is loading
            if (Game.IsLoading) return;

            // Guard: Ensure player exists and is valid
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists() || player.IsDead) return;

            long currentTick = DateTime.Now.Ticks;
            Vector3 playerPos = player.Position;

            // Altitude indicator (throttled to 0.1s) - now supports 3 modes
            int altitudeMode = _settings.GetIntSetting("altitudeMode");
            if (altitudeMode != Constants.ALTITUDE_MODE_OFF && currentTick - _lastAltitudeTick > Constants.TICK_INTERVAL_ALTITUDE)
            {
                _lastAltitudeTick = currentTick;
                float altitude = player.HeightAboveGround;

                if (altitudeMode == Constants.ALTITUDE_MODE_NORMAL)
                {
                    // Normal mode: continuous tone
                    if (Math.Abs(altitude - _lastAltitude) > Constants.HEIGHT_CHANGE_THRESHOLD)
                    {
                        _lastAltitude = altitude;
                        _audio.PlayAltitudeIndicator(altitude);
                    }
                }
                else if (altitudeMode == Constants.ALTITUDE_MODE_AIRCRAFT)
                {
                    // Aircraft mode: spoken altitude at intervals
                    float altitudeFeet = altitude * Constants.METERS_TO_FEET;
                    float interval = altitudeFeet < Constants.AIRCRAFT_ALTITUDE_THRESHOLD
                        ? Constants.AIRCRAFT_ALTITUDE_FINE_INTERVAL    // Every 50 feet below 500
                        : Constants.AIRCRAFT_ALTITUDE_COARSE_INTERVAL; // Every 500 feet above 500

                    // Calculate which interval we're in
                    float currentInterval = (float)Math.Floor(altitudeFeet / interval) * interval;

                    if (Math.Abs(currentInterval - _lastAnnouncedAltitudeFeet) >= interval)
                    {
                        _lastAnnouncedAltitudeFeet = currentInterval;
                        int displayAltitude = (int)Math.Round(currentInterval);
                        if (displayAltitude > 0)
                        {
                            _audio.Speak($"{displayAltitude} feet");
                        }
                    }
                }
            }

            // Pitch indicator (throttled to 0.05s)
            if (_settings.GetSetting("targetPitchIndicator") && currentTick - _lastPitchTick > Constants.TICK_INTERVAL_PITCH)
            {
                _lastPitchTick = currentTick;

                if (GameplayCamera.IsAimCamActive)
                {
                    float pitch = GameplayCamera.RelativePitch;

                    if (Math.Abs(pitch - _lastPitch) > Constants.PITCH_CHANGE_THRESHOLD)
                    {
                        _lastPitch = pitch;
                        _audio.PlayPitchIndicator(pitch);
                    }
                }
            }

            // Wanted level changes are handled by BlipManager.Update() below

            // Radio control - use IsInVehicle() to avoid stale CurrentVehicle references
            Vehicle currentVehicle = null;
            try
            {
                currentVehicle = player.IsInVehicle() ? player.CurrentVehicle : null;
                // Validate vehicle still exists (can become invalid mid-tick)
                if (currentVehicle != null && !currentVehicle.Exists())
                    currentVehicle = null;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Getting current vehicle");
                currentVehicle = null;
            }

            if (currentVehicle != null)
            {
                try
                {
                    // Only set radio property when setting changes or vehicle changes (avoid per-tick writes)
                    bool radioOff = _settings.GetSetting("radioOff");
                    int vehicleHandle = currentVehicle.Handle;
                    if (radioOff != _cachedRadioOff || vehicleHandle != _lastVehicleHandleForRadio)
                    {
                        _cachedRadioOff = radioOff;
                        _lastVehicleHandleForRadio = vehicleHandle;
                        currentVehicle.IsRadioEnabled = !radioOff;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "Setting radio state");
                }
            }

            // Apply cheat settings
            ApplyCheatSettings(player, currentVehicle, currentTick);

            // Heading announcements (only when not in certain states)
            if (!player.IsFalling && !player.IsGettingIntoVehicle && !player.IsGettingUp &&
                !player.IsProne && !player.IsRagdoll)
            {
                UpdateHeadingAnnouncement(player.Heading);
            }

            // Time announcements (every 3 hours at :00)
            UpdateTimeAnnouncement();

            // Street/Zone changes (throttled to 0.5s)
            if (currentTick - _lastStreetCheckTick > Constants.TICK_INTERVAL_STREET_CHECK)
            {
                _lastStreetCheckTick = currentTick;
                UpdateStreetAnnouncement(playerPos);
            }

            if (currentTick - _lastZoneCheckTick > Constants.TICK_INTERVAL_ZONE_CHECK)
            {
                _lastZoneCheckTick = currentTick;
                UpdateZoneAnnouncement(playerPos);
            }

            // Weapon changes (compare enum directly - no ToString() allocation)
            try
            {
                var currentWeapon = player.Weapons?.Current;
                if (currentWeapon != null)
                {
                    WeaponHash weaponHash = currentWeapon.Hash;
                    if (weaponHash != _currentWeaponHash)
                    {
                        _currentWeaponHash = weaponHash;
                        _audio.Speak(weaponHash.ToString());  // Only allocate string when weapon actually changes
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Weapon change detection");
            }

            // Vehicle speed announcement (throttled to 2.5s)
            if (_settings.GetSetting("speed") && currentVehicle != null && currentVehicle.Exists() &&
                currentTick - _lastVehicleSpeedTick > Constants.TICK_INTERVAL_VEHICLE_SPEED)
            {
                _lastVehicleSpeedTick = currentTick;
                try
                {
                    float speed = currentVehicle.Speed;
                    if (speed > 1)
                    {
                        double speedMph = speed * Constants.METERS_PER_SECOND_TO_MPH;
                        _audio.Speak($"{Math.Round(speedMph)} mph");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "Vehicle speed announcement");
                }
            }

            // Targeting feedback (throttled to 0.2s)
            try
            {
                Entity targetedEntity = Game.Player?.TargetedEntity;
                var currentWeapon = player.Weapons?.Current;
                if (targetedEntity != null && targetedEntity.Exists() &&
                    currentWeapon != null && currentWeapon.Hash != WeaponHash.HomingLauncher &&
                    currentTick - _lastTargetingTick > Constants.TICK_INTERVAL_TARGET)
                {
                    _lastTargetingTick = currentTick;
                    PlayTargetingSound(targetedEntity);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Targeting feedback");
            }

            // Aircraft attitude indicator (pitch and roll) - hybrid pulse rate with aircraft type
            if (_settings.GetSetting("aircraftAttitude") &&
                currentVehicle != null && currentVehicle.Exists() &&
                IsAircraft(currentVehicle) &&
                currentTick - _lastAircraftAttitudeTick > Constants.TICK_INTERVAL_AIRCRAFT_ATTITUDE)
            {
                _lastAircraftAttitudeTick = currentTick;

                try
                {
                    // Determine aircraft type (affects thresholds and features)
                    int aircraftType = GetAircraftType(currentVehicle);

                    // Get vehicle rotation (pitch, roll, yaw)
                    Vector3 rotation = currentVehicle.Rotation;
                    float pitch = rotation.X;  // Nose up/down
                    float roll = rotation.Y;   // Bank left/right

                    // Inverted/upright detection (fixed-wing and VTOL plane mode only)
                    if (aircraftType == Constants.AIRCRAFT_TYPE_FIXED_WING ||
                        aircraftType == Constants.AIRCRAFT_TYPE_VTOL_PLANE)
                    {
                        bool isInverted = Math.Abs(roll) > Constants.INVERTED_ROLL_THRESHOLD;

                        if (isInverted && !_wasInverted)
                        {
                            _wasInverted = true;
                            _audio.Speak("Inverted", true);
                        }
                        else if (!isInverted && _wasInverted)
                        {
                            _wasInverted = false;
                            _audio.Speak("Upright", true);
                        }
                    }

                    // Get pulse interval based on pitch angle and aircraft type
                    float absPitch = Math.Abs(pitch);
                    long pitchPulseInterval = GetPulseIntervalForAngle(absPitch, aircraftType);

                    // Play pitch indicator based on variable pulse rate
                    if (pitchPulseInterval != Constants.AIRCRAFT_PULSE_SILENT &&
                        currentTick - _lastAircraftPitchPulseTick > pitchPulseInterval)
                    {
                        _lastAircraftPitchPulseTick = currentTick;
                        _lastAircraftPitch = pitch;
                        _audio.PlayAircraftPitchIndicator(pitch);
                    }

                    // Get pulse interval based on roll angle and aircraft type
                    // For inverted aircraft, use adjusted roll for pulse calculation
                    float absRoll = Math.Abs(roll);
                    if (_wasInverted && absRoll > 90f)
                    {
                        absRoll = 180f - absRoll;  // Normalize for inverted flight
                    }
                    long rollPulseInterval = GetPulseIntervalForAngle(absRoll, aircraftType);

                    // Play roll indicator based on variable pulse rate (stereo panned)
                    if (rollPulseInterval != Constants.AIRCRAFT_PULSE_SILENT &&
                        currentTick - _lastAircraftRollPulseTick > rollPulseInterval)
                    {
                        _lastAircraftRollPulseTick = currentTick;
                        _lastAircraftRoll = roll;
                        _audio.PlayAircraftRollIndicator(roll);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "Aircraft Attitude");
                }
            }

            // Update aircraft indicator audio (handles timed stop - runs every frame, very lightweight)
            _audio.UpdateAircraftIndicators();

            // Aircraft landing navigation updates (when flying with active navigation)
            if (currentVehicle != null && currentVehicle.Exists() && IsAircraft(currentVehicle))
            {
                try
                {
                    _menu.UpdateAircraftNavigation(currentVehicle, playerPos, currentTick);
                    _menu.UpdateAircraftBeacon(currentVehicle, playerPos, currentTick);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "Aircraft navigation update");
                }
            }

            // AutoDrive and road feature updates (ground vehicles only)
            if (currentVehicle != null && currentVehicle.Exists() && !IsAircraft(currentVehicle))
            {
                try
                {
                    // AutoDrive navigation updates (0.2s throttle)
                    if (_menu.IsAutoDriveActive &&
                        currentTick - _lastAutoDriveTick > Constants.TICK_INTERVAL_AUTODRIVE_UPDATE)
                    {
                        _lastAutoDriveTick = currentTick;
                        _menu.UpdateAutoDrive(currentVehicle, playerPos, currentTick);

                        // Road type detection (throttled internally to 1.0s)
                        bool announceRoadType = _settings.GetSetting("announceRoadType");
                        _menu.CheckRoadTypeChange(playerPos, currentTick, announceRoadType);

                        // Road seeking updates (throttled internally to 3.0s)
                        _menu.UpdateRoadSeeking(currentVehicle, playerPos, currentTick);
                    }

                    // Road feature announcements (0.5s throttle) - works even without autodrive
                    if (_settings.GetSetting("roadFeatureAnnouncements") &&
                        currentTick - _lastRoadFeatureTick > Constants.TICK_INTERVAL_ROAD_FEATURE)
                    {
                        _lastRoadFeatureTick = currentTick;
                        _menu.CheckRoadFeatures(currentVehicle, playerPos, currentTick);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "AutoDrive/road feature update");
                }
            }

            // Turret crew updates (for weaponized vehicles)
            if (_menu.IsTurretCrewActive)
            {
                try
                {
                    _menu.UpdateTurretCrew(currentTick);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "Turret crew update");
                }
            }

            // Periodic Tolk health check (every 10s) - ensures speech stays working
            if (currentTick - _lastTolkHealthCheckTick > Constants.TICK_INTERVAL_TOLK_HEALTH)
            {
                _lastTolkHealthCheckTick = currentTick;
                _audio.CheckTolkHealth();
            }

            // Health and armor monitoring (throttled internally to 1s)
            _healthArmor.Update(player, currentTick);

            // Vehicle damage monitoring (when in vehicle)
            if (currentVehicle != null && currentVehicle.Exists())
            {
                _vehicleDamage.Update(currentVehicle, currentTick);
            }

            // Combat assistance (damage direction, combat state)
            _combat.Update(player, playerPos, currentTick);

            // Wanted level and blip monitoring (throttled internally)
            _blips.Update(currentTick);

            // Game state monitoring (cutscenes, phone, loading - throttled internally to 500ms)
            _gameState.Update(currentTick);

            // Pedestrian navigation (when on foot and active)
            if (_menu.IsPedestrianNavigationActive && currentVehicle == null)
            {
                try
                {
                    _menu.UpdatePedestrianNavigation(player, playerPos, currentTick);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "Pedestrian navigation update");
                }
            }
        }

        /// <summary>
        /// Check if a vehicle is an aircraft (plane or helicopter)
        /// </summary>
        private bool IsAircraft(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return false;

            try
            {
                // Check vehicle class - Planes = 16, Helicopters = 15
                VehicleClass vehicleClass = vehicle.ClassType;
                return vehicleClass == VehicleClass.Planes || vehicleClass == VehicleClass.Helicopters;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determine the aircraft type for threshold selection
        /// </summary>
        private int GetAircraftType(Vehicle vehicle)
        {
            try
            {
                if (vehicle == null || !vehicle.Exists())
                    return Constants.AIRCRAFT_TYPE_FIXED_WING;

                int modelHash = vehicle.Model.Hash;

                // Check if it's a blimp (HashSet lookup is O(1))
                if (Constants.BLIMP_VEHICLE_HASHES.Contains(modelHash))
                    return Constants.AIRCRAFT_TYPE_BLIMP;

                // Check if it's a VTOL aircraft (HashSet lookup is O(1))
                if (Constants.VTOL_VEHICLE_HASHES.Contains(modelHash))
                {
                    // Get nozzle position to determine mode (0.0 = plane, 1.0 = hover)
                    // Use direct hash since enum may not exist in all SHVDN versions
                    try
                    {
                        float nozzlePosition = Function.Call<float>(
                            _getFlightNozzlePositionHash,
                            vehicle);

                        return nozzlePosition > Constants.VTOL_HOVER_THRESHOLD
                            ? Constants.AIRCRAFT_TYPE_VTOL_HOVER
                            : Constants.AIRCRAFT_TYPE_VTOL_PLANE;
                    }
                    catch
                    {
                        // If native call fails, default to plane mode for VTOL
                        return Constants.AIRCRAFT_TYPE_VTOL_PLANE;
                    }
                }

                // Check vehicle class
                if (vehicle.ClassType == VehicleClass.Helicopters)
                    return Constants.AIRCRAFT_TYPE_HELICOPTER;

                // Default to fixed-wing for planes
                return Constants.AIRCRAFT_TYPE_FIXED_WING;
            }
            catch
            {
                // If anything fails, default to fixed-wing
                return Constants.AIRCRAFT_TYPE_FIXED_WING;
            }
        }

        /// <summary>
        /// Get pulse interval based on angle and aircraft type (hybrid pulse rate system)
        /// Different aircraft types have different sensitivity thresholds
        /// </summary>
        private long GetPulseIntervalForAngle(float absAngle, int aircraftType)
        {
            float levelThreshold, slightThreshold, moderateThreshold;

            // Select thresholds based on aircraft type
            switch (aircraftType)
            {
                case Constants.AIRCRAFT_TYPE_HELICOPTER:
                case Constants.AIRCRAFT_TYPE_VTOL_HOVER:
                    // Helicopters and VTOL in hover mode - tighter thresholds
                    levelThreshold = Constants.HELI_ANGLE_LEVEL;
                    slightThreshold = Constants.HELI_ANGLE_SLIGHT;
                    moderateThreshold = Constants.HELI_ANGLE_MODERATE;
                    break;

                case Constants.AIRCRAFT_TYPE_BLIMP:
                    // Blimps - tightest thresholds
                    levelThreshold = Constants.BLIMP_ANGLE_LEVEL;
                    slightThreshold = Constants.BLIMP_ANGLE_SLIGHT;
                    moderateThreshold = Constants.BLIMP_ANGLE_MODERATE;
                    break;

                case Constants.AIRCRAFT_TYPE_FIXED_WING:
                case Constants.AIRCRAFT_TYPE_VTOL_PLANE:
                default:
                    // Fixed-wing and VTOL in plane mode - standard thresholds
                    levelThreshold = Constants.FIXED_WING_ANGLE_LEVEL;
                    slightThreshold = Constants.FIXED_WING_ANGLE_SLIGHT;
                    moderateThreshold = Constants.FIXED_WING_ANGLE_MODERATE;
                    break;
            }

            // Determine pulse interval based on thresholds
            if (absAngle < levelThreshold)
                return Constants.AIRCRAFT_PULSE_SILENT;  // Level - no sound
            else if (absAngle < slightThreshold)
                return Constants.AIRCRAFT_PULSE_SLOW;    // Slight tilt - every 0.5s
            else if (absAngle < moderateThreshold)
                return Constants.AIRCRAFT_PULSE_MEDIUM;  // Moderate tilt - every 0.25s
            else
                return Constants.AIRCRAFT_PULSE_RAPID;   // Steep tilt - every 0.1s
        }

        /// <summary>
        /// Apply cheat settings (god mode, infinite ammo, etc.)
        /// Optimized to minimize per-frame work
        /// </summary>
        private void ApplyCheatSettings(Ped player, Vehicle currentVehicle, long ticksFromOnTick)
        {
            // Guard: Ensure player is valid
            if (player == null || !player.Exists()) return;

            try
            {
                // Cache settings lookups (dictionary access is fast but adds up)
                bool godMode = _settings.GetSetting("godMode");
                bool vehicleGodMode = _settings.GetSetting("vehicleGodMode");
                bool policeIgnore = _settings.GetSetting("policeIgnore");
                bool neverWanted = _settings.GetSetting("neverWanted");
                bool infiniteAmmo = _settings.GetSetting("infiniteAmmo");

                // God mode - only set if value differs (avoid redundant property sets)
                if (Game.Player != null && Game.Player.IsInvincible != godMode)
                {
                    Game.Player.IsInvincible = godMode;
                    player.CanBeDraggedOutOfVehicle = !godMode;
                    player.CanBeKnockedOffBike = !godMode;
                    player.CanBeShotInVehicle = !godMode;
                    player.CanFlyThroughWindscreen = !godMode;
                    player.DrownsInSinkingVehicle = !godMode;
                }

                // Vehicle god mode
                if (currentVehicle != null && currentVehicle.Exists() && player.IsInVehicle())
                {
                    try
                    {
                        if (currentVehicle.IsInvincible != vehicleGodMode)
                        {
                            currentVehicle.IsInvincible = vehicleGodMode;
                            currentVehicle.CanWheelsBreak = !vehicleGodMode;
                            currentVehicle.CanTiresBurst = !vehicleGodMode;
                            currentVehicle.CanBeVisiblyDamaged = !vehicleGodMode;
                            currentVehicle.IsBulletProof = vehicleGodMode;
                            currentVehicle.IsCollisionProof = vehicleGodMode;
                            currentVehicle.IsExplosionProof = vehicleGodMode;
                            currentVehicle.IsMeleeProof = vehicleGodMode;
                            currentVehicle.IsFireProof = vehicleGodMode;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "Vehicle god mode");
                    }
                }
                else if (vehicleGodMode && !player.IsInVehicle())
                {
                    // Disable god mode when exiting vehicle
                    try
                    {
                        Vehicle lastVehicle = player.LastVehicle;
                        if (lastVehicle != null && lastVehicle.Exists() && lastVehicle.IsInvincible)
                        {
                            lastVehicle.IsInvincible = false;
                            lastVehicle.CanWheelsBreak = true;
                            lastVehicle.CanTiresBurst = true;
                            lastVehicle.CanBeVisiblyDamaged = true;
                            lastVehicle.IsBulletProof = false;
                            lastVehicle.IsCollisionProof = false;
                            lastVehicle.IsExplosionProof = false;
                            lastVehicle.IsMeleeProof = false;
                            lastVehicle.IsFireProof = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "Last vehicle god mode disable");
                    }
                }

                // Police settings - only set if changed (use cached state since property is write-only)
                if (_cachedPoliceIgnore != policeIgnore)
                {
                    _cachedPoliceIgnore = policeIgnore;
                    if (Game.Player != null)
                        Game.Player.IgnoredByPolice = policeIgnore;
                }

                if (neverWanted && Game.Player != null && Game.Player.WantedLevel > 0)
                    Game.Player.WantedLevel = 0;

                // Weapon settings - use cached state since InfiniteAmmo is write-only
                if (infiniteAmmo != _cachedInfiniteAmmo)
                {
                    _cachedInfiniteAmmo = infiniteAmmo;
                    if (infiniteAmmo)
                    {
                        try
                        {
                            Weapon currentWeapon = player.Weapons?.Current;
                            if (currentWeapon != null)
                            {
                                currentWeapon.InfiniteAmmo = true;
                                currentWeapon.InfiniteAmmoClip = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Exception(ex, "Infinite ammo setting");
                        }
                    }
                }

                // PERFORMANCE: Refresh cached per-frame settings periodically (every 500ms)
                // These settings rarely change, so avoid dictionary lookups every frame
                long currentTick = ticksFromOnTick / TimeSpan.TicksPerMillisecond;
                if (currentTick - _lastCheatSettingsRefreshTick > 500)
                {
                    _lastCheatSettingsRefreshTick = currentTick;
                    _cachedExplosiveAmmo = _settings.GetSetting("explosiveAmmo");
                    _cachedFireAmmo = _settings.GetSetting("fireAmmo");
                    _cachedExplosiveMelee = _settings.GetSetting("explosiveMelee");
                    _cachedSuperJump = _settings.GetSetting("superJump");
                    _cachedRunFaster = _settings.GetSetting("runFaster");
                    _cachedSwimFaster = _settings.GetSetting("swimFaster");
                    _cachedEnableMPMaps = _settings.GetSetting("enableMPMaps");

                    // Apply GTA Online MP Maps setting when it changes
                    if (_cachedEnableMPMaps != _mpMapsCurrentlyEnabled)
                    {
                        ApplyMPMapsSettings(_cachedEnableMPMaps);
                        _mpMapsCurrentlyEnabled = _cachedEnableMPMaps;
                    }
                }

                // Per-frame cheat settings (must be called each frame when active)
                // Uses cached values to avoid dictionary lookups every frame
                if (Game.Player != null)
                {
                    if (_cachedExplosiveAmmo)
                        Game.Player.SetExplosiveAmmoThisFrame();
                    if (_cachedFireAmmo)
                        Game.Player.SetFireAmmoThisFrame();
                    if (_cachedExplosiveMelee)
                        Game.Player.SetExplosiveMeleeThisFrame();
                    if (_cachedSuperJump)
                        Game.Player.SetSuperJumpThisFrame();
                    if (_cachedRunFaster)
                        Game.Player.SetRunSpeedMultThisFrame(2f);
                    if (_cachedSwimFaster)
                        Game.Player.SetSwimSpeedMultThisFrame(2f);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ApplyCheatSettings");
            }
        }

        // PERFORMANCE: Pre-cached Hash for native calls
        private static readonly Hash _getFlightNozzlePositionHash = (Hash)Constants.NATIVE_GET_VEHICLE_FLIGHT_NOZZLE_POSITION;

        // PERFORMANCE: Pre-cached Hash for MP map natives
        private static readonly Hash _onEnterMPHash = (Hash)Constants.NATIVE_ON_ENTER_MP;
        private static readonly Hash _onEnterSPHash = (Hash)Constants.NATIVE_ON_ENTER_SP;
        private static readonly Hash _setInstancePriorityModeHash = (Hash)Constants.NATIVE_SET_INSTANCE_PRIORITY_MODE;

        /// <summary>
        /// Apply GTA Online MP Maps settings.
        /// Enables/disables multiplayer map content (interiors, DLC locations) in single player.
        /// </summary>
        private void ApplyMPMapsSettings(bool enable)
        {
            try
            {
                if (enable)
                {
                    // Enable MP maps - this activates GTA Online map content in single player
                    // ON_ENTER_MP native tells the game to load multiplayer map content
                    Function.Call(_onEnterMPHash);

                    // Set instance priority mode for better MP content loading
                    Function.Call(_setInstancePriorityModeHash, 1);

                    Logger.Info("GTA Online maps and interiors enabled");
                }
                else
                {
                    // Disable MP maps - return to standard single player map
                    // ON_ENTER_SP native returns to normal single player map state
                    Function.Call(_onEnterSPHash);

                    // Reset instance priority mode
                    Function.Call(_setInstancePriorityModeHash, 0);

                    Logger.Info("GTA Online maps and interiors disabled");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ApplyMPMapsSettings");
            }
        }

        /// <summary>
        /// Update heading announcement when player turns
        /// </summary>
        private void UpdateHeadingAnnouncement(double heading)
        {
            int currentSlice = SpatialCalculator.GetHeadingSlice(heading);

            if (!_headingSlices[currentSlice])
            {
                _headingSlices[currentSlice] = true;

                // Reset other slices
                for (int i = 0; i < _headingSlices.Length; i++)
                {
                    if (i != currentSlice)
                        _headingSlices[i] = false;
                }

                if (_settings.GetSetting("announceHeadings"))
                {
                    _audio.Speak(SpatialCalculator.GetHeadingSliceName(currentSlice), true);
                }
            }
        }

        /// <summary>
        /// Update time announcement (every 3 hours)
        /// </summary>
        private void UpdateTimeAnnouncement()
        {
            TimeSpan time = World.CurrentTimeOfDay;

            if (time.Minutes == 0)
            {
                if ((time.Hours == 0 || time.Hours == 3 || time.Hours == 6 || time.Hours == 9 ||
                     time.Hours == 12 || time.Hours == 15 || time.Hours == 18 ||
                     time.Hours == 21) && !_timeAnnounced)
                {
                    _timeAnnounced = true;

                    if (_settings.GetSetting("announceTime"))
                    {
                        _audio.Speak($"The time is now: {time.Hours}:00");
                    }
                }
            }
            else
            {
                _timeAnnounced = false;
            }
        }

        /// <summary>
        /// Update street announcement when changed
        /// </summary>
        private void UpdateStreetAnnouncement(Vector3 position)
        {
            string street = World.GetStreetName(position);

            if (street != _currentStreet)
            {
                _currentStreet = street;

                if (_settings.GetSetting("announceZones"))
                {
                    _audio.Speak(street);
                }
            }
        }

        /// <summary>
        /// Update zone announcement when changed
        /// </summary>
        private void UpdateZoneAnnouncement(Vector3 position)
        {
            string zone = World.GetZoneLocalizedName(position);

            if (zone != _currentZone)
            {
                _currentZone = zone;

                if (_settings.GetSetting("announceZones"))
                {
                    _audio.Speak(zone);
                }
            }
        }

        /// <summary>
        /// Play targeting sound based on entity type
        /// </summary>
        private void PlayTargetingSound(Entity target)
        {
            if (target == null || !target.Exists() || target.IsDead) return;

            switch (target.EntityType)
            {
                case EntityType.Ped:
                    _audio.PlayPedTargetSound();
                    break;

                case EntityType.Vehicle:
                    _audio.PlayVehicleTargetSound();
                    break;

                case EntityType.Prop:
                    if (!target.IsExplosionProof || !target.IsBulletProof)
                    {
                        _audio.PlayPropTargetSound();
                    }
                    break;
            }
        }

        /// <summary>
        /// Play a frontend sound with proper sound ID lifecycle.
        /// GTA.Audio.PlaySoundFrontend() leaks sound IDs (calls GET_SOUND_ID
        /// but never RELEASE_SOUND_ID), exhausting the ~100 ID pool after
        /// enough menu navigations. This method gets, plays, and immediately releases.
        /// </summary>
        private static void PlayFrontendSound(string soundName, string soundSet)
        {
            int id = -1;
            try
            {
                id = Function.Call<int>(Hash.GET_SOUND_ID);
                Function.Call(Hash.PLAY_SOUND_FRONTEND, id, soundName, soundSet, false);
            }
            catch { }
            finally
            {
                if (id >= 0)
                {
                    try { Function.Call(Hash.RELEASE_SOUND_ID, id); } catch { }
                }
            }
        }

        /// <summary>
        /// Key down event handler
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Track Control key
                if (e.Control)
                {
                    _controlHeld = true;
                }

                // Bounds check for key states array
                int keyIndex = GetIndexForKey(e.KeyCode);

                // Toggle accessibility keys (Ctrl+NumPad2)
                if (e.KeyCode == Keys.NumPad2 && _controlHeld && keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex])
                {
                    _keyStates[keyIndex] = true;
                    _keysDisabled = !_keysDisabled;
                    _audio.Speak(_keysDisabled ? "Accessibility keys deactivated" : "Accessibility keys activated");
                    return;
                }

                if (_keysDisabled) return;

                // Handle key presses
                switch (e.KeyCode)
                {
                    case Keys.NumPad0 when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        HandleNumPad0(e.Control);
                        break;

                    case Keys.NumPad1 when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        PlayFrontendSound("NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                        _menu.NavigatePreviousItem(_controlHeld);
                        _audio.Speak(_menu.GetCurrentItemText());
                        break;

                    case Keys.NumPad2 when !_controlHeld && keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        PlayFrontendSound("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                        _menu.ExecuteSelection();
                        break;

                    case Keys.NumPad3 when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        PlayFrontendSound("NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                        _menu.NavigateNextItem(_controlHeld);
                        _audio.Speak(_menu.GetCurrentItemText());
                        break;

                    case Keys.NumPad4 when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        if (_controlHeld)
                        {
                            // Ctrl+NumPad4: Health and armor status
                            Ped p4 = Game.Player?.Character;
                            if (p4 != null && p4.Exists()) _healthArmor.AnnounceStatus(p4);
                        }
                        else
                        {
                            HandleNearbyVehicles();
                        }
                        break;

                    case Keys.NumPad5 when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        if (_controlHeld)
                        {
                            // Ctrl+NumPad5: Repeat last announcement
                            _audio.RepeatLast();
                        }
                        else
                        {
                            HandleNearbyDoors();
                        }
                        break;

                    case Keys.NumPad6 when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        if (_controlHeld)
                        {
                            // Ctrl+NumPad6: Nearest enemy
                            Ped p6 = Game.Player?.Character;
                            if (p6 != null && p6.Exists()) _combat.AnnounceNearestEnemy(p6, p6.Position);
                        }
                        else
                        {
                            HandleNearbyPedestrians();
                        }
                        break;

                    case Keys.NumPad7 when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        if (_controlHeld)
                        {
                            // Ctrl+NumPad7: Nearby points of interest
                            Ped p7 = Game.Player?.Character;
                            if (p7 != null && p7.Exists()) _blips.AnnounceNearbyBlips(p7.Position);
                        }
                        else
                        {
                            PlayFrontendSound("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                            _menu.NavigatePreviousMenu();
                            _audio.Speak(_menu.GetCurrentMenuDescription(), true);
                        }
                        break;

                    case Keys.NumPad8 when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        if (_controlHeld)
                        {
                            // Ctrl+NumPad8: Ammo count
                            Ped p8 = Game.Player?.Character;
                            if (p8 != null && p8.Exists()) _combat.AnnounceAmmo(p8);
                        }
                        else
                        {
                            HandleNearbyObjects();
                        }
                        break;

                    case Keys.NumPad9 when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        if (_controlHeld)
                        {
                            // Ctrl+NumPad9: Mission objective location
                            Ped p9 = Game.Player?.Character;
                            if (p9 != null && p9.Exists()) _blips.AnnounceMissionBlip(p9.Position);
                        }
                        else
                        {
                            PlayFrontendSound("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                            _menu.NavigateNextMenu();
                            _audio.Speak(_menu.GetCurrentMenuDescription(), true);
                        }
                        break;

                    case Keys.Decimal when keyIndex >= 0 && keyIndex < _keyStates.Length && !_keyStates[keyIndex]:
                        _keyStates[keyIndex] = true;
                        // Back/Exit submenu
                        if (_menu.HasActiveSubmenu())
                        {
                            PlayFrontendSound("BACK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                            _menu.ExitSubmenu();
                            _audio.Speak(_menu.GetCurrentMenuDescription());
                        }
                        else if (_controlHeld)
                        {
                            // Ctrl+Decimal: Time with minutes (moved from Ctrl+NumPad0)
                            TimeSpan time = World.CurrentTimeOfDay;
                            string minuteStr = time.Minutes < 10 ? $"0{time.Minutes}" : time.Minutes.ToString();
                            _audio.Speak($"The time is: {time.Hours}:{minuteStr}");
                        }
                        else
                        {
                            // Show heading (moved from Decimal alone - now requires no submenu active)
                            Ped player = Game.Player?.Character;
                            if (player != null && player.Exists())
                            {
                                string direction = SpatialCalculator.GetDirectionFromHeading(player.Heading);
                                _audio.Speak($"facing {direction}");
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "OnKeyDown");
            }
        }

        /// <summary>
        /// Get array index for a key code
        /// </summary>
        private int GetIndexForKey(Keys key)
        {
            switch (key)
            {
                case Keys.NumPad0: return 0;
                case Keys.NumPad1: return 1;
                case Keys.NumPad2: return 2;
                case Keys.NumPad3: return 3;
                case Keys.NumPad4: return 4;
                case Keys.NumPad5: return 5;
                case Keys.NumPad6: return 6;
                case Keys.NumPad7: return 7;
                case Keys.NumPad8: return 8;
                case Keys.NumPad9: return 9;
                case Keys.Decimal: return 10;
                default: return -1;
            }
        }

        /// <summary>
        /// Key up event handler
        /// </summary>
        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                if (!e.Control || e.KeyCode == Keys.ControlKey)
                {
                    _controlHeld = false;
                }

                // Reset key states with bounds checking
                int keyIndex = GetIndexForKey(e.KeyCode);
                if (keyIndex >= 0 && keyIndex < _keyStates.Length)
                {
                    _keyStates[keyIndex] = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "OnKeyUp");
            }
        }

        /// <summary>
        /// Cleanup when script is unloaded - prevents resource leaks
        /// </summary>
        private void OnAborted(object sender, EventArgs e)
        {
            Logger.Info("GTA11Y script aborting, cleaning up resources...");

            try
            {
                // CRITICAL: Unsubscribe all event handlers to prevent accumulation on script reload.
                // Without this, every F2 reload adds duplicate handlers causing exponential slowdown.
                Tick -= OnTick;
                KeyDown -= OnKeyDown;
                KeyUp -= OnKeyUp;
                Aborted -= OnAborted;

                _menu?.Dispose();
                _audio?.Dispose();
                Logger.Info("Cleanup complete");

                // Shutdown logger last - flushes pending logs and stops background writer thread
                Logger.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "OnAborted cleanup");
            }
        }

        /// <summary>
        /// Handle NumPad0 (location/time info)
        /// Ctrl+NumPad0 = heading (moved from Decimal)
        /// </summary>
        private void HandleNumPad0(bool controlHeld)
        {
            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                {
                    _audio.Speak("Location unavailable");
                    return;
                }

                if (controlHeld)
                {
                    // Ctrl+NumPad0: Toggle pedestrian navigation to waypoint
                    if (_menu.IsPedestrianNavigationActive)
                    {
                        _menu.StopPedestrianNavigation();
                    }
                    else
                    {
                        _menu.StartPedestrianNavigation();
                    }
                }
                else
                {
                    Vector3 pos = player.Position;
                    Vehicle vehicle = player.IsInVehicle() ? player.CurrentVehicle : null;

                    if (vehicle == null || !vehicle.Exists())
                    {
                        _audio.Speak($"Current location: {World.GetStreetName(pos)}, {World.GetZoneLocalizedName(pos)}");
                    }
                    else
                    {
                        string vehicleName = "vehicle";
                        try { vehicleName = vehicle.LocalizedName ?? vehicle.DisplayName; } catch { }
                        _audio.Speak($"Current location: Inside of a {vehicleName} at {World.GetStreetName(pos)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HandleNumPad0");
                _audio.Speak("Location unavailable");
            }
        }

        /// <summary>
        /// Handle NumPad4 (nearby vehicles)
        /// </summary>
        private void HandleNearbyVehicles()
        {
            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                {
                    _audio.Speak("Cannot scan - player not available");
                    return;
                }

                bool onScreenOnly = _settings.GetSetting("onscreen");
                Vehicle currentVehicle = player.IsInVehicle() ? player.CurrentVehicle : null;
                if (currentVehicle != null && !currentVehicle.Exists())
                    currentVehicle = null;

                string result = _scanner.ScanNearbyVehicles(
                    player.Position,
                    currentVehicle,
                    onScreenOnly
                );
                _audio.Speak(result);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HandleNearbyVehicles");
                _audio.Speak("Error scanning vehicles");
            }
        }

        /// <summary>
        /// Handle NumPad5 (nearby doors)
        /// </summary>
        private void HandleNearbyDoors()
        {
            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                {
                    _audio.Speak("Cannot scan - player not available");
                    return;
                }

                bool onScreenOnly = _settings.GetSetting("onscreen");
                string result = _scanner.ScanNearbyDoors(
                    player.Position,
                    player,
                    onScreenOnly
                );
                _audio.Speak(result);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HandleNearbyDoors");
                _audio.Speak("Error scanning doors");
            }
        }

        /// <summary>
        /// Handle NumPad6 (nearby pedestrians)
        /// </summary>
        private void HandleNearbyPedestrians()
        {
            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                {
                    _audio.Speak("Cannot scan - player not available");
                    return;
                }

                bool onScreenOnly = _settings.GetSetting("onscreen");
                string result = _scanner.ScanNearbyPedestrians(
                    player.Position,
                    onScreenOnly
                );
                _audio.Speak(result);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HandleNearbyPedestrians");
                _audio.Speak("Error scanning pedestrians");
            }
        }

        /// <summary>
        /// Handle NumPad8 (nearby objects)
        /// </summary>
        private void HandleNearbyObjects()
        {
            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                {
                    _audio.Speak("Cannot scan - player not available");
                    return;
                }

                bool onScreenOnly = _settings.GetSetting("onscreen");
                string result = _scanner.ScanNearbyObjects(
                    player.Position,
                    player,
                    onScreenOnly
                );
                _audio.Speak(result);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HandleNearbyObjects");
                _audio.Speak("Error scanning objects");
            }
        }

    }
}
