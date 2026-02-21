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
    /// Features: minimum engagement range (self-damage protection), vehicle threat detection,
    /// priority-based targeting, and combat effectiveness tuning.
    /// PERFORMANCE OPTIMIZED: Manual loops, HashSet for O(1) lookups, squared distances
    /// </summary>
    public class TurretCrewManager
    {
        private readonly SettingsManager _settings;
        private readonly AudioManager _audio;

        // Active turret crew state
        private Vehicle _vehicle;
        private int _vehicleHandle;
        private List<Ped> _turretPeds;
        private HashSet<int> _turretPedHandles;  // O(1) lookup for turret ped contains check
        private Dictionary<Ped, int> _turretPedStates;  // TurretPedState enum values
        private Dictionary<Ped, int> _turretPedLastHealth;
        private bool _isSpawned;

        // Cached player state (reduce native calls)
        private RelationshipGroup _cachedPlayerRelationshipGroup;
        private long _lastRelationshipGroupUpdate;
        private int _cachedPlayerHandle;

        // Message tracking to prevent spam
        private bool _hasEngagedMessagePlayed;
        private bool _hasOutOfRangeMessagePlayed;
        private bool _hasDamagedMessagePlayed;
        private bool _hasTooCloseMessagePlayed;
        private DateTime _lastDeathMessageTime;

        // Tick throttling
        private long _lastUpdateTick;

        // Pre-cached Hash values to avoid repeated casting
        private static readonly Hash _setFiringPatternHash = (Hash)Constants.NATIVE_SET_PED_FIRING_PATTERN;
        private static readonly Hash _shootAtCoordHash = (Hash)Constants.NATIVE_TASK_VEHICLE_SHOOT_AT_COORD;
        private static readonly Hash _isTurretSeatHash = (Hash)Constants.NATIVE_IS_TURRET_SEAT;

        private static readonly Hash _hasBeenDamagedByHash = (Hash)Constants.NATIVE_HAS_ENTITY_BEEN_DAMAGED_BY;
        private static readonly Hash _isPedInCombatHash = (Hash)Constants.NATIVE_IS_PED_IN_COMBAT;
        private static readonly Hash _setCombatAbilityHash = (Hash)Constants.NATIVE_SET_PED_COMBAT_ABILITY;
        private static readonly Hash _setCombatRangeHash = (Hash)Constants.NATIVE_SET_PED_COMBAT_RANGE;

        public TurretCrewManager(SettingsManager settings, AudioManager audio)
        {
            _settings = settings;
            _audio = audio;
            _turretPeds = new List<Ped>(8);
            _turretPedHandles = new HashSet<int>();
            _turretPedStates = new Dictionary<Ped, int>(8);
            _turretPedLastHealth = new Dictionary<Ped, int>(8);
            _isSpawned = false;
            _hasEngagedMessagePlayed = false;
            _hasOutOfRangeMessagePlayed = false;
            _hasDamagedMessagePlayed = false;
            _hasTooCloseMessagePlayed = false;
            _lastDeathMessageTime = DateTime.MinValue;
            _cachedPlayerRelationshipGroup = default;
            _lastRelationshipGroupUpdate = 0;
            _cachedPlayerHandle = 0;
        }

        public bool IsSpawned => _isSpawned;

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

                _vehicleHandle = _vehicle.Handle;
                _cachedPlayerHandle = player.Handle;
                int vehicleHash = _vehicle.Model.Hash;

                // Check if vehicle has weapons
                bool hasWeapons = Function.Call<bool>(Hash.DOES_VEHICLE_HAVE_WEAPONS, _vehicle);
                if (!hasWeapons)
                {
                    Announce("Failed: Vehicle has no weapons", true, true);
                    return;
                }

                int playerSeat = GetPlayerSeatIndex();

                // Dynamically detect turret seats - works with modded vehicles
                int[] validSeats;
                if (Constants.TURRET_VEHICLE_SEATS.TryGetValue(vehicleHash, out int[] knownSeats))
                {
                    validSeats = knownSeats;
                    if (Logger.IsDebugEnabled) Logger.Debug($"TurretCrewManager: Using known turret seats for hash {vehicleHash}");
                }
                else
                {
                    validSeats = DetectTurretSeats(_vehicle);
                    if (validSeats.Length == 0)
                    {
                        Announce("Failed: No turret seats detected", true, true);
                        return;
                    }
                    Logger.Info($"TurretCrewManager: Dynamically detected {validSeats.Length} turret seat(s) for modded vehicle");
                }

                // Clear all tracking collections
                _turretPeds.Clear();
                _turretPedHandles.Clear();
                _turretPedStates.Clear();
                _turretPedLastHealth.Clear();

                Logger.Info($"TurretCrewManager: Vehicle hash {vehicleHash}, player in seat {playerSeat}, valid turret seats: [{string.Join(", ", validSeats)}]");

                int spawnedCount = 0;
                foreach (int seatIndex in validSeats)
                {
                    if (seatIndex == playerSeat)
                    {
                        if (Logger.IsDebugEnabled) Logger.Debug($"TurretCrewManager: Skipping seat {seatIndex} - player is in this seat");
                        continue;
                    }

                    bool seatFree = _vehicle.IsSeatFree((VehicleSeat)seatIndex);
                    if (!seatFree)
                    {
                        if (Logger.IsDebugEnabled) Logger.Debug($"TurretCrewManager: Skipping seat {seatIndex} - occupied");
                        continue;
                    }

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
                    _turretPedHandles.Add(ped.Handle);
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
                    _vehicle = null;
                    return;
                }

                // Update cached player info periodically
                if (currentTick - _lastRelationshipGroupUpdate > 10_000_000) // 1 second
                {
                    Ped player = Game.Player?.Character;
                    if (player != null && player.Exists())
                    {
                        _cachedPlayerRelationshipGroup = player.RelationshipGroup;
                        _cachedPlayerHandle = player.Handle;
                    }
                    _lastRelationshipGroupUpdate = currentTick;
                }

                bool anyFighting = false;
                bool anyInRange = false;
                bool anyDamaged = false;
                bool anyTooClose = false;

                // Iterate in reverse to safely remove dead peds
                for (int i = _turretPeds.Count - 1; i >= 0; i--)
                {
                    Ped ped = _turretPeds[i];

                    if (ped == null || !ped.Exists() || !ped.IsInVehicle(_vehicle))
                    {
                        RemoveTurretPed(i);
                        continue;
                    }

                    if (ped.IsDead)
                    {
                        HandleTurretPedDeath(i);
                        continue;
                    }

                    UpdateTurretPedState(ped, ref anyFighting, ref anyInRange, ref anyDamaged, ref anyTooClose);
                }

                UpdateCollectiveMessages(anyFighting, anyInRange, anyDamaged, anyTooClose);

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
        /// Configure a turret ped with proper combat settings
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

                // Combat effectiveness tuning
                ped.Accuracy = Constants.TURRET_CREW_ACCURACY;
                Function.Call(_setCombatAbilityHash, ped, Constants.TURRET_COMBAT_ABILITY_PROFESSIONAL);
                Function.Call(_setCombatRangeHash, ped, Constants.TURRET_COMBAT_RANGE_FAR);

                // Combat attributes for effective turret behavior
                // 2 = CanDoDrivebys
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 2, true);
                // 5 = CanFightArmedPedsWhenNotArmed
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 5, true);
                // 20 = CanTauntInVehicle
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 20, true);
                // 46 = UseVehicleAttack (use vehicle weapons against vehicles)
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 46, true);
                // 52 = UseVehicleAttackIfVehicleHasMountedGuns
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 52, true);

                // Config flag 132 (combat-related)
                Function.Call(Hash.SET_PED_CONFIG_FLAG, ped, 132, true);

                // Put ped in player's relationship group so they're friendly
                Ped player = Game.Player?.Character;
                if (player != null && player.Exists())
                {
                    uint playerGroup = Function.Call<uint>(Hash.GET_PED_RELATIONSHIP_GROUP_HASH, player);
                    Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, ped, playerGroup);
                }

                // Give them a weapon for when they dismount
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
                int playerHandle = player.Handle;
                for (int i = -1; i < totalSeats - 1; i++)
                {
                    Ped pedInSeat = _vehicle.GetPedOnSeat((VehicleSeat)i);
                    if (pedInSeat != null && pedInSeat.Handle == playerHandle)
                    {
                        return i;
                    }
                }

                return -2;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "GetPlayerSeatIndex");
                return -2;
            }
        }

        /// <summary>
        /// Dynamically detect turret seats in a vehicle using the IS_TURRET_SEAT native.
        /// </summary>
        private int[] DetectTurretSeats(Vehicle vehicle)
        {
            try
            {
                if (vehicle == null || !vehicle.Exists())
                    return Array.Empty<int>();

                int totalSeats = vehicle.PassengerCapacity + 1;
                List<int> turretSeats = new List<int>(totalSeats);

                for (int seatIndex = 0; seatIndex < totalSeats; seatIndex++)
                {
                    bool isTurret = Function.Call<bool>(_isTurretSeatHash, vehicle, seatIndex);
                    if (isTurret)
                    {
                        turretSeats.Add(seatIndex);
                        if (Logger.IsDebugEnabled) Logger.Debug($"TurretCrewManager: Seat {seatIndex} is a turret seat");
                    }
                }

                // Fallback for vehicles where IS_TURRET_SEAT doesn't work
                if (turretSeats.Count == 0)
                {
                    if (Logger.IsDebugEnabled) Logger.Debug("TurretCrewManager: IS_TURRET_SEAT found no turrets, trying fallback");
                    for (int seatIndex = 0; seatIndex < Math.Min(3, totalSeats); seatIndex++)
                    {
                        turretSeats.Add(seatIndex);
                        if (Logger.IsDebugEnabled) Logger.Debug($"TurretCrewManager: Adding seat {seatIndex} as potential turret (fallback)");
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
        /// Update a turret ped's combat state with priority-based targeting and minimum range enforcement.
        /// 3-tier engagement: dead zone (0-25m), full auto (25-80m), aimed fire (80-150m)
        /// </summary>
        private void UpdateTurretPedState(Ped ped, ref bool anyFighting, ref bool anyInRange, ref bool anyDamaged, ref bool anyTooClose)
        {
            try
            {
                int currentState = _turretPedStates.TryGetValue(ped, out int state) ? state : Constants.TURRET_STATE_IDLE;

                // Find the best target using priority system
                float distanceToEnemy;
                bool enemyInDeadZone;
                Ped bestTarget = FindBestTarget(ped, out distanceToEnemy, out enemyInDeadZone);

                if (enemyInDeadZone)
                    anyTooClose = true;

                switch (currentState)
                {
                    case Constants.TURRET_STATE_IDLE:
                        if (bestTarget != null)
                        {
                            if (distanceToEnemy <= Constants.TURRET_FULL_AUTO_RANGE)
                            {
                                EngageTarget(ped, bestTarget);
                                _turretPedStates[ped] = Constants.TURRET_STATE_FIGHTING;
                                anyFighting = true;
                                anyInRange = true;
                            }
                            else if (distanceToEnemy <= Constants.TURRET_AIM_RANGE)
                            {
                                ped.Task.AimAt(bestTarget, -1);
                                _turretPedStates[ped] = Constants.TURRET_STATE_AIMING;
                            }
                        }
                        else if (!enemyInDeadZone)
                        {
                            // No targets at all - use autonomous combat as fallback
                            ClearTargeting(ped);
                        }
                        break;

                    case Constants.TURRET_STATE_AIMING:
                        if (bestTarget != null)
                        {
                            if (distanceToEnemy <= Constants.TURRET_FULL_AUTO_RANGE)
                            {
                                EngageTarget(ped, bestTarget);
                                _turretPedStates[ped] = Constants.TURRET_STATE_FIGHTING;
                                anyFighting = true;
                                anyInRange = true;
                            }
                            else
                            {
                                ped.Task.AimAt(bestTarget, -1);
                            }
                        }
                        else
                        {
                            DisengageTarget(ped);
                            _turretPedStates[ped] = Constants.TURRET_STATE_IDLE;
                        }
                        break;

                    case Constants.TURRET_STATE_FIGHTING:
                        if (bestTarget != null)
                        {
                            if (distanceToEnemy <= Constants.TURRET_FULL_AUTO_RANGE)
                            {
                                // Continue fighting - re-issue task to keep targeting best priority
                                EngageTarget(ped, bestTarget);
                                anyFighting = true;
                                anyInRange = true;
                            }
                            else
                            {
                                // Out of full auto range - fall back to aiming
                                Function.Call(_setFiringPatternHash, ped, Constants.FIRING_PATTERN_DEFAULT);
                                ped.Task.AimAt(bestTarget, -1);
                                _turretPedStates[ped] = Constants.TURRET_STATE_AIMING;
                            }
                        }
                        else
                        {
                            DisengageTarget(ped);
                            _turretPedStates[ped] = Constants.TURRET_STATE_IDLE;
                        }
                        break;
                }

                // Check for damage
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
        /// Engage a target with full auto fire
        /// </summary>
        private void EngageTarget(Ped ped, Ped target)
        {
            Function.Call(_setFiringPatternHash, ped, Constants.FIRING_PATTERN_FULL_AUTO);
            ped.Task.FightAgainst(target);
        }

        /// <summary>
        /// Disengage from current target and clear firing
        /// </summary>
        private void DisengageTarget(Ped ped)
        {
            Function.Call(_setFiringPatternHash, ped, Constants.FIRING_PATTERN_DEFAULT);
            ClearTargeting(ped);
        }

        /// <summary>
        /// Clear targeting task on a ped
        /// </summary>
        private void ClearTargeting(Ped ped)
        {
            Function.Call(_shootAtCoordHash, ped, 0f, 0f, 0f, 0);
        }

        /// <summary>
        /// Find the best target using priority-based scoring.
        /// Considers: threat level (attacking > armed vehicle > armed foot > unarmed),
        /// distance, and minimum engagement range (25m dead zone).
        /// Also scans hostile vehicle occupants that GetNearbyPeds may miss.
        /// </summary>
        private Ped FindBestTarget(Ped turretPed, out float bestDistance, out bool enemyInDeadZone)
        {
            bestDistance = float.MaxValue;
            enemyInDeadZone = false;

            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists())
                    return null;

                Vector3 vehiclePos = _vehicle.Position;
                Vector3 turretPos = turretPed.Position;

                Ped bestTarget = null;
                int bestScore = -1;
                float bestDistSq = float.MaxValue;

                // === Phase 1: Scan nearby peds on foot ===
                Ped[] nearbyPeds = World.GetNearbyPeds(turretPed, Constants.TURRET_AIM_RANGE);
                if (nearbyPeds != null)
                {
                    for (int i = 0; i < nearbyPeds.Length; i++)
                    {
                        Ped p = nearbyPeds[i];
                        if (p == null || !p.Exists() || !p.IsAlive)
                            continue;

                        if (p.Handle == _cachedPlayerHandle)
                            continue;

                        if (_turretPedHandles.Contains(p.Handle))
                            continue;

                        // Check if hostile
                        if (p.RelationshipGroup == _cachedPlayerRelationshipGroup)
                            continue;

                        // Distance from the vehicle (for dead zone) and from turret ped (for engagement)
                        Vector3 pPos = p.Position;
                        float dxV = pPos.X - vehiclePos.X;
                        float dyV = pPos.Y - vehiclePos.Y;
                        float dzV = pPos.Z - vehiclePos.Z;
                        float distSqFromVehicle = dxV * dxV + dyV * dyV + dzV * dzV;

                        // Check dead zone relative to our vehicle
                        if (distSqFromVehicle < Constants.TURRET_MIN_ENGAGEMENT_RANGE_SQ)
                        {
                            enemyInDeadZone = true;
                            continue; // Too close - skip
                        }

                        float dxT = pPos.X - turretPos.X;
                        float dyT = pPos.Y - turretPos.Y;
                        float dzT = pPos.Z - turretPos.Z;
                        float distSqFromTurret = dxT * dxT + dyT * dyT + dzT * dzT;

                        if (distSqFromTurret > Constants.TURRET_AIM_RANGE_SQ)
                            continue;

                        int score = ScoreTarget(p, player);

                        // Higher score wins; equal score = closer wins
                        if (score > bestScore || (score == bestScore && distSqFromTurret < bestDistSq))
                        {
                            bestScore = score;
                            bestDistSq = distSqFromTurret;
                            bestTarget = p;
                        }
                    }
                }

                // === Phase 2: Scan hostile vehicle occupants ===
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(turretPed, Constants.TURRET_AIM_RANGE);
                if (nearbyVehicles != null)
                {
                    for (int i = 0; i < nearbyVehicles.Length; i++)
                    {
                        Vehicle v = nearbyVehicles[i];
                        if (v == null || !v.Exists())
                            continue;

                        // Skip our own vehicle
                        if (v.Handle == _vehicleHandle)
                            continue;

                        // Check distance from our vehicle for dead zone
                        Vector3 vPos = v.Position;
                        float dxV = vPos.X - vehiclePos.X;
                        float dyV = vPos.Y - vehiclePos.Y;
                        float dzV = vPos.Z - vehiclePos.Z;
                        float distSqFromVehicle = dxV * dxV + dyV * dyV + dzV * dzV;

                        if (distSqFromVehicle < Constants.TURRET_MIN_ENGAGEMENT_RANGE_SQ)
                        {
                            enemyInDeadZone = true;
                            continue;
                        }

                        float dxT = vPos.X - turretPos.X;
                        float dyT = vPos.Y - turretPos.Y;
                        float dzT = vPos.Z - turretPos.Z;
                        float distSqFromTurret = dxT * dxT + dyT * dyT + dzT * dzT;

                        if (distSqFromTurret > Constants.TURRET_AIM_RANGE_SQ)
                            continue;

                        // Check occupants for hostiles - scan driver + up to 3 passengers
                        Ped hostileOccupant = FindHostileOccupant(v);
                        if (hostileOccupant == null)
                            continue;

                        // Already counted via nearby peds? Skip duplicate
                        if (_turretPedHandles.Contains(hostileOccupant.Handle))
                            continue;

                        // Score vehicle-based target
                        int score = ScoreVehicleTarget(v, hostileOccupant, player);

                        if (score > bestScore || (score == bestScore && distSqFromTurret < bestDistSq))
                        {
                            bestScore = score;
                            bestDistSq = distSqFromTurret;
                            bestTarget = hostileOccupant;
                        }
                    }
                }

                if (bestTarget != null)
                {
                    bestDistance = (float)Math.Sqrt(bestDistSq);
                }

                return bestTarget;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "FindBestTarget");
                return null;
            }
        }

        /// <summary>
        /// Score a ped target based on threat level.
        /// Higher score = higher priority.
        /// </summary>
        private int ScoreTarget(Ped target, Ped player)
        {
            try
            {
                // Highest priority: actively attacking the player or our vehicle
                bool isInCombatWithPlayer = Function.Call<bool>(_isPedInCombatHash, target, player);
                if (isInCombatWithPlayer)
                    return Constants.TURRET_PRIORITY_ATTACKING;

                // Check if this ped has damaged our vehicle recently
                if (_vehicle != null && _vehicle.Exists())
                {
                    bool damagedVehicle = Function.Call<bool>(_hasBeenDamagedByHash, _vehicle, target, true);
                    if (damagedVehicle)
                        return Constants.TURRET_PRIORITY_ATTACKING;
                }

                // Armed on foot
                if (target.Weapons != null && target.Weapons.Current != null &&
                    target.Weapons.Current.Hash != WeaponHash.Unarmed)
                    return Constants.TURRET_PRIORITY_ARMED_ON_FOOT;

                // Hostile but unarmed
                return Constants.TURRET_PRIORITY_UNARMED;
            }
            catch
            {
                return Constants.TURRET_PRIORITY_UNARMED;
            }
        }

        /// <summary>
        /// Score a vehicle-based target. Returns higher scores for weaponized vehicles.
        /// </summary>
        private int ScoreVehicleTarget(Vehicle vehicle, Ped occupant, Ped player)
        {
            try
            {
                // Check if occupant is in combat with player
                bool isInCombatWithPlayer = Function.Call<bool>(_isPedInCombatHash, occupant, player);
                if (isInCombatWithPlayer)
                    return Constants.TURRET_PRIORITY_ATTACKING;

                // Check if this vehicle has damaged our vehicle
                if (_vehicle != null && _vehicle.Exists())
                {
                    bool damagedVehicle = Function.Call<bool>(_hasBeenDamagedByHash, _vehicle, vehicle, true);
                    if (damagedVehicle)
                        return Constants.TURRET_PRIORITY_ATTACKING;
                }

                // Weaponized vehicle
                bool hasWeapons = Function.Call<bool>(Hash.DOES_VEHICLE_HAVE_WEAPONS, vehicle);
                if (hasWeapons)
                    return Constants.TURRET_PRIORITY_ARMED_VEHICLE;

                // Regular hostile vehicle
                return Constants.TURRET_PRIORITY_ARMED_ON_FOOT;
            }
            catch
            {
                return Constants.TURRET_PRIORITY_UNARMED;
            }
        }

        /// <summary>
        /// Find a hostile occupant in a vehicle. Returns the driver if hostile, otherwise first hostile passenger.
        /// </summary>
        private Ped FindHostileOccupant(Vehicle vehicle)
        {
            try
            {
                // Check driver first
                Ped driver = vehicle.GetPedOnSeat(VehicleSeat.Driver);
                if (driver != null && driver.Exists() && driver.IsAlive &&
                    driver.Handle != _cachedPlayerHandle &&
                    driver.RelationshipGroup != _cachedPlayerRelationshipGroup)
                {
                    return driver;
                }

                // Check passengers (up to 4)
                int passengerCount = Math.Min(vehicle.PassengerCapacity, 4);
                for (int seat = 0; seat < passengerCount; seat++)
                {
                    Ped passenger = vehicle.GetPedOnSeat((VehicleSeat)seat);
                    if (passenger != null && passenger.Exists() && passenger.IsAlive &&
                        passenger.Handle != _cachedPlayerHandle &&
                        passenger.RelationshipGroup != _cachedPlayerRelationshipGroup)
                    {
                        return passenger;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Handle collective announcement messages including "too close" warning
        /// </summary>
        private void UpdateCollectiveMessages(bool anyFighting, bool anyInRange, bool anyDamaged, bool anyTooClose)
        {
            int announceMode = _settings.GetIntSetting("turretCrewAnnouncements");
            if (announceMode == Constants.TURRET_ANNOUNCE_OFF)
                return;

            bool announceFiring = (announceMode == Constants.TURRET_ANNOUNCE_FIRING_ONLY ||
                                   announceMode == Constants.TURRET_ANNOUNCE_BOTH);
            bool announceApproaching = (announceMode == Constants.TURRET_ANNOUNCE_APPROACHING_ONLY ||
                                        announceMode == Constants.TURRET_ANNOUNCE_BOTH);

            // "Too close" warning (firing category)
            if (announceFiring)
            {
                if (anyTooClose && !anyFighting && !_hasTooCloseMessagePlayed)
                {
                    Announce("Enemy too close, holding fire", false, false);
                    _hasTooCloseMessagePlayed = true;
                }
                else if (!anyTooClose && _hasTooCloseMessagePlayed)
                {
                    _hasTooCloseMessagePlayed = false;
                }

                // Engagement messages
                if (anyFighting && anyInRange && !_hasEngagedMessagePlayed)
                {
                    Announce("Turret crew engaging enemies", false, false);
                    _hasEngagedMessagePlayed = true;
                    _hasOutOfRangeMessagePlayed = false;
                    _hasTooCloseMessagePlayed = false;
                }
                else if (!anyInRange && !anyTooClose && _hasEngagedMessagePlayed && !_hasOutOfRangeMessagePlayed)
                {
                    Announce("Turret crew holding fire", false, false);
                    _hasOutOfRangeMessagePlayed = true;
                    _hasEngagedMessagePlayed = false;
                }
            }

            // Damage messages
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
                    _turretPedHandles.Remove(ped.Handle);
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
                    _turretPedHandles.Clear();
                    _turretPedStates.Clear();
                    _turretPedLastHealth.Clear();
                }

                _vehicle = null;
                _vehicleHandle = 0;
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
            _hasTooCloseMessagePlayed = false;
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

                bool hasWeapons = Function.Call<bool>(Hash.DOES_VEHICLE_HAVE_WEAPONS, vehicle);
                if (!hasWeapons)
                    return false;

                int vehicleHash = vehicle.Model.Hash;
                if (Constants.TURRET_VEHICLE_SEATS.ContainsKey(vehicleHash))
                    return true;

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
