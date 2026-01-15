namespace GrandTheftAccessibility
{
    /// <summary>
    /// Curve severity classification
    /// </summary>
    public enum CurveSeverity
    {
        None = 0,
        Gentle = 1,
        Moderate = 2,
        Sharp = 3,
        Hairpin = 4
    }

    /// <summary>
    /// Curve direction
    /// </summary>
    public enum CurveDirection
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// Comprehensive curve information including physics-based safe speed calculation
    /// </summary>
    public struct CurveInfo
    {
        /// <summary>
        /// Curve severity classification
        /// </summary>
        public CurveSeverity Severity { get; }

        /// <summary>
        /// Curve direction (left or right)
        /// </summary>
        public CurveDirection Direction { get; }

        /// <summary>
        /// Turn angle in degrees
        /// </summary>
        public float Angle { get; }

        /// <summary>
        /// Estimated curve radius in meters
        /// </summary>
        public float Radius { get; }

        /// <summary>
        /// Calculated safe speed for this curve in m/s
        /// </summary>
        public float SafeSpeed { get; }

        public CurveInfo(CurveSeverity severity, CurveDirection direction, float angle, float radius, float safeSpeed)
        {
            Severity = severity;
            Direction = direction;
            Angle = angle;
            Radius = radius;
            SafeSpeed = safeSpeed;
        }
    }
}
