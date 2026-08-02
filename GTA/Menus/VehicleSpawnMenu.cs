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

                _vehicles.Add(new VehicleSpawn(ResolveName(vh), vh));
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

                // Get the vehicle class for display in special categories
                int vehicleClassIndex = Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, (int)vh);
                string className = null;
                if (vehicleClassIndex >= 0 && vehicleClassIndex < Constants.VEHICLE_CLASS_NAMES.Length)
                {
                    className = Constants.VEHICLE_CLASS_NAMES[vehicleClassIndex];
                }

                _vehicles.Add(new VehicleSpawn(ResolveName(vh), vh, className));
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

            // Seat count alongside the class, so choosing a car for a job that
            // needs four people does not mean spawning them to find out. Kept to
            // one short clause because this is spoken on every keypress while
            // scrolling - the full description lives in the Vehicle Guide menu.
            string detail = SafeShortDescription(vehicle.id);
            if (!string.IsNullOrEmpty(detail))
                return $"{index + 1} of {_vehicles.Count}: {vehicle.name}, {detail}";

            if (!string.IsNullOrEmpty(vehicle.vehicleClassName))
            {
                return $"{index + 1} of {_vehicles.Count}: {vehicle.name}, {vehicle.vehicleClassName}";
            }
            return $"{index + 1} of {_vehicles.Count}: {vehicle.name}";
        }

        /// <summary>
        /// Short class-and-seats line for a model. Never throws and never blocks
        /// the list: an undescribable vehicle just falls back to its stored class.
        /// </summary>
        private static string SafeShortDescription(VehicleHash id)
        {
            try { return VehicleDescriber.GetShortDescription(new Model(id)); }
            catch { return null; }
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

        /// <summary>
        /// The best name available for a model, and never nothing.
        ///
        /// This used to read the game's display name and SKIP the vehicle when it
        /// came back empty or "NULL", which happens for models whose text label
        /// is not loaded. A skipped vehicle is not merely unnamed, it is absent
        /// from every category and therefore impossible to spawn at all. Falling
        /// back to the model's own name keeps everything reachable - a slightly
        /// awkward name is a far smaller problem than a missing vehicle, and the
        /// Vehicle Guide will still describe it correctly because the curated
        /// notes are keyed on exactly this name.
        /// </summary>
        private static string ResolveName(VehicleHash hash)
        {
            try
            {
                string displayName = Game.GetLocalizedString(
                    Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, hash));

                if (!string.IsNullOrWhiteSpace(displayName) && displayName != "NULL")
                    return displayName;
            }
            catch
            {
                // Fall through to the model name
            }

            return PrettifyModelName(hash.ToString());
        }

        /// <summary>
        /// Break a model name into something that reads aloud sensibly:
        /// "DeathBike2" becomes "Death Bike 2", "ZR3802" becomes "ZR 3802".
        /// </summary>
        private static string PrettifyModelName(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return "Unknown vehicle";

            var builder = new System.Text.StringBuilder(modelName.Length + 8);

            for (int i = 0; i < modelName.Length; i++)
            {
                char current = modelName[i];

                if (i > 0)
                {
                    char previous = modelName[i - 1];
                    bool startsWord = char.IsUpper(current) && !char.IsUpper(previous);
                    bool startsNumber = char.IsDigit(current) && !char.IsDigit(previous);

                    if (startsWord || startsNumber)
                        builder.Append(' ');
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        #endregion
    }
}
