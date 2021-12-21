using System;
using DevBot9.Protocols.Homie;
using IotFleet.Shed;
using NLog;
using NLog.Config;
using NLog.Targets;
using Tevux.Protocols.Mqtt;
using Tinkerforge;


BrickletAirQuality _airQualityBricklet;
IPConnection _brickConnection;
ShedMonitorProducer _shedMonitorProducer = new();

// Configuring logger.
var log = LogManager.GetCurrentClassLogger();
var config = new LoggingConfiguration();
var logconsole = new ColoredConsoleTarget("console");
config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
var logdebug = new DebuggerTarget("debugger");
config.AddRule(LogLevel.Trace, LogLevel.Fatal, logdebug);
IotFleet.Helpers.AddFileOutputToLogger(config);
LogManager.Configuration = config;

// Load environment variables.
var brokerIp = IotFleet.Helpers.LoadEnvOrDie("MQTT_BROKER_IP", "localhost");
var shedMonitorIp = IotFleet.Helpers.LoadEnvOrDie("SHED_MONITOR_IP", "localhost");

// Initializing classes.
log.Info("Initializing connections.");
DeviceFactory.Initialize("homie");
var channelOptions = new ChannelConnectionOptions();
channelOptions.SetHostname(brokerIp);
_shedMonitorProducer.Initialize(channelOptions);

// Connecting to bricklets.
_brickConnection = new IPConnection();
_brickConnection.EnumerateCallback += HandleEnumeration;
_brickConnection.Connected += HandleConnection;
_brickConnection.Connect(shedMonitorIp, 4223);

log.Info("Application started.");

Console.ReadLine();

void HandleEnumeration(IPConnection sender, string UID, string connectedUID, char position, short[] hardwareVersion, short[] firmwareVersion, int deviceIdentifier, short enumerationType) {
    if (enumerationType == IPConnection.ENUMERATION_TYPE_CONNECTED || enumerationType == IPConnection.ENUMERATION_TYPE_AVAILABLE) {
        if (deviceIdentifier == BrickletAirQuality.DEVICE_IDENTIFIER) {
            _airQualityBricklet = new BrickletAirQuality(UID, _brickConnection);

            log.Info($"Found AirQuality bricklet {UID}. Giving it to ShedMonitor.");
            _shedMonitorProducer.AirQualityBricklet = _airQualityBricklet;
        }
        if (deviceIdentifier == BrickletIndustrialDual020mA.DEVICE_IDENTIFIER) {
            log.Info($"Found BrickletIndustrialDual020mA bricklet {UID}. Giving it to ShedMonitor.");
            _shedMonitorProducer.Industrial020Bricklet = new BrickletIndustrialDual020mA(UID, _brickConnection);
        }
    }
}

void HandleConnection(IPConnection sender, short connectReason) {
    log.Info("Connection to BrickDaemon has been established. Doing the (re)initialization.");

    // Enumerate devices again. If we reconnected, the Bricks/Bricklets may have been offline and the configuration may be lost.
    // In this case we don't care for the reason of the connection.
    log.Info("Beginning re-enumeration of bricklets.");
    _brickConnection.Enumerate();
}

