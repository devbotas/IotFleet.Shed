using System;
using System.Threading;
using DevBot9.Protocols.Homie;
using NLog;
using Tinkerforge;

namespace ShedMonitor {
    class ShedMonitorProducer {
        private CancellationTokenSource _globalCancellationTokenSource = new CancellationTokenSource();
        private ReliableBroker _reliableBroker;

        private HostDevice _device;
        public HostFloatProperty Pressure;
        public HostFloatProperty Temperature;
        public HostFloatProperty Humidity;
        public HostFloatProperty QualityIndex;

        public HostFloatProperty WaterPressure;

        private DateTime _startTime = DateTime.Now;
        private HostFloatProperty _systemUptime;
        private HostStringProperty _systemIpAddress;

        public BrickletAirQuality AirQualityBricklet { get; set; }
        public BrickletIndustrialDual020mA Industrial020Bricklet { get; set; }
        public static Logger Log = LogManager.GetCurrentClassLogger();

        public ShedMonitorProducer() { }

        public void Initialize(ReliableBroker reliableBroker) {
            Log.Info($"Initializing {nameof(ShedMonitorProducer)}.");

            _globalCancellationTokenSource = new CancellationTokenSource();
            _reliableBroker = reliableBroker;
            _device = DeviceFactory.CreateHostDevice("shed-monitor", "Shed monitor");
            _reliableBroker.PublishReceived += _device.HandlePublishReceived;

            Log.Info($"Creating Homie properties.");
            _device.UpdateNodeInfo("general", "General properties", "no-type");
            WaterPressure = _device.CreateHostFloatProperty(PropertyType.State, "general", "actual-water-pressure", "Water pressure");

            _device.UpdateNodeInfo("ambient", "Ambient properties", "no-type");
            Pressure = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "pressure", "Pressure", 0, "hPa");
            Temperature = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "temperature", "Temperature", 0, "°C");
            Humidity = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "humidity", "Humidity", 0, "%");
            QualityIndex = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "quality-index", "Quality index");

            _device.UpdateNodeInfo("system", "System", "no-type");
            _systemUptime = _device.CreateHostFloatProperty(PropertyType.State, "system", "uptime", "Uptime", 0, "h");
            _systemIpAddress = _device.CreateHostStringProperty(PropertyType.State, "system", "ip-address", "IP address", Program.GetLocalIpAddress());

            Log.Info($"Initializing Homie entities.");
            _device.Initialize(_reliableBroker.PublishToTopic, _reliableBroker.SubscribeToTopic);

            new Thread(() => {
                Log.Info($"Spinning up parameter monitoring task.");
                while (_globalCancellationTokenSource.IsCancellationRequested == false) {
                    try {
                        if (AirQualityBricklet != null) {
                            Pressure.Value = (float)(AirQualityBricklet.GetAirPressure() / 100.0);
                            Temperature.Value = (float)(AirQualityBricklet.GetTemperature() / 100.0);
                            Humidity.Value = (float)(AirQualityBricklet.GetHumidity() / 100.0);
                            AirQualityBricklet.GetIAQIndex(out var index, out var _);
                            QualityIndex.Value = index;
                        }
                    }
                    catch (Exception) {
                        // Sometimes this happens. No problem, swallowing, and giving some time to recover.
                        Log.Info("Reading Tinkerforge bricklet timeouted.");
                        Thread.Sleep(2000);
                    }

                    _systemUptime.Value = (float)(DateTime.Now - _startTime).TotalHours;
                    _systemIpAddress.Value = Program.GetLocalIpAddress();

                    Thread.Sleep(5000);
                }
            }).Start();

            new Thread(() => {
                Log.Info($"Spinning up fast monitoring task.");
                while (_globalCancellationTokenSource.IsCancellationRequested == false) {
                    try {
                        if (Industrial020Bricklet != null) {
                            var sensorCurrent = Industrial020Bricklet.GetCurrent(0) / 100.0f;
                            if (sensorCurrent >= 4) {
                                // My sensor is 0-6 bar.
                                WaterPressure.Value = (sensorCurrent - 4) / 20 * 6;
                            }
                            else {
                                WaterPressure.Value = -1;
                            }
                        }
                    }
                    catch (Exception) {
                        // Sometimes this happens. No problem, swallowing, and giving some time to recover.
                        Log.Info("Reading Tinkerforge bricklet timeouted.");
                        Thread.Sleep(2000);
                    }

                    Thread.Sleep(500);
                }

            }).Start();
        }
    }
}
