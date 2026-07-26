using System.Collections.Generic;
using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for controlling AutoDrive functionality.
    /// Top level = AutoDrive actions, submenu = road types for the seek feature.
    /// </summary>
    public class AutoDriveMenu : HierarchicalMenuBase
    {
        #region Constants

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

        #endregion

        #region Fields

        private readonly AutoDriveManager _manager;
        private readonly List<string> _menuItems;

        #endregion

        #region Properties

        public AutoDriveManager Manager => _manager;

        #endregion

        #region Construction

        public AutoDriveMenu(AutoDriveManager manager, AudioManager audio) : base(audio)
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
        }

        #endregion

        #region Top Level - AutoDrive actions

        protected override int ItemCount => _menuItems.Count;

        protected override string GetItemText(int index)
        {
            // Show current driving style in the menu item
            if (index == ITEM_DRIVING_STYLE)
            {
                string styleName = Constants.GetDrivingStyleName(_manager.CurrentDrivingStyleMode);
                return $"Driving Style: {styleName}";
            }

            return _menuItems[index];
        }

        protected override void OnItemActivated(int index)
        {
            switch (index)
            {
                case ITEM_WANDER:
                    if (Game.Player.Character?.CurrentVehicle == null)
                    {
                        Speak("Not in a vehicle");
                        return;
                    }
                    _manager.StartWander();
                    break;
                case ITEM_WAYPOINT:
                    if (Game.Player.Character?.CurrentVehicle == null)
                    {
                        Speak("Not in a vehicle");
                        return;
                    }
                    _manager.StartWaypoint();
                    break;
                case ITEM_SEEK_ROAD:
                    EnterSubmenu();
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

        public override string GetMenuName()
        {
            if (InSubmenu)
            {
                return "Seek Road Type";
            }
            return "AutoDrive";
        }

        #endregion

        #region Submenu - seek road types

        protected override int SubmenuItemCount => Constants.ROAD_SEEK_MODE_NAMES.Length;

        protected override string GetSubmenuItemText(int index)
        {
            return Constants.GetRoadSeekModeName(index);
        }

        protected override void OnSubmenuItemActivated(int index)
        {
            // Execute seek with selected mode, then leave the submenu
            _manager.StartSeeking((RoadSeekMode)index);
            ExitSubmenu();
        }

        #endregion
    }
}
