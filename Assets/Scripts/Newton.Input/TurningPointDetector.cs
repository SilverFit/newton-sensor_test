using System;
using System.Collections.Generic;
using System.Linq;

namespace SilverFit.Newton.Input
{
    /// <summary>
    /// A TurningPointDetector keeps track of turning points in sequences of sensor input. To feed it with input, use the AddValueAndGetTurningPoint method.
    /// </summary>
    public class TurningPointDetector
    {
        private readonly float minimalDistanceForTurningPoint;

        private readonly TimeSpan movingAverageWindowSize;

        private readonly Queue<SensorReading> readingsHistory;

        private float startPosition;
        private float avgRecent;
        private float avgOlder;
        private int movementDirection; // should be either 0 (undecided), 1 (values going up) or -1 (values going down)
        private TurningPoint potentialTurningPoint;

        /// <summary>
        /// Initializes a <see cref="TurningPointDetector"/>.
        /// </summary>
        /// <param name="minimalDistanceForTurningPoint">the required distance travelled before an extremum is labelled as a turning point</param>
        /// <param name="movingAverageWindowSize">the window size for the moving average filter</param>
        public TurningPointDetector(float minimalDistanceForTurningPoint, TimeSpan movingAverageWindowSize)
        {
            this.minimalDistanceForTurningPoint = minimalDistanceForTurningPoint;
            this.movingAverageWindowSize = movingAverageWindowSize;

            this.startPosition = 0;
            this.movementDirection = 0;
            this.readingsHistory = new Queue<SensorReading>(125);
            potentialTurningPoint = null;
        }

        /// <summary>
        /// Feed a sensor reading value to this <see cref="TurningPointDetector"/>
        /// </summary>
        /// <param name="reading"> the value of the sensor reading </param>
        /// <param name="time"> the time of the sensor reading</param>
        /// <returns></returns>
        public TurningPoint AddValueAndGetTurningPoint(float reading, TimeSpan time)
        {
            // set startpoint for the initial sample
            if (this.startPosition == 0)
            { this.startPosition = reading; }

            // add new reading to list of readings
            this.readingsHistory.Enqueue(new SensorReading(reading, time));

            // get the recent and older reading positions from a moving average filter
            ComputeOlderAndRecentAverages(time);

            // Check if movement direction was set
            if (movementDirection == 0) // if not, set the movement direction and return null
            {
                ObtainMovementDirection();
                return null;
            }
            else // if movement direction was set, check if there was a turning point
            {
                TurningPoint turningPoint = ObtainTurningPointIfPresent(time);

                if (turningPoint != null) // if there was a turning point, return it
                {
                    this.ResetAfterTurningPoint();
                    return turningPoint;
                }
                else // if there was no turning point, return null
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// sets avgRecent and avgOlder to their respective moving average values
        /// </summary>
        /// <param name="currentTime">the time of the last added sensor reading</param>
        private void ComputeOlderAndRecentAverages(TimeSpan currentTime)
        {
            // specify time windows
            TimeSpan timeNow = currentTime;
            TimeSpan timeRecent = timeNow.Subtract(this.movingAverageWindowSize);
            TimeSpan timeOld = timeRecent.Subtract(this.movingAverageWindowSize);

            // Remove old readings
            while (this.readingsHistory.Peek().TimeOfReading < timeOld)
            {
                this.readingsHistory.Dequeue();
            }
            //this.readingsHistory.RemoveAll(o => o.TimeOfReading < currentTime - (readingsSeconds * 2));

            // compute moving averages
            this.avgRecent = AverageValueOnTimeInterval(this.readingsHistory.ToList(), timeRecent, timeNow);
            this.avgOlder = AverageValueOnTimeInterval(this.readingsHistory.ToList(), timeOld, timeRecent);
        }

        /// <summary>
        /// Computes and sets the current movement direction by looking at the difference between the current position and the starting position.
        /// </summary>
        private void ObtainMovementDirection()
        {
            if (!float.IsNaN(avgOlder) && !float.IsNaN(avgRecent)) // make sure that both "old" and "recent" data are available
            {
                if (Math.Abs(this.avgRecent - this.startPosition) > this.minimalDistanceForTurningPoint)
                {
                    this.movementDirection = Math.Sign(this.avgRecent - this.startPosition);
                }
            }
        }

        private void ResetAfterTurningPoint()
        {
            this.potentialTurningPoint = null;
            this.startPosition = this.avgRecent;
            this.movementDirection *= -1;
        }

        private float AverageValueOnTimeInterval(List<SensorReading> sensorReadings, TimeSpan timeLowerBound, TimeSpan timeUpperBound)
        {
            var selectedReadings = sensorReadings.Where(r => r.TimeOfReading >= timeLowerBound & r.TimeOfReading <= timeUpperBound).ToList();
            return selectedReadings.Sum(r => r.SensorValue) / selectedReadings.Count;
        }

        private TurningPoint ObtainTurningPointIfPresent(TimeSpan currentTime)
        {
            // check if there is a turning point,
            // by checking whether the current movement direction is opposite to the stored moving direction
            if (this.movementDirection * (this.avgRecent - this.avgOlder) < 0) 
            {
                if (this.potentialTurningPoint == null) // If there is no candidate turning point yet, set it. Return null.
                {
                    this.potentialTurningPoint = new TurningPoint(this.avgOlder, currentTime.Subtract(this.movingAverageWindowSize), this.movementDirection);
                    return null;
                }
                else if (Math.Abs(this.avgRecent - this.potentialTurningPoint.Value) > this.minimalDistanceForTurningPoint)// If there was a candidate turning point already, check if the current position is far enough from there
                {
                    return this.potentialTurningPoint; //return the candidate turning point
                }
                else
                {
                    return null;
                }
            }
            else // if not, reset the potential turning point
            {
                this.potentialTurningPoint = null;
                return null;
            }
        }

        private class SensorReading
        {
            public float SensorValue => sensorValue;
            public TimeSpan TimeOfReading { get { return timeOfReading; } }
            private readonly float sensorValue;
            private TimeSpan timeOfReading;

            public SensorReading(float reading, TimeSpan time)
            {
                this.sensorValue = reading;
                this.timeOfReading = time;
            }
        }
    }

    /// <summary>
    /// A class to store a turning point, including its value, time and the direction of movement prior to the turning point
    /// </summary>
    public record TurningPoint
    {
        /// <summary>
        /// The sensor value at the turning point
        /// </summary>
        public float Value { get { return value; } }
        /// <summary>
        /// The time-stamp for the turning point.
        /// </summary>
        public TimeSpan Time { get { return time; } }
        /// <summary>
        /// The direction of movement prior to the turning point. Is either <c>1</c> or <c>-1</c>.
        /// </summary>
        public int Direction { get { return direction; } }
        private readonly float value;
        private readonly TimeSpan time;
        private readonly int direction;

        /// <summary>
        /// Constructs a <see cref="TurningPoint"/>
        /// </summary>
        /// <param name="Value">the sensor value at the turning point</param>
        /// <param name="time">the time at the turning point</param>
        /// <param name="direction">the direction of movement prior to the turning point, should be either <c>1</c> or <c>-1</c>.</param>
        public TurningPoint(float Value, TimeSpan time, int direction)
        {
            this.value = Value;
            this.time = time;
            this.direction = direction;
        }
    }
}
