using System;
using System.Globalization;
using System.Threading;
using DevBot9.Protocols.Homie;
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
        var logdebug = new DebuggerTarget("debugger");
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, logdebug);
        Helpers.AddFileOutputToLogger(config);
        LogManager.Configuration = config;

        // Load environment variables.
        var brokerIp = Helpers.LoadEnvOrDie("MQTT_BROKER_IP", "localhost");
        var domektIp = Helpers.LoadEnvOrDie("DOMEKT_IP", "localhost");

        Log.Info("Application started.");
        DeviceFactory.Initialize("homie");
        _recuperator.Initialize(brokerIp, domektIp);

        Console.ReadLine();
    }
}
