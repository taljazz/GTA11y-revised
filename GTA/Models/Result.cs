using System;

namespace GrandTheftAccessibility
{
    public class Result : IComparable<Result>
    {
        #region Fields
        public string name;
        public double xyDistance;
        public double zDistance;
        public string direction;
        public double totalDistance;
        #endregion
        #region Methods
        public int CompareTo(Result other)
        {
            if (other == null) return 1;
            return totalDistance.CompareTo(other.totalDistance);
        }
        public Result(string name, double xyDistance, double zDistance, string direction)
        {
            this.name = name;
            this.xyDistance = xyDistance;
            this.zDistance = zDistance;
            this.totalDistance = xyDistance + Math.Abs(zDistance);
            this.direction = direction;
        }
        #endregion
    }
}
