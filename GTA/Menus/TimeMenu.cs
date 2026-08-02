using System;
using GTA;
using GTA.Chrono;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Reads and sets the game clock. Time of day is something a sighted player
    /// reads off the sky and the lighting; here it has to be asked for. It also
    /// has real consequences - shops open and close, traffic thins out at night,
    /// and pedestrians behave differently - so being able to move it matters as
    /// much as being able to read it.
    /// </summary>
    public class TimeMenu : MenuBase
    {
        #region Types

        private class TimePreset
        {
            public string Name { get; }
            public int Hour { get; }

            public TimePreset(string name, int hour)
            {
                Name = name;
                Hour = hour;
            }
        }

        #endregion

        #region Constants

        private static readonly TimePreset[] Presets =
        {
            new TimePreset("Dawn, 5 AM", 5),
            new TimePreset("Sunrise, 7 AM", 7),
            new TimePreset("Morning, 9 AM", 9),
            new TimePreset("Midday, 12 noon", 12),
            new TimePreset("Afternoon, 3 PM", 15),
            new TimePreset("Evening, 6 PM", 18),
            new TimePreset("Sunset, 8 PM", 20),
            new TimePreset("Night, 10 PM", 22),
            new TimePreset("Midnight", 0),
            new TimePreset("Small hours, 3 AM", 3)
        };

        private const int ITEM_CURRENT = 0;
        private const int ITEM_SYNC_NOW = 1;
        private const int ITEM_KEEP_SYNCED = 2;
        private const int ITEM_PAUSE = 3;
        private const int ITEM_FORWARD_HOUR = 4;
        private const int ITEM_BACK_HOUR = 5;
        private const int ITEM_FORWARD_SIX = 6;
        private const int ITEM_NEXT_DAY = 7;
        private const int FIXED_ITEM_COUNT = 8;

        /// <summary>One real minute per game minute - a real-time clock.</summary>
        private const int REALTIME_MS_PER_GAME_MINUTE = 60000;

        #endregion

        #region Fields

        // Whether the game clock is being held to the system clock
        private bool _keepSynced;

        // The game's own clock rate, captured before we change it so it can be
        // put back exactly rather than guessed at
        private int _originalMsPerGameMinute;
        private bool _haveOriginalRate;

        private long _lastResyncTick;

        #endregion

        #region Construction

        public TimeMenu(AudioManager audio) : base(audio)
        {
        }

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount => FIXED_ITEM_COUNT + Presets.Length;

        protected override string GetItemText(int index)
        {
            switch (index)
            {
                case ITEM_CURRENT:
                    return $"Current time: {DescribeNow()}";
                case ITEM_SYNC_NOW:
                    return $"Set game time to my system clock, now {DescribeSystemClock()}";
                case ITEM_KEEP_SYNCED:
                    return _keepSynced
                        ? "Keeping game time matched to the system clock. Select to stop"
                        : "Keep game time matched to the system clock. Select to start";
                case ITEM_PAUSE:
                    return SafePaused() ? "Time is frozen. Select to let it run" : "Time is running. Select to freeze it";
                case ITEM_FORWARD_HOUR:
                    return "Forward one hour";
                case ITEM_BACK_HOUR:
                    return "Back one hour";
                case ITEM_FORWARD_SIX:
                    return "Forward six hours";
                case ITEM_NEXT_DAY:
                    return "Skip to tomorrow morning";
            }

            return Presets[index - FIXED_ITEM_COUNT].Name;
        }

        protected override void OnItemActivated(int index)
        {
            try
            {
                switch (index)
                {
                    case ITEM_CURRENT:
                        Speak(DescribeNow());
                        return;

                    case ITEM_SYNC_NOW:
                        SyncToSystemClock();
                        Speak($"Game time set to your system clock. {DescribeNow()}");
                        Logger.Info($"TIME|sync-once|{DescribeClock()}");
                        return;

                    case ITEM_KEEP_SYNCED:
                        ToggleKeepSynced();
                        return;

                    case ITEM_PAUSE:
                    {
                        bool freeze = !SafePaused();
                        GameClock.IsPaused = freeze;
                        Speak(freeze
                            ? $"Time frozen at {DescribeClock()}."
                            : $"Time running from {DescribeClock()}.");
                        return;
                    }

                    case ITEM_FORWARD_HOUR:
                        GameClock.AddToCurrentTime(1, 0, 0);
                        Speak($"Forward one hour. {DescribeNow()}");
                        return;

                    case ITEM_BACK_HOUR:
                        // No negative step, so go the long way round the clock
                        GameClock.AddToCurrentTime(23, 0, 0);
                        Speak($"Back one hour. {DescribeNow()}");
                        return;

                    case ITEM_FORWARD_SIX:
                        GameClock.AddToCurrentTime(6, 0, 0);
                        Speak($"Forward six hours. {DescribeNow()}");
                        return;

                    case ITEM_NEXT_DAY:
                        SetHour(8);
                        GameClock.Day = GameClock.Day + 1;
                        Speak($"Tomorrow morning. {DescribeNow()}");
                        return;
                }

                TimePreset preset = Presets[index - FIXED_ITEM_COUNT];
                SetHour(preset.Hour);
                Speak($"{preset.Name}. {DescribeNow()}");
                Logger.Info($"TIME|set={preset.Hour}:00");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TimeMenu.OnItemActivated");
                Speak("Failed to change the time.");
            }
        }

        public override string GetMenuName()
        {
            return "Time";
        }

        #endregion

        #region Helpers

        private static void SetHour(int hour)
        {
            GameClock.TimeOfDay = GameClockTime.FromHms(hour, 0, 0);
        }

        /// <summary>Put the game clock on the real clock, to the second.</summary>
        private static void SyncToSystemClock()
        {
            DateTime now = DateTime.Now;
            GameClock.TimeOfDay = GameClockTime.FromHms(now.Hour, now.Minute, now.Second);
        }

        /// <summary>
        /// Start or stop holding the game clock to the system clock.
        ///
        /// A one-off sync drifts apart almost immediately: by default a game
        /// minute passes in about two real seconds, so the game runs roughly
        /// thirty times too fast. Staying in step therefore means slowing the
        /// game's own clock to real time as well as setting it, which is what
        /// MillisecondsPerGameMinute does. The original rate is captured first so
        /// turning this off restores exactly what the game had, rather than a
        /// hard-coded value that might not be right for this build.
        /// </summary>
        private void ToggleKeepSynced()
        {
            try
            {
                if (_keepSynced)
                {
                    if (_haveOriginalRate)
                        GameClock.MillisecondsPerGameMinute = _originalMsPerGameMinute;

                    _keepSynced = false;
                    Speak("No longer matching the system clock. Game time runs at its normal speed again.");
                    Logger.Info($"TIME|keep-synced=off|restored={_originalMsPerGameMinute}ms");
                    return;
                }

                if (!_haveOriginalRate)
                {
                    _originalMsPerGameMinute = GameClock.MillisecondsPerGameMinute;
                    _haveOriginalRate = true;
                }

                // Real-time rate, then set the clock, so it is both correct now
                // and stays correct
                GameClock.MillisecondsPerGameMinute = REALTIME_MS_PER_GAME_MINUTE;
                SyncToSystemClock();

                // Freezing the clock would fight the sync
                if (GameClock.IsPaused)
                    GameClock.IsPaused = false;

                _keepSynced = true;
                _lastResyncTick = Game.GameTime;

                Speak($"Matching the system clock. Game time is now {DescribeClock()} " +
                      "and will run at real-world speed.");
                Logger.Info($"TIME|keep-synced=on|was={_originalMsPerGameMinute}ms|now={REALTIME_MS_PER_GAME_MINUTE}ms");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TimeMenu.ToggleKeepSynced");
                Speak("Failed to change the clock sync.");
            }
        }

        /// <summary>
        /// Called each tick. Nudges the game clock back onto the system clock
        /// when it has drifted - the rate change gets it close, but pauses,
        /// cutscenes and loading all cost time the game clock does not spend.
        /// </summary>
        public void Update(long currentTick)
        {
            if (!_keepSynced)
                return;

            if (currentTick - _lastResyncTick < Constants.CLOCK_RESYNC_INTERVAL)
                return;

            _lastResyncTick = currentTick;

            try
            {
                DateTime now = DateTime.Now;
                GameClockTime game = GameClock.TimeOfDay;

                int systemSeconds = (now.Hour * 3600) + (now.Minute * 60) + now.Second;
                int gameSeconds = (game.Hour * 3600) + (game.Minute * 60) + game.Second;

                int drift = Math.Abs(systemSeconds - gameSeconds);
                if (drift > 43200)          // wrapped past midnight - measure the short way
                    drift = 86400 - drift;

                if (drift < Constants.CLOCK_DRIFT_TOLERANCE)
                    return;

                SyncToSystemClock();
                Logger.Debug($"TIME|resync|drift={drift}s");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TimeMenu.Update");
            }
        }

        /// <summary>The real clock, worded the same way as the game clock.</summary>
        private static string DescribeSystemClock()
        {
            try
            {
                DateTime now = DateTime.Now;
                int hour12 = now.Hour % 12;
                if (hour12 == 0)
                    hour12 = 12;

                string minute = now.Minute < 10 ? $"0{now.Minute}" : now.Minute.ToString();
                return $"{hour12}:{minute} {(now.Hour < 12 ? "AM" : "PM")}";
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>Clock time plus the day, and whether the clock is stopped.</summary>
        private static string DescribeNow()
        {
            try
            {
                string text = DescribeClock();

                GameClockDate today = GameClock.Today;
                text += $", {today.DayOfWeek}";

                if (GameClock.IsPaused)
                    text += ", time frozen";

                return text;
            }
            catch
            {
                return "unavailable";
            }
        }

        /// <summary>
        /// Twelve-hour clock with minutes, which reads far better aloud than a
        /// bare 24-hour figure.
        /// </summary>
        private static string DescribeClock()
        {
            try
            {
                GameClockTime time = GameClock.TimeOfDay;
                int hour24 = time.Hour;
                int minute = time.Minute;

                string period = hour24 < 12 ? "AM" : "PM";
                int hour12 = hour24 % 12;
                if (hour12 == 0)
                    hour12 = 12;

                string minuteText = minute < 10 ? $"0{minute}" : minute.ToString();
                return $"{hour12}:{minuteText} {period}";
            }
            catch
            {
                return "unknown";
            }
        }

        private static bool SafePaused()
        {
            try { return GameClock.IsPaused; }
            catch { return false; }
        }

        #endregion
    }
}
