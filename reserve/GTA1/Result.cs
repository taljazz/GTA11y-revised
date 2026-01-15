#region Imports
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#endregion

namespace GrandTheftAccessibility
{
    public class Result : IComparable
    {
        #region Fields
        public string name;
        public double xyDistance;
        public double zDistance;
        public string direction;
        public double totalDistance;
        #endregion
        #region Methods
        public int CompareTo(object obj)
        {
            Result c2 = (Result)obj;
            return totalDistance.CompareTo(c2.totalDistance);
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