using System.Collections.Generic;
using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for selecting vehicle categories, with a full VehicleSpawnMenu as the
    /// submenu for each category. Unlike the other hierarchical menus, the submenu
    /// here is a complete child menu object, so this class derives from MenuBase
    /// and overrides the navigation methods to delegate to the active child.
    /// </summary>
    public class VehicleCategoryMenu : MenuBase
    {
        #region Types

        /// <summary>
        /// Represents a vehicle category with display name.
        /// Supports both VehicleClass-based and special name-set-based categories.
        /// </summary>
        private class VehicleCategory
        {
            public string Name { get; }
            public VehicleClass? Class { get; }
            public bool IsSpecial { get; }  // True for special categories like Weaponized

            // Standard category based on VehicleClass
            public VehicleCategory(string name, VehicleClass vehicleClass)
            {
                Name = name;
                Class = vehicleClass;
                IsSpecial = false;
            }

            // Special category (uses name-based filtering)
            public VehicleCategory(string name)
            {
                Name = name;
                Class = null;
                IsSpecial = true;
            }
        }

        #endregion

        #region Fields

        private readonly SettingsManager _settings;
        private readonly AudioManager _audio;
        private readonly List<VehicleCategory> _categories;

        // Submenu state - a full child menu per category
        private bool _inSubmenu;
        private VehicleSpawnMenu _currentSubmenu;

        // Cache constructed VehicleSpawnMenu instances to avoid reconstructing on every submenu entry
        private readonly Dictionary<string, VehicleSpawnMenu> _submenuCache = new Dictionary<string, VehicleSpawnMenu>();

        #endregion

        #region Construction

        public VehicleCategoryMenu(SettingsManager settings, AudioManager audio) : base(audio)
        {
            _settings = settings;
            _audio = audio;
            _inSubmenu = false;
            _currentSubmenu = null;

            // Initialize categories in a user-friendly order
            // Special categories first for easy access
            _categories = new List<VehicleCategory>
            {
                // Special category - Weaponized vehicles (armed with guns/missiles)
                new VehicleCategory("Weaponized"),

                // Catch-all. The class categories below only reach a vehicle if
                // the game reports a class that matches one of them; anything
                // that reports something unexpected would otherwise be in no
                // category at all and impossible to spawn. This one filters on
                // nothing, so every model in the game is reachable from here.
                new VehicleCategory("All Vehicles"),

                // Standard categories
                new VehicleCategory("Super Cars", VehicleClass.Super),
                new VehicleCategory("Sports Cars", VehicleClass.Sports),
                new VehicleCategory("Sports Classics", VehicleClass.SportsClassics),
                new VehicleCategory("Muscle Cars", VehicleClass.Muscle),
                new VehicleCategory("Coupes", VehicleClass.Coupes),
                new VehicleCategory("Sedans", VehicleClass.Sedans),
                new VehicleCategory("Compacts", VehicleClass.Compacts),
                new VehicleCategory("SUVs", VehicleClass.SUVs),
                new VehicleCategory("Off-Road", VehicleClass.OffRoad),
                new VehicleCategory("Motorcycles", VehicleClass.Motorcycles),
                new VehicleCategory("Cycles (Bicycles)", VehicleClass.Cycles),
                new VehicleCategory("Vans", VehicleClass.Vans),
                new VehicleCategory("Commercial", VehicleClass.Commercial),
                new VehicleCategory("Industrial", VehicleClass.Industrial),
                new VehicleCategory("Service", VehicleClass.Service),
                new VehicleCategory("Utility", VehicleClass.Utility),
                new VehicleCategory("Emergency", VehicleClass.Emergency),
                new VehicleCategory("Military", VehicleClass.Military),
                new VehicleCategory("Planes", VehicleClass.Planes),
                new VehicleCategory("Helicopters", VehicleClass.Helicopters),
                new VehicleCategory("Boats", VehicleClass.Boats),
                new VehicleCategory("Open Wheel", VehicleClass.OpenWheel),
                new VehicleCategory("Trains", VehicleClass.Trains)
            };
        }

        #endregion

        #region MenuBase Overrides - category level

        protected override int ItemCount => _categories.Count;

        protected override string EmptyMenuText => "(no categories)";

        protected override string GetItemText(int index)
        {
            return _categories[index].Name;
        }

        protected override void OnItemActivated(int index)
        {
            // Enter submenu for current category, building it on first use
            VehicleCategory category = _categories[index];

            if (!_submenuCache.TryGetValue(category.Name, out _currentSubmenu))
            {
                if (category.IsSpecial)
                {
                    // Special category - use name-based filtering
                    if (category.Name == "Weaponized")
                    {
                        _currentSubmenu = new VehicleSpawnMenu(_settings, Constants.WEAPONIZED_VEHICLE_NAMES, category.Name, _audio);
                    }
                    else if (category.Name == "All Vehicles")
                    {
                        // No class filter at all - every model the game defines
                        _currentSubmenu = new VehicleSpawnMenu(_settings, (VehicleClass?)null, category.Name, _audio);
                    }
                    // Add more special categories here as needed
                }
                else
                {
                    // Standard category - use VehicleClass filtering
                    _currentSubmenu = new VehicleSpawnMenu(_settings, category.Class, category.Name, _audio);
                }

                if (_currentSubmenu != null)
                    _submenuCache[category.Name] = _currentSubmenu;
            }

            _inSubmenu = true;
        }

        public override string GetMenuName()
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                return _currentSubmenu.GetMenuName();
            }
            return "Spawn Vehicle";
        }

        #endregion

        #region Child Menu Delegation

        public override void NavigatePrevious(bool fastScroll = false)
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                _currentSubmenu.NavigatePrevious(fastScroll);
                return;
            }
            base.NavigatePrevious(fastScroll);
        }

        public override void NavigateNext(bool fastScroll = false)
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                _currentSubmenu.NavigateNext(fastScroll);
                return;
            }
            base.NavigateNext(fastScroll);
        }

        public override string GetCurrentItemText()
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                return _currentSubmenu.GetCurrentItemText();
            }
            return base.GetCurrentItemText();
        }

        public override void ExecuteSelection()
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                // Spawn the selected vehicle
                _currentSubmenu.ExecuteSelection();
                return;
            }
            base.ExecuteSelection();
        }

        public override bool HasActiveSubmenu => _inSubmenu;

        public override void ExitSubmenu()
        {
            if (_inSubmenu)
            {
                _inSubmenu = false;
                _currentSubmenu = null;
            }
        }

        #endregion
    }
}
