using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Auth;
using PipeDream.Mcp.Config;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Startup;

namespace PipeDream.Mcp.Commands;

/// <summary>
/// Handler for the Dataverse subcommand
/// </summary>
public class DataverseCommandHandler : ICommandHandler
{
    public string CommandName => "dataverse";
    public string Description => "Run Dataverse MCP server";

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        // Parse command line arguments
        var options = CommandLineParser.ParseDataverseArgs(args);
        
        // Show help if help flag used
        if (options.ShowHelp)
        {
            ShowHelp();
            return 0;
        }

        // Load or build configuration
        EnvironmentConfig config;
            
        if (!string.IsNullOrWhiteSpace(options.DataverseUrl))
        {
            // Inline configuration via command-line arguments
            config = new EnvironmentConfig
            {
                Environment = "inline",
                Dataverse = new DataverseConfig
                {
                    Url = options.DataverseUrl,
                    ApiVersion = options.ApiVersion ?? "v9.2",
                    Timeout = options.Timeout ?? 30,
                    EnableWriteOperations = options.EnableWriteOperations,
                    EnableDeleteOperations = options.EnableDeleteOperations
                }
            };
        }
        else if (!string.IsNullOrWhiteSpace(options.ConfigFile))
        {
            // Direct config file path
            var configLoader = new ConfigLoader();
            config = configLoader.LoadFromFile(options.ConfigFile);
            
            // Command-line flags override config file values
            if (config.Dataverse != null)
            {
                if (options.EnableWriteOperations)
                {
                    config.Dataverse.EnableWriteOperations = true;
                }
                if (options.EnableDeleteOperations)
                {
                    config.Dataverse.EnableDeleteOperations = true;
                }
            }
        }
        else
        {
            Console.Error.WriteLine("Error: Either --dataverse-url or --config-file must be specified");
            return 1;
        }

        // Validate Dataverse configuration
        if (config.Dataverse == null || string.IsNullOrWhiteSpace(config.Dataverse.Url))
        {
            Console.Error.WriteLine("Error: Dataverse URL is required");
            return 1;
        }

        // Extract org name from Dataverse URL for log file naming
        var orgName = ExtractOrgNameFromUrl(config.Dataverse.Url);

        // Setup dependency injection
        var services = new ServiceCollection();
        
        // Configure logging (file-based to avoid MCP protocol conflicts)
        var logLevel = options.Verbose ? LogLevel.Debug : LogLevel.Information;
        var logFilePath = services.ConfigureLogging(logLevel, "dataverse", orgName);
        
        // Show log file location on stderr (minimal output per MCP best practices)
        Console.Error.WriteLine($"Logs: {logFilePath}");

        // Register Dataverse services (includes auth, client, query services, MCP server)
        services.AddDataverseServices(config.Dataverse);

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<DataverseCommandHandler>>();
        
        // Verify Azure CLI authentication
        logger.LogInformation("Verifying Azure CLI authentication...");
        var authProvider = serviceProvider.GetRequiredService<AzureAuthProvider>();
        var isAuthenticated = await authProvider.VerifyAuthenticationAsync();
        if (!isAuthenticated)
        {
            Console.Error.WriteLine("Error: Azure CLI authentication failed. Please run 'az login'");
            return 1;
        }

        // Test network connectivity to Dataverse
        logger.LogInformation("Testing connectivity to {DataverseUrl}...", config.Dataverse.Url);
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            await httpClient.GetAsync(config.Dataverse.Url);
            logger.LogInformation("Dataverse endpoint is reachable");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Cannot reach Dataverse endpoint: {Message}", ex.Message);
            logger.LogWarning("Continuing anyway - will retry with backoff if needed");
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "Dataverse connectivity test timed out: {Message}", ex.Message);
            logger.LogWarning("Continuing anyway - will retry with backoff if needed");
        }

        // Resolve MCP server
        var server = serviceProvider.GetRequiredService<McpServer>();
        logger.LogInformation("Dataverse services initialized via DI container");
        using var cts = new CancellationTokenSource();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutting down gracefully...");
            cts.Cancel();
        };

        logger.LogInformation("MCP server started - ready to accept requests");
        logger.LogInformation("Environment: {Environment}", config.Environment);
        logger.LogInformation("Dataverse URL: {DataverseUrl}", config.Dataverse.Url);
        logger.LogInformation("Write Operations: {WriteOps}", config.Dataverse.EnableWriteOperations ? "ENABLED" : "DISABLED");
        logger.LogInformation("Delete Operations: {DeleteOps}", config.Dataverse.EnableDeleteOperations ? "ENABLED" : "DISABLED");
        
        await server.RunAsync(cts.Token);
        
        logger.LogInformation("MCP server stopped");
        return 0;
    }

    public void ShowHelp()
    {
        HelpProvider.ShowDataverseHelp();
    }

    /// <summary>
    /// Extract organization name from Dataverse URL for log file naming.
    /// </summary>
    private static string ExtractOrgNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var hostname = uri.Host;
            
            // Extract first part before .crm (e.g., "my-org" from "my-org.crm.dynamics.com")
            var parts = hostname.Split('.');
            if (parts.Length > 0)
            {
                return parts[0];
            }
        }
        catch
        {
            // If URL parsing fails, return default
        }
        
        return "default";
    }
}
