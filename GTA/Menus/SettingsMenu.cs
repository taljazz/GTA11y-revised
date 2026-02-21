using System.Collections.Generic;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for toggling mod settings
    /// Supports both boolean (toggle) and int (cycle) settings
    /// </summary>
    public class SettingsMenu : IMenuState
    {
        private readonly SettingsManager _settings;
        private readonly List<string> _settingIds;
        private int _currentIndex;

        public SettingsMenu(SettingsManager settings)
        {
            _settings = settings;
            _settingIds = _settings.GetAllSettingIds();
            _currentIndex = 0;
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            int step = fastScroll ? 5 : 1;
            _currentIndex -= step;
            if (_currentIndex < 0)
                _currentIndex = (((_currentIndex % _settingIds.Count) + _settingIds.Count) % _settingIds.Count);
        }

        public void NavigateNext(bool fastScroll = false)
        {
            int step = fastScroll ? 5 : 1;
            _currentIndex += step;
            if (_currentIndex >= _settingIds.Count)
                _currentIndex = _currentIndex % _settingIds.Count;
        }

        public string GetCurrentItemText()
        {
            // Defensive: Validate settings and indices
            if (_settingIds == null || _settingIds.Count == 0)
                return "(no settings)";

            if (_currentIndex < 0 || _currentIndex >= _settingIds.Count)
                _currentIndex = 0;

            if (_settings == null)
                return "(settings unavailable)";

            string settingId = _settingIds[_currentIndex];
            string displayName = _settings.GetDisplayName(settingId);

            // Handle int settings differently
            if (_settings.IsIntSetting(settingId))
            {
                string valueName = _settings.GetIntSettingValueName(settingId);
                return $"{displayName}: {valueName}";
            }
            else
            {
                string toggleState = _settings.GetSetting(settingId) ? "On" : "Off";
                return $"{displayName} {toggleState}";
            }
        }

        public void ExecuteSelection()
        {
            // Defensive: Validate settings and indices
            if (_settingIds == null || _settingIds.Count == 0 || _settings == null)
                return;

            if (_currentIndex < 0 || _currentIndex >= _settingIds.Count)
                _currentIndex = 0;

            string settingId = _settingIds[_currentIndex];
            string displayName = _settings.GetDisplayName(settingId);
            string message;

            // Handle int settings (cycle) vs boolean settings (toggle)
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

            DavyKager.Tolk.Speak(message);
        }

        public string GetMenuName()
        {
            return "Settings";
        }

        public bool HasActiveSubmenu => false;

        public void ExitSubmenu()
        {
            // No submenu - do nothing
        }
    }
}
