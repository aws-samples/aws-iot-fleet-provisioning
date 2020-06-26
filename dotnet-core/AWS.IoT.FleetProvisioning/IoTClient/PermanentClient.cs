using AWS.IoT.FleetProvisioning.Certificates;
using AWS.IoT.FleetProvisioning.Configuration;
using Microsoft.Extensions.Logging;

namespace AWS.IoT.FleetProvisioning.IoTClient
{
    public class PermanentClient : ProvisioningClient, IPermanentClient
    {
        private readonly ILogger<PermanentClient> _logger;

        public PermanentClient(ILogger<PermanentClient> logger, ISettings settings,
            ICertificateLoader certificateLoader) : base(logger, settings, certificateLoader)
        {
            _logger = logger;
        }

        public void UpdateClientCredentials(string permanentCertificate, string permanentCertificateKey)
        {
            _logger.LogDebug($"Withing {nameof(UpdateClientCredentials)} method.");

            _logger.LogTrace($"{nameof(permanentCertificate)}: {permanentCertificate}");
            _logger.LogTrace($"{nameof(permanentCertificateKey)}: {permanentCertificateKey}");

            UpdateClient(permanentCertificate, permanentCertificateKey);
        }
    }
}