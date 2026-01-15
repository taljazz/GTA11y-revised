using System;
using System.Collections.Generic;
using GTA;
using DavyKager;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for controlling AutoFly functionality (aircraft autopilot).
    /// Provides cruise mode, waypoint navigation, altitude/speed controls, and status.
    /// </summary>
    public class AutoFlyMenu : IMenuState
    {
        private readonly AutoFlyManager _manager;
        private int _currentIndex;

        // Menu items
        private const int ITEM_CRUISE = 0;
        private const int ITEM_WAYPOINT = 1;
        private const int ITEM_LAND_HERE = 2;
        private const int ITEM_INCREASE_ALT = 3;
        private const int ITEM_DECREASE_ALT = 4;
        private const int ITEM_INCREASE_SPEED = 5;
        private const int ITEM_DECREASE_SPEED = 6;
        private const int ITEM_PAUSE_RESUME = 7;
        private const int ITEM_STATUS = 8;
        private const int ITEM_STOP = 9;

        private const int MENU_ITEM_COUNT = 10;

        public AutoFlyMenu(AutoFlyManager manager)
        {
            _manager = manager;
            _currentIndex = 0;
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            if (_currentIndex > 0)
                _currentIndex--;
            else
                _currentIndex = MENU_ITEM_COUNT - 1;
        }

        public void NavigateNext(bool fastScroll = false)
        {
            if (_currentIndex < MENU_ITEM_COUNT - 1)
                _currentIndex++;
            else
                _currentIndex = 0;
        }

        public string GetCurrentItemText()
        {
            // Check if in aircraft
            Ped player = Game.Player.Character;
            Vehicle vehicle = player?.CurrentVehicle;
            bool inAircraft = vehicle != null &&
                (vehicle.ClassType == VehicleClass.Planes || vehicle.ClassType == VehicleClass.Helicopters);

            if (!inAircraft)
            {
                return $"{_currentIndex + 1} of {MENU_ITEM_COUNT}: Not in aircraft";
            }

            string itemText;
            switch (_currentIndex)
            {
                case ITEM_CRUISE:
                    if (_manager.IsActive && _manager.FlightMode == Constants.FLIGHT_MODE_CRUISE)
                        itemText = "Cruise Mode (Active)";
                    else
                        itemText = "Start Cruise Mode";
                    break;

                case ITEM_WAYPOINT:
                    if (_manager.IsActive && _manager.FlightMode == Constants.FLIGHT_MODE_WAYPOINT)
                        itemText = "Fly to Waypoint (Active)";
                    else
                        itemText = "Fly to GPS Waypoint";
                    break;

                case ITEM_LAND_HERE:
                    itemText = "Land at Current Location";
                    break;

                case ITEM_INCREASE_ALT:
                    if (_manager.IsActive)
                    {
                        int altFeet = (int)(_manager.TargetAltitude * Constants.METERS_TO_FEET);
                        itemText = $"Increase Altitude (currently {altFeet} feet)";
                    }
                    else
                        itemText = "Increase Altitude";
                    break;

                case ITEM_DECREASE_ALT:
                    if (_manager.IsActive)
                    {
                        int altFeet = (int)(_manager.TargetAltitude * Constants.METERS_TO_FEET);
                        itemText = $"Decrease Altitude (currently {altFeet} feet)";
                    }
                    else
                        itemText = "Decrease Altitude";
                    break;

                case ITEM_INCREASE_SPEED:
                    if (_manager.IsActive)
                    {
                        int mph = (int)(_manager.TargetSpeed * Constants.METERS_PER_SECOND_TO_MPH);
                        itemText = $"Increase Speed (currently {mph} mph)";
                    }
                    else
                        itemText = "Increase Speed";
                    break;

                case ITEM_DECREASE_SPEED:
                    if (_manager.IsActive)
                    {
                        int mph = (int)(_manager.TargetSpeed * Constants.METERS_PER_SECOND_TO_MPH);
                        itemText = $"Decrease Speed (currently {mph} mph)";
                    }
                    else
                        itemText = "Decrease Speed";
                    break;

                case ITEM_PAUSE_RESUME:
                    if (_manager.IsPaused)
                        itemText = "Resume AutoFly";
                    else if (_manager.IsActive)
                        itemText = "Pause AutoFly";
                    else
                        itemText = "Pause/Resume (inactive)";
                    break;

                case ITEM_STATUS:
                    itemText = "Current Status";
                    break;

                case ITEM_STOP:
                    if (_manager.IsActive)
                        itemText = "Stop AutoFly";
                    else
                        itemText = "Stop AutoFly (inactive)";
                    break;

                default:
                    itemText = "Unknown";
                    break;
            }

            return $"{_currentIndex + 1} of {MENU_ITEM_COUNT}: {itemText}";
        }

        public void ExecuteSelection()
        {
            // Check if in aircraft
            Ped player = Game.Player.Character;
            Vehicle vehicle = player?.CurrentVehicle;
            bool inAircraft = vehicle != null &&
                (vehicle.ClassType == VehicleClass.Planes || vehicle.ClassType == VehicleClass.Helicopters);

            if (!inAircraft && _currentIndex != ITEM_STATUS)
            {
                Tolk.Speak("You must be in an aircraft to use AutoFly");
                GTA.Audio.PlaySoundFrontend("ERROR", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                return;
            }

            switch (_currentIndex)
            {
                case ITEM_CRUISE:
                    _manager.StartCruise();
                    GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    break;

                case ITEM_WAYPOINT:
                    _manager.StartWaypoint();
                    GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    break;

                case ITEM_LAND_HERE:
                    _manager.LandHere();
                    GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    break;

                case ITEM_INCREASE_ALT:
                    if (_manager.IsActive)
                    {
                        _manager.IncreaseAltitude();
                        GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    else
                    {
                        Tolk.Speak("AutoFly is not active");
                        GTA.Audio.PlaySoundFrontend("ERROR", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    break;

                case ITEM_DECREASE_ALT:
                    if (_manager.IsActive)
                    {
                        _manager.DecreaseAltitude();
                        GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    else
                    {
                        Tolk.Speak("AutoFly is not active");
                        GTA.Audio.PlaySoundFrontend("ERROR", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    break;

                case ITEM_INCREASE_SPEED:
                    if (_manager.IsActive)
                    {
                        _manager.IncreaseSpeed();
                        GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    else
                    {
                        Tolk.Speak("AutoFly is not active");
                        GTA.Audio.PlaySoundFrontend("ERROR", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    break;

                case ITEM_DECREASE_SPEED:
                    if (_manager.IsActive)
                    {
                        _manager.DecreaseSpeed();
                        GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    else
                    {
                        Tolk.Speak("AutoFly is not active");
                        GTA.Audio.PlaySoundFrontend("ERROR", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    break;

                case ITEM_PAUSE_RESUME:
                    if (_manager.IsPaused)
                    {
                        _manager.Resume();
                        GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    else if (_manager.IsActive)
                    {
                        _manager.Pause();
                        GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    else
                    {
                        Tolk.Speak("AutoFly is not active");
                        GTA.Audio.PlaySoundFrontend("ERROR", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    break;

                case ITEM_STATUS:
                    _manager.AnnounceStatus();
                    GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    break;

                case ITEM_STOP:
                    if (_manager.IsActive)
                    {
                        _manager.Stop();
                        GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    else
                    {
                        Tolk.Speak("AutoFly is not active");
                        GTA.Audio.PlaySoundFrontend("ERROR", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    }
                    break;
            }
        }

        public string GetMenuName()
        {
            if (_manager.IsActive)
            {
                string modeName = Constants.GetFlightModeName(_manager.FlightMode);
                return $"AutoFly ({modeName})";
            }
            return "AutoFly";
        }

        public bool HasActiveSubmenu => false;

        public void ExitSubmenu()
        {
            // No submenu
        }
    }
}
