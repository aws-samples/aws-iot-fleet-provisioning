using System;
using System.Threading.Tasks;

namespace AWS.IoT.FleetProvisioning.Provisioning
{
    public interface IDeviceProvisioningHandler
    {
        /// <summary>
        /// Initiates an async loop/call to kick off the provisioning flow
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        Task BeginProvisioningFlowAsync(Action<object> callback);

        /// <summary>
        /// Name of the provisioned thing
        /// </summary>
        string ThingName { get; set; }
    }
}