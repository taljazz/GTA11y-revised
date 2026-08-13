using System;
using System.Collections.Generic;
using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Explains vehicles and their upgrades - the things a sighted player learns
    /// by looking at a car and reading a garage screen.
    ///
    /// Top level: describe whatever you are sitting in, plus an entry into the
    /// upgrade guide. The upgrade submenu lists every modification category and
    /// says plainly what it does to the car, so choosing upgrades is an informed
    /// decision rather than a guess at what "Transmission, level 3" means.
    /// </summary>
    public class VehicleGuideMenu : HierarchicalMenuBase
    {
        #region Constants

        private const int ITEM_DESCRIBE_CURRENT = 0;
        private const int ITEM_UPGRADE_GUIDE = 1;
        private const int ITEM_COUNT = 2;

        #endregion

        #region Fields

        private readonly List<VehicleModType> _upgradeTypes;
        private readonly List<VehicleToggleModType> _toggleTypes;

        #endregion

        #region Construction

        public VehicleGuideMenu(AudioManager audio) : base(audio)
        {
            _upgradeTypes = VehicleDescriber.GetUpgradeTypes();
            _toggleTypes = VehicleDescriber.GetToggleTypes();
        }

        #endregion

        #region Top Level

        protected override int ItemCount => ITEM_COUNT;

        protected override string GetItemText(int index)
        {
            switch (index)
            {
                case ITEM_DESCRIBE_CURRENT:
                {
                    Vehicle vehicle = CurrentVehicle;
                    return vehicle == null
                        ? "Describe my vehicle: you are not in one"
                        : "Describe the vehicle I am in";
                }
                case ITEM_UPGRADE_GUIDE:
                    return $"Upgrade guide: what each modification does, {SubmenuItemCount} categories";
                default:
                    return "Unknown";
            }
        }

        protected override void OnItemActivated(int index)
        {
            switch (index)
            {
                case ITEM_DESCRIBE_CURRENT:
                    DescribeCurrentVehicle();
                    break;

                case ITEM_UPGRADE_GUIDE:
                    EnterSubmenu();
                    Logger.Info($"GUIDE|upgrades|categories={SubmenuItemCount}");
                    // The caveat comes first: several categories are numbered
                    // slots that mean different things on different vehicles,
                    // and hearing that up front stops the rest being read as
                    // promises the game does not keep
                    Speak($"Upgrade guide, {SubmenuItemCount} categories. " +
                          $"{VehicleDescriber.GetSlotCaveat()} {GetSubmenuItemText(0)}");
                    break;
            }
        }

        public override string GetMenuName()
        {
            return InSubmenu ? "Upgrade Guide" : "Vehicle Guide";
        }

        #endregion

        #region Upgrade Submenu

        // The on-or-off upgrades follow the numbered ones in the same list
        protected override int SubmenuItemCount => _upgradeTypes.Count + _toggleTypes.Count;

        protected override string GetSubmenuItemText(int index)
        {
            int total = SubmenuItemCount;
            if (index < 0 || index >= total)
                return EmptyMenuText;

            string effect = index < _upgradeTypes.Count
                ? VehicleDescriber.GetUpgradeEffect(_upgradeTypes[index])
                : VehicleDescriber.GetToggleEffect(_toggleTypes[index - _upgradeTypes.Count]);

            return $"{index + 1} of {total}: {effect}";
        }

        protected override void OnSubmenuItemActivated(int index)
        {
            if (index < 0 || index >= SubmenuItemCount)
                return;

            // Repeating the entry is the useful action here - these are long
            // lines and the player may want to hear one again
            Speak(index < _upgradeTypes.Count
                ? VehicleDescriber.GetUpgradeEffect(_upgradeTypes[index])
                : VehicleDescriber.GetToggleEffect(_toggleTypes[index - _upgradeTypes.Count]));
        }

        #endregion

        #region Helpers

        private static Vehicle CurrentVehicle
        {
            get
            {
                try
                {
                    Ped player = Game.Player?.Character;
                    if (player == null || !player.Exists())
                        return null;

                    Vehicle vehicle = player.CurrentVehicle;
                    return vehicle != null && vehicle.Exists() ? vehicle : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        private void DescribeCurrentVehicle()
        {
            Vehicle vehicle = CurrentVehicle;
            if (vehicle == null)
            {
                Speak("You are not in a vehicle. Get in one and try again.");
                return;
            }

            try
            {
                // Log which vehicle was asked about and whether it had a
                // hand-written note - that is the only way to find out from a
                // test session which vehicles fell back to the generic facts
                bool curated = VehicleDescriber.GetCuratedNote(vehicle.Model) != null;
                Logger.Info($"GUIDE|describe|hash={vehicle.Model.Hash}|curated={curated}");

                Speak(VehicleDescriber.GetFullDescription(vehicle.Model));
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "VehicleGuideMenu.DescribeCurrentVehicle");
                Speak("Could not describe this vehicle.");
            }
        }

        #endregion
    }
}
