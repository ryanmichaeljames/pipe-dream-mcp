using System.Text.Json;

namespace PipeDreamMcp.Config;

/// <summary>
/// Loads environment configuration from JSON files
/// </summary>
public class ConfigLoader
{
    private readonly string _configDirectory;

    public ConfigLoader(string? configDirectory = null)
    {
        _configDirectory = configDirectory ?? GetDefaultConfigDirectory();
        LogToStderr($"Config directory: {_configDirectory}");
    }

    /// <summary>
    /// Load configuration for specified environment
    /// </summary>
    public EnvironmentConfig LoadEnvironment(string environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            throw new ArgumentException("Environment name cannot be empty", nameof(environmentName));
        }

        var configPath = Path.Combine(_configDirectory, $"{environmentName}.json");
        
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}. Please create it or use --config-dir to specify location.");
        }

        LogToStderr($"Loading config: {configPath}");

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<EnvironmentConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (config == null)
        {
            throw new InvalidOperationException($"Failed to parse config file: {configPath}");
        }

        ValidateConfig(config);
        
        LogToStderr($"Loaded environment: {config.Environment}");
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

    /// <summary>
    /// Get default configuration directory
    /// Priority: PIPE_DREAM_MCP_CONFIG env var > ~/.pipe-dream-mcp/config > ./config
    /// </summary>
    private static string GetDefaultConfigDirectory()
    {
        // Check environment variable
        var envPath = Environment.GetEnvironmentVariable("PIPE_DREAM_MCP_CONFIG");
        if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath))
        {
            return envPath;
        }

        // Check user profile directory
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userConfigPath = Path.Combine(userProfile, ".pipe-dream-mcp", "config");
        if (Directory.Exists(userConfigPath))
        {
            return userConfigPath;
        }

        // Fallback to current directory
        var localConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "config");
        if (Directory.Exists(localConfigPath))
        {
            return localConfigPath;
        }

        // Default to local config (will fail later if not exists)
        return localConfigPath;
    }

    /// <summary>
    /// Log to stderr for debugging
    /// </summary>
    private void LogToStderr(string message)
    {
        Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConfigLoader: {message}");
    }
}
