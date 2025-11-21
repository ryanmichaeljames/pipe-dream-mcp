using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.Flow;

/// <summary>
/// Handler for dataverse_activate_flow tool
/// </summary>
public class FlowActivateToolHandler : IToolHandler
{
    private readonly IFlowStateService _flowStateService;
    private readonly ILogger<FlowActivateToolHandler> _logger;

    public string ToolName => "dataverse_activate_flow";
    public ToolDefinition Definition => FlowTools.ActivateFlow;

    public FlowActivateToolHandler(IFlowStateService flowStateService, ILogger<FlowActivateToolHandler> logger)
    {
        _flowStateService = flowStateService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        try
        {
            var workflowId = arguments?.GetProperty("workflowId").GetString()
                ?? throw new ArgumentException("workflowId parameter required");

            var validateConnectionReferences = arguments.Value.TryGetProperty("validateConnectionReferences", out var validateProp)
                && validateProp.GetBoolean();

            // Validate workflowId is a GUID
            if (!Guid.TryParse(workflowId, out var guid))
                throw new ArgumentException("workflowId must be a valid GUID");

            // Delegate to flow state service
            if (validateConnectionReferences)
                _logger.LogDebug("Validating connection references for workflow {WorkflowId}", workflowId);

            await _flowStateService.ActivateFlowAsync(guid, validateConnectionReferences, cancellationToken);

            return $"Flow {workflowId} successfully activated (StateCode: 1 - Activated, StatusCode: 2)";
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Invalid parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Failed to activate flow: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Activate flow operation failed: {ex.Message}", ex);
        }
    }

    private static string GetUserFriendlyHttpError(HttpRequestException ex)
    {
        var message = ex.Message;

        if (message.Contains("404") || message.Contains("Not Found"))
            return "Flow not found. Verify the workflow ID is correct.";
        if (message.Contains("400") || message.Contains("Bad Request"))
            return "Cannot activate flow. Check connection references are configured.";
        if (message.Contains("401") || message.Contains("Unauthorized"))
            return "Authentication failed. Verify Azure CLI is logged in with correct permissions.";

        return $"HTTP error occurred: {message}";
    }
}
