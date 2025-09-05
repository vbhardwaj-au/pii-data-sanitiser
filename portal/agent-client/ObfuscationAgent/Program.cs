using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ObfuscationAgent.Services;
using ObfuscationAgent.Configuration;

namespace ObfuscationAgent;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("==========================================");
        Console.WriteLine("  PII Data Obfuscation Agent v1.0");
        Console.WriteLine("==========================================");
        Console.WriteLine();

        var host = CreateHostBuilder(args).Build();

        // Handle graceful shutdown
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() =>
        {
            Console.WriteLine("\nAgent is shutting down gracefully...");
        });

        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                    .AddEnvironmentVariables("OBFUSCATION_")  // Use OBFUSCATION_ prefix
                    .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<AgentConfiguration>(context.Configuration.GetSection("Agent"));
                services.Configure<PortalConfiguration>(context.Configuration.GetSection("Portal"));
                services.Configure<StorageConfiguration>(context.Configuration.GetSection("Storage"));

                // HTTP Client for portal communication
                services.AddHttpClient<IPortalCommunicationService, PortalCommunicationService>(client =>
                {
                    var portalConfig = context.Configuration.GetSection("Portal").Get<PortalConfiguration>();
                    client.BaseAddress = new Uri(portalConfig?.BaseUrl ?? "http://localhost:7001");
                    client.DefaultRequestHeaders.Add("User-Agent", "ObfuscationAgent/1.0");
                    client.Timeout = TimeSpan.FromMinutes(5);
                });

                // Core services
                services.AddSingleton<IAgentStateService, AgentStateService>();
                services.AddScoped<IJobExecutionService, JobExecutionService>();
                services.AddScoped<IObfuscationEngineService, ObfuscationEngineService>();
                services.AddScoped<IProgressReportingService, ProgressReportingService>();

                // Background services
                services.AddHostedService<AgentWorkerService>();
                services.AddHostedService<HeartbeatService>();
                services.AddHostedService<JobPollingService>();

                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                });
            })
            .UseConsoleLifetime();
}