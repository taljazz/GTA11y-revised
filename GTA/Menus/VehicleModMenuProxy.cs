using GTA;
using DavyKager;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Proxy menu for vehicle modifications
    /// Creates/updates the actual VehicleModMenu when player is in a vehicle
    /// Shows appropriate message when not in a vehicle
    /// </summary>
    public class VehicleModMenuProxy : IMenuState
    {
        private readonly SettingsManager _settings;
        private VehicleModMenu _modMenu;
        private int _lastVehicleHandle;  // Compare by Handle, not reference

        public VehicleModMenuProxy(SettingsManager settings)
        {
            _settings = settings;
            _modMenu = null;
            _lastVehicleHandle = 0;
        }

        /// <summary>
        /// Check if player is in a vehicle and update the mod menu if needed
        /// </summary>
        private void UpdateModMenu()
        {
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
            {
                _modMenu = null;
                _lastVehicleHandle = 0;
                return;
            }

            Vehicle currentVehicle = player.CurrentVehicle;

            if (currentVehicle == null)
            {
                _modMenu = null;
                _lastVehicleHandle = 0;
                return;
            }

            // Compare by Handle (integer) - SHVDN returns new wrapper objects each call
            int currentHandle = currentVehicle.Handle;
            if (_lastVehicleHandle == currentHandle && _modMenu != null)
            {
                return;
            }

            // Create new mod menu for current vehicle
            _modMenu = new VehicleModMenu(currentVehicle, _settings);
            _lastVehicleHandle = currentHandle;
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            UpdateModMenu();
            if (_modMenu != null)
            {
                _modMenu.NavigatePrevious(fastScroll);
            }
        }

        public void NavigateNext(bool fastScroll = false)
        {
            UpdateModMenu();
            if (_modMenu != null)
            {
                _modMenu.NavigateNext(fastScroll);
            }
        }

        public string GetCurrentItemText()
        {
            UpdateModMenu();
            if (_modMenu != null)
            {
                return _modMenu.GetCurrentItemText();
            }
            return "Not in vehicle";
        }

        public void ExecuteSelection()
        {
            UpdateModMenu();
            if (_modMenu != null)
            {
                _modMenu.ExecuteSelection();
            }
            else
            {
                Tolk.Speak("You must be in a vehicle to use mods.");
            }
        }

        public string GetMenuName()
        {
            // Check if player left vehicle - reset menu state if so
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
            {
                _modMenu = null;
                _lastVehicleHandle = 0;
                return "Vehicle Mods";
            }

            Vehicle currentVehicle = player.CurrentVehicle;
            if (currentVehicle == null && _modMenu != null)
            {
                _modMenu = null;
                _lastVehicleHandle = 0;
            }

            if (_modMenu != null)
            {
                return _modMenu.GetMenuName();
            }
            return "Vehicle Mods";
        }

        public bool HasActiveSubmenu
        {
            get
            {
                // Check if player left vehicle - reset menu state if so
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                {
                    _modMenu = null;
                    _lastVehicleHandle = 0;
                    return false;
                }

                Vehicle currentVehicle = player.CurrentVehicle;
                if (currentVehicle == null && _modMenu != null)
                {
                    _modMenu = null;
                    _lastVehicleHandle = 0;
                    return false;
                }

                return _modMenu != null && _modMenu.HasActiveSubmenu;
            }
        }

        public void ExitSubmenu()
        {
            // Don't call UpdateModMenu - just exit existing submenu if any
            if (_modMenu != null)
            {
                _modMenu.ExitSubmenu();
            }
        }
    }
}
