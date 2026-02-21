using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Native;
using DavyKager;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Comprehensive vehicle modification menu supporting all mod types including
    /// weaponized vehicles, Benny's customs, colors, and special modifications.
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
            public int ModType { get; }          // VehicleModType enum value (-ve for special types)
            public int ModCount { get; }         // Number of available mods
            public bool IsToggle { get; }        // True for Turbo, Xenon, etc.
            public VehicleToggleModType? ToggleType { get; }
            public CategoryType Type { get; }    // Type of category for special handling

            public enum CategoryType
            {
                Standard,       // Regular GET_VEHICLE_MOD based
                Toggle,         // VehicleToggleModType based
                Neons,          // Neon lights
                WheelType,      // Wheel category selection
                WindowTint,     // Window tint
                PrimaryColor,   // Primary vehicle color
                SecondaryColor, // Secondary vehicle color
                PearlescentColor, // Pearlescent color
                RimColor,       // Wheel rim color
                NeonColor,      // Neon light color
                TireSmokeColor, // Tire smoke color
                Horn,           // Special handling for horns
                PlateStyle      // License plate style
            }

            // Standard mod category
            public ModCategory(string name, int modType, int modCount)
            {
                Name = name;
                ModType = modType;
                ModCount = modCount;
                IsToggle = false;
                ToggleType = null;
                Type = CategoryType.Standard;
            }

            // Toggle mod category
            public ModCategory(string name, VehicleToggleModType toggleType)
            {
                Name = name;
                ModType = -1;
                ModCount = 2; // On/Off
                IsToggle = true;
                ToggleType = toggleType;
                Type = CategoryType.Toggle;
            }

            // Special category (neons, colors, etc.)
            public ModCategory(string name, CategoryType type, int modCount)
            {
                Name = name;
                ModType = (int)type * -10; // Unique negative value
                ModCount = modCount;
                IsToggle = false;
                ToggleType = null;
                Type = type;
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

            // Install mod kit first - required for many mods
            _vehicle.Mods.InstallModKit();

            BuildModCategories();
            _currentCategoryIndex = 0;
        }

        /// <summary>
        /// Build the list of available mod categories for this vehicle
        /// </summary>
        private void BuildModCategories()
        {
            // ===== PERFORMANCE MODS (Most commonly used) =====
            AddModCategoryIfAvailable("Engine", 11);
            AddModCategoryIfAvailable("Transmission", 13);
            AddModCategoryIfAvailable("Brakes", 12);
            AddModCategoryIfAvailable("Suspension", 15);
            AddModCategoryIfAvailable("Armor", 16);

            // Toggle performance mods
            _categories.Add(new ModCategory("Turbo", VehicleToggleModType.Turbo));

            // Nitrous (if available)
            AddModCategoryIfAvailable("Nitrous", 17);

            // Note: Weaponized vehicle mods (Primary Weapon, Secondary Weapon, Countermeasures, etc.)
            // are NOT accessible through GET_NUM_VEHICLE_MODS/SET_VEHICLE_MOD natives.
            // They require the in-game Vehicle Workshop (Facility, MOC, Avenger) to modify.
            // The standard mod type system only goes up to index 48 (Livery).

            // ===== APPEARANCE - BODY =====
            AddModCategoryIfAvailable("Spoiler", 0);
            AddModCategoryIfAvailable("Front Bumper", 1);
            AddModCategoryIfAvailable("Rear Bumper", 2);
            AddModCategoryIfAvailable("Side Skirt", 3);
            AddModCategoryIfAvailable("Exhaust", 4);
            AddModCategoryIfAvailable("Frame", 5);
            AddModCategoryIfAvailable("Grille", 6);
            AddModCategoryIfAvailable("Hood", 7);
            AddModCategoryIfAvailable("Left Fender", 8);
            AddModCategoryIfAvailable("Right Fender", 9);
            AddModCategoryIfAvailable("Roof", 10);
            AddModCategoryIfAvailable("Left Door", 46);
            AddModCategoryIfAvailable("Right Door", 47);

            // ===== WHEELS =====
            AddModCategoryIfAvailable("Front Wheels", 23);
            AddModCategoryIfAvailable("Rear Wheels", 24);

            // Wheel type selection (always available for wheeled vehicles)
            VehicleClass vClass = _vehicle.ClassType;
            bool hasWheels = vClass != VehicleClass.Boats && vClass != VehicleClass.Helicopters && vClass != VehicleClass.Planes;
            if (hasWheels)
            {
                _categories.Add(new ModCategory("Wheel Type", ModCategory.CategoryType.WheelType, Constants.WHEEL_TYPE_COUNT));
            }

            // ===== INTERIOR / BENNY'S MODS =====
            AddModCategoryIfAvailable("Plate Holder", 25);
            AddModCategoryIfAvailable("Vanity Plate", 26);
            AddModCategoryIfAvailable("Trim", 27);
            AddModCategoryIfAvailable("Ornaments", 28);
            AddModCategoryIfAvailable("Dashboard", 29);
            AddModCategoryIfAvailable("Dial Design", 30);
            AddModCategoryIfAvailable("Door Speaker", 31);
            AddModCategoryIfAvailable("Seats", 32);
            AddModCategoryIfAvailable("Steering Wheel", 33);
            AddModCategoryIfAvailable("Shift Lever", 34);
            AddModCategoryIfAvailable("Plaques", 35);
            AddModCategoryIfAvailable("Speakers", 36);
            AddModCategoryIfAvailable("Trunk", 37);
            AddModCategoryIfAvailable("Hydraulics", 38);
            AddModCategoryIfAvailable("Engine Block", 39);
            AddModCategoryIfAvailable("Air Filter", 40);
            AddModCategoryIfAvailable("Strut Bar", 41);
            AddModCategoryIfAvailable("Arch Cover", 42);
            AddModCategoryIfAvailable("Antenna", 43);
            AddModCategoryIfAvailable("Exterior Parts", 44);
            AddModCategoryIfAvailable("Tank", 45);

            // ===== HORN =====
            int hornCount = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, _vehicle, 14);
            if (hornCount > 0)
            {
                _categories.Add(new ModCategory("Horn", ModCategory.CategoryType.Horn, hornCount));
            }

            // ===== LIGHTS =====
            _categories.Add(new ModCategory("Xenon Headlights", VehicleToggleModType.XenonHeadlights));
            AddModCategoryIfAvailable("Light Bar", 49);

            // Neons (if supported)
            if (_vehicle.Mods.HasNeonLights)
            {
                _categories.Add(new ModCategory("Neon Lights", ModCategory.CategoryType.Neons, 6));
                _categories.Add(new ModCategory("Neon Color", ModCategory.CategoryType.NeonColor, 15));
            }

            // ===== LIVERY =====
            AddModCategoryIfAvailable("Livery", 48);

            // ===== COLORS =====
            _categories.Add(new ModCategory("Primary Color", ModCategory.CategoryType.PrimaryColor, 161));
            _categories.Add(new ModCategory("Secondary Color", ModCategory.CategoryType.SecondaryColor, 161));
            _categories.Add(new ModCategory("Pearlescent", ModCategory.CategoryType.PearlescentColor, 161));
            _categories.Add(new ModCategory("Rim Color", ModCategory.CategoryType.RimColor, 161));

            // Window tint
            _categories.Add(new ModCategory("Window Tint", ModCategory.CategoryType.WindowTint, 7));

            // Tire smoke color (if available)
            int tireSmokeCount = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, _vehicle, 20);
            if (tireSmokeCount > 0)
            {
                _categories.Add(new ModCategory("Tire Smoke Color", ModCategory.CategoryType.TireSmokeColor, 10));
            }

            // License plate style
            _categories.Add(new ModCategory("Plate Style", ModCategory.CategoryType.PlateStyle, 6));
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
            catch (Exception ex) { Logger.Debug($"Mod type {modType} ({name}) not available: {ex.Message}"); }
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
                int step = fastScroll ? 10 : 1;

                _currentModIndex -= step;
                if (_currentModIndex < minIndex)
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
                int step = fastScroll ? 10 : 1;

                _currentModIndex += step;
                if (_currentModIndex > maxIndex)
                    _currentModIndex = minIndex;
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
            switch (category.Type)
            {
                case ModCategory.CategoryType.WheelType:
                case ModCategory.CategoryType.WindowTint:
                case ModCategory.CategoryType.PrimaryColor:
                case ModCategory.CategoryType.SecondaryColor:
                case ModCategory.CategoryType.PearlescentColor:
                case ModCategory.CategoryType.RimColor:
                case ModCategory.CategoryType.NeonColor:
                case ModCategory.CategoryType.TireSmokeColor:
                case ModCategory.CategoryType.PlateStyle:
                case ModCategory.CategoryType.Horn:
                    return 0;
                case ModCategory.CategoryType.Toggle:
                    return 0;
                case ModCategory.CategoryType.Neons:
                    return -1; // All off option
                default:
                    return -1; // Stock option for regular mods
            }
        }

        private int GetMaxModIndex(ModCategory category)
        {
            switch (category.Type)
            {
                case ModCategory.CategoryType.Toggle:
                    return 1;
                case ModCategory.CategoryType.Neons:
                    return 5; // Off, Left, Right, Front, Back, All
                case ModCategory.CategoryType.WheelType:
                    return Constants.WHEEL_TYPE_COUNT - 1;
                case ModCategory.CategoryType.WindowTint:
                    return 6; // 7 tint options (0-6)
                case ModCategory.CategoryType.PrimaryColor:
                case ModCategory.CategoryType.SecondaryColor:
                case ModCategory.CategoryType.PearlescentColor:
                case ModCategory.CategoryType.RimColor:
                    return 160; // 161 colors (0-160)
                case ModCategory.CategoryType.NeonColor:
                case ModCategory.CategoryType.TireSmokeColor:
                    return 14; // 15 preset colors
                case ModCategory.CategoryType.PlateStyle:
                    return 5; // 6 plate styles (0-5)
                case ModCategory.CategoryType.Horn:
                    return category.ModCount - 1;
                default:
                    return category.ModCount - 1;
            }
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
            if (_vehicle == null || !_vehicle.Exists())
                return "N/A";

            try
            {
                switch (category.Type)
                {
                    case ModCategory.CategoryType.Toggle:
                        if (category.ToggleType.HasValue)
                        {
                            bool installed = _vehicle.Mods[category.ToggleType.Value].IsInstalled;
                            return installed ? "On" : "Off";
                        }
                        return "N/A";

                    case ModCategory.CategoryType.Neons:
                        bool anyNeon = _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Left) ||
                                       _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Right) ||
                                       _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Front) ||
                                       _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Back);
                        return anyNeon ? "On" : "Off";

                    case ModCategory.CategoryType.WheelType:
                        return Constants.GetWheelTypeName((int)_vehicle.Mods.WheelType);

                    case ModCategory.CategoryType.WindowTint:
                        return GetWindowTintName((int)_vehicle.Mods.WindowTint);

                    case ModCategory.CategoryType.PrimaryColor:
                        return GetColorName((int)_vehicle.Mods.PrimaryColor);

                    case ModCategory.CategoryType.SecondaryColor:
                        return GetColorName((int)_vehicle.Mods.SecondaryColor);

                    case ModCategory.CategoryType.PearlescentColor:
                        return GetColorName((int)_vehicle.Mods.PearlescentColor);

                    case ModCategory.CategoryType.RimColor:
                        return GetColorName((int)_vehicle.Mods.RimColor);

                    case ModCategory.CategoryType.NeonColor:
                        return GetNeonColorName(_vehicle.Mods.NeonLightsColor);

                    case ModCategory.CategoryType.TireSmokeColor:
                        return GetTireSmokeColorName(_vehicle.Mods.TireSmokeColor);

                    case ModCategory.CategoryType.PlateStyle:
                        return GetPlateStyleName((int)_vehicle.Mods.LicensePlateStyle);

                    case ModCategory.CategoryType.Horn:
                        int currentHorn = Function.Call<int>(Hash.GET_VEHICLE_MOD, _vehicle, 14);
                        return Constants.GetHornName(currentHorn);

                    default:
                        int currentMod = Function.Call<int>(Hash.GET_VEHICLE_MOD, _vehicle, category.ModType);
                        if (currentMod < 0)
                            return "Stock";
                        return GetModLevelName(category.ModType, currentMod);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"GetCurrentModValue failed for {category.Name}: {ex.Message}");
                return "N/A";
            }
        }

        private string GetModOptionText(ModCategory category, int index)
        {
            switch (category.Type)
            {
                case ModCategory.CategoryType.Toggle:
                    return index == 0 ? "Off" : "On";

                case ModCategory.CategoryType.Neons:
                    switch (index)
                    {
                        case -1: return "All Off";
                        case 0: return "Left Only";
                        case 1: return "Right Only";
                        case 2: return "Front Only";
                        case 3: return "Back Only";
                        case 4: return "All Sides";
                        case 5: return "Front and Back";
                        default: return "Unknown";
                    }

                case ModCategory.CategoryType.WheelType:
                    return Constants.GetWheelTypeName(index);

                case ModCategory.CategoryType.WindowTint:
                    return GetWindowTintName(index);

                case ModCategory.CategoryType.PrimaryColor:
                case ModCategory.CategoryType.SecondaryColor:
                case ModCategory.CategoryType.PearlescentColor:
                case ModCategory.CategoryType.RimColor:
                    return GetColorName(index);

                case ModCategory.CategoryType.NeonColor:
                case ModCategory.CategoryType.TireSmokeColor:
                    return GetPresetColorName(index);

                case ModCategory.CategoryType.PlateStyle:
                    return GetPlateStyleName(index);

                case ModCategory.CategoryType.Horn:
                    return Constants.GetHornName(index);

                default:
                    if (index < 0)
                        return "Stock";
                    return GetModLevelName(category.ModType, index);
            }
        }

        private string GetModLevelName(int modType, int index)
        {
            // Try to get localized mod name from the game
            try
            {
                string localizedLabel = Function.Call<string>(Hash.GET_MOD_TEXT_LABEL, _vehicle, modType, index);
                if (!string.IsNullOrEmpty(localizedLabel) && localizedLabel != "NULL")
                {
                    string displayName = Game.GetLocalizedString(localizedLabel);
                    if (!string.IsNullOrEmpty(displayName) && displayName != "NULL" && !displayName.StartsWith("~"))
                        return displayName;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"GetModLevelName failed for type {modType} index {index}: {ex.Message}");
            }

            // Fallback based on mod type
            string typeName = Constants.GetModTypeName(modType);
            return $"{typeName} Level {index + 1}";
        }

        // ===== COLOR AND STYLE HELPERS =====

        private static readonly string[] WindowTintNames = {
            "None", "Pure Black", "Dark Smoke", "Light Smoke", "Stock", "Limo", "Green"
        };

        private string GetWindowTintName(int index)
        {
            if (index >= 0 && index < WindowTintNames.Length)
                return WindowTintNames[index];
            return $"Tint {index}";
        }

        private static readonly string[] PlateStyleNames = {
            "Blue on White 1", "Yellow on Black", "Yellow on Blue", "Blue on White 2", "Blue on White 3", "Yankton"
        };

        private string GetPlateStyleName(int index)
        {
            if (index >= 0 && index < PlateStyleNames.Length)
                return PlateStyleNames[index];
            return $"Style {index}";
        }

        // Vehicle color names (basic set - covers common indices)
        private static readonly Dictionary<int, string> ColorNames = new Dictionary<int, string>
        {
            { 0, "Metallic Black" }, { 1, "Metallic Graphite Black" }, { 2, "Metallic Black Steel" },
            { 3, "Metallic Dark Silver" }, { 4, "Metallic Silver" }, { 5, "Metallic Blue Silver" },
            { 6, "Metallic Steel Gray" }, { 7, "Metallic Shadow Silver" }, { 8, "Metallic Stone Silver" },
            { 9, "Metallic Midnight Silver" }, { 10, "Metallic Gun Metal" }, { 11, "Metallic Anthracite Grey" },
            { 12, "Matte Black" }, { 13, "Matte Gray" }, { 14, "Matte Light Grey" },
            { 15, "Util Black" }, { 16, "Util Black Poly" }, { 17, "Util Dark Silver" },
            { 18, "Util Silver" }, { 19, "Util Gun Metal" }, { 20, "Util Shadow Silver" },
            { 21, "Worn Black" }, { 22, "Worn Graphite" }, { 23, "Worn Silver Grey" },
            { 24, "Worn Silver" }, { 25, "Worn Blue Silver" }, { 26, "Worn Shadow Silver" },
            { 27, "Metallic Red" }, { 28, "Metallic Torino Red" }, { 29, "Metallic Formula Red" },
            { 30, "Metallic Blaze Red" }, { 31, "Metallic Graceful Red" }, { 32, "Metallic Garnet Red" },
            { 33, "Metallic Desert Red" }, { 34, "Metallic Cabernet Red" }, { 35, "Metallic Candy Red" },
            { 36, "Metallic Sunrise Orange" }, { 37, "Metallic Classic Gold" }, { 38, "Metallic Orange" },
            { 39, "Matte Red" }, { 40, "Matte Dark Red" }, { 41, "Matte Orange" },
            { 42, "Matte Yellow" }, { 43, "Util Red" }, { 44, "Util Bright Red" },
            { 45, "Util Garnet Red" }, { 46, "Worn Red" }, { 47, "Worn Golden Red" },
            { 48, "Worn Dark Red" }, { 49, "Metallic Dark Green" }, { 50, "Metallic Racing Green" },
            { 51, "Metallic Sea Green" }, { 52, "Metallic Olive Green" }, { 53, "Metallic Green" },
            { 54, "Metallic Gasoline Blue Green" }, { 55, "Matte Lime Green" }, { 56, "Util Dark Green" },
            { 57, "Util Green" }, { 58, "Worn Dark Green" }, { 59, "Worn Green" },
            { 60, "Worn Sea Wash" }, { 61, "Metallic Midnight Blue" }, { 62, "Metallic Dark Blue" },
            { 63, "Metallic Saxony Blue" }, { 64, "Metallic Blue" }, { 65, "Metallic Mariner Blue" },
            { 66, "Metallic Harbor Blue" }, { 67, "Metallic Diamond Blue" }, { 68, "Metallic Surf Blue" },
            { 69, "Metallic Nautical Blue" }, { 70, "Metallic Bright Blue" }, { 71, "Metallic Purple Blue" },
            { 72, "Metallic Spinnaker Blue" }, { 73, "Metallic Ultra Blue" }, { 74, "Metallic Bright Blue 2" },
            { 75, "Util Dark Blue" }, { 76, "Util Midnight Blue" }, { 77, "Util Blue" },
            { 78, "Util Sea Foam Blue" }, { 79, "Util Lightning Blue" }, { 80, "Util Maui Blue Poly" },
            { 81, "Util Bright Blue" }, { 82, "Matte Dark Blue" }, { 83, "Matte Blue" },
            { 84, "Matte Midnight Blue" }, { 85, "Worn Dark Blue" }, { 86, "Worn Blue" },
            { 87, "Worn Light Blue" }, { 88, "Metallic Taxi Yellow" }, { 89, "Metallic Race Yellow" },
            { 90, "Metallic Bronze" }, { 91, "Metallic Yellow Bird" }, { 92, "Metallic Lime" },
            { 93, "Metallic Champagne" }, { 94, "Metallic Pueblo Beige" }, { 95, "Metallic Dark Ivory" },
            { 96, "Metallic Choco Brown" }, { 97, "Metallic Golden Brown" }, { 98, "Metallic Light Brown" },
            { 99, "Metallic Straw Beige" }, { 100, "Metallic Moss Brown" }, { 101, "Metallic Bison Brown" },
            { 102, "Metallic Creek Brown" }, { 103, "Metallic Feltzer Brown" }, { 104, "Metallic Maple Brown" },
            { 105, "Metallic Beechwood" }, { 106, "Metallic Dark Beechwood" }, { 107, "Metallic Choco Orange" },
            { 108, "Metallic Beach Sand" }, { 109, "Metallic Sun Bleeched Sand" }, { 110, "Metallic Cream" },
            { 111, "Util Brown" }, { 112, "Util Medium Brown" }, { 113, "Util Light Brown" },
            { 114, "Metallic White" }, { 115, "Metallic Frost White" }, { 116, "Worn Honey Beige" },
            { 117, "Worn Brown" }, { 118, "Worn Dark Brown" }, { 119, "Worn Straw Beige" },
            { 120, "Brushed Steel" }, { 121, "Brushed Black Steel" }, { 122, "Brushed Aluminum" },
            { 123, "Chrome" }, { 124, "Worn Off White" }, { 125, "Util Off White" },
            { 126, "Worn Orange" }, { 127, "Worn Light Orange" }, { 128, "Metallic Securicor Green" },
            { 129, "Worn Taxi Yellow" }, { 130, "Police Car Blue" }, { 131, "Matte Green" },
            { 132, "Matte Brown" }, { 133, "Worn Orange 2" }, { 134, "Matte White" },
            { 135, "Worn White" }, { 136, "Worn Olive Army Green" }, { 137, "Pure White" },
            { 138, "Hot Pink" }, { 139, "Salmon Pink" }, { 140, "Metallic Vermillion Pink" },
            { 141, "Orange" }, { 142, "Green" }, { 143, "Blue" },
            { 144, "Mettalic Black Blue" }, { 145, "Metallic Black Purple" }, { 146, "Metallic Black Red" },
            { 147, "Hunter Green" }, { 148, "Metallic Purple" }, { 149, "Metallic V Dark Blue" },
            { 150, "Modshop Black" }, { 151, "Matte Purple" }, { 152, "Matte Dark Purple" },
            { 153, "Metallic Lava Red" }, { 154, "Matte Forest Green" }, { 155, "Matte Olive Drab" },
            { 156, "Matte Desert Brown" }, { 157, "Matte Desert Tan" }, { 158, "Matte Foliage Green" },
            { 159, "Default Alloy Color" }, { 160, "Epsilon Blue" }
        };

        private string GetColorName(int index)
        {
            if (ColorNames.TryGetValue(index, out string name))
                return name;
            return $"Color {index}";
        }

        // Preset colors for neon/tire smoke
        private static readonly string[] PresetColorNames = {
            "White", "Blue", "Electric Blue", "Mint Green", "Lime Green", "Yellow",
            "Golden Shower", "Orange", "Red", "Pony Pink", "Hot Pink", "Purple",
            "Blacklight", "Smoke", "Custom"
        };

        private string GetPresetColorName(int index)
        {
            if (index >= 0 && index < PresetColorNames.Length)
                return PresetColorNames[index];
            return $"Preset {index}";
        }

        private string GetNeonColorName(Color color)
        {
            // Try to match to a preset
            if (color == Color.White) return "White";
            if (color == Color.Blue) return "Blue";
            if (color == Color.Cyan) return "Electric Blue";
            if (color == Color.LimeGreen || color == Color.Lime) return "Lime Green";
            if (color == Color.Yellow) return "Yellow";
            if (color == Color.Orange) return "Orange";
            if (color == Color.Red) return "Red";
            if (color == Color.HotPink || color == Color.DeepPink) return "Hot Pink";
            if (color == Color.Purple || color == Color.DarkViolet) return "Purple";
            return $"RGB({color.R},{color.G},{color.B})";
        }

        private string GetTireSmokeColorName(Color color)
        {
            return GetNeonColorName(color); // Same logic
        }

        // ===== SELECTION EXECUTION =====

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
            if (_vehicle == null || !_vehicle.Exists())
                return 0;

            try
            {
                switch (category.Type)
                {
                    case ModCategory.CategoryType.Toggle:
                        if (category.ToggleType.HasValue)
                            return _vehicle.Mods[category.ToggleType.Value].IsInstalled ? 1 : 0;
                        return 0;

                    case ModCategory.CategoryType.Neons:
                        bool all = _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Left) &&
                                   _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Right) &&
                                   _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Front) &&
                                   _vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Back);
                        if (all) return 4;
                        if (_vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Left)) return 0;
                        if (_vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Right)) return 1;
                        if (_vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Front)) return 2;
                        if (_vehicle.Mods.IsNeonLightsOn(VehicleNeonLight.Back)) return 3;
                        return -1;

                    case ModCategory.CategoryType.WheelType:
                        return (int)_vehicle.Mods.WheelType;

                    case ModCategory.CategoryType.WindowTint:
                        return (int)_vehicle.Mods.WindowTint;

                    case ModCategory.CategoryType.PrimaryColor:
                        return (int)_vehicle.Mods.PrimaryColor;

                    case ModCategory.CategoryType.SecondaryColor:
                        return (int)_vehicle.Mods.SecondaryColor;

                    case ModCategory.CategoryType.PearlescentColor:
                        return (int)_vehicle.Mods.PearlescentColor;

                    case ModCategory.CategoryType.RimColor:
                        return (int)_vehicle.Mods.RimColor;

                    case ModCategory.CategoryType.NeonColor:
                    case ModCategory.CategoryType.TireSmokeColor:
                        return 0; // Start at first preset

                    case ModCategory.CategoryType.PlateStyle:
                        return (int)_vehicle.Mods.LicensePlateStyle;

                    case ModCategory.CategoryType.Horn:
                        return Function.Call<int>(Hash.GET_VEHICLE_MOD, _vehicle, 14);

                    default:
                        return Function.Call<int>(Hash.GET_VEHICLE_MOD, _vehicle, category.ModType);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"GetCurrentModIndex failed for {category.Name}: {ex.Message}");
                return 0;
            }
        }

        private void ApplyMod()
        {
            if (_vehicle == null || !_vehicle.Exists())
            {
                Tolk.Speak("Vehicle no longer available");
                return;
            }

            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count)
                return;

            ModCategory category = _categories[_currentCategoryIndex];

            try
            {
                switch (category.Type)
                {
                    case ModCategory.CategoryType.Toggle:
                        ApplyToggleMod(category);
                        break;

                    case ModCategory.CategoryType.Neons:
                        ApplyNeonMod();
                        break;

                    case ModCategory.CategoryType.WheelType:
                        _vehicle.Mods.WheelType = (VehicleWheelType)_currentModIndex;
                        Tolk.Speak($"Wheel type: {Constants.GetWheelTypeName(_currentModIndex)}");
                        break;

                    case ModCategory.CategoryType.WindowTint:
                        _vehicle.Mods.WindowTint = (VehicleWindowTint)_currentModIndex;
                        Tolk.Speak($"Window tint: {GetWindowTintName(_currentModIndex)}");
                        break;

                    case ModCategory.CategoryType.PrimaryColor:
                        _vehicle.Mods.PrimaryColor = (VehicleColor)_currentModIndex;
                        Tolk.Speak($"Primary color: {GetColorName(_currentModIndex)}");
                        break;

                    case ModCategory.CategoryType.SecondaryColor:
                        _vehicle.Mods.SecondaryColor = (VehicleColor)_currentModIndex;
                        Tolk.Speak($"Secondary color: {GetColorName(_currentModIndex)}");
                        break;

                    case ModCategory.CategoryType.PearlescentColor:
                        _vehicle.Mods.PearlescentColor = (VehicleColor)_currentModIndex;
                        Tolk.Speak($"Pearlescent: {GetColorName(_currentModIndex)}");
                        break;

                    case ModCategory.CategoryType.RimColor:
                        _vehicle.Mods.RimColor = (VehicleColor)_currentModIndex;
                        Tolk.Speak($"Rim color: {GetColorName(_currentModIndex)}");
                        break;

                    case ModCategory.CategoryType.NeonColor:
                        ApplyNeonColor();
                        break;

                    case ModCategory.CategoryType.TireSmokeColor:
                        ApplyTireSmokeColor();
                        break;

                    case ModCategory.CategoryType.PlateStyle:
                        _vehicle.Mods.LicensePlateStyle = (LicensePlateStyle)_currentModIndex;
                        Tolk.Speak($"Plate style: {GetPlateStyleName(_currentModIndex)}");
                        break;

                    case ModCategory.CategoryType.Horn:
                        Function.Call(Hash.SET_VEHICLE_MOD, _vehicle, 14, _currentModIndex, false);
                        Tolk.Speak($"Horn: {Constants.GetHornName(_currentModIndex)}");
                        break;

                    default:
                        ApplyStandardMod(category);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ApplyMod");
                Tolk.Speak("Failed to apply modification");
            }
        }

        private void ApplyToggleMod(ModCategory category)
        {
            if (!category.ToggleType.HasValue) return;

            bool newState = _currentModIndex == 1;
            _vehicle.Mods[category.ToggleType.Value].IsInstalled = newState;
            Tolk.Speak(newState ? $"{category.Name} installed" : $"{category.Name} removed");
        }

        private void ApplyNeonMod()
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
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, false);
                    Tolk.Speak("Left neon only");
                    break;
                case 1:
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, true);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, false);
                    Tolk.Speak("Right neon only");
                    break;
                case 2:
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, true);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, false);
                    Tolk.Speak("Front neon only");
                    break;
                case 3:
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, true);
                    Tolk.Speak("Back neon only");
                    break;
                case 4: // All on
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, true);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, true);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, true);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, true);
                    Tolk.Speak("All neons on");
                    break;
                case 5: // Front and back
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, false);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, true);
                    _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, true);
                    Tolk.Speak("Front and back neons");
                    break;
            }
        }

        // Preset neon colors
        private static readonly Color[] NeonColors = {
            Color.White, Color.Blue, Color.Cyan, Color.FromArgb(50, 255, 155), Color.LimeGreen,
            Color.Yellow, Color.FromArgb(255, 200, 0), Color.Orange, Color.Red, Color.FromArgb(255, 105, 180),
            Color.HotPink, Color.Purple, Color.FromArgb(75, 0, 130), Color.FromArgb(100, 100, 100), Color.White
        };

        private void ApplyNeonColor()
        {
            if (_currentModIndex >= 0 && _currentModIndex < NeonColors.Length)
            {
                _vehicle.Mods.NeonLightsColor = NeonColors[_currentModIndex];
                Tolk.Speak($"Neon color: {GetPresetColorName(_currentModIndex)}");
            }
        }

        private void ApplyTireSmokeColor()
        {
            if (_currentModIndex >= 0 && _currentModIndex < NeonColors.Length)
            {
                _vehicle.Mods.TireSmokeColor = NeonColors[_currentModIndex];
                Tolk.Speak($"Tire smoke: {GetPresetColorName(_currentModIndex)}");
            }
        }

        private void ApplyStandardMod(ModCategory category)
        {
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
                Tolk.Speak($"{category.Name}: {modName}");
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
