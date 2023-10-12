using SilverFit.Newton.Common.Input;
using SilverFit.Newton.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilverFit.Newton.Input
{
    public interface ISensorInputControl
    {
        /// <summary>
        /// Contains the index of the active sensor that is used at this moment
        /// -1 means no sensor active
        /// A sensor is active when it is sending values
        /// </summary>
        int ActiveSensor { get; }

        /// <summary>
        /// Call this function when the user changes the sensor settings
        /// All existing connections will be dropped and tested again
        /// The connection with an active sensor is dropped
        /// </summary>
        /// <param name="newtonSettings"></param>
        void UpdateSensorSettings(NewtonSettings newtonSettings);

        /// <summary>
        /// Search in the list of configured sensor what sensor is connected
        /// Should be called when the program is started or when the user physically connects or disconnects a sensor
        /// </summary>
        void SearchConnectedSensors();

        /// <summary>
        /// Return a list of all active sensors, used for the user interface to select a connected sensor
        /// </summary>
        /// <returns></returns>
        Dictionary<int, Sensor> GetConnectedSensors();

        /// <summary>
        /// Select and activate a sensor
        /// </summary>
        /// <param name="selected">Index of the sensor in the settings list</param>
        /// <param name="eventFunction">Function that is called when the sensor value changes</param>
        void SelectSensor(int selected, NewInputDelegate eventFunction);

        /// <summary>
        /// If a sensor is active, stop and destroy the sensor
        /// </summary>
        void DeselectSensor();

        /// <summary>
        /// If a user likes to add a new sensor, call this method to find a connected sensor
        /// Only for a Wi-Fi sensor a address is needed
        /// </summary>
        /// <param name="sensorType"></param>
        /// <returns></returns>
        SensorSettings SearchSensor(SensorTypes sensorType, string ipAddres = "");
    }
}
