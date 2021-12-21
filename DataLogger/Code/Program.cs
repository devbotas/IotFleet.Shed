using System.Globalization;
using DevBot9.Protocols.Homie;
using DevBot9.Protocols.Homie.Utilities;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using IotFleet;
using NLog;
using NLog.Config;
using NLog.Targets;
using Tevux.Protocols.Mqtt;
using Helpers = IotFleet.Helpers;

// Configuring logger.
var Log = LogManager.GetCurrentClassLogger();
var config = new LoggingConfiguration();
var logconsole = new ColoredConsoleTarget("console");
config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
//var logdebug = new DebuggerTarget("debugger");
//config.AddRule(LogLevel.Trace, LogLevel.Fatal, logdebug);
Helpers.AddFileOutputToLogger(config);
LogManager.Configuration = config;

// Load environment variables.
var influxDbToken = Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_TOKEN");
var bucket = Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_BUCKET");
var org = Helpers.LoadEnvOrDie("INFLUXDB_ORG");
var influxDbHost = Helpers.LoadEnvOrDie("INFLUXDB_HOST", "http://127.0.0.1:8086");
var brokerIp = Helpers.LoadEnvOrDie("MQTT_BROKER_IP", "localhost");

// InfluxDB part.
Log.Info("Initializing InfluxDB.");
var tevukasSystemClient = InfluxDBClientFactory.Create(influxDbHost, influxDbToken.ToCharArray());
var tevukasWriteApi = tevukasSystemClient.GetWriteApi();
tevukasWriteApi.EventHandler += (sender, e) => {
    if (e is WriteErrorEvent) {
        Log.Warn("Cannot write to InfluxDB. Unfortunately, InfluxDB does not provide any useful debug information :(");
    }
};

DeviceFactory.Initialize("homie");

var channelOptions = new ChannelConnectionOptions();
channelOptions.SetHostname(brokerIp);

// Consumers can share a single connection to the broker.
var _clientConnection = new YahiTevuxClientConnection();
_clientConnection.Initialize(channelOptions);
_clientConnection.Connect();


// Creating a air conditioner device.
var _recuperator = DeviceFactory.CreateClientDevice("recuperator");
var _inletTemperature = _recuperator.CreateClientNumberProperty(new ClientPropertyMetadata() {
    PropertyType = PropertyType.State,
    NodeId = "temperatures",
    PropertyId = "supply-air-temperature",
    DataType = DataType.Float,
    InitialValue = "0"
});

var _shedMonitor = DeviceFactory.CreateClientDevice("shed-monitor");
var _shedMonitorAmbientTemperature = _shedMonitor.CreateClientNumberProperty(new ClientPropertyMetadata() {
    PropertyType = PropertyType.State,
    NodeId = "ambient",
    PropertyId = "temperature",
    DataType = DataType.Float,
    InitialValue = "0"
});
_shedMonitorAmbientTemperature.PropertyChanged += (sender, e) => {
    if (e.PropertyName == nameof(_shedMonitorAmbientTemperature.Value)) {
        var temperaturePoint = PointData.Measurement("Ambient").Field("Temperature", Convert.ToDouble(_shedMonitorAmbientTemperature.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        tevukasWriteApi.WritePoint(bucket, org, temperaturePoint);
    }
};

var _shedMonitorAmbientHumidity = _shedMonitor.CreateClientNumberProperty(new ClientPropertyMetadata() {
    PropertyType = PropertyType.State,
    NodeId = "ambient",
    PropertyId = "humidity",
    DataType = DataType.Float,
    InitialValue = "0"
});
_shedMonitorAmbientHumidity.PropertyChanged += (sender, e) => {
    if (e.PropertyName == nameof(_shedMonitorAmbientHumidity.Value)) {
        var temperaturePoint = PointData.Measurement("Ambient").Field("Humidity", Convert.ToDouble(_shedMonitorAmbientHumidity.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        tevukasWriteApi.WritePoint(bucket, org, temperaturePoint);
    }
};

var _shedMonitorAmbientPressure = _shedMonitor.CreateClientNumberProperty(new ClientPropertyMetadata() {
    PropertyType = PropertyType.State,
    NodeId = "ambient",
    PropertyId = "pressure",
    DataType = DataType.Float,
    InitialValue = "0"
});
_shedMonitorAmbientPressure.PropertyChanged += (sender, e) => {
    if (e.PropertyName == nameof(_shedMonitorAmbientPressure.Value)) {
        var temperaturePoint = PointData.Measurement("Ambient").Field("Pressure", Convert.ToDouble(_shedMonitorAmbientPressure.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        tevukasWriteApi.WritePoint(bucket, org, temperaturePoint);
    }
};

var _shedMonitorAmbientAirQualityIndex = _shedMonitor.CreateClientNumberProperty(new ClientPropertyMetadata() {
    PropertyType = PropertyType.State,
    NodeId = "ambient",
    PropertyId = "quality-index",
    DataType = DataType.Float,
    InitialValue = "0"
});
_shedMonitorAmbientAirQualityIndex.PropertyChanged += (sender, e) => {
    if (e.PropertyName == nameof(_shedMonitorAmbientAirQualityIndex.Value)) {
        var temperaturePoint = PointData.Measurement("Ambient").Field("AirQualityIndex", Convert.ToDouble(_shedMonitorAmbientAirQualityIndex.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        tevukasWriteApi.WritePoint(bucket, org, temperaturePoint);
    }
};

var _shedMonitorWaterPressure = _shedMonitor.CreateClientNumberProperty(new ClientPropertyMetadata() {
    PropertyType = PropertyType.State,
    NodeId = "general",
    PropertyId = "actual-water-pressure",
    DataType = DataType.Float,
    InitialValue = "0"
});
_shedMonitorWaterPressure.PropertyChanged += (sender, e) => {
    if (e.PropertyName == nameof(_shedMonitorWaterPressure.Value)) {
        var temperaturePoint = PointData.Measurement("Water").Field("ActualPressure", Convert.ToDouble(_shedMonitorWaterPressure.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        tevukasWriteApi.WritePoint(bucket, org, temperaturePoint);
    }
};

_inletTemperature.PropertyChanged += (sender, e) => {
    if (e.PropertyName == nameof(_inletTemperature.Value)) {
        var temperaturePoint = PointData.Measurement("Recuperator").Field("SupplyAirTemperature", Convert.ToDouble(_inletTemperature.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        tevukasWriteApi.WritePoint(bucket, org, temperaturePoint);
    }
};

_recuperator.Initialize(_clientConnection);
_shedMonitor.Initialize(_clientConnection);

Log.Info("Application started.");
Console.ReadLine();
