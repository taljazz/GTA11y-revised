using DavyKager;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Abstract base class for all menus. Implements IMenuState once, providing
    /// shared wraparound navigation, bounds checking, and speech output, so each
    /// concrete menu only supplies its item list and activation behavior through
    /// the abstract members below.
    ///
    /// Inheritance layout:
    ///   MenuBase                - flat menus (Help, Settings, Functions, ...)
    ///   HierarchicalMenuBase    - menus with a two-level structure (categories then items)
    /// </summary>
    public abstract class MenuBase : IMenuState
    {
        #region Fields

        private readonly AudioManager _audio;   // may be null - Speak falls back to Tolk
        private int _selectedIndex;

        #endregion

        #region Construction

        /// <summary>Create a menu with no audio manager (speech falls back to Tolk directly).</summary>
        protected MenuBase()
        {
        }

        /// <summary>Create a menu that speaks through the shared AudioManager (recommended - keeps repeat-last and Tolk reconnection working).</summary>
        protected MenuBase(AudioManager audio)
        {
            _audio = audio;
        }

        #endregion

        #region Abstract Surface - every menu must provide these

        /// <summary>Number of items at the top level of this menu.</summary>
        protected abstract int ItemCount { get; }

        /// <summary>Speech text for the item at the given (already validated) index.</summary>
        protected abstract string GetItemText(int index);

        /// <summary>Perform the action for the item at the given (already validated) index.</summary>
        protected abstract void OnItemActivated(int index);

        /// <summary>The spoken name of this menu.</summary>
        public abstract string GetMenuName();

        #endregion

        #region Virtual Surface - menus may override these

        /// <summary>How many items a fast scroll (Ctrl held) jumps at once.</summary>
        protected virtual int FastScrollStep => 5;

        /// <summary>Text spoken when the menu has no items.</summary>
        protected virtual string EmptyMenuText => "(empty)";

        /// <summary>Called after the selection index changes via navigation.</summary>
        protected virtual void OnNavigated()
        {
        }

        /// <summary>Whether a submenu is currently active. Flat menus have none.</summary>
        public virtual bool HasActiveSubmenu => false;

        /// <summary>Exit the active submenu. Flat menus do nothing.</summary>
        public virtual void ExitSubmenu()
        {
        }

        #endregion

        #region Selection State

        /// <summary>The currently selected top-level index.</summary>
        protected int SelectedIndex
        {
            get => _selectedIndex;
            set => _selectedIndex = value;
        }

        /// <summary>Reset the selection to the first item.</summary>
        protected void ResetSelection()
        {
            _selectedIndex = 0;
        }

        /// <summary>Wrap an index into [0, count) handling negative values.</summary>
        protected static int Wrap(int index, int count)
        {
            if (count <= 0) return 0;
            return ((index % count) + count) % count;
        }

        #endregion

        #region Navigation - shared implementation

        public virtual void NavigatePrevious(bool fastScroll = false)
        {
            int count = ItemCount;
            if (count <= 0) return;

            int step = fastScroll ? FastScrollStep : 1;
            _selectedIndex = Wrap(_selectedIndex - step, count);
            OnNavigated();
        }

        public virtual void NavigateNext(bool fastScroll = false)
        {
            int count = ItemCount;
            if (count <= 0) return;

            int step = fastScroll ? FastScrollStep : 1;
            _selectedIndex = Wrap(_selectedIndex + step, count);
            OnNavigated();
        }

        public virtual string GetCurrentItemText()
        {
            int count = ItemCount;
            if (count <= 0)
                return EmptyMenuText;

            // Defensive: clamp index in case the item list shrank
            if (_selectedIndex < 0 || _selectedIndex >= count)
                _selectedIndex = 0;

            return GetItemText(_selectedIndex);
        }

        public virtual void ExecuteSelection()
        {
            int count = ItemCount;
            if (count <= 0) return;

            if (_selectedIndex < 0 || _selectedIndex >= count)
                _selectedIndex = 0;

            OnItemActivated(_selectedIndex);
        }

        #endregion

        #region Speech

        /// <summary>
        /// Speak through the shared AudioManager when available (keeps Ctrl+NumPad5
        /// repeat-last and Tolk auto-reconnection working), otherwise fall back to Tolk.
        /// </summary>
        protected void Speak(string text, bool interrupt = false)
        {
            if (_audio != null)
                _audio.Speak(text, interrupt);
            else
                Tolk.Speak(text, interrupt);
        }

        #endregion
    }

    /// <summary>
    /// Abstract base class for menus with a two-level structure: a top level
    /// (usually categories or actions) and a submenu (usually the items within
    /// the chosen category). Routes navigation to whichever level is active and
    /// tracks the submenu state that every hierarchical menu used to duplicate.
    /// </summary>
    public abstract class HierarchicalMenuBase : MenuBase
    {
        #region Fields

        private bool _inSubmenu;
        private int _submenuIndex;

        #endregion

        #region Construction

        protected HierarchicalMenuBase()
        {
        }

        protected HierarchicalMenuBase(AudioManager audio) : base(audio)
        {
        }

        #endregion

        #region Abstract Surface - submenu contract

        /// <summary>Number of items in the currently active submenu.</summary>
        protected abstract int SubmenuItemCount { get; }

        /// <summary>Speech text for the submenu item at the given index.</summary>
        protected abstract string GetSubmenuItemText(int index);

        /// <summary>Perform the action for the submenu item at the given index.</summary>
        protected abstract void OnSubmenuItemActivated(int index);

        #endregion

        #region Virtual Surface

        /// <summary>
        /// Lowest valid submenu index. Usually 0, but menus like the vehicle mod
        /// menu use -1 to represent a "Stock / all off" pseudo-item.
        /// </summary>
        protected virtual int SubmenuMinIndex => 0;

        /// <summary>Fast scroll step inside the submenu (defaults to the top-level step).</summary>
        protected virtual int SubmenuFastScrollStep => FastScrollStep;

        /// <summary>Called when the submenu is entered.</summary>
        protected virtual void OnSubmenuEntered()
        {
        }

        /// <summary>Called when the submenu is exited.</summary>
        protected virtual void OnSubmenuExited()
        {
        }

        #endregion

        #region Submenu State

        /// <summary>Whether the submenu level is currently active.</summary>
        protected bool InSubmenu => _inSubmenu;

        /// <summary>The currently selected submenu index.</summary>
        protected int SubmenuIndex
        {
            get => _submenuIndex;
            set => _submenuIndex = value;
        }

        /// <summary>Activate the submenu, positioned at the given index.</summary>
        protected void EnterSubmenu(int initialIndex = 0)
        {
            _inSubmenu = true;
            _submenuIndex = initialIndex;
            OnSubmenuEntered();
        }

        public override bool HasActiveSubmenu => _inSubmenu;

        public override void ExitSubmenu()
        {
            if (!_inSubmenu) return;

            _inSubmenu = false;
            _submenuIndex = 0;
            OnSubmenuExited();
        }

        #endregion

        #region Navigation Routing

        public override void NavigatePrevious(bool fastScroll = false)
        {
            if (!_inSubmenu)
            {
                base.NavigatePrevious(fastScroll);
                return;
            }

            int count = SubmenuItemCount;
            if (count <= 0) return;

            int step = fastScroll ? SubmenuFastScrollStep : 1;
            int min = SubmenuMinIndex;
            int max = min + count - 1;

            _submenuIndex -= step;
            if (_submenuIndex < min)
                _submenuIndex = max;

            OnNavigated();
        }

        public override void NavigateNext(bool fastScroll = false)
        {
            if (!_inSubmenu)
            {
                base.NavigateNext(fastScroll);
                return;
            }

            int count = SubmenuItemCount;
            if (count <= 0) return;

            int step = fastScroll ? SubmenuFastScrollStep : 1;
            int min = SubmenuMinIndex;
            int max = min + count - 1;

            _submenuIndex += step;
            if (_submenuIndex > max)
                _submenuIndex = min;

            OnNavigated();
        }

        public override string GetCurrentItemText()
        {
            if (!_inSubmenu)
                return base.GetCurrentItemText();

            int count = SubmenuItemCount;
            if (count <= 0)
                return EmptyMenuText;

            int min = SubmenuMinIndex;
            int max = min + count - 1;
            if (_submenuIndex < min || _submenuIndex > max)
                _submenuIndex = min;

            return GetSubmenuItemText(_submenuIndex);
        }

        public override void ExecuteSelection()
        {
            if (!_inSubmenu)
            {
                base.ExecuteSelection();
                return;
            }

            int count = SubmenuItemCount;
            if (count <= 0) return;

            int min = SubmenuMinIndex;
            int max = min + count - 1;
            if (_submenuIndex < min || _submenuIndex > max)
                _submenuIndex = min;

            OnSubmenuItemActivated(_submenuIndex);
        }

        #endregion
    }
}
