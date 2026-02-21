using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using GTA;
using GTA.Native;
using Newtonsoft.Json;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages saving and loading vehicle configurations to JSON
    /// Supports 10 fixed slots
    /// </summary>
    public class VehicleSaveManager
    {
        private readonly string _savePath;
        private SavedVehicle[] _slots;

        public VehicleSaveManager()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string modSettingsPath = Path.Combine(documentsPath, Constants.SETTINGS_FOLDER_PATH.TrimStart('/'));

            // Ensure directory exists
            if (!Directory.Exists(modSettingsPath))
            {
                Directory.CreateDirectory(modSettingsPath);
            }

            _savePath = Path.Combine(modSettingsPath, Constants.SAVED_VEHICLES_FILE_NAME);
            _slots = new SavedVehicle[Constants.VEHICLE_SAVE_SLOT_COUNT];

            LoadSlots();
        }

        /// <summary>
        /// Load slots from file
        /// </summary>
        private void LoadSlots()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    string json = File.ReadAllText(_savePath);
                    SavedVehicle[] loadedSlots = JsonConvert.DeserializeObject<SavedVehicle[]>(json);

                    // Handle null or wrong-sized arrays by preserving valid data
                    if (loadedSlots != null)
                    {
                        // Copy loaded data into correctly-sized array
                        int copyCount = Math.Min(loadedSlots.Length, Constants.VEHICLE_SAVE_SLOT_COUNT);
                        for (int i = 0; i < copyCount; i++)
                        {
                            _slots[i] = loadedSlots[i];
                        }
                        Logger.Info($"Loaded {copyCount} saved vehicle slots");
                    }
                }
            }
            catch (JsonException ex)
            {
                // JSON corruption - log and start fresh but don't delete file yet
                Logger.Error($"Corrupted saved vehicles file: {ex.Message}");
                // _slots already initialized to empty array in constructor
            }
            catch (IOException ex)
            {
                // File access issue - could be locked or permissions
                Logger.Error($"Cannot read saved vehicles file: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load saved vehicles: {ex.Message}");
            }
        }

        /// <summary>
        /// Save slots to file
        /// </summary>
        private void SaveSlots()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_slots, Formatting.Indented);
                File.WriteAllText(_savePath, json);
            }
            catch (IOException ex)
            {
                // File locked or permissions issue
                Logger.Error($"Cannot write saved vehicles file (may be locked): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error($"Permission denied writing saved vehicles: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save vehicles: {ex.Message}");
            }
        }

        /// <summary>
        /// Get saved vehicle at slot (0-9)
        /// Returns null if slot is empty
        /// </summary>
        public SavedVehicle GetSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Constants.VEHICLE_SAVE_SLOT_COUNT)
                return null;
            return _slots[slotIndex];
        }

        /// <summary>
        /// Get description for slot (for menu display)
        /// </summary>
        public string GetSlotDescription(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Constants.VEHICLE_SAVE_SLOT_COUNT)
                return $"Slot {slotIndex + 1}: Invalid";

            SavedVehicle saved = _slots[slotIndex];
            if (saved == null || string.IsNullOrEmpty(saved.DisplayName))
                return $"Slot {slotIndex + 1}: Empty";

            return $"Slot {slotIndex + 1}: {saved.GetSummary()}";
        }

        /// <summary>
        /// Check if slot has a saved vehicle
        /// </summary>
        public bool IsSlotOccupied(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Constants.VEHICLE_SAVE_SLOT_COUNT)
                return false;
            return _slots[slotIndex] != null && !string.IsNullOrEmpty(_slots[slotIndex].DisplayName);
        }

        /// <summary>
        /// Save current vehicle to slot
        /// </summary>
        public bool SaveVehicleToSlot(Vehicle vehicle, int slotIndex)
        {
            if (vehicle == null || slotIndex < 0 || slotIndex >= Constants.VEHICLE_SAVE_SLOT_COUNT)
                return false;

            try
            {
                SavedVehicle saved = new SavedVehicle
                {
                    DisplayName = vehicle.LocalizedName ?? vehicle.DisplayName,
                    ModelHash = vehicle.Model.Hash
                };

                // Save all mods
                vehicle.Mods.InstallModKit();
                for (int modType = 0; modType < 50; modType++)
                {
                    try
                    {
                        int modIndex = Function.Call<int>(Hash.GET_VEHICLE_MOD, vehicle, modType);
                        if (modIndex >= 0)
                        {
                            saved.Mods[modType] = modIndex;
                        }
                    }
                    catch { /* Some mod types not supported by vehicle - expected */ }
                }

                // Save colors
                saved.PrimaryColor = (int)vehicle.Mods.PrimaryColor;
                saved.SecondaryColor = (int)vehicle.Mods.SecondaryColor;
                saved.PearlescentColor = (int)vehicle.Mods.PearlescentColor;
                saved.RimColor = (int)vehicle.Mods.RimColor;

                // Check for custom colors
                saved.HasCustomPrimaryColor = vehicle.Mods.IsPrimaryColorCustom;
                if (saved.HasCustomPrimaryColor)
                {
                    Color customPrimary = vehicle.Mods.CustomPrimaryColor;
                    saved.CustomPrimaryR = customPrimary.R;
                    saved.CustomPrimaryG = customPrimary.G;
                    saved.CustomPrimaryB = customPrimary.B;
                }

                saved.HasCustomSecondaryColor = vehicle.Mods.IsSecondaryColorCustom;
                if (saved.HasCustomSecondaryColor)
                {
                    Color customSecondary = vehicle.Mods.CustomSecondaryColor;
                    saved.CustomSecondaryR = customSecondary.R;
                    saved.CustomSecondaryG = customSecondary.G;
                    saved.CustomSecondaryB = customSecondary.B;
                }

                // Save wheel type
                saved.WheelType = (int)vehicle.Mods.WheelType;

                // Save window tint
                saved.WindowTint = (int)vehicle.Mods.WindowTint;

                // Save neons
                if (vehicle.Mods.HasNeonLights)
                {
                    saved.NeonLeft = vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Left);
                    saved.NeonRight = vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Right);
                    saved.NeonFront = vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Front);
                    saved.NeonBack = vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Back);

                    Color neonColor = vehicle.Mods.NeonLightsColor;
                    saved.NeonColorR = neonColor.R;
                    saved.NeonColorG = neonColor.G;
                    saved.NeonColorB = neonColor.B;
                }

                // Save license plate
                saved.LicensePlate = vehicle.Mods.LicensePlate;
                saved.LicensePlateStyle = (int)vehicle.Mods.LicensePlateStyle;

                // Save toggle mods
                saved.HasTurbo = vehicle.Mods[VehicleToggleModType.Turbo].IsInstalled;
                saved.HasXenon = vehicle.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled;
                saved.HasTireSmoke = vehicle.Mods[VehicleToggleModType.TireSmoke].IsInstalled;

                if (saved.HasTireSmoke)
                {
                    Color tireSmokeColor = vehicle.Mods.TireSmokeColor;
                    saved.TireSmokeColorR = tireSmokeColor.R;
                    saved.TireSmokeColorG = tireSmokeColor.G;
                    saved.TireSmokeColorB = tireSmokeColor.B;
                }

                _slots[slotIndex] = saved;
                SaveSlots();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save vehicle: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Spawn saved vehicle from slot
        /// </summary>
        public Vehicle SpawnVehicleFromSlot(int slotIndex, SettingsManager settings)
        {
            if (slotIndex < 0 || slotIndex >= Constants.VEHICLE_SAVE_SLOT_COUNT)
                return null;

            SavedVehicle saved = _slots[slotIndex];
            if (saved == null || string.IsNullOrEmpty(saved.DisplayName))
                return null;

            // Validate model hash before attempting to spawn
            if (saved.ModelHash == 0)
            {
                Logger.Error($"Invalid model hash in slot {slotIndex}");
                return null;
            }

            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists())
                {
                    Logger.Error("Cannot spawn vehicle: player not available");
                    return null;
                }

                // Spawn vehicle
                Vehicle vehicle = World.CreateVehicle(
                    (VehicleHash)saved.ModelHash,
                    player.Position + player.ForwardVector * 2.0f,
                    player.Heading + 90
                );

                if (vehicle == null)
                    return null;

                vehicle.IsPersistent = true;
                vehicle.PlaceOnGround();

                // Install mod kit first
                vehicle.Mods.InstallModKit();

                // Apply all mods
                foreach (var mod in saved.Mods)
                {
                    try
                    {
                        Function.Call(Hash.SET_VEHICLE_MOD, vehicle, mod.Key, mod.Value, false);
                    }
                    catch { /* Some saved mods may not apply to spawned vehicle variant - expected */ }
                }

                // Apply colors
                vehicle.Mods.PrimaryColor = (VehicleColor)saved.PrimaryColor;
                vehicle.Mods.SecondaryColor = (VehicleColor)saved.SecondaryColor;
                vehicle.Mods.PearlescentColor = (VehicleColor)saved.PearlescentColor;
                vehicle.Mods.RimColor = (VehicleColor)saved.RimColor;

                // Apply custom colors
                if (saved.HasCustomPrimaryColor)
                {
                    vehicle.Mods.CustomPrimaryColor = Color.FromArgb(
                        saved.CustomPrimaryR, saved.CustomPrimaryG, saved.CustomPrimaryB);
                }

                if (saved.HasCustomSecondaryColor)
                {
                    vehicle.Mods.CustomSecondaryColor = Color.FromArgb(
                        saved.CustomSecondaryR, saved.CustomSecondaryG, saved.CustomSecondaryB);
                }

                // Apply wheel type
                vehicle.Mods.WheelType = (VehicleWheelType)saved.WheelType;

                // Apply window tint
                vehicle.Mods.WindowTint = (VehicleWindowTint)saved.WindowTint;

                // Apply neons
                if (vehicle.Mods.HasNeonLights)
                {
                    vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, saved.NeonLeft);
                    vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, saved.NeonRight);
                    vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, saved.NeonFront);
                    vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, saved.NeonBack);

                    vehicle.Mods.NeonLightsColor = Color.FromArgb(
                        saved.NeonColorR, saved.NeonColorG, saved.NeonColorB);
                }

                // Apply license plate
                if (!string.IsNullOrEmpty(saved.LicensePlate))
                {
                    vehicle.Mods.LicensePlate = saved.LicensePlate;
                    vehicle.Mods.LicensePlateStyle = (LicensePlateStyle)saved.LicensePlateStyle;
                }

                // Apply toggle mods
                vehicle.Mods[VehicleToggleModType.Turbo].IsInstalled = saved.HasTurbo;
                vehicle.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = saved.HasXenon;
                vehicle.Mods[VehicleToggleModType.TireSmoke].IsInstalled = saved.HasTireSmoke;

                if (saved.HasTireSmoke)
                {
                    vehicle.Mods.TireSmokeColor = Color.FromArgb(
                        saved.TireSmokeColorR, saved.TireSmokeColorG, saved.TireSmokeColorB);
                }

                // Warp player inside if setting enabled
                if (settings.GetSetting("warpInsideVehicle"))
                {
                    player.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                }

                return vehicle;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to spawn saved vehicle: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clear a slot
        /// </summary>
        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Constants.VEHICLE_SAVE_SLOT_COUNT)
                return;

            _slots[slotIndex] = null;
            SaveSlots();
        }
    }
}
