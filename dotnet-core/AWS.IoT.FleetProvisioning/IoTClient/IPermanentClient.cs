namespace AWS.IoT.FleetProvisioning.IoTClient
{
    public interface IPermanentClient : IProvisioningClient
    {
        void UpdateClientCredentials(string permanentCertificate, string permanentCertificateKey);
    }
}