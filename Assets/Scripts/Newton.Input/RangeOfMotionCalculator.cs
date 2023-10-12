using SilverFit.Newton.Interface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SilverFit.Newton.Input
{
    /// <summary>
    /// A class to assist in the ROM autodetection
    /// </summary>
    public static class RangeOfMotionCalculator
    {
        /// <summary>
        /// Computes the <see cref="RomSettings"/> for a collection of <see cref="TurningPoint"/>s.
        /// This is done by taking the average of the last two values of the turning points in each direction.
        /// If there are not enough turning points, or their values are to far apart, this function will return null.
        /// </summary>
        /// <param name="turningPoints">The detected turning points</param>
        /// <param name="maxDifferenceBetweenRomFindings">The maximum distance between two turning points</param>
        /// <returns><see cref="RomSettings"/> if proper range of motion calibration is found, <c>null</c> otherwise. </returns>
        public static RomSettings GetRomSettings(IList<TurningPoint> turningPoints, float maxDifferenceBetweenRomFindings)
        {
            int[] directions = new int[2] { -1, 1 }; // the two movement directions
            float[] romAverages = new float[2] { float.NaN, float.NaN }; // allocate a variable for the averaged results

            for (int iDirection = 0; iDirection < 2; iDirection++) // for each direction
            {
                // get only the corresponding turning points
                var thisDirectionTurningPoints = turningPoints.Where(t => t.Direction == directions[iDirection]).ToList();
                {
                    // check if there are enough turning points
                    int n = thisDirectionTurningPoints.Count;
                    if (n >= 2)

                        // check if the last two turning points are not too far apart
                        if (Math.Abs(thisDirectionTurningPoints[n - 1].Value - thisDirectionTurningPoints[n - 2].Value) <= maxDifferenceBetweenRomFindings)
                        {
                            // compute average of last two turning points
                            romAverages[iDirection] = 0.5f * (thisDirectionTurningPoints[n - 1].Value + thisDirectionTurningPoints[n - 2].Value);
                        }
                }
            }

            if (romAverages.Any(r => float.IsNaN(r)))
            {
                return null;
            }
            else
            {
                RomSettings newRom = GetRomSettings(romAverages[0], romAverages[1]);
                return newRom;
            }
        }

        /// <summary>
        /// Computes the correct romsettings, given averages of the turning point positions. A margin is taken from each of the averages to correct for sensor noise.
        /// </summary>
        /// <param name="romResultLow">The average of the lower-end turning points</param>
        /// <param name="romResultHigh">The average of the higher-end turning points</param>
        /// <returns>The romsettings</returns>
        public static RomSettings GetRomSettings(float romResultLow, float romResultHigh)
        {
            // Create new ROM
            float margin = 0.05f; // size of error correction, as a fraction from distance to the sensor
            RomSettings newRom = new RomSettings();

            // Correct for measurement error
            float errorCorrectionUpperBound = 0.2f * (romResultHigh - romResultLow); // prevent the low and high bound to be inverted
            romResultHigh -= Math.Max(romResultHigh * margin, errorCorrectionUpperBound); // decrease high value
            romResultLow += Math.Max(romResultLow * margin, errorCorrectionUpperBound); // increase low value

            // NEW-380: When making small movements during calibration, ROMHigh would sometimes be lower than ROMLow,
            // which inverted the controls
            newRom.ROMHigh = Math.Max(romResultLow, romResultHigh);
            newRom.ROMLow = Math.Min(romResultLow, romResultHigh);

            return newRom;
        }
    }
}
