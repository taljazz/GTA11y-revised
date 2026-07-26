using System;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Detects and announces game state transitions (cutscenes, phone, loading, pause)
    /// via TTS so the player understands what the game is doing.
    /// Derives from MonitorBase for the shared throttle plumbing; it has no
    /// subject entity, so it exposes its own Update(long).
    /// </summary>
    public class GameStateManager : MonitorBase
    {
        #region Constants

        // Phone detection: use GET_IS_TASK_ACTIVE with CTaskMobilePhone (task index 500)
        private const int TASK_MOBILE_PHONE = 500;

        // State checks every 500ms (no need to check every frame)
        private const long UPDATE_INTERVAL = 500;

        #endregion

        #region Fields

        // Previous state tracking for transition detection
        private bool _wasCutsceneActive;
        private bool _wasPhoneActive;
        private bool _wasPaused;

        #endregion

        #region Construction

        public GameStateManager(AudioManager audio) : base(audio, null)
        {
            // Initialize to current state to avoid false announcements on startup
            _wasCutsceneActive = false;
            _wasPhoneActive = false;
            _wasPaused = false;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Whether a cutscene is currently playing
        /// </summary>
        public bool IsCutsceneActive => _wasCutsceneActive;

        /// <summary>
        /// Whether the phone is currently active
        /// </summary>
        public bool IsPhoneActive => _wasPhoneActive;

        /// <summary>
        /// Whether the game is paused
        /// </summary>
        public bool IsPaused => _wasPaused;

        #endregion

        #region Update Loop

        protected override long UpdateIntervalMs => UPDATE_INTERVAL;

        /// <summary>
        /// Check for state transitions and announce changes.
        /// Called from OnTick.
        /// </summary>
        public void Update(long currentTick)
        {
            if (!TryBeginUpdate(currentTick))
                return;

            try
            {
                CheckCutscene();
                CheckPhone();
                CheckPause();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "GameStateManager.Update");
            }
        }

        #endregion

        #region State Checks

        private void CheckCutscene()
        {
            bool active = Game.IsCutsceneActive;

            if (active != _wasCutsceneActive)
            {
                Audio.Speak(active ? "Cutscene started." : "Cutscene ended.");
                _wasCutsceneActive = active;
            }
        }

        private void CheckPhone()
        {
            // Check if the player ped has an active mobile phone task
            bool active = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, Game.Player.Character, TASK_MOBILE_PHONE);

            if (active != _wasPhoneActive)
            {
                Audio.Speak(active ? "Phone active." : "Phone closed.");
                _wasPhoneActive = active;
            }
        }

        // Loading detection removed: since ScriptHookV 1.0.3351.0, scripts can no
        // longer start before the loading screen finishes, so Game.IsLoading is
        // deprecated and always false while scripts run.

        private void CheckPause()
        {
            bool paused = Game.IsPaused;

            if (paused != _wasPaused)
            {
                Audio.Speak(paused ? "Game paused." : "Game resumed.");
                _wasPaused = paused;
            }
        }

        #endregion
    }
}
