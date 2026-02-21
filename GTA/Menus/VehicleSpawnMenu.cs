using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for spawning vehicles with optional category filtering
    /// </summary>
    public class VehicleSpawnMenu : IMenuState
    {
        // Cached VehicleHash array to avoid repeated Enum.GetValues allocations
        private static readonly VehicleHash[] AllVehicleHashes = (VehicleHash[])Enum.GetValues(typeof(VehicleHash));

        private readonly List<VehicleSpawn> _vehicles;
        private readonly SettingsManager _settings;
        private readonly VehicleClass? _filterClass;
        private readonly HashSet<string> _filterNames;
        private readonly string _categoryName;
        private int _currentIndex;

        /// <summary>
        /// Create menu with all vehicles (legacy constructor)
        /// </summary>
        public VehicleSpawnMenu(SettingsManager settings) : this(settings, (VehicleClass?)null, null)
        {
        }

        /// <summary>
        /// Create menu filtered by vehicle class
        /// </summary>
        public VehicleSpawnMenu(SettingsManager settings, VehicleClass? filterClass, string categoryName)
        {
            _settings = settings;
            _filterClass = filterClass;
            _filterNames = null;
            _categoryName = categoryName ?? "All Vehicles";
            _vehicles = new List<VehicleSpawn>();

            // Load vehicles, optionally filtered by class
            foreach (VehicleHash vh in AllVehicleHashes)
            {
                // If filtering, check if vehicle matches the class
                if (_filterClass.HasValue)
                {
                    VehicleClass vehicleClass = GetVehicleClass(vh);
                    if (vehicleClass != _filterClass.Value)
                        continue;
                }

                string displayName = Game.GetLocalizedString(Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, vh));

                // Skip vehicles with empty or null names
                if (string.IsNullOrWhiteSpace(displayName) || displayName == "NULL")
                    continue;

                _vehicles.Add(new VehicleSpawn(displayName, vh));
            }

            _vehicles.Sort();
            _currentIndex = 0;
        }

        /// <summary>
        /// Create menu filtered by a set of vehicle model names (for special categories like Weaponized)
        /// </summary>
        public VehicleSpawnMenu(SettingsManager settings, HashSet<string> filterNames, string categoryName)
        {
            _settings = settings;
            _filterClass = null;
            _filterNames = filterNames;
            _categoryName = categoryName ?? "Special Vehicles";
            _vehicles = new List<VehicleSpawn>();

            // Load vehicles filtered by name set
            foreach (VehicleHash vh in AllVehicleHashes)
            {
                // Get the enum name (e.g., "Oppressor2", "Deluxo")
                string enumName = vh.ToString();

                // Check if this vehicle is in the filter set
                if (!_filterNames.Contains(enumName))
                    continue;

                string displayName = Game.GetLocalizedString(Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, vh));

                // Skip vehicles with empty or null names
                if (string.IsNullOrWhiteSpace(displayName) || displayName == "NULL")
                    continue;

                // Get the vehicle class for display in special categories
                int vehicleClassIndex = Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, (int)vh);
                string className = null;
                if (vehicleClassIndex >= 0 && vehicleClassIndex < Constants.VEHICLE_CLASS_NAMES.Length)
                {
                    className = Constants.VEHICLE_CLASS_NAMES[vehicleClassIndex];
                }

                _vehicles.Add(new VehicleSpawn(displayName, vh, className));
            }

            _vehicles.Sort();
            _currentIndex = 0;
        }

        /// <summary>
        /// Get the VehicleClass for a VehicleHash
        /// </summary>
        private VehicleClass GetVehicleClass(VehicleHash hash)
        {
            // Use native function to get vehicle class
            return (VehicleClass)Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, (int)hash);
        }

        /// <summary>
        /// Get the number of vehicles in this menu
        /// </summary>
        public int VehicleCount => _vehicles.Count;

        public void NavigatePrevious(bool fastScroll = false)
        {
            int step = fastScroll ? Constants.VEHICLE_SPAWN_FAST_SCROLL_AMOUNT : 1;

            if (_currentIndex >= step)
            {
                _currentIndex -= step;
            }
            else
            {
                // Wrap around
                int remainder = _currentIndex;
                _currentIndex = _vehicles.Count - 1 - remainder;
            }
        }

        public void NavigateNext(bool fastScroll = false)
        {
            int step = fastScroll ? Constants.VEHICLE_SPAWN_FAST_SCROLL_AMOUNT : 1;

            if (_currentIndex < _vehicles.Count - step)
            {
                _currentIndex += step;
            }
            else
            {
                // Wrap around
                int remainder = _vehicles.Count - 1 - _currentIndex;
                _currentIndex = remainder;
            }
        }

        public string GetCurrentItemText()
        {
            if (_vehicles.Count == 0)
                return "(empty)";

            VehicleSpawn vehicle = _vehicles[_currentIndex];
            if (!string.IsNullOrEmpty(vehicle.vehicleClassName))
            {
                return $"{_currentIndex + 1} of {_vehicles.Count}: {vehicle.name}, {vehicle.vehicleClassName}";
            }
            return $"{_currentIndex + 1} of {_vehicles.Count}: {vehicle.name}";
        }

        public void ExecuteSelection()
        {
            if (_vehicles.Count == 0)
                return;

            // Defensive: Validate current index
            if (_currentIndex < 0 || _currentIndex >= _vehicles.Count)
            {
                _currentIndex = 0;
                return;
            }

            Ped player = Game.Player.Character;

            // Defensive: Validate player
            if (player == null || !player.Exists())
            {
                Logger.Warning("VehicleSpawnMenu: Player is null or doesn't exist");
                return;
            }

            VehicleHash vehicleHash = _vehicles[_currentIndex].id;

            try
            {
                // Spawn vehicle in front of player
                Vehicle vehicle = World.CreateVehicle(
                    vehicleHash,
                    player.Position + player.ForwardVector * 2.0f,
                    player.Heading + 90
                );

                // Check for null - World.CreateVehicle returns null if entity pool is full
                if (vehicle == null)
                {
                    Logger.Warning($"Failed to spawn vehicle {vehicleHash} - entity pool may be full");
                    DavyKager.Tolk.Speak("Failed to spawn vehicle");
                    return;
                }

                vehicle.PlaceOnGround();
                DavyKager.Tolk.Speak($"Spawned {_vehicles[_currentIndex].name}");

                // Warp player inside if setting enabled
                if (_settings != null && _settings.GetSetting("warpInsideVehicle"))
                {
                    player.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "VehicleSpawnMenu.ExecuteSelection");
                DavyKager.Tolk.Speak("Failed to spawn vehicle");
            }
        }

        public string GetMenuName()
        {
            return _filterClass.HasValue ? _categoryName : "Spawn Vehicle";
        }

        public bool HasActiveSubmenu => false;

        public void ExitSubmenu()
        {
            // No submenu - do nothing
        }
    }
}
