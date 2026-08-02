using System;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Loads GTA Online interior map data into story mode with REQUEST_IPL.
    ///
    /// The names below come from the IPL list the official native documentation
    /// links to, but a name that does not exist fails silently - REQUEST_IPL
    /// returns nothing and simply does not load. So nothing here is taken on
    /// trust: after requesting, every IPL is read back with IS_IPL_ACTIVE and the
    /// real count is announced and logged. "Apartments, 24 of 24 loaded" means it
    /// worked; "0 of 24" means the names are wrong, and we will know rather than
    /// wonder.
    ///
    /// Only IPL-based interiors are handled. The newer DLC interiors are driven
    /// by interior prop sets instead, and ENABLE_INTERIOR_PROP is absent from
    /// SHVDN's Hash enum - adding it would mean hand-typing a hash, which is
    /// exactly how the MP maps toggle came to be calling an unrelated HUD native.
    /// </summary>
    public class InteriorManager
    {
        #region Types

        private class InteriorGroup
        {
            public string Name { get; }
            public string[] Ipls { get; }

            public InteriorGroup(string name, string[] ipls)
            {
                Name = name;
                Ipls = ipls;
            }
        }

        #endregion

        #region Interior Data

        private static readonly InteriorGroup[] Groups =
        {
            new InteriorGroup("High-end apartments", new[]
            {
                "apa_v_mp_h_01_a", "apa_v_mp_h_01_b", "apa_v_mp_h_01_c",
                "apa_v_mp_h_02_a", "apa_v_mp_h_02_b", "apa_v_mp_h_02_c",
                "apa_v_mp_h_03_a", "apa_v_mp_h_03_b", "apa_v_mp_h_03_c",
                "apa_v_mp_h_04_a", "apa_v_mp_h_04_b", "apa_v_mp_h_04_c",
                "apa_v_mp_h_05_a", "apa_v_mp_h_05_b", "apa_v_mp_h_05_c",
                "apa_v_mp_h_06_a", "apa_v_mp_h_06_b", "apa_v_mp_h_06_c",
                "apa_v_mp_h_07_a", "apa_v_mp_h_07_b", "apa_v_mp_h_07_c",
                "apa_v_mp_h_08_a", "apa_v_mp_h_08_b", "apa_v_mp_h_08_c"
            })

            // Executive offices were here and are gone: verified in-game on
            // 2026-07-31 as 0 of 9 active, i.e. every ex_int_office_*_dlc name
            // from the published list is wrong for this build. The offices are
            // almost certainly driven by interior prop sets rather than IPLs.
            // Apartments from the same list came back 24 of 24, so the mechanism
            // is sound - it is those names specifically that do not exist.
            // Add more groups only once a run shows them actually activating.
        };

        #endregion

        #region Fields

        private readonly AudioManager _audio;
        private readonly SettingsManager _settings;

        // Deferred verification: an IPL is not necessarily active the instant it
        // is requested, so the read-back happens a moment later on the tick
        private int _pendingGroup = -1;
        private long _pendingTick;
        private bool _pendingWasEnabling;

        #endregion

        #region Construction

        public InteriorManager(AudioManager audio, SettingsManager settings)
        {
            _audio = audio;
            _settings = settings;
        }

        #endregion

        #region Public API

        public int GroupCount => Groups.Length;

        public string GetGroupName(int index)
        {
            return IsValidIndex(index) ? Groups[index].Name : "Unknown";
        }

        /// <summary>Spoken status line for the menu: name plus how many are live.</summary>
        public string GetGroupStatus(int index)
        {
            if (!IsValidIndex(index))
                return "Unknown";

            InteriorGroup group = Groups[index];
            int active = CountActive(group);

            if (active == 0)
                return $"{group.Name}: not loaded";
            if (active == group.Ipls.Length)
                return $"{group.Name}: loaded";

            return $"{group.Name}: partly loaded, {active} of {group.Ipls.Length}";
        }

        /// <summary>Load the group if it is off, unload it if it is on.</summary>
        public void Toggle(int index)
        {
            if (!IsValidIndex(index))
                return;

            InteriorGroup group = Groups[index];
            bool anyActive = CountActive(group) > 0;

            // The online map group has to be loaded for these to have anything to
            // attach to - say so rather than silently doing nothing useful
            if (!anyActive && _settings != null && !_settings.GetSetting("enableMPMaps"))
            {
                Speak($"Turn on Enable GTA Online Maps and Interiors in Settings first, " +
                      $"then load {group.Name}.");
                return;
            }

            try
            {
                foreach (string ipl in group.Ipls)
                {
                    if (anyActive)
                        Function.Call(Hash.REMOVE_IPL, ipl);
                    else
                        Function.Call(Hash.REQUEST_IPL, ipl);
                }

                _pendingGroup = index;
                _pendingTick = Game.GameTime;
                _pendingWasEnabling = !anyActive;

                Speak(anyActive ? $"Unloading {group.Name}." : $"Loading {group.Name}.");
                Logger.Info($"IPL|{(anyActive ? "remove" : "request")}|group={group.Name}|count={group.Ipls.Length}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "InteriorManager.Toggle");
                Speak($"Failed to change {group.Name}.");
            }
        }

        /// <summary>
        /// Called each tick. Reports what actually happened once the request has
        /// had a moment to take effect.
        /// </summary>
        public void Update(long currentTick)
        {
            if (_pendingGroup < 0)
                return;

            if (currentTick - _pendingTick < Constants.INTERIOR_VERIFY_DELAY)
                return;

            InteriorGroup group = Groups[_pendingGroup];
            int active = CountActive(group);
            int total = group.Ipls.Length;
            _pendingGroup = -1;

            if (_pendingWasEnabling)
            {
                if (active == total)
                    Speak($"{group.Name} loaded, all {total} sections.");
                else if (active > 0)
                    Speak($"{group.Name} partly loaded, {active} of {total} sections.");
                else
                    Speak($"{group.Name} did not load. The game may not have that content.");
            }
            else
            {
                Speak(active == 0
                    ? $"{group.Name} unloaded."
                    : $"{group.Name} partly unloaded, {active} of {total} still active.");
            }

            Logger.Info($"IPL|verify|group={group.Name}|active={active}/{total}" +
                        $"|enabling={(_pendingWasEnabling ? 1 : 0)}|{DescribeActive(group)}");
        }

        #endregion

        #region Helpers

        private static bool IsValidIndex(int index)
        {
            return index >= 0 && index < Groups.Length;
        }

        private static int CountActive(InteriorGroup group)
        {
            int active = 0;
            foreach (string ipl in group.Ipls)
            {
                if (IsActive(ipl))
                    active++;
            }
            return active;
        }

        private static bool IsActive(string ipl)
        {
            try { return Function.Call<bool>(Hash.IS_IPL_ACTIVE, ipl); }
            catch { return false; }
        }

        /// <summary>Per-IPL detail for the log, so a bad name is identifiable.</summary>
        private static string DescribeActive(InteriorGroup group)
        {
            var sb = new System.Text.StringBuilder();
            foreach (string ipl in group.Ipls)
                sb.Append($"{ipl}={(IsActive(ipl) ? 1 : 0)} ");
            return sb.ToString().Trim();
        }

        private void Speak(string message)
        {
            if (_audio != null && !string.IsNullOrEmpty(message))
                _audio.Speak(message, true);
        }

        #endregion
    }
}
