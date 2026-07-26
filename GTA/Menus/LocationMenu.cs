using GTA;
using GTA.Math;
using GTA.Native;
using GrandTheftAccessibility.Data;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for teleporting to predefined locations, organized by category.
    /// Top level = categories, submenu = locations within the chosen category.
    /// Uses LocationDataLoader to load from JSON or fallback to hardcoded defaults.
    /// </summary>
    public class LocationMenu : HierarchicalMenuBase
    {
        #region Construction

        public LocationMenu(AudioManager audio) : base(audio)
        {
            // Pre-load location data at construction (will use cache on subsequent calls)
            LocationDataLoader.LoadTeleportLocations();
        }

        #endregion

        #region Top Level - categories

        protected override int ItemCount => LocationDataLoader.GetTeleportCategoryCount();

        protected override int FastScrollStep => 10;

        protected override string GetItemText(int index)
        {
            var categoryNames = LocationDataLoader.GetTeleportCategoryNames();
            var locations = LocationDataLoader.GetTeleportLocationsByCategory(index);
            return $"{categoryNames[index]} ({locations.Length} locations)";
        }

        protected override void OnItemActivated(int index)
        {
            // Enter the location list for this category
            EnterSubmenu();
            var categoryNames = LocationDataLoader.GetTeleportCategoryNames();
            var locations = LocationDataLoader.GetTeleportLocationsByCategory(index);
            Speak($"{categoryNames[index]}, {locations.Length} locations");
        }

        public override string GetMenuName()
        {
            if (InSubmenu)
            {
                var categoryNames = LocationDataLoader.GetTeleportCategoryNames();
                return categoryNames[SelectedIndex];
            }
            return "Teleport to location";
        }

        #endregion

        #region Submenu - locations

        protected override int SubmenuItemCount =>
            LocationDataLoader.GetTeleportLocationsByCategory(SelectedIndex).Length;

        protected override string GetSubmenuItemText(int index)
        {
            var locations = LocationDataLoader.GetTeleportLocationsByCategory(SelectedIndex);
            return $"{index + 1} of {locations.Length}: {locations[index].Name}";
        }

        protected override void OnSubmenuItemActivated(int index)
        {
            var locations = LocationDataLoader.GetTeleportLocationsByCategory(SelectedIndex);
            var location = locations[index];

            TeleportToLocation(location.Coords, location.Name);
        }

        #endregion

        #region Teleport

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

                // Check if player entity exists and is valid
                if (player == null)
                {
                    Logger.Warning("Teleport failed: Game.Player.Character is null");
                    return;
                }

                bool entityExists = Function.Call<bool>(Hash.DOES_ENTITY_EXIST, player.Handle);
                if (!entityExists)
                {
                    Logger.Warning("Teleport failed: Player entity does not exist");
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

                if (inVehicle && vehicle != null)
                {
                    // Double-check: verify player and vehicle are actually near each other
                    Vector3 vehiclePos = vehicle.Position;
                    float playerToVehicleDistance = prePosition.DistanceTo(vehiclePos);

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
                        Logger.Info($"Pre-teleport vehicle position: X={vehiclePos.X:F2}, Y={vehiclePos.Y:F2}, Z={vehiclePos.Z:F2}");
                    }
                }
                else
                {
                    entityToTeleport = player;
                }

                // Use SET_ENTITY_COORDS_NO_OFFSET - the most reliable teleport method
                // Parameters: entity, x, y, z, keepTasks, keepIK, doWarp
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET,
                    entityToTeleport.Handle,
                    destination.X,
                    destination.Y,
                    destination.Z,
                    false,  // keepTasks - clear tasks
                    false,  // keepIK - reset IK
                    true);  // doWarp - instant warp, clear contacts

                // Clear velocity to prevent continued movement
                Function.Call(Hash.SET_ENTITY_VELOCITY, entityToTeleport.Handle, 0f, 0f, 0f);

                // If in vehicle, also place it properly on ground
                if (inVehicle)
                {
                    Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, entityToTeleport.Handle, 5f);
                }

                // Log post-teleport position to verify
                Vector3 postPosition = entityToTeleport.Position;
                Logger.Info($"Post-teleport position: X={postPosition.X:F2}, Y={postPosition.Y:F2}, Z={postPosition.Z:F2}");

                // Calculate distance from intended destination
                float distanceFromTarget = destination.DistanceTo(postPosition);
                if (distanceFromTarget > 10f)
                {
                    Logger.Warning($"Teleport may have failed - entity is {distanceFromTarget:F2}m from destination");
                }

                Speak($"Teleported to {locationName}");
                Logger.Info($"=== TELEPORT COMPLETE: {locationName} ===");
            }
            catch (System.Exception ex)
            {
                Logger.Exception(ex, "Teleport");

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

        #endregion
    }
}
