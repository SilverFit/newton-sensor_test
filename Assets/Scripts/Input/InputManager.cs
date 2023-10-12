using NLog;
using SilverFit.Newton.Input;
using SilverFit.Newton.Interface;
using SilverUtil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SilverFit.Newton.Common.Input
{
    public class InputManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// If this is the automated ROM test before an exercise, we don't want to use the InputFilter
        /// </summary>
        private readonly bool PreventFiltering;

        private readonly NewtonSettings newtonSettings;
        private readonly IInput inputSource;
        
        private readonly Queue<float> InputFilter;
        private readonly int filterAmount;

        private SensorSettings activeSensor;
        private float lastPullAmount;
        private float lastPullAmountUnscaled;

        public InputManager(RomSettings romSettings, NewtonSettings newtonSettings, bool preventFiltering)
        {
            this.RomSettings = romSettings ?? throw new ArgumentNullException(nameof(romSettings));
            this.newtonSettings = newtonSettings ?? throw new ArgumentNullException(nameof(newtonSettings));
            this.PreventFiltering = preventFiltering;

            if (newtonSettings.IsSensorFilteringEnabled)
            {
                this.filterAmount = this.GetFilterAmount();
                this.InputFilter = new Queue<float>(this.filterAmount);
            }
           
            this.lastPullAmount = 0;

            this.inputSource = CreateInput(romSettings, newtonSettings);
        }

        public RomSettings RomSettings { get; private set; }

        public bool Inversed => this.activeSensor?.InputInversed == true;

        public void Initialize()
        {
            SensorSettings newSensor = inputSource.Initialize();
            this.activeSensor = this.newtonSettings.AddSensor(newSensor);
            this.inputSource.NewInput += this.InputSource_NewInput;
            this.inputSource.IdReceived += this.InputSource_IdReceived;
        }

        public void Destroy()
        {
            if (this.inputSource is not null)
            {
                this.inputSource.NewInput -= this.InputSource_NewInput;
                this.inputSource.IdReceived -= this.InputSource_IdReceived;
                this.inputSource.Dispose(); // NEW-557: calling this at the end of an exercise seemed to cause the crash, which resulted in results not being saved sometimes
            }
        }

        public void Calibrate()
        {
            inputSource.Calibrate();
        }

        public void Update()
        {
            inputSource.Update();
        }

        /// <summary>
        /// Calculates the amount of inputs are used in the moving average filter.
        /// It maps the range of 1 to 10 inputs over a ROM of 0 to 1200 mm.
        /// Any ROM over 1200 uses 10 inputs in the filter. 
        /// </summary>
        public int GetFilterAmount()
        {
            var RomHigh = this.RomSettings.ROMHigh;
            var RomLow = this.RomSettings.ROMLow;

            var highestOfTwo = Math.Max(RomHigh, RomLow);

            var result = (int)SilverUtil.MathF.Lerp(1, 10, SilverUtil.MathF.InverseLerp(0, 1000, highestOfTwo));
#if DEBUG
            if (!this.PreventFiltering)
            {
                Logger.Debug("Filter amount: {value}, ROMHigh: {ROMHigh}, ROMLow: {ROMLow}", result.ToString(), RomHigh, RomLow);
            }
#endif
            return result;
        }

        /// <summary>
        /// Get last data from the sensor, scaled (0 - 1) in respect to ROM
        /// </summary>
        /// <returns></returns>
        public float GetPullAmount()
        {
            return lastPullAmount;
        }

        /// <summary>
        /// Get last data from the sensor, unscaled
        /// </summary>
        /// <returns></returns>
        public float GetPullAmountUnscaled()
        {
            return lastPullAmountUnscaled;
        }

        /// <summary>
        /// Scale an unscaled value, received from the inputManager
        /// </summary>
        /// <param name="unscaledValue"></param>
        /// <returns></returns>
        public float ScaleValue(float unscaledValue)
        {
            if (this.activeSensor == null)
            {
                return inputSource.ScaleValue(unscaledValue);
            }
            else
            {
                if (this.activeSensor.InputInversed)
                    return 1 - inputSource.ScaleValue(unscaledValue);
                else
                    return inputSource.ScaleValue(unscaledValue);
            }
        }

        /// <summary>
        /// The inputSource received new input.
        /// </summary>
        private void InputSource_NewInput(float newDataUnscaled)
        {
            ExecuteOnMainThread.Queue.Enqueue(() =>
            {
                lastPullAmountUnscaled = newDataUnscaled;
                lastPullAmount = ScaleValue(newDataUnscaled);

                // No filtering occurs during the Automated ROM test
                if(!this.PreventFiltering && this.newtonSettings.IsSensorFilteringEnabled)
                {
                    // If the filter queue is full, remove one element
                    while (this.InputFilter.Count >= this.filterAmount)
                        this.InputFilter.Dequeue();

                    // Enqueue the most recent pull amount
                    this.InputFilter.Enqueue(lastPullAmount);

                    // If sensor filtering is enabled in F3-menu, make 'lastPullAmount' the average of the filter's values
                    this.lastPullAmount = this.InputFilter.Average();
                }
            });
        }

        private void InputSource_IdReceived(string SensorId)
        {
            ExecuteOnMainThread.Queue.Enqueue(() =>
            {
                SensorSettings settings = this.newtonSettings.Sensors.Find(x => x.Id == SensorId);
                if (settings != null)
                {
                    this.activeSensor = settings;
                }
            });
        }

        public int MinimalDistanceForTurningPoint()
        {
            if (this.activeSensor == null)
            {
                return RangeOfMotionConstants.DefaultMinimalRom;
            }
            else
            {
                try
                {
                    return this.activeSensor.MinimalRangeOfMotion;
                }
                catch (Exception)
                {
                    return RangeOfMotionConstants.DefaultMinimalRom;
                }
            }
        }

        private static IInput CreateInput(RomSettings romSettings, NewtonSettings newtonSettings)
        {
            if (newtonSettings.UseSensor)
            {
                if (!newtonSettings.Sensors.Any() || newtonSettings.Sensors.Any(s => s.SensorType is SensorTypes.YRF_USB or SensorTypes.YRF_WiFi))
                {
                    if (TryStartYoctopuce(romSettings, newtonSettings) is IInput input)
                    {
                        return input;
                    }
                }

                Logger.Error("Could not initialize usable sensor");
                return new NoInput();
            }
            else
            {
                Logger.Error("Could not initialize usable sensor");
                return new NoInput();
            }
        }

        private static InputYoctopuceSensor TryStartYoctopuce(RomSettings romSettings, NewtonSettings newtonSettings)
        {
            var yocto = new InputYoctopuceSensor(romSettings, newtonSettings, SensorTypes.YRF_USB);
            if (yocto.SensorConnected())
            {
                return yocto;
            }
            else
            {
                yocto.Dispose();
                return null;
            }
        }
    }
}
