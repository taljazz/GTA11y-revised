using System.Collections.Generic;
using System.Windows.Forms;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Translates physical key presses into AccessibilityCommand values based on
    /// the active hotkey layout. Two layouts exist:
    ///
    ///   Numpad  - the classic layout on the numeric keypad.
    ///   Letters - for keyboards without a numpad. Mirrors the numpad's shape on
    ///             the right hand: U/I/O sit above J/K/L exactly like 7/8/9 sit
    ///             above 1/2/3, with Y for location, semicolon for back, and the
    ///             bracket/apostrophe keys for the remaining scans.
    ///
    /// Every letter chosen here is unbound in GTA V single player (verified
    /// against the default PC key bindings), so the letter layout never fights
    /// the game. The active layout persists via the "hotkeyLayout" setting and
    /// toggles with F9 - a key GTA V, ScriptHookV, and SHVDN all leave free.
    /// </summary>
    public class HotkeyMapper
    {
        #region Constants

        /// <summary>Setting id that stores the active layout.</summary>
        public const string LAYOUT_SETTING_ID = "hotkeyLayout";

        /// <summary>The key that switches layouts (unbound in GTA V; F10 is avoided because Windows treats it as a system key).</summary>
        public const Keys LAYOUT_TOGGLE_KEY = Keys.F9;

        // Classic numpad layout
        private static readonly Dictionary<Keys, AccessibilityCommand> NumpadMap = new Dictionary<Keys, AccessibilityCommand>
        {
            { Keys.NumPad0, AccessibilityCommand.LocationInfo },
            { Keys.NumPad1, AccessibilityCommand.MenuPreviousItem },
            { Keys.NumPad2, AccessibilityCommand.MenuSelect },
            { Keys.NumPad3, AccessibilityCommand.MenuNextItem },
            { Keys.NumPad4, AccessibilityCommand.ScanVehicles },
            { Keys.NumPad5, AccessibilityCommand.ScanDoors },
            { Keys.NumPad6, AccessibilityCommand.ScanPedestrians },
            { Keys.NumPad7, AccessibilityCommand.MenuPrevious },
            { Keys.NumPad8, AccessibilityCommand.ScanObjects },
            { Keys.NumPad9, AccessibilityCommand.MenuNext },
            { Keys.Decimal, AccessibilityCommand.Back }
        };

        // Letter layout for keyboards without a numpad.
        // All keys verified unbound in GTA V single player:
        // comma/period (radio), M (interaction menu), Z (radar zoom), G (throwable),
        // H (headlights), N/T/B (online chat and gestures) are deliberately avoided.
        private static readonly Dictionary<Keys, AccessibilityCommand> LetterMap = new Dictionary<Keys, AccessibilityCommand>
        {
            { Keys.Y, AccessibilityCommand.LocationInfo },
            { Keys.J, AccessibilityCommand.MenuPreviousItem },
            { Keys.K, AccessibilityCommand.MenuSelect },
            { Keys.L, AccessibilityCommand.MenuNextItem },
            { Keys.OemOpenBrackets, AccessibilityCommand.ScanVehicles },
            { Keys.OemCloseBrackets, AccessibilityCommand.ScanDoors },
            { Keys.OemQuotes, AccessibilityCommand.ScanPedestrians },
            { Keys.U, AccessibilityCommand.MenuPrevious },
            { Keys.I, AccessibilityCommand.ScanObjects },
            { Keys.O, AccessibilityCommand.MenuNext },
            { Keys.OemSemicolon, AccessibilityCommand.Back }
        };

        // Spoken key names per command, used by the Help menu and announcements
        private static readonly Dictionary<AccessibilityCommand, string> NumpadLabels = new Dictionary<AccessibilityCommand, string>
        {
            { AccessibilityCommand.LocationInfo, "NumPad 0" },
            { AccessibilityCommand.MenuPreviousItem, "NumPad 1" },
            { AccessibilityCommand.MenuSelect, "NumPad 2" },
            { AccessibilityCommand.MenuNextItem, "NumPad 3" },
            { AccessibilityCommand.ScanVehicles, "NumPad 4" },
            { AccessibilityCommand.ScanDoors, "NumPad 5" },
            { AccessibilityCommand.ScanPedestrians, "NumPad 6" },
            { AccessibilityCommand.MenuPrevious, "NumPad 7" },
            { AccessibilityCommand.ScanObjects, "NumPad 8" },
            { AccessibilityCommand.MenuNext, "NumPad 9" },
            { AccessibilityCommand.Back, "NumPad Decimal" }
        };

        private static readonly Dictionary<AccessibilityCommand, string> LetterLabels = new Dictionary<AccessibilityCommand, string>
        {
            { AccessibilityCommand.LocationInfo, "Y" },
            { AccessibilityCommand.MenuPreviousItem, "J" },
            { AccessibilityCommand.MenuSelect, "K" },
            { AccessibilityCommand.MenuNextItem, "L" },
            { AccessibilityCommand.ScanVehicles, "Left Bracket" },
            { AccessibilityCommand.ScanDoors, "Right Bracket" },
            { AccessibilityCommand.ScanPedestrians, "Apostrophe" },
            { AccessibilityCommand.MenuPrevious, "U" },
            { AccessibilityCommand.ScanObjects, "I" },
            { AccessibilityCommand.MenuNext, "O" },
            { AccessibilityCommand.Back, "Semicolon" }
        };

        #endregion

        #region Fields

        private readonly SettingsManager _settings;

        #endregion

        #region Construction

        public HotkeyMapper(SettingsManager settings)
        {
            _settings = settings;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The active layout, read live from settings so changes made in the
        /// Settings menu apply immediately without a restart.
        /// </summary>
        public HotkeyLayout CurrentLayout =>
            (HotkeyLayout)(_settings?.GetIntSetting(LAYOUT_SETTING_ID, 0, 1) ?? 0);

        /// <summary>Spoken name of the active layout.</summary>
        public string LayoutName =>
            CurrentLayout == HotkeyLayout.Numpad ? "Numpad" : "Letter keys";

        #endregion

        #region Public API

        /// <summary>
        /// Map a pressed key to its command under the active layout.
        /// Returns AccessibilityCommand.None for keys the layout doesn't use,
        /// so the game and other mods see those keys untouched.
        /// </summary>
        public AccessibilityCommand GetCommand(Keys key)
        {
            var map = CurrentLayout == HotkeyLayout.Numpad ? NumpadMap : LetterMap;
            return map.TryGetValue(key, out AccessibilityCommand command)
                ? command
                : AccessibilityCommand.None;
        }

        /// <summary>
        /// Switch to the other layout, persist the choice, and return the new layout.
        /// </summary>
        public HotkeyLayout ToggleLayout()
        {
            HotkeyLayout newLayout = CurrentLayout == HotkeyLayout.Numpad
                ? HotkeyLayout.Letters
                : HotkeyLayout.Numpad;

            if (_settings != null)
            {
                _settings.SetIntSetting(LAYOUT_SETTING_ID, (int)newLayout);
                _settings.SaveSettings();
            }

            return newLayout;
        }

        /// <summary>
        /// Spoken key name for a command under the active layout (for help text).
        /// </summary>
        public string GetKeyLabel(AccessibilityCommand command)
        {
            var labels = CurrentLayout == HotkeyLayout.Numpad ? NumpadLabels : LetterLabels;
            return labels.TryGetValue(command, out string label) ? label : "Unknown";
        }

        #endregion
    }
}
