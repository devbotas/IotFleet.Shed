using System;
using System.Globalization;
using System.Threading;
using DevBot9.Protocols.Homie;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace IotFleet.Shed;

class Program {
    private static Domekt200 _recuperator = new Domekt200();
    public static Logger Log = LogManager.GetLogger("HomieWrapper.Main");
    static void Main(string[] args) {
        // Configuring logger.
        var Log = LogManager.GetCurrentClassLogger();
        var config = new LoggingConfiguration();
        var logconsole = new ColoredConsoleTarget("console");
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
        //var logdebug = new DebuggerTarget("debugger");
        //config.AddRule(LogLevel.Trace, LogLevel.Fatal, logdebug);
        Helpers.AddFileOutputToLogger(config);
        LogManager.Configuration = config;

        // Load remaining environment variables.
        var influxDbToken = Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_TOKEN");
        var bucket = Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_BUCKET");
        var org = Helpers.LoadEnvOrDie("INFLUXDB_ORG");
        var influxDbHost = Helpers.LoadEnvOrDie("INFLUXDB_HOST", "http://127.0.0.1:8086");
        var brokerIp = Helpers.LoadEnvOrDie("MQTT_BROKER_IP", "localhost");
        var domektIp = Helpers.LoadEnvOrDie("DOMEKT_IP", "localhost");

        Log.Info("Application started.");
        DeviceFactory.Initialize("homie");
        _recuperator.Initialize(brokerIp, domektIp);

        // InfluxDB part.
        Log.Info("Initializing InfluxDB.");
        var tevukasSystemClient = InfluxDBClientFactory.Create(influxDbHost, influxDbToken.ToCharArray());
        var tevukasWriteApi = tevukasSystemClient.GetWriteApi();
        tevukasWriteApi.EventHandler += (sender, e) => {
            if (e is WriteErrorEvent) {
                Log.Warn("Cannot write to InfluxDB. Unfortunately, InfluxDB does not provide any useful debug information :(");
            }
        };

        new Thread(() => {
            while (true) {
                var temperaturePoint = PointData.Measurement("Recuperator").Field("SupplyAirTemperature", Convert.ToDouble(_recuperator.SupplyAirTemperatureProperty, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

                tevukasWriteApi.WritePoint(bucket, org, temperaturePoint);

                Thread.Sleep(5000);
            }
        }).Start();


        Console.ReadLine();
    }
}
