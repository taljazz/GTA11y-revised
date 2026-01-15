#region Imports
using GTA.Math; // For Vector3
using System;   // For ArgumentNullException
#endregion

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Represents a named location with 3D coordinates in the game world.
    /// </summary>
    public class Location
    {
        #region Properties
        /// <summary>
        /// Gets the name of the location.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the 3D coordinates of the location.
        /// </summary>
        public Vector3 Coordinates { get; private set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the Location class.
        /// </summary>
        /// <param name="name">The name of the location.</param>
        /// <param name="coordinates">The 3D coordinates of the location.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
        public Location(string name, Vector3 coordinates)
        {
            if (name == null)
                throw new ArgumentNullException("name", "Location name cannot be null.");
            Name = name;
            Coordinates = coordinates;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns a string representation of the location.
        /// </summary>
        /// <returns>A string in the format "Name: (X, Y, Z)".</returns>
        public override string ToString()
        {
            return string.Format("{0}: ({1}, {2}, {3})", Name, Coordinates.X, Coordinates.Y, Coordinates.Z);
        }
        #endregion
    }
}