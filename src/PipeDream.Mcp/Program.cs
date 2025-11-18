using System.Reflection;
using PipeDream.Mcp.Auth;
using PipeDream.Mcp.Config;
using PipeDream.Mcp.Protocol;

namespace PipeDream.Mcp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Parse command line arguments
            var options = ParseArguments(args);
            
            // Show help if no arguments provided or help flag used
            if (args.Length == 0 || options.ShowHelp)
            {
                ShowHelp();
                return 0;
            }

            if (options.ShowVersion)
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "unknown";
                var frameworkVersion = Environment.Version;
                Console.WriteLine("PipeDream MCP");
                Console.WriteLine($"Version: {version} (.NET {frameworkVersion})");
                return 0;
            }

            // Load or build configuration
            EnvironmentConfig config;
            
            if (!string.IsNullOrWhiteSpace(options.DataverseUrl))
            {
                // Inline configuration via command-line arguments
                Console.Error.WriteLine($"Using inline configuration");
                config = new EnvironmentConfig
                {
                    Environment = "inline",
                    Dataverse = new DataverseConfig
                    {
                        Url = options.DataverseUrl,
                        ApiVersion = options.ApiVersion ?? "v9.2",
                        Timeout = options.Timeout ?? 30
                    }
                };
            }
            else if (!string.IsNullOrWhiteSpace(options.ConfigFile))
            {
                // Direct config file path
                Console.Error.WriteLine($"Loading configuration from: {options.ConfigFile}");
                var configLoader = new ConfigLoader();
                config = configLoader.LoadFromFile(options.ConfigFile);
                Console.Error.WriteLine($"Configuration loaded successfully");
            }
            else
            {
                Console.Error.WriteLine("Error: Either --dataverse-url or --config-file must be specified");
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  pipe-dream-mcp --dataverse-url <url>");
                Console.Error.WriteLine("  pipe-dream-mcp --config-file <path>");
                Console.Error.WriteLine("Run 'pipe-dream-mcp --help' for more information");
                return 1;
            }

            // Validate Dataverse configuration
            if (config.Dataverse == null || string.IsNullOrWhiteSpace(config.Dataverse.Url))
            {
                Console.Error.WriteLine("Error: Dataverse URL is required");
                Console.Error.WriteLine("Specify via --dataverse-url or in config file");
                return 1;
            }

            // Initialize authentication
            var authProvider = new AzureAuthProvider();
            
            // Verify Azure CLI authentication
            Console.Error.WriteLine("Verifying Azure CLI authentication...");
            var isAuthenticated = await authProvider.VerifyAuthenticationAsync();
            if (!isAuthenticated)
            {
                Console.Error.WriteLine("Error: Azure CLI authentication failed");
                Console.Error.WriteLine("Please run 'az login' to authenticate");
                return 1;
            }

            // Test network connectivity to Dataverse
            Console.Error.WriteLine($"Testing connectivity to {config.Dataverse.Url}...");
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                await httpClient.GetAsync(config.Dataverse.Url);
                Console.Error.WriteLine($"Dataverse endpoint is reachable");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Warning: Cannot reach Dataverse endpoint: {ex.Message}");
                Console.Error.WriteLine("Continuing anyway - will retry with backoff if needed");
            }
            catch (TaskCanceledException ex)
            {
                Console.Error.WriteLine($"Warning: Dataverse connectivity test timed out: {ex.Message}");
                Console.Error.WriteLine("Continuing anyway - will retry with backoff if needed");
            }

            // Initialize Dataverse client
            var dataverseClient = new Dataverse.DataverseClient(authProvider, config.Dataverse);
            Console.Error.WriteLine("Dataverse client initialized");

            // Create and run MCP server
            var server = new McpServer(dataverseClient);
            using var cts = new CancellationTokenSource();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.Error.WriteLine("\nShutting down gracefully...");
                cts.Cancel();
            };

            Console.Error.WriteLine("MCP server started - ready to accept requests");
            Console.Error.WriteLine($"Environment: {config.Environment}");
            Console.Error.WriteLine($"Dataverse URL: {config.Dataverse.Url}");
            Console.Error.WriteLine("Press Ctrl+C to stop");
            
            await server.RunAsync(cts.Token);
            
            Console.Error.WriteLine("MCP server stopped");
            return 0;
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
                case "--version":
                case "-v":
                    options.ShowVersion = true;
                    break;
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
            }
        }

        return options;
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
        Console.WriteLine("  pipedream-mcp --dataverse-url <url> [options]");
        Console.WriteLine("  pipedream-mcp --config-file <path>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --dataverse-url, -u <url>   Dataverse instance URL (inline config)");
        Console.WriteLine("  --api-version, -a <ver>     API version (default: v9.2)");
        Console.WriteLine("  --timeout, -t <seconds>     Request timeout (default: 30)");
        Console.WriteLine("  --config-file, -c <path>    Path to config JSON file");
        Console.WriteLine("  --version, -v               Show version information");
        Console.WriteLine("  --help, -h                  Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Inline configuration:");
        Console.WriteLine("    pipedream-mcp -u https://org.crm.dynamics.com/");
        Console.WriteLine("    pipedream-mcp -u https://org.crm.dynamics.com/ -t 60");
        Console.WriteLine();
        Console.WriteLine("  Config file:");
        Console.WriteLine("    pipedream-mcp -c C:/configs/prod.json");
    }

    private class CommandLineOptions
    {
        public string? DataverseUrl { get; set; }
        public string? ConfigFile { get; set; }
        public string? ApiVersion { get; set; }
        public int? Timeout { get; set; }
        public bool ShowVersion { get; set; }
        public bool ShowHelp { get; set; }
    }
}
