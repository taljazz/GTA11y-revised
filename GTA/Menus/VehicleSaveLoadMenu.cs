using System.Collections.Generic;
using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for saving, loading, and clearing vehicle save slots.
    /// Top level = the three actions, submenu = the ten slots.
    /// The active action is tracked with an enum (SubmenuMode) rather than
    /// re-deriving it from the selection index.
    /// </summary>
    public class VehicleSaveLoadMenu : HierarchicalMenuBase
    {
        #region Types

        /// <summary>Which action the slot submenu applies to.</summary>
        private enum SubmenuMode
        {
            Save,
            Load,
            Clear
        }

        #endregion

        #region Fields

        private readonly VehicleSaveManager _saveManager;
        private readonly SettingsManager _settings;
        private readonly List<string> _options;
        private SubmenuMode _submenuMode;

        #endregion

        #region Construction

        public VehicleSaveLoadMenu(VehicleSaveManager saveManager, SettingsManager settings, AudioManager audio)
            : base(audio)
        {
            _saveManager = saveManager;
            _settings = settings;

            _options = new List<string>
            {
                "Save Current Vehicle",
                "Load Saved Vehicle",
                "Clear Slot"
            };
        }

        #endregion

        #region Top Level - actions

        protected override int ItemCount => _options.Count;

        protected override string GetItemText(int index)
        {
            return _options[index];
        }

        protected override void OnItemActivated(int index)
        {
            switch (index)
            {
                case 0:
                    // Save - check if player is in a vehicle (use IsInVehicle to avoid stale references)
                    if (!Game.Player.Character.IsInVehicle())
                    {
                        Speak("You must be in a vehicle to save.");
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

            EnterSubmenu();
        }

        public override string GetMenuName()
        {
            if (InSubmenu)
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

        #endregion

        #region Submenu - slots

        protected override int SubmenuItemCount => Constants.VEHICLE_SAVE_SLOT_COUNT;

        protected override string GetSubmenuItemText(int index)
        {
            return _saveManager.GetSlotDescription(index);
        }

        protected override void OnSubmenuItemActivated(int index)
        {
            // Defensive: Validate save manager
            if (_saveManager == null)
            {
                Speak("Save system unavailable.");
                return;
            }

            switch (_submenuMode)
            {
                case SubmenuMode.Save:
                    ExecuteSave(index);
                    break;

                case SubmenuMode.Load:
                    ExecuteLoad(index);
                    break;

                case SubmenuMode.Clear:
                    ExecuteClear(index);
                    break;
            }
        }

        #endregion

        #region Slot Actions

        private void ExecuteSave(int slotIndex)
        {
            // Defensive: Check player exists
            Ped player = Game.Player?.Character;
            if (player == null || !player.Exists())
            {
                Speak("Player unavailable.");
                return;
            }

            // Use IsInVehicle() to avoid stale CurrentVehicle references
            if (!player.IsInVehicle())
            {
                Speak("No vehicle to save.");
                return;
            }
            Vehicle vehicle = player.CurrentVehicle;

            // Defensive: Validate vehicle
            if (vehicle == null || !vehicle.Exists())
            {
                Speak("Vehicle unavailable.");
                return;
            }

            bool saved = _saveManager.SaveVehicleToSlot(vehicle, slotIndex);
            if (saved)
            {
                Speak($"Saved {vehicle.DisplayName} to slot {slotIndex + 1}");
            }
            else
            {
                Speak("Failed to save vehicle.");
            }
        }

        private void ExecuteLoad(int slotIndex)
        {
            if (!_saveManager.IsSlotOccupied(slotIndex))
            {
                Speak("Slot is empty.");
                return;
            }

            Vehicle spawned = _saveManager.SpawnVehicleFromSlot(slotIndex, _settings);
            if (spawned != null)
            {
                Speak($"Loaded {spawned.DisplayName}");
            }
            else
            {
                Speak("Failed to load vehicle.");
            }
        }

        private void ExecuteClear(int slotIndex)
        {
            if (!_saveManager.IsSlotOccupied(slotIndex))
            {
                Speak("Slot is already empty.");
                return;
            }

            _saveManager.ClearSlot(slotIndex);
            Speak($"Cleared slot {slotIndex + 1}");
        }

        #endregion
    }
}
