using SilverFit.Newton.Interface;
using System;

namespace SilverFit.Newton.Common.Input
{
    public delegate void NewInputDelegate(float newDataUnscaled);
    public delegate void IdReceivedDelegate(string sensorIdentification);

    /// <summary>
    /// Interface for Input classes.
    /// </summary>
    public interface IInput : IDisposable
    {
        SensorSettings Initialize();
        void Calibrate();
        float ScaleValue(float unscaledValue);
        void Update();

        event NewInputDelegate NewInput;
        event IdReceivedDelegate IdReceived;
    }
}
