using System.Collections.Generic;
using GTA;
using DavyKager;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for controlling AutoDrive functionality
    /// Includes road seeking submenu for accessibility navigation
    /// </summary>
    public class AutoDriveMenu : IMenuState
    {
        private readonly AutoDriveManager _manager;
        private readonly List<string> _menuItems;
        private int _currentIndex;

        // Seek Road Type submenu state
        private bool _inSeekSubmenu;
        private int _seekSubmenuIndex;

        // Menu item indices
        private const int ITEM_WANDER = 0;
        private const int ITEM_WAYPOINT = 1;
        private const int ITEM_SEEK_ROAD = 2;
        private const int ITEM_DRIVING_STYLE = 3;
        private const int ITEM_CURRENT_ROAD = 4;
        private const int ITEM_STOP = 5;
        private const int ITEM_INCREASE_SPEED = 6;
        private const int ITEM_DECREASE_SPEED = 7;
        private const int ITEM_STATUS = 8;

        public AutoDriveManager Manager => _manager;

        public AutoDriveMenu(AutoDriveManager manager)
        {
            _manager = manager;

            _menuItems = new List<string>
            {
                "Start Wander Mode",
                "Drive to Waypoint",
                "Seek Road Type",
                "Driving Style",
                "Current Road Type",
                "Stop AutoDrive",
                "Increase Speed",
                "Decrease Speed",
                "Current Status"
            };

            _currentIndex = 0;
            _inSeekSubmenu = false;
            _seekSubmenuIndex = 0;
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            if (_inSeekSubmenu)
            {
                // Navigate seek submenu
                int count = Constants.ROAD_SEEK_MODE_NAMES.Length;
                if (_seekSubmenuIndex > 0)
                    _seekSubmenuIndex--;
                else
                    _seekSubmenuIndex = count - 1;
            }
            else
            {
                // Navigate main menu
                if (_currentIndex > 0)
                    _currentIndex--;
                else
                    _currentIndex = _menuItems.Count - 1;
            }
        }

        public void NavigateNext(bool fastScroll = false)
        {
            if (_inSeekSubmenu)
            {
                // Navigate seek submenu
                int count = Constants.ROAD_SEEK_MODE_NAMES.Length;
                if (_seekSubmenuIndex < count - 1)
                    _seekSubmenuIndex++;
                else
                    _seekSubmenuIndex = 0;
            }
            else
            {
                // Navigate main menu
                if (_currentIndex < _menuItems.Count - 1)
                    _currentIndex++;
                else
                    _currentIndex = 0;
            }
        }

        public string GetCurrentItemText()
        {
            if (_inSeekSubmenu)
            {
                return Constants.GetRoadSeekModeName(_seekSubmenuIndex);
            }

            // Show current driving style in the menu item
            if (_currentIndex == ITEM_DRIVING_STYLE)
            {
                string styleName = Constants.GetDrivingStyleName(_manager.CurrentDrivingStyleMode);
                return $"Driving Style: {styleName}";
            }

            return _menuItems[_currentIndex];
        }

        public string GetMenuName()
        {
            if (_inSeekSubmenu)
            {
                return "Seek Road Type";
            }
            return "AutoDrive";
        }

        public bool HasActiveSubmenu => _inSeekSubmenu;

        public void ExitSubmenu()
        {
            _inSeekSubmenu = false;
            _seekSubmenuIndex = 0;
        }

        public void ExecuteSelection()
        {
            if (_inSeekSubmenu)
            {
                // Execute seek with selected mode
                _manager.StartSeeking(_seekSubmenuIndex);
                _inSeekSubmenu = false;
                return;
            }

            switch (_currentIndex)
            {
                case ITEM_WANDER:
                    if (Game.Player.Character?.CurrentVehicle == null)
                    {
                        Tolk.Speak("Not in a vehicle");
                        return;
                    }
                    _manager.StartWander();
                    break;
                case ITEM_WAYPOINT:
                    if (Game.Player.Character?.CurrentVehicle == null)
                    {
                        Tolk.Speak("Not in a vehicle");
                        return;
                    }
                    _manager.StartWaypoint();
                    break;
                case ITEM_SEEK_ROAD:
                    // Enter seek submenu
                    _inSeekSubmenu = true;
                    _seekSubmenuIndex = 0;
                    break;
                case ITEM_DRIVING_STYLE:
                    _manager.CycleDrivingStyle();
                    break;
                case ITEM_CURRENT_ROAD:
                    _manager.AnnounceCurrentRoadType();
                    break;
                case ITEM_STOP:
                    _manager.Stop();
                    break;
                case ITEM_INCREASE_SPEED:
                    _manager.IncreaseSpeed();
                    break;
                case ITEM_DECREASE_SPEED:
                    _manager.DecreaseSpeed();
                    break;
                case ITEM_STATUS:
                    _manager.AnnounceStatus();
                    break;
            }
        }
    }
}
