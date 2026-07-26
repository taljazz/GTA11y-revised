using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Comprehensive vehicle modification menu supporting all mod types including
    /// Benny's customs, colors, liveries, and special modifications.
    /// Top level = mod categories, submenu = the options within a category.
    /// Modernized onto the typed SHVDN VehicleModCollection API: mod types are the
    /// VehicleModType enum, categories use game-localized names where available,
    /// wheel categories are filtered to AllowedWheelTypes, and property-based
    /// liveries (police cars, Titan, ...) are supported alongside mod-slot liveries.
    /// Overrides SubmenuMinIndex because standard mods use -1 for "Stock".
    /// </summary>
    public class VehicleModMenu : HierarchicalMenuBase
    {
        #region Types

        /// <summary>
        /// Represents a mod category with available options
        /// </summary>
        private class ModCategory
        {
            public string Name { get; }
            public VehicleModType ModType { get; }   // For Standard and Horn categories
            public int OptionCount { get; }          // Number of selectable options
            public VehicleToggleModType? ToggleType { get; }
            public CategoryType Type { get; }        // Type of category for special handling

            public enum CategoryType
            {
                Standard,        // VehicleModType slot based (spoiler, engine, ...)
                Toggle,          // VehicleToggleModType based (turbo, xenon)
                Neons,           // Neon light layout
                WheelType,       // Wheel category selection (AllowedWheelTypes-filtered)
                WindowTint,      // Window tint
                PrimaryColor,    // Primary vehicle color
                SecondaryColor,  // Secondary vehicle color
                PearlescentColor,// Pearlescent color
                RimColor,        // Wheel rim color
                TrimColor,       // Interior trim color (Benny's)
                DashboardColor,  // Dashboard color (Benny's)
                NeonColor,       // Neon light color
                TireSmokeColor,  // Tire smoke color
                Horn,            // Horns with curated names
                PlateStyle,      // License plate style
                LiveryProperty   // Property-based livery (GET_VEHICLE_LIVERY vehicles)
            }

            // Standard mod slot category
            public ModCategory(string name, VehicleModType modType, int optionCount)
            {
                Name = name;
                ModType = modType;
                OptionCount = optionCount;
                ToggleType = null;
                Type = modType == VehicleModType.Horns ? CategoryType.Horn : CategoryType.Standard;
            }

            // Toggle mod category
            public ModCategory(string name, VehicleToggleModType toggleType)
            {
                Name = name;
                ModType = VehicleModType.None;
                OptionCount = 2; // On/Off
                ToggleType = toggleType;
                Type = CategoryType.Toggle;
            }

            // Special category (neons, colors, wheel type, livery, ...)
            public ModCategory(string name, CategoryType type, int optionCount)
            {
                Name = name;
                ModType = VehicleModType.None;
                OptionCount = optionCount;
                ToggleType = null;
                Type = type;
            }
        }

        #endregion

        #region Constants

        // Mod slots the game supports but the SHVDN enum does not name.
        // 47 = right door variants on some vehicles, 49 = light bar (emergency vehicles).
        private const VehicleModType MOD_TYPE_RIGHT_DOOR = (VehicleModType)47;
        private const VehicleModType MOD_TYPE_LIGHT_BAR = (VehicleModType)49;

        // Standard mod slots offered when the vehicle has options for them,
        // in spoken menu order: performance first, then body, then interior.
        private static readonly KeyValuePair<string, VehicleModType>[] StandardSlots =
        {
            // Performance (most commonly used)
            new KeyValuePair<string, VehicleModType>("Engine", VehicleModType.Engine),
            new KeyValuePair<string, VehicleModType>("Transmission", VehicleModType.Transmission),
            new KeyValuePair<string, VehicleModType>("Brakes", VehicleModType.Brakes),
            new KeyValuePair<string, VehicleModType>("Suspension", VehicleModType.Suspension),
            new KeyValuePair<string, VehicleModType>("Armor", VehicleModType.Armor),

            // Body
            new KeyValuePair<string, VehicleModType>("Spoiler", VehicleModType.Spoilers),
            new KeyValuePair<string, VehicleModType>("Front Bumper", VehicleModType.FrontBumper),
            new KeyValuePair<string, VehicleModType>("Rear Bumper", VehicleModType.RearBumper),
            new KeyValuePair<string, VehicleModType>("Side Skirt", VehicleModType.SideSkirt),
            new KeyValuePair<string, VehicleModType>("Exhaust", VehicleModType.Exhaust),
            new KeyValuePair<string, VehicleModType>("Frame", VehicleModType.Frame),
            new KeyValuePair<string, VehicleModType>("Grille", VehicleModType.Grille),
            new KeyValuePair<string, VehicleModType>("Hood", VehicleModType.Hood),
            new KeyValuePair<string, VehicleModType>("Left Fender", VehicleModType.Fender),
            new KeyValuePair<string, VehicleModType>("Right Fender", VehicleModType.RightFender),
            new KeyValuePair<string, VehicleModType>("Roof", VehicleModType.Roof),
            new KeyValuePair<string, VehicleModType>("Left Door", VehicleModType.Windows),
            new KeyValuePair<string, VehicleModType>("Right Door", MOD_TYPE_RIGHT_DOOR),

            // Wheels
            new KeyValuePair<string, VehicleModType>("Front Wheels", VehicleModType.FrontWheel),
            new KeyValuePair<string, VehicleModType>("Rear Wheels", VehicleModType.RearWheel),

            // Interior / Benny's
            new KeyValuePair<string, VehicleModType>("Plate Holder", VehicleModType.PlateHolder),
            new KeyValuePair<string, VehicleModType>("Vanity Plate", VehicleModType.VanityPlates),
            new KeyValuePair<string, VehicleModType>("Trim Design", VehicleModType.TrimDesign),
            new KeyValuePair<string, VehicleModType>("Ornaments", VehicleModType.Ornaments),
            new KeyValuePair<string, VehicleModType>("Dashboard", VehicleModType.Dashboard),
            new KeyValuePair<string, VehicleModType>("Dial Design", VehicleModType.DialDesign),
            new KeyValuePair<string, VehicleModType>("Door Speaker", VehicleModType.DoorSpeakers),
            new KeyValuePair<string, VehicleModType>("Seats", VehicleModType.Seats),
            new KeyValuePair<string, VehicleModType>("Steering Wheel", VehicleModType.SteeringWheels),
            new KeyValuePair<string, VehicleModType>("Shift Lever", VehicleModType.ColumnShifterLevers),
            new KeyValuePair<string, VehicleModType>("Plaques", VehicleModType.Plaques),
            new KeyValuePair<string, VehicleModType>("Speakers", VehicleModType.Speakers),
            new KeyValuePair<string, VehicleModType>("Trunk", VehicleModType.Trunk),
            new KeyValuePair<string, VehicleModType>("Hydraulics", VehicleModType.Hydraulics),
            new KeyValuePair<string, VehicleModType>("Engine Block", VehicleModType.EngineBlock),
            new KeyValuePair<string, VehicleModType>("Air Filter", VehicleModType.AirFilter),
            new KeyValuePair<string, VehicleModType>("Strut Bar", VehicleModType.Struts),
            new KeyValuePair<string, VehicleModType>("Arch Cover", VehicleModType.ArchCover),
            new KeyValuePair<string, VehicleModType>("Antenna", VehicleModType.Aerials),
            new KeyValuePair<string, VehicleModType>("Exterior Parts", VehicleModType.Trim),
            new KeyValuePair<string, VehicleModType>("Tank", VehicleModType.Tank),
        };

        #endregion

        #region Fields

        private readonly Vehicle _vehicle;
        private readonly SettingsManager _settings;

        // Available mod categories that have options for this vehicle
        private readonly List<ModCategory> _categories;

        // Wheel categories this vehicle actually supports (AllowedWheelTypes)
        private VehicleWheelType[] _allowedWheelTypes = new VehicleWheelType[0];

        #endregion

        #region Construction

        public VehicleModMenu(Vehicle vehicle, SettingsManager settings, AudioManager audio) : base(audio)
        {
            _vehicle = vehicle;
            _settings = settings;
            _categories = new List<ModCategory>();

            if (_vehicle == null)
                return;

            // Install mod kit first - required for many mods
            _vehicle.Mods.InstallModKit();

            BuildModCategories();
        }

        /// <summary>
        /// Build the list of available mod categories for this vehicle using the
        /// typed VehicleModCollection API. Categories with no options are skipped.
        /// </summary>
        private void BuildModCategories()
        {
            VehicleModCollection mods = _vehicle.Mods;

            // ===== STANDARD MOD SLOTS (performance, body, wheels, interior) =====
            foreach (var slot in StandardSlots)
            {
                AddStandardCategoryIfAvailable(slot.Key, slot.Value);
            }

            // Toggle performance mod
            _categories.Add(new ModCategory("Turbo", VehicleToggleModType.Turbo));

            // ===== WHEEL TYPE (filtered to categories this vehicle allows) =====
            try
            {
                _allowedWheelTypes = mods.AllowedWheelTypes ?? new VehicleWheelType[0];
            }
            catch (Exception ex)
            {
                Logger.Debug($"AllowedWheelTypes unavailable: {ex.Message}");
                _allowedWheelTypes = new VehicleWheelType[0];
            }

            if (_allowedWheelTypes.Length > 1)
            {
                _categories.Add(new ModCategory("Wheel Type", ModCategory.CategoryType.WheelType, _allowedWheelTypes.Length));
            }

            // ===== HORN (typed slot, curated names) =====
            AddStandardCategoryIfAvailable("Horn", VehicleModType.Horns);

            // ===== LIGHTS =====
            _categories.Add(new ModCategory("Xenon Headlights", VehicleToggleModType.XenonHeadlights));
            AddStandardCategoryIfAvailable("Light Bar", MOD_TYPE_LIGHT_BAR);

            // Neons (if supported)
            if (mods.HasNeonLights)
            {
                _categories.Add(new ModCategory("Neon Lights", ModCategory.CategoryType.Neons, 7));
                _categories.Add(new ModCategory("Neon Color", ModCategory.CategoryType.NeonColor, 15));
            }

            // ===== LIVERY =====
            // Property-based liveries (police cars, Titan, many service vehicles)
            // take priority; fall back to the livery mod slot when absent.
            int liveryCount = 0;
            try { liveryCount = mods.LiveryCount; } catch { /* Not all vehicles report */ }

            if (liveryCount > 0)
            {
                _categories.Add(new ModCategory("Livery", ModCategory.CategoryType.LiveryProperty, liveryCount));
            }
            else
            {
                AddStandardCategoryIfAvailable("Livery", VehicleModType.Livery);
            }

            // ===== COLORS =====
            _categories.Add(new ModCategory("Primary Color", ModCategory.CategoryType.PrimaryColor, 161));
            _categories.Add(new ModCategory("Secondary Color", ModCategory.CategoryType.SecondaryColor, 161));
            _categories.Add(new ModCategory("Pearlescent", ModCategory.CategoryType.PearlescentColor, 161));
            _categories.Add(new ModCategory("Rim Color", ModCategory.CategoryType.RimColor, 161));
            _categories.Add(new ModCategory("Trim Color", ModCategory.CategoryType.TrimColor, 161));
            _categories.Add(new ModCategory("Dashboard Color", ModCategory.CategoryType.DashboardColor, 161));

            // Window tint
            _categories.Add(new ModCategory("Window Tint", ModCategory.CategoryType.WindowTint, 7));

            // Tire smoke color (applies once tire smoke is installed)
            _categories.Add(new ModCategory("Tire Smoke Color", ModCategory.CategoryType.TireSmokeColor, 15));

            // License plate style
            _categories.Add(new ModCategory("Plate Style", ModCategory.CategoryType.PlateStyle, 6));
        }

        /// <summary>
        /// Add a standard mod slot category when the vehicle has options for it.
        /// Prefers the game's localized category name over our fallback.
        /// </summary>
        private void AddStandardCategoryIfAvailable(string fallbackName, VehicleModType modType)
        {
            try
            {
                VehicleMod mod = _vehicle.Mods[modType];
                int count = mod.Count;
                if (count <= 0)
                    return;

                string name = fallbackName;
                try
                {
                    string localized = mod.LocalizedTypeName;
                    if (!string.IsNullOrEmpty(localized) && localized != "NULL")
                        name = localized;
                }
                catch
                {
                    // Keep the fallback name
                }

                _categories.Add(new ModCategory(name, modType, count));
            }
            catch (Exception ex)
            {
                Logger.Debug($"Mod type {modType} ({fallbackName}) not available: {ex.Message}");
            }
        }

        #endregion

        #region Top Level - categories

        protected override int ItemCount => _categories.Count;

        protected override string EmptyMenuText => "No modifications available";

        protected override string GetItemText(int index)
        {
            ModCategory category = _categories[index];
            string currentValue = GetCurrentModValue(category);
            return $"{category.Name}: {currentValue}";
        }

        protected override void OnItemActivated(int index)
        {
            // Enter submenu positioned at the currently installed option
            ModCategory category = _categories[index];
            EnterSubmenu(GetCurrentModIndex(category));
        }

        public override string GetMenuName()
        {
            if (InSubmenu && _categories.Count > 0)
            {
                return _categories[SelectedIndex].Name;
            }
            return "Vehicle Mods";
        }

        #endregion

        #region Submenu - mod options

        private ModCategory CurrentCategory =>
            _categories.Count > 0 ? _categories[SelectedIndex] : null;

        protected override int SubmenuMinIndex
        {
            get
            {
                ModCategory category = CurrentCategory;
                return category != null ? GetMinModIndex(category) : 0;
            }
        }

        protected override int SubmenuItemCount
        {
            get
            {
                ModCategory category = CurrentCategory;
                if (category == null) return 0;
                return GetMaxModIndex(category) - GetMinModIndex(category) + 1;
            }
        }

        protected override int SubmenuFastScrollStep => 10;

        protected override string GetSubmenuItemText(int index)
        {
            return GetModOptionText(CurrentCategory, index);
        }

        protected override void OnSubmenuItemActivated(int index)
        {
            ApplyMod(index);
        }

        #endregion

        #region Index Ranges

        private int GetMinModIndex(ModCategory category)
        {
            switch (category.Type)
            {
                case ModCategory.CategoryType.Neons:
                    return -1; // All off option
                case ModCategory.CategoryType.Standard:
                    return -1; // Stock option for regular mod slots
                default:
                    return 0;
            }
        }

        private int GetMaxModIndex(ModCategory category)
        {
            switch (category.Type)
            {
                case ModCategory.CategoryType.Toggle:
                    return 1;
                case ModCategory.CategoryType.Neons:
                    return 5; // Off, Left, Right, Front, Back, All, Front+Back
                case ModCategory.CategoryType.WindowTint:
                    return 6; // 7 tint options (0-6)
                case ModCategory.CategoryType.PrimaryColor:
                case ModCategory.CategoryType.SecondaryColor:
                case ModCategory.CategoryType.PearlescentColor:
                case ModCategory.CategoryType.RimColor:
                case ModCategory.CategoryType.TrimColor:
                case ModCategory.CategoryType.DashboardColor:
                    return 160; // 161 colors (0-160)
                case ModCategory.CategoryType.NeonColor:
                case ModCategory.CategoryType.TireSmokeColor:
                    return 14; // 15 preset colors
                case ModCategory.CategoryType.PlateStyle:
                    return 5; // 6 plate styles (0-5)
                default:
                    // WheelType, Horn, LiveryProperty, Standard slots
                    return category.OptionCount - 1;
            }
        }

        #endregion

        #region Current Value Lookup

        private string GetCurrentModValue(ModCategory category)
        {
            if (_vehicle == null || !_vehicle.Exists())
                return "N/A";

            VehicleModCollection mods = _vehicle.Mods;

            try
            {
                switch (category.Type)
                {
                    case ModCategory.CategoryType.Toggle:
                        if (category.ToggleType.HasValue)
                        {
                            bool installed = mods[category.ToggleType.Value].IsInstalled;
                            return installed ? "On" : "Off";
                        }
                        return "N/A";

                    case ModCategory.CategoryType.Neons:
                        bool anyNeon = mods.IsNeonLightsOn(VehicleNeonLight.Left) ||
                                       mods.IsNeonLightsOn(VehicleNeonLight.Right) ||
                                       mods.IsNeonLightsOn(VehicleNeonLight.Front) ||
                                       mods.IsNeonLightsOn(VehicleNeonLight.Back);
                        return anyNeon ? "On" : "Off";

                    case ModCategory.CategoryType.WheelType:
                        return GetWheelTypeDisplayName(mods.WheelType);

                    case ModCategory.CategoryType.WindowTint:
                        return GetWindowTintName((int)mods.WindowTint);

                    case ModCategory.CategoryType.PrimaryColor:
                        return GetColorName((int)mods.PrimaryColor);

                    case ModCategory.CategoryType.SecondaryColor:
                        return GetColorName((int)mods.SecondaryColor);

                    case ModCategory.CategoryType.PearlescentColor:
                        return GetColorName((int)mods.PearlescentColor);

                    case ModCategory.CategoryType.RimColor:
                        return GetColorName((int)mods.RimColor);

                    case ModCategory.CategoryType.TrimColor:
                        return GetColorName((int)mods.TrimColor);

                    case ModCategory.CategoryType.DashboardColor:
                        return GetColorName((int)mods.DashboardColor);

                    case ModCategory.CategoryType.NeonColor:
                        return GetNeonColorName(mods.NeonLightsColor);

                    case ModCategory.CategoryType.TireSmokeColor:
                        return GetTireSmokeColorName(mods.TireSmokeColor);

                    case ModCategory.CategoryType.PlateStyle:
                        return GetPlateStyleName((int)mods.LicensePlateStyle);

                    case ModCategory.CategoryType.Horn:
                        return Constants.GetHornName(mods[VehicleModType.Horns].Index);

                    case ModCategory.CategoryType.LiveryProperty:
                        return GetLiveryPropertyName(mods.Livery);

                    default:
                        VehicleMod mod = mods[category.ModType];
                        int currentIndex = mod.Index;
                        if (currentIndex < 0)
                            return "Stock";

                        // The typed API names the currently installed mod directly
                        try
                        {
                            string localized = mod.LocalizedName;
                            if (!string.IsNullOrEmpty(localized) && localized != "NULL")
                                return localized;
                        }
                        catch
                        {
                            // Fall through to per-index label lookup
                        }
                        return GetModLevelName(category, currentIndex);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"GetCurrentModValue failed for {category.Name}: {ex.Message}");
                return "N/A";
            }
        }

        private int GetCurrentModIndex(ModCategory category)
        {
            if (_vehicle == null || !_vehicle.Exists())
                return 0;

            VehicleModCollection mods = _vehicle.Mods;

            try
            {
                switch (category.Type)
                {
                    case ModCategory.CategoryType.Toggle:
                        if (category.ToggleType.HasValue)
                            return mods[category.ToggleType.Value].IsInstalled ? 1 : 0;
                        return 0;

                    case ModCategory.CategoryType.Neons:
                        bool all = mods.IsNeonLightsOn(VehicleNeonLight.Left) &&
                                   mods.IsNeonLightsOn(VehicleNeonLight.Right) &&
                                   mods.IsNeonLightsOn(VehicleNeonLight.Front) &&
                                   mods.IsNeonLightsOn(VehicleNeonLight.Back);
                        if (all) return 4;
                        if (mods.IsNeonLightsOn(VehicleNeonLight.Left)) return 0;
                        if (mods.IsNeonLightsOn(VehicleNeonLight.Right)) return 1;
                        if (mods.IsNeonLightsOn(VehicleNeonLight.Front)) return 2;
                        if (mods.IsNeonLightsOn(VehicleNeonLight.Back)) return 3;
                        return -1;

                    case ModCategory.CategoryType.WheelType:
                        int wheelIndex = Array.IndexOf(_allowedWheelTypes, mods.WheelType);
                        return wheelIndex >= 0 ? wheelIndex : 0;

                    case ModCategory.CategoryType.WindowTint:
                        return (int)mods.WindowTint;

                    case ModCategory.CategoryType.PrimaryColor:
                        return (int)mods.PrimaryColor;

                    case ModCategory.CategoryType.SecondaryColor:
                        return (int)mods.SecondaryColor;

                    case ModCategory.CategoryType.PearlescentColor:
                        return (int)mods.PearlescentColor;

                    case ModCategory.CategoryType.RimColor:
                        return (int)mods.RimColor;

                    case ModCategory.CategoryType.TrimColor:
                        return (int)mods.TrimColor;

                    case ModCategory.CategoryType.DashboardColor:
                        return (int)mods.DashboardColor;

                    case ModCategory.CategoryType.NeonColor:
                    case ModCategory.CategoryType.TireSmokeColor:
                        return 0; // Start at first preset

                    case ModCategory.CategoryType.PlateStyle:
                        return (int)mods.LicensePlateStyle;

                    case ModCategory.CategoryType.Horn:
                        return Math.Max(0, mods[VehicleModType.Horns].Index);

                    case ModCategory.CategoryType.LiveryProperty:
                        return Math.Max(0, mods.Livery);

                    default:
                        return mods[category.ModType].Index;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"GetCurrentModIndex failed for {category.Name}: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region Option Naming

        private string GetModOptionText(ModCategory category, int index)
        {
            if (category == null)
                return "(unavailable)";

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
                    if (index >= 0 && index < _allowedWheelTypes.Length)
                        return GetWheelTypeDisplayName(_allowedWheelTypes[index]);
                    return "Unknown";

                case ModCategory.CategoryType.WindowTint:
                    return GetWindowTintName(index);

                case ModCategory.CategoryType.PrimaryColor:
                case ModCategory.CategoryType.SecondaryColor:
                case ModCategory.CategoryType.PearlescentColor:
                case ModCategory.CategoryType.RimColor:
                case ModCategory.CategoryType.TrimColor:
                case ModCategory.CategoryType.DashboardColor:
                    return GetColorName(index);

                case ModCategory.CategoryType.NeonColor:
                case ModCategory.CategoryType.TireSmokeColor:
                    return GetPresetColorName(index);

                case ModCategory.CategoryType.PlateStyle:
                    return GetPlateStyleName(index);

                case ModCategory.CategoryType.Horn:
                    return Constants.GetHornName(index);

                case ModCategory.CategoryType.LiveryProperty:
                    return $"Livery {index + 1} of {category.OptionCount}";

                default:
                    if (index < 0)
                        return "Stock";
                    return GetModLevelName(category, index);
            }
        }

        /// <summary>
        /// Get the display name for a specific mod option index.
        /// The typed API only names the installed option, so browsing uses the
        /// GET_MOD_TEXT_LABEL native for arbitrary indices, with a level fallback.
        /// </summary>
        private string GetModLevelName(ModCategory category, int index)
        {
            try
            {
                string localizedLabel = Function.Call<string>(Hash.GET_MOD_TEXT_LABEL, _vehicle, (int)category.ModType, index);
                if (!string.IsNullOrEmpty(localizedLabel) && localizedLabel != "NULL")
                {
                    string displayName = Game.GetLocalizedString(localizedLabel);
                    if (!string.IsNullOrEmpty(displayName) && displayName != "NULL" && !displayName.StartsWith("~"))
                        return displayName;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"GetModLevelName failed for {category.Name} index {index}: {ex.Message}");
            }

            // Fallback based on category name
            return $"{category.Name} Level {index + 1}";
        }

        /// <summary>
        /// Game-localized wheel category name with a constants fallback.
        /// </summary>
        private string GetWheelTypeDisplayName(VehicleWheelType wheelType)
        {
            try
            {
                string localized = _vehicle.Mods.GetLocalizedWheelTypeName(wheelType);
                if (!string.IsNullOrEmpty(localized) && localized != "NULL")
                    return localized;
            }
            catch
            {
                // Fall through to constants fallback
            }
            return Constants.GetWheelTypeName((int)wheelType);
        }

        /// <summary>
        /// Name for the currently applied property-based livery.
        /// The game only names the installed livery, so browsing uses numbers.
        /// </summary>
        private string GetLiveryPropertyName(int liveryIndex)
        {
            try
            {
                string localized = _vehicle.Mods.LocalizedLiveryName;
                if (!string.IsNullOrEmpty(localized) && localized != "NULL")
                    return localized;
            }
            catch
            {
                // Fall through to numbered fallback
            }
            return $"Livery {liveryIndex + 1}";
        }

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

        #endregion

        #region Mod Application

        private void ApplyMod(int modIndex)
        {
            if (_vehicle == null || !_vehicle.Exists())
            {
                Speak("Vehicle no longer available");
                return;
            }

            ModCategory category = CurrentCategory;
            if (category == null)
                return;

            VehicleModCollection mods = _vehicle.Mods;

            try
            {
                switch (category.Type)
                {
                    case ModCategory.CategoryType.Toggle:
                        ApplyToggleMod(category, modIndex);
                        break;

                    case ModCategory.CategoryType.Neons:
                        ApplyNeonMod(modIndex);
                        break;

                    case ModCategory.CategoryType.WheelType:
                        if (modIndex >= 0 && modIndex < _allowedWheelTypes.Length)
                        {
                            mods.WheelType = _allowedWheelTypes[modIndex];
                            Speak($"Wheel type: {GetWheelTypeDisplayName(_allowedWheelTypes[modIndex])}");
                        }
                        break;

                    case ModCategory.CategoryType.WindowTint:
                        mods.WindowTint = (VehicleWindowTint)modIndex;
                        Speak($"Window tint: {GetWindowTintName(modIndex)}");
                        break;

                    case ModCategory.CategoryType.PrimaryColor:
                        mods.PrimaryColor = (VehicleColor)modIndex;
                        Speak($"Primary color: {GetColorName(modIndex)}");
                        break;

                    case ModCategory.CategoryType.SecondaryColor:
                        mods.SecondaryColor = (VehicleColor)modIndex;
                        Speak($"Secondary color: {GetColorName(modIndex)}");
                        break;

                    case ModCategory.CategoryType.PearlescentColor:
                        mods.PearlescentColor = (VehicleColor)modIndex;
                        Speak($"Pearlescent: {GetColorName(modIndex)}");
                        break;

                    case ModCategory.CategoryType.RimColor:
                        mods.RimColor = (VehicleColor)modIndex;
                        Speak($"Rim color: {GetColorName(modIndex)}");
                        break;

                    case ModCategory.CategoryType.TrimColor:
                        mods.TrimColor = (VehicleColor)modIndex;
                        Speak($"Trim color: {GetColorName(modIndex)}");
                        break;

                    case ModCategory.CategoryType.DashboardColor:
                        mods.DashboardColor = (VehicleColor)modIndex;
                        Speak($"Dashboard color: {GetColorName(modIndex)}");
                        break;

                    case ModCategory.CategoryType.NeonColor:
                        ApplyNeonColor(modIndex);
                        break;

                    case ModCategory.CategoryType.TireSmokeColor:
                        ApplyTireSmokeColor(modIndex);
                        break;

                    case ModCategory.CategoryType.PlateStyle:
                        mods.LicensePlateStyle = (LicensePlateStyle)modIndex;
                        Speak($"Plate style: {GetPlateStyleName(modIndex)}");
                        break;

                    case ModCategory.CategoryType.Horn:
                        mods[VehicleModType.Horns].Index = modIndex;
                        Speak($"Horn: {Constants.GetHornName(modIndex)}");
                        break;

                    case ModCategory.CategoryType.LiveryProperty:
                        mods.Livery = modIndex;
                        Speak($"Livery: {GetLiveryPropertyName(modIndex)}");
                        break;

                    default:
                        ApplyStandardMod(category, modIndex);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ApplyMod");
                Speak("Failed to apply modification");
            }
        }

        private void ApplyToggleMod(ModCategory category, int modIndex)
        {
            if (!category.ToggleType.HasValue) return;

            bool newState = modIndex == 1;
            _vehicle.Mods[category.ToggleType.Value].IsInstalled = newState;
            Speak(newState ? $"{category.Name} installed" : $"{category.Name} removed");
        }

        private void ApplyNeonMod(int modIndex)
        {
            switch (modIndex)
            {
                case -1: // All off
                    SetNeons(false, false, false, false);
                    Speak("All neons off");
                    break;
                case 0:
                    SetNeons(true, false, false, false);
                    Speak("Left neon only");
                    break;
                case 1:
                    SetNeons(false, true, false, false);
                    Speak("Right neon only");
                    break;
                case 2:
                    SetNeons(false, false, true, false);
                    Speak("Front neon only");
                    break;
                case 3:
                    SetNeons(false, false, false, true);
                    Speak("Back neon only");
                    break;
                case 4: // All on
                    SetNeons(true, true, true, true);
                    Speak("All neons on");
                    break;
                case 5: // Front and back
                    SetNeons(false, false, true, true);
                    Speak("Front and back neons");
                    break;
            }
        }

        private void SetNeons(bool left, bool right, bool front, bool back)
        {
            _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Left, left);
            _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Right, right);
            _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Front, front);
            _vehicle.Mods.SetNeonLightsOn(VehicleNeonLight.Back, back);
        }

        // Preset neon colors
        private static readonly Color[] NeonColors = {
            Color.White, Color.Blue, Color.Cyan, Color.FromArgb(50, 255, 155), Color.LimeGreen,
            Color.Yellow, Color.FromArgb(255, 200, 0), Color.Orange, Color.Red, Color.FromArgb(255, 105, 180),
            Color.HotPink, Color.Purple, Color.FromArgb(75, 0, 130), Color.FromArgb(100, 100, 100), Color.White
        };

        private void ApplyNeonColor(int modIndex)
        {
            if (modIndex >= 0 && modIndex < NeonColors.Length)
            {
                _vehicle.Mods.NeonLightsColor = NeonColors[modIndex];
                Speak($"Neon color: {GetPresetColorName(modIndex)}");
            }
        }

        private void ApplyTireSmokeColor(int modIndex)
        {
            if (modIndex >= 0 && modIndex < NeonColors.Length)
            {
                _vehicle.Mods.TireSmokeColor = NeonColors[modIndex];
                Speak($"Tire smoke: {GetPresetColorName(modIndex)}");
            }
        }

        private void ApplyStandardMod(ModCategory category, int modIndex)
        {
            VehicleMod mod = _vehicle.Mods[category.ModType];

            if (modIndex < 0)
            {
                // Remove mod (set to stock) via the typed API
                mod.Remove();
                Speak($"{category.Name} removed");
            }
            else
            {
                mod.Index = modIndex;
                string modName = GetModOptionText(category, modIndex);
                Speak($"{category.Name}: {modName}");
            }
        }

        #endregion
    }
}
