using NLog;
using SilverFit.Newton.Interface;
using SilverUtil;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SilverFit.Newton.Common.Input
{
    public sealed class InputYoctopuceSensor : IInput
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private const string usb = "usb";
        private const string extensionPartSensorId = ".rangeFinder1";
        private const double SAMPLE_INTERVAL_IN_MS = 50;

        private readonly System.Timers.Timer timer = new System.Timers.Timer(1000);
        /// <summary>
        /// Object that is used to 'lock' a thread. To solve a conflict between main thread closing the serial port and the data received thread trying to read from it.
        /// </summary>
        private readonly object locker = new();

        private string activeIpAddress = string.Empty;

        private float ROMLow;
        private float ROMHigh;
        private string ipAddress;
        private SensorTypes sensorType;
        private bool busy = false;

        private YRangeFinder rangeFinder = null;

        public InputYoctopuceSensor(RomSettings romSettings, SensorTypes sensorType, string ipAddress)
        {
            if (romSettings is null)
                throw new ArgumentNullException(nameof(romSettings));
            this.Init(romSettings, sensorType, ipAddress);
        }

        public InputYoctopuceSensor(RomSettings romSettings, NewtonSettings sensorSettings, SensorTypes sensorType)
        {
            if (romSettings is null)
                throw new ArgumentNullException(nameof(romSettings));
            if (sensorSettings is null)
                throw new ArgumentNullException(nameof(sensorSettings));

            if (!TypeIsYRF(sensorType))
            {
                throw new ArgumentException($"{sensorType} is not an YRF sensor", nameof(sensorType));
            }

            //string ipAddress = string.Empty;
            //this.sensorType = sensorType;
            //if (this.sensorType == SensorTypes.YRF_WiFi)
            //{
            //    var sensor = sensorSettings.Sensors.Find(x => x.SensorType == SensorTypes.YRF_WiFi && x.YRFIpAddress == sensorSettings.SelectedSensorAddress);
            //    if (sensor != null)
            //    {
            //        ipAddress = sensor.YRFIpAddress;
            //    }
            //}

            this.Init(romSettings, sensorType, ipAddress);
        }

        public event NewInputDelegate NewInput;
        public event IdReceivedDelegate IdReceived; // not used for YRF sensors

        public static IEnumerable<YRFSensorModes> AllowedModes
        {
            get
            {
                return new List<YRFSensorModes>()
                {
                    YRFSensorModes.LongRange,
                    YRFSensorModes.HighSpeed,
                    YRFSensorModes.Default,
                };
            }
        }

        private void Init(RomSettings romSettings, SensorTypes sensorType, string ipAddress)
        {
            this.ROMLow = romSettings.ROMLow;
            this.ROMHigh = romSettings.ROMHigh;
            this.ipAddress = ipAddress;
            this.sensorType = sensorType;

            this.timer.Interval = SAMPLE_INTERVAL_IN_MS;
        }

        private static bool TypeIsYRF(SensorTypes typeToCheck)
        {
            return typeToCheck is SensorTypes.YRF_USB or SensorTypes.YRF_WiFi;
        }

        /// <summary>
        /// Call register the hub and store the last used url
        /// </summary>
        /// <param name="url"></param>
        private void RegisterHub(string url)
        {
            string errmsg = "";
            YAPI.RegisterHub(url, ref errmsg); // if an error occurs, errmsg contains a string 

            bool success = !string.IsNullOrEmpty(errmsg);

            if (!success)
            {
                Logger.Error("Error while registing hub ip address: {ipAddress} message: {errmsg}", this.ipAddress, errmsg);
                this.activeIpAddress = url;
            }
            else
            {
                this.activeIpAddress = string.Empty;
            }
        }

        private void RegisterRangeFinder()
        {
            if (this.ipAddress == null || this.ipAddress == string.Empty)
            {
                this.RegisterHub(usb);
            }
            else
            {
                DateTime startTime = DateTime.Now;
                Logger.Info("Start making connection with {ipAddress}", this.ipAddress);
                this.RegisterHub(this.ipAddress);
                Logger.Info("It took {duration} ms to make a connection", (DateTime.Now - startTime).TotalMilliseconds);
            }
            this.rangeFinder = YRangeFinder.FirstRangeFinder();
        }

        /// <summary>
        /// Can be called to test if a sensor is connected without starting the loop that sends the values
        /// </summary>
        /// <returns></returns>
        public bool SensorConnected()
        {
            if (this.rangeFinder == null || this.ipAddress != this.activeIpAddress) // else already initialised and running 
            {
                this.RegisterRangeFinder();
            }
            return this.rangeFinder != null;
        }

        /// <summary>
        /// Initializes the sensor and starts the loop that sends the values
        /// </summary>
        /// <returns></returns>
        public SensorSettings Initialize()
        {
            SensorSettings connectedSensor = null;
            try
            {
                if (this.rangeFinder == null)
                {
                    this.RegisterRangeFinder();
                }

                if (this.rangeFinder != null)
                {
                    connectedSensor = new SensorSettings
                    {
                        SensorType = this.sensorType,
                        YRFSensorMode = YRFSensorMode.FromYocto(this.rangeFinder.get_rangeFinderMode()),
                        Id = this.rangeFinder.get_module().get_serialNumber(),
                    };
                    if (this.sensorType == SensorTypes.YRF_WiFi)
                    {
                        connectedSensor.YRFIpAddress = this.ipAddress;
                    }
                }

                DateTime sampleTime = DateTime.Now;
                DateTime newsample = DateTime.Now;
                double sampleLimit = 0;

                this.timer.Enabled = true;
                this.timer.Start();
                this.timer.Elapsed += (s, e) =>
                {
                    try
                    {
                        this.busy = true;
                        double sample;
                        if (this.CheckStatus(out sample))
                        {
                            if (sample < 2000.0) // > 2000 means no measurement
                            {
                                float sensorOutput = (float)sample;

                                // Finally call the event of newData on the main thread
                                if (NewInput != null)
                                {
                                    newsample = DateTime.Now;
                                    if ((newsample - sampleTime).TotalMilliseconds > sampleLimit)
                                    {
                                        sampleLimit = (newsample - sampleTime).TotalMilliseconds;
                                        Logger.Trace("New limit {sampleLimit} ms Value {sensorOutput} mm at {newsample}", sampleLimit, sensorOutput, newsample);
                                    }
                                    sampleTime = DateTime.Now;
                                    NewInput(sensorOutput);
                                }
                            }
                            string errmsg = string.Empty;
                            YAPI.Sleep(((int)SAMPLE_INTERVAL_IN_MS) - 10, ref errmsg);
                        }
                    }
                    finally
                    {
                        this.busy = false;
                    }

                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Exception in communication with YRF sensor");
            }
            return connectedSensor;
        }

        public bool CheckStatus(out double distance)
        {
            bool result = false;
            lock (this.locker)
            {
                distance = 0.0;

                try
                {
                    if (this.rangeFinder.isOnline())
                    {
                        StringBuilder sb = new();
                        distance = this.rangeFinder.get_currentValue();
                        sb.AppendLine($"Distance    : {distance}");
                        sb.AppendLine($"Temperature : {this.rangeFinder.get_currentTemperature()}");
                        sb.AppendLine($"Raw value   : {this.rangeFinder.get_currentRawValue()}");
                        Logger.Debug("Sensor status {status}", sb.ToString());
                        result = true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to check status");
                    var temp = e.Message;
                    result = false;
                }
                finally
                {
                }
            }
            return result;
        }

        public void Dispose()
        {
            this.timer.Stop();
            while (this.busy)
            {
                Thread.Sleep(100);
            }

            YAPI.FreeAPI();
        }

        public void Calibrate()
        { }

        public void Update()
        { }

        public float ScaleValue(float unscaledValue)
        {
            return Math.Abs(SilverUtil.MathF.Clamp(((float)unscaledValue - this.ROMLow) / (this.ROMHigh - this.ROMLow), 0, 1) - 1);
        }

        public bool SetSensorMode(string sensorId, YRFSensorModes newMode)
        {
            bool result = false;

            int requestedMode = YRFSensorMode.ToYocto(newMode);

            if (requestedMode != YRangeFinder.RANGEFINDERMODE_INVALID)
            {
                try
                {
                    if (this.sensorType == SensorTypes.YRF_USB)
                    {
                        this.RegisterHub(usb);
                    }
                    else
                    {
                        this.RegisterHub(this.ipAddress);
                    }
                    this.rangeFinder = YRangeFinder.FindRangeFinder(sensorId + extensionPartSensorId);
                    if (this.rangeFinder.isOnline())
                    {
                        int currentMode = this.rangeFinder.get_rangeFinderMode();
                        if (currentMode == requestedMode)
                        {
                            result = true;
                        }
                        else
                        { // we only save when the mode is changed, the flash memory is a limited to about 100000 write cycles 
                            result = this.rangeFinder.set_rangeFinderMode(requestedMode) == YAPI.SUCCESS;
                            if (result)
                            {
                                this.rangeFinder.get_module().saveToFlash();
                            }
                        }
                    }

                    Logger.Info("Sensor mode set to {mode} on sensor {sensorId}", newMode, sensorId);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to set sensor mode {mode} on sensor {sensorId}", newMode, sensorId);
                }
            }
            return result;
        }
    }
}
