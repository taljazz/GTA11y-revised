using System;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages priority-based announcement throttling for AutoDrive.
    /// Prevents announcement spam by enforcing cooldowns based on priority levels.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class AnnouncementQueue
    {
        private readonly AudioManager _audio;
        private readonly SettingsManager _settings;

        // Per-priority cooldown tracking
        private long _lastCriticalAnnounceTick;
        private long _lastHighAnnounceTick;
        private long _lastMediumAnnounceTick;
        private long _lastLowAnnounceTick;

        // Global cooldown tracking
        private long _lastAnyAnnounceTick;

        /// <summary>
        /// Create a new announcement queue.
        /// </summary>
        /// <param name="audio">Audio manager for speech output</param>
        /// <param name="settings">Settings manager for announcement toggles (optional)</param>
        public AnnouncementQueue(AudioManager audio, SettingsManager settings = null)
        {
            _audio = audio;
            _settings = settings;
        }

        /// <summary>
        /// Try to announce a message with priority-based throttling.
        /// </summary>
        /// <param name="message">Message to announce</param>
        /// <param name="priority">Priority level (0=Critical, 1=High, 2=Medium, 3=Low)</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="settingName">Optional setting name to check before announcing (e.g. "announceNavigation")</param>
        /// <returns>True if the message was announced, false if throttled or setting disabled</returns>
        public bool TryAnnounce(string message, int priority, long currentTick, string settingName = null)
        {
            // Defensive: Validate message
            if (string.IsNullOrEmpty(message))
                return false;

            // Check if announcement is disabled by setting
            if (!string.IsNullOrEmpty(settingName) && _settings != null)
            {
                if (!_settings.GetSetting(settingName))
                {
                    if (Logger.IsDebugEnabled) Logger.Debug($"Announcement suppressed by setting '{settingName}': {message}");
                    return false;
                }
            }

            // Defensive: Validate and clamp priority to valid range
            if (priority < Constants.ANNOUNCE_PRIORITY_CRITICAL)
                priority = Constants.ANNOUNCE_PRIORITY_CRITICAL;
            else if (priority > Constants.ANNOUNCE_PRIORITY_LOW)
                priority = Constants.ANNOUNCE_PRIORITY_LOW;

            // Defensive: Guard against invalid tick values
            if (currentTick < 0)
                return false;

            // Defensive: Check audio manager is valid
            if (_audio == null)
                return false;

            // Check if we can announce at this priority level
            if (!CanAnnounce(priority, currentTick))
                return false;

            // Update cooldown tracking
            UpdateCooldown(priority, currentTick);

            // Actually announce
            try
            {
                _audio.Speak(message);
                if (Logger.IsDebugEnabled) Logger.Debug($"Announced (P{priority}): {message}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AnnouncementQueue.TryAnnounce");
                return false;
            }
        }

        /// <summary>
        /// Check if an announcement at the given priority can be made.
        /// </summary>
        /// <param name="priority">Priority level</param>
        /// <param name="currentTick">Current game tick</param>
        /// <returns>True if announcement can be made</returns>
        public bool CanAnnounce(int priority, long currentTick)
        {
            // Critical (0) - nearly always allowed, only 0.5 second cooldown
            // High (1) - 2 second cooldown
            // Medium (2) - 3 second cooldown
            // Low (3) - 5 second cooldown

            long cooldown = GetCooldownForPriority(priority);
            long lastAnnounce = GetLastAnnounceForPriority(priority);

            // Check priority-specific cooldown
            if (currentTick - lastAnnounce < cooldown)
                return false;

            // Higher priority can interrupt lower priority, but not the same level
            // This ensures critical messages get through even if low priority just spoke
            if (priority > Constants.ANNOUNCE_PRIORITY_CRITICAL)
            {
                // Non-critical messages also need global cooldown (except critical)
                if (currentTick - _lastAnyAnnounceTick < Constants.ANNOUNCE_GLOBAL_COOLDOWN)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get cooldown duration for a priority level.
        /// </summary>
        private long GetCooldownForPriority(int priority)
        {
            switch (priority)
            {
                case Constants.ANNOUNCE_PRIORITY_CRITICAL:
                    return Constants.ANNOUNCE_COOLDOWN_CRITICAL;
                case Constants.ANNOUNCE_PRIORITY_HIGH:
                    return Constants.ANNOUNCE_COOLDOWN_HIGH;
                case Constants.ANNOUNCE_PRIORITY_MEDIUM:
                    return Constants.ANNOUNCE_COOLDOWN_MEDIUM;
                case Constants.ANNOUNCE_PRIORITY_LOW:
                default:
                    return Constants.ANNOUNCE_COOLDOWN_LOW;
            }
        }

        /// <summary>
        /// Get last announcement time for a priority level.
        /// </summary>
        private long GetLastAnnounceForPriority(int priority)
        {
            switch (priority)
            {
                case Constants.ANNOUNCE_PRIORITY_CRITICAL:
                    return _lastCriticalAnnounceTick;
                case Constants.ANNOUNCE_PRIORITY_HIGH:
                    return _lastHighAnnounceTick;
                case Constants.ANNOUNCE_PRIORITY_MEDIUM:
                    return _lastMediumAnnounceTick;
                case Constants.ANNOUNCE_PRIORITY_LOW:
                default:
                    return _lastLowAnnounceTick;
            }
        }

        /// <summary>
        /// Update cooldown tracking for a priority level.
        /// </summary>
        private void UpdateCooldown(int priority, long currentTick)
        {
            _lastAnyAnnounceTick = currentTick;

            switch (priority)
            {
                case Constants.ANNOUNCE_PRIORITY_CRITICAL:
                    _lastCriticalAnnounceTick = currentTick;
                    break;
                case Constants.ANNOUNCE_PRIORITY_HIGH:
                    _lastHighAnnounceTick = currentTick;
                    break;
                case Constants.ANNOUNCE_PRIORITY_MEDIUM:
                    _lastMediumAnnounceTick = currentTick;
                    break;
                case Constants.ANNOUNCE_PRIORITY_LOW:
                default:
                    _lastLowAnnounceTick = currentTick;
                    break;
            }
        }

        /// <summary>
        /// Force immediate announcement without throttling (for critical system messages).
        /// </summary>
        /// <param name="message">Message to announce</param>
        public void AnnounceImmediate(string message)
        {
            // Defensive: Validate message
            if (string.IsNullOrEmpty(message))
                return;

            // Defensive: Check audio manager is valid
            if (_audio == null)
                return;

            try
            {
                _audio.Speak(message);
                if (Logger.IsDebugEnabled) Logger.Debug($"Announced (immediate): {message}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AnnouncementQueue.AnnounceImmediate");
            }
        }

        /// <summary>
        /// Reset all cooldown tracking.
        /// </summary>
        public void Reset()
        {
            _lastCriticalAnnounceTick = 0;
            _lastHighAnnounceTick = 0;
            _lastMediumAnnounceTick = 0;
            _lastLowAnnounceTick = 0;
            _lastAnyAnnounceTick = 0;
        }
    }
}
