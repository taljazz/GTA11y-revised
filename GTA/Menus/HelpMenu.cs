namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Read-only menu listing every keyboard command.
    /// Layout-aware: the key names come from the HotkeyMapper, so the help
    /// text always matches the active layout (numpad or letter keys) and
    /// rebuilds itself automatically when the layout changes.
    /// </summary>
    public class HelpMenu : MenuBase
    {
        #region Fields

        private readonly HotkeyMapper _hotkeys;

        // Cached items, rebuilt when the layout changes
        private string[] _items;
        private HotkeyLayout _itemsLayout;

        #endregion

        #region Construction

        public HelpMenu(HotkeyMapper hotkeys, AudioManager audio) : base(audio)
        {
            _hotkeys = hotkeys;
            RebuildItems();
        }

        #endregion

        #region Item Building

        /// <summary>
        /// Build the help text for the active layout, naming each key through
        /// the HotkeyMapper so both layouts read naturally.
        /// </summary>
        private void RebuildItems()
        {
            _itemsLayout = _hotkeys.CurrentLayout;

            string location = _hotkeys.GetKeyLabel(AccessibilityCommand.LocationInfo);
            string prevItem = _hotkeys.GetKeyLabel(AccessibilityCommand.MenuPreviousItem);
            string select = _hotkeys.GetKeyLabel(AccessibilityCommand.MenuSelect);
            string nextItem = _hotkeys.GetKeyLabel(AccessibilityCommand.MenuNextItem);
            string vehicles = _hotkeys.GetKeyLabel(AccessibilityCommand.ScanVehicles);
            string doors = _hotkeys.GetKeyLabel(AccessibilityCommand.ScanDoors);
            string peds = _hotkeys.GetKeyLabel(AccessibilityCommand.ScanPedestrians);
            string prevMenu = _hotkeys.GetKeyLabel(AccessibilityCommand.MenuPrevious);
            string objects = _hotkeys.GetKeyLabel(AccessibilityCommand.ScanObjects);
            string nextMenu = _hotkeys.GetKeyLabel(AccessibilityCommand.MenuNext);
            string back = _hotkeys.GetKeyLabel(AccessibilityCommand.Back);

            _items = new string[]
            {
                $"Active layout: {_hotkeys.LayoutName}. Press F9 to switch between Numpad and Letter key layouts",
                $"{location}: Current location. Ctrl plus {location}: Toggle pedestrian navigation to waypoint",
                $"{prevItem}: Previous menu item. Hold Ctrl: Fast scroll",
                $"{select}: Select menu item. Ctrl plus {select}: Toggle accessibility keys",
                $"{nextItem}: Next menu item. Hold Ctrl: Fast scroll",
                $"{prevMenu}: Previous menu. Ctrl plus {prevMenu}: Announce nearby points of interest",
                $"{nextMenu}: Next menu. Ctrl plus {nextMenu}: Mission objective location",
                $"{back}: Back, or show heading. Ctrl plus {back}: Time with minutes",
                $"{vehicles}: Scan nearby vehicles. Ctrl plus {vehicles}: Health and armor status",
                $"{doors}: Scan nearby doors. Ctrl plus {doors}: Repeat last announcement",
                $"{peds}: Scan nearby pedestrians. Ctrl plus {peds}: Nearest enemy",
                $"{objects}: Scan nearby objects. Ctrl plus {objects}: Ammo count"
            };
        }

        /// <summary>
        /// Rebuild the cached items if the layout changed since they were built.
        /// </summary>
        private void EnsureItemsCurrent()
        {
            if (_itemsLayout != _hotkeys.CurrentLayout)
            {
                RebuildItems();
            }
        }

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount
        {
            get
            {
                EnsureItemsCurrent();
                return _items.Length;
            }
        }

        protected override string GetItemText(int index)
        {
            EnsureItemsCurrent();
            return $"{index + 1} of {_items.Length}: {_items[index]}";
        }

        protected override void OnItemActivated(int index)
        {
            // Items are informational - no action needed
        }

        public override string GetMenuName()
        {
            return "Help";
        }

        #endregion
    }
}
