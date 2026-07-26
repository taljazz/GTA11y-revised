using System;
using System.Collections.Generic;
using GTA;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Menu for obtaining, equipping, and discarding weapons - the companion to
    /// WeaponModMenu, which only tunes the weapon that is already in hand.
    ///
    /// Top level = weapon categories (melee, pistols, SMGs, ...) plus bulk
    /// actions (give all, refill ammo, remove all, drop, go unarmed).
    /// Submenu = the weapons in that category. Every submenu item announces the
    /// ownership state AND what the next press will do, so a single key covers
    /// all three actions without hidden modes:
    ///     not owned            -> press adds the weapon with full ammo and equips it
    ///     owned, not in hand   -> press equips it
    ///     currently equipped   -> press removes it from the inventory
    /// Removal therefore always takes two deliberate steps (equip, then remove),
    /// which keeps a mis-press from silently destroying a loadout.
    ///
    /// Uses the typed SHVDN 3.7 WeaponCollection API only - no natives.
    /// </summary>
    public class WeaponSelectMenu : HierarchicalMenuBase
    {
        #region Types

        /// <summary>One weapon offered by the menu, with the name to speak for it.</summary>
        private class WeaponEntry
        {
            public WeaponHash Hash { get; }
            public string Name { get; }

            /// <summary>True for items whose ammo count is meaningless (goggles, parachute...).</summary>
            public bool NoAmmo { get; }

            public WeaponEntry(WeaponHash hash, string name, bool noAmmo = false)
            {
                Hash = hash;
                Name = name;
                NoAmmo = noAmmo;
            }
        }

        /// <summary>
        /// A spoken weapon category. GiveAmmo is the ammo count used when the
        /// game cannot report a maximum; 0 marks a category whose weapons take
        /// no ammo at all (melee). InGiveAll excludes prop-like oddities from
        /// the "give all weapons" action.
        /// </summary>
        private class WeaponCategory
        {
            public string Name { get; }
            public int GiveAmmo { get; }
            public bool InGiveAll { get; }
            public WeaponEntry[] Weapons { get; }

            public WeaponCategory(string name, int giveAmmo, bool inGiveAll, WeaponEntry[] weapons)
            {
                Name = name;
                GiveAmmo = giveAmmo;
                InGiveAll = inGiveAll;
                Weapons = weapons;
            }
        }

        #endregion

        #region Constants - weapon catalog

        // Bulk action item indices, counted from the end of the category list
        private const int ITEM_GIVE_ALL = 0;
        private const int ITEM_REFILL_AMMO = 1;
        private const int ITEM_REMOVE_ALL = 2;
        private const int ITEM_DROP_CURRENT = 3;
        private const int ITEM_GO_UNARMED = 4;
        private const int BULK_ITEM_COUNT = 5;

        /// <summary>How long a "press again to confirm" arming stays valid, in game milliseconds.</summary>
        private const int CONFIRM_WINDOW_MS = 5000;

        /// <summary>
        /// How long after a menu-initiated weapon swap the main script should stay
        /// quiet, so the automatic weapon-change announcement does not repeat the
        /// name this menu just spoke.
        /// </summary>
        private const int SELF_CHANGE_SUPPRESS_MS = 2000;

        /// <summary>
        /// Every weapon a human ped can carry, grouped for navigation. Names are
        /// written for speech: initialisms are spaced out ("S M G") because screen
        /// readers otherwise run them together, and "Mk II" is spelled "Mark 2".
        /// Weapons missing from the installed game build are filtered out at
        /// runtime by IsWeaponValid, so newer DLC entries are safe to list.
        /// Unarmed is deliberately absent - the "Go unarmed" action covers it.
        /// </summary>
        private static readonly WeaponCategory[] Catalog =
        {
            new WeaponCategory("Melee", 0, true, new[]
            {
                new WeaponEntry(WeaponHash.Knife, "Knife"),
                new WeaponEntry(WeaponHash.Nightstick, "Nightstick"),
                new WeaponEntry(WeaponHash.Hammer, "Hammer"),
                new WeaponEntry(WeaponHash.Bat, "Baseball Bat"),
                new WeaponEntry(WeaponHash.GolfClub, "Golf Club"),
                new WeaponEntry(WeaponHash.Crowbar, "Crowbar"),
                new WeaponEntry(WeaponHash.Bottle, "Broken Bottle"),
                new WeaponEntry(WeaponHash.Dagger, "Antique Cavalry Dagger"),
                new WeaponEntry(WeaponHash.Hatchet, "Hatchet"),
                new WeaponEntry(WeaponHash.KnuckleDuster, "Brass Knuckles"),
                new WeaponEntry(WeaponHash.Machete, "Machete"),
                new WeaponEntry(WeaponHash.Flashlight, "Flashlight"),
                new WeaponEntry(WeaponHash.SwitchBlade, "Switchblade"),
                new WeaponEntry(WeaponHash.PoolCue, "Pool Cue"),
                new WeaponEntry(WeaponHash.Wrench, "Pipe Wrench"),
                new WeaponEntry(WeaponHash.BattleAxe, "Battle Axe"),
                new WeaponEntry(WeaponHash.StoneHatchet, "Stone Hatchet"),
                new WeaponEntry(WeaponHash.CandyCane, "Candy Cane")
            }),

            new WeaponCategory("Pistols", 250, true, new[]
            {
                new WeaponEntry(WeaponHash.Pistol, "Pistol"),
                new WeaponEntry(WeaponHash.PistolMk2, "Pistol Mark 2"),
                new WeaponEntry(WeaponHash.CombatPistol, "Combat Pistol"),
                new WeaponEntry(WeaponHash.APPistol, "A P Pistol"),
                new WeaponEntry(WeaponHash.Pistol50, "Pistol Fifty"),
                new WeaponEntry(WeaponHash.SNSPistol, "S N S Pistol"),
                new WeaponEntry(WeaponHash.SNSPistolMk2, "S N S Pistol Mark 2"),
                new WeaponEntry(WeaponHash.HeavyPistol, "Heavy Pistol"),
                new WeaponEntry(WeaponHash.VintagePistol, "Vintage Pistol"),
                new WeaponEntry(WeaponHash.MarksmanPistol, "Marksman Pistol"),
                new WeaponEntry(WeaponHash.CeramicPistol, "Ceramic Pistol"),
                new WeaponEntry(WeaponHash.PericoPistol, "Perico Pistol"),
                new WeaponEntry(WeaponHash.WM29Pistol, "W M 29 Pistol"),
                new WeaponEntry(WeaponHash.Revolver, "Heavy Revolver"),
                new WeaponEntry(WeaponHash.RevolverMk2, "Heavy Revolver Mark 2"),
                new WeaponEntry(WeaponHash.DoubleActionRevolver, "Double Action Revolver"),
                new WeaponEntry(WeaponHash.NavyRevolver, "Navy Revolver"),
                new WeaponEntry(WeaponHash.StunGun, "Stun Gun"),
                new WeaponEntry(WeaponHash.StunGunMultiplayer, "Stun Gun, online version"),
                new WeaponEntry(WeaponHash.FlareGun, "Flare Gun"),
                new WeaponEntry(WeaponHash.UpNAtomizer, "Up and Atomizer")
            }),

            new WeaponCategory("Submachine Guns", 500, true, new[]
            {
                new WeaponEntry(WeaponHash.MicroSMG, "Micro S M G"),
                new WeaponEntry(WeaponHash.MachinePistol, "Machine Pistol"),
                new WeaponEntry(WeaponHash.MiniSMG, "Mini S M G"),
                new WeaponEntry(WeaponHash.SMG, "S M G"),
                new WeaponEntry(WeaponHash.SMGMk2, "S M G Mark 2"),
                new WeaponEntry(WeaponHash.AssaultSMG, "Assault S M G"),
                new WeaponEntry(WeaponHash.CombatPDW, "Combat P D W"),
                new WeaponEntry(WeaponHash.TacticalSMG, "Tactical S M G"),
                new WeaponEntry(WeaponHash.Gusenberg, "Gusenberg Sweeper"),
                new WeaponEntry(WeaponHash.UnholyHellbringer, "Unholy Hellbringer")
            }),

            new WeaponCategory("Shotguns", 200, true, new[]
            {
                new WeaponEntry(WeaponHash.PumpShotgun, "Pump Shotgun"),
                new WeaponEntry(WeaponHash.PumpShotgunMk2, "Pump Shotgun Mark 2"),
                new WeaponEntry(WeaponHash.SawnOffShotgun, "Sawed-Off Shotgun"),
                new WeaponEntry(WeaponHash.BullpupShotgun, "Bullpup Shotgun"),
                new WeaponEntry(WeaponHash.AssaultShotgun, "Assault Shotgun"),
                new WeaponEntry(WeaponHash.HeavyShotgun, "Heavy Shotgun"),
                new WeaponEntry(WeaponHash.DoubleBarrelShotgun, "Double-Barrel Shotgun"),
                new WeaponEntry(WeaponHash.SweeperShotgun, "Sweeper Shotgun"),
                new WeaponEntry(WeaponHash.CombatShotgun, "Combat Shotgun"),
                new WeaponEntry(WeaponHash.Musket, "Musket")
            }),

            new WeaponCategory("Assault Rifles", 500, true, new[]
            {
                new WeaponEntry(WeaponHash.AssaultRifle, "Assault Rifle"),
                new WeaponEntry(WeaponHash.AssaultrifleMk2, "Assault Rifle Mark 2"),
                new WeaponEntry(WeaponHash.CarbineRifle, "Carbine Rifle"),
                new WeaponEntry(WeaponHash.CarbineRifleMk2, "Carbine Rifle Mark 2"),
                new WeaponEntry(WeaponHash.AdvancedRifle, "Advanced Rifle"),
                new WeaponEntry(WeaponHash.SpecialCarbine, "Special Carbine"),
                new WeaponEntry(WeaponHash.SpecialCarbineMk2, "Special Carbine Mark 2"),
                new WeaponEntry(WeaponHash.BullpupRifle, "Bullpup Rifle"),
                new WeaponEntry(WeaponHash.BullpupRifleMk2, "Bullpup Rifle Mark 2"),
                new WeaponEntry(WeaponHash.CompactRifle, "Compact Rifle"),
                new WeaponEntry(WeaponHash.MilitaryRifle, "Military Rifle"),
                new WeaponEntry(WeaponHash.HeavyRifle, "Heavy Rifle"),
                new WeaponEntry(WeaponHash.ServiceCarbine, "Service Carbine"),
                new WeaponEntry(WeaponHash.BattleRifle, "Battle Rifle")
            }),

            new WeaponCategory("Machine Guns", 500, true, new[]
            {
                new WeaponEntry(WeaponHash.MG, "M G"),
                new WeaponEntry(WeaponHash.CombatMG, "Combat M G"),
                new WeaponEntry(WeaponHash.CombatMGMk2, "Combat M G Mark 2")
            }),

            new WeaponCategory("Sniper Rifles", 100, true, new[]
            {
                new WeaponEntry(WeaponHash.SniperRifle, "Sniper Rifle"),
                new WeaponEntry(WeaponHash.HeavySniper, "Heavy Sniper"),
                new WeaponEntry(WeaponHash.HeavySniperMk2, "Heavy Sniper Mark 2"),
                new WeaponEntry(WeaponHash.MarksmanRifle, "Marksman Rifle"),
                new WeaponEntry(WeaponHash.MarksmanRifleMk2, "Marksman Rifle Mark 2"),
                new WeaponEntry(WeaponHash.PrecisionRifle, "Precision Rifle")
            }),

            new WeaponCategory("Heavy Weapons", 50, true, new[]
            {
                new WeaponEntry(WeaponHash.RPG, "R P G"),
                new WeaponEntry(WeaponHash.GrenadeLauncher, "Grenade Launcher"),
                new WeaponEntry(WeaponHash.GrenadeLauncherSmoke, "Smoke Grenade Launcher"),
                new WeaponEntry(WeaponHash.CompactGrenadeLauncher, "Compact Grenade Launcher"),
                new WeaponEntry(WeaponHash.Firework, "Firework Launcher"),
                new WeaponEntry(WeaponHash.Minigun, "Minigun"),
                new WeaponEntry(WeaponHash.Widowmaker, "Widowmaker"),
                new WeaponEntry(WeaponHash.Railgun, "Railgun"),
                new WeaponEntry(WeaponHash.RailgunXmas3, "Railgun, festive version"),
                new WeaponEntry(WeaponHash.HomingLauncher, "Homing Launcher"),
                new WeaponEntry(WeaponHash.CompactEMPLauncher, "Compact E M P Launcher"),
                new WeaponEntry(WeaponHash.SnowballLauncher, "Snowball Launcher")
            }),

            new WeaponCategory("Throwables", 25, true, new[]
            {
                new WeaponEntry(WeaponHash.Grenade, "Grenade"),
                new WeaponEntry(WeaponHash.StickyBomb, "Sticky Bomb"),
                new WeaponEntry(WeaponHash.ProximityMine, "Proximity Mine"),
                new WeaponEntry(WeaponHash.PipeBomb, "Pipe Bomb"),
                new WeaponEntry(WeaponHash.Molotov, "Molotov Cocktail"),
                new WeaponEntry(WeaponHash.SmokeGrenade, "Tear Gas"),
                new WeaponEntry(WeaponHash.BZGas, "B Z Gas"),
                new WeaponEntry(WeaponHash.Flare, "Flare"),
                new WeaponEntry(WeaponHash.Ball, "Baseball"),
                new WeaponEntry(WeaponHash.Snowball, "Snowball"),
                new WeaponEntry(WeaponHash.AcidPackage, "Acid Package")
            }),

            // Mission props and gadgets - valid weapons, but kept out of "give all"
            new WeaponCategory("Utility and Special", 25, false, new[]
            {
                new WeaponEntry(WeaponHash.Parachute, "Parachute", true),
                new WeaponEntry(WeaponHash.PetrolCan, "Jerry Can"),
                new WeaponEntry(WeaponHash.HazardousJerryCan, "Acid Jerry Can"),
                new WeaponEntry(WeaponHash.FertilizerCan, "Fertilizer Can"),
                new WeaponEntry(WeaponHash.FireExtinguisher, "Fire Extinguisher"),
                new WeaponEntry(WeaponHash.NightVision, "Night Vision Goggles", true),
                new WeaponEntry(WeaponHash.MetalDetector, "Metal Detector", true),
                new WeaponEntry(WeaponHash.HackingDevice, "Hacking Device", true)
            })
        };

        #endregion

        #region Fields

        // Catalog filtered to what the installed game build actually supports.
        // Built once, the first time the menu is used with a live player ped.
        private readonly List<WeaponCategory> _categories = new List<WeaponCategory>();
        private bool _catalogBuilt;

        // "Press again to confirm" arming for the destructive bulk actions
        private long _confirmArmedTick;
        private int _confirmArmedItem = -1;

        // Last time this menu changed which weapon is in the player's hands
        private long _selfChangeTick;

        // Name lookup shared with the main script's weapon-change announcement
        private static Dictionary<WeaponHash, string> _nameLookup;

        #endregion

        #region Construction

        public WeaponSelectMenu(AudioManager audio) : base(audio)
        {
        }

        #endregion

        #region Catalog Build

        /// <summary>
        /// The player's weapon collection, or null when the player is unavailable.
        /// </summary>
        private static WeaponCollection PlayerWeapons
        {
            get
            {
                try
                {
                    Ped player = Game.Player?.Character;
                    if (player == null || !player.Exists())
                        return null;
                    return player.Weapons;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Filter the static catalog down to weapons this game build knows about.
        /// Runs once; the set of valid weapons cannot change while the game runs.
        /// </summary>
        private void EnsureCatalogBuilt()
        {
            if (_catalogBuilt)
                return;

            WeaponCollection weapons = PlayerWeapons;
            if (weapons == null)
                return;  // Try again on the next access - the player is not ready yet

            try
            {
                foreach (WeaponCategory category in Catalog)
                {
                    var valid = new List<WeaponEntry>();
                    foreach (WeaponEntry entry in category.Weapons)
                    {
                        if (IsWeaponSupported(weapons, entry.Hash))
                            valid.Add(entry);
                    }

                    if (valid.Count > 0)
                    {
                        _categories.Add(new WeaponCategory(
                            category.Name, category.GiveAmmo, category.InGiveAll, valid.ToArray()));
                    }
                }

                if (_categories.Count == 0)
                {
                    // The validity check rejected everything - trust the catalog
                    // instead of leaving the player with a dead menu
                    Logger.Warning("WeaponSelectMenu: no weapons passed validation, using full catalog");
                    _categories.AddRange(Catalog);
                }

                _catalogBuilt = true;
                Logger.Debug($"WeaponSelectMenu: {_categories.Count} categories available");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponSelectMenu.EnsureCatalogBuilt");

                // Fall back to the full catalog rather than showing an empty menu
                _categories.Clear();
                _categories.AddRange(Catalog);
                _catalogBuilt = true;
            }
        }

        /// <summary>
        /// Whether the installed build recognizes this weapon. Fails open so a
        /// broken validity check never hides the whole category.
        /// </summary>
        private static bool IsWeaponSupported(WeaponCollection weapons, WeaponHash hash)
        {
            try { return weapons.IsWeaponValid(hash); }
            catch { return true; }
        }

        #endregion

        #region Top Level - categories and bulk actions

        protected override int ItemCount
        {
            get
            {
                EnsureCatalogBuilt();
                return _categories.Count > 0 ? _categories.Count + BULK_ITEM_COUNT : 0;
            }
        }

        protected override string EmptyMenuText => "Weapon list not available yet";

        protected override string GetItemText(int index)
        {
            if (index < _categories.Count)
            {
                WeaponCategory category = _categories[index];
                int owned = CountOwned(category);
                return $"{category.Name}, {category.Weapons.Length} weapons, {owned} owned";
            }

            int bulkItem = index - _categories.Count;
            switch (bulkItem)
            {
                case ITEM_GIVE_ALL:
                    return "Give all weapons";
                case ITEM_REFILL_AMMO:
                    return "Refill ammo on all weapons";
                case ITEM_REMOVE_ALL:
                    return IsConfirmArmed(ITEM_REMOVE_ALL)
                        ? "Remove all weapons, press again to confirm"
                        : "Remove all weapons";
                case ITEM_DROP_CURRENT:
                    return IsConfirmArmed(ITEM_DROP_CURRENT)
                        ? "Drop current weapon, press again to confirm"
                        : $"Drop current weapon, {DescribeEquippedWeapon()}";
                case ITEM_GO_UNARMED:
                    return "Go unarmed";
                default:
                    return EmptyMenuText;
            }
        }

        protected override void OnItemActivated(int index)
        {
            if (index < _categories.Count)
            {
                WeaponCategory category = _categories[index];
                EnterSubmenu();
                Speak($"{category.Name}, {category.Weapons.Length} weapons. {GetSubmenuItemText(0)}");
                return;
            }

            int bulkItem = index - _categories.Count;
            switch (bulkItem)
            {
                case ITEM_GIVE_ALL:
                    GiveAllWeapons();
                    break;
                case ITEM_REFILL_AMMO:
                    RefillAllAmmo();
                    break;
                case ITEM_REMOVE_ALL:
                    if (RequireConfirm(ITEM_REMOVE_ALL, "Press again to remove all weapons"))
                        RemoveAllWeapons();
                    break;
                case ITEM_DROP_CURRENT:
                    if (RequireConfirm(ITEM_DROP_CURRENT, "Press again to drop the current weapon"))
                        DropCurrentWeapon();
                    break;
                case ITEM_GO_UNARMED:
                    GoUnarmed();
                    break;
            }
        }

        public override string GetMenuName()
        {
            EnsureCatalogBuilt();

            WeaponCategory active = ActiveCategory;
            if (InSubmenu && active != null)
                return $"Weapons, {active.Name}";

            return "Weapons";
        }

        #endregion

        #region Submenu - weapons in the selected category

        /// <summary>
        /// The category whose weapon list the submenu is showing. The top-level
        /// selection stays put while the submenu is open, so it identifies the category.
        /// </summary>
        private WeaponCategory ActiveCategory
        {
            get
            {
                if (SelectedIndex < 0 || SelectedIndex >= _categories.Count)
                    return null;
                return _categories[SelectedIndex];
            }
        }

        protected override int SubmenuItemCount
        {
            get
            {
                WeaponCategory category = ActiveCategory;
                return category?.Weapons.Length ?? 0;
            }
        }

        protected override string GetSubmenuItemText(int index)
        {
            WeaponCategory category = ActiveCategory;
            if (category == null || index < 0 || index >= category.Weapons.Length)
                return EmptyMenuText;

            WeaponEntry entry = category.Weapons[index];
            string position = $"{index + 1} of {category.Weapons.Length}: {entry.Name}";

            if (!HasWeapon(entry.Hash))
                return $"{position}, not owned, press to add";

            string ammo = DescribeAmmo(category, entry);
            return IsEquipped(entry.Hash)
                ? $"{position}, equipped{ammo}, press to remove"
                : $"{position}, owned{ammo}, press to equip";
        }

        protected override void OnSubmenuItemActivated(int index)
        {
            WeaponCategory category = ActiveCategory;
            if (category == null || index < 0 || index >= category.Weapons.Length)
                return;

            WeaponEntry entry = category.Weapons[index];

            if (!HasWeapon(entry.Hash))
                GiveWeapon(category, entry, true);
            else if (!IsEquipped(entry.Hash))
                EquipWeapon(entry);
            else
                RemoveWeapon(entry);
        }

        #endregion

        #region Weapon Actions

        /// <summary>
        /// Add a weapon with a full ammo load. The give call seeds the category
        /// default, then the real maximum is applied when the game reports one.
        /// </summary>
        private void GiveWeapon(WeaponCategory category, WeaponEntry entry, bool equip, bool announce = true)
        {
            WeaponCollection weapons = PlayerWeapons;
            if (weapons == null)
            {
                if (announce) Speak("Player not available");
                return;
            }

            try
            {
                int ammo = category.GiveAmmo > 0 ? category.GiveAmmo : 1;
                Weapon given = weapons.Give(entry.Hash, ammo, equip, true);
                FillAmmo(category, entry, given);

                if (equip)
                    MarkSelfChange();

                if (!announce)
                    return;

                if (given == null && !HasWeapon(entry.Hash))
                {
                    Speak($"Could not add {entry.Name}");
                    return;
                }

                Speak($"{entry.Name} added{DescribeAmmo(category, entry)}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponSelectMenu.GiveWeapon");
                if (announce) Speak($"Failed to add {entry.Name}");
            }
        }

        /// <summary>
        /// Put an already owned weapon in the player's hands.
        /// </summary>
        private void EquipWeapon(WeaponEntry entry)
        {
            WeaponCollection weapons = PlayerWeapons;
            if (weapons == null)
            {
                Speak("Player not available");
                return;
            }

            try
            {
                bool selected = weapons.Select(entry.Hash, true);
                MarkSelfChange();
                Speak(selected ? $"{entry.Name} equipped" : $"Could not equip {entry.Name}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponSelectMenu.EquipWeapon");
                Speak($"Failed to equip {entry.Name}");
            }
        }

        /// <summary>
        /// Take a weapon out of the inventory. Only reachable for the equipped
        /// weapon, so the player has always confirmed the choice by holding it.
        /// </summary>
        private void RemoveWeapon(WeaponEntry entry)
        {
            WeaponCollection weapons = PlayerWeapons;
            if (weapons == null)
            {
                Speak("Player not available");
                return;
            }

            try
            {
                weapons.Remove(entry.Hash);

                // Removing the equipped weapon drops the player back to fists
                MarkSelfChange();

                Speak(HasWeapon(entry.Hash) ? $"Could not remove {entry.Name}" : $"{entry.Name} removed");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponSelectMenu.RemoveWeapon");
                Speak($"Failed to remove {entry.Name}");
            }
        }

        #endregion

        #region Bulk Actions

        /// <summary>
        /// Give every combat weapon the build supports, skipping the utility props.
        /// </summary>
        private void GiveAllWeapons()
        {
            EnsureCatalogBuilt();

            WeaponCollection weapons = PlayerWeapons;
            if (weapons == null)
            {
                Speak("Player not available");
                return;
            }

            int added = 0;
            foreach (WeaponCategory category in _categories)
            {
                if (!category.InGiveAll)
                    continue;

                foreach (WeaponEntry entry in category.Weapons)
                {
                    if (HasWeapon(entry.Hash))
                        continue;

                    GiveWeapon(category, entry, false, false);
                    if (HasWeapon(entry.Hash))
                        added++;
                }
            }

            Speak(added > 0
                ? $"{added} weapons added, all with full ammo"
                : "You already have every weapon");
        }

        /// <summary>
        /// Top every owned weapon back up to its maximum ammo.
        /// </summary>
        private void RefillAllAmmo()
        {
            EnsureCatalogBuilt();

            if (PlayerWeapons == null)
            {
                Speak("Player not available");
                return;
            }

            int refilled = 0;
            foreach (WeaponCategory category in _categories)
            {
                if (category.GiveAmmo <= 0)
                    continue;  // Melee - nothing to refill

                foreach (WeaponEntry entry in category.Weapons)
                {
                    if (entry.NoAmmo || !HasWeapon(entry.Hash))
                        continue;

                    if (FillAmmo(category, entry, GetWeapon(entry.Hash)))
                        refilled++;
                }
            }

            Speak(refilled > 0
                ? $"Ammo refilled on {refilled} weapons"
                : "No weapons needed ammo");
        }

        /// <summary>
        /// Clear the whole inventory (two-press confirmed by the caller).
        /// </summary>
        private void RemoveAllWeapons()
        {
            WeaponCollection weapons = PlayerWeapons;
            if (weapons == null)
            {
                Speak("Player not available");
                return;
            }

            try
            {
                weapons.RemoveAll();
                MarkSelfChange();
                Speak("All weapons removed");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponSelectMenu.RemoveAllWeapons");
                Speak("Failed to remove weapons");
            }
        }

        /// <summary>
        /// Throw the equipped weapon on the ground (two-press confirmed by the caller).
        /// </summary>
        private void DropCurrentWeapon()
        {
            WeaponCollection weapons = PlayerWeapons;
            if (weapons == null)
            {
                Speak("Player not available");
                return;
            }

            try
            {
                Weapon current = weapons.Current;
                if (current == null || current.Hash == WeaponHash.Unarmed)
                {
                    Speak("You are not holding a weapon");
                    return;
                }

                string name = GetWeaponName(current.Hash);
                weapons.Drop();
                MarkSelfChange();
                Speak($"{name} dropped");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponSelectMenu.DropCurrentWeapon");
                Speak("Failed to drop weapon");
            }
        }

        /// <summary>
        /// Holster whatever is in hand and go back to fists.
        /// </summary>
        private void GoUnarmed()
        {
            WeaponCollection weapons = PlayerWeapons;
            if (weapons == null)
            {
                Speak("Player not available");
                return;
            }

            try
            {
                weapons.Select(WeaponHash.Unarmed, true);
                MarkSelfChange();
                Speak("Unarmed");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponSelectMenu.GoUnarmed");
                Speak("Failed to holster weapon");
            }
        }

        #endregion

        #region Confirmation

        /// <summary>
        /// Arm a destructive action on the first press and run it on the second,
        /// as long as the second press lands inside the confirmation window.
        /// Returns true when the caller should go ahead.
        /// </summary>
        private bool RequireConfirm(int bulkItem, string prompt)
        {
            if (IsConfirmArmed(bulkItem))
            {
                ClearConfirm();
                return true;
            }

            _confirmArmedItem = bulkItem;
            _confirmArmedTick = Game.GameTime;
            Speak(prompt);
            return false;
        }

        private bool IsConfirmArmed(int bulkItem)
        {
            return _confirmArmedItem == bulkItem &&
                   _confirmArmedTick > 0 &&
                   Game.GameTime - _confirmArmedTick <= CONFIRM_WINDOW_MS;
        }

        private void ClearConfirm()
        {
            _confirmArmedItem = -1;
            _confirmArmedTick = 0;
        }

        /// <summary>Moving off an armed item cancels the pending confirmation.</summary>
        protected override void OnNavigated()
        {
            ClearConfirm();
        }

        protected override void OnSubmenuExited()
        {
            ClearConfirm();
        }

        #endregion

        #region Weapon State Helpers

        private static bool HasWeapon(WeaponHash hash)
        {
            try
            {
                WeaponCollection weapons = PlayerWeapons;
                return weapons != null && weapons.HasWeapon(hash);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsEquipped(WeaponHash hash)
        {
            try
            {
                Weapon current = PlayerWeapons?.Current;
                return current != null && current.Hash == hash;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Owned weapon wrapper, or null when the player does not have it.</summary>
        private static Weapon GetWeapon(WeaponHash hash)
        {
            try { return PlayerWeapons?[hash]; }
            catch { return null; }
        }

        /// <summary>
        /// Set a weapon to its maximum ammo. Returns true when ammo was applied.
        /// </summary>
        private static bool FillAmmo(WeaponCategory category, WeaponEntry entry, Weapon weapon)
        {
            if (weapon == null || entry.NoAmmo || category.GiveAmmo <= 0)
                return false;

            try
            {
                int max = weapon.MaxAmmo;
                if (max <= 0)
                    return false;

                weapon.Ammo = max;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WeaponSelectMenu.FillAmmo");
                return false;
            }
        }

        /// <summary>
        /// Spoken ammo suffix (", 120 rounds") for weapons that use ammo,
        /// or an empty string for melee weapons and gadgets.
        /// </summary>
        private static string DescribeAmmo(WeaponCategory category, WeaponEntry entry)
        {
            if (category.GiveAmmo <= 0 || entry.NoAmmo)
                return "";

            try
            {
                Weapon weapon = GetWeapon(entry.Hash);
                if (weapon == null)
                    return "";

                int ammo = weapon.Ammo;
                return ammo == 1 ? ", 1 round" : $", {ammo} rounds";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>Name of the weapon in the player's hands, for the drop item.</summary>
        private static string DescribeEquippedWeapon()
        {
            try
            {
                Weapon current = PlayerWeapons?.Current;
                if (current == null || current.Hash == WeaponHash.Unarmed)
                    return "nothing equipped";

                return GetWeaponName(current.Hash);
            }
            catch
            {
                return "nothing equipped";
            }
        }

        private int CountOwned(WeaponCategory category)
        {
            int owned = 0;
            foreach (WeaponEntry entry in category.Weapons)
            {
                if (HasWeapon(entry.Hash))
                    owned++;
            }
            return owned;
        }

        #endregion

        #region Weapon Change Suppression

        /// <summary>Record that this menu just changed which weapon is in hand.</summary>
        private void MarkSelfChange()
        {
            _selfChangeTick = Game.GameTime;
        }

        /// <summary>
        /// Whether the main script should skip its automatic weapon-change
        /// announcement because this menu already spoke a confirmation.
        /// One-shot: the flag is consumed by the first change it explains.
        /// </summary>
        public bool ConsumeWeaponChangeSuppression(long currentTick)
        {
            if (_selfChangeTick == 0)
                return false;

            bool withinWindow = currentTick - _selfChangeTick <= SELF_CHANGE_SUPPRESS_MS;
            _selfChangeTick = 0;
            return withinWindow;
        }

        #endregion

        #region Public Name Lookup

        /// <summary>
        /// Speech-friendly name for a weapon hash, shared with the main script's
        /// weapon-change announcement so it says "Combat Pistol" rather than the
        /// run-together enum name. Falls back to the enum name for hashes the
        /// catalog does not cover (pickups, vehicle weapons, future DLC).
        /// </summary>
        public static string GetWeaponName(WeaponHash hash)
        {
            if (hash == WeaponHash.Unarmed)
                return "Fists";

            if (_nameLookup == null)
            {
                var lookup = new Dictionary<WeaponHash, string>();
                foreach (WeaponCategory category in Catalog)
                {
                    foreach (WeaponEntry entry in category.Weapons)
                        lookup[entry.Hash] = entry.Name;
                }
                _nameLookup = lookup;
            }

            string name;
            return _nameLookup.TryGetValue(hash, out name) ? name : hash.ToString();
        }

        #endregion
    }
}
