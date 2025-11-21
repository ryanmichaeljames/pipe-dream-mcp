using System.Text.Json.Serialization;

namespace PipeDream.Mcp.Protocol.Messages;

/// <summary>
/// Base JSON-RPC message structure for MCP protocol
/// </summary>
public class McpMessage
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("params")]
    public object? Params { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}
