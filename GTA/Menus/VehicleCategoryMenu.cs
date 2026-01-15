using System.Collections.Generic;
using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for selecting vehicle categories, with submenus for each category
    /// Categories are based on GTA V VehicleClass enum, plus special categories like Weaponized
    /// </summary>
    public class VehicleCategoryMenu : IMenuState
    {
        private readonly SettingsManager _settings;
        private readonly List<VehicleCategory> _categories;
        private int _currentCategoryIndex;

        // Submenu state
        private bool _inSubmenu;
        private VehicleSpawnMenu _currentSubmenu;

        /// <summary>
        /// Represents a vehicle category with display name
        /// Supports both VehicleClass-based and special name-set-based categories
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

        public VehicleCategoryMenu(SettingsManager settings)
        {
            _settings = settings;
            _inSubmenu = false;
            _currentSubmenu = null;

            // Initialize categories in a user-friendly order
            // Special categories first for easy access
            _categories = new List<VehicleCategory>
            {
                // Special category - Weaponized vehicles (armed with guns/missiles)
                new VehicleCategory("Weaponized"),

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

            _currentCategoryIndex = 0;
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                // Navigate within submenu
                _currentSubmenu.NavigatePrevious(fastScroll);
            }
            else
            {
                // Navigate categories
                if (_currentCategoryIndex > 0)
                    _currentCategoryIndex--;
                else
                    _currentCategoryIndex = _categories.Count - 1;
            }
        }

        public void NavigateNext(bool fastScroll = false)
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                // Navigate within submenu
                _currentSubmenu.NavigateNext(fastScroll);
            }
            else
            {
                // Navigate categories
                if (_currentCategoryIndex < _categories.Count - 1)
                    _currentCategoryIndex++;
                else
                    _currentCategoryIndex = 0;
            }
        }

        public string GetCurrentItemText()
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                return _currentSubmenu.GetCurrentItemText();
            }
            else
            {
                // Defensive: Validate category index
                if (_categories == null || _categories.Count == 0)
                    return "(no categories)";

                if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count)
                    _currentCategoryIndex = 0;

                VehicleCategory category = _categories[_currentCategoryIndex];
                return category.Name;
            }
        }

        public void ExecuteSelection()
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                // Spawn the selected vehicle
                _currentSubmenu.ExecuteSelection();
            }
            else
            {
                // Defensive: Validate category index
                if (_categories == null || _categories.Count == 0)
                    return;

                if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count)
                    _currentCategoryIndex = 0;

                // Enter submenu for current category
                VehicleCategory category = _categories[_currentCategoryIndex];

                if (category.IsSpecial)
                {
                    // Special category - use name-based filtering
                    if (category.Name == "Weaponized")
                    {
                        _currentSubmenu = new VehicleSpawnMenu(_settings, Constants.WEAPONIZED_VEHICLE_NAMES, category.Name);
                    }
                    // Add more special categories here as needed
                }
                else
                {
                    // Standard category - use VehicleClass filtering
                    _currentSubmenu = new VehicleSpawnMenu(_settings, category.Class, category.Name);
                }

                _inSubmenu = true;
            }
        }

        public string GetMenuName()
        {
            if (_inSubmenu && _currentSubmenu != null)
            {
                return _currentSubmenu.GetMenuName();
            }
            return "Spawn Vehicle";
        }

        public bool HasActiveSubmenu => _inSubmenu;

        public void ExitSubmenu()
        {
            if (_inSubmenu)
            {
                _inSubmenu = false;
                _currentSubmenu = null;
            }
        }
    }
}
