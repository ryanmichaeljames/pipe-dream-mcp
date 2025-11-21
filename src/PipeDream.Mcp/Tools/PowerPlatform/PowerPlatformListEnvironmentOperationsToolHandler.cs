using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Common;
using PipeDream.Mcp.PowerPlatform;
using PipeDream.Mcp.PowerPlatform.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Messages;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.PowerPlatform;

/// <summary>
/// Tool handler for listing Power Platform environment operations
/// </summary>
public class PowerPlatformListEnvironmentOperationsToolHandler : IToolHandler
{
    private readonly IPowerPlatformEnvironmentService _service;
    private readonly ILogger<PowerPlatformListEnvironmentOperationsToolHandler> _logger;

    public string ToolName => "powerplatform_environmentmanagement_list_environment_operations";
    public ToolDefinition Definition => PowerPlatformTools.ListEnvironmentOperations;

    public PowerPlatformListEnvironmentOperationsToolHandler(
        IPowerPlatformEnvironmentService service,
        ILogger<PowerPlatformListEnvironmentOperationsToolHandler> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        try
        {
            // Validate and extract environmentId
            if (!arguments.HasValue || !arguments.Value.TryGetProperty("environmentId", out var environmentIdElement) ||
                string.IsNullOrWhiteSpace(environmentIdElement.GetString()))
            {
                throw new ArgumentException("Missing or invalid 'environmentId' parameter");
            }

            var environmentId = environmentIdElement.GetString()!;

            // Validate GUID format
            InputValidator.ValidateGuid(environmentId, "environmentId");

            // Extract optional parameters
            int? limit = null;
            if (arguments.Value.TryGetProperty("limit", out var limitElement) && limitElement.TryGetInt32(out var limitValue))
            {
                limit = limitValue;
            }

            string? continuationToken = null;
            if (arguments.Value.TryGetProperty("continuationToken", out var tokenElement))
            {
                continuationToken = tokenElement.GetString();
            }

            _logger.LogInformation("Listing environment operations for {EnvironmentId}", environmentId);

            var result = await _service.ListEnvironmentOperationsAsync(environmentId, limit, continuationToken, cancellationToken);
            var json = result.RootElement.GetRawText();

            _logger.LogInformation("Successfully listed environment operations");
            
            return json;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Invalid parameters: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list environment operations");
            throw new InvalidOperationException($"Failed to list environment operations: {ex.Message}", ex);
        }
    }
}
