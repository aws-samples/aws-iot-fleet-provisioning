using System.Security.Cryptography.X509Certificates;

namespace AWS.IoT.FleetProvisioning.Certificates
{
    public interface ICertificateLoader
    {
        X509Certificate2 LoadX509Certificate(string directory, string certificate, string privateKey);
    }
}