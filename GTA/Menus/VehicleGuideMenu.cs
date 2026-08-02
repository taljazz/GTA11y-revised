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

        #endregion

        #region Construction

        public VehicleGuideMenu(AudioManager audio) : base(audio)
        {
            _upgradeTypes = VehicleDescriber.GetUpgradeTypes();
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
                    return $"Upgrade guide: what each modification does, {_upgradeTypes.Count} categories";
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
                    Speak($"Upgrade guide, {_upgradeTypes.Count} categories. {GetSubmenuItemText(0)}");
                    break;
            }
        }

        public override string GetMenuName()
        {
            return InSubmenu ? "Upgrade Guide" : "Vehicle Guide";
        }

        #endregion

        #region Upgrade Submenu

        protected override int SubmenuItemCount => _upgradeTypes.Count;

        protected override string GetSubmenuItemText(int index)
        {
            if (index < 0 || index >= _upgradeTypes.Count)
                return EmptyMenuText;

            return $"{index + 1} of {_upgradeTypes.Count}: " +
                   VehicleDescriber.GetUpgradeEffect(_upgradeTypes[index]);
        }

        protected override void OnSubmenuItemActivated(int index)
        {
            if (index < 0 || index >= _upgradeTypes.Count)
                return;

            // Repeating the entry is the useful action here - these are long
            // lines and the player may want to hear one again
            Speak(VehicleDescriber.GetUpgradeEffect(_upgradeTypes[index]));
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
