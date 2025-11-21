using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Auth;
using PipeDream.Mcp.Config;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Startup;

namespace PipeDream.Mcp.Commands;

/// <summary>
/// Handler for the Power Platform subcommand
/// </summary>
public class PowerPlatformCommandHandler : ICommandHandler
{
    public string CommandName => "powerplatform";
    public string Description => "Run Power Platform MCP server";

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        // Parse command line arguments
        var options = CommandLineParser.ParsePowerPlatformArgs(args);
        
        // Show help if help flag used
        if (options.ShowHelp)
        {
            ShowHelp();
            return 0;
        }

        // Load configuration
        EnvironmentConfig config;
            
        if (!string.IsNullOrWhiteSpace(options.ConfigFile))
        {
            // Direct config file path
            var configLoader = new ConfigLoader();
            config = configLoader.LoadFromFile(options.ConfigFile);
        }
        else
        {
            Console.Error.WriteLine("Error: --config-file must be specified for Power Platform");
            return 1;
        }

        // Validate Power Platform configuration
        if (config.PowerPlatform == null)
        {
            Console.Error.WriteLine("Error: Power Platform configuration section is missing in config file");
            return 1;
        }

        // Command-line args override config file values
        if (options.ApiVersion != null)
        {
            config.PowerPlatform.ApiVersion = options.ApiVersion;
        }
        if (options.Timeout.HasValue)
        {
            config.PowerPlatform.Timeout = options.Timeout.Value;
        }

        // Setup dependency injection
        var services = new ServiceCollection();
        
        // Configure logging (file-based to avoid MCP protocol conflicts)
        var logLevel = options.Verbose ? LogLevel.Debug : LogLevel.Information;
        var logFilePath = services.ConfigureLogging(logLevel, "powerplatform", string.Empty);
        
        // Show log file location on stderr (minimal output per MCP best practices)
        Console.Error.WriteLine($"Logs: {logFilePath}");

        // Register Power Platform services (includes auth, client, services, MCP server)
        services.AddPowerPlatformServices(config.PowerPlatform);

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<PowerPlatformCommandHandler>>();
        
        // Verify Azure CLI authentication
        logger.LogInformation("Verifying Azure CLI authentication...");
        var authProvider = serviceProvider.GetRequiredService<AzureAuthProvider>();
        var isAuthenticated = await authProvider.VerifyAuthenticationAsync();
        if (!isAuthenticated)
        {
            Console.Error.WriteLine("Error: Azure CLI authentication failed. Please run 'az login'");
            return 1;
        }

        // Resolve MCP server
        var server = serviceProvider.GetRequiredService<McpServer>();
        logger.LogInformation("Power Platform services initialized via DI container");
        using var cts = new CancellationTokenSource();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutting down gracefully...");
            cts.Cancel();
        };

        logger.LogInformation("MCP server started - ready to accept requests");
        logger.LogInformation("API Version: {ApiVersion}", config.PowerPlatform.ApiVersion);
        
        await server.RunAsync(cts.Token);
        
        logger.LogInformation("MCP server stopped");
        return 0;
    }

    public void ShowHelp()
    {
        HelpProvider.ShowPowerPlatformHelp();
    }
}
