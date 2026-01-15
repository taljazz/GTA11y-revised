#region Imports
using System; // Only necessary import for IComparable and Math
#endregion

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Represents a result with a name, distances, and direction, comparable by total distance.
    /// </summary>
    public class Result : IComparable
    {
        #region Properties
        /// <summary>
        /// Gets the name of the result (e.g., entity or object name).
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the horizontal distance (XY plane) to the result.
        /// </summary>
        public double XYDistance { get; private set; }

        /// <summary>
        /// Gets the vertical distance (Z axis) to the result.
        /// </summary>
        public double ZDistance { get; private set; }

        /// <summary>
        /// Gets the direction to the result.
        /// </summary>
        public string Direction { get; private set; }

        /// <summary>
        /// Gets the total distance to the result (XY + absolute Z).
        /// </summary>
        public double TotalDistance { get; private set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the Result class.
        /// </summary>
        /// <param name="name">The name of the result.</param>
        /// <param name="xyDistance">The horizontal distance (XY plane).</param>
        /// <param name="zDistance">The vertical distance (Z axis).</param>
        /// <param name="direction">The direction to the result.</param>
        /// <exception cref="ArgumentNullException">Thrown when name or direction is null.</exception>
        public Result(string name, double xyDistance, double zDistance, string direction)
        {
            if (name == null)
                throw new ArgumentNullException("name", "Result name cannot be null.");
            if (direction == null)
                throw new ArgumentNullException("direction", "Result direction cannot be null.");

            Name = name;
            XYDistance = xyDistance;
            ZDistance = zDistance;
            Direction = direction;
            TotalDistance = xyDistance + Math.Abs(zDistance);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Compares this result to another based on total distance.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>A value indicating the relative order of the objects.</returns>
        /// <exception cref="ArgumentException">Thrown when obj is not a Result.</exception>
        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1; // Null is considered greater
            Result other = obj as Result;
            if (other == null)
                throw new ArgumentException("Object is not a Result.", "obj");
            return TotalDistance.CompareTo(other.TotalDistance);
        }

        /// <summary>
        /// Returns a string representation of the result.
        /// </summary>
        /// <returns>A string in the format "Name, XYDistance meters Direction, ZDistance meters [above/below]".</returns>
        public override string ToString()
        {
            string vertical = ZDistance != 0 ? (ZDistance > 0 ? " " + Math.Abs(ZDistance) + " meters above" : " " + Math.Abs(ZDistance) + " meters below") : "";
            return string.Format("{0}, {1} meters {2}{3}", Name, XYDistance, Direction, vertical);
        }
        #endregion
    }
}