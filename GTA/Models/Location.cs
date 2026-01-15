#region Imports
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#endregion

namespace GrandTheftAccessibility
{
    public class Location
    {
        #region Fields
        public string name;
        public GTA.Math.Vector3 coords;
        #endregion
        #region Constructors
        public Location(string name, GTA.Math.Vector3 coords)
        {
            this.name = name;
            this.coords = coords;
        }
        #endregion
    }
}