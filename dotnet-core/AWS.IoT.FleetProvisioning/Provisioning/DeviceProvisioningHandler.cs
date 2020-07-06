using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AWS.IoT.FleetProvisioning.Configuration;
using AWS.IoT.FleetProvisioning.Extensions;
using AWS.IoT.FleetProvisioning.IoTClient;
using Microsoft.Extensions.Logging;

namespace AWS.IoT.FleetProvisioning.Provisioning
{
    public class DeviceProvisioningHandler : IDeviceProvisioningHandler
    {
        private const int LoopDelay = 500;
        private readonly Guid _clientId;

        private readonly ILogger<DeviceProvisioningHandler> _logger;
        private readonly IPermanentClient _permanentClient;
        private readonly IProvisioningClient _provisioningClient;
        private readonly ISettings _settings;

        private bool _callbackReturned;
        private object _messagePayload;
        private string _permanentCertificate;
        private string _permanentCertificateKey;

        public DeviceProvisioningHandler(ILogger<DeviceProvisioningHandler> logger, ISettings settings,
            IProvisioningClient provisioningClient, IPermanentClient permanentClient)
        {
            _logger = logger;
            _settings = settings;
            _provisioningClient = provisioningClient;
            _permanentClient = permanentClient;

            _clientId = Guid.NewGuid();
        }

        public string ThingName { get; set; }

        /// <summary>
        /// Initiates an async loop/call to kick off the provisioning flow
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public async Task BeginProvisioningFlowAsync(Action<object> callback)
        {
            _logger.LogDebug($"Within {nameof(BeginProvisioningFlowAsync)} method.");

            // Set OnMessageCallback on the provisioning client
            _provisioningClient.OnMessage(MessageCallback);

            // Connect to IoT Core with provision claim credentials
            _logger.LogInformation("##### CONNECTING WITH PROVISIONING CLAIM CERT #####");
            Console.WriteLine("##### CONNECTING WITH PROVISIONING CLAIM CERT #####");
            _provisioningClient.Connect(_clientId.ToString());

            // Monitors topics for errors
            EnableErrorMonitor();

            // Make a publish call to topic to get official certificates
            _provisioningClient.Publish("$aws/certificates/create/json", new { }, 0);

            while (!_callbackReturned)
            {
                _logger.LogDebug($"Adding a delay of {LoopDelay} milliseconds.");
                await Task.Delay(LoopDelay);
            }

            callback?.Invoke(_messagePayload);
        }

        /// <summary>
        /// Subscribe to pertinent IoTCore topics that would emit errors
        /// </summary>
        private void EnableErrorMonitor()
        {
            _logger.LogDebug($"Within {nameof(EnableErrorMonitor)} method.");

            _provisioningClient.Subscribe(
                $"$aws/provisioning-templates/{_settings.ProvisioningTemplate}/provision/json/rejected", 1,
                BasicCallback);
            _provisioningClient.Subscribe("$aws/certificates/create/json/rejected", 1, BasicCallback);
        }

        /// <summary>
        /// Callback Message handler responsible for workflow routing of msg responses from provisioning services.
        /// </summary>
        /// <param name="message">The response message payload.</param>
        private void MessageCallback(string message)
        {
            _logger.LogDebug($"Within {nameof(MessageCallback)} method.");

            var data = message.FromJson<dynamic>();

            // A response has been received from the service that contains certificate data.
            if (data.certificateId != null)
            {
                _logger.LogInformation("##### SUCCESS. SAVING KEYS TO DEVICE! #####");
                Console.WriteLine("##### SUCCESS. SAVING KEYS TO DEVICE! #####");
                AssembleCertificates(data);
            }
            else if (data.deviceConfiguration != null)
            {
                ThingName = (string) data.thingName;
                _logger.LogInformation($"##### CERT ACTIVATED AND THING {ThingName} CREATED #####");
                Console.WriteLine($"##### CERT ACTIVATED AND THING {ThingName} CREATED #####");


                ValidateCertificate();
            }
        }

        /// <summary>
        /// Method takes the payload and constructs/saves the certificate and private key. Method uses existing AWS IoT Core naming
        /// convention.
        /// </summary>
        /// <param name="data">Certifiable certificate/key data.</param>
        private void AssembleCertificates(dynamic data)
        {
            _logger.LogDebug($"Within {nameof(AssembleCertificates)} method.");

            var certificateId = data.certificateId;
            var prefix = ((string) certificateId).Substring(0, 10);

            _permanentCertificate = $"{prefix}-certificate.pem.crt";
            File.WriteAllText(Path.Combine(_settings.SecureCertificatePath, _permanentCertificate),
                (string) data.certificatePem);

            _permanentCertificateKey = $"{prefix}-private.pem.key";
            File.WriteAllText(Path.Combine(_settings.SecureCertificatePath, _permanentCertificateKey),
                (string) data.privateKey);

            RegisterThing((string) data.certificateOwnershipToken);
        }

        /// <summary>
        /// Calls the fleet provisioning service responsible for acting upon instructions within device templates.
        /// </summary>
        /// <param name="token">The token response from certificate creation to prove ownership/immediate possession of the certs.</param>
        private void RegisterThing(string token)
        {
            _logger.LogDebug($"Within {nameof(RegisterThing)} method.");

            _logger.LogInformation("##### CREATING THING ACTIVATING CERT #####");
            Console.WriteLine("##### CREATING THING ACTIVATING CERT #####");

            var registerTemplate = new
            {
                certificateOwnershipToken = token,
                parameters = new
                {
                    SerialNumber = _clientId.ToString()
                }
            };

            // Register thing / activate certificate
            _provisioningClient.Publish($"$aws/provisioning-templates/{_settings.ProvisioningTemplate}/provision/json",
                registerTemplate, 0);
        }

        /// <summary>
        /// Responsible for (re)connecting to IoTCore with the newly provisioned/activated certificate - (first class citizen cert)
        /// </summary>
        private void ValidateCertificate()
        {
            _logger.LogDebug($"Within {nameof(ValidateCertificate)} method.");
            _permanentClient.UpdateClientCredentials(_permanentCertificate, _permanentCertificateKey);

            _logger.LogInformation("##### CONNECTING WITH OFFICIAL CERT #####");
            Console.WriteLine("##### CONNECTING WITH OFFICIAL CERT #####");

            _permanentClient.Connect(ThingName);

            NewCertificatePublishAndSubscribe();
        }

        /// <summary>
        /// Method testing a call to the 'openworld' topic (which was specified in the policy for the new certificate)
        /// </summary>
        private void NewCertificatePublishAndSubscribe()
        {
            _permanentClient.Subscribe($"{ThingName}/openworld", 1, BasicCallback);

            Thread.Sleep(500);

            var payload = new
            {
                ServiceResponse = "##### RESPONSE FROM A PREVIOUSLY FORBIDDEN TOPIC #####"
            };
            _permanentClient.Publish($"{ThingName}/openworld", payload, 0);
        }

        /// <summary>
        /// Method responding to the openworld publish attempt. Demonstrating a successful pub/sub with new certificate.
        /// </summary>
        /// <param name="message"></param>
        private void BasicCallback(string message)
        {
            _logger.LogDebug($"Within {nameof(BasicCallback)} method.");
            _logger.LogDebug(message);

            _messagePayload = message;
            _callbackReturned = true;
        }
    }
}