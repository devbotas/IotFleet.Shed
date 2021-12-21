using System.Device.Gpio;
using IotFleet;

namespace PoorMansCooler {
    public class ShmooController {
        private readonly GpioController _gpioController;
        private int _relay2Pin = 19;
        private readonly int _relay3Pin = 16;
        private int _noLedChannel = 8;
        private int _ncLedChannel = 9;


        private readonly Sn3218 _ledController = new();

        public bool IsShmooReleased { get; private set; }
        public bool IsGatesOpen { get; private set; }

        public void Initialize(int pinNumber, int noLedChannel, int ncLedChannel) {
            _relay2Pin = pinNumber;
            _noLedChannel = noLedChannel;
            _ncLedChannel = ncLedChannel;

#if !DEBUG
            _gpioController = new GpioController();
            _gpioController.OpenPin(_relay2Pin, PinMode.Output);
            _gpioController.OpenPin(_relay3Pin, PinMode.Output);


            _ledController.Initialize();
            _ledController.EnableDisableOutputs((1 << _noLedChannel) + (1 << _ncLedChannel));
#endif
        }

        public void ReleaseTheShmoo() {
#if !DEBUG
            _gpioController.Write(_relay2Pin, PinValue.High);
            _ledController.Set(_ncLedChannel);
            _ledController.Clear(_noLedChannel);
            IsShmooReleased = true;
#endif
        }

        public void StopTheShmoo() {
#if !DEBUG
            _gpioController.Write(_relay2Pin, PinValue.Low);
            _ledController.Set(_noLedChannel);
            _ledController.Clear(_ncLedChannel);
            IsShmooReleased = false;
#endif
        }

        public void OpenTheGates() {
            _gpioController.Write(_relay3Pin, PinValue.High);
            IsGatesOpen = true;
        }

        public void CloseTheGates() {
            _gpioController.Write(_relay3Pin, PinValue.Low);
            IsGatesOpen = false;
        }
    }
}
