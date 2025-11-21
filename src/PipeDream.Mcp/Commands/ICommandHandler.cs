namespace PipeDream.Mcp.Commands;

/// <summary>
/// Interface for application subcommand handlers
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Command name (e.g., "dataverse")
    /// </summary>
    string CommandName { get; }

    /// <summary>
    /// Brief description of the command
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Execute the command with given arguments
    /// </summary>
    Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken);

    /// <summary>
    /// Show help text for this command
    /// </summary>
    void ShowHelp();
}
