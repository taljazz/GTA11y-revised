using System;
using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Sets the weather, and reports the conditions a sighted player would just
    /// look up and see. Weather is not cosmetic here - rain changes how the roads
    /// behave under AutoDrive and fog changes what the scanners can pick out - so
    /// being able to both read and set it matters.
    ///
    /// Setting a type writes both the current and the next weather, otherwise the
    /// game's own weather cycle rolls it away again within a few minutes.
    /// </summary>
    public class WeatherMenu : MenuBase
    {
        #region Types

        private class WeatherChoice
        {
            public string Name { get; }
            public Weather Type { get; }

            public WeatherChoice(string name, Weather type)
            {
                Name = name;
                Type = type;
            }
        }

        #endregion

        #region Constants

        // Spoken names for every weather the game defines. Unknown is left out -
        // it is a read-back value, not something worth setting.
        private static readonly WeatherChoice[] Choices =
        {
            new WeatherChoice("Extra sunny", Weather.ExtraSunny),
            new WeatherChoice("Clear", Weather.Clear),
            new WeatherChoice("Cloudy", Weather.Clouds),
            new WeatherChoice("Smoggy", Weather.Smog),
            new WeatherChoice("Foggy", Weather.Foggy),
            new WeatherChoice("Overcast", Weather.Overcast),
            new WeatherChoice("Raining", Weather.Raining),
            new WeatherChoice("Thunderstorm", Weather.ThunderStorm),
            new WeatherChoice("Clearing after rain", Weather.Clearing),
            new WeatherChoice("Neutral", Weather.Neutral),
            new WeatherChoice("Snowing", Weather.Snowing),
            new WeatherChoice("Blizzard", Weather.Blizzard),
            new WeatherChoice("Light snow", Weather.Snowlight),
            new WeatherChoice("Christmas snow", Weather.Christmas),
            new WeatherChoice("Halloween", Weather.Halloween)
        };

        private const int ITEM_CONDITIONS = 0;
        private const int ITEM_RANDOM = 1;
        private const int ITEM_BLACKOUT = 2;
        private const int FIXED_ITEM_COUNT = 3;

        #endregion

        #region Construction

        public WeatherMenu(AudioManager audio) : base(audio)
        {
        }

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount => FIXED_ITEM_COUNT + Choices.Length;

        protected override string GetItemText(int index)
        {
            switch (index)
            {
                case ITEM_CONDITIONS:
                    return $"Current conditions: {DescribeCurrent()}";
                case ITEM_RANDOM:
                    return "Random weather";
                case ITEM_BLACKOUT:
                    return SafeBlackout() ? "City blackout: on" : "City blackout: off";
            }

            WeatherChoice choice = Choices[index - FIXED_ITEM_COUNT];
            bool current = SafeWeather() == choice.Type;
            return current ? $"{choice.Name}, current" : choice.Name;
        }

        protected override void OnItemActivated(int index)
        {
            try
            {
                switch (index)
                {
                    case ITEM_CONDITIONS:
                        Speak(DescribeCurrent());
                        return;

                    case ITEM_RANDOM:
                        World.SetRandomWeather();
                        Speak($"Random weather. Now {DescribeCurrent()}");
                        return;

                    case ITEM_BLACKOUT:
                    {
                        bool on = !SafeBlackout();
                        World.Blackout = on;
                        Speak(on ? "City blackout on. The power is out." : "City blackout off.");
                        return;
                    }
                }

                WeatherChoice choice = Choices[index - FIXED_ITEM_COUNT];

                // Both, or the weather cycle simply moves on from it
                World.Weather = choice.Type;
                World.NextWeather = choice.Type;

                Speak($"Weather set to {choice.Name}.");
                Logger.Info($"WEATHER|set={choice.Type}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeatherMenu.OnItemActivated");
                Speak("Failed to change the weather.");
            }
        }

        public override string GetMenuName()
        {
            return "Weather";
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Everything a glance out of the window would tell you: the weather
        /// type, how hard it is raining, and how strong the wind is.
        /// </summary>
        private static string DescribeCurrent()
        {
            try
            {
                Weather now = SafeWeather();
                string name = "unknown";
                foreach (WeatherChoice choice in Choices)
                {
                    if (choice.Type == now)
                    {
                        name = choice.Name.ToLowerInvariant();
                        break;
                    }
                }

                string detail = name;

                float rain = World.RainLevel;
                if (rain > 0.05f)
                    detail += $", rain {DescribeLevel(rain)}";

                float wind = World.WindSpeed;
                if (wind > 0.05f)
                    detail += $", wind {DescribeLevel(wind)}";

                if (World.Blackout)
                    detail += ", city blacked out";

                return detail;
            }
            catch
            {
                return "unavailable";
            }
        }

        /// <summary>Turn a 0-1 intensity into something worth hearing.</summary>
        private static string DescribeLevel(float level)
        {
            if (level < 0.25f) return "light";
            if (level < 0.6f) return "moderate";
            if (level < 0.85f) return "heavy";
            return "extreme";
        }

        private static Weather SafeWeather()
        {
            try { return World.Weather; }
            catch { return Weather.Unknown; }
        }

        private static bool SafeBlackout()
        {
            try { return World.Blackout; }
            catch { return false; }
        }

        #endregion
    }
}
