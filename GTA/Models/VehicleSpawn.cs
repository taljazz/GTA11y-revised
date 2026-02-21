using System;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility
{
    public class VehicleSpawn : IComparable<VehicleSpawn>
    {
        #region Fields
        public string name;
        public VehicleHash id;
        public string vehicleClassName;
        #endregion
        #region Methods
        public int CompareTo(VehicleSpawn other)
        {
            if (other == null) return 1;
            return name.CompareTo(other.name);
        }
        public VehicleSpawn(string name, VehicleHash id)
        {
            this.name = name;
            this.id = id;
            this.vehicleClassName = null;
            if (name == "")
                this.name = Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, id);
        }

        public VehicleSpawn(string name, VehicleHash id, string vehicleClassName)
        {
            this.name = name;
            this.id = id;
            this.vehicleClassName = vehicleClassName;
            if (name == "")
                this.name = Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, id);
        }
        #endregion
    }
}
