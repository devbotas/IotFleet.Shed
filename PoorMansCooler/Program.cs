using DevBot9.Protocols.Homie;
using DevBot9.Protocols.Homie.Utilities;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using NLog;
using NLog.Config;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace PoorMansCooler {
    class Program {
        public static Logger Log = LogManager.GetCurrentClassLogger();

        private static PahoClientDeviceConnection _broker = new PahoClientDeviceConnection();
        private static string _mqttBrokerIpAddress;

        private static double _threshold = 25;
        private static bool _isItFuckingHot = false;
        private static ShmooController _shmooController = new ShmooController();

        private static ClientDevice _clientDevice;
        private static ClientNumberProperty _supplyAirTemperature;
        private static ClientTextProperty _actualLevel;

        private static string _influxDbToken;
        private static string _influxDbBucket;
        private static string _influxDbOrg;
        private static InfluxDBClient _influxDbClient;
#if DEBUG
        private static string _influxDbAddress = "http://172.16.0.198:8086";
#else
        private static string _influxDbAddress = "http://localhost:8086";
#endif

        static void Main(string[] args) {
            ConfigureLogger();
            LoadEnvironment();
            InitializeDatabase();
            AttachToHomieProperties();
            _shmooController.Initialize(19, 8, 9);
            InitializeConnections();
            RunMonitor();
        }

        private static void ConfigureLogger() {
            // First, the logger.
            var logsFolder = Directory.GetCurrentDirectory();

            // NLog doesn't like backslashes.
            logsFolder = logsFolder.Replace("\\", "/");

            // Finalizing NLog configuration.
            LogManager.Configuration = new XmlLoggingConfiguration(new XmlTextReader(new MemoryStream(Encoding.UTF8.GetBytes(Properties.Resources.NLogConfig.Replace("!LogsFolderTag!", logsFolder)))), "NLogConfig.xml");
        }
        private static void RunMonitor() {
            var gateOpenTime = DateTime.Now;
            var gateCloseTime = DateTime.Now;

            using (var influxDbWriteApi = _influxDbClient.GetWriteApi()) {
                var valveStatePoint = PointData.Measurement("recuperator").Field("valve-state", _shmooController.IsShmooReleased ? 1 : 0).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                influxDbWriteApi.WritePoint(_influxDbBucket, _influxDbOrg, valveStatePoint);
            }

            while (true) {
                Thread.Sleep(1000);
                if (_supplyAirTemperature.Value > _threshold + 0.2) {
                    _isItFuckingHot = true;
                }
                if (_supplyAirTemperature.Value < _threshold - 0.2) { _isItFuckingHot = false; }

                if (_isItFuckingHot && (_shmooController.IsShmooReleased == false)) {
                    Log.Info($"Releasing the shmoo. It is {_supplyAirTemperature.Value} °C now.");
                    _shmooController.ReleaseTheShmoo();
                    using (var influxDbWriteApi = _influxDbClient.GetWriteApi()) {
                        var valveStatePoint = PointData.Measurement("recuperator").Field("valve-state", 1).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                        influxDbWriteApi.WritePoint(_influxDbBucket, _influxDbOrg, valveStatePoint);
                    }
                }

                if ((_isItFuckingHot == false) && _shmooController.IsShmooReleased) {
                    Log.Info($"Stopping the shmoo. It is {_supplyAirTemperature.Value} °C now.");
                    _shmooController.StopTheShmoo();
                    using (var influxDbWriteApi = _influxDbClient.GetWriteApi()) {
                        var valveStatePoint = PointData.Measurement("recuperator").Field("valve-state", 0).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                        influxDbWriteApi.WritePoint(_influxDbBucket, _influxDbOrg, valveStatePoint);
                    }
                }

                var cpuTempString = System.IO.File.ReadAllText("/sys/class/thermal/thermal_zone0/temp");
                var cpuTemp = Convert.ToInt32(cpuTempString) / 1000.0;
                using (var influxDbWriteApi = _influxDbClient.GetWriteApi()) {
                    var cpuPoint = PointData.Measurement("recuperator").Field("cpu-temperature", cpuTemp).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                    influxDbWriteApi.WritePoint(_influxDbBucket, _influxDbOrg, cpuPoint);
                }

                if (_isItFuckingHot) {
                    if (DateTime.Now > gateOpenTime) {
                        Log.Info($"Opening the gates!");
                        _shmooController.OpenTheGates();
                        gateCloseTime = DateTime.Now.AddSeconds(10);
                        gateOpenTime = DateTime.Now.AddSeconds(120);
                    }

                    if (DateTime.Now > gateCloseTime) {
                        Log.Info($"Closing the gates!");
                        _shmooController.CloseTheGates();
                        gateCloseTime = DateTime.Now;
                    }
                } else {
                    _shmooController.CloseTheGates();
                }

            }
        }
        private static void LoadEnvironment() {
            Log.Info("Loading WATER_THRESHOLD environment variable...");
            var thresholdString = Environment.GetEnvironmentVariable("WATER_THRESHOLD");
            if (string.IsNullOrEmpty(thresholdString)) {
                thresholdString = "23";
            }
            _threshold = double.Parse(thresholdString);

            Log.Info("Loading INFLUXDB_TOKENS environment variable...");
            var influxDbTokensString = Environment.GetEnvironmentVariable("INFLUXDB_TOKENS");
            if (string.IsNullOrEmpty(influxDbTokensString)) {
                Log.Error("Environment variable \"INFLUXDB_TOKENS\" is not provided, so no InfluxDB for your today, sir. ");
                influxDbTokensString = "a,b,c";
            }
            var influxParts = influxDbTokensString.Split(",");
            _influxDbToken = influxParts[0];
            _influxDbBucket = influxParts[1];
            _influxDbOrg = influxParts[2];

            Log.Info("Loading MQTT_TOKENS environment variable...");
            var mqttTokens = Environment.GetEnvironmentVariable("MQTT_TOKENS");
            if (string.IsNullOrEmpty(mqttTokens)) {
                Log.Error("Environment variable \"MQTT_TOKENS\" is not provided, so no MQTT for your today, sir. ");
                mqttTokens = "172.16.0.2";
            }
            var mqttParts = mqttTokens.Split(",");
            _mqttBrokerIpAddress = mqttParts[0];
        }
        private static void InitializeDatabase() {
            _influxDbClient = InfluxDBClientFactory.Create(_influxDbAddress, _influxDbToken);
        }
        private static void AttachToHomieProperties() {
            DeviceFactory.Initialize("homie");
            _clientDevice = DeviceFactory.CreateClientDevice("recuperator");

            _supplyAirTemperature = _clientDevice.CreateClientNumberProperty(new ClientPropertyMetadata { PropertyType = PropertyType.State, NodeId = "temperatures", PropertyId = "supply-air-temperature" });
            _supplyAirTemperature.PropertyChanged += (sender, e) => {
                var temperaturePoint = PointData.Measurement("recuperator").Field("supply-air-temperature", _supplyAirTemperature.Value).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                using (var influxDbWriteApi = _influxDbClient.GetWriteApi()) {
                    influxDbWriteApi.WritePoint(_influxDbBucket, _influxDbOrg, temperaturePoint);
                }
            };

            _actualLevel = _clientDevice.CreateClientTextProperty(new ClientPropertyMetadata { PropertyType = PropertyType.State, NodeId = "ventilation", PropertyId = "actual-level" });
            _actualLevel.PropertyChanged += (sender, e) => {
                var ventilationLevelPoint = PointData.Measurement("recuperator").Field("actual-ventilation-level", _actualLevel.Value).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                using (var influxDbWriteApi = _influxDbClient.GetWriteApi()) {
                    influxDbWriteApi.WritePoint(_influxDbBucket, _influxDbOrg, ventilationLevelPoint);
                }
            };
        }
        private static void InitializeConnections() {
            _broker.Initialize(_mqttBrokerIpAddress, (severity, message) => {
                if (severity == "Info") { Log.Info(message); } else if (severity == "Error") { Log.Error(message); } else { Log.Debug(message); }
            });
            _clientDevice.Initialize(_broker, (severity, message) => {
                if (severity == "Info") { Log.Info(message); } else if (severity == "Error") { Log.Error(message); } else { Log.Debug(message); }
            });
        }
    }
}
