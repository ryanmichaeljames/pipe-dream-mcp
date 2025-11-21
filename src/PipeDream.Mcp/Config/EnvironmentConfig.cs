using System.Text.Json.Serialization;

namespace PipeDream.Mcp.Config;

/// <summary>
/// Configuration for connecting to Dataverse, Power Platform, and Azure DevOps
/// </summary>
public class EnvironmentConfig
{
    [JsonPropertyName("dataverse")]
    public DataverseConfig? Dataverse { get; set; }

    [JsonPropertyName("devops")]
    public DevOpsConfig? DevOps { get; set; }

    [JsonPropertyName("powerplatform")]
    public PowerPlatformConfig? PowerPlatform { get; set; }

    [JsonPropertyName("logging")]
    public LoggingConfig? Logging { get; set; }
}

/// <summary>
/// Dataverse connection configuration
/// </summary>
public class DataverseConfig
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "v9.2";

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Enable write operations (Create/Update - PATCH, POST)
    /// Default: false for safety
    /// </summary>
    [JsonPropertyName("enableWriteOperations")]
    public bool EnableWriteOperations { get; set; } = false;

    /// <summary>
    /// Enable delete operations (DELETE)
    /// Default: false for maximum safety - requires explicit opt-in beyond write operations
    /// </summary>
    [JsonPropertyName("enableDeleteOperations")]
    public bool EnableDeleteOperations { get; set; } = false;
}

/// <summary>
/// Azure DevOps connection configuration
/// </summary>
public class DevOpsConfig
{
    [JsonPropertyName("organization")]
    public string Organization { get; set; } = string.Empty;

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "7.0";

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 30;
}

/// <summary>
/// Logging configuration
/// </summary>
public class LoggingConfig
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = "info";
}

/// <summary>
/// Power Platform API configuration
/// </summary>
public class PowerPlatformConfig
{
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "2022-03-01-preview";

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 30;
}
