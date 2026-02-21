using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Provides combat accessibility features for blind players.
    /// Announces damage direction, nearest enemy location, ammo counts,
    /// and combat state transitions via TTS.
    /// </summary>
    public class CombatAssistManager
    {
        private readonly AudioManager _audio;
        private readonly SettingsManager _settings;

        // Combat state tracking
        private bool _wasInCombat;

        // Throttle tracking (ticks, using DateTime.Now.Ticks)
        private long _lastDamageAnnounceTick;
        private long _lastEnemyAnnounceTick;
        private long _lastAmmoAnnounceTick;

        // Cooldowns in ticks (1 second = 10,000,000 ticks)
        private const long UPDATE_INTERVAL = 2_000_000;            // 200ms throttle
        private const long DAMAGE_ANNOUNCE_COOLDOWN = 20_000_000;  // 2 seconds
        private const long ENEMY_ANNOUNCE_COOLDOWN = 30_000_000;   // 3 seconds
        private const long AMMO_ANNOUNCE_COOLDOWN = 10_000_000;    // 1 second

        private long _lastUpdateTick;

        // Cached native hash for GET_RELATIONSHIP_BETWEEN_PEDS
        private static readonly Hash _getRelationshipBetweenPeds = Hash.GET_RELATIONSHIP_BETWEEN_PEDS;

        public CombatAssistManager(AudioManager audio, SettingsManager settings)
        {
            _audio = audio;
            _settings = settings;
            _wasInCombat = false;
        }

        /// <summary>
        /// Main update loop - call from OnTick. Handles passive combat announcements.
        /// Checks for damage events and combat state transitions.
        /// </summary>
        public void Update(Ped player, Vector3 playerPos, long currentTick)
        {
            if (player == null || !player.Exists() || player.IsDead)
                return;

            if (!_settings.GetSetting("announceCombat"))
                return;

            // Throttle: max once per 200ms
            if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                return;
            _lastUpdateTick = currentTick;

            try
            {
                // Check combat state transitions
                UpdateCombatState(player);

                // Check for damage and announce direction
                UpdateDamageDirection(player, playerPos, currentTick);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CombatAssistManager.Update");
            }
        }

        /// <summary>
        /// Announce combat enter/exit transitions.
        /// Tracks _wasInCombat to only fire on state change.
        /// </summary>
        private void UpdateCombatState(Ped player)
        {
            bool isInCombat = player.IsInCombat;

            if (isInCombat && !_wasInCombat)
            {
                _audio.Speak("In combat");
                if (Logger.IsDebugEnabled) Logger.Debug("CombatAssistManager: Player entered combat");
            }
            else if (!isInCombat && _wasInCombat)
            {
                _audio.Speak("Combat ended");
                if (Logger.IsDebugEnabled) Logger.Debug("CombatAssistManager: Player exited combat");
            }

            _wasInCombat = isInCombat;
        }

        /// <summary>
        /// Detect when the player takes damage and announce the direction of the attacker.
        /// Scans nearby hostile peds in combat against the player to find the damage source,
        /// then calculates a relative direction (front, behind, left, right) based on
        /// the angle between the player's heading and the attacker's position.
        /// </summary>
        private void UpdateDamageDirection(Ped player, Vector3 playerPos, long currentTick)
        {
            // Throttle: max once per 2 seconds
            if (currentTick - _lastDamageAnnounceTick < DAMAGE_ANNOUNCE_COOLDOWN)
                return;

            // Check if player has been damaged by any ped
            bool wasDamaged = Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ANY_PED, player);
            if (!wasDamaged)
                return;

            // Clear the damage flag so we don't re-detect the same hit
            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, player);

            // Find the most likely attacker by scanning nearby hostile peds
            Ped attacker = FindNearestHostilePed(player, playerPos, 50f);

            if (attacker != null && attacker.Exists())
            {
                string direction = GetRelativeDirection(player, playerPos, attacker.Position);
                _audio.Speak($"Damage from the {direction}", true);
                if (Logger.IsDebugEnabled) Logger.Debug($"CombatAssistManager: Damage from {direction}");
            }
            else
            {
                // Could not identify attacker, just announce damage
                _audio.Speak("Taking damage", true);
            }

            _lastDamageAnnounceTick = currentTick;
        }

        /// <summary>
        /// On-demand: Find and announce the nearest hostile ped's direction and distance.
        /// Call this from a keybind or menu action.
        /// </summary>
        public void AnnounceNearestEnemy(Ped player, Vector3 playerPos)
        {
            if (player == null || !player.Exists() || player.IsDead)
                return;

            try
            {
                long currentTick = DateTime.Now.Ticks;
                if (currentTick - _lastEnemyAnnounceTick < ENEMY_ANNOUNCE_COOLDOWN)
                    return;
                _lastEnemyAnnounceTick = currentTick;

                Ped nearest = FindNearestHostilePed(player, playerPos, 50f);

                if (nearest != null && nearest.Exists())
                {
                    float distance = playerPos.DistanceTo(nearest.Position);
                    int distMeters = (int)Math.Round(distance);
                    string direction = GetRelativeDirection(player, playerPos, nearest.Position);
                    _audio.Speak($"Enemy {distMeters} meters {direction}", true);
                    if (Logger.IsDebugEnabled) Logger.Debug($"CombatAssistManager: Nearest enemy {distMeters}m {direction}");
                }
                else
                {
                    _audio.Speak("No enemies nearby", true);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CombatAssistManager.AnnounceNearestEnemy");
            }
        }

        /// <summary>
        /// On-demand: Announce current weapon ammo status.
        /// Reports clip ammo and total ammo, or weapon name for melee/unarmed.
        /// </summary>
        public void AnnounceAmmo(Ped player)
        {
            if (player == null || !player.Exists() || player.IsDead)
                return;

            try
            {
                long currentTick = DateTime.Now.Ticks;
                if (currentTick - _lastAmmoAnnounceTick < AMMO_ANNOUNCE_COOLDOWN)
                    return;
                _lastAmmoAnnounceTick = currentTick;

                WeaponCollection weapons = player.Weapons;
                if (weapons == null)
                {
                    _audio.Speak("Unarmed", true);
                    return;
                }

                Weapon current = weapons.Current;
                if (current == null || current.Hash == WeaponHash.Unarmed)
                {
                    _audio.Speak("Unarmed", true);
                    return;
                }

                int maxClip = current.MaxAmmoInClip;

                // Melee weapons have no ammo - just announce the weapon type
                if (maxClip <= 0)
                {
                    _audio.Speak($"Melee weapon equipped", true);
                    return;
                }

                int inClip = current.AmmoInClip;
                int total = current.Ammo;

                _audio.Speak($"{inClip} in clip, {total} total", true);
                if (Logger.IsDebugEnabled) Logger.Debug($"CombatAssistManager: Ammo {inClip}/{maxClip} clip, {total} total");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CombatAssistManager.AnnounceAmmo");
            }
        }

        /// <summary>
        /// Find the nearest hostile ped within the given radius.
        /// Checks both relationship (hate/dislike = 4 or 5) and active combat status.
        /// </summary>
        private Ped FindNearestHostilePed(Ped player, Vector3 playerPos, float radius)
        {
            Ped[] nearbyPeds = World.GetNearbyPeds(playerPos, radius);
            if (nearbyPeds == null || nearbyPeds.Length == 0)
                return null;

            Ped nearest = null;
            float nearestDistSq = float.MaxValue;
            int playerHandle = player.Handle;

            for (int i = 0; i < nearbyPeds.Length; i++)
            {
                Ped ped = nearbyPeds[i];
                if (ped == null || !ped.Exists() || !ped.IsAlive)
                    continue;

                if (ped.Handle == playerHandle)
                    continue;

                // Check if hostile: relationship 4 (dislike) or 5 (hate), or actively in combat
                bool isHostile = false;

                int relationship = Function.Call<int>(_getRelationshipBetweenPeds, ped, player);
                if (relationship == 4 || relationship == 5)
                {
                    isHostile = true;
                }
                else if (ped.IsInCombatAgainst(player))
                {
                    isHostile = true;
                }

                if (!isHostile)
                    continue;

                // Squared distance for comparison (avoid sqrt)
                Vector3 pedPos = ped.Position;
                float dx = pedPos.X - playerPos.X;
                float dy = pedPos.Y - playerPos.Y;
                float dz = pedPos.Z - playerPos.Z;
                float distSq = dx * dx + dy * dy + dz * dz;

                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = ped;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Calculate a relative direction from the player to a target position,
        /// based on where the player is currently facing.
        /// Returns simplified directions: "ahead", "behind", "left", "right",
        /// "ahead-left", "ahead-right", "behind-left", "behind-right".
        /// </summary>
        private static string GetRelativeDirection(Ped player, Vector3 playerPos, Vector3 targetPos)
        {
            // Calculate the world angle from the player to the target
            double angleToTarget = SpatialCalculator.CalculateAngle(playerPos.X, playerPos.Y, targetPos.X, targetPos.Y);

            // Get the player's facing heading
            double playerHeading = player.Heading;

            // Calculate relative angle: how far the target is from where the player faces
            // Both are in GTA V's coordinate system (0=N, clockwise but mirrored E/W)
            double relativeAngle = angleToTarget - playerHeading;

            // Normalize to 0-360
            relativeAngle = ((relativeAngle % 360) + 360) % 360;

            // Convert to 8 relative directions
            // 0 = directly ahead, 180 = directly behind
            // GTA V mirror: increasing angle goes counterclockwise (left)
            if (relativeAngle < 22.5 || relativeAngle >= 337.5)
                return "ahead";
            if (relativeAngle < 67.5)
                return "ahead-left";
            if (relativeAngle < 112.5)
                return "left";
            if (relativeAngle < 157.5)
                return "behind-left";
            if (relativeAngle < 202.5)
                return "behind";
            if (relativeAngle < 247.5)
                return "behind-right";
            if (relativeAngle < 292.5)
                return "right";
            return "ahead-right";
        }
    }
}
