namespace AWS.IoT.FleetProvisioning.Configuration
{
    public class Settings : ISettings
    {
        public string SecureCertificatePath { get; set; }
        public string RootCertificate { get; set; }
        public string ClaimCertificate { get; set; }
        public string ClaimCertificateKey { get; set; }
        public string IotEndpoint { get; set; }
        public string ProvisioningTemplate { get; set; }
    }
}