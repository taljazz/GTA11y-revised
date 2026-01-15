using System.Collections.Generic;
using GTA;
using DavyKager;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for saving, loading, and clearing vehicle save slots
    /// </summary>
    public class VehicleSaveLoadMenu : IMenuState
    {
        private readonly VehicleSaveManager _saveManager;
        private readonly SettingsManager _settings;

        // Menu options at top level
        private readonly List<string> _options;
        private int _currentOptionIndex;

        // Submenu state (slot selection)
        private bool _inSubmenu;
        private int _currentSlotIndex;
        private SubmenuMode _submenuMode;

        private enum SubmenuMode
        {
            Save,
            Load,
            Clear
        }

        public VehicleSaveLoadMenu(VehicleSaveManager saveManager, SettingsManager settings)
        {
            _saveManager = saveManager;
            _settings = settings;
            _inSubmenu = false;
            _currentSlotIndex = 0;

            _options = new List<string>
            {
                "Save Current Vehicle",
                "Load Saved Vehicle",
                "Clear Slot"
            };

            _currentOptionIndex = 0;
        }

        public void NavigatePrevious(bool fastScroll = false)
        {
            if (_inSubmenu)
            {
                // Navigate slots
                if (_currentSlotIndex > 0)
                    _currentSlotIndex--;
                else
                    _currentSlotIndex = Constants.VEHICLE_SAVE_SLOT_COUNT - 1;
            }
            else
            {
                // Navigate options
                if (_currentOptionIndex > 0)
                    _currentOptionIndex--;
                else
                    _currentOptionIndex = _options.Count - 1;
            }
        }

        public void NavigateNext(bool fastScroll = false)
        {
            if (_inSubmenu)
            {
                // Navigate slots
                if (_currentSlotIndex < Constants.VEHICLE_SAVE_SLOT_COUNT - 1)
                    _currentSlotIndex++;
                else
                    _currentSlotIndex = 0;
            }
            else
            {
                // Navigate options
                if (_currentOptionIndex < _options.Count - 1)
                    _currentOptionIndex++;
                else
                    _currentOptionIndex = 0;
            }
        }

        public string GetCurrentItemText()
        {
            if (_inSubmenu)
            {
                return _saveManager.GetSlotDescription(_currentSlotIndex);
            }
            else
            {
                return _options[_currentOptionIndex];
            }
        }

        public void ExecuteSelection()
        {
            if (_inSubmenu)
            {
                ExecuteSlotAction();
            }
            else
            {
                EnterSubmenu();
            }
        }

        private void EnterSubmenu()
        {
            switch (_currentOptionIndex)
            {
                case 0:
                    // Save - check if player is in a vehicle (use IsInVehicle to avoid stale references)
                    if (!Game.Player.Character.IsInVehicle())
                    {
                        Tolk.Speak("You must be in a vehicle to save.");
                        return;
                    }
                    _submenuMode = SubmenuMode.Save;
                    break;

                case 1:
                    _submenuMode = SubmenuMode.Load;
                    break;

                case 2:
                    _submenuMode = SubmenuMode.Clear;
                    break;
            }

            _inSubmenu = true;
            _currentSlotIndex = 0;
        }

        private void ExecuteSlotAction()
        {
            // Defensive: Validate slot index
            if (_currentSlotIndex < 0 || _currentSlotIndex >= Constants.VEHICLE_SAVE_SLOT_COUNT)
            {
                _currentSlotIndex = 0;
                return;
            }

            // Defensive: Validate save manager
            if (_saveManager == null)
            {
                Tolk.Speak("Save system unavailable.");
                return;
            }

            switch (_submenuMode)
            {
                case SubmenuMode.Save:
                    // Defensive: Check player exists
                    Ped player = Game.Player?.Character;
                    if (player == null || !player.Exists())
                    {
                        Tolk.Speak("Player unavailable.");
                        return;
                    }

                    // Use IsInVehicle() to avoid stale CurrentVehicle references
                    if (!player.IsInVehicle())
                    {
                        Tolk.Speak("No vehicle to save.");
                        return;
                    }
                    Vehicle vehicle = player.CurrentVehicle;

                    // Defensive: Validate vehicle
                    if (vehicle == null || !vehicle.Exists())
                    {
                        Tolk.Speak("Vehicle unavailable.");
                        return;
                    }

                    bool saved = _saveManager.SaveVehicleToSlot(vehicle, _currentSlotIndex);
                    if (saved)
                    {
                        Tolk.Speak($"Saved {vehicle.DisplayName} to slot {_currentSlotIndex + 1}");
                    }
                    else
                    {
                        Tolk.Speak("Failed to save vehicle.");
                    }
                    break;

                case SubmenuMode.Load:
                    if (!_saveManager.IsSlotOccupied(_currentSlotIndex))
                    {
                        Tolk.Speak("Slot is empty.");
                        return;
                    }

                    Vehicle spawned = _saveManager.SpawnVehicleFromSlot(_currentSlotIndex, _settings);
                    if (spawned != null)
                    {
                        Tolk.Speak($"Loaded {spawned.DisplayName}");
                    }
                    else
                    {
                        Tolk.Speak("Failed to load vehicle.");
                    }
                    break;

                case SubmenuMode.Clear:
                    if (!_saveManager.IsSlotOccupied(_currentSlotIndex))
                    {
                        Tolk.Speak("Slot is already empty.");
                        return;
                    }

                    _saveManager.ClearSlot(_currentSlotIndex);
                    Tolk.Speak($"Cleared slot {_currentSlotIndex + 1}");
                    break;
            }
        }

        public string GetMenuName()
        {
            if (_inSubmenu)
            {
                switch (_submenuMode)
                {
                    case SubmenuMode.Save:
                        return "Save to Slot";
                    case SubmenuMode.Load:
                        return "Load from Slot";
                    case SubmenuMode.Clear:
                        return "Clear Slot";
                }
            }
            return "Vehicle Save/Load";
        }

        public bool HasActiveSubmenu => _inSubmenu;

        public void ExitSubmenu()
        {
            if (_inSubmenu)
            {
                _inSubmenu = false;
                _currentSlotIndex = 0;
            }
        }
    }
}
