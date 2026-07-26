using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Proxy menu for vehicle modifications.
    /// Creates/updates the actual VehicleModMenu when the player is in a vehicle
    /// and delegates every menu operation to it; speaks an explanatory message
    /// when not in a vehicle.
    /// </summary>
    public class VehicleModMenuProxy : MenuBase
    {
        #region Fields

        private readonly SettingsManager _settings;
        private readonly AudioManager _audio;
        private VehicleModMenu _modMenu;
        private int _lastVehicleHandle;  // Compare by Handle, not reference

        #endregion

        #region Construction

        public VehicleModMenuProxy(SettingsManager settings, AudioManager audio) : base(audio)
        {
            _settings = settings;
            _audio = audio;
            _modMenu = null;
            _lastVehicleHandle = 0;
        }

        #endregion

        #region MenuBase Overrides

        // The proxy has no items of its own - everything is delegated to the
        // real VehicleModMenu below. These satisfy the abstract contract.
        protected override int ItemCount => 0;

        protected override string EmptyMenuText => "Not in vehicle";

        protected override string GetItemText(int index) => EmptyMenuText;

        protected override void OnItemActivated(int index)
        {
        }

        public override string GetMenuName()
        {
            RefreshForCurrentVehicle();
            if (_modMenu != null)
            {
                return _modMenu.GetMenuName();
            }
            return "Vehicle Mods";
        }

        #endregion

        #region Delegation

        /// <summary>
        /// Check if player is in a vehicle and rebuild the mod menu if the vehicle changed.
        /// Clears the menu when the player has no vehicle.
        /// </summary>
        private void RefreshForCurrentVehicle()
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
            _modMenu = new VehicleModMenu(currentVehicle, _settings, _audio);
            _lastVehicleHandle = currentHandle;
        }

        public override void NavigatePrevious(bool fastScroll = false)
        {
            RefreshForCurrentVehicle();
            _modMenu?.NavigatePrevious(fastScroll);
        }

        public override void NavigateNext(bool fastScroll = false)
        {
            RefreshForCurrentVehicle();
            _modMenu?.NavigateNext(fastScroll);
        }

        public override string GetCurrentItemText()
        {
            RefreshForCurrentVehicle();
            if (_modMenu != null)
            {
                return _modMenu.GetCurrentItemText();
            }
            return EmptyMenuText;
        }

        public override void ExecuteSelection()
        {
            RefreshForCurrentVehicle();
            if (_modMenu != null)
            {
                _modMenu.ExecuteSelection();
            }
            else
            {
                Speak("You must be in a vehicle to use mods.");
            }
        }

        public override bool HasActiveSubmenu
        {
            get
            {
                RefreshForCurrentVehicle();
                return _modMenu != null && _modMenu.HasActiveSubmenu;
            }
        }

        public override void ExitSubmenu()
        {
            // Don't refresh - just exit existing submenu if any
            _modMenu?.ExitSubmenu();
        }

        #endregion
    }
}
