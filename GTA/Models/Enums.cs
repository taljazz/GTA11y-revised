namespace GrandTheftAccessibility
{
    // Strongly-typed enums replacing the int constant families in Constants.cs.
    // Underlying values match the legacy constants exactly, so casting an enum
    // to int (or an int from settings/JSON to the enum) is always safe.

    #region Altitude and Aircraft

    /// <summary>
    /// Altitude indicator behavior, cycled via the "altitudeMode" setting.
    /// </summary>
    public enum AltitudeMode
    {
        Off = 0,
        NormalTone = 1,
        AircraftSpoken = 2
    }

    /// <summary>
    /// Aircraft classification used to select attitude-indicator sensitivity.
    /// Each value has a matching AircraftProfile subclass.
    /// </summary>
    public enum AircraftType
    {
        FixedWing = 0,
        Helicopter = 1,
        Blimp = 2,
        VtolHover = 3,
        VtolPlane = 4
    }

    #endregion

    #region Driving

    /// <summary>
    /// AutoDrive driving personality. Indexes the style value/ability/aggressiveness
    /// arrays in Constants.
    /// </summary>
    public enum DrivingStyleMode
    {
        Cautious = 0,
        Normal = 1,
        Aggressive = 2,
        Reckless = 3
    }

    /// <summary>
    /// Road classification derived from vehicle node flags, density, and lane count.
    /// </summary>
    public enum RoadType
    {
        Unknown = 0,
        Highway = 1,
        CityStreet = 2,
        Suburban = 3,
        Rural = 4,
        DirtTrail = 5,
        Tunnel = 6
    }

    /// <summary>
    /// Target road type for the AutoDrive road-seeking feature.
    /// </summary>
    public enum RoadSeekMode
    {
        Any = 0,
        Highway = 1,
        City = 2,
        Suburban = 3,
        Rural = 4,
        Dirt = 5
    }

    /// <summary>
    /// Structure the vehicle is currently inside of or on top of.
    /// </summary>
    public enum StructureType
    {
        None = 0,
        Tunnel = 1,
        Bridge = 2,
        Overpass = 3,
        Underpass = 4
    }

    /// <summary>
    /// AutoDrive pause state machine.
    /// </summary>
    public enum PauseState
    {
        None = 0,
        Paused = 1,
        Resuming = 2
    }

    /// <summary>
    /// Announcement priority. Lower value = more important (Critical interrupts
    /// everything; Low waits the longest between repeats).
    /// </summary>
    public enum AnnouncementPriority
    {
        Critical = 0,
        High = 1,
        Medium = 2,
        Low = 3
    }

    #endregion

    #region Input

    /// <summary>
    /// Which physical key set drives the mod, toggled with F9 and persisted
    /// via the "hotkeyLayout" setting. Numpad is the classic layout; Letters
    /// serves keyboards without a numpad (laptops, tenkeyless boards).
    /// </summary>
    public enum HotkeyLayout
    {
        Numpad = 0,
        Letters = 1
    }

    /// <summary>
    /// Every accessibility action a hotkey can trigger, independent of which
    /// physical key is bound to it. Values 0-10 double as indices into the
    /// key-repeat state array, so keep them contiguous from zero.
    /// </summary>
    public enum AccessibilityCommand
    {
        None = -1,
        LocationInfo = 0,       // NumPad 0 / Y
        MenuPreviousItem = 1,   // NumPad 1 / J
        MenuSelect = 2,         // NumPad 2 / K
        MenuNextItem = 3,       // NumPad 3 / L
        ScanVehicles = 4,       // NumPad 4 / Left bracket
        ScanDoors = 5,          // NumPad 5 / Right bracket
        ScanPedestrians = 6,    // NumPad 6 / Apostrophe
        MenuPrevious = 7,       // NumPad 7 / U
        ScanObjects = 8,        // NumPad 8 / I
        MenuNext = 9,           // NumPad 9 / O
        Back = 10               // NumPad Decimal / Semicolon
    }

    #endregion

    #region Turret Crew

    /// <summary>
    /// Which turret crew events are announced, cycled via the
    /// "turretCrewAnnouncements" setting.
    /// </summary>
    public enum TurretAnnounceMode
    {
        Off = 0,
        FiringOnly = 1,
        ApproachingOnly = 2,
        Both = 3
    }

    /// <summary>
    /// Combat state of an individual turret crew member.
    /// </summary>
    public enum TurretCrewState
    {
        Idle = 0,
        Aiming = 1,
        Fighting = 2
    }

    #endregion
}
