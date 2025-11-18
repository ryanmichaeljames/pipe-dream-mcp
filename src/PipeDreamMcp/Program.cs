using System.Reflection;
using PipeDreamMcp.Auth;
using PipeDreamMcp.Config;
using PipeDreamMcp.Protocol;

namespace PipeDreamMcp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Parse command line arguments
            var options = ParseArguments(args);
            
            if (options.ShowHelp)
            {
                ShowHelp();
                return 0;
            }

            if (options.ShowVersion)
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "unknown";
                Console.WriteLine($"pipe-dream-mcp v{version}");
                return 0;
            }

            if (string.IsNullOrWhiteSpace(options.Environment))
            {
                Console.Error.WriteLine("Error: --environment parameter is required");
                Console.Error.WriteLine("Usage: pipe-dream-mcp --environment <dev|test|prod>");
                Console.Error.WriteLine("Run 'pipe-dream-mcp --help' for more information");
                return 1;
            }

            // Load configuration
            Console.Error.WriteLine($"Loading configuration for environment: {options.Environment}");
            var configLoader = new ConfigLoader(options.ConfigDirectory);
            var config = configLoader.LoadEnvironment(options.Environment);
            Console.Error.WriteLine($"Configuration loaded successfully");

            // Validate Dataverse configuration
            if (config.Dataverse == null)
            {
                Console.Error.WriteLine("Error: Dataverse configuration is missing");
                Console.Error.WriteLine($"Please add 'dataverse' section to config file for environment '{options.Environment}'");
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
                var response = await httpClient.GetAsync(config.Dataverse.Url);
                Console.Error.WriteLine($"Dataverse endpoint is reachable");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Cannot reach Dataverse endpoint: {ex.Message}");
                Console.Error.WriteLine("Continuing anyway - will retry with backoff if needed");
            }

            // Initialize Dataverse client
            var dataverseClient = new Dataverse.DataverseClient(authProvider, config.Dataverse);
            Console.Error.WriteLine("Dataverse client initialized");

            // Create and run MCP server
            var server = new McpServer(dataverseClient);
            var cts = new CancellationTokenSource();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.Error.WriteLine("\nShutting down gracefully...");
                cts.Cancel();
            };

            Console.Error.WriteLine("MCP server started - ready to accept requests");
            Console.Error.WriteLine($"Environment: {options.Environment}");
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
                case "--environment" when i + 1 < args.Length:
                    options.Environment = args[++i];
                    break;
                case "--config-dir" when i + 1 < args.Length:
                    options.ConfigDirectory = args[++i];
                    break;
                case "--version":
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
        Console.WriteLine("pipe-dream-mcp - Model Context Protocol server for Dataverse and DevOps");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  pipe-dream-mcp --environment <name> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --environment <name>    Environment to use (required: dev, test, prod, etc.)");
        Console.WriteLine("  --config-dir <path>     Config directory path (optional)");
        Console.WriteLine("  --version               Show version information");
        Console.WriteLine("  --help, -h              Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  pipe-dream-mcp --environment dev");
        Console.WriteLine("  pipe-dream-mcp --environment prod --config-dir C:/custom/configs");
        Console.WriteLine();
        Console.WriteLine("Config file location priority:");
        Console.WriteLine("  1. --config-dir argument");
        Console.WriteLine("  2. PIPE_DREAM_MCP_CONFIG environment variable");
        Console.WriteLine("  3. ~/.pipe-dream-mcp/config/");
        Console.WriteLine("  4. ./config/");
    }

    private class CommandLineOptions
    {
        public string? Environment { get; set; }
        public string? ConfigDirectory { get; set; }
        public bool ShowVersion { get; set; }
        public bool ShowHelp { get; set; }
    }
}
