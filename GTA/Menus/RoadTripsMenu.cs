using GTA;
using GTA.Math;
using GTA.Native;
using GrandTheftAccessibility.Data;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Curated drives along a single road: pick one, get teleported to its
    /// start facing the right way, and autodrive to the far end.
    ///
    /// This is the guaranteed way to stay on one road type. The wander-based
    /// hold in the AutoDrive menu can only notice a stray and turn back;
    /// here the start and end sit on the same road, so the game's own router
    /// keeps the car on it the whole way - there is nothing to correct.
    ///
    /// AutoDrive does NOT need to be running first. Be in the driver's seat,
    /// pick a drive, and this arranges the rest: it stops anything already
    /// driving, teleports and lines the car up, then starts the drive itself.
    /// </summary>
    public class RoadTripsMenu : MenuBase
    {
        #region Constants

        // The survey sits above the drives: it is what verifies them
        private const int ITEM_SURVEY = 0;
        private const int ITEM_SURVEY_RESULTS = 1;
        private const int FIXED_ITEM_COUNT = 2;

        #endregion

        #region Fields

        private readonly AutoDriveManager _autoDrive;
        private readonly RoadSurveyor _surveyor;

        // A drive waiting for the teleport to settle. Handing the vehicle AI a
        // cross-map route in the same frame the vehicle was warped several
        // kilometres crashed the game: the destination area has no collision,
        // no path nodes and no streamed terrain yet. This codebase already
        // carries "prevents crash" comments about issuing drive tasks in the
        // wrong frame - the same lesson, one frame later.
        private bool _hasPendingDrive;
        private Vector3 _pendingDestination;
        private string _pendingName;
        private long _pendingStartTick;

        #endregion

        #region Construction

        public RoadTripsMenu(AutoDriveManager autoDrive, RoadSurveyor surveyor, AudioManager audio)
            : base(audio)
        {
            _autoDrive = autoDrive;
            _surveyor = surveyor;
        }

        #endregion

        #region MenuBase Overrides

        protected override int ItemCount => FIXED_ITEM_COUNT + LocationData.RoadDrives.Length;

        protected override string GetItemText(int index)
        {
            if (index == ITEM_SURVEY)
            {
                return _surveyor.IsRunning
                    ? "Cancel the road survey"
                    : "Survey the map's roads: ask the game where every road is";
            }

            if (index == ITEM_SURVEY_RESULTS)
            {
                return _surveyor.HasSavedSurvey()
                    ? "What the survey found"
                    : "What the survey found: nothing surveyed yet";
            }

            int driveIndex = index - FIXED_ITEM_COUNT;
            if (driveIndex < 0 || driveIndex >= LocationData.RoadDrives.Length)
                return EmptyMenuText;

            RoadDrive drive = LocationData.RoadDrives[driveIndex];
            return $"{driveIndex + 1} of {LocationData.RoadDrives.Length}: " +
                   $"{drive.Name}, a {drive.RoadTypeName} drive";
        }

        protected override void OnItemActivated(int index)
        {
            if (index == ITEM_SURVEY)
            {
                _surveyor.Toggle();
                return;
            }

            if (index == ITEM_SURVEY_RESULTS)
            {
                string summary = _surveyor.DescribeSavedSurvey();
                Speak(summary ?? "No survey has been run yet. Choose the survey option above first.");
                return;
            }

            int driveIndex = index - FIXED_ITEM_COUNT;
            if (driveIndex < 0 || driveIndex >= LocationData.RoadDrives.Length)
                return;

            RoadDrive drive = LocationData.RoadDrives[driveIndex];

            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists() || !player.IsInVehicle())
                {
                    Speak("Get in the driver seat of a vehicle first, then pick the drive again.");
                    return;
                }

                if (player.SeatIndex != VehicleSeat.Driver)
                {
                    Speak("Move to the driver seat first.");
                    return;
                }

                Vehicle vehicle = player.CurrentVehicle;
                if (vehicle == null || !vehicle.Exists())
                {
                    Speak("Could not find your vehicle.");
                    return;
                }

                // Stop any drive already running BEFORE the teleport, so the
                // queued drive starts from an idle manager. Otherwise starting
                // one would stop the old drive and defer the new one, and the
                // deferral is the fragile path - better not to need it at all.
                if (_autoDrive.IsActive)
                {
                    _autoDrive.Stop(false);
                    Logger.Info("DRIVE|route-start|stopped the drive already running");
                }

                // Same teleport recipe as the Locations menu: no offset, clear
                // tasks, warp, kill any leftover speed, settle onto the wheels
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET,
                    vehicle.Handle,
                    drive.Start.X, drive.Start.Y, drive.Start.Z,
                    false, false, true);
                Function.Call(Hash.SET_ENTITY_VELOCITY, vehicle.Handle, 0f, 0f, 0f);
                Function.Call(Hash.SET_ENTITY_HEADING, vehicle.Handle, drive.StartHeading);
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle.Handle, 5f);

                Logger.Info($"DRIVE|route-start|{drive.Name}" +
                            $"|start={drive.Start.X:F0},{drive.Start.Y:F0}" +
                            $"|hdg={drive.StartHeading:F0}");

                Speak($"{drive.Name}: at the start, lined up on the {drive.RoadTypeName}. Setting off.");

                // Queue the drive rather than starting it now - see the field
                // comment. The autodrive announces distance and speed when it
                // actually begins.
                _hasPendingDrive = true;
                _pendingDestination = drive.End;
                _pendingName = drive.Name;
                _pendingStartTick = Game.GameTime + Constants.ROAD_TRIP_SETTLE_MS;
            }
            catch (System.Exception ex)
            {
                Logger.Exception(ex, "RoadTripsMenu.OnItemActivated");
                Speak("Failed to start the drive.");
            }
        }

        public override string GetMenuName()
        {
            return "Road Trips";
        }

        #endregion

        #region Deferred Start

        /// <summary>
        /// Starts a queued drive once the teleport has settled. Called every
        /// tick; does nothing when nothing is queued.
        /// </summary>
        public void Update(long currentTick)
        {
            if (!_hasPendingDrive || currentTick < _pendingStartTick)
                return;

            _hasPendingDrive = false;

            try
            {
                Ped player = Game.Player?.Character;
                if (player == null || !player.Exists() || !player.IsInVehicle() ||
                    player.SeatIndex != VehicleSeat.Driver)
                {
                    Speak("You left the driver seat, so the drive was not started.");
                    Logger.Info("DRIVE|route-deferred|abandoned, not driving");
                    return;
                }

                Logger.Info($"DRIVE|route-deferred|starting|{_pendingName}");
                _autoDrive.StartRouteDrive(_pendingDestination, _pendingName);
            }
            catch (System.Exception ex)
            {
                Logger.Exception(ex, "RoadTripsMenu.Update");
                Speak("Failed to start the drive.");
            }
        }

        #endregion
    }
}
