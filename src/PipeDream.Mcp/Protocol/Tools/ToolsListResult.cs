using System.Text.Json.Serialization;

namespace PipeDream.Mcp.Protocol.Tools;

/// <summary>
/// Tools list result
/// </summary>
public class ToolsListResult
{
    [JsonPropertyName("tools")]
    public List<ToolDefinition> Tools { get; set; } = new();
}
