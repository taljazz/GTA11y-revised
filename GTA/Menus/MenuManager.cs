using System;
using System.Collections.Generic;
using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Manages the main menu hierarchy and state transitions
    /// Supports hierarchical submenus with back navigation
    /// </summary>
    public class MenuManager : IDisposable
    {
        private readonly List<IMenuState> _menus;
        private readonly SettingsManager _settings;
        private readonly VehicleSaveManager _saveManager;
        private readonly AircraftLandingMenu _aircraftLandingMenu;
        private readonly AutoDriveMenu _autoDriveMenu;
        private readonly AutoDriveManager _autoDriveManager;
        private readonly TurretCrewManager _turretCrewManager;
        private readonly PedestrianNavigationManager _pedNav;
        private int _currentMenuIndex;

        public MenuManager(SettingsManager settings, AudioManager audio)
        {
            _settings = settings;
            _saveManager = new VehicleSaveManager();

            // Create AutoDrive manager and menu
            _autoDriveManager = new AutoDriveManager(audio, settings);
            _autoDriveMenu = new AutoDriveMenu(_autoDriveManager);

            // Create TurretCrewManager
            _turretCrewManager = new TurretCrewManager(settings, audio);

            // Create AircraftLandingMenu with audio beacon support
            _aircraftLandingMenu = new AircraftLandingMenu(settings, audio);

            // Create PedestrianNavigationManager for on-foot waypoint guidance
            _pedNav = new PedestrianNavigationManager(audio, settings);

            // Initialize menus in order:
            // 1. Location (teleport)
            // 2. GPS Waypoint (driving destinations)
            // 3. AutoDrive (autonomous driving)
            // 4. Aircraft Landing (flying destinations with voice navigation)
            // 5. Vehicle Spawn (by category)
            // 6. Vehicle Mods (when in vehicle)
            // 7. Vehicle Save/Load
            // 8. Functions (chaos)
            // 9. Settings
            _menus = new List<IMenuState>
            {
                new LocationMenu(),
                new WaypointMenu(),
                _autoDriveMenu,
                _aircraftLandingMenu,
                new VehicleCategoryMenu(settings),
                new VehicleModMenuProxy(settings),
                new VehicleSaveLoadMenu(_saveManager, settings),
                new FunctionsMenu(settings, _turretCrewManager),
                new SettingsMenu(settings),
                new HelpMenu()
            };

            _currentMenuIndex = 0;
        }

        /// <summary>
        /// Navigate to previous main menu
        /// </summary>
        public void NavigatePreviousMenu()
        {
            if (_currentMenuIndex > 0)
                _currentMenuIndex--;
            else
                _currentMenuIndex = _menus.Count - 1;
        }

        /// <summary>
        /// Navigate to next main menu
        /// </summary>
        public void NavigateNextMenu()
        {
            if (_currentMenuIndex < _menus.Count - 1)
                _currentMenuIndex++;
            else
                _currentMenuIndex = 0;
        }

        /// <summary>
        /// Navigate to previous item in current submenu
        /// </summary>
        public void NavigatePreviousItem(bool fastScroll = false)
        {
            _menus[_currentMenuIndex].NavigatePrevious(fastScroll);
        }

        /// <summary>
        /// Navigate to next item in current submenu
        /// </summary>
        public void NavigateNextItem(bool fastScroll = false)
        {
            _menus[_currentMenuIndex].NavigateNext(fastScroll);
        }

        /// <summary>
        /// Get current menu description for speech
        /// </summary>
        public string GetCurrentMenuDescription()
        {
            IMenuState currentMenu = _menus[_currentMenuIndex];
            return $"{currentMenu.GetMenuName()}. {currentMenu.GetCurrentItemText()}";
        }

        /// <summary>
        /// Execute selection in current menu
        /// </summary>
        public void ExecuteSelection()
        {
            _menus[_currentMenuIndex].ExecuteSelection();
        }

        /// <summary>
        /// Get current submenu item text
        /// </summary>
        public string GetCurrentItemText()
        {
            return _menus[_currentMenuIndex].GetCurrentItemText();
        }

        /// <summary>
        /// Check if current menu has an active submenu
        /// </summary>
        public bool HasActiveSubmenu()
        {
            return _menus[_currentMenuIndex].HasActiveSubmenu;
        }

        /// <summary>
        /// Exit current submenu (back navigation)
        /// Returns true if a submenu was exited, false if already at top level
        /// </summary>
        public bool ExitSubmenu()
        {
            if (_menus[_currentMenuIndex].HasActiveSubmenu)
            {
                _menus[_currentMenuIndex].ExitSubmenu();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Update aircraft landing navigation (called from OnTick when in aircraft)
        /// </summary>
        public void UpdateAircraftNavigation(Vehicle aircraft, GTA.Math.Vector3 position, long currentTick)
        {
            _aircraftLandingMenu.UpdateNavigation(aircraft, position, currentTick);
        }

        /// <summary>
        /// Check if aircraft navigation is currently active
        /// </summary>
        public bool IsAircraftNavigationActive => _aircraftLandingMenu.IsNavigationActive;

        /// <summary>
        /// Cancel active aircraft navigation
        /// </summary>
        public void CancelAircraftNavigation()
        {
            _aircraftLandingMenu.CancelNavigation();
        }

        /// <summary>
        /// Update aircraft landing beacon audio (called from OnTick when in aircraft)
        /// </summary>
        public void UpdateAircraftBeacon(Vehicle aircraft, GTA.Math.Vector3 position, long currentTick)
        {
            _aircraftLandingMenu.UpdateBeacon(aircraft, position, currentTick);
        }

        /// <summary>
        /// Update AutoDrive navigation (called from OnTick when in vehicle)
        /// </summary>
        public void UpdateAutoDrive(Vehicle vehicle, GTA.Math.Vector3 position, long currentTick)
        {
            _autoDriveManager.Update(vehicle, position, currentTick);
        }

        /// <summary>
        /// Check and announce road features (curves, intersections, etc.)
        /// </summary>
        public void CheckRoadFeatures(Vehicle vehicle, GTA.Math.Vector3 position, long currentTick)
        {
            _autoDriveManager.CheckRoadFeatures(vehicle, position, currentTick);
        }

        /// <summary>
        /// Check if AutoDrive is currently active
        /// </summary>
        public bool IsAutoDriveActive => _autoDriveManager.IsActive;

        /// <summary>
        /// Stop AutoDrive if active
        /// </summary>
        public void StopAutoDrive()
        {
            if (_autoDriveManager.IsActive)
            {
                _autoDriveManager.Stop();
            }
        }

        /// <summary>
        /// Check for road type changes and announce if enabled
        /// Called from OnTick during AutoDrive
        /// </summary>
        public void CheckRoadTypeChange(GTA.Math.Vector3 position, long currentTick, bool announceEnabled)
        {
            _autoDriveManager.CheckRoadTypeChange(position, currentTick, announceEnabled);
        }

        /// <summary>
        /// Update road seeking - rescan and navigate if drifted off
        /// Called from OnTick during AutoDrive
        /// </summary>
        public void UpdateRoadSeeking(Vehicle vehicle, GTA.Math.Vector3 position, long currentTick)
        {
            _autoDriveManager.UpdateRoadSeeking(vehicle, position, currentTick);
        }

        /// <summary>
        /// Check if road seeking is currently active
        /// </summary>
        public bool IsRoadSeekingActive => _autoDriveManager.IsSeeking;

        /// <summary>
        /// Update turret crew behavior (called from OnTick when in weaponized vehicle)
        /// </summary>
        public void UpdateTurretCrew(long currentTick)
        {
            _turretCrewManager.Update(currentTick);
        }

        /// <summary>
        /// Check if turret crew is currently spawned
        /// </summary>
        public bool IsTurretCrewActive => _turretCrewManager.IsSpawned;

        /// <summary>
        /// Destroy turret crew if active
        /// </summary>
        public void DestroyTurretCrew()
        {
            if (_turretCrewManager.IsSpawned)
            {
                _turretCrewManager.DestroyTurretCrew();
            }
        }

        // Pedestrian Navigation pass-through
        public bool IsPedestrianNavigationActive => _pedNav.IsActive;

        public void StartPedestrianNavigation()
        {
            _pedNav.StartNavigation();
        }

        public void StopPedestrianNavigation()
        {
            _pedNav.StopNavigation();
        }

        public void UpdatePedestrianNavigation(Ped player, GTA.Math.Vector3 position, long currentTick)
        {
            _pedNav.Update(player, position, currentTick);
        }

        /// <summary>
        /// Cleanup resources on script unload to prevent leaks across script reloads
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_autoDriveManager != null && _autoDriveManager.IsActive)
                    _autoDriveManager.Stop();

                if (_turretCrewManager != null && _turretCrewManager.IsSpawned)
                    _turretCrewManager.DestroyTurretCrew();

                if (_aircraftLandingMenu != null && _aircraftLandingMenu.IsNavigationActive)
                    _aircraftLandingMenu.CancelNavigation();

                if (_pedNav != null && _pedNav.IsActive)
                    _pedNav.StopNavigation();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "MenuManager.Dispose");
            }
        }

    }
}
