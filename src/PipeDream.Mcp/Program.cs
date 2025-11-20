using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Auth;
using PipeDream.Mcp.Config;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Startup;

namespace PipeDream.Mcp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Check for subcommand
            if (args.Length == 0)
            {
                ShowHelp();
                return 0;
            }

            var subcommand = args[0];
            
            // Handle global flags before subcommand routing
            if (subcommand == "--version" || subcommand == "-v")
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "unknown";
                var frameworkVersion = Environment.Version;
                Console.WriteLine("PipeDream MCP");
                Console.WriteLine($"Version: {version} (.NET {frameworkVersion})");
                return 0;
            }
            
            if (subcommand == "--help" || subcommand == "-h")
            {
                ShowHelp();
                return 0;
            }

            // Route to subcommand handler
            var subcommandArgs = args.Skip(1).ToArray();
            
            return subcommand.ToLowerInvariant() switch
            {
                "dataverse" => await RunDataverseAsync(subcommandArgs),
                "azure-devops" => await RunAzureDevOpsAsync(subcommandArgs),
                _ => HandleInvalidSubcommand(subcommand)
            };
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static int HandleInvalidSubcommand(string subcommand)
    {
        Console.Error.WriteLine($"Error: Unknown subcommand '{subcommand}'");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Available subcommands:");
        Console.Error.WriteLine("  dataverse       - Run Dataverse MCP server");
        Console.Error.WriteLine("  azure-devops    - Run Azure DevOps MCP server (coming soon)");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Run 'pipedream-mcp --help' for more information");
        return 1;
    }

    private static async Task<int> RunDataverseAsync(string[] args)
    {
        // Parse command line arguments
        var options = ParseArguments(args);
        
        // Show help if help flag used
        if (options.ShowHelp)
        {
            ShowDataverseHelp();
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
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            
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

    private static Task<int> RunAzureDevOpsAsync(string[] args)
    {
        Console.Error.WriteLine("Azure DevOps subcommand is not yet implemented");
        Console.Error.WriteLine("Coming soon in a future release!");
        return Task.FromResult(1);
    }

    private static CommandLineOptions ParseArguments(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dataverse-url" when i + 1 < args.Length:
                case "-u" when i + 1 < args.Length:
                    options.DataverseUrl = args[++i];
                    break;
                case "--config-file" when i + 1 < args.Length:
                case "-c" when i + 1 < args.Length:
                    options.ConfigFile = args[++i];
                    break;
                case "--api-version" when i + 1 < args.Length:
                case "-a" when i + 1 < args.Length:
                    options.ApiVersion = args[++i];
                    break;
                case "--timeout" when i + 1 < args.Length:
                case "-t" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out int timeout))
                    {
                        options.Timeout = timeout;
                    }
                    break;
                case "--enable-write-operations":
                    options.EnableWriteOperations = true;
                    break;
                case "--enable-delete-operations":
                    options.EnableDeleteOperations = true;
                    break;
                case "--version":
                case "-v":
                    options.ShowVersion = true;
                    break;
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
            }
        }

        return options;
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
            
            // Extract first part before .crm (e.g., "teaureka-coredev" from "teaureka-coredev.crm.dynamics.com")
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

    private static void ShowHelp()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
        
        var frameworkVersion = Environment.Version;
        
        Console.WriteLine("PipeDream MCP");
        Console.WriteLine($"Version: {version} (.NET {frameworkVersion})");
        Console.WriteLine("Online documentation: https://github.com/ryanmichaeljames/pipe-dream-mcp");
        Console.WriteLine("Feedback, Suggestions, Issues: https://github.com/ryanmichaeljames/pipe-dream-mcp/issues");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  pipedream-mcp <subcommand> [options]");
        Console.WriteLine();
        Console.WriteLine("Subcommands:");
        Console.WriteLine("  dataverse       Run Dataverse MCP server");
        Console.WriteLine("  azure-devops    Run Azure DevOps MCP server (coming soon)");
        Console.WriteLine();
        Console.WriteLine("Global Options:");
        Console.WriteLine("  --version, -v   Show version information");
        Console.WriteLine("  --help, -h      Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  pipedream-mcp dataverse --dataverse-url https://org.crm.dynamics.com/");
        Console.WriteLine("  pipedream-mcp dataverse --config-file C:/configs/prod.json");
        Console.WriteLine("  pipedream-mcp azure-devops --organization myorg --project myproject");
        Console.WriteLine();
        Console.WriteLine("For subcommand-specific help:");
        Console.WriteLine("  pipedream-mcp dataverse --help");
        Console.WriteLine("  pipedream-mcp azure-devops --help");
    }

    private static void ShowDataverseHelp()
    {
        Console.WriteLine("PipeDream MCP - Dataverse Subcommand");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  pipedream-mcp dataverse --dataverse-url <url> [options]");
        Console.WriteLine("  pipedream-mcp dataverse --config-file <path>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --dataverse-url, -u <url>       Dataverse instance URL (inline config)");
        Console.WriteLine("  --api-version, -a <ver>         API version (default: v9.2)");
        Console.WriteLine("  --timeout, -t <seconds>         Request timeout (default: 30)");
        Console.WriteLine("  --enable-write-operations       Enable Create/Update operations (default: false)");
        Console.WriteLine("  --enable-delete-operations      Enable Delete operations (default: false)");
        Console.WriteLine("  --config-file, -c <path>        Path to config JSON file");
        Console.WriteLine("  --verbose                       Enable debug logging");
        Console.WriteLine("  --help, -h                      Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  pipedream-mcp dataverse -u https://org.crm.dynamics.com/");
        Console.WriteLine("  pipedream-mcp dataverse -c C:/configs/prod.json --enable-write-operations");
    }

    private class CommandLineOptions
    {
        public string? DataverseUrl { get; set; }
        public string? ConfigFile { get; set; }
        public string? ApiVersion { get; set; }
        public int? Timeout { get; set; }
        public bool EnableWriteOperations { get; set; }
        public bool EnableDeleteOperations { get; set; }
        public bool ShowVersion { get; set; }
        public bool ShowHelp { get; set; }
        public bool Verbose { get; set; }
    }
}
