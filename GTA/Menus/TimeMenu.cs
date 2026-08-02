using System;
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
        private const int ITEM_PAUSE = 1;
        private const int ITEM_FORWARD_HOUR = 2;
        private const int ITEM_BACK_HOUR = 3;
        private const int ITEM_FORWARD_SIX = 4;
        private const int ITEM_NEXT_DAY = 5;
        private const int FIXED_ITEM_COUNT = 6;

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
