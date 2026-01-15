#region Imports
using GTA;        // For VehicleHash
using GTA.Native; // For Hash and Function
using System;     // For IComparable and ArgumentNullException
#endregion

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Represents a vehicle spawn option with a name and identifier, comparable by name.
    /// </summary>
    public class VehicleSpawn : IComparable
    {
        #region Properties
        /// <summary>
        /// Gets the display name of the vehicle.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the unique identifier (hash) of the vehicle.
        /// </summary>
        public VehicleHash Id { get; private set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the VehicleSpawn class.
        /// </summary>
        /// <param name="name">The display name of the vehicle, or empty to use the localized name.</param>
        /// <param name="id">The unique identifier (hash) of the vehicle.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
        public VehicleSpawn(string name, VehicleHash id)
        {
            if (name == null)
                throw new ArgumentNullException("name", "Vehicle spawn name cannot be null.");

            Id = id;
            Name = string.IsNullOrEmpty(name) ? Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, id) : name;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Compares this vehicle spawn to another based on name.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>A value indicating the relative order of the objects.</returns>
        /// <exception cref="ArgumentException">Thrown when obj is not a VehicleSpawn.</exception>
        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1; // Null is considered greater
            VehicleSpawn other = obj as VehicleSpawn;
            if (other == null)
                throw new ArgumentException("Object is not a VehicleSpawn.", "obj");
            return Name.CompareTo(other.Name);
        }

        /// <summary>
        /// Returns a string representation of the vehicle spawn.
        /// </summary>
        /// <returns>The display name of the vehicle.</returns>
        public override string ToString()
        {
            return Name;
        }
        #endregion
    }
}