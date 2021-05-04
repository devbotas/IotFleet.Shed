using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using uPLibrary.Networking.M2Mqtt;

namespace ShedMonitor {
    class ReliableBroker {
        public delegate void PublishReceivedDelegate(string topic, string payload);
        public event PublishReceivedDelegate PublishReceived;

        public bool IsConnected { get; private set; }
        public bool IsInitialized { get; private set; }

        public void Initialize(string mqttBrokerIpAddress) {
            if (IsInitialized) { return; }

            _globalCancellationTokenSource = new CancellationTokenSource();

            _mqttBrokerIp = mqttBrokerIpAddress;
            _mqttClient = new MqttClient(mqttBrokerIpAddress);
            _mqttClient.MqttMsgPublishReceived += HandlePublishReceived;
            // _mqttClient.Connect(_mqttClientGuid);
            _mqttClient.ConnectionClosed += (sender, e) => {
                _log.Error("M2Mqtt library reports a ConnectionClosed event.");
                IsConnected = false;
            };

            Task.Run(async () => await MonitorMqttConnectionContinuously(_globalCancellationTokenSource.Token));

            IsInitialized = true;
        }
        public async Task MonitorMqttConnectionContinuously(CancellationToken cancelationToken) {
            while (cancelationToken.IsCancellationRequested == false) {
                if (IsConnected == false) {
                    try {
                        _mqttClient.Connect(_mqttClientGuid);
                        IsConnected = true;
                    }
                    catch (Exception ex) {
                        _log.Error(ex, $"{nameof(MonitorMqttConnectionContinuously)} tried to connect to broker, but that did not work.");
                    }
                }
                await Task.Delay(1000, cancelationToken);
            }
        }

        public void PublishToTopic(string topic, string payload, byte qosLevel, bool isRetained) {
            if (IsConnected == false) { return; }

            var retryCount = 0;
            var isPublishSuccessful = false;
            while ((retryCount < 3) && (isPublishSuccessful == false)) {
                try {
                    _mqttClient.Publish(topic, Encoding.UTF8.GetBytes(payload), qosLevel, isRetained);
                    isPublishSuccessful = true;
                }
                catch (Exception ex) {
                    retryCount++;
                    _log.Error(ex, $"Could not publish topic {topic} to broker {_mqttBrokerIp}, attempt {retryCount}");
                }

            }

            if (isPublishSuccessful == false) {
                _log.Error($"Too many fails at publishing, going to disconnected state.");
                IsConnected = false;
            }
        }
        public void SubscribeToTopic(string topic) {
            _mqttClient.Subscribe(new string[] { topic }, new byte[] { 2 });
        }

        private CancellationTokenSource _globalCancellationTokenSource;
        private Logger _log = LogManager.GetCurrentClassLogger();
        private MqttClient _mqttClient;
        private string _mqttBrokerIp = "localhost";
        private string _mqttClientGuid = Guid.NewGuid().ToString();

        private void HandlePublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e) {
            PublishReceived(e.Topic, Encoding.UTF8.GetString(e.Message));
        }
    }
}
