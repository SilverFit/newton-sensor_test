using SilverFit.Newton.Interface;

namespace SilverFit.Newton.Common.Input
{
    public static class YRFSensorMode
    {
        /// <summary>
        /// Convert the enum YRFSensorMode to a int value that is known in YRangeFinder
        /// </summary>
        public static int ToYocto(YRFSensorModes sensormode)
        {
            return sensormode switch
            {
                YRFSensorModes.Default => YRangeFinder.RANGEFINDERMODE_DEFAULT,
                YRFSensorModes.HighAccuracy => YRangeFinder.RANGEFINDERMODE_HIGH_ACCURACY,
                YRFSensorModes.HighSpeed => YRangeFinder.RANGEFINDERMODE_HIGH_SPEED,
                YRFSensorModes.LongRange => YRangeFinder.RANGEFINDERMODE_LONG_RANGE,
                _ => YRangeFinder.RANGEFINDERMODE_INVALID,
            };
        }

        /// <summary>
        /// Convert the int value that is known in YRangeFinder to the enum YRFSensorModes
        /// </summary>
        public static YRFSensorModes FromYocto(int sensormode)
        {
            return sensormode switch
            {
                YRangeFinder.RANGEFINDERMODE_DEFAULT => YRFSensorModes.Default,
                YRangeFinder.RANGEFINDERMODE_HIGH_ACCURACY => YRFSensorModes.HighAccuracy,
                YRangeFinder.RANGEFINDERMODE_HIGH_SPEED => YRFSensorModes.HighSpeed,
                YRangeFinder.RANGEFINDERMODE_LONG_RANGE => YRFSensorModes.LongRange,
                _ => YRFSensorModes.Unknown,
            };
        }
    }
}
