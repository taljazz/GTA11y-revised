#region Imports
using System; // Only necessary import for ArgumentNullException
#endregion

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Represents a configurable setting with an identifier, display name, and value.
    /// </summary>
    public class Setting
    {
        #region Properties
        /// <summary>
        /// Gets the unique identifier of the setting.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the display name of the setting.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// Gets or sets the value of the setting (0 or 1).
        /// </summary>
        public int Value { get; set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the Setting class.
        /// </summary>
        /// <param name="id">The unique identifier of the setting.</param>
        /// <param name="displayName">The display name of the setting.</param>
        /// <param name="value">The initial value of the setting (0 or 1).</param>
        /// <exception cref="ArgumentNullException">Thrown when id or displayName is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not 0 or 1.</exception>
        public Setting(string id, string displayName, int value)
        {
            if (id == null)
                throw new ArgumentNullException("id", "Setting ID cannot be null.");
            if (displayName == null)
                throw new ArgumentNullException("displayName", "Setting display name cannot be null.");
            if (value != 0 && value != 1)
                throw new ArgumentOutOfRangeException("value", "Setting value must be 0 or 1.");

            Id = id;
            DisplayName = displayName;
            Value = value;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns a string representation of the setting.
        /// </summary>
        /// <returns>A string in the format "DisplayName: Value".</returns>
        public override string ToString()
        {
            return string.Format("{0}: {1}", DisplayName, Value == 1 ? "On" : "Off");
        }
        #endregion
    }
}