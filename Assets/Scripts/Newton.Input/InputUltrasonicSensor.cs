using NLog;
using SilverFit.Newton.Interface;
using SilverUtil;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace SilverFit.Newton.Common.Input
{
    /// <summary>
    /// Sensor driver for the original SilverFit Newton sensor
    /// </summary>
    public sealed class InputUltrasonicSensor : IInput
    {
        private const int BaudRate = 9600;
        private const string IdentificationConfirmation = "Newton";
        public const string DefaultId = "Unknown";
        
        /// <summary>
        /// Command sent to sensor to request identification
        /// </summary>
        private const string IdentificationRequest = "S";
        private const double MaxConfirmationDelaySeconds = 3;

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly string comport;

        private SerialPort serialPort;
        private bool disposed;
        private bool initialized;

        /// <summary>
        /// Object that is used to 'lock' a thread. To solve a conflict between main thread closing the serial port and the data received thread trying to read from it.
        /// </summary>
        private readonly object locker = new();

        private float ROMLow;
        private float ROMHigh;

        public InputUltrasonicSensor(RomSettings romSettings, string serialPort)
        {
            this.ROMLow = romSettings.ROMLow;
            this.ROMHigh = romSettings.ROMHigh;
            this.comport = serialPort;
        }

        public event NewInputDelegate NewInput;
        public event IdReceivedDelegate IdReceived;

        public string Version { get; private set; }
        public string SerialNumber { get; private set; }
        public bool FirstConfirmationReceived { get; private set; }

        public SensorSettings Initialize()
        {
            return this.Initialize(false);
        }

        public SensorSettings Initialize(bool waitForSuccess)
        {
            if (this.initialized)
            {
                return new SensorSettings
                {
                    Id = this.SerialNumber,
                    SensorType = SensorTypes.Ultrasonic
                };
            }
            else
            {
                SensorSettings newSensor = null;
                try
                {
                    this.serialPort = new SerialPort(this.comport, BaudRate);
                    this.serialPort.DataReceived += this.DataReceived;
                    this.serialPort.ReadTimeout = 5;
                    this.serialPort.Open();
                    this.SerialNumber = DefaultId;
                    this.serialPort.Write(IdentificationRequest);

                    if (waitForSuccess)
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        while (sw.Elapsed.TotalMilliseconds < 2000 && this.SerialNumber == DefaultId)
                        {
                            Thread.Sleep(10);
                        }

                        newSensor = new SensorSettings
                        {
                            Id = this.SerialNumber,
                            SensorType = SensorTypes.Ultrasonic
                        };
                    }

                    this.initialized = true;
                }
                catch (IOException e)
                {
                    Logger.Trace(e, "Failed to open serial port {port}", this.comport);
                    this.serialPort.Dispose();
                }
                catch (UnauthorizedAccessException e)
                {
                    Logger.Trace(e, "Failed to open serial port {port}", this.comport);
                    this.serialPort.Dispose();
                }
                return newSensor;
            }
        }

        public void Dispose()
        {
            Logger.Trace("Dispose");
            if (!this.disposed)
            {
                if (this.serialPort != null)
                {
                    this.serialPort.DataReceived -= this.DataReceived;
                    lock (this.locker)
                    {
                        this.serialPort.Close();
                        this.serialPort.Dispose();
                    }
                    this.serialPort = null;
                }
                this.disposed = true;
            }
        }

        public void Calibrate()
        { }
        public void Update()
        { }

        public float ScaleValue(float unscaledValue)
        {
            return Math.Abs(SilverUtil.MathF.Clamp(((float)unscaledValue - this.ROMLow) / (this.ROMHigh - this.ROMLow), 0, 1) - 1);
        }

        private void CheckIdentificationMessage(string identificationMessage)
        {
            // format is like const char IDString[] = "Newton,v2,2.11.0000";
            var regions = identificationMessage.Trim().Split(',');

            if (regions[0] == IdentificationConfirmation)
            {
                if (regions.Length > 2)
                {
                    this.Version = regions[1];
                    this.SerialNumber = regions[2];

                    SensorSettings connectedSensor = new SensorSettings();
                    connectedSensor.SensorType = SensorTypes.Ultrasonic;
                    connectedSensor.Id = this.SerialNumber;
                    Logger.Info("Connection established with Ultrasonic sensor: {id}", connectedSensor.Id);
                    // Call the event that the Id is received
                    this.IdReceived?.Invoke(connectedSensor.Id);
                }

                if (!this.FirstConfirmationReceived)
                {
                    Logger.Debug("First confirmation received");
                    this.FirstConfirmationReceived = true;
                }
            }
            else
            {
                this.SerialNumber = DefaultId;
            }
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (this.locker)
            {
                try
                {
                    string stringSensorOutput = "";
                    try
                    {
                        // If there was data received, but no new line, the function will time-out
                        stringSensorOutput = this.serialPort.ReadLine();
                        
                    }
                    catch (TimeoutException ex)
                    {
                        Debug.WriteLine(ex.Message);
                        return;
                    }

                    double parseOutput = 0;
                    if (double.TryParse(stringSensorOutput, NumberStyles.Float, CultureInfo.InvariantCulture, out parseOutput) && parseOutput > 0)
                    {
                        float sensorOutput = 10 * (float)parseOutput; // convert from centimeters to millimeters

                        // Finally call the event of newData on the main thread
                        this.NewInput?.Invoke(sensorOutput);
                    }
                    else // If it's not a measurement, check if this is a confirmation message
                    {
                        this.CheckIdentificationMessage(stringSensorOutput);
                    }
                }
                catch (Exception exc)
                {
                    Logger.Info(exc, $"Exception in Sensor DataReceived handler");
                }
            }
        }
    }
}
