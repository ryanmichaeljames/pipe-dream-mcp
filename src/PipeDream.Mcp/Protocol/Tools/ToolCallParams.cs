using System.Text.Json;
using System.Text.Json.Serialization;

namespace PipeDream.Mcp.Protocol.Tools;

/// <summary>
/// Tool call parameters
/// </summary>
public class ToolCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; set; }
}
