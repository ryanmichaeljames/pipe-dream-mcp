using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PipeDream.Mcp.Startup;

/// <summary>
/// Extension methods for configuring logging services.
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures file-based logging with per-environment log files.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="logLevel">The minimum log level. Defaults to Information.</param>
    /// <param name="subcommand">The subcommand (e.g., "dataverse", "devops").</param>
    /// <param name="identifier">The environment identifier (e.g., org name from URL).</param>
    /// <returns>The absolute path to the log file.</returns>
    public static string ConfigureLogging(
        this IServiceCollection services,
        LogLevel logLevel,
        string subcommand,
        string identifier)
    {
        // Determine log directory (logs subfolder in app directory)
        var appDirectory = AppContext.BaseDirectory;
        var logDirectory = Path.Combine(appDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        // Sanitize identifier for filename (remove invalid characters)
        var sanitizedIdentifier = string.IsNullOrWhiteSpace(identifier) ? string.Empty : SanitizeFileName(identifier);

        // Log file path - Serilog inserts date before .log extension (e.g., filename.log becomes filename-20251121.log)
        var logFilePrefix = string.IsNullOrWhiteSpace(sanitizedIdentifier) 
            ? $"pipedream-mcp-{subcommand}"
            : $"pipedream-mcp-{subcommand}-{sanitizedIdentifier}";
        var logFilePath = Path.Combine(logDirectory, $"{logFilePrefix}.log");

        // Clean up old log files (older than 30 days)
        CleanupOldLogs(logDirectory, logFilePrefix, retentionDays: 30);

        // Create logger factory with file logging and add as singleton
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(logLevel);
            
            // Filter out noisy Microsoft logs unless debugging
            if (logLevel > LogLevel.Debug)
            {
                builder.AddFilter("Microsoft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);
            }

            // Add Serilog file provider (it will append date automatically)
            builder.AddFile(logFilePath, logLevel);
        });

        // Register the logger factory and make it available for DI
        services.AddSingleton(loggerFactory);
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(logLevel);
            
            // Filter out noisy Microsoft logs unless debugging
            if (logLevel > LogLevel.Debug)
            {
                builder.AddFilter("Microsoft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);
            }

            // Add Serilog file provider (it will append date automatically)
            builder.AddFile(logFilePath, logLevel);
        });

        // Return the log file path pattern for display (Serilog inserts date)
        return Path.Combine(logDirectory, $"{logFilePrefix}-{{yyyyMMdd}}.log");
    }

    /// <summary>
    /// Clean up log files older than the specified retention period.
    /// </summary>
    private static void CleanupOldLogs(string logDirectory, string logFilePrefix, int retentionDays)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var logFiles = Directory.GetFiles(logDirectory, $"{logFilePrefix}-*.log");

            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                if (fileInfo.LastWriteTimeUtc < cutoffDate)
                {
                    File.Delete(logFile);
                }
            }
        }
        catch
        {
            // Silently ignore cleanup errors - don't fail startup due to log cleanup
        }
    }

    /// <summary>
    /// Sanitize a string to be safe for use in a filename.
    /// </summary>
    private static string SanitizeFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", input.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
