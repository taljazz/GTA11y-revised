using System.Collections.Generic;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for toggling mod settings.
    /// Supports both boolean (toggle) and int (cycle) settings.
    /// </summary>
    public class SettingsMenu : MenuBase
    {
        #region Fields

        private readonly SettingsManager _settings;
        private readonly List<string> _settingIds;

        #endregion

        #region Construction

        public SettingsMenu(SettingsManager settings, AudioManager audio) : base(audio)
        {
            _settings = settings;
            _settingIds = _settings.GetAllSettingIds();
        }

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount => _settingIds?.Count ?? 0;

        protected override string EmptyMenuText => "(no settings)";

        protected override string GetItemText(int index)
        {
            if (_settings == null)
                return "(settings unavailable)";

            string settingId = _settingIds[index];
            string displayName = _settings.GetDisplayName(settingId);

            // Int settings show their current value name; bool settings show On/Off
            if (_settings.IsIntSetting(settingId))
            {
                string valueName = _settings.GetIntSettingValueName(settingId);
                return $"{displayName}: {valueName}";
            }

            string toggleState = _settings.GetSetting(settingId) ? "On" : "Off";
            return $"{displayName} {toggleState}";
        }

        protected override void OnItemActivated(int index)
        {
            if (_settings == null)
                return;

            string settingId = _settingIds[index];
            string displayName = _settings.GetDisplayName(settingId);
            string message;

            // Int settings cycle through values; bool settings toggle
            if (_settings.IsIntSetting(settingId))
            {
                _settings.CycleIntSetting(settingId);
                _settings.SaveSettings();

                string valueName = _settings.GetIntSettingValueName(settingId);
                message = $"{displayName}: {valueName}";
            }
            else
            {
                bool newValue = _settings.ToggleSetting(settingId);
                _settings.SaveSettings();

                message = newValue ? $"{displayName} On!" : $"{displayName} Off!";
            }

            Speak(message);
        }

        public override string GetMenuName()
        {
            return "Settings";
        }

        #endregion
    }
}
