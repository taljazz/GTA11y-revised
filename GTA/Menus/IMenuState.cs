namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Interface for menu state pattern
    /// Each menu implements this to handle navigation and selection
    /// Supports hierarchical submenus
    /// </summary>
    public interface IMenuState
    {
        /// <summary>
        /// Navigate to previous item
        /// </summary>
        void NavigatePrevious(bool fastScroll = false);

        /// <summary>
        /// Navigate to next item
        /// </summary>
        void NavigateNext(bool fastScroll = false);

        /// <summary>
        /// Get current menu item text for speech
        /// </summary>
        string GetCurrentItemText();

        /// <summary>
        /// Execute the selected menu item
        /// </summary>
        void ExecuteSelection();

        /// <summary>
        /// Get the menu name
        /// </summary>
        string GetMenuName();

        /// <summary>
        /// Returns true if menu is currently showing a submenu
        /// </summary>
        bool HasActiveSubmenu { get; }

        /// <summary>
        /// Exit current submenu and return to parent level
        /// </summary>
        void ExitSubmenu();
    }
}
