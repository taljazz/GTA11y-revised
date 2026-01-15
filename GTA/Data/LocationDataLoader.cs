using System;
using System.Collections.Generic;
using System.IO;
using GTA.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GrandTheftAccessibility.Data
{
    /// <summary>
    /// Loads location data from external JSON files with fallback to hardcoded defaults.
    /// JSON files are stored in the GTA V scripts folder for easy user customization.
    /// </summary>
    public static class LocationDataLoader
    {
        // Scripts folder path where JSON files are stored
        private static readonly string ScriptsFolder;
        private static readonly string TeleportLocationsFile;
        private static readonly string WaypointDestinationsFile;

        // Cached data - loaded once and reused
        private static List<TeleportCategory> _teleportCategories;
        private static List<WaypointDestination> _waypointDestinations;
        private static bool _teleportLoaded = false;
        private static bool _waypointLoaded = false;

        static LocationDataLoader()
        {
            // Use Documents/Rockstar Games/GTA V/scripts folder for JSON files
            // This allows users to edit locations without recompiling
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            ScriptsFolder = Path.Combine(documentsPath, "Rockstar Games", "GTA V", "scripts");
            TeleportLocationsFile = Path.Combine(ScriptsFolder, "teleport_locations.json");
            WaypointDestinationsFile = Path.Combine(ScriptsFolder, "waypoint_destinations.json");
        }

        #region Teleport Locations

        /// <summary>
        /// Loads teleport locations from JSON file, falling back to hardcoded defaults if unavailable.
        /// </summary>
        public static List<TeleportCategory> LoadTeleportLocations()
        {
            if (_teleportLoaded && _teleportCategories != null)
            {
                return _teleportCategories;
            }

            _teleportCategories = new List<TeleportCategory>();

            try
            {
                if (File.Exists(TeleportLocationsFile))
                {
                    Logger.Info($"Loading teleport locations from: {TeleportLocationsFile}");
                    string json = File.ReadAllText(TeleportLocationsFile);

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var data = JsonConvert.DeserializeObject<TeleportLocationsJson>(json);

                        if (data?.Categories != null && data.Categories.Count > 0)
                        {
                            foreach (var category in data.Categories)
                            {
                                if (ValidateCategory(category))
                                {
                                    var teleportCategory = new TeleportCategory(category.Name);
                                    foreach (var loc in category.Locations)
                                    {
                                        if (ValidateLocation(loc))
                                        {
                                            teleportCategory.Locations.Add(new TeleportLocation(
                                                loc.Name, loc.X, loc.Y, loc.Z, category.Name));
                                        }
                                    }
                                    if (teleportCategory.Locations.Count > 0)
                                    {
                                        _teleportCategories.Add(teleportCategory);
                                    }
                                }
                            }

                            if (_teleportCategories.Count > 0)
                            {
                                _teleportLoaded = true;
                                Logger.Info($"Loaded {_teleportCategories.Count} teleport categories from JSON");
                                return _teleportCategories;
                            }
                        }
                    }
                    Logger.Warning("Teleport locations JSON is empty or invalid, using defaults");
                }
                else
                {
                    Logger.Info($"Teleport locations JSON not found at {TeleportLocationsFile}, using defaults");
                }
            }
            catch (JsonException ex)
            {
                Logger.Error($"JSON parsing error in teleport locations: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "LoadTeleportLocations");
            }

            // Fall back to hardcoded defaults
            _teleportCategories = GetDefaultTeleportCategories();
            _teleportLoaded = true;
            Logger.Info($"Using {_teleportCategories.Count} default teleport categories");
            return _teleportCategories;
        }

        /// <summary>
        /// Gets the list of teleport category names
        /// </summary>
        public static string[] GetTeleportCategoryNames()
        {
            var categories = LoadTeleportLocations();
            string[] names = new string[categories.Count];
            for (int i = 0; i < categories.Count; i++)
            {
                names[i] = categories[i].Name;
            }
            return names;
        }

        /// <summary>
        /// Gets locations for a specific category by index
        /// </summary>
        public static TeleportLocation[] GetTeleportLocationsByCategory(int categoryIndex)
        {
            var categories = LoadTeleportLocations();
            if (categoryIndex >= 0 && categoryIndex < categories.Count)
            {
                return categories[categoryIndex].Locations.ToArray();
            }
            return new TeleportLocation[0];
        }

        /// <summary>
        /// Gets the number of teleport categories
        /// </summary>
        public static int GetTeleportCategoryCount()
        {
            return LoadTeleportLocations().Count;
        }

        #endregion

        #region Waypoint Destinations

        /// <summary>
        /// Loads waypoint destinations from JSON file, falling back to hardcoded defaults if unavailable.
        /// </summary>
        public static WaypointDestination[] LoadWaypointDestinations()
        {
            if (_waypointLoaded && _waypointDestinations != null)
            {
                return _waypointDestinations.ToArray();
            }

            _waypointDestinations = new List<WaypointDestination>();

            try
            {
                if (File.Exists(WaypointDestinationsFile))
                {
                    Logger.Info($"Loading waypoint destinations from: {WaypointDestinationsFile}");
                    string json = File.ReadAllText(WaypointDestinationsFile);

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var data = JsonConvert.DeserializeObject<WaypointDestinationsJson>(json);

                        if (data?.Destinations != null && data.Destinations.Count > 0)
                        {
                            foreach (var dest in data.Destinations)
                            {
                                if (ValidateWaypointDestination(dest))
                                {
                                    _waypointDestinations.Add(new WaypointDestination(
                                        dest.Name, dest.X, dest.Y, dest.Z));
                                }
                            }

                            if (_waypointDestinations.Count > 0)
                            {
                                _waypointLoaded = true;
                                Logger.Info($"Loaded {_waypointDestinations.Count} waypoint destinations from JSON");
                                return _waypointDestinations.ToArray();
                            }
                        }
                    }
                    Logger.Warning("Waypoint destinations JSON is empty or invalid, using defaults");
                }
                else
                {
                    Logger.Info($"Waypoint destinations JSON not found at {WaypointDestinationsFile}, using defaults");
                }
            }
            catch (JsonException ex)
            {
                Logger.Error($"JSON parsing error in waypoint destinations: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "LoadWaypointDestinations");
            }

            // Fall back to hardcoded defaults
            _waypointDestinations = new List<WaypointDestination>(LocationData.WaypointDestinations);
            _waypointLoaded = true;
            Logger.Info($"Using {_waypointDestinations.Count} default waypoint destinations");
            return _waypointDestinations.ToArray();
        }

        /// <summary>
        /// Gets the number of waypoint destinations
        /// </summary>
        public static int GetWaypointDestinationCount()
        {
            return LoadWaypointDestinations().Length;
        }

        #endregion

        #region Validation

        private static bool ValidateCategory(TeleportCategoryJson category)
        {
            if (category == null)
            {
                Logger.Warning("Skipping null category");
                return false;
            }

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                Logger.Warning("Skipping category with empty name");
                return false;
            }

            if (category.Locations == null || category.Locations.Count == 0)
            {
                Logger.Warning($"Skipping category '{category.Name}' with no locations");
                return false;
            }

            return true;
        }

        private static bool ValidateLocation(LocationJson location)
        {
            if (location == null)
            {
                Logger.Warning("Skipping null location");
                return false;
            }

            if (string.IsNullOrWhiteSpace(location.Name))
            {
                Logger.Warning("Skipping location with empty name");
                return false;
            }

            if (float.IsNaN(location.X) || float.IsNaN(location.Y) || float.IsNaN(location.Z))
            {
                Logger.Warning($"Skipping location '{location.Name}' with NaN coordinates");
                return false;
            }

            if (float.IsInfinity(location.X) || float.IsInfinity(location.Y) || float.IsInfinity(location.Z))
            {
                Logger.Warning($"Skipping location '{location.Name}' with infinite coordinates");
                return false;
            }

            return true;
        }

        private static bool ValidateWaypointDestination(LocationJson destination)
        {
            return ValidateLocation(destination);
        }

        #endregion

        #region Default Data

        /// <summary>
        /// Creates default teleport categories from the hardcoded LocationData class
        /// </summary>
        private static List<TeleportCategory> GetDefaultTeleportCategories()
        {
            var categories = new List<TeleportCategory>();

            // Character Houses
            var characterHouses = new TeleportCategory("Character Houses");
            foreach (var loc in LocationData.CharacterHouses)
            {
                characterHouses.Locations.Add(loc);
            }
            categories.Add(characterHouses);

            // Airports and Runways
            var airports = new TeleportCategory("Airports and Runways");
            foreach (var loc in LocationData.AirportsAndRunways)
            {
                airports.Locations.Add(loc);
            }
            categories.Add(airports);

            // Sniping Vantage Points
            var sniping = new TeleportCategory("Sniping Vantage Points");
            foreach (var loc in LocationData.SnipingVantagePoints)
            {
                sniping.Locations.Add(loc);
            }
            categories.Add(sniping);

            // Military and Restricted
            var military = new TeleportCategory("Military and Restricted");
            foreach (var loc in LocationData.MilitaryAndRestricted)
            {
                military.Locations.Add(loc);
            }
            categories.Add(military);

            // Landmarks
            var landmarks = new TeleportCategory("Landmarks");
            foreach (var loc in LocationData.Landmarks)
            {
                landmarks.Locations.Add(loc);
            }
            categories.Add(landmarks);

            // Blaine County
            var blaineCounty = new TeleportCategory("Blaine County");
            foreach (var loc in LocationData.BlaineCounty)
            {
                blaineCounty.Locations.Add(loc);
            }
            categories.Add(blaineCounty);

            // Coastal and Beaches
            var coastal = new TeleportCategory("Coastal and Beaches");
            foreach (var loc in LocationData.CoastalAndBeaches)
            {
                coastal.Locations.Add(loc);
            }
            categories.Add(coastal);

            // Remote Areas
            var remote = new TeleportCategory("Remote Areas");
            foreach (var loc in LocationData.RemoteAreas)
            {
                remote.Locations.Add(loc);
            }
            categories.Add(remote);

            // Emergency Services
            var emergency = new TeleportCategory("Emergency Services");
            foreach (var loc in LocationData.EmergencyServices)
            {
                emergency.Locations.Add(loc);
            }
            categories.Add(emergency);

            return categories;
        }

        #endregion

        #region Reload Support

        /// <summary>
        /// Forces a reload of location data from JSON files on next access.
        /// Useful for hot-reloading during gameplay.
        /// </summary>
        public static void ReloadLocations()
        {
            _teleportLoaded = false;
            _waypointLoaded = false;
            _teleportCategories = null;
            _waypointDestinations = null;
            Logger.Info("Location data marked for reload");
        }

        #endregion

        #region JSON Model Classes

        /// <summary>
        /// JSON model for teleport locations file
        /// </summary>
        private class TeleportLocationsJson
        {
            [JsonProperty("categories")]
            public List<TeleportCategoryJson> Categories { get; set; }
        }

        /// <summary>
        /// JSON model for a teleport category
        /// </summary>
        private class TeleportCategoryJson
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("locations")]
            public List<LocationJson> Locations { get; set; }
        }

        /// <summary>
        /// JSON model for waypoint destinations file
        /// </summary>
        private class WaypointDestinationsJson
        {
            [JsonProperty("destinations")]
            public List<LocationJson> Destinations { get; set; }
        }

        /// <summary>
        /// JSON model for a single location
        /// </summary>
        private class LocationJson
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("x")]
            public float X { get; set; }

            [JsonProperty("y")]
            public float Y { get; set; }

            [JsonProperty("z")]
            public float Z { get; set; }
        }

        #endregion
    }

    #region Data Model Classes

    /// <summary>
    /// Represents a category of teleport locations
    /// </summary>
    public class TeleportCategory
    {
        public string Name { get; }
        public List<TeleportLocation> Locations { get; }

        public TeleportCategory(string name)
        {
            Name = name;
            Locations = new List<TeleportLocation>();
        }
    }

    #endregion
}
