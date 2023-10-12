namespace SilverFit.Newton.Input
{
    public static class RangeOfMotionConstants
    {
        /// <summary>
        /// The default minimal distance in millimeters required to perform an automatic Range of Motion calibration.
        /// </summary>
        public const int DefaultMinimalRom = 50;

        /// <summary>
        /// Error margin in millimeters that determines in what margin turningpoints with the same direction provide a correct
        /// boundary for an automatic Range of Motion calibration.
        /// </summary>
        public const float TurningPointErrorMargin = 150;
    }
}
