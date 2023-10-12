using SilverFit.Newton.Common.Input;
using SilverFit.Newton.Interface;

namespace SilverFit.Newton.Input
{
    public class Sensor
    {
        /// <summary>
        /// Settings that are stored in the settings file and that are updated by the user in the F3 menu
        /// </summary>
        public SensorSettings Settings { get; set; }

        /// <summary>
        /// The sensor is connected
        /// </summary>
        public bool Connected { get; set; }

        public IInput Input { get; set; }
    }
}
