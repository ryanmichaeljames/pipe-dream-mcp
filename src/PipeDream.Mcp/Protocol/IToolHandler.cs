using System.Text.Json;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Protocol;

/// <summary>
/// Interface for MCP tool handlers
/// </summary>
public interface IToolHandler
{
    /// <summary>
    /// Tool name (e.g., "dataverse_query")
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Tool definition for tools/list response
    /// </summary>
    ToolDefinition Definition { get; }

    /// <summary>
    /// Execute the tool with given arguments
    /// </summary>
    Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken);
}
