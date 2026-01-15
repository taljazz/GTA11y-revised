using System.Collections.Generic;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Data model for a saved vehicle configuration
    /// Stores all mod settings to recreate the vehicle
    /// </summary>
    public class SavedVehicle
    {
        /// <summary>
        /// Vehicle display name (e.g., "Zentorno")
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Vehicle model hash (int representation of VehicleHash)
        /// </summary>
        public int ModelHash { get; set; }

        /// <summary>
        /// All applied mods (ModType index -> Mod index)
        /// -1 means stock/no mod for that slot
        /// </summary>
        public Dictionary<int, int> Mods { get; set; }

        /// <summary>
        /// Primary color (VehicleColor enum value)
        /// </summary>
        public int PrimaryColor { get; set; }

        /// <summary>
        /// Secondary color (VehicleColor enum value)
        /// </summary>
        public int SecondaryColor { get; set; }

        /// <summary>
        /// Pearlescent color (VehicleColor enum value)
        /// </summary>
        public int PearlescentColor { get; set; }

        /// <summary>
        /// Rim/wheel color (VehicleColor enum value)
        /// </summary>
        public int RimColor { get; set; }

        /// <summary>
        /// Wheel type (VehicleWheelType enum value)
        /// </summary>
        public int WheelType { get; set; }

        /// <summary>
        /// Window tint level (VehicleWindowTint enum value)
        /// </summary>
        public int WindowTint { get; set; }

        /// <summary>
        /// Neon light states
        /// </summary>
        public bool NeonLeft { get; set; }
        public bool NeonRight { get; set; }
        public bool NeonFront { get; set; }
        public bool NeonBack { get; set; }

        /// <summary>
        /// Neon color (RGB)
        /// </summary>
        public int NeonColorR { get; set; }
        public int NeonColorG { get; set; }
        public int NeonColorB { get; set; }

        /// <summary>
        /// License plate text
        /// </summary>
        public string LicensePlate { get; set; }

        /// <summary>
        /// License plate style (LicensePlateStyle enum value)
        /// </summary>
        public int LicensePlateStyle { get; set; }

        /// <summary>
        /// Toggle mods (Turbo, Xenon, TireSmoke)
        /// </summary>
        public bool HasTurbo { get; set; }
        public bool HasXenon { get; set; }
        public bool HasTireSmoke { get; set; }

        /// <summary>
        /// Tire smoke color (RGB)
        /// </summary>
        public int TireSmokeColorR { get; set; }
        public int TireSmokeColorG { get; set; }
        public int TireSmokeColorB { get; set; }

        /// <summary>
        /// Custom primary color (RGB, -1 if not custom)
        /// </summary>
        public int CustomPrimaryR { get; set; }
        public int CustomPrimaryG { get; set; }
        public int CustomPrimaryB { get; set; }
        public bool HasCustomPrimaryColor { get; set; }

        /// <summary>
        /// Custom secondary color (RGB, -1 if not custom)
        /// </summary>
        public int CustomSecondaryR { get; set; }
        public int CustomSecondaryG { get; set; }
        public int CustomSecondaryB { get; set; }
        public bool HasCustomSecondaryColor { get; set; }

        /// <summary>
        /// Create empty saved vehicle
        /// </summary>
        public SavedVehicle()
        {
            Mods = new Dictionary<int, int>();
            DisplayName = "";
            LicensePlate = "";
        }

        /// <summary>
        /// Get a summary description of the vehicle for audio
        /// </summary>
        public string GetSummary()
        {
            var parts = new List<string>();
            parts.Add(DisplayName);

            // Add notable mods
            if (Mods.TryGetValue(11, out int engineLevel) && engineLevel >= 0)
                parts.Add($"Engine Level {engineLevel + 1}");

            if (HasTurbo)
                parts.Add("Turbo");

            if (HasXenon)
                parts.Add("Xenon");

            if (NeonLeft || NeonRight || NeonFront || NeonBack)
                parts.Add("Neons");

            return string.Join(", ", parts);
        }
    }
}
