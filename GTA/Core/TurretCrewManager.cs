using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages AI turret crew members for weaponized vehicles.
    /// Spawns and controls AI peds in turret seats that automatically engage enemies.
    /// Based on PedTurrets mod functionality, integrated into GTA11Y.
    /// PERFORMANCE OPTIMIZED: Uses manual loops instead of LINQ, HashSet for O(1) lookups
    /// </summary>
    public class TurretCrewManager
    {
        private readonly SettingsManager _settings;
        private readonly AudioManager _audio;

        // Active turret crew state
        private Vehicle _vehicle;
        private List<Ped> _turretPeds;
        private HashSet<int> _turretPedHandles;  // O(1) lookup for turret ped contains check
        private Dictionary<Ped, int> _turretPedStates;  // TurretPedState enum values
        private Dictionary<Ped, int> _turretPedLastHealth;
        private bool _isSpawned;

        // Cached player relationship group (avoid repeated native calls)
        private RelationshipGroup _cachedPlayerRelationshipGroup;
        private long _lastRelationshipGroupUpdate;

        // Message tracking to prevent spam
        private bool _hasEngagedMessagePlayed;
        private bool _hasOutOfRangeMessagePlayed;
        private bool _hasDamagedMessagePlayed;
        private DateTime _lastDeathMessageTime;

        // Tick throttling
        private long _lastUpdateTick;

        public TurretCrewManager(SettingsManager settings, AudioManager audio)
        {
            _settings = settings;
            _audio = audio;
            _turretPeds = new List<Ped>(8);  // Pre-allocate for typical max crew size
            _turretPedHandles = new HashSet<int>();  // O(1) contains check
            _turretPedStates = new Dictionary<Ped, int>(8);
            _turretPedLastHealth = new Dictionary<Ped, int>(8);
            _isSpawned = false;
            _hasEngagedMessagePlayed = false;
            _hasOutOfRangeMessagePlayed = false;
            _hasDamagedMessagePlayed = false;
            _lastDeathMessageTime = DateTime.MinValue;
            _cachedPlayerRelationshipGroup = default;
            _lastRelationshipGroupUpdate = 0;
        }

        /// <summary>
        /// Check if turret crew is currently spawned
        /// </summary>
        public bool IsSpawned => _isSpawned;

        /// <summary>
        /// Get the current turret crew count
        /// </summary>
        public int CrewCount => _turretPeds?.Count ?? 0;

        /// <summary>
        /// Toggle turret crew - spawn if not active, destroy if active
        /// </summary>
        public void ToggleTurretCrew()
        {
            if (!_isSpawned)
            {
                SpawnTurretCrew();
            }
            else
            {
                DestroyTurretCrew();
            }
        }

        /// <summary>
        /// Spawn turret crew in the player's current vehicle
        /// </summary>
        public void SpawnTurretCrew()
        {
            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                {
                    Announce("Failed: Player not available", true, true);
                    return;
                }

                if (!player.IsInVehicle())
                {
                    Announce("Failed: Player must be in a vehicle", true, true);
                    return;
                }

                _vehicle = player.CurrentVehicle;
                if (_vehicle == null || !_vehicle.Exists())
                {
                    Announce("Failed: Vehicle not available", true, true);
                    return;
                }

                int vehicleHash = _vehicle.Model.Hash;

                // Check if vehicle has weapons
                bool hasWeapons = Function.Call<bool>(Hash.DOES_VEHICLE_HAVE_WEAPONS, _vehicle);
                if (!hasWeapons)
                {
                    Announce($"Failed: Vehicle has no weapons", true, true);
                    return;
                }

                int playerSeat = GetPlayerSeatIndex();

                // Dynamically detect turret seats - works with modded vehicles!
                // First try hardcoded seats (stock vehicles), then fall back to dynamic detection
                int[] validSeats;
                if (Constants.TURRET_VEHICLE_SEATS.TryGetValue(vehicleHash, out int[] knownSeats))
                {
                    validSeats = knownSeats;
                    Logger.Debug($"TurretCrewManager: Using known turret seats for hash {vehicleHash}");
                }
                else
                {
                    // Dynamic detection for modded vehicles - scan all seats with IS_TURRET_SEAT
                    validSeats = DetectTurretSeats(_vehicle);
                    if (validSeats.Length == 0)
                    {
                        Announce($"Failed: No turret seats detected", true, true);
                        return;
                    }
                    Logger.Info($"TurretCrewManager: Dynamically detected {validSeats.Length} turret seat(s) for modded vehicle");
                }

                // Clear all tracking collections
                _turretPeds.Clear();
                _turretPedHandles.Clear();  // IMPORTANT: Clear HashSet too!
                _turretPedStates.Clear();
                _turretPedLastHealth.Clear();

                Logger.Info($"TurretCrewManager: Vehicle hash {vehicleHash}, player in seat {playerSeat}, valid turret seats: [{string.Join(", ", validSeats)}]");

                int spawnedCount = 0;
                foreach (int seatIndex in validSeats)
                {
                    // Skip if player is in this seat
                    if (seatIndex == playerSeat)
                    {
                        Logger.Debug($"TurretCrewManager: Skipping seat {seatIndex} - player is in this seat");
                        continue;
                    }

                    // Check if seat is free
                    bool seatFree = _vehicle.IsSeatFree((VehicleSeat)seatIndex);
                    if (!seatFree)
                    {
                        // Log who is in the seat for debugging
                        Ped occupant = _vehicle.GetPedOnSeat((VehicleSeat)seatIndex);
                        string occupantInfo = occupant != null ? $"ped handle {occupant.Handle}" : "unknown occupant";
                        Logger.Debug($"TurretCrewManager: Skipping seat {seatIndex} - occupied by {occupantInfo}");
                        continue;
                    }

                    Logger.Debug($"TurretCrewManager: Attempting to spawn ped for seat {seatIndex}");

                    // Spawn ped near vehicle, then put in seat
                    Vector3 spawnPos = _vehicle.Position + _vehicle.RightVector * (spawnedCount + 1) * 2f;
                    Ped ped = World.CreatePed(PedHash.Blackops01SMY, spawnPos);

                    if (ped == null || !ped.Exists())
                    {
                        Logger.Warning($"Failed to spawn turret ped for seat {seatIndex}");
                        continue;
                    }

                    ped.SetIntoVehicle(_vehicle, (VehicleSeat)seatIndex);
                    ConfigureTurretPed(ped);

                    _turretPeds.Add(ped);
                    _turretPedHandles.Add(ped.Handle);  // Track handle for O(1) contains
                    _turretPedStates[ped] = Constants.TURRET_STATE_IDLE;
                    _turretPedLastHealth[ped] = ped.MaxHealth;
                    spawnedCount++;
                }

                if (_turretPeds.Count > 0)
                {
                    _isSpawned = true;
                    ResetMessageFlags();
                    Announce($"Turret crew spawned: {_turretPeds.Count} gunners", true, true);
                    Logger.Info($"TurretCrewManager: Spawned {_turretPeds.Count} turret peds");
                }
                else
                {
                    Announce("No turret seats available", true, true);
                    CleanupTurretPeds();
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "SpawnTurretCrew");
                Announce("Failed to spawn turret crew", true, true);
            }
        }

        /// <summary>
        /// Destroy all turret crew members
        /// </summary>
        public void DestroyTurretCrew()
        {
            CleanupTurretPeds();
            _isSpawned = false;
            Announce("Turret crew destroyed", true, true);
            Logger.Info("TurretCrewManager: Turret crew destroyed");
        }

        /// <summary>
        /// Update turret crew behavior - call from main tick
        /// </summary>
        public void Update(long currentTick)
        {
            if (!_isSpawned || _turretPeds == null || _turretPeds.Count == 0)
                return;

            // Throttle updates
            if (currentTick - _lastUpdateTick < Constants.TICK_INTERVAL_TURRET_UPDATE)
                return;

            _lastUpdateTick = currentTick;

            try
            {
                // Check if vehicle still exists
                if (_vehicle == null || !_vehicle.Exists())
                {
                    CleanupTurretPeds();
                    _isSpawned = false;
                    return;
                }

                bool anyFighting = false;
                bool anyInRange = false;
                bool anyDamaged = false;

                // Iterate in reverse to safely remove dead peds
                for (int i = _turretPeds.Count - 1; i >= 0; i--)
                {
                    Ped ped = _turretPeds[i];

                    // Check if ped is valid and still in vehicle
                    if (ped == null || !ped.Exists() || !ped.IsInVehicle(_vehicle))
                    {
                        RemoveTurretPed(i);
                        continue;
                    }

                    // Update ped behavior
                    UpdateTurretPedState(ped, ref anyFighting, ref anyInRange, ref anyDamaged);

                    // Check for death
                    if (ped.IsDead)
                    {
                        HandleTurretPedDeath(i);
                        continue;
                    }
                }

                // Handle collective announcements
                UpdateCollectiveMessages(anyFighting, anyInRange, anyDamaged);

                // Check if all crew gone
                if (_turretPeds.Count == 0)
                {
                    _isSpawned = false;
                    _vehicle = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TurretCrewManager.Update");
            }
        }

        /// <summary>
        /// Configure a turret ped with proper settings
        /// </summary>
        private void ConfigureTurretPed(Ped ped)
        {
            try
            {
                // Make ped persistent and reliable
                ped.AlwaysKeepTask = true;
                ped.BlockPermanentEvents = true;
                ped.IsPersistent = true;
                ped.CanRagdoll = false;

                // Set config flag 132 (combat-related)
                Function.Call(Hash.SET_PED_CONFIG_FLAG, ped, 132, true);

                // Put ped in player's relationship group so they're friendly
                Ped player = Game.Player?.Character;
                if (player != null && player.Exists())
                {
                    uint playerGroup = Function.Call<uint>(Hash.GET_PED_RELATIONSHIP_GROUP_HASH, player);
                    Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, ped, playerGroup);
                }

                // Give them a weapon for when they dismount (turret weapons are vehicle-based)
                ped.Weapons.Give(WeaponHash.MicroSMG, 9999, true, true);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "ConfigureTurretPed");
            }
        }

        /// <summary>
        /// Get the seat index the player is currently in
        /// </summary>
        private int GetPlayerSeatIndex()
        {
            try
            {
                if (_vehicle == null || !_vehicle.Exists())
                    return -2;

                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                    return -2;

                int totalSeats = _vehicle.PassengerCapacity + 1;
                for (int i = -1; i < totalSeats - 1; i++)
                {
                    Ped pedInSeat = _vehicle.GetPedOnSeat((VehicleSeat)i);
                    if (pedInSeat != null && pedInSeat == player)
                    {
                        return i;
                    }
                }

                return -2; // Not found
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "GetPlayerSeatIndex");
                return -2;
            }
        }

        /// <summary>
        /// Dynamically detect turret seats in a vehicle using the IS_TURRET_SEAT native.
        /// This allows turret crew to work with modded vehicles whose hashes have changed.
        /// </summary>
        /// <param name="vehicle">The vehicle to scan for turret seats</param>
        /// <returns>Array of seat indices that are turret seats</returns>
        private int[] DetectTurretSeats(Vehicle vehicle)
        {
            try
            {
                if (vehicle == null || !vehicle.Exists())
                    return Array.Empty<int>();

                // Get total seat count (PassengerCapacity doesn't include driver)
                int totalSeats = vehicle.PassengerCapacity + 1;

                // Use a list to collect turret seats (can't predict count ahead of time)
                List<int> turretSeats = new List<int>(totalSeats);

                // Scan all seats (starting from 0, as driver seat -1 is rarely a turret)
                // Check seats 0 through totalSeats-1 (passenger indices)
                for (int seatIndex = 0; seatIndex < totalSeats; seatIndex++)
                {
                    bool isTurret = Function.Call<bool>((Hash)Constants.NATIVE_IS_TURRET_SEAT, vehicle, seatIndex);
                    if (isTurret)
                    {
                        turretSeats.Add(seatIndex);
                        Logger.Debug($"TurretCrewManager: Seat {seatIndex} is a turret seat");
                    }
                }

                // If no turret seats found via IS_TURRET_SEAT, try alternative detection
                // Some vehicles may have weapons but not report turret seats properly
                if (turretSeats.Count == 0)
                {
                    Logger.Debug("TurretCrewManager: IS_TURRET_SEAT found no turrets, trying weapon seat detection");

                    // Try checking if seats can use vehicle weapons via GET_VEHICLE_PED_IS_IN
                    // For now, try common turret seat patterns: seats 0, 1, 2 (many military vehicles)
                    for (int seatIndex = 0; seatIndex < Math.Min(3, totalSeats); seatIndex++)
                    {
                        // Add seat if vehicle has weapons and seat exists
                        // This is a fallback for vehicles where IS_TURRET_SEAT doesn't work
                        if (seatIndex < totalSeats)
                        {
                            turretSeats.Add(seatIndex);
                            Logger.Debug($"TurretCrewManager: Adding seat {seatIndex} as potential turret (fallback)");
                        }
                    }
                }

                return turretSeats.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "DetectTurretSeats");
                return Array.Empty<int>();
            }
        }

        /// <summary>
        /// Update a turret ped's combat state
        /// OPTIMIZED: Uses TryGetValue instead of ContainsKey + access
        /// </summary>
        private void UpdateTurretPedState(Ped ped, ref bool anyFighting, ref bool anyInRange, ref bool anyDamaged)
        {
            try
            {
                // OPTIMIZED: Single dictionary lookup instead of ContainsKey + access
                int currentState = _turretPedStates.TryGetValue(ped, out int state) ? state : Constants.TURRET_STATE_IDLE;
                Ped nearestEnemy = FindNearestEnemy(ped);
                float distanceToEnemy = nearestEnemy != null ? ped.Position.DistanceTo(nearestEnemy.Position) : float.MaxValue;

                switch (currentState)
                {
                    case Constants.TURRET_STATE_IDLE:
                        if (nearestEnemy != null)
                        {
                            if (distanceToEnemy <= Constants.TURRET_FULL_AUTO_RANGE)
                            {
                                // Engage in full auto
                                Function.Call((Hash)Constants.NATIVE_SET_PED_FIRING_PATTERN, ped, Constants.FIRING_PATTERN_FULL_AUTO);
                                ped.Task.FightAgainst(nearestEnemy);
                                _turretPedStates[ped] = Constants.TURRET_STATE_FIGHTING;
                                anyFighting = true;
                                anyInRange = true;
                            }
                            else if (distanceToEnemy <= Constants.TURRET_AIM_RANGE)
                            {
                                // Aim at distant enemy
                                ped.Task.AimAt(nearestEnemy, -1);
                                _turretPedStates[ped] = Constants.TURRET_STATE_AIMING;
                            }
                        }
                        else
                        {
                            // No enemy - clear targeting
                            Function.Call((Hash)Constants.NATIVE_TASK_VEHICLE_SHOOT_AT_COORD, ped, 0f, 0f, 0f, 0);
                        }
                        break;

                    case Constants.TURRET_STATE_AIMING:
                        if (nearestEnemy != null)
                        {
                            if (distanceToEnemy <= Constants.TURRET_FULL_AUTO_RANGE)
                            {
                                // Enemy now in range - engage
                                Function.Call((Hash)Constants.NATIVE_SET_PED_FIRING_PATTERN, ped, Constants.FIRING_PATTERN_FULL_AUTO);
                                ped.Task.FightAgainst(nearestEnemy);
                                _turretPedStates[ped] = Constants.TURRET_STATE_FIGHTING;
                                anyFighting = true;
                                anyInRange = true;
                            }
                            else
                            {
                                // Keep aiming
                                ped.Task.AimAt(nearestEnemy, -1);
                            }
                        }
                        else
                        {
                            // Enemy gone
                            Function.Call((Hash)Constants.NATIVE_SET_PED_FIRING_PATTERN, ped, Constants.FIRING_PATTERN_DEFAULT);
                            Function.Call((Hash)Constants.NATIVE_TASK_VEHICLE_SHOOT_AT_COORD, ped, 0f, 0f, 0f, 0);
                            _turretPedStates[ped] = Constants.TURRET_STATE_IDLE;
                        }
                        break;

                    case Constants.TURRET_STATE_FIGHTING:
                        if (nearestEnemy != null)
                        {
                            if (distanceToEnemy <= Constants.TURRET_FULL_AUTO_RANGE)
                            {
                                // Continue fighting
                                Function.Call((Hash)Constants.NATIVE_SET_PED_FIRING_PATTERN, ped, Constants.FIRING_PATTERN_FULL_AUTO);
                                ped.Task.FightAgainst(nearestEnemy);
                                anyFighting = true;
                                anyInRange = true;
                            }
                            else
                            {
                                // Out of range - fall back to aiming
                                Function.Call((Hash)Constants.NATIVE_SET_PED_FIRING_PATTERN, ped, Constants.FIRING_PATTERN_DEFAULT);
                                ped.Task.AimAt(nearestEnemy, -1);
                                _turretPedStates[ped] = Constants.TURRET_STATE_AIMING;
                            }
                        }
                        else
                        {
                            // No more enemies
                            Function.Call((Hash)Constants.NATIVE_SET_PED_FIRING_PATTERN, ped, Constants.FIRING_PATTERN_DEFAULT);
                            Function.Call((Hash)Constants.NATIVE_TASK_VEHICLE_SHOOT_AT_COORD, ped, 0f, 0f, 0f, 0);
                            _turretPedStates[ped] = Constants.TURRET_STATE_IDLE;
                        }
                        break;
                }

                // Check for damage - OPTIMIZED: TryGetValue instead of ContainsKey + access
                int currentHealth = ped.Health;
                int lastHealth = _turretPedLastHealth.TryGetValue(ped, out int health) ? health : ped.MaxHealth;

                if (currentHealth < lastHealth &&
                    currentHealth <= ped.MaxHealth * Constants.TURRET_CREW_DAMAGE_THRESHOLD &&
                    lastHealth > ped.MaxHealth * Constants.TURRET_CREW_DAMAGE_THRESHOLD)
                {
                    anyDamaged = true;
                }

                _turretPedLastHealth[ped] = currentHealth;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "UpdateTurretPedState");
            }
        }

        /// <summary>
        /// Find the nearest enemy to a ped
        /// PERFORMANCE OPTIMIZED: Manual loop instead of LINQ to avoid allocations
        /// Uses HashSet for O(1) turret ped check, caches player relationship group
        /// </summary>
        private Ped FindNearestEnemy(Ped turretPed)
        {
            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                    return null;

                Ped[] nearbyPeds = World.GetNearbyPeds(turretPed, Constants.TURRET_AIM_RANGE);
                if (nearbyPeds == null || nearbyPeds.Length == 0)
                    return null;

                // Cache player relationship group (update every ~1 second to reduce native calls)
                long currentTick = DateTime.Now.Ticks;
                if (currentTick - _lastRelationshipGroupUpdate > 10_000_000) // 1 second
                {
                    _cachedPlayerRelationshipGroup = player.RelationshipGroup;
                    _lastRelationshipGroupUpdate = currentTick;
                }

                Vector3 turretPos = turretPed.Position;
                Ped nearestEnemy = null;
                float nearestDistSq = float.MaxValue;
                int playerHandle = player.Handle;

                // Manual loop - no LINQ allocations
                for (int i = 0; i < nearbyPeds.Length; i++)
                {
                    Ped p = nearbyPeds[i];

                    // Quick null/validity checks first
                    if (p == null || !p.Exists() || !p.IsAlive)
                        continue;

                    // Skip player
                    if (p.Handle == playerHandle)
                        continue;

                    // O(1) check if this is one of our turret peds
                    if (_turretPedHandles.Contains(p.Handle))
                        continue;

                    // Check relationship (enemy only)
                    if (p.RelationshipGroup == _cachedPlayerRelationshipGroup)
                        continue;

                    // Calculate squared distance (avoid sqrt for comparison)
                    Vector3 pPos = p.Position;
                    float dx = pPos.X - turretPos.X;
                    float dy = pPos.Y - turretPos.Y;
                    float dz = pPos.Z - turretPos.Z;
                    float distSq = dx * dx + dy * dy + dz * dz;

                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearestEnemy = p;
                    }
                }

                return nearestEnemy;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "FindNearestEnemy");
                return null;
            }
        }

        /// <summary>
        /// Handle collective announcement messages
        /// </summary>
        private void UpdateCollectiveMessages(bool anyFighting, bool anyInRange, bool anyDamaged)
        {
            int announceMode = _settings.GetIntSetting("turretCrewAnnouncements");
            if (announceMode == Constants.TURRET_ANNOUNCE_OFF)
                return;

            bool announceFiring = (announceMode == Constants.TURRET_ANNOUNCE_FIRING_ONLY ||
                                   announceMode == Constants.TURRET_ANNOUNCE_BOTH);
            bool announceApproaching = (announceMode == Constants.TURRET_ANNOUNCE_APPROACHING_ONLY ||
                                        announceMode == Constants.TURRET_ANNOUNCE_BOTH);

            // Engagement message (firing)
            if (announceFiring)
            {
                if (anyFighting && anyInRange && !_hasEngagedMessagePlayed)
                {
                    Announce("Turret crew engaging enemies", false, false);
                    _hasEngagedMessagePlayed = true;
                    _hasOutOfRangeMessagePlayed = false;
                }
                else if (!anyInRange && _hasEngagedMessagePlayed && !_hasOutOfRangeMessagePlayed)
                {
                    Announce("Turret crew holding fire", false, false);
                    _hasOutOfRangeMessagePlayed = true;
                    _hasEngagedMessagePlayed = false;
                }
            }

            // Damage message (approaching/critical)
            if (announceApproaching)
            {
                if (anyDamaged && !_hasDamagedMessagePlayed)
                {
                    Announce("Turret crew heavily damaged", false, false);
                    _hasDamagedMessagePlayed = true;
                }
                else if (!anyDamaged && _hasDamagedMessagePlayed)
                {
                    _hasDamagedMessagePlayed = false;
                }
            }
        }

        /// <summary>
        /// Handle a turret ped death
        /// </summary>
        private void HandleTurretPedDeath(int index)
        {
            try
            {
                int announceMode = _settings.GetIntSetting("turretCrewAnnouncements");
                bool announceApproaching = (announceMode == Constants.TURRET_ANNOUNCE_APPROACHING_ONLY ||
                                            announceMode == Constants.TURRET_ANNOUNCE_BOTH);

                if (announceApproaching && (DateTime.Now - _lastDeathMessageTime).TotalSeconds > 5)
                {
                    Announce("Turret crew member killed", false, false);
                    _lastDeathMessageTime = DateTime.Now;
                }

                RemoveTurretPed(index);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HandleTurretPedDeath");
            }
        }

        /// <summary>
        /// Remove a turret ped from tracking and delete it
        /// </summary>
        private void RemoveTurretPed(int index)
        {
            try
            {
                if (index < 0 || index >= _turretPeds.Count)
                    return;

                Ped ped = _turretPeds[index];
                if (ped != null)
                {
                    _turretPedHandles.Remove(ped.Handle);  // Remove from HashSet
                    if (ped.Exists())
                    {
                        ped.Delete();
                    }
                }

                _turretPedStates.Remove(ped);
                _turretPedLastHealth.Remove(ped);
                _turretPeds.RemoveAt(index);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "RemoveTurretPed");
            }
        }

        /// <summary>
        /// Clean up all turret peds
        /// </summary>
        private void CleanupTurretPeds()
        {
            try
            {
                if (_turretPeds != null && _turretPeds.Count > 0)
                {
                    foreach (Ped ped in _turretPeds)
                    {
                        if (ped != null && ped.Exists())
                        {
                            ped.Delete();
                        }
                    }
                    _turretPeds.Clear();
                    _turretPedHandles.Clear();  // Clear HashSet
                    _turretPedStates.Clear();
                    _turretPedLastHealth.Clear();
                }

                _vehicle = null;
                _isSpawned = false;
                ResetMessageFlags();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CleanupTurretPeds");
            }
        }

        /// <summary>
        /// Reset message tracking flags
        /// </summary>
        private void ResetMessageFlags()
        {
            _hasEngagedMessagePlayed = false;
            _hasOutOfRangeMessagePlayed = false;
            _hasDamagedMessagePlayed = false;
            _lastDeathMessageTime = DateTime.MinValue;
        }

        /// <summary>
        /// Announce a message via speech
        /// </summary>
        private void Announce(string message, bool forceAnnounce, bool ignoreSettings)
        {
            try
            {
                if (ignoreSettings)
                {
                    _audio?.Speak(message, forceAnnounce);
                    return;
                }

                int announceMode = _settings.GetIntSetting("turretCrewAnnouncements");
                if (announceMode != Constants.TURRET_ANNOUNCE_OFF)
                {
                    _audio?.Speak(message, forceAnnounce);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Announce");
            }
        }

        /// <summary>
        /// Check if player's current vehicle supports turret crew.
        /// Uses dynamic detection for modded vehicles.
        /// </summary>
        public bool IsCurrentVehicleTurretCapable()
        {
            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists() || !player.IsInVehicle())
                    return false;

                Vehicle vehicle = player.CurrentVehicle;
                if (vehicle == null || !vehicle.Exists())
                    return false;

                // First check: does vehicle have weapons at all?
                bool hasWeapons = Function.Call<bool>(Hash.DOES_VEHICLE_HAVE_WEAPONS, vehicle);
                if (!hasWeapons)
                    return false;

                // Check if we have hardcoded seats for this hash (stock vehicles)
                int vehicleHash = vehicle.Model.Hash;
                if (Constants.TURRET_VEHICLE_SEATS.ContainsKey(vehicleHash))
                    return true;

                // For modded vehicles, dynamically detect turret seats
                int[] turretSeats = DetectTurretSeats(vehicle);
                return turretSeats.Length > 0;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "IsCurrentVehicleTurretCapable");
                return false;
            }
        }

        /// <summary>
        /// Get status text for display
        /// </summary>
        public string GetStatusText()
        {
            if (!_isSpawned)
                return "Turret Crew: Inactive";

            return $"Turret Crew: {_turretPeds.Count} gunner(s) active";
        }
    }
}
