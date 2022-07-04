using System;
using DevBot9.Protocols.Homie.Utilities;
using DevBot9.Protocols.Homie;
using NLog;
using Tevux.Protocols.Mqtt;
using System.Threading;

namespace IotFleet.Shed;

internal class CoolerProducer {
    private CancellationTokenSource _globalCancellationTokenSource = new();
    public static Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly YahiTevuxClientConnection _broker = new();

    private static double _threshold = 26;
    private static bool _isItFuckingHot = false;
    private static bool _isItDaytime = false;
    private static readonly ShmooController _shmooController = new();

    private static ClientDevice _clientDevice;
    private static ClientNumberProperty _supplyAirTemperature;
    private static ClientTextProperty _actualState;
    private static ClientTextProperty _targetState;

    public void Initialize(ChannelConnectionOptions channelOptions) {
        Log.Info($"Initializing {nameof(CoolerProducer)}.");

        _globalCancellationTokenSource = new CancellationTokenSource();

        _shmooController.Initialize(19, 8, 9);

        Log.Info($"Creating Homie properties.");
        _clientDevice = DeviceFactory.CreateClientDevice("recuperator");
        _supplyAirTemperature = _clientDevice.CreateClientNumberProperty(new ClientPropertyMetadata { PropertyType = PropertyType.State, NodeId = "temperatures", PropertyId = "supply-air-temperature", InitialValue = "0" });
        _actualState = _clientDevice.CreateClientTextProperty(new ClientPropertyMetadata { PropertyType = PropertyType.State, NodeId = "general", PropertyId = "actual-state", InitialValue = "1" });
        _targetState = _clientDevice.CreateClientTextProperty(new ClientPropertyMetadata { PropertyType = PropertyType.Command, NodeId = "general", PropertyId = "target-state", InitialValue = "1" });

        Log.Info($"Initializing Homie entities.");
        _broker.Initialize(channelOptions);
        _clientDevice.Initialize(_broker);

        Log.Info($"Connecting to broker at {channelOptions.Hostname}.");
        _broker.Connect();

        new Thread(() => {
            var gateOpenTime = DateTime.Now;
            var gateCloseTime = DateTime.Now;

            Log.Info($"Spinning up monitoring task.");
            while (_globalCancellationTokenSource.IsCancellationRequested == false) {
                Thread.Sleep(1000);
                if (_supplyAirTemperature.Value > _threshold + 0.2) {
                    _isItFuckingHot = true;
                }
                if (_supplyAirTemperature.Value < _threshold - 0.2) { _isItFuckingHot = false; }

                var hour = DateTime.UtcNow.Hour + 3;
                if ((hour < 23) && (hour >= 8)) {
                    _isItDaytime = true;
                }
                else {
                    _isItDaytime = false;
                }

                var shmooShallGo = _isItFuckingHot && _isItDaytime;

                // Air cooler control.
                if (shmooShallGo && (_shmooController.IsShmooReleased == false)) {
                    Log.Info($"Releasing the shmoo. It is {_supplyAirTemperature.Value} °C, {hour}h now.");
                    _shmooController.ReleaseTheShmoo();
                }

                if ((shmooShallGo == false) && _shmooController.IsShmooReleased) {
                    Log.Info($"Stopping the shmoo. It is {_supplyAirTemperature.Value} °C, {hour}h now.");
                    _shmooController.StopTheShmoo();
                }

                // var cpuTempString = System.IO.File.ReadAllText("/sys/class/thermal/thermal_zone0/temp");
                // var cpuTemp = Convert.ToInt32(cpuTempString) / 1000.0;

                // Roof sprinkler control.
                if (shmooShallGo) {
                    if (DateTime.Now > gateOpenTime) {
                        Log.Info($"Opening the gates!");
                        _shmooController.OpenTheGates();
                        gateCloseTime = DateTime.Now.AddSeconds(10);
                        gateOpenTime = DateTime.Now.AddSeconds(120);
                    }

                    if ((DateTime.Now > gateCloseTime) && _shmooController.IsGatesOpen) {
                        Log.Info($"Closing the gates!");
                        _shmooController.CloseTheGates();
                        gateCloseTime = DateTime.Now;
                    }
                }
                else {
                    _shmooController.CloseTheGates();
                }

                // Recuperator control.
                if ((shmooShallGo == false) && ((_actualState.Value == "ON-HIGH") || (_actualState.Value == "ON-MEDIUM"))) {
                    Log.Info("Spinning recuperator down.");
                    _targetState.Value = "ON-LOW";
                    Thread.Sleep(10000);
                }

                if (shmooShallGo && (_actualState.Value == "ON-LOW")) {
                    Log.Info("Spinning recuperator up.");
                    _targetState.Value = "ON-HIGH";
                    Thread.Sleep(10000);
                }
            }
        }).Start();
    }
}

