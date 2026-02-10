using System;

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
