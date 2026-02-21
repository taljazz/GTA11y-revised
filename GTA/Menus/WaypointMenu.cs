using GTA;
using GTA.Native;
using DavyKager;
using GrandTheftAccessibility.Data;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for setting GPS waypoints to predefined driving destinations.
    /// Uses LocationDataLoader to load from JSON or fallback to hardcoded defaults.
    /// </summary>
    public class WaypointMenu : IMenuState
    {
        private int _currentIndex;
        private WaypointDestination[] _destinations;

        // PERFORMANCE: Pre-cached Hash for native calls
        private static readonly Hash _setNewWaypointHash = Hash.SET_NEW_WAYPOINT;

        public WaypointMenu()
        {
            _currentIndex = 0;

            // Pre-load waypoint destinations at construction
            _destinations = LocationDataLoader.LoadWaypointDestinations();
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            if (_destinations.Length == 0) return;

            int step = fastScroll ? 10 : 1;

            _currentIndex -= step;
            if (_currentIndex < 0)
                _currentIndex = ((_currentIndex % _destinations.Length) + _destinations.Length) % _destinations.Length;
        }

        public void NavigateNext(bool fastScroll = false)
        {
            if (_destinations.Length == 0) return;

            int step = fastScroll ? 10 : 1;

            _currentIndex += step;
            if (_currentIndex >= _destinations.Length)
                _currentIndex = _currentIndex % _destinations.Length;
        }

        public string GetCurrentItemText()
        {
            if (_destinations.Length == 0) return "No destinations available";

            int displayIndex = _currentIndex + 1;
            var dest = _destinations[_currentIndex];
            return $"{displayIndex} of {_destinations.Length}: {dest.Name}";
        }

        public void ExecuteSelection()
        {
            if (_destinations.Length == 0) return;

            var dest = _destinations[_currentIndex];

            // Set GPS waypoint on the map (uses X, Y coordinates only)
            Function.Call(_setNewWaypointHash, dest.Coords.X, dest.Coords.Y);

            // Play confirmation sound (get ID, play, release to avoid sound ID leak)
            int soundId = Function.Call<int>(Hash.GET_SOUND_ID);
            Function.Call(Hash.PLAY_SOUND_FRONTEND, soundId, "WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
            Function.Call(Hash.RELEASE_SOUND_ID, soundId);

            Tolk.Speak($"Waypoint set to {dest.Name}");
            Logger.Info($"Set waypoint to {dest.Name}");
        }

        public string GetMenuName()
        {
            return "Set GPS Waypoint";
        }

        public bool HasActiveSubmenu => false;

        public void ExitSubmenu()
        {
            // No submenu - do nothing
        }

        /// <summary>
        /// Reload destinations from JSON (useful for hot-reload)
        /// </summary>
        public void ReloadDestinations()
        {
            LocationDataLoader.ReloadLocations();
            _destinations = LocationDataLoader.LoadWaypointDestinations();
            if (_currentIndex >= _destinations.Length)
                _currentIndex = 0;
        }
    }
}
