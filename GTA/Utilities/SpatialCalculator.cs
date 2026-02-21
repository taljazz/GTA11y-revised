using System;
using GTA;
using GTA.Math;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Handles all spatial calculations including distance, direction, and heading
    /// </summary>
    public static class SpatialCalculator
    {
        /// <summary>
        /// Calculate the angle between two points in degrees (0-360)
        /// </summary>
        public static double CalculateAngle(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y2 - y1;

            if (dx == 0 && dy == 0)
                return 0;

            double deg = Math.Atan2(dx, dy) * (180.0 / Math.PI);
            if (deg < 0)
                deg += 360.0;

            return Math.Floor(deg);
        }

        /// <summary>
        /// Convert a heading (0-360) to a compass direction string
        /// Note: GTA V uses mirrored coordinates where 90° = West, 270° = East
        /// </summary>
        public static string GetDirectionFromHeading(double heading)
        {
            // Normalize heading to 0-360
            heading = ((heading % 360) + 360) % 360;

            // GTA V coordinate system: East/West are swapped compared to standard compass
            if (heading < Constants.NORTH_NORTHEAST) return "north";
            if (heading < Constants.NORTHEAST) return "north-northwest";      // mirrored
            if (heading < Constants.EAST_NORTHEAST) return "northwest";       // mirrored
            if (heading < Constants.EAST) return "west-northwest";            // mirrored
            if (heading < Constants.EAST_SOUTHEAST) return "west";            // mirrored
            if (heading < Constants.SOUTHEAST) return "west-southwest";       // mirrored
            if (heading < Constants.SOUTH_SOUTHEAST) return "southwest";      // mirrored
            if (heading < Constants.SOUTH) return "south-southwest";          // mirrored
            if (heading < Constants.SOUTH_SOUTHWEST) return "south";
            if (heading < Constants.SOUTHWEST) return "south-southeast";      // mirrored
            if (heading < Constants.WEST_SOUTHWEST) return "southeast";       // mirrored
            if (heading < Constants.WEST) return "east-southeast";            // mirrored
            if (heading < Constants.WEST_NORTHWEST) return "east";            // mirrored
            if (heading < Constants.NORTHWEST) return "east-northeast";       // mirrored
            if (heading < Constants.NORTH_NORTHWEST) return "northeast";      // mirrored
            return "north-northeast";                                          // mirrored
        }

        /// <summary>
        /// Calculate which heading slice (0-7) a heading falls into
        /// </summary>
        public static int GetHeadingSlice(double heading)
        {
            // Normalize heading to 0-360
            heading = ((heading % 360) + 360) % 360;

            // Simple division - CPU handles this efficiently
            int slice = (int)(heading / Constants.HEADING_SLICE_DEGREES);
            return Math.Min(slice, Constants.HEADING_SLICE_COUNT - 1);
        }

        /// <summary>
        /// Get the name of a heading slice
        /// Note: GTA V uses mirrored coordinates where 90° = West, 270° = East
        /// </summary>
        public static string GetHeadingSliceName(int slice)
        {
            switch (slice)
            {
                case 0: return "north";
                case 1: return "northwest";  // GTA V: 45° is northwest, not northeast
                case 2: return "west";       // GTA V: 90° is west, not east
                case 3: return "southwest";  // GTA V: 135° is southwest, not southeast
                case 4: return "south";
                case 5: return "southeast";  // GTA V: 225° is southeast, not southwest
                case 6: return "east";       // GTA V: 270° is east, not west
                case 7: return "northeast";  // GTA V: 315° is northeast, not northwest
                default: return "unknown";
            }
        }

        /// <summary>
        /// Calculate horizontal (XY) distance between two positions
        /// </summary>
        public static double GetHorizontalDistance(Vector3 pos1, Vector3 pos2)
        {
            double dx = pos1.X - pos2.X;
            double dy = pos1.Y - pos2.Y;
            return Math.Round(Math.Sqrt(dx * dx + dy * dy), 1);
        }

        /// <summary>
        /// Calculate vertical (Z) distance between two positions
        /// </summary>
        public static double GetVerticalDistance(Vector3 pos1, Vector3 pos2)
        {
            return Math.Round(pos2.Z - pos1.Z, 1);
        }

        /// <summary>
        /// Get direction from pos1 to pos2 as a compass string
        /// </summary>
        public static string GetDirectionTo(Vector3 from, Vector3 to)
        {
            double angle = CalculateAngle(from.X, from.Y, to.X, to.Y);
            return GetDirectionFromHeading(angle);
        }
    }
}
