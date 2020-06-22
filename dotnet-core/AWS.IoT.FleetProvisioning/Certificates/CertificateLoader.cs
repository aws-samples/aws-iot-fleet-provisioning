using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace AWS.IoT.FleetProvisioning.Certificates
{
    public class CertificateLoader : ICertificateLoader
    {
        private readonly ILogger<CertificateLoader> _logger;

        public CertificateLoader(ILogger<CertificateLoader> logger)
        {
            _logger = logger;
        }

        public X509Certificate2 LoadX509Certificate(string directory, string certificate, string privateKey)
        {
            _logger.LogDebug($"Within {nameof(LoadX509Certificate)} method.");

            _logger.LogTrace($"{nameof(directory)}: {directory}");
            _logger.LogTrace($"{nameof(certificate)}: {certificate}");
            _logger.LogTrace($"{nameof(privateKey)}: {privateKey}");

            var certificatePath = Path.Combine(directory, certificate);
            var privateKeyPath = Path.Combine(directory, privateKey);

            // thanks to:
            //    https://github.com/dotnet/runtime/issues/19581#issuecomment-581147166
            using var publicKey = new X509Certificate2(certificatePath);

            var privateKeyText = File.ReadAllText(privateKeyPath);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);
            using var rsa = RSA.Create();

            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
            {
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            }
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY")
            {
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
            }

            return publicKey.CopyWithPrivateKey(rsa);
        }
    }
}