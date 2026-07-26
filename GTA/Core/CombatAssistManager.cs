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
    /// Derives from MonitorBase&lt;Ped&gt; which supplies throttling, the enabled
    /// setting check, and exception handling.
    /// </summary>
    public class CombatAssistManager : MonitorBase<Ped>
    {
        #region Constants

        // Cooldowns in ticks (1 second = 10,000,000 ticks)
        private const long UPDATE_INTERVAL = 200;                  // 200ms throttle
        private const long DAMAGE_ANNOUNCE_COOLDOWN = 2_000;      // 2 seconds
        private const long ENEMY_ANNOUNCE_COOLDOWN = 3_000;       // 3 seconds
        private const long AMMO_ANNOUNCE_COOLDOWN = 1_000;        // 1 second

        // Cached native hash for GET_RELATIONSHIP_BETWEEN_PEDS
        private static readonly Hash _getRelationshipBetweenPeds = Hash.GET_RELATIONSHIP_BETWEEN_PEDS;

        #endregion

        #region Fields

        // Combat state tracking
        private bool _wasInCombat;

        // Throttle tracking (game-time milliseconds via Game.GameTime)
        private long _lastDamageAnnounceTick;
        private long _lastEnemyAnnounceTick;
        private long _lastAmmoAnnounceTick;

        #endregion

        #region Construction

        public CombatAssistManager(AudioManager audio, SettingsManager settings)
            : base(audio, settings)
        {
            _wasInCombat = false;
        }

        #endregion

        #region MonitorBase Overrides

        protected override long UpdateIntervalMs => UPDATE_INTERVAL;

        protected override string EnabledSettingKey => "announceCombat";

        protected override bool ValidateSubject(Ped player)
        {
            return player != null && player.Exists() && !player.IsDead;
        }

        protected override void OnUpdate(Ped player, long currentTick)
        {
            // Check combat state transitions
            UpdateCombatState(player);

            // Check for damage and announce direction
            UpdateDamageDirection(player, player.Position, currentTick);
        }

        #endregion

        #region Passive Monitoring

        /// <summary>
        /// Announce combat enter/exit transitions.
        /// Tracks _wasInCombat to only fire on state change.
        /// </summary>
        private void UpdateCombatState(Ped player)
        {
            bool isInCombat = player.IsInCombat;

            if (isInCombat && !_wasInCombat)
            {
                Audio.Speak("In combat");
                if (Logger.IsDebugEnabled) Logger.Debug("CombatAssistManager: Player entered combat");
            }
            else if (!isInCombat && _wasInCombat)
            {
                Audio.Speak("Combat ended");
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
                Audio.Speak($"Damage from the {direction}", true);
                if (Logger.IsDebugEnabled) Logger.Debug($"CombatAssistManager: Damage from {direction}");
            }
            else
            {
                // Could not identify attacker, just announce damage
                Audio.Speak("Taking damage", true);
            }

            _lastDamageAnnounceTick = currentTick;
        }

        #endregion

        #region Public API - on-demand announcements

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
                long currentTick = Game.GameTime;
                if (currentTick - _lastEnemyAnnounceTick < ENEMY_ANNOUNCE_COOLDOWN)
                    return;
                _lastEnemyAnnounceTick = currentTick;

                Ped nearest = FindNearestHostilePed(player, playerPos, 50f);

                if (nearest != null && nearest.Exists())
                {
                    float distance = playerPos.DistanceTo(nearest.Position);
                    int distMeters = (int)Math.Round(distance);
                    string direction = GetRelativeDirection(player, playerPos, nearest.Position);
                    Audio.Speak($"Enemy {distMeters} meters {direction}", true);
                    if (Logger.IsDebugEnabled) Logger.Debug($"CombatAssistManager: Nearest enemy {distMeters}m {direction}");
                }
                else
                {
                    Audio.Speak("No enemies nearby", true);
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
                long currentTick = Game.GameTime;
                if (currentTick - _lastAmmoAnnounceTick < AMMO_ANNOUNCE_COOLDOWN)
                    return;
                _lastAmmoAnnounceTick = currentTick;

                WeaponCollection weapons = player.Weapons;
                if (weapons == null)
                {
                    Audio.Speak("Unarmed", true);
                    return;
                }

                Weapon current = weapons.Current;
                if (current == null || current.Hash == WeaponHash.Unarmed)
                {
                    Audio.Speak("Unarmed", true);
                    return;
                }

                int maxClip = current.MaxAmmoInClip;

                // Melee weapons have no ammo - just announce the weapon type
                if (maxClip <= 0)
                {
                    Audio.Speak("Melee weapon equipped", true);
                    return;
                }

                int inClip = current.AmmoInClip;
                int total = current.Ammo;

                Audio.Speak($"{inClip} in clip, {total} total", true);
                if (Logger.IsDebugEnabled) Logger.Debug($"CombatAssistManager: Ammo {inClip}/{maxClip} clip, {total} total");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "CombatAssistManager.AnnounceAmmo");
            }
        }

        #endregion

        #region Helpers

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

        #endregion
    }
}
