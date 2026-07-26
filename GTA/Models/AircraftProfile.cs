using GTA;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Abstract base class describing how the attitude indicator behaves for a
    /// category of aircraft. Each subclass supplies its own sensitivity thresholds
    /// by overriding the abstract threshold properties; the pulse-rate logic itself
    /// lives here in the base class (template method pattern).
    /// Replaces the old Constants.AIRCRAFT_TYPE_* int constants and the
    /// switch-based GetPulseIntervalForAngle in GTA11Y.
    /// </summary>
    public abstract class AircraftProfile
    {
        #region Abstract Surface

        /// <summary>The aircraft category this profile describes.</summary>
        public abstract AircraftType Type { get; }

        /// <summary>Angle in degrees below which the aircraft is considered level (silent).</summary>
        protected abstract float LevelThreshold { get; }

        /// <summary>Angle in degrees below which the tilt is considered slight (slow pulse).</summary>
        protected abstract float SlightThreshold { get; }

        /// <summary>Angle in degrees below which the tilt is considered moderate (medium pulse). Beyond this is steep (rapid pulse).</summary>
        protected abstract float ModerateThreshold { get; }

        #endregion

        #region Virtual Surface

        /// <summary>
        /// Whether inverted/upright flight announcements apply to this aircraft.
        /// Only meaningful for fixed-wing flight; helicopters and blimps that roll
        /// past 90 degrees are crashing, not flying inverted.
        /// </summary>
        public virtual bool SupportsInvertedDetection => false;

        #endregion

        #region Pulse Calculation

        /// <summary>
        /// Get the attitude pulse interval for a given absolute pitch or roll angle.
        /// Shared logic for every aircraft type; only the thresholds differ per subclass.
        /// </summary>
        public long GetPulseIntervalForAngle(float absAngle)
        {
            if (absAngle < LevelThreshold)
                return Constants.AIRCRAFT_PULSE_SILENT;
            if (absAngle < SlightThreshold)
                return Constants.AIRCRAFT_PULSE_SLOW;
            if (absAngle < ModerateThreshold)
                return Constants.AIRCRAFT_PULSE_MEDIUM;
            return Constants.AIRCRAFT_PULSE_RAPID;
        }

        #endregion

        #region Factory

        // Profiles are stateless, so one shared instance per type is enough.
        private static readonly FixedWingProfile _fixedWing = new FixedWingProfile();
        private static readonly HelicopterProfile _helicopter = new HelicopterProfile();
        private static readonly BlimpProfile _blimp = new BlimpProfile();
        private static readonly VtolHoverProfile _vtolHover = new VtolHoverProfile();
        private static readonly VtolPlaneProfile _vtolPlane = new VtolPlaneProfile();

        private static readonly Hash _getFlightNozzlePositionHash =
            (Hash)Constants.NATIVE_GET_VEHICLE_FLIGHT_NOZZLE_POSITION;

        /// <summary>
        /// Determine the profile for a vehicle. Checks blimp and VTOL model hashes
        /// first (VTOL mode depends on the current nozzle position), then falls back
        /// to vehicle class.
        /// </summary>
        public static AircraftProfile ForVehicle(Vehicle vehicle)
        {
            try
            {
                if (vehicle == null || !vehicle.Exists())
                    return _fixedWing;

                int modelHash = vehicle.Model.Hash;

                if (Constants.BLIMP_VEHICLE_HASHES.Contains(modelHash))
                    return _blimp;

                if (Constants.VTOL_VEHICLE_HASHES.Contains(modelHash))
                {
                    // Nozzle position: 0.0 = plane mode, 1.0 = hover mode
                    try
                    {
                        float nozzlePosition = Function.Call<float>(_getFlightNozzlePositionHash, vehicle);
                        return nozzlePosition > Constants.VTOL_HOVER_THRESHOLD
                            ? (AircraftProfile)_vtolHover
                            : _vtolPlane;
                    }
                    catch
                    {
                        return _vtolPlane;
                    }
                }

                if (vehicle.ClassType == VehicleClass.Helicopters)
                    return _helicopter;

                return _fixedWing;
            }
            catch
            {
                return _fixedWing;
            }
        }

        #endregion
    }

    #region Concrete Profiles

    /// <summary>
    /// Fixed-wing aircraft: standard thresholds, supports inverted flight detection.
    /// </summary>
    public class FixedWingProfile : AircraftProfile
    {
        public override AircraftType Type => AircraftType.FixedWing;
        protected override float LevelThreshold => Constants.FIXED_WING_ANGLE_LEVEL;
        protected override float SlightThreshold => Constants.FIXED_WING_ANGLE_SLIGHT;
        protected override float ModerateThreshold => Constants.FIXED_WING_ANGLE_MODERATE;
        public override bool SupportsInvertedDetection => true;
    }

    /// <summary>
    /// Helicopters: tighter thresholds because small tilts matter more in a hover.
    /// </summary>
    public class HelicopterProfile : AircraftProfile
    {
        public override AircraftType Type => AircraftType.Helicopter;
        protected override float LevelThreshold => Constants.HELI_ANGLE_LEVEL;
        protected override float SlightThreshold => Constants.HELI_ANGLE_SLIGHT;
        protected override float ModerateThreshold => Constants.HELI_ANGLE_MODERATE;
    }

    /// <summary>
    /// Blimps: the tightest thresholds - a blimp should barely tilt at all.
    /// </summary>
    public class BlimpProfile : AircraftProfile
    {
        public override AircraftType Type => AircraftType.Blimp;
        protected override float LevelThreshold => Constants.BLIMP_ANGLE_LEVEL;
        protected override float SlightThreshold => Constants.BLIMP_ANGLE_SLIGHT;
        protected override float ModerateThreshold => Constants.BLIMP_ANGLE_MODERATE;
    }

    /// <summary>
    /// VTOL aircraft in hover mode: behaves like a helicopter, so it inherits the
    /// helicopter thresholds and only overrides its identity.
    /// </summary>
    public sealed class VtolHoverProfile : HelicopterProfile
    {
        public override AircraftType Type => AircraftType.VtolHover;
    }

    /// <summary>
    /// VTOL aircraft in plane mode: behaves like a fixed-wing (including inverted
    /// detection), inheriting everything except its identity.
    /// </summary>
    public sealed class VtolPlaneProfile : FixedWingProfile
    {
        public override AircraftType Type => AircraftType.VtolPlane;
    }

    #endregion
}
