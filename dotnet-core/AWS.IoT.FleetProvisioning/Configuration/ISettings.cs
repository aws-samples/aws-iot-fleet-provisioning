namespace AWS.IoT.FleetProvisioning.Configuration
{
    public interface ISettings
    {
        string SecureCertificatePath { get; set; }
        string RootCertificate { get; set; }
        string ClaimCertificate { get; set; }
        string ClaimCertificateKey { get; set; }
        string IotEndpoint { get; set; }
        string ProvisioningTemplate { get; set; }
    }
}