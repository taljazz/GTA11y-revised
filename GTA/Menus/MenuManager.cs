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
        #region Fields

        private readonly List<IMenuState> _menus;
        private readonly SettingsManager _settings;
        private readonly VehicleSaveManager _saveManager;
        private readonly AircraftLandingMenu _aircraftLandingMenu;
        private readonly WeaponSelectMenu _weaponSelectMenu;
        private readonly AutoDriveMenu _autoDriveMenu;
        private readonly AutoDriveManager _autoDriveManager;
        private readonly TurretCrewManager _turretCrewManager;
        private readonly PedestrianNavigationManager _pedNav;
        private readonly PlayerModelManager _playerModel;
        private readonly InteriorManager _interiors;
        private readonly TimeMenu _timeMenu;
        private int _currentMenuIndex;

        #endregion

        #region Construction

        public MenuManager(SettingsManager settings, AudioManager audio, HotkeyMapper hotkeys)
        {
            _settings = settings;
            _saveManager = new VehicleSaveManager();

            // Create AutoDrive manager and menu
            _autoDriveManager = new AutoDriveManager(audio, settings);
            _autoDriveMenu = new AutoDriveMenu(_autoDriveManager, audio);

            // Create PlayerModelManager (owns the online model swap, and must be
            // able to restore the original model on shutdown)
            _playerModel = new PlayerModelManager(audio);

            // Create InteriorManager (loads online interior map data on request)
            _interiors = new InteriorManager(audio, settings);

            // Create TurretCrewManager
            _turretCrewManager = new TurretCrewManager(settings, audio);

            // Create AircraftLandingMenu with audio beacon support
            _aircraftLandingMenu = new AircraftLandingMenu(settings, audio);

            // Create PedestrianNavigationManager for on-foot waypoint guidance
            _pedNav = new PedestrianNavigationManager(audio, settings);

            // Create TimeMenu (kept as a field so its clock sync can be ticked)
            _timeMenu = new TimeMenu(audio);

            // Create WeaponSelectMenu (kept as a field so the main script can
            // suppress its duplicate weapon-change announcement)
            _weaponSelectMenu = new WeaponSelectMenu(audio);

            // Initialize menus in order:
            // 1. Location (teleport)
            // 2. GPS Waypoint (driving destinations)
            // 3. AutoDrive (autonomous driving)
            // 4. Aircraft Landing (flying destinations with voice navigation)
            // 5. Vehicle Spawn (by category)
            // 6. Vehicle Mods (when in vehicle)
            // 7. Weapons (add, equip, and discard weapons by category)
            // 8. Weapon Mods (attachments and tints for the equipped weapon)
            // 9. Vehicle Save/Load
            // 10. Functions (chaos)
            // 11. Online Interiors (load GTA Online interior map data)
            // 12. Weather
            // 13. Time
            // 14. Vehicle Guide (what each vehicle is, what each upgrade does)
            // 15. Settings
            // 16. Help
            // Every menu receives the shared AudioManager so speech goes through
            // the Tolk health/reconnect logic and Ctrl+NumPad5 repeat-last works.
            _menus = new List<IMenuState>
            {
                new LocationMenu(audio),
                new WaypointMenu(audio),
                _autoDriveMenu,
                _aircraftLandingMenu,
                new VehicleCategoryMenu(settings, audio),
                new VehicleModMenuProxy(settings, audio),
                _weaponSelectMenu,
                new WeaponModMenu(audio),
                new VehicleSaveLoadMenu(_saveManager, settings, audio),
                new FunctionsMenu(settings, _turretCrewManager, _playerModel, audio),
                new OnlineInteriorsMenu(_interiors, audio),
                new WeatherMenu(audio),
                _timeMenu,
                new VehicleGuideMenu(audio),
                new SettingsMenu(settings, audio),
                new HelpMenu(hotkeys, audio)
            };

            _currentMenuIndex = 0;
        }

        #endregion

        #region Menu Navigation

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

        #endregion

        #region Subsystem Pass-Through

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

        /// <summary>
        /// Put the player's own model back. Safe to call at any time - it does
        /// nothing unless a swapped model is actually on right now. Called on
        /// death, because the game's respawn sequence hangs if it runs while the
        /// player is an NPC model.
        /// </summary>
        public void RestorePlayerModel(bool announce)
        {
            _playerModel?.Restore(announce);
        }

        /// <summary>
        /// What the player is currently wearing, or null for their own character.
        /// Verified against the live ped, not a remembered flag.
        /// </summary>
        public string GetPlayerModelStatus()
        {
            return _playerModel?.GetStatusText();
        }

        /// <summary>
        /// Let the interior manager report what actually loaded. Called every
        /// tick; it only speaks once, shortly after a load or unload request.
        /// </summary>
        public void UpdateInteriors(long currentTick)
        {
            _interiors.Update(currentTick);
        }

        /// <summary>
        /// Keep the game clock on the system clock when that is switched on.
        /// Called every tick; it only acts when synced and only when drifted.
        /// </summary>
        public void UpdateClockSync(long currentTick)
        {
            _timeMenu.Update(currentTick);
        }

        /// <summary>
        /// Whether the automatic weapon-change announcement should stay quiet
        /// because the weapon menu just spoke its own confirmation for this swap.
        /// One-shot - calling this consumes the suppression.
        /// </summary>
        public bool ConsumeWeaponChangeSuppression(long currentTick)
        {
            return _weaponSelectMenu.ConsumeWeaponChangeSuppression(currentTick);
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

                if (_aircraftLandingMenu != null &&
                    (_aircraftLandingMenu.IsNavigationActive || _aircraftLandingMenu.IsAutopilotActive))
                    _aircraftLandingMenu.CancelNavigation();

                if (_pedNav != null && _pedNav.IsActive)
                    _pedNav.StopNavigation();

                // Put the player's own model back before the script goes away.
                // A reload while wearing an online model would otherwise strand
                // them as an NPC with no menu left to change back.
                if (_playerModel != null && _playerModel.IsSwapped)
                    _playerModel.Restore(false);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "MenuManager.Dispose");
            }
        }

        #endregion
    }
}
