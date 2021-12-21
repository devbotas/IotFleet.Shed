using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;
using DevBot9.Protocols.Homie;
using NLog;
using NLog.Config;
using Tinkerforge;

namespace IotFleer.Shed;

class Program {
    private static BrickletAirQuality _airQualityBricklet;
    private static IPConnection _brickConnection;

    private static string _localHostname = "no-hostname";
    private static string _localIpAddress = "0.0.0.0";

    private static ShedMonitorProducer _shedMonitorProducer = new ShedMonitorProducer();

    public static Logger Log = LogManager.GetCurrentClassLogger();

    static void Main() {
        // First, the logger.
        var logsFolder = Directory.GetCurrentDirectory();

        // NLog doesn't like backslashes.
        logsFolder = logsFolder.Replace("\\", "/");

        // Finalizing NLOG configuration.
        LogManager.Configuration = new XmlLoggingConfiguration(new XmlTextReader(new MemoryStream(Encoding.UTF8.GetBytes(Properties.Resources.NLogConfig.Replace("!LogsFolderTag!", logsFolder)))), "NLogConfig.xml");

        // Load environment variables.
        var brokerIp = Environment.GetEnvironmentVariable("MQTT_BROKER_IP");
        if (string.IsNullOrEmpty(brokerIp)) {
            Log.Warn("Evironment variable \"MQTT_BROKER_IP\" is not provided. Using 127.0.0.1.");
            brokerIp = "127.0.0.1";
        }
        var shedMonitor = Environment.GetEnvironmentVariable("SHED_MONITOR_IP");
        if (string.IsNullOrEmpty(shedMonitor)) {
            Log.Warn("Evironment variable \"SHED_MONITOR_IP\" is not provided. Using 127.0.0.1.");
            shedMonitor = "127.0.0.1";
        }


        // Initializing classes.
        Log.Info("Initializing connections.");
        DeviceFactory.Initialize("homie");
        _shedMonitorProducer.Initialize(brokerIp);

        // Connecting to bricklets.
        _brickConnection = new IPConnection();
        _brickConnection.EnumerateCallback += HandleEnumeration;
        _brickConnection.Connected += HandleConnection;
        _brickConnection.Connect(shedMonitor, 4223);

        Log.Info("Application started.");
    }
    static void HandleEnumeration(IPConnection sender, string UID, string connectedUID, char position, short[] hardwareVersion, short[] firmwareVersion, int deviceIdentifier, short enumerationType) {
        if (enumerationType == IPConnection.ENUMERATION_TYPE_CONNECTED || enumerationType == IPConnection.ENUMERATION_TYPE_AVAILABLE) {
            if (deviceIdentifier == BrickletAirQuality.DEVICE_IDENTIFIER) {
                _airQualityBricklet = new BrickletAirQuality(UID, _brickConnection);

                Log.Info($"Found AirQuality bricklet {UID}. Giving it to ShedMonitor.");
                _shedMonitorProducer.AirQualityBricklet = _airQualityBricklet;
            }
            if (deviceIdentifier == BrickletIndustrialDual020mA.DEVICE_IDENTIFIER) {
                Log.Info($"Found BrickletIndustrialDual020mA bricklet {UID}. Giving it to ShedMonitor.");
                _shedMonitorProducer.Industrial020Bricklet = new BrickletIndustrialDual020mA(UID, _brickConnection);
            }
        }
    }

    static void HandleConnection(IPConnection sender, short connectReason) {
        Log.Info("Connection to BrickDaemon has been established. Doing the (re)initialization.");

        _localIpAddress = GetLocalIpAddress();

        // Enumerate devices again. If we reconnected, the Bricks/Bricklets may have been offline and the configuration may be lost.
        // In this case we don't care for the reason of the connection.
        Log.Info("Beginning re-enumeration of bricklets.");
        _brickConnection.Enumerate();
    }

    public static string GetLocalIpAddress() {
        var returnValue = "0.0.0.0";

        _localHostname = Dns.GetHostName();
        var host = Dns.GetHostEntry(_localHostname);

        foreach (var ip in host.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork) {
                returnValue = ip.ToString();
            }
        }

        return returnValue;
    }
}
