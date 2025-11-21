using System.Text.Json.Serialization;

namespace PipeDream.Mcp.Protocol.Initialize;

/// <summary>
/// Initialize result
/// </summary>
public class InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; set; } = new();

    [JsonPropertyName("serverInfo")]
    public ServerInfo ServerInfo { get; set; } = new();
}

/// <summary>
/// Server capabilities
/// </summary>
public class ServerCapabilities
{
    [JsonPropertyName("tools")]
    public ToolsCapability? Tools { get; set; } = new();
}

/// <summary>
/// Tools capability
/// </summary>
public class ToolsCapability
{
}

/// <summary>
/// Server information
/// </summary>
public class ServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "pipe-dream-mcp";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.1.0";
}
