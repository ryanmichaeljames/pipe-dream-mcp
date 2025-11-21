using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Protocol;

/// <summary>
/// Registry for discovering and executing MCP tools
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, IToolHandler> _handlers = new();

    /// <summary>
    /// Register a tool handler
    /// </summary>
    public void Register(IToolHandler handler)
    {
        _handlers[handler.ToolName] = handler;
    }

    /// <summary>
    /// Try to get a handler by tool name
    /// </summary>
    public bool TryGetHandler(string toolName, out IToolHandler? handler)
    {
        return _handlers.TryGetValue(toolName, out handler);
    }

    /// <summary>
    /// Get all registered tool definitions
    /// </summary>
    public IEnumerable<ToolDefinition> GetAllDefinitions()
    {
        return _handlers.Values.Select(h => h.Definition);
    }
}
