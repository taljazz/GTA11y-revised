using GTA;
using GTA.Math;
using GTA.Native;
using DavyKager;
using GrandTheftAccessibility.Data;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for teleporting to predefined locations, organized by category
    /// Supports hierarchical submenus with back navigation
    /// Uses LocationDataLoader to load from JSON or fallback to hardcoded defaults
    /// </summary>
    public class LocationMenu : IMenuState
    {
        private int _currentCategoryIndex;
        private int _currentLocationIndex;
        private bool _inSubmenu;

        public LocationMenu()
        {
            _currentCategoryIndex = 0;
            _currentLocationIndex = 0;
            _inSubmenu = false;

            // Pre-load location data at construction (will use cache on subsequent calls)
            LocationDataLoader.LoadTeleportLocations();
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            if (_inSubmenu)
            {
                // Navigate within location list
                int step = fastScroll ? 10 : 1;
                var locations = LocationDataLoader.GetTeleportLocationsByCategory(_currentCategoryIndex);
                _currentLocationIndex -= step;
                if (_currentLocationIndex < 0)
                    _currentLocationIndex = locations.Length - 1;
            }
            else
            {
                // Navigate between categories
                int categoryCount = LocationDataLoader.GetTeleportCategoryCount();
                if (_currentCategoryIndex > 0)
                    _currentCategoryIndex--;
                else
                    _currentCategoryIndex = categoryCount - 1;
            }
        }

        public void NavigateNext(bool fastScroll = false)
        {
            if (_inSubmenu)
            {
                // Navigate within location list
                int step = fastScroll ? 10 : 1;
                var locations = LocationDataLoader.GetTeleportLocationsByCategory(_currentCategoryIndex);
                _currentLocationIndex += step;
                if (_currentLocationIndex >= locations.Length)
                    _currentLocationIndex = 0;
            }
            else
            {
                // Navigate between categories
                int categoryCount = LocationDataLoader.GetTeleportCategoryCount();
                if (_currentCategoryIndex < categoryCount - 1)
                    _currentCategoryIndex++;
                else
                    _currentCategoryIndex = 0;
            }
        }

        public string GetCurrentItemText()
        {
            if (_inSubmenu)
            {
                var locations = LocationDataLoader.GetTeleportLocationsByCategory(_currentCategoryIndex);
                return $"{_currentLocationIndex + 1} of {locations.Length}: {locations[_currentLocationIndex].Name}";
            }
            else
            {
                var categoryNames = LocationDataLoader.GetTeleportCategoryNames();
                var categoryName = categoryNames[_currentCategoryIndex];
                var locations = LocationDataLoader.GetTeleportLocationsByCategory(_currentCategoryIndex);
                return $"{categoryName} ({locations.Length} locations)";
            }
        }

        public void ExecuteSelection()
        {
            if (!_inSubmenu)
            {
                // Enter submenu
                _inSubmenu = true;
                _currentLocationIndex = 0;
                var categoryNames = LocationDataLoader.GetTeleportCategoryNames();
                var locations = LocationDataLoader.GetTeleportLocationsByCategory(_currentCategoryIndex);
                Tolk.Speak($"{categoryNames[_currentCategoryIndex]}, {locations.Length} locations");
                return;
            }

            // Teleport to selected location
            var locs = LocationDataLoader.GetTeleportLocationsByCategory(_currentCategoryIndex);
            var location = locs[_currentLocationIndex];

            TeleportToLocation(location.Coords, location.Name);
        }

        /// <summary>
        /// Simple, reliable teleportation using SET_ENTITY_COORDS_NO_OFFSET.
        /// Based on Native Trainer implementation - proven to work reliably.
        /// NO Script.Wait() calls - completely non-blocking.
        /// </summary>
        private void TeleportToLocation(Vector3 destination, string locationName)
        {
            Logger.Info($"=== TELEPORT START: {locationName} ===");
            Logger.Info($"Destination coords: X={destination.X:F2}, Y={destination.Y:F2}, Z={destination.Z:F2}");

            try
            {
                // Get the entity to teleport (vehicle if in one, otherwise player)
                Ped player = Game.Player.Character;
                Logger.Debug($"Game.Player.Character retrieved: {(player != null ? "not null" : "NULL")}");

                // Check if player entity exists and is valid
                if (player == null)
                {
                    Logger.Warning("Teleport failed: Game.Player.Character is null");
                    return;
                }

                bool entityExists = Function.Call<bool>(Hash.DOES_ENTITY_EXIST, player.Handle);
                Logger.Debug($"Player handle: {player.Handle}, DOES_ENTITY_EXIST: {entityExists}");

                if (!entityExists)
                {
                    Logger.Warning("Teleport failed: Player entity does not exist (DOES_ENTITY_EXIST returned false)");
                    return;
                }

                // Log pre-teleport position
                Vector3 prePosition = player.Position;
                Logger.Info($"Pre-teleport player position: X={prePosition.X:F2}, Y={prePosition.Y:F2}, Z={prePosition.Z:F2}");

                Entity entityToTeleport;

                // IMPORTANT: Use IsInVehicle() instead of checking CurrentVehicle != null
                // CurrentVehicle can return stale references to vehicles the player has exited
                bool inVehicle = player.IsInVehicle();
                Vehicle vehicle = inVehicle ? player.CurrentVehicle : null;
                Logger.Debug($"player.IsInVehicle(): {inVehicle}");

                if (inVehicle && vehicle != null)
                {
                    // Double-check: verify player and vehicle are actually near each other
                    Vector3 vehiclePos = vehicle.Position;
                    float playerToVehicleDistance = prePosition.DistanceTo(vehiclePos);
                    Logger.Debug($"Player-to-vehicle distance: {playerToVehicleDistance:F2}m");

                    if (playerToVehicleDistance > 10f)
                    {
                        // Player is not actually in this vehicle - stale reference
                        Logger.Warning($"Stale vehicle reference detected! Player is {playerToVehicleDistance:F2}m from vehicle. Teleporting player instead.");
                        entityToTeleport = player;
                        inVehicle = false;
                    }
                    else
                    {
                        entityToTeleport = vehicle;
                        Logger.Debug($"Teleporting vehicle - Handle: {vehicle.Handle}, Model: {vehicle.Model.Hash}");
                        Logger.Info($"Pre-teleport vehicle position: X={vehiclePos.X:F2}, Y={vehiclePos.Y:F2}, Z={vehiclePos.Z:F2}");
                    }
                }
                else
                {
                    entityToTeleport = player;
                    Logger.Debug($"Teleporting player on foot - Handle: {player.Handle}");
                }

                // Use SET_ENTITY_COORDS_NO_OFFSET - the most reliable teleport method
                // Parameters: entity, x, y, z, keepTasks, keepIK, doWarp
                // keepTasks=false, keepIK=false, doWarp=true (clears contacts, warps instantly)
                Logger.Debug($"Calling SET_ENTITY_COORDS_NO_OFFSET with handle {entityToTeleport.Handle}");
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET,
                    entityToTeleport.Handle,
                    destination.X,
                    destination.Y,
                    destination.Z,
                    false,  // keepTasks - clear tasks
                    false,  // keepIK - reset IK
                    true);  // doWarp - instant warp, clear contacts
                Logger.Debug("SET_ENTITY_COORDS_NO_OFFSET call completed");

                // Clear velocity to prevent continued movement
                Logger.Debug("Calling SET_ENTITY_VELOCITY to clear momentum");
                Function.Call(Hash.SET_ENTITY_VELOCITY, entityToTeleport.Handle, 0f, 0f, 0f);

                // If in vehicle, also place it properly on ground
                if (inVehicle)
                {
                    Logger.Debug("Calling SET_VEHICLE_ON_GROUND_PROPERLY");
                    Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, entityToTeleport.Handle, 5f);
                }

                // Log post-teleport position to verify
                Vector3 postPosition = entityToTeleport.Position;
                Logger.Info($"Post-teleport position: X={postPosition.X:F2}, Y={postPosition.Y:F2}, Z={postPosition.Z:F2}");

                // Calculate distance from intended destination
                float distanceFromTarget = destination.DistanceTo(postPosition);
                Logger.Info($"Distance from target: {distanceFromTarget:F2} meters");

                if (distanceFromTarget > 10f)
                {
                    Logger.Warning($"Teleport may have failed - entity is {distanceFromTarget:F2}m from destination");
                }

                Tolk.Speak($"Teleported to {locationName}");
                Logger.Info($"=== TELEPORT COMPLETE: {locationName} ===");
            }
            catch (System.Exception ex)
            {
                Logger.Exception(ex, "Teleport");
                Logger.Error($"Exception during teleport to {locationName}: {ex.Message}");

                // Fallback: direct position property set
                try
                {
                    Logger.Debug("Attempting fallback teleport via Position property");
                    Ped player = Game.Player.Character;
                    if (player != null)
                    {
                        // Use IsInVehicle() instead of CurrentVehicle != null to avoid stale references
                        Entity entity = player.IsInVehicle() ? (Entity)player.CurrentVehicle : (Entity)player;
                        entity.Position = destination;
                        Logger.Info("Fallback teleport completed via Position property");

                        Vector3 fallbackPos = entity.Position;
                        Logger.Info($"Fallback post-position: X={fallbackPos.X:F2}, Y={fallbackPos.Y:F2}, Z={fallbackPos.Z:F2}");
                    }
                    else
                    {
                        Logger.Error("Fallback failed: Game.Player.Character is null");
                    }
                }
                catch (System.Exception fallbackEx)
                {
                    Logger.Error($"Fallback teleport also failed: {fallbackEx.Message}");
                }
            }
        }

        public string GetMenuName()
        {
            if (_inSubmenu)
            {
                var categoryNames = LocationDataLoader.GetTeleportCategoryNames();
                return categoryNames[_currentCategoryIndex];
            }
            return "Teleport to location";
        }

        public bool HasActiveSubmenu => _inSubmenu;

        public void ExitSubmenu()
        {
            if (_inSubmenu)
            {
                _inSubmenu = false;
            }
        }
    }
}
