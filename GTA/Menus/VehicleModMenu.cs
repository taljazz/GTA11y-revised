using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Native;
using DavyKager;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for modifying vehicle (tuning, appearance)
    /// Only available when player is in a vehicle
    /// </summary>
    public class VehicleModMenu : IMenuState
    {
        private readonly Vehicle _vehicle;
        private readonly SettingsManager _settings;

        // Available mod categories that have options for this vehicle
        private readonly List<ModCategory> _categories;
        private int _currentCategoryIndex;

        // Submenu state (mod selection within category)
        private bool _inSubmenu;
        private int _currentModIndex;

        /// <summary>
        /// Represents a mod category with available options
        /// </summary>
        private class ModCategory
        {
            public string Name { get; }
            public int ModType { get; }          // VehicleModType enum value
            public int ModCount { get; }          // Number of available mods
            public bool IsToggle { get; }         // True for Turbo, Xenon, etc.
            public VehicleToggleModType? ToggleType { get; }

            public ModCategory(string name, int modType, int modCount)
            {
                Name = name;
                ModType = modType;
                ModCount = modCount;
                IsToggle = false;
                ToggleType = null;
            }

            public ModCategory(string name, VehicleToggleModType toggleType)
            {
                Name = name;
                ModType = -1;
                ModCount = 2; // On/Off
                IsToggle = true;
                ToggleType = toggleType;
            }
        }

        public VehicleModMenu(Vehicle vehicle, SettingsManager settings)
        {
            _vehicle = vehicle;
            _settings = settings;
            _inSubmenu = false;
            _currentModIndex = 0;
            _categories = new List<ModCategory>();

            if (_vehicle == null)
                return;

            // Install mod kit first
            _vehicle.Mods.InstallModKit();

            // Add performance mods first (most commonly used)
            AddModCategoryIfAvailable("Engine", 11);
            AddModCategoryIfAvailable("Transmission", 13);
            AddModCategoryIfAvailable("Brakes", 12);
            AddModCategoryIfAvailable("Suspension", 15);
            AddModCategoryIfAvailable("Armor", 16);

            // Add toggle mods
            _categories.Add(new ModCategory("Turbo", VehicleToggleModType.Turbo));
            _categories.Add(new ModCategory("Xenon Headlights", VehicleToggleModType.XenonHeadlights));

            // Add appearance mods
            AddModCategoryIfAvailable("Spoiler", 0);
            AddModCategoryIfAvailable("Front Bumper", 1);
            AddModCategoryIfAvailable("Rear Bumper", 2);
            AddModCategoryIfAvailable("Side Skirt", 3);
            AddModCategoryIfAvailable("Exhaust", 4);
            AddModCategoryIfAvailable("Grille", 6);
            AddModCategoryIfAvailable("Hood", 7);
            AddModCategoryIfAvailable("Roof", 10);
            AddModCategoryIfAvailable("Left Fender/Wing", 8);
            AddModCategoryIfAvailable("Right Fender/Wing", 9);

            // Add wheels
            AddModCategoryIfAvailable("Front Wheels", 23);
            AddModCategoryIfAvailable("Rear Wheels", 24);

            // Add wheel type selection (always available for wheeled vehicles)
            VehicleClass vClass = _vehicle.ClassType;
            bool hasWheels = vClass != VehicleClass.Boats && vClass != VehicleClass.Helicopters && vClass != VehicleClass.Planes;
            if (hasWheels)
            {
                _categories.Add(new ModCategory("Wheel Type", Constants.MOD_TYPE_WHEEL_TYPE, Constants.WHEEL_TYPE_COUNT));
            }

            // Add livery if available
            AddModCategoryIfAvailable("Livery", 48);

            // Add neons if supported
            if (_vehicle.Mods.HasNeonLights)
            {
                // We'll handle neons specially
                _categories.Add(new ModCategory("Neon Lights", -2, 5)); // Special type -2 for neons
            }

            _currentCategoryIndex = 0;
        }

        private void AddModCategoryIfAvailable(string name, int modType)
        {
            try
            {
                int count = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, _vehicle, modType);
                if (count > 0)
                {
                    _categories.Add(new ModCategory(name, modType, count));
                }
            }
            catch { /* Some mod types not available for all vehicles - expected failure */ }
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            if (_categories.Count == 0)
                return;

            if (_inSubmenu)
            {
                ModCategory category = _categories[_currentCategoryIndex];
                int maxIndex = GetMaxModIndex(category);
                int minIndex = GetMinModIndex(category);

                if (_currentModIndex > minIndex)
                    _currentModIndex--;
                else
                    _currentModIndex = maxIndex;
            }
            else
            {
                if (_currentCategoryIndex > 0)
                    _currentCategoryIndex--;
                else
                    _currentCategoryIndex = _categories.Count - 1;
            }
        }

        public void NavigateNext(bool fastScroll = false)
        {
            if (_categories.Count == 0)
                return;

            if (_inSubmenu)
            {
                ModCategory category = _categories[_currentCategoryIndex];
                int maxIndex = GetMaxModIndex(category);
                int minIndex = GetMinModIndex(category);

                if (_currentModIndex < maxIndex)
                    _currentModIndex++;
                else
                    _currentModIndex = minIndex; // Wrap to minimum
            }
            else
            {
                if (_currentCategoryIndex < _categories.Count - 1)
                    _currentCategoryIndex++;
                else
                    _currentCategoryIndex = 0;
            }
        }

        private int GetMinModIndex(ModCategory category)
        {
            // Wheel types and toggles start at 0, other mods can go to -1 (stock)
            if (category.ModType == Constants.MOD_TYPE_WHEEL_TYPE)
                return 0;
            if (category.IsToggle)
                return 0;
            return -1; // Stock option for regular mods
        }

        private int GetMaxModIndex(ModCategory category)
        {
            if (category.IsToggle)
                return 1; // Off (0) or On (1)
            if (category.ModType == Constants.MOD_TYPE_NEONS)
                return 4; // Off, Left, Right, Front, Back, All
            if (category.ModType == Constants.MOD_TYPE_WHEEL_TYPE)
                return Constants.WHEEL_TYPE_COUNT - 1; // 0-12
            return category.ModCount - 1;
        }

        public string GetCurrentItemText()
        {
            if (_categories.Count == 0)
                return "No modifications available";

            if (_inSubmenu)
            {
                ModCategory category = _categories[_currentCategoryIndex];
                return GetModOptionText(category, _currentModIndex);
            }
            else
            {
                ModCategory category = _categories[_currentCategoryIndex];
                string currentValue = GetCurrentModValue(category);
                return $"{category.Name}: {currentValue}";
            }
        }

        private string GetCurrentModValue(ModCategory category)
        {
            // Defensive: Check vehicle is still valid
            if (_vehicle == null || !_vehicle.Exists())
                return "N/A";

            if (category.IsToggle && category.ToggleType.HasValue)
            {
                bool installed = _vehicle.Mods[category.ToggleType.Value].IsInstalled;
                return installed ? "On" : "Off";
            }
            else if (category.ModType == Constants.MOD_TYPE_NEONS)
            {
                bool any = _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Left) ||
                           _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Right) ||
                           _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Front) ||
                           _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Back);
                return any ? "On" : "Off";
            }
            else if (category.ModType == Constants.MOD_TYPE_WHEEL_TYPE)
            {
                int wheelType = (int)_vehicle.Mods.WheelType;
                return Constants.GetWheelTypeName(wheelType);
            }
            else
            {
                int currentMod = Function.Call<int>(Hash.GET_VEHICLE_MOD, _vehicle, category.ModType);
                if (currentMod < 0)
                    return "Stock";
                return $"Level {currentMod + 1}";
            }
        }

        private string GetModOptionText(ModCategory category, int index)
        {
            if (category.IsToggle)
            {
                return index == 0 ? "Off" : "On";
            }
            else if (category.ModType == Constants.MOD_TYPE_NEONS)
            {
                switch (index)
                {
                    case -1: return "All Off";
                    case 0: return "Left Only";
                    case 1: return "Right Only";
                    case 2: return "Front Only";
                    case 3: return "Back Only";
                    case 4: return "All On";
                    default: return "Unknown";
                }
            }
            else if (category.ModType == Constants.MOD_TYPE_WHEEL_TYPE)
            {
                // Wheel types don't have a "stock" option - index 0-12 maps directly to wheel types
                return Constants.GetWheelTypeName(index);
            }
            else
            {
                if (index < 0)
                    return "Stock";

                // Try to get localized mod name
                try
                {
                    string localizedName = Function.Call<string>(
                        Hash.GET_MOD_TEXT_LABEL, _vehicle, category.ModType, index);
                    if (!string.IsNullOrEmpty(localizedName) && localizedName != "NULL")
                    {
                        string displayName = Game.GetLocalizedString(localizedName);
                        if (!string.IsNullOrEmpty(displayName) && displayName != "NULL")
                            return displayName;
                    }
                }
                catch { /* Localized name lookup can fail for some mods - fall back to level number */ }

                return $"Level {index + 1}";
            }
        }

        public void ExecuteSelection()
        {
            if (_categories.Count == 0)
                return;

            if (_inSubmenu)
            {
                ApplyMod();
            }
            else
            {
                // Enter submenu
                ModCategory category = _categories[_currentCategoryIndex];
                _currentModIndex = GetCurrentModIndex(category);
                _inSubmenu = true;
            }
        }

        private int GetCurrentModIndex(ModCategory category)
        {
            if (category.IsToggle && category.ToggleType.HasValue)
            {
                return _vehicle.Mods[category.ToggleType.Value].IsInstalled ? 1 : 0;
            }
            else if (category.ModType == Constants.MOD_TYPE_NEONS)
            {
                bool all = _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Left) &&
                           _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Right) &&
                           _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Front) &&
                           _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Back);
                if (all) return 4;

                if (_vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Left)) return 0;
                if (_vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Right)) return 1;
                if (_vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Front)) return 2;
                if (_vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Back)) return 3;

                return -1; // All off
            }
            else if (category.ModType == Constants.MOD_TYPE_WHEEL_TYPE)
            {
                return (int)_vehicle.Mods.WheelType;
            }
            else
            {
                return Function.Call<int>(Hash.GET_VEHICLE_MOD, _vehicle, category.ModType);
            }
        }

        private void ApplyMod()
        {
            // Defensive: Check vehicle is still valid
            if (_vehicle == null || !_vehicle.Exists())
            {
                DavyKager.Tolk.Speak("Vehicle no longer available");
                return;
            }

            // Defensive: Validate category index
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count)
                return;

            ModCategory category = _categories[_currentCategoryIndex];

            if (category.IsToggle && category.ToggleType.HasValue)
            {
                bool newState = _currentModIndex == 1;
                _vehicle.Mods[category.ToggleType.Value].IsInstalled = newState;
                Tolk.Speak(newState ? $"{category.Name} installed" : $"{category.Name} removed");
            }
            else if (category.ModType == Constants.MOD_TYPE_NEONS)
            {
                switch (_currentModIndex)
                {
                    case -1: // All off
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, false);
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, false);
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, false);
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, false);
                        Tolk.Speak("All neons off");
                        break;
                    case 0:
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, true);
                        Tolk.Speak("Left neon on");
                        break;
                    case 1:
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, true);
                        Tolk.Speak("Right neon on");
                        break;
                    case 2:
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, true);
                        Tolk.Speak("Front neon on");
                        break;
                    case 3:
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, true);
                        Tolk.Speak("Back neon on");
                        break;
                    case 4: // All on
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, true);
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, true);
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, true);
                        _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, true);
                        // Set a default neon color (blue)
                        _vehicle.Mods.NeonLightsColor = Color.Blue;
                        Tolk.Speak("All neons on");
                        break;
                }
            }
            else if (category.ModType == Constants.MOD_TYPE_WHEEL_TYPE)
            {
                // Set wheel type (using safe bounds-checked accessor)
                _vehicle.Mods.WheelType = (VehicleWheelType)_currentModIndex;
                string typeName = Constants.GetWheelTypeName(_currentModIndex);
                Tolk.Speak($"Wheel type set to {typeName}");
            }
            else
            {
                // Regular mod
                if (_currentModIndex < 0)
                {
                    // Remove mod (set to stock)
                    Function.Call(Hash.REMOVE_VEHICLE_MOD, _vehicle, category.ModType);
                    Tolk.Speak($"{category.Name} removed");
                }
                else
                {
                    Function.Call(Hash.SET_VEHICLE_MOD, _vehicle, category.ModType, _currentModIndex, false);
                    string modName = GetModOptionText(category, _currentModIndex);
                    Tolk.Speak($"{category.Name}: {modName} installed");
                }
            }
        }

        public string GetMenuName()
        {
            if (_inSubmenu)
            {
                return _categories[_currentCategoryIndex].Name;
            }
            return "Vehicle Mods";
        }

        public bool HasActiveSubmenu => _inSubmenu;

        public void ExitSubmenu()
        {
            if (_inSubmenu)
            {
                _inSubmenu = false;
                _currentModIndex = 0;
            }
        }
    }
}
