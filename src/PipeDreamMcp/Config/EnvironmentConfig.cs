using System.Text.Json.Serialization;

namespace PipeDreamMcp.Config;

/// <summary>
/// Configuration for a specific environment (dev/test/prod)
/// </summary>
public class EnvironmentConfig
{
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [JsonPropertyName("dataverse")]
    public DataverseConfig? Dataverse { get; set; }

    [JsonPropertyName("devops")]
    public DevOpsConfig? DevOps { get; set; }

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
