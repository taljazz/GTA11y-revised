using System;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Detects and announces game state transitions (cutscenes, phone, loading, pause)
    /// via TTS so the player understands what the game is doing.
    /// </summary>
    public class GameStateManager
    {
        // Phone detection: use GET_IS_TASK_ACTIVE with CTaskMobilePhone (task index 500)
        private const int TASK_MOBILE_PHONE = 500;

        private readonly AudioManager _audio;

        // Previous state tracking for transition detection
        private bool _wasCutsceneActive;
        private bool _wasPhoneActive;
        private bool _wasLoading;
        private bool _wasPaused;

        public GameStateManager(AudioManager audio)
        {
            _audio = audio;

            // Initialize to current state to avoid false announcements on startup
            _wasCutsceneActive = false;
            _wasPhoneActive = false;
            _wasLoading = false;
            _wasPaused = false;
        }

        /// <summary>
        /// Whether a cutscene is currently playing
        /// </summary>
        public bool IsCutsceneActive => _wasCutsceneActive;

        /// <summary>
        /// Whether the phone is currently active
        /// </summary>
        public bool IsPhoneActive => _wasPhoneActive;

        /// <summary>
        /// Whether the game is currently loading
        /// </summary>
        public bool IsLoading => _wasLoading;

        /// <summary>
        /// Whether the game is paused
        /// </summary>
        public bool IsPaused => _wasPaused;

        /// <summary>
        /// Check for state transitions and announce changes.
        /// Called from OnTick.
        /// </summary>
        // Throttle: state checks every 500ms (no need to check every frame)
        private long _lastUpdateTick;
        private const long UPDATE_INTERVAL = 5_000_000; // 500ms

        public void Update(long currentTick)
        {
            if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                return;
            _lastUpdateTick = currentTick;

            try
            {
                CheckCutscene();
                CheckPhone();
                CheckLoading();
                CheckPause();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "GameStateManager.Update");
            }
        }

        private void CheckCutscene()
        {
            bool active = Game.IsCutsceneActive;

            if (active != _wasCutsceneActive)
            {
                _audio.Speak(active ? "Cutscene started." : "Cutscene ended.");
                _wasCutsceneActive = active;
            }
        }

        private void CheckPhone()
        {
            // Check if the player ped has an active mobile phone task
            bool active = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, Game.Player.Character, TASK_MOBILE_PHONE);

            if (active != _wasPhoneActive)
            {
                _audio.Speak(active ? "Phone active." : "Phone closed.");
                _wasPhoneActive = active;
            }
        }

        private void CheckLoading()
        {
            bool loading = Game.IsLoading;

            if (loading != _wasLoading)
            {
                _audio.Speak(loading ? "Loading." : "Game ready.");
                _wasLoading = loading;
            }
        }

        private void CheckPause()
        {
            bool paused = Game.IsPaused;

            if (paused != _wasPaused)
            {
                _audio.Speak(paused ? "Game paused." : "Game resumed.");
                _wasPaused = paused;
            }
        }
    }
}
