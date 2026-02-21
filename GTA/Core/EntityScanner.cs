using System;
using System.Collections.Generic;
using System.Text;
using GTA;
using GTA.Math;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Handles scanning for nearby entities with optimized distance/direction calculations
    /// Uses object pooling and StringBuilder to minimize allocations
    /// Uses HashManager for shared hash lookups (loaded once, shared across classes)
    /// </summary>
    public class EntityScanner
    {
        private readonly StringBuilder _resultBuilder;

        // Object pool for Result objects to reduce allocations
        private readonly List<Result> _resultPool;
        private int _poolIndex;

        // Reusable results list to avoid allocation on each scan
        private readonly List<Result> _scanResults;

        public EntityScanner()
        {
            _resultBuilder = new StringBuilder(512);
            _resultPool = new List<Result>(150);
            _scanResults = new List<Result>(150);  // Reusable list for scan results

            // Pre-populate result pool (150 to handle dense city areas without overflow allocations)
            for (int i = 0; i < 150; i++)
            {
                _resultPool.Add(new Result("", 0, 0, ""));
            }

            // Ensure HashManager is initialized (will load hashes on first access)
            if (Logger.IsDebugEnabled) Logger.Debug($"EntityScanner using HashManager with {HashManager.Count} hashes");
        }

        /// <summary>
        /// Get a Result object from the pool (reuse existing objects)
        /// </summary>
        private Result GetPooledResult(string name, double xyDistance, double zDistance, string direction)
        {
            if (_poolIndex < _resultPool.Count)
            {
                Result r = _resultPool[_poolIndex++];
                r.name = name;
                r.xyDistance = xyDistance;
                r.zDistance = zDistance;
                r.direction = direction;
                r.totalDistance = xyDistance + Math.Abs(zDistance);
                return r;
            }
            // Pool exhausted, create new
            return new Result(name, xyDistance, zDistance, direction);
        }

        /// <summary>
        /// Reset the result pool for next use
        /// </summary>
        private void ResetPool()
        {
            _poolIndex = 0;
        }

        /// <summary>
        /// Scan for nearby vehicles
        /// </summary>
        public string ScanNearbyVehicles(Vector3 playerPos, Vehicle currentVehicle, bool onScreenOnly)
        {
            ResetPool();
            _scanResults.Clear();

            try
            {
                Vehicle[] vehicles = World.GetNearbyVehicles(playerPos, Constants.NEARBY_ENTITY_RADIUS);
                if (vehicles == null || vehicles.Length == 0)
                    return FormatResults(_scanResults, "Nearest Vehicles: ");

                // Get current vehicle handle once for comparison
                int currentVehicleHandle = (currentVehicle != null && currentVehicle.Exists()) ? currentVehicle.Handle : -1;

                foreach (Vehicle vehicle in vehicles)
                {
                    // Defensive: Check if vehicle is valid
                    if (vehicle == null || !vehicle.Exists())
                        continue;

                    try
                    {
                        // Compare by Handle - SHVDN returns new wrapper objects each call
                        if (!vehicle.IsVisible || vehicle.IsDead || vehicle.Handle == currentVehicleHandle)
                            continue;

                        if (onScreenOnly && !vehicle.IsOnScreen)
                            continue;

                        string localizedName = "vehicle";
                        try { localizedName = vehicle.LocalizedName ?? "vehicle"; }
                        catch { /* LocalizedName can fail for some vehicle types, fallback is acceptable */ }
                        string name = vehicle.IsStopped
                            ? string.Concat("a stationary ", localizedName)
                            : string.Concat("a moving ", localizedName);

                        double xyDistance = SpatialCalculator.GetHorizontalDistance(playerPos, vehicle.Position);
                        double zDistance = SpatialCalculator.GetVerticalDistance(playerPos, vehicle.Position);
                        string direction = SpatialCalculator.GetDirectionTo(playerPos, vehicle.Position);

                        _scanResults.Add(GetPooledResult(name, xyDistance, zDistance, direction));
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "ScanNearbyVehicles - vehicle iteration");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ScanNearbyVehicles");
            }

            return FormatResults(_scanResults, "Nearest Vehicles: ");
        }

        /// <summary>
        /// Scan for nearby pedestrians
        /// </summary>
        public string ScanNearbyPedestrians(Vector3 playerPos, bool onScreenOnly)
        {
            ResetPool();
            _scanResults.Clear();

            try
            {
                Ped[] peds = World.GetNearbyPeds(playerPos, Constants.NEARBY_ENTITY_RADIUS);
                if (peds == null || peds.Length == 0)
                    return FormatResults(_scanResults, "Nearest Characters: ");

                foreach (Ped ped in peds)
                {
                    // Defensive: Check if ped is valid
                    if (ped == null || !ped.Exists())
                        continue;

                    try
                    {
                        if (!ped.IsVisible || ped.IsDead)
                            continue;

                        if (onScreenOnly && !ped.IsOnScreen)
                            continue;

                        // Use int directly - avoids ToString() allocation
                        int modelHash = (int)ped.Model.NativeValue;
                        if (!HashManager.TryGetName(modelHash, out string pedName))
                            continue;

                        // Skip player models
                        if (Constants.PLAYER_MODELS != null && Array.IndexOf(Constants.PLAYER_MODELS, pedName) >= 0)
                            continue;

                        double xyDistance = SpatialCalculator.GetHorizontalDistance(playerPos, ped.Position);
                        double zDistance = SpatialCalculator.GetVerticalDistance(playerPos, ped.Position);
                        string direction = SpatialCalculator.GetDirectionTo(playerPos, ped.Position);

                        _scanResults.Add(GetPooledResult(pedName, xyDistance, zDistance, direction));
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "ScanNearbyPedestrians - ped iteration");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ScanNearbyPedestrians");
            }

            return FormatResults(_scanResults, "Nearest Characters: ");
        }

        /// <summary>
        /// Scan for nearby doors and gates
        /// </summary>
        public string ScanNearbyDoors(Vector3 playerPos, Ped player, bool onScreenOnly)
        {
            ResetPool();
            _scanResults.Clear();

            try
            {
                Prop[] props = World.GetNearbyProps(playerPos, Constants.NEARBY_ENTITY_RADIUS);
                if (props == null || props.Length == 0)
                    return FormatResults(_scanResults, "Nearest Doors: ");

                // Get player handle for attachment check
                int playerHandle = (player != null && player.Exists()) ? player.Handle : -1;

                foreach (Prop prop in props)
                {
                    // Defensive: Check if prop is valid
                    if (prop == null || !prop.Exists())
                        continue;

                    try
                    {
                        if (!prop.IsVisible)
                            continue;

                        // Check if attached to player using handle comparison
                        if (playerHandle >= 0 && player != null && prop.IsAttachedTo(player))
                            continue;

                        if (onScreenOnly && !prop.IsOnScreen)
                            continue;

                        // Use int directly - avoids ToString() allocation
                        int modelHash = (int)prop.Model.NativeValue;
                        if (!HashManager.TryGetName(modelHash, out string propName))
                            continue;

                        // Check if it's a door or gate
                        if (string.IsNullOrEmpty(propName) || (!propName.Contains("door") && !propName.Contains("gate")))
                            continue;

                        double xyDistance = SpatialCalculator.GetHorizontalDistance(playerPos, prop.Position);
                        double zDistance = SpatialCalculator.GetVerticalDistance(playerPos, prop.Position);
                        string direction = SpatialCalculator.GetDirectionTo(playerPos, prop.Position);

                        _scanResults.Add(GetPooledResult(propName, xyDistance, zDistance, direction));
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "ScanNearbyDoors - prop iteration");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ScanNearbyDoors");
            }

            return FormatResults(_scanResults, "Nearest Doors: ");
        }

        /// <summary>
        /// Scan for nearby objects (non-door props)
        /// </summary>
        public string ScanNearbyObjects(Vector3 playerPos, Ped player, bool onScreenOnly)
        {
            ResetPool();
            _scanResults.Clear();

            try
            {
                Prop[] props = World.GetNearbyProps(playerPos, Constants.NEARBY_ENTITY_RADIUS);
                if (props == null || props.Length == 0)
                    return FormatResults(_scanResults, "Nearest Objects: ");

                // Get player handle for attachment check
                int playerHandle = (player != null && player.Exists()) ? player.Handle : -1;

                foreach (Prop prop in props)
                {
                    // Defensive: Check if prop is valid
                    if (prop == null || !prop.Exists())
                        continue;

                    try
                    {
                        if (!prop.IsVisible)
                            continue;

                        // Check if attached to player
                        if (playerHandle >= 0 && player != null && prop.IsAttachedTo(player))
                            continue;

                        if (onScreenOnly && !prop.IsOnScreen)
                            continue;

                        // Use int directly - avoids ToString() allocation
                        int modelHash = (int)prop.Model.NativeValue;
                        if (!HashManager.TryGetName(modelHash, out string propName))
                            continue;

                        // Skip doors and gates (show other objects only)
                        if (string.IsNullOrEmpty(propName) || propName.Contains("door") || propName.Contains("gate"))
                            continue;

                        double xyDistance = SpatialCalculator.GetHorizontalDistance(playerPos, prop.Position);
                        double zDistance = SpatialCalculator.GetVerticalDistance(playerPos, prop.Position);
                        string direction = SpatialCalculator.GetDirectionTo(playerPos, prop.Position);

                        _scanResults.Add(GetPooledResult(propName, xyDistance, zDistance, direction));
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "ScanNearbyObjects - prop iteration");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ScanNearbyObjects");
            }

            return FormatResults(_scanResults, "Nearest Objects: ");
        }

        /// <summary>
        /// Format results list into spoken string using StringBuilder
        /// OPTIMIZED: Pre-estimate capacity, fewer Append calls
        /// </summary>
        private string FormatResults(List<Result> results, string header)
        {
            try
            {
                _resultBuilder.Clear();

                // Handle null or empty results
                if (results == null || results.Count == 0)
                {
                    _resultBuilder.Append(header ?? "Results: ").Append("None found nearby.");
                    return _resultBuilder.ToString();
                }

                // OPTIMIZED: Estimate capacity to avoid StringBuilder resizing
                // Average result ~60 chars, header ~20 chars
                int estimatedCapacity = (header?.Length ?? 10) + (results.Count * 65);
                if (_resultBuilder.Capacity < estimatedCapacity)
                {
                    _resultBuilder.EnsureCapacity(estimatedCapacity);
                }

                _resultBuilder.Append(header ?? "Results: ");

                // Sort by total distance
                try
                {
                    results.Sort();
                }
                catch
                {
                    // If sort fails, continue with unsorted results
                }

                // OPTIMIZED: Use fewer individual Append calls by batching
                for (int i = 0; i < results.Count; i++)
                {
                    Result result = results[i];
                    if (result == null) continue;

                    // Batch the main format together
                    _resultBuilder.Append(Math.Round(result.xyDistance)).Append(" meters ")
                        .Append(result.direction ?? "unknown").Append(", ");

                    if (result.zDistance != 0)
                    {
                        double absZDistance = Math.Round(Math.Abs(result.zDistance));
                        _resultBuilder.Append(absZDistance).Append(" meters ")
                            .Append(result.zDistance > 0 ? "above" : "below").Append(", ");
                    }

                    _resultBuilder.Append(result.name ?? "unknown").Append(". ");
                }

                return _resultBuilder.ToString();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "FormatResults");
                return header + "Error formatting results.";
            }
        }
    }
}
