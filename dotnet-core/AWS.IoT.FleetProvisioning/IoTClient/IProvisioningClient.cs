using System;

namespace AWS.IoT.FleetProvisioning.IoTClient
{
    public interface IProvisioningClient
    {
        void OnMessage(Action<string> callback);
        void Connect(string clientId);
        void Subscribe(string topic, int qos, Action<string> callback);
        void Publish(string topic, object payload, int qos);
    }
}