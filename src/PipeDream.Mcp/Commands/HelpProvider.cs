using System.Reflection;

namespace PipeDream.Mcp.Commands;

/// <summary>
/// Provides help text and version information for the application
/// </summary>
public static class HelpProvider
{
    /// <summary>
    /// Show global help with all available commands
    /// </summary>
    public static void ShowGlobalHelp()
    {
        var version = GetVersionString();
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
        Console.WriteLine("  powerplatform   Run Power Platform MCP server");
        Console.WriteLine("  azure-devops    Run Azure DevOps MCP server (coming soon)");
        Console.WriteLine();
        Console.WriteLine("Global Options:");
        Console.WriteLine("  --version, -v   Show version information");
        Console.WriteLine("  --help, -h      Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  pipedream-mcp dataverse --dataverse-url https://org.crm.dynamics.com/");
        Console.WriteLine("  pipedream-mcp dataverse --config-file C:/configs/prod.json");
        Console.WriteLine("  pipedream-mcp powerplatform --config-file C:/configs/prod.json");
        Console.WriteLine("  pipedream-mcp azure-devops --organization myorg --project myproject");
        Console.WriteLine();
        Console.WriteLine("For subcommand-specific help:");
        Console.WriteLine("  pipedream-mcp dataverse --help");
        Console.WriteLine("  pipedream-mcp powerplatform --help");
        Console.WriteLine("  pipedream-mcp azure-devops --help");
    }

    /// <summary>
    /// Show version information
    /// </summary>
    public static void ShowVersionInfo()
    {
        var version = GetVersionString();
        var frameworkVersion = Environment.Version;
        Console.WriteLine("PipeDream MCP");
        Console.WriteLine($"Version: {version} (.NET {frameworkVersion})");
    }

    /// <summary>
    /// Show help for Dataverse subcommand
    /// </summary>
    public static void ShowDataverseHelp()
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

    /// <summary>
    /// Show help for Power Platform subcommand
    /// </summary>
    public static void ShowPowerPlatformHelp()
    {
        Console.WriteLine("PipeDream MCP - Power Platform Subcommand");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  pipedream-mcp powerplatform --config-file <path>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --config-file, -c <path>        Path to config JSON file (required)");
        Console.WriteLine("  --api-version, -a <ver>         API version (default: 2022-03-01-preview)");
        Console.WriteLine("  --timeout, -t <seconds>         Request timeout (default: 30)");
        Console.WriteLine("  --verbose                       Enable debug logging");
        Console.WriteLine("  --help, -h                      Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  pipedream-mcp powerplatform -c C:/configs/prod.json");
        Console.WriteLine("  pipedream-mcp powerplatform -c ./config.json --verbose");
    }

    private static string GetVersionString()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
    }
}
