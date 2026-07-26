using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for spawning vehicles with optional category filtering.
    /// Demonstrates overriding FastScrollStep for large lists.
    /// </summary>
    public class VehicleSpawnMenu : MenuBase
    {
        #region Fields

        // Cached VehicleHash array to avoid repeated Enum.GetValues allocations
        private static readonly VehicleHash[] AllVehicleHashes = (VehicleHash[])Enum.GetValues(typeof(VehicleHash));

        private readonly List<VehicleSpawn> _vehicles;
        private readonly SettingsManager _settings;
        private readonly VehicleClass? _filterClass;
        private readonly HashSet<string> _filterNames;
        private readonly string _categoryName;

        #endregion

        #region Construction

        /// <summary>
        /// Create menu with all vehicles (legacy constructor)
        /// </summary>
        public VehicleSpawnMenu(SettingsManager settings, AudioManager audio)
            : this(settings, (VehicleClass?)null, null, audio)
        {
        }

        /// <summary>
        /// Create menu filtered by vehicle class
        /// </summary>
        public VehicleSpawnMenu(SettingsManager settings, VehicleClass? filterClass, string categoryName, AudioManager audio)
            : base(audio)
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
        }

        /// <summary>
        /// Create menu filtered by a set of vehicle model names (for special categories like Weaponized)
        /// </summary>
        public VehicleSpawnMenu(SettingsManager settings, HashSet<string> filterNames, string categoryName, AudioManager audio)
            : base(audio)
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
        }

        #endregion

        #region Properties

        /// <summary>
        /// Get the number of vehicles in this menu
        /// </summary>
        public int VehicleCount => _vehicles.Count;

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount => _vehicles.Count;

        protected override int FastScrollStep => Constants.VEHICLE_SPAWN_FAST_SCROLL_AMOUNT;

        protected override string GetItemText(int index)
        {
            VehicleSpawn vehicle = _vehicles[index];
            if (!string.IsNullOrEmpty(vehicle.vehicleClassName))
            {
                return $"{index + 1} of {_vehicles.Count}: {vehicle.name}, {vehicle.vehicleClassName}";
            }
            return $"{index + 1} of {_vehicles.Count}: {vehicle.name}";
        }

        protected override void OnItemActivated(int index)
        {
            Ped player = Game.Player.Character;

            // Defensive: Validate player
            if (player == null || !player.Exists())
            {
                Logger.Warning("VehicleSpawnMenu: Player is null or doesn't exist");
                return;
            }

            VehicleHash vehicleHash = _vehicles[index].id;

            try
            {
                // Spawn vehicle in front of player
                Vehicle vehicle = Vehicle.Create(
                    vehicleHash,
                    player.Position + player.ForwardVector * 2.0f,
                    player.Heading + 90
                );

                // Check for null - Vehicle.Create returns null if entity pool is full
                if (vehicle == null)
                {
                    Logger.Warning($"Failed to spawn vehicle {vehicleHash} - entity pool may be full");
                    Speak("Failed to spawn vehicle");
                    return;
                }

                vehicle.PlaceOnGround();
                Speak($"Spawned {_vehicles[index].name}");

                // Warp player inside if setting enabled
                if (_settings != null && _settings.GetSetting("warpInsideVehicle"))
                {
                    player.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "VehicleSpawnMenu.OnItemActivated");
                Speak("Failed to spawn vehicle");
            }
        }

        public override string GetMenuName()
        {
            return _filterClass.HasValue || _filterNames != null ? _categoryName : "Spawn Vehicle";
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Get the VehicleClass for a VehicleHash
        /// </summary>
        private VehicleClass GetVehicleClass(VehicleHash hash)
        {
            // Use native function to get vehicle class
            return (VehicleClass)Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, (int)hash);
        }

        #endregion
    }
}
