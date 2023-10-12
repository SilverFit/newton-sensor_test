using SilverFit.Newton.Common.Input;
using SilverFit.Newton.Interface;

namespace SilverFit.Newton.Input
{
    public class NoInput : IInput
    {
        public event NewInputDelegate NewInput;
        public event IdReceivedDelegate IdReceived;

        public void Calibrate()
        {
        }

        public void Dispose()
        {
        }

        public SensorSettings Initialize()
        {
            return null;
        }

        public float ScaleValue(float unscaledValue)
        {
            return unscaledValue;
        }

        public void Update()
        {
        }
    }
}
