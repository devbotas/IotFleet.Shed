using System;
using DevBot9.Protocols.Homie;
using NLog;
using NLog.Config;
using NLog.Targets;
using Tevux.Protocols.Mqtt;

namespace IotFleet.Shed;

class Program {
    private static readonly Domekt200 _recuperator = new();
    public static Logger Log = LogManager.GetLogger("HomieWrapper.Main");
    static void Main() {
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


        DeviceFactory.Initialize("homie");
        var channelOptions = new ChannelConnectionOptions();
        channelOptions.SetHostname(brokerIp);

        _recuperator.Initialize(channelOptions, domektIp);

        Log.Info("Application started.");
        Console.ReadLine();
    }
}
