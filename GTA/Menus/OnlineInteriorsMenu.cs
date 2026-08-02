namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Loads and unloads GTA Online interior map data in story mode.
    /// One item per interior group; selecting toggles it. The spoken result
    /// reports what actually loaded rather than what was asked for, because a
    /// wrong IPL name fails silently.
    /// </summary>
    public class OnlineInteriorsMenu : MenuBase
    {
        #region Fields

        private readonly InteriorManager _interiors;

        #endregion

        #region Construction

        public OnlineInteriorsMenu(InteriorManager interiors, AudioManager audio) : base(audio)
        {
            _interiors = interiors;
        }

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount => _interiors?.GroupCount ?? 0;

        protected override string EmptyMenuText => "No online interiors available";

        protected override string GetItemText(int index)
        {
            return _interiors.GetGroupStatus(index);
        }

        protected override void OnItemActivated(int index)
        {
            // The manager speaks the outcome itself, including the verified count
            _interiors.Toggle(index);
        }

        public override string GetMenuName()
        {
            return "Online Interiors";
        }

        #endregion
    }
}
