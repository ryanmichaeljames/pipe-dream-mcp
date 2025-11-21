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
/// Tool handler for getting Power Platform operation details by ID
/// </summary>
public class PowerPlatformGetOperationToolHandler : IToolHandler
{
    private readonly IPowerPlatformEnvironmentService _service;
    private readonly ILogger<PowerPlatformGetOperationToolHandler> _logger;

    public string ToolName => "powerplatform_environmentmanagement_get_operation";
    public ToolDefinition Definition => PowerPlatformTools.GetOperation;

    public PowerPlatformGetOperationToolHandler(
        IPowerPlatformEnvironmentService service,
        ILogger<PowerPlatformGetOperationToolHandler> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        try
        {
            // Validate and extract operationId
            if (!arguments.HasValue || !arguments.Value.TryGetProperty("operationId", out var operationIdElement) ||
                string.IsNullOrWhiteSpace(operationIdElement.GetString()))
            {
                throw new ArgumentException("Missing or invalid 'operationId' parameter");
            }

            var operationId = operationIdElement.GetString()!;

            // Validate GUID format
            InputValidator.ValidateGuid(operationId, "operationId");

            _logger.LogInformation("Getting operation {OperationId}", operationId);

            var result = await _service.GetOperationByIdAsync(operationId, cancellationToken);
            var json = result.RootElement.GetRawText();

            _logger.LogInformation("Successfully retrieved operation details");
            
            return json;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Invalid parameters: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get operation");
            throw new InvalidOperationException($"Failed to get operation: {ex.Message}", ex);
        }
    }
}
