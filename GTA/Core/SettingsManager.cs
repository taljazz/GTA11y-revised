using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages mod settings with JSON persistence
    /// Optimized with proper boolean types and caching
    /// Supports both boolean (toggle) and int (cycle) settings
    /// </summary>
    public class SettingsManager
    {
        private Dictionary<string, bool> _settings;
        private Dictionary<string, int> _intSettings;
        private readonly string _settingsFilePath;

        // Public accessors for logging purposes (read-only)
        public Dictionary<string, bool> AllBoolSettings => _settings;
        public Dictionary<string, int> AllIntSettings => _intSettings;

        // Boolean setting definitions with default values
        private static readonly Dictionary<string, bool> DefaultSettings = new Dictionary<string, bool>
        {
            // === GENERAL ANNOUNCEMENTS ===
            { "announceHeadings", true },
            { "announceZones", true },
            { "announceTime", true },

            // === AUTODRIVE ANNOUNCEMENTS (reduce spam by grouping) ===
            { "roadFeatureAnnouncements", true },        // Curves, intersections
            { "announceRoadType", true },                // Road type changes during AutoDrive
            { "announceTrafficAwareness", true },        // Lane changes, overtaking
            { "announceCollisionWarnings", true },       // Collision warnings, following distance
            { "announceEmergencyVehicles", true },       // Emergency vehicle warnings
            { "announceWeather", true },                 // Weather changes affecting driving
            { "announceStructures", true },              // Tunnels, bridges, hills, U-turns
            { "announceTrafficLights", true },           // Traffic light warnings
            { "announceNavigation", true },              // ETA, distance milestones, arrival

            // === AUDIO INDICATORS ===
            { "targetPitchIndicator", true },
            { "aircraftAttitude", false },

            // === VEHICLE & SPAWN OPTIONS ===
            { "radioOff", false },
            { "warpInsideVehicle", false },
            { "onscreen", false },
            { "speed", false },

            // === CHEAT MODES ===
            { "godMode", false },
            { "policeIgnore", false },
            { "vehicleGodMode", false },
            { "infiniteAmmo", false },
            { "neverWanted", false },
            { "superJump", false },
            { "runFaster", false },
            { "swimFaster", false },
            { "explosiveAmmo", false },
            { "fireAmmo", false },
            { "explosiveMelee", false },

            // === GTA ONLINE FEATURES ===
            { "enableMPMaps", false }  // Enable GTA Online maps/interiors in single player
        };

        // Int setting definitions with default values and max values
        // Format: { "settingId", defaultValue } - max values defined separately
        private static readonly Dictionary<string, int> DefaultIntSettings = new Dictionary<string, int>
        {
            { "altitudeMode", 1 },  // 0=Off, 1=Normal (tone), 2=Aircraft (spoken)
            { "turretCrewAnnouncements", 3 }  // 0=Off, 1=Firing only, 2=Enemy approaching only, 3=Both
        };

        // Max values for int settings (for cycling)
        private static readonly Dictionary<string, int> IntSettingMaxValues = new Dictionary<string, int>
        {
            { "altitudeMode", 2 },  // Cycles 0 -> 1 -> 2 -> 0
            { "turretCrewAnnouncements", 3 }  // Cycles 0 -> 1 -> 2 -> 3 -> 0
        };

        // Display names for int setting values
        private static readonly Dictionary<string, string[]> IntSettingValueNames = new Dictionary<string, string[]>
        {
            { "altitudeMode", new[] { "Off", "Normal (Tone)", "Aircraft (Spoken)" } },
            { "turretCrewAnnouncements", new[] { "Off", "Firing Only", "Enemy Approaching Only", "Both" } }
        };

        // Display names for settings
        private static readonly Dictionary<string, string> SettingDisplayNames = new Dictionary<string, string>
        {
            // === GENERAL ANNOUNCEMENTS ===
            { "announceTime", "Time of Day Announcements" },
            { "announceHeadings", "Heading Change Announcements" },
            { "announceZones", "Street and Zone Change Announcements" },

            // === AUTODRIVE ANNOUNCEMENTS ===
            { "roadFeatureAnnouncements", "AutoDrive: Announce Curves and Intersections" },
            { "announceRoadType", "AutoDrive: Announce Road Type Changes" },
            { "announceTrafficAwareness", "AutoDrive: Announce Lane Changes and Overtaking" },
            { "announceCollisionWarnings", "AutoDrive: Announce Collision Warnings" },
            { "announceEmergencyVehicles", "AutoDrive: Announce Emergency Vehicles" },
            { "announceWeather", "AutoDrive: Announce Weather Changes" },
            { "announceStructures", "AutoDrive: Announce Tunnels, Bridges, Hills" },
            { "announceTrafficLights", "AutoDrive: Announce Traffic Lights" },
            { "announceNavigation", "AutoDrive: Announce ETA and Arrival" },

            // === AUDIO INDICATORS ===
            { "altitudeMode", "Altitude Indicator Mode" },
            { "turretCrewAnnouncements", "Turret Crew Announcements" },
            { "targetPitchIndicator", "Audible Targeting Pitch Indicator" },
            { "aircraftAttitude", "Aircraft Attitude Indicator (Pitch/Roll)" },

            // === VEHICLE & SPAWN OPTIONS ===
            { "radioOff", "Always Disable Vehicle Radios" },
            { "warpInsideVehicle", "Teleport Inside Newly Spawned Vehicles" },
            { "onscreen", "Announce Only Visible Nearby Items" },
            { "speed", "Announce Current Vehicle Speed" },

            // === CHEAT MODES ===
            { "godMode", "God Mode" },
            { "policeIgnore", "Police Ignore Player" },
            { "vehicleGodMode", "Vehicle God Mode (Indestructible)" },
            { "infiniteAmmo", "Infinite Ammo" },
            { "neverWanted", "Wanted Level Never Increases" },
            { "superJump", "Super Jump" },
            { "runFaster", "Run Faster" },
            { "swimFaster", "Swim Faster" },
            { "explosiveAmmo", "Explosive Ammo" },
            { "fireAmmo", "Fire Ammo" },
            { "explosiveMelee", "Explosive Melee Attacks" },

            // === GTA ONLINE FEATURES ===
            { "enableMPMaps", "Enable GTA Online Maps and Interiors" }
        };

        public SettingsManager()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string settingsFolder = Path.Combine(documentsPath, "Rockstar Games", "GTA V", "ModSettings");
            _settingsFilePath = Path.Combine(settingsFolder, Constants.SETTINGS_FILE_NAME);

            LoadSettings();
        }

        /// <summary>
        /// Get a setting value (returns false if not found or on error)
        /// </summary>
        public bool GetSetting(string id)
        {
            if (string.IsNullOrEmpty(id) || _settings == null)
                return false;

            try
            {
                return _settings.TryGetValue(id, out bool value) && value;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Set a setting value
        /// </summary>
        public void SetSetting(string id, bool value)
        {
            if (string.IsNullOrEmpty(id) || _settings == null)
                return;

            try
            {
                _settings[id] = value;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, $"SetSetting({id})");
            }
        }

        /// <summary>
        /// Toggle a setting and return the new value
        /// </summary>
        public bool ToggleSetting(string id)
        {
            if (string.IsNullOrEmpty(id) || _settings == null)
                return false;

            try
            {
                if (_settings.TryGetValue(id, out bool current))
                {
                    bool newValue = !current;
                    _settings[id] = newValue;
                    return newValue;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, $"ToggleSetting({id})");
            }
            return false;
        }

        /// <summary>
        /// Get an int setting value (returns 0 if not found or on error)
        /// </summary>
        public int GetIntSetting(string id)
        {
            if (string.IsNullOrEmpty(id) || _intSettings == null)
                return 0;

            try
            {
                return _intSettings.TryGetValue(id, out int value) ? value : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get an int setting value with bounds validation
        /// </summary>
        public int GetIntSetting(string id, int minValue, int maxValue)
        {
            int value = GetIntSetting(id);
            return Math.Max(minValue, Math.Min(maxValue, value));
        }

        /// <summary>
        /// Set an int setting value
        /// </summary>
        public void SetIntSetting(string id, int value)
        {
            if (string.IsNullOrEmpty(id) || _intSettings == null)
                return;

            try
            {
                _intSettings[id] = value;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, $"SetIntSetting({id})");
            }
        }

        /// <summary>
        /// Cycle an int setting to the next value and return the new value
        /// </summary>
        public int CycleIntSetting(string id)
        {
            if (string.IsNullOrEmpty(id) || _intSettings == null)
                return 0;

            try
            {
                if (_intSettings.ContainsKey(id) && IntSettingMaxValues.TryGetValue(id, out int maxValue))
                {
                    int current = _intSettings[id];
                    int next = (current + 1) % (maxValue + 1);
                    _intSettings[id] = next;
                    return next;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, $"CycleIntSetting({id})");
            }
            return 0;
        }

        /// <summary>
        /// Get the display name for the current value of an int setting
        /// </summary>
        public string GetIntSettingValueName(string id)
        {
            if (string.IsNullOrEmpty(id) || _intSettings == null)
                return "Unknown";

            try
            {
                if (_intSettings.TryGetValue(id, out int value) &&
                    IntSettingValueNames.TryGetValue(id, out string[] names) &&
                    names != null && value >= 0 && value < names.Length)
                {
                    return names[value] ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, $"GetIntSettingValueName({id})");
            }
            return "Unknown";
        }

        /// <summary>
        /// Check if a setting ID is an int setting
        /// </summary>
        public bool IsIntSetting(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            try
            {
                return DefaultIntSettings.ContainsKey(id);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get display name for a setting
        /// </summary>
        public string GetDisplayName(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "Unknown";

            try
            {
                return SettingDisplayNames.TryGetValue(id, out string name) ? (name ?? id) : id;
            }
            catch
            {
                return id;
            }
        }

        /// <summary>
        /// Get all setting IDs for menu display (both bool and int settings)
        /// </summary>
        public List<string> GetAllSettingIds()
        {
            var allIds = new List<string>();

            try
            {
                if (_settings != null)
                    allIds.AddRange(_settings.Keys);
                if (_intSettings != null)
                    allIds.AddRange(_intSettings.Keys);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "GetAllSettingIds");
            }

            return allIds;
        }

        /// <summary>
        /// Save settings to JSON file
        /// </summary>
        public void SaveSettings()
        {
            if (string.IsNullOrEmpty(_settingsFilePath))
            {
                Logger.Warning("SaveSettings: No settings file path configured");
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Combine both settings types into a single object for serialization
                var allSettings = new Dictionary<string, object>();

                if (_settings != null)
                {
                    foreach (var kvp in _settings)
                    {
                        if (!string.IsNullOrEmpty(kvp.Key))
                            allSettings[kvp.Key] = kvp.Value;
                    }
                }

                if (_intSettings != null)
                {
                    foreach (var kvp in _intSettings)
                    {
                        if (!string.IsNullOrEmpty(kvp.Key))
                            allSettings[kvp.Key] = kvp.Value;
                    }
                }

                string json = JsonConvert.SerializeObject(allSettings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
                Logger.Debug("Settings saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "SaveSettings");
            }
        }

        /// <summary>
        /// Load settings from JSON file or create defaults
        /// </summary>
        private void LoadSettings()
        {
            // Always start with defaults
            _settings = new Dictionary<string, bool>(DefaultSettings);
            _intSettings = new Dictionary<string, int>(DefaultIntSettings);

            if (string.IsNullOrEmpty(_settingsFilePath))
            {
                Logger.Warning("LoadSettings: No settings file path configured");
                return;
            }

            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        Logger.Warning("LoadSettings: Settings file is empty");
                        SaveSettings();
                        return;
                    }

                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                    if (loaded != null)
                    {
                        // Load boolean settings
                        foreach (var kvp in DefaultSettings)
                        {
                            try
                            {
                                if (loaded.TryGetValue(kvp.Key, out object val) && val != null)
                                {
                                    if (val is bool boolVal)
                                        _settings[kvp.Key] = boolVal;
                                    else if (bool.TryParse(val.ToString(), out bool parsed))
                                        _settings[kvp.Key] = parsed;
                                }
                            }
                            catch
                            {
                                // Keep default value if parsing fails
                            }
                        }

                        // Load int settings
                        foreach (var kvp in DefaultIntSettings)
                        {
                            try
                            {
                                if (loaded.TryGetValue(kvp.Key, out object val) && val != null)
                                {
                                    if (val is long longVal)
                                        _intSettings[kvp.Key] = (int)longVal;
                                    else if (val is int intVal)
                                        _intSettings[kvp.Key] = intVal;
                                    else if (int.TryParse(val.ToString(), out int parsed))
                                        _intSettings[kvp.Key] = parsed;
                                }
                            }
                            catch
                            {
                                // Keep default value if parsing fails
                            }
                        }

                        // Handle migration from old altitudeIndicator (bool) to altitudeMode (int)
                        try
                        {
                            if (loaded.TryGetValue("altitudeIndicator", out object oldAlt) && oldAlt != null)
                            {
                                bool wasEnabled = false;
                                if (oldAlt is bool b) wasEnabled = b;
                                else if (bool.TryParse(oldAlt.ToString(), out bool parsed)) wasEnabled = parsed;

                                // If old setting exists and we haven't explicitly set altitudeMode
                                if (!loaded.ContainsKey("altitudeMode"))
                                {
                                    _intSettings["altitudeMode"] = wasEnabled ? 1 : 0;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore migration errors
                        }
                    }
                    Logger.Debug("Settings loaded successfully");
                }
                else
                {
                    Logger.Info("Settings file not found, creating defaults");
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "LoadSettings");

                // If loading fails, reset to defaults
                _settings = new Dictionary<string, bool>(DefaultSettings);
                _intSettings = new Dictionary<string, int>(DefaultIntSettings);

                // Try to delete corrupted file
                try
                {
                    if (File.Exists(_settingsFilePath))
                    {
                        File.Delete(_settingsFilePath);
                        Logger.Info("Deleted corrupted settings file");
                    }
                }
                catch (Exception deleteEx)
                {
                    Logger.Exception(deleteEx, "Deleting corrupted settings file");
                }

                SaveSettings();
            }
        }
    }
}
