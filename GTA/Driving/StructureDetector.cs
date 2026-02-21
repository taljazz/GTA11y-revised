using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Detects and announces tunnels, bridges, U-turns, and hills.
    /// Extracted from AutoDriveManager for separation of concerns.
    /// </summary>
    public class StructureDetector
    {
        // PERFORMANCE: Pre-cached Hash value to avoid repeated casting
        private static readonly Hash _getGroundZHash = (Hash)Constants.NATIVE_GET_GROUND_Z_FOR_3D_COORD;

        private readonly AudioManager _audio;
        private readonly AnnouncementQueue _announcementQueue;

        // Structure detection state
        private int _currentStructureType;
        private long _lastStructureCheckTick;
        private long _lastStructureAnnounceTick;
        private bool _inStructure;

        // Pre-allocated OutputArguments to avoid per-tick allocations
        private readonly OutputArgument _structureBelowArg = new OutputArgument();
        private readonly OutputArgument _structureAboveArg = new OutputArgument();

        // U-turn tracking
        private Vector3 _uturnTrackingPosition;
        private float _uturnTrackingHeading;
        private long _lastUturnAnnounceTick;

        // Hill tracking
        private long _lastHillAnnounceTick;
        private bool _announcedCurrentHill;
        private float _lastHillGradient;

        /// <summary>
        /// Current structure type (STRUCTURE_TYPE_NONE, TUNNEL, BRIDGE, etc.)
        /// </summary>
        public int CurrentStructureType => _currentStructureType;

        /// <summary>
        /// Whether currently in a structure
        /// </summary>
        public bool IsInStructure => _inStructure;

        /// <summary>
        /// Whether a hill has been announced
        /// </summary>
        public bool AnnouncedCurrentHill => _announcedCurrentHill;

        public StructureDetector(AudioManager audio, AnnouncementQueue announcementQueue)
        {
            _audio = audio;
            _announcementQueue = announcementQueue;
            Reset();
        }

        /// <summary>
        /// Reset all state
        /// </summary>
        public void Reset()
        {
            _currentStructureType = Constants.STRUCTURE_TYPE_NONE;
            _lastStructureCheckTick = 0;
            _lastStructureAnnounceTick = 0;
            _inStructure = false;

            _uturnTrackingPosition = Vector3.Zero;
            _uturnTrackingHeading = 0f;
            _lastUturnAnnounceTick = 0;

            _lastHillAnnounceTick = 0;
            _announcedCurrentHill = false;
            _lastHillGradient = 0f;
        }

        /// <summary>
        /// Initialize U-turn tracking when driving starts
        /// </summary>
        public void InitializeUturnTracking(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return;

            _uturnTrackingPosition = vehicle.Position;
            _uturnTrackingHeading = vehicle.Heading;
        }

        /// <summary>
        /// Detect and announce U-turns
        /// </summary>
        public void CheckUturn(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (vehicle == null || !vehicle.Exists())
                return;

            // Cooldown check
            if (currentTick - _lastUturnAnnounceTick < Constants.UTURN_ANNOUNCE_COOLDOWN)
                return;

            float currentHeading = vehicle.Heading;
            float distance = (position - _uturnTrackingPosition).Length();

            // Check if we've traveled enough distance to evaluate
            if (distance >= Constants.UTURN_DISTANCE_THRESHOLD)
            {
                float headingChange = Math.Abs(RoadFeatureDetector.NormalizeAngleDiff(currentHeading - _uturnTrackingHeading));

                if (headingChange >= Constants.UTURN_HEADING_THRESHOLD)
                {
                    _lastUturnAnnounceTick = currentTick;
                    _announcementQueue.TryAnnounce("Making U-turn",
                        Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceStructures");
                }

                // Reset tracking
                _uturnTrackingPosition = position;
                _uturnTrackingHeading = currentHeading;
            }
        }

        /// <summary>
        /// Check and announce hills/gradients
        /// </summary>
        public void CheckHillGradient(Vehicle vehicle, Vector3 position, long currentTick)
        {
            if (vehicle == null || !vehicle.Exists())
                return;

            // Cooldown check
            if (currentTick - _lastHillAnnounceTick < Constants.HILL_ANNOUNCE_COOLDOWN)
                return;

            try
            {
                // Get vehicle pitch (negative = going uphill, positive = going downhill in GTA V)
                float pitch = vehicle.Rotation.X;

                // Check for significant gradient
                if (Math.Abs(pitch) >= Constants.HILL_STEEP_THRESHOLD)
                {
                    if (!_announcedCurrentHill || Math.Abs(pitch - _lastHillGradient) > 3f)
                    {
                        _announcedCurrentHill = true;
                        _lastHillGradient = pitch;
                        _lastHillAnnounceTick = currentTick;

                        string hillType = pitch < 0 ? "Steep uphill" : "Steep downhill";
                        _announcementQueue.TryAnnounce(hillType,
                            Constants.ANNOUNCE_PRIORITY_HIGH, currentTick, "announceStructures");
                    }
                }
                else if (Math.Abs(pitch) >= Constants.HILL_MODERATE_THRESHOLD)
                {
                    if (!_announcedCurrentHill || Math.Abs(pitch - _lastHillGradient) > 3f)
                    {
                        _announcedCurrentHill = true;
                        _lastHillGradient = pitch;
                        _lastHillAnnounceTick = currentTick;

                        string hillType = pitch < 0 ? "Uphill grade" : "Downhill grade";
                        _announcementQueue.TryAnnounce(hillType,
                            Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceStructures");
                    }
                }
                else
                {
                    // Reset when on flat ground
                    if (_announcedCurrentHill && Math.Abs(pitch) < Constants.HILL_MODERATE_THRESHOLD - 1f)
                    {
                        _announcedCurrentHill = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StructureDetector.CheckHillGradient");
            }
        }

        /// <summary>
        /// Check for tunnels and bridges
        /// </summary>
        /// <param name="vehicle">Current vehicle</param>
        /// <param name="position">Current position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="currentRoadType">Current road type from RoadTypeManager</param>
        public void CheckStructures(Vehicle vehicle, Vector3 position, long currentTick, int currentRoadType)
        {
            if (vehicle == null || !vehicle.Exists())
                return;

            // Throttle checks
            if (currentTick - _lastStructureCheckTick < Constants.TICK_INTERVAL_STRUCTURE_CHECK)
                return;

            _lastStructureCheckTick = currentTick;

            try
            {
                int detectedType = Constants.STRUCTURE_TYPE_NONE;

                // Check for ceiling above (tunnel/overpass) - result not used but call determines if something is above
                // Uses pre-allocated OutputArgument to avoid per-tick allocations
                Function.Call<bool>(
                    _getGroundZHash,
                    position.X, position.Y, position.Z + Constants.STRUCTURE_CHECK_HEIGHT,
                    _structureAboveArg,
                    false);

                // Check current road type for tunnel
                if (currentRoadType == Constants.ROAD_TYPE_TUNNEL)
                {
                    detectedType = Constants.STRUCTURE_TYPE_TUNNEL;
                }
                else
                {
                    // Check for ground below (bridge check)
                    // Use pre-allocated OutputArgument to avoid per-tick allocations
                    bool hasBelowGround = Function.Call<bool>(
                        _getGroundZHash,
                        position.X, position.Y, position.Z - 2f,
                        _structureBelowArg,
                        false);

                    if (hasBelowGround)
                    {
                        float belowZ = _structureBelowArg.GetResult<float>();
                        // Guard against invalid float values from native
                        if (float.IsNaN(belowZ) || float.IsInfinity(belowZ))
                            return;

                        float heightAboveGround = position.Z - belowZ;

                        if (heightAboveGround > Constants.BRIDGE_MIN_HEIGHT_BELOW)
                        {
                            detectedType = Constants.STRUCTURE_TYPE_BRIDGE;
                        }
                    }
                }

                // Announce structure changes
                if (detectedType != _currentStructureType)
                {
                    bool wasInStructure = _inStructure;
                    _currentStructureType = detectedType;
                    _inStructure = detectedType != Constants.STRUCTURE_TYPE_NONE;

                    if (currentTick - _lastStructureAnnounceTick > Constants.STRUCTURE_ANNOUNCE_COOLDOWN)
                    {
                        _lastStructureAnnounceTick = currentTick;

                        if (_inStructure && !wasInStructure)
                        {
                            string structureName = GetStructureName(detectedType);
                            _announcementQueue.TryAnnounce($"Entering {structureName}",
                                Constants.ANNOUNCE_PRIORITY_MEDIUM, currentTick, "announceStructures");
                        }
                        else if (!_inStructure && wasInStructure)
                        {
                            _announcementQueue.TryAnnounce("Exiting structure",
                                Constants.ANNOUNCE_PRIORITY_LOW, currentTick, "announceStructures");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "StructureDetector.CheckStructures");
            }
        }

        /// <summary>
        /// Get structure name for announcement
        /// </summary>
        public static string GetStructureName(int structureType)
        {
            switch (structureType)
            {
                case Constants.STRUCTURE_TYPE_TUNNEL: return "tunnel";
                case Constants.STRUCTURE_TYPE_BRIDGE: return "bridge";
                case Constants.STRUCTURE_TYPE_OVERPASS: return "overpass";
                case Constants.STRUCTURE_TYPE_UNDERPASS: return "underpass";
                default: return "structure";
            }
        }

    }
}
