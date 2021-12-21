using System;
using System.Threading;
using DevBot9.Protocols.Homie;
using DevBot9.Protocols.Homie.Utilities;
using NLog;
using Tevux.Protocols.Mqtt;
using Tinkerforge;

namespace IotFleet.Shed;
class ShedMonitorProducer {
    private CancellationTokenSource _globalCancellationTokenSource = new();
    private readonly YahiTevuxHostConnection _broker = new();

    private HostDevice _device;
    public HostNumberProperty Pressure;
    public HostNumberProperty Temperature;
    public HostNumberProperty Humidity;
    public HostNumberProperty QualityIndex;

    public HostNumberProperty WaterPressure;

    private readonly DateTime _startTime = DateTime.Now;
    private HostNumberProperty _systemUptime;

    public BrickletAirQuality AirQualityBricklet { get; set; }
    public BrickletIndustrialDual020mA Industrial020Bricklet { get; set; }
    public static Logger Log = LogManager.GetCurrentClassLogger();

    public ShedMonitorProducer() { }

    public void Initialize(ChannelConnectionOptions channelOptions) {
        Log.Info($"Initializing {nameof(ShedMonitorProducer)}.");

        _globalCancellationTokenSource = new CancellationTokenSource();
        _device = DeviceFactory.CreateHostDevice("shed-monitor", "Shed monitor");

        Log.Info($"Creating Homie properties.");
        _device.UpdateNodeInfo("general", "General properties", "no-type");
        WaterPressure = _device.CreateHostNumberProperty(PropertyType.State, "general", "actual-water-pressure", "Water pressure");

        _device.UpdateNodeInfo("ambient", "Ambient properties", "no-type");
        Pressure = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "pressure", "Pressure", 0, "hPa");
        Temperature = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "temperature", "Temperature", 0, "°C");
        Humidity = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "humidity", "Humidity", 0, "%");
        QualityIndex = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "quality-index", "Quality index");

        _device.UpdateNodeInfo("system", "System", "no-type");
        _systemUptime = _device.CreateHostNumberProperty(PropertyType.State, "system", "uptime", "Uptime", 0, "h");

        Log.Info($"Initializing Homie entities.");
        // This builds topic trees and subscribes to everything.
        _broker.Initialize(channelOptions);
        _device.Initialize(_broker);

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
