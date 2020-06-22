using System;
using System.Threading.Tasks;
using AWS.IoT.FleetProvisioning.Extensions;
using AWS.IoT.FleetProvisioning.Provisioning;
using Microsoft.Extensions.Logging;

namespace AWS.IoT.FleetProvisioning
{
    public class ConsoleApplication
    {
        private readonly IDeviceProvisioningHandler _handler;
        private readonly ILogger<ConsoleApplication> _logger;

        public ConsoleApplication(ILogger<ConsoleApplication> logger, IDeviceProvisioningHandler handler)
        {
            _logger = logger;
            _handler = handler;
        }

        public async Task GetPermanentCertificatesAsync()
        {
            _logger.LogDebug($"Within {nameof(GetPermanentCertificatesAsync)} method.");
            await _handler.BeginProvisioningFlowAsync(Callback);
        }

        private void Callback(object message)
        {
            _logger.LogDebug($"Within {nameof(Callback)} method.");
            _logger.LogInformation($"Message: {message.ToJson()}");
            Console.WriteLine(message.ToJson());
            Console.WriteLine($"##### PROVISIONED THING NAMED '{_handler.ThingName}' SUCCESSFULLY #####");
        }
    }
}