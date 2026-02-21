namespace GrandTheftAccessibility.Menus
{
    public class HelpMenu : IMenuState
    {
        private readonly string[] _items = new string[]
        {
            "NumPad 0: Current location. Ctrl+NumPad 0: Heading",
            "NumPad 1: Previous menu item. Ctrl: Fast scroll",
            "NumPad 2: Select menu item. Ctrl+NumPad 2: Toggle accessibility keys",
            "NumPad 3: Next menu item. Ctrl: Fast scroll",
            "NumPad 4: Scan nearby vehicles",
            "NumPad 5: Scan nearby doors",
            "NumPad 6: Scan nearby pedestrians",
            "NumPad 7: Previous menu",
            "NumPad 8: Scan nearby objects",
            "NumPad 9: Next menu",
            "Decimal: Back, or show heading. Ctrl+Decimal: Time with minutes",
            "Ctrl+NumPad 5: Repeat last announcement",
            "Ctrl+NumPad 4: Health and armor status",
            "Ctrl+NumPad 6: Nearest enemy",
            "Ctrl+NumPad 8: Ammo count",
            "Ctrl+NumPad 7: Announce nearby points of interest",
            "Ctrl+NumPad 9: Mission objective location"
        };

        private int _currentIndex;

        public void NavigatePrevious(bool fastScroll = false)
        {
            int step = fastScroll ? 5 : 1;
            _currentIndex -= step;
            if (_currentIndex < 0)
                _currentIndex = (((_currentIndex % _items.Length) + _items.Length) % _items.Length);
        }

        public void NavigateNext(bool fastScroll = false)
        {
            int step = fastScroll ? 5 : 1;
            _currentIndex += step;
            if (_currentIndex >= _items.Length)
                _currentIndex = _currentIndex % _items.Length;
        }

        public string GetCurrentItemText()
        {
            return $"{_currentIndex + 1} of {_items.Length}: {_items[_currentIndex]}";
        }

        public void ExecuteSelection()
        {
            // Items are informational - no action needed
        }

        public string GetMenuName()
        {
            return "Help";
        }

        public bool HasActiveSubmenu => false;

        public void ExitSubmenu()
        {
            // No submenu
        }
    }
}
