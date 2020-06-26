using System;
using System.IO;
using System.Threading.Tasks;
using AWS.IoT.FleetProvisioning.Certificates;
using AWS.IoT.FleetProvisioning.Configuration;
using AWS.IoT.FleetProvisioning.IoTClient;
using AWS.IoT.FleetProvisioning.Provisioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AWS.IoT.FleetProvisioning
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var serviceProvider = CreateHostBuilder(args).Build().Services;

                await serviceProvider
                    .GetService<ConsoleApplication>()
                    .GetPermanentCertificatesAsync();
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("### Bootstrap cert non-existent. Official cert may already be in place. ###");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadKey(true);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Program completed... Press Ctrl+C to exit.");
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            ISettings settings = null;

            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.SetMinimumLevel(LogLevel.None);
                    loggingBuilder.AddConsole();
                })
                .ConfigureAppConfiguration(builder =>
                {
                    var configurationRoot = builder.Build();
                    var configurationSection = configurationRoot.GetSection(nameof(Settings));
                    settings = configurationSection.Get<Settings>();
                })
                .ConfigureServices(services =>
                {
                    // Add settings to our DI container for later uses
                    services.AddSingleton(settings);

                    services.AddTransient<ICertificateLoader, CertificateLoader>();
                    services.AddTransient<IProvisioningClient, ProvisioningClient>();
                    services.AddTransient<IPermanentClient, PermanentClient>();
                    services.AddTransient<IDeviceProvisioningHandler, DeviceProvisioningHandler>();

                    // IMPORTANT! Register our application entry point
                    services.AddTransient<ConsoleApplication>();
                });
        }
    }
}