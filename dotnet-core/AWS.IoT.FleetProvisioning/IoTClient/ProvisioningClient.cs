using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AWS.IoT.FleetProvisioning.Certificates;
using AWS.IoT.FleetProvisioning.Configuration;
using AWS.IoT.FleetProvisioning.Extensions;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AWS.IoT.FleetProvisioning.IoTClient
{
    public class ProvisioningClient : IProvisioningClient
    {
        private readonly ICertificateLoader _certificateLoader;
        private readonly ILogger<ProvisioningClient> _logger;
        private readonly ISettings _settings;
        private Action<string> _messageCallback;
        private readonly Dictionary<string, Action<string>> _subscribeCallbackDictionary = new Dictionary<string, Action<string>>();

        public ProvisioningClient(ILogger<ProvisioningClient> logger, ISettings settings,
            ICertificateLoader certificateLoader)
        {
            _logger = logger;
            _settings = settings;
            _certificateLoader = certificateLoader;

            InitializeClient(settings.IotEndpoint, settings.SecureCertificatePath, settings.RootCertificate,
                settings.ClaimCertificate, settings.ClaimCertificateKey);
        }

        private MqttClient MqttClient { get; set; }

        /// <summary>
        /// Callback that gets called when the client receives a new message. The callback registration should happen before
        /// calling connect/connectAsync. This callback, if present, will always be triggered regardless of whether there is any
        /// message callback registered upon subscribe API call. It is for the purpose to aggregating the processing of received
        /// messages in one function.
        /// </summary>
        /// <param name="callback"></param>
        public void OnMessage(Action<string> callback)
        {
            _logger.LogDebug($"Within {nameof(OnMessage)} method.");
            _messageCallback = callback;
        }

        /// <summary>
        /// Method used to connect to connect to AWS IoTCore Service. Endpoint collected from config.
        /// </summary>
        /// <param name="clientId">A unique identifier used for the MQTT connection</param>
        public void Connect(string clientId)
        {
            _logger.LogDebug($"Within {nameof(Connect)} method.");

            _logger.LogTrace($"{nameof(clientId)}: {clientId}");

            MqttClient.Connect(clientId.ToString());

            _logger.LogDebug($"Connected to AWS IoT with client id: {clientId}");
        }

        /// <summary>
        /// Subscribe to the desired topic and register a callback.
        /// </summary>
        /// <param name="topic">Topic name or filter to subscribe to.</param>
        /// <param name="qos">Quality of Service. Could be 0 or 1.</param>
        /// <param name="callback">
        /// Function to be called when a new message for the subscribed topic comes in.
        /// </param>
        public void Subscribe(string topic, int qos, Action<string> callback)
        {
            _logger.LogDebug($"Within {nameof(Subscribe)} method.");

            _logger.LogTrace($"{nameof(topic)}: {topic}");
            _logger.LogTrace($"{nameof(qos)}: {qos}");

            _subscribeCallbackDictionary[topic] = callback;

            MqttClient.Subscribe(new[] {topic}, new[] {(byte) qos});
        }

        /// <summary>
        /// Publish a new message to the desired topic with QoS.
        /// </summary>
        /// <param name="topic">Topic name to publish to.</param>
        /// <param name="payload">Payload to publish.</param>
        /// <param name="qos">Quality of Service. Could be 0 or 1.</param>
        public void Publish(string topic, object payload, int qos)
        {
            _logger.LogDebug($"Within {nameof(Publish)} method.");

            _logger.LogTrace($"{nameof(topic)}: {topic}");
            _logger.LogTrace($"{nameof(qos)}: {qos}");

            var message = payload.ToJson(true, new JsonSerializerSettings());

            _logger.LogTrace($"{nameof(message)}: {message}");

            MqttClient.Publish(topic, Encoding.UTF8.GetBytes(message), (byte) qos, false);
        }

        /// <summary>
        /// Method used by the derived class `PermanentClient` to apply the permanent certificate.
        /// </summary>
        /// <param name="certificate">Name of the permanent certificate file (*.pem.crt)</param>
        /// <param name="certificateKey">Name of the private key file (*.pem.key)</param>
        protected void UpdateClient(string certificate, string certificateKey)
        {
            MqttClient.MqttMsgPublished -= ClientOnMqttMsgPublished;
            MqttClient.MqttMsgSubscribed -= ClientOnMqttMsgSubscribed;
            MqttClient.MqttMsgUnsubscribed -= ClientOnMqttMsgUnsubscribed;
            MqttClient.MqttMsgPublishReceived -= ClientOnMqttMsgPublishReceived;

            InitializeClient(_settings.IotEndpoint, _settings.SecureCertificatePath, _settings.RootCertificate,
                certificate, certificateKey);
        }

        private void InitializeClient(string iotEndpoint, string certificatePath, string rootCertificate,
            string certificate, string certificateKey)
        {
            var caCert = X509Certificate.CreateFromCertFile(Path.Join(certificatePath, rootCertificate));
            var clientCert = _certificateLoader.LoadX509Certificate(certificatePath, certificate, certificateKey);

            MqttClient = new MqttClient(iotEndpoint, 8883, true, caCert, clientCert, MqttSslProtocols.TLSv1_2);

            MqttClient.MqttMsgPublished += ClientOnMqttMsgPublished;
            MqttClient.MqttMsgSubscribed += ClientOnMqttMsgSubscribed;
            MqttClient.MqttMsgUnsubscribed += ClientOnMqttMsgUnsubscribed;
            MqttClient.MqttMsgPublishReceived += ClientOnMqttMsgPublishReceived;
        }

        private void ClientOnMqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            _logger.LogTrace($"OnMqttMsgPublished triggered with sender: '{sender.ToJson()}', e: '{e.ToJson()}'");
        }

        private void ClientOnMqttMsgSubscribed(object sender, MqttMsgSubscribedEventArgs e)
        {
            _logger.LogTrace($"OnMqttMsgSubscribed triggered with sender: '{sender.ToJson()}', e: '{e.ToJson()}'");
        }

        private void ClientOnMqttMsgUnsubscribed(object sender, MqttMsgUnsubscribedEventArgs e)
        {
            _logger.LogTrace($"OnMqttMsgUnsubscribed triggered with sender: '{sender.ToJson()}', e: '{e.ToJson()}'");
        }

        private void ClientOnMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            _logger.LogTrace($"OnMqttMsgPublishReceived on topic: '{e.Topic}' with QoS level '{e.QosLevel}'");

            var message = Encoding.UTF8.GetString(e.Message);
            _logger.LogTrace($"{nameof(message)}: {message}");

            _messageCallback?.Invoke(message);

            if (_subscribeCallbackDictionary.ContainsKey(e.Topic))
            {
                _subscribeCallbackDictionary[e.Topic].Invoke(message);
            }
        }
    }
}