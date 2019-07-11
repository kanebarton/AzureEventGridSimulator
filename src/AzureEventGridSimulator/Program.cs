using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var host = WebHost.CreateDefaultBuilder()
                                  .UseSimulatorSettings()
                                  .UseStartup<Startup>()
                                  .ConfigureLogging((hostingContext, logging) =>
                                  {
                                      logging.AddConsole(options =>
                                      {
                                          options.IncludeScopes = true;
                                          options.DisableColors = false;
                                      });
                                      logging.AddDebug();

                                      logging.SetMinimumLevel(LogLevel.Debug);

                                      logging.AddFilter("System", LogLevel.Warning);
                                      logging.AddFilter("Microsoft", LogLevel.Warning);
                                  })
                                  .UseKestrel(options =>
                                  {
                                      var simulatorSettings = (SimulatorSettings)options.ApplicationServices.GetService(typeof(SimulatorSettings));

                                      using (var store = new X509Store(StoreName.Root))
                                      {
                                          store.Open(OpenFlags.ReadOnly);

                                          var certs = store.Certificates.Find(X509FindType.FindBySubjectName, "localhost", false);
                                          if (certs.Count > 0)
                                          {
                                              var certificate = certs[0];

                                              foreach (var topics in simulatorSettings.Topics)
                                              {

                                                  options.Listen(IPAddress.Loopback, topics.Port, listenOptions =>
                                                  {
                                                      listenOptions.UseHttps(certificate);
                                                  });
                                              }
                                          }
                                          else
                                          {
                                              throw new ApplicationException("You do not have a .NET Core DEV certificate in your trusted root certificate store. Please run `dotnet dev-certs https --trust`");
                                          }
                                      }
                                  })
                                  .Build();

                var logger = (ILogger)host.Services.GetService(typeof(ILogger));
                logger.LogInformation("Started");
                logger.LogCritical("When connecting make sure you use localhost (not 127.0.0.1)");

                try
                {
                    host.Run();
                }
                catch (Exception ex)
                {
                    logger.LogError("Failed to run the Azure Event Grid Simulator: {ErrorMessage}.", ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("");
            Console.WriteLine("Any key to exit...");
            Console.ReadKey();
        }
    }
}
