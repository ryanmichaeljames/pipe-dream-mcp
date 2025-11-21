using PipeDream.Mcp.Commands;

namespace PipeDream.Mcp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Register all command handlers
            var commands = new Dictionary<string, ICommandHandler>
            {
                ["dataverse"] = new DataverseCommandHandler(),
                ["powerplatform"] = new PowerPlatformCommandHandler(),
                // Future: ["azure-devops"] = new AzureDevOpsCommandHandler()
            };
            
            // Handle no args or help
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                HelpProvider.ShowGlobalHelp();
                return 0;
            }
            
            // Handle version flag
            if (args[0] == "--version" || args[0] == "-v")
            {
                HelpProvider.ShowVersionInfo();
                return 0;
            }

            // Route to command handler
            var commandName = args[0].ToLowerInvariant();
            if (commands.TryGetValue(commandName, out var handler))
            {
                return await handler.ExecuteAsync(args.Skip(1).ToArray(), CancellationToken.None);
            }
            
            // Unknown command
            Console.Error.WriteLine($"Error: Unknown subcommand '{args[0]}'");
            Console.Error.WriteLine();
            HelpProvider.ShowGlobalHelp();
            return 1;
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
}
