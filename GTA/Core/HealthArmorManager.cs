using System;
using GTA;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Monitors player health and armor, announcing threshold changes via TTS.
    /// Tracks death/respawn state and provides on-demand status reporting.
    /// Health model: effective health = Health - 100 (first 100 is internal filler),
    /// effective max = MaxHealth - 100 (usually 100 effective HP).
    /// Armor range: 0-100.
    /// Derives from MonitorBase&lt;Ped&gt; which supplies throttling, the enabled
    /// setting check, and exception handling.
    /// </summary>
    public class HealthArmorManager : MonitorBase<Ped>
    {
        #region Constants

        private const long UPDATE_INTERVAL = 1_000;             // 1 second
        private const long THRESHOLD_COOLDOWN = 3_000;          // 3 seconds between threshold announcements

        #endregion

        #region Fields

        // Previous state tracking
        private int _lastHealthPercent;
        private int _lastArmorPercent;
        private bool _wasDead;

        // Threshold tracking to prevent spam
        private int _lastHealthThreshold;
        private int _lastArmorThreshold;
        private long _lastThresholdAnnounceTick;

        #endregion

        #region Construction

        public HealthArmorManager(AudioManager audio, SettingsManager settings)
            : base(audio, settings)
        {
            _lastHealthPercent = 100;
            _lastArmorPercent = 0;
            _wasDead = false;
            _lastHealthThreshold = 100;
            _lastArmorThreshold = 0;
            _lastThresholdAnnounceTick = 0;
        }

        #endregion

        #region MonitorBase Overrides

        protected override long UpdateIntervalMs => UPDATE_INTERVAL;

        protected override string EnabledSettingKey => "announceHealth";

        protected override bool ValidateSubject(Ped player)
        {
            return player != null && player.Exists();
        }

        protected override void OnUpdate(Ped player, long currentTick)
        {
            bool isDead = player.IsDead;

            // Death detection
            if (isDead && !_wasDead)
            {
                Audio.Speak("Wasted", true);
                _wasDead = true;
                _lastHealthPercent = 0;
                return;
            }

            // Respawn detection
            if (!isDead && _wasDead)
            {
                Audio.Speak("Respawned", true);
                _wasDead = false;
                // Reset thresholds so we don't immediately announce after respawn
                _lastHealthPercent = 100;
                _lastArmorPercent = 0;
                _lastHealthThreshold = 100;
                _lastArmorThreshold = 0;
                return;
            }

            if (isDead)
                return;

            int healthPercent = GetHealthPercent(player);
            int armorPercent = Math.Max(0, Math.Min(100, player.Armor));

            // Check health thresholds (only announce drops)
            if (healthPercent < _lastHealthPercent)
            {
                int newThreshold = GetHealthThreshold(healthPercent);
                if (newThreshold != _lastHealthThreshold &&
                    currentTick - _lastThresholdAnnounceTick > THRESHOLD_COOLDOWN)
                {
                    string message = GetHealthThresholdMessage(healthPercent);
                    if (message != null)
                    {
                        Audio.Speak(message, true);
                        _lastThresholdAnnounceTick = currentTick;
                    }
                    _lastHealthThreshold = newThreshold;
                }
            }

            // Check armor thresholds (only announce drops)
            if (armorPercent < _lastArmorPercent)
            {
                int newArmorThreshold = GetArmorThreshold(armorPercent);
                if (newArmorThreshold != _lastArmorThreshold &&
                    currentTick - _lastThresholdAnnounceTick > THRESHOLD_COOLDOWN)
                {
                    string message = GetArmorThresholdMessage(armorPercent);
                    if (message != null)
                    {
                        Audio.Speak(message, true);
                        _lastThresholdAnnounceTick = currentTick;
                    }
                    _lastArmorThreshold = newArmorThreshold;
                }
            }

            _lastHealthPercent = healthPercent;
            _lastArmorPercent = armorPercent;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Speak the player's current health and armor percentages on demand.
        /// </summary>
        public void AnnounceStatus(Ped player)
        {
            if (player == null || !player.Exists())
                return;

            try
            {
                if (player.IsDead)
                {
                    Audio.Speak("You are dead", true);
                    return;
                }

                int healthPercent = GetHealthPercent(player);
                int armorPercent = Math.Max(0, Math.Min(100, player.Armor));

                Audio.Speak($"Health {healthPercent} percent, Armor {armorPercent} percent", true);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HealthArmorManager.AnnounceStatus");
            }
        }

        #endregion

        #region Threshold Helpers

        /// <summary>
        /// Calculate effective health percentage.
        /// GTA V: Health range is 100 (dead) to MaxHealth (full), first 100 is filler.
        /// </summary>
        private static int GetHealthPercent(Ped player)
        {
            int effectiveHealth = player.Health - 100;
            int effectiveMax = player.MaxHealth - 100;
            return effectiveMax > 0
                ? Math.Max(0, Math.Min(100, (effectiveHealth * 100) / effectiveMax))
                : 0;
        }

        /// <summary>
        /// Get the threshold bucket for health: 0 (critical), 15, 25, 50, 75, 100.
        /// Used to detect when health crosses a boundary.
        /// </summary>
        private static int GetHealthThreshold(int percent)
        {
            if (percent < 15) return 0;
            if (percent < 25) return 15;
            if (percent < 50) return 25;
            if (percent < 75) return 50;
            return 100;
        }

        /// <summary>
        /// Get the TTS message for the current health level, or null if none.
        /// </summary>
        private static string GetHealthThresholdMessage(int percent)
        {
            if (percent < 15) return "Health critical";
            if (percent < 25) return "Health low";
            if (percent < 50) return "Health below half";
            if (percent < 75) return "Health below 75 percent";
            return null;
        }

        /// <summary>
        /// Get the threshold bucket for armor: 0, 25, 50, 75, 100.
        /// </summary>
        private static int GetArmorThreshold(int percent)
        {
            if (percent <= 0) return 0;
            if (percent < 25) return 25;
            if (percent < 50) return 50;
            if (percent < 75) return 75;
            return 100;
        }

        /// <summary>
        /// Get the TTS message for the current armor level, or null if none.
        /// </summary>
        private static string GetArmorThresholdMessage(int percent)
        {
            if (percent <= 0) return "Armor depleted";
            if (percent < 25) return "Armor critical";
            if (percent < 50) return "Armor below half";
            if (percent < 75) return "Armor below 75 percent";
            return null;
        }

        #endregion
    }
}
