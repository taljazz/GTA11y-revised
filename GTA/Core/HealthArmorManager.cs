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
    /// </summary>
    public class HealthArmorManager
    {
        private readonly AudioManager _audio;
        private readonly SettingsManager _settings;

        // Previous state tracking
        private int _lastHealthPercent;
        private int _lastArmorPercent;
        private bool _wasDead;

        // Threshold tracking to prevent spam
        private int _lastHealthThreshold;
        private int _lastArmorThreshold;
        private long _lastThresholdAnnounceTick;

        // Tick throttling (1 second = 10,000,000 ticks)
        private long _lastUpdateTick;
        private const long UPDATE_INTERVAL = 10_000_000;
        private const long THRESHOLD_COOLDOWN = 30_000_000; // 3 seconds between threshold announcements

        public HealthArmorManager(AudioManager audio, SettingsManager settings)
        {
            _audio = audio;
            _settings = settings;

            _lastHealthPercent = 100;
            _lastArmorPercent = 0;
            _wasDead = false;
            _lastHealthThreshold = 100;
            _lastArmorThreshold = 0;
            _lastThresholdAnnounceTick = 0;
            _lastUpdateTick = 0;
        }

        /// <summary>
        /// Periodically check player health/armor and announce changes.
        /// Call from OnTick. Throttled to 1-second intervals.
        /// </summary>
        public void Update(Ped player, long currentTick)
        {
            if (player == null || !player.Exists())
                return;

            if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                return;

            _lastUpdateTick = currentTick;

            if (!_settings.GetSetting("announceHealth"))
                return;

            try
            {
                bool isDead = player.IsDead;

                // Death detection
                if (isDead && !_wasDead)
                {
                    _audio.Speak("Wasted", true);
                    _wasDead = true;
                    _lastHealthPercent = 0;
                    return;
                }

                // Respawn detection
                if (!isDead && _wasDead)
                {
                    _audio.Speak("Respawned", true);
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

                // Calculate effective health percentage
                // GTA V: Health range is 100 (dead) to MaxHealth (full), first 100 is filler
                int effectiveHealth = player.Health - 100;
                int effectiveMax = player.MaxHealth - 100;
                int healthPercent = effectiveMax > 0
                    ? Math.Max(0, Math.Min(100, (effectiveHealth * 100) / effectiveMax))
                    : 0;

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
                            _audio.Speak(message, true);
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
                            _audio.Speak(message, true);
                            _lastThresholdAnnounceTick = currentTick;
                        }
                        _lastArmorThreshold = newArmorThreshold;
                    }
                }

                _lastHealthPercent = healthPercent;
                _lastArmorPercent = armorPercent;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HealthArmorManager.Update");
            }
        }

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
                    _audio.Speak("You are dead", true);
                    return;
                }

                int effectiveHealth = player.Health - 100;
                int effectiveMax = player.MaxHealth - 100;
                int healthPercent = effectiveMax > 0
                    ? Math.Max(0, Math.Min(100, (effectiveHealth * 100) / effectiveMax))
                    : 0;

                int armorPercent = Math.Max(0, Math.Min(100, player.Armor));

                _audio.Speak($"Health {healthPercent} percent, Armor {armorPercent} percent", true);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HealthArmorManager.AnnounceStatus");
            }
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
    }
}
