using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PipeDream.Mcp.Config;

/// <summary>
/// Loads environment configuration from JSON files
/// </summary>
public class ConfigLoader
{
    private readonly ILogger<ConfigLoader>? _logger;

    public ConfigLoader(ILogger<ConfigLoader>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load configuration from a specific file path
    /// </summary>
    public EnvironmentConfig LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Config file not found: {filePath}");
        }

        _logger?.LogInformation("Loading config: {FilePath}", filePath);

        var json = File.ReadAllText(filePath);
        var config = JsonSerializer.Deserialize<EnvironmentConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (config == null)
        {
            throw new InvalidOperationException($"Failed to parse config file: {filePath}");
        }

        ValidateConfig(config);
        
        _logger?.LogInformation("Loaded environment: {Environment}", config.Environment);
        if (config.Dataverse != null)
        {
            _logger?.LogInformation("Dataverse Write Operations: {WriteOps}", config.Dataverse.EnableWriteOperations ? "ENABLED" : "DISABLED");
            _logger?.LogInformation("Dataverse Delete Operations: {DeleteOps}", config.Dataverse.EnableDeleteOperations ? "ENABLED" : "DISABLED");
        }
        return config;
    }

    /// <summary>
    /// Validate required configuration fields
    /// </summary>
    private void ValidateConfig(EnvironmentConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Environment))
        {
            throw new InvalidOperationException("Config missing 'environment' field");
        }

        if (config.Dataverse != null)
        {
            if (string.IsNullOrWhiteSpace(config.Dataverse.Url))
            {
                throw new InvalidOperationException("Dataverse config missing 'url' field");
            }

            if (!Uri.TryCreate(config.Dataverse.Url, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"Dataverse URL is invalid: {config.Dataverse.Url}");
            }
        }

        if (config.DevOps != null)
        {
            if (string.IsNullOrWhiteSpace(config.DevOps.Organization))
            {
                throw new InvalidOperationException("DevOps config missing 'organization' field");
            }
        }
    }
}
