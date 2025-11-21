using System.Text.Json.Serialization;

namespace PipeDream.Mcp.Protocol.Messages;

/// <summary>
/// MCP error structure
/// </summary>
public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
