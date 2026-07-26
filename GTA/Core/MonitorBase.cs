using System;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Abstract base class for the periodic monitoring managers (health, vehicle
    /// damage, combat, game state, blips, turret crew). Centralizes the pattern
    /// they all shared: hold AudioManager/SettingsManager references, throttle
    /// updates to an interval, honor an enable/disable setting, and wrap the work
    /// in exception logging.
    ///
    /// Two layers:
    ///   MonitorBase           - throttle + enabled-check plumbing (for monitors
    ///                           whose Update takes only the current tick)
    ///   MonitorBase&lt;TSubject&gt; - adds a template-method Update(subject, tick)
    ///                           with subject validation (Ped, Vehicle, ...)
    /// </summary>
    public abstract class MonitorBase
    {
        #region Fields

        private long _lastUpdateTick;

        #endregion

        #region Construction

        protected MonitorBase(AudioManager audio, SettingsManager settings)
        {
            Audio = audio;
            Settings = settings;
        }

        #endregion

        #region Protected Surface

        /// <summary>Shared audio manager for speech output.</summary>
        protected AudioManager Audio { get; }

        /// <summary>Shared settings manager. May be null for monitors without settings.</summary>
        protected SettingsManager Settings { get; }

        /// <summary>Minimum interval between updates, in game-time milliseconds (Game.GameTime).</summary>
        protected abstract long UpdateIntervalMs { get; }

        /// <summary>
        /// Boolean setting id that enables this monitor, or null if it is always on.
        /// </summary>
        protected virtual string EnabledSettingKey => null;

        /// <summary>Whether this monitor is currently enabled by its setting.</summary>
        public bool IsEnabled =>
            EnabledSettingKey == null || Settings == null || Settings.GetSetting(EnabledSettingKey);

        /// <summary>
        /// Returns true when enough time has passed since the last update AND the
        /// monitor is enabled. Consumes the throttle window when the interval has
        /// elapsed, so call it exactly once per tick.
        /// </summary>
        protected bool TryBeginUpdate(long currentTick)
        {
            if (currentTick - _lastUpdateTick < UpdateIntervalMs)
                return false;

            _lastUpdateTick = currentTick;
            return IsEnabled;
        }

        #endregion
    }

    /// <summary>
    /// Generic monitor base for managers that observe a single game entity each
    /// tick (the player Ped, the current Vehicle, ...). The public Update is a
    /// template method: it validates the subject, applies throttling and the
    /// enabled setting, then calls the subclass's OnUpdate inside a try/catch.
    /// </summary>
    /// <typeparam name="TSubject">The entity type this monitor observes.</typeparam>
    public abstract class MonitorBase<TSubject> : MonitorBase where TSubject : class
    {
        #region Construction

        protected MonitorBase(AudioManager audio, SettingsManager settings)
            : base(audio, settings)
        {
        }

        #endregion

        #region Template Method

        /// <summary>
        /// Run one monitoring pass. Call from OnTick every frame; the base class
        /// handles throttling, validation, and exception logging.
        /// </summary>
        public void Update(TSubject subject, long currentTick)
        {
            if (!ValidateSubject(subject))
                return;

            if (!TryBeginUpdate(currentTick))
                return;

            try
            {
                OnUpdate(subject, currentTick);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, GetType().Name + ".Update");
            }
        }

        /// <summary>
        /// Check that the subject is safe to observe this tick.
        /// Override to add entity-specific checks (Exists, IsDead, ...).
        /// </summary>
        protected virtual bool ValidateSubject(TSubject subject)
        {
            return subject != null;
        }

        /// <summary>The subclass's actual monitoring work.</summary>
        protected abstract void OnUpdate(TSubject subject, long currentTick);

        #endregion
    }
}
