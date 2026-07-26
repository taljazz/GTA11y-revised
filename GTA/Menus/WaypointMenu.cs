using GTA.Native;
using GrandTheftAccessibility.Data;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for setting GPS waypoints to predefined driving destinations.
    /// Uses LocationDataLoader to load from JSON or fallback to hardcoded defaults.
    /// </summary>
    public class WaypointMenu : MenuBase
    {
        #region Fields

        private WaypointDestination[] _destinations;

        // PERFORMANCE: Pre-cached Hash for native calls
        private static readonly Hash _setNewWaypointHash = Hash.SET_NEW_WAYPOINT;

        #endregion

        #region Construction

        public WaypointMenu(AudioManager audio) : base(audio)
        {
            // Pre-load waypoint destinations at construction
            _destinations = LocationDataLoader.LoadWaypointDestinations();
        }

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount => _destinations?.Length ?? 0;

        protected override int FastScrollStep => 10;

        protected override string EmptyMenuText => "No destinations available";

        protected override string GetItemText(int index)
        {
            var dest = _destinations[index];
            return $"{index + 1} of {_destinations.Length}: {dest.Name}";
        }

        protected override void OnItemActivated(int index)
        {
            var dest = _destinations[index];

            // Set GPS waypoint on the map (uses X, Y coordinates only)
            Function.Call(_setNewWaypointHash, dest.Coords.X, dest.Coords.Y);

            // Play confirmation sound (get ID, play, release to avoid sound ID leak)
            int soundId = Function.Call<int>(Hash.GET_SOUND_ID);
            Function.Call(Hash.PLAY_SOUND_FRONTEND, soundId, "WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
            Function.Call(Hash.RELEASE_SOUND_ID, soundId);

            Speak($"Waypoint set to {dest.Name}");
            Logger.Info($"Set waypoint to {dest.Name}");
        }

        public override string GetMenuName()
        {
            return "Set GPS Waypoint";
        }

        #endregion

        #region Public API

        /// <summary>
        /// Reload destinations from JSON (useful for hot-reload)
        /// </summary>
        public void ReloadDestinations()
        {
            LocationDataLoader.ReloadLocations();
            _destinations = LocationDataLoader.LoadWaypointDestinations();
            if (SelectedIndex >= _destinations.Length)
                ResetSelection();
        }

        #endregion
    }
}
