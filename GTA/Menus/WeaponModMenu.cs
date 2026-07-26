using System;
using System.Collections.Generic;
using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for weapon attachments and tints on the currently equipped weapon.
    /// Top level = one toggle item per supported component (suppressor, scope,
    /// extended clip, grip, flashlight, barrel, finish...) plus a "Weapon tint"
    /// item that opens a color submenu. The component list rebuilds automatically
    /// whenever the equipped weapon changes, mirroring the vehicle mod proxy.
    /// Uses the typed SHVDN 3.7 WeaponComponent API (Active is read/write).
    /// </summary>
    public class WeaponModMenu : HierarchicalMenuBase
    {
        #region Constants

        // Classic weapon tint names, indexed by the WeaponTint enum values (0-7)
        private static readonly string[] ClassicTintNames =
        {
            "Normal", "Green", "Gold", "Pink", "Army", "L S P D", "Orange", "Platinum"
        };

        // Mark 2 weapon tint names (TintCount is 32 for Mk II weapons)
        private static readonly string[] Mk2TintNames =
        {
            "Classic Black", "Classic Gray", "Classic Two-Tone", "Classic White",
            "Classic Beige", "Classic Green", "Classic Blue", "Classic Earth",
            "Classic Brown and Black", "Red Contrast", "Blue Contrast", "Yellow Contrast",
            "Orange Contrast", "Bold Pink", "Bold Purple and Yellow", "Bold Orange",
            "Bold Green and Purple", "Bold Red Features", "Bold Green Features",
            "Bold Cyan Features", "Bold Yellow Features", "Bold Red and White",
            "Bold Blue and White", "Metallic Gold", "Metallic Platinum",
            "Metallic Gray and Lilac", "Metallic Purple and Lime", "Metallic Red",
            "Metallic Green", "Metallic Blue", "Metallic White and Aqua",
            "Metallic Red and Yellow"
        };

        #endregion

        #region Fields

        private readonly List<WeaponComponent> _components = new List<WeaponComponent>();
        private WeaponHash _lastWeaponHash = (WeaponHash)0;
        private bool _hasWeapon;

        #endregion

        #region Construction

        public WeaponModMenu(AudioManager audio) : base(audio)
        {
        }

        #endregion

        #region Weapon Tracking

        /// <summary>
        /// Get the currently equipped weapon, or null when unavailable.
        /// </summary>
        private Weapon CurrentWeapon
        {
            get
            {
                try
                {
                    Ped player = Game.Player?.Character;
                    if (player == null || !player.Exists())
                        return null;
                    return player.Weapons?.Current;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Rebuild the component list when the equipped weapon changes.
        /// Components are sorted by attachment point so clips, scopes, and
        /// suppressors group together predictably for navigation.
        /// </summary>
        private void RefreshForCurrentWeapon()
        {
            Weapon weapon = CurrentWeapon;
            WeaponHash hash = weapon?.Hash ?? WeaponHash.Unarmed;

            if (hash == _lastWeaponHash)
                return;

            _lastWeaponHash = hash;
            _components.Clear();
            ResetSelection();
            ExitSubmenu();

            _hasWeapon = weapon != null && hash != WeaponHash.Unarmed && weapon.IsPresent;
            if (!_hasWeapon)
                return;

            try
            {
                foreach (WeaponComponent component in weapon.Components)
                {
                    if (component == null)
                        continue;
                    if (component.AttachmentPoint == WeaponAttachmentPoint.Invalid)
                        continue;

                    _components.Add(component);
                }

                _components.Sort((a, b) =>
                {
                    int byPoint = a.AttachmentPoint.CompareTo(b.AttachmentPoint);
                    if (byPoint != 0) return byPoint;
                    return string.Compare(GetComponentName(a), GetComponentName(b), StringComparison.Ordinal);
                });
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponModMenu.RefreshForCurrentWeapon");
            }
        }

        #endregion

        #region Top Level - components and tint entry

        // The tint item sits after all components
        private int TintItemIndex => _components.Count;

        protected override int ItemCount
        {
            get
            {
                RefreshForCurrentWeapon();
                return _hasWeapon ? _components.Count + 1 : 0;
            }
        }

        protected override string EmptyMenuText => "No weapon equipped";

        protected override string GetItemText(int index)
        {
            if (index == TintItemIndex)
            {
                Weapon weapon = CurrentWeapon;
                int tintCount = weapon?.TintCount ?? 0;
                if (tintCount <= 0)
                    return "Weapon tint: not available";

                int current = weapon != null ? (int)weapon.Tint : 0;
                return $"Weapon tint: {GetTintName(current, tintCount)}";
            }

            WeaponComponent component = _components[index];
            string state = SafeIsActive(component) ? "attached" : "not attached";
            return $"{index + 1} of {_components.Count}: {GetComponentName(component)}, {state}";
        }

        protected override void OnItemActivated(int index)
        {
            Weapon weapon = CurrentWeapon;
            if (weapon == null)
            {
                Speak("No weapon equipped");
                return;
            }

            if (index == TintItemIndex)
            {
                int tintCount = weapon.TintCount;
                if (tintCount <= 0)
                {
                    Speak("This weapon has no tints.");
                    return;
                }

                // Open the tint submenu positioned at the current tint
                EnterSubmenu((int)weapon.Tint);
                return;
            }

            ToggleComponent(_components[index]);
        }

        public override string GetMenuName()
        {
            RefreshForCurrentWeapon();

            if (InSubmenu)
                return "Weapon Tint";

            if (_hasWeapon)
            {
                Weapon weapon = CurrentWeapon;
                string weaponName = null;
                try { weaponName = weapon?.LocalizedName; } catch { }
                if (!string.IsNullOrEmpty(weaponName))
                    return $"Weapon Mods, {weaponName}";
            }

            return "Weapon Mods";
        }

        #endregion

        #region Submenu - tints

        protected override int SubmenuItemCount => CurrentWeapon?.TintCount ?? 0;

        protected override string GetSubmenuItemText(int index)
        {
            Weapon weapon = CurrentWeapon;
            int tintCount = weapon?.TintCount ?? 0;
            string current = weapon != null && (int)weapon.Tint == index ? ", current" : "";
            return $"{index + 1} of {tintCount}: {GetTintName(index, tintCount)}{current}";
        }

        protected override void OnSubmenuItemActivated(int index)
        {
            Weapon weapon = CurrentWeapon;
            if (weapon == null)
            {
                Speak("No weapon equipped");
                return;
            }

            try
            {
                weapon.Tint = (WeaponTint)index;
                Speak($"Tint: {GetTintName(index, weapon.TintCount)}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponModMenu.OnSubmenuItemActivated");
                Speak("Failed to apply tint");
            }
        }

        #endregion

        #region Component Helpers

        /// <summary>
        /// Attach or detach a component and announce the result.
        /// The game handles mutual exclusion itself (attaching one scope
        /// removes another), so the menu reads states live rather than caching.
        /// </summary>
        private void ToggleComponent(WeaponComponent component)
        {
            try
            {
                bool target = !component.Active;
                component.Active = target;

                // Read back the real state - the game may refuse some combinations
                bool nowActive = component.Active;
                string name = GetComponentName(component);

                if (nowActive == target)
                {
                    Speak(nowActive ? $"{name} attached" : $"{name} removed");
                }
                else
                {
                    Speak($"Could not change {name}");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponModMenu.ToggleComponent");
                Speak("Failed to change attachment");
            }
        }

        /// <summary>
        /// Component Active reads can fail for stale wrappers - treat as detached.
        /// </summary>
        private static bool SafeIsActive(WeaponComponent component)
        {
            try { return component.Active; }
            catch { return false; }
        }

        /// <summary>
        /// Best spoken name for a component: the game's localized name when it
        /// has one, otherwise a friendly name for its attachment point.
        /// </summary>
        private static string GetComponentName(WeaponComponent component)
        {
            try
            {
                string localized = component.LocalizedName;
                if (!string.IsNullOrEmpty(localized) && localized != "NULL")
                    return localized;
            }
            catch
            {
                // Fall through to the attachment point name
            }

            return GetAttachmentPointName(component.AttachmentPoint);
        }

        /// <summary>
        /// Friendly names for attachment points (fallback when no localized name).
        /// </summary>
        private static string GetAttachmentPointName(WeaponAttachmentPoint point)
        {
            switch (point)
            {
                case WeaponAttachmentPoint.Clip:
                case WeaponAttachmentPoint.Clip2:
                    return "Magazine";
                case WeaponAttachmentPoint.Scope:
                case WeaponAttachmentPoint.Scope2:
                    return "Scope";
                case WeaponAttachmentPoint.Supp:
                case WeaponAttachmentPoint.Supp2:
                    return "Suppressor";
                case WeaponAttachmentPoint.Grip:
                case WeaponAttachmentPoint.Grip2:
                    return "Grip";
                case WeaponAttachmentPoint.Flash:
                case WeaponAttachmentPoint.FlashLaser:
                case WeaponAttachmentPoint.FlashLaser2:
                    return "Flashlight";
                case WeaponAttachmentPoint.Barrel:
                    return "Barrel";
                case WeaponAttachmentPoint.GunRoot:
                    return "Finish";
                case WeaponAttachmentPoint.Rail:
                case WeaponAttachmentPoint.Rail2:
                    return "Rail";
                case WeaponAttachmentPoint.TorchBulb:
                    return "Torch bulb";
                default:
                    return "Attachment";
            }
        }

        /// <summary>
        /// Spoken tint name: Mk 2 weapons have 32 named camo tints,
        /// classic weapons have the 8 standard colors.
        /// </summary>
        private static string GetTintName(int index, int tintCount)
        {
            if (tintCount > ClassicTintNames.Length)
            {
                if (index >= 0 && index < Mk2TintNames.Length)
                    return Mk2TintNames[index];
            }
            else if (index >= 0 && index < ClassicTintNames.Length)
            {
                return ClassicTintNames[index];
            }

            return $"Tint {index + 1}";
        }

        #endregion
    }
}
