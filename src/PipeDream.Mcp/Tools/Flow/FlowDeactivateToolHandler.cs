using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.Flow;

/// <summary>
/// Handler for dataverse_deactivate_flow tool
/// </summary>
public class FlowDeactivateToolHandler : IToolHandler
{
    private readonly IFlowStateService _flowStateService;
    private readonly ILogger<FlowDeactivateToolHandler> _logger;

    public string ToolName => "dataverse_deactivate_flow";
    public ToolDefinition Definition => FlowTools.DeactivateFlow;

    public FlowDeactivateToolHandler(IFlowStateService flowStateService, ILogger<FlowDeactivateToolHandler> logger)
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

            // Validate workflowId is a GUID
            if (!Guid.TryParse(workflowId, out var guid))
                throw new ArgumentException("workflowId must be a valid GUID");

            // Delegate to flow state service
            await _flowStateService.DeactivateFlowAsync(guid, cancellationToken);

            return $"Flow {workflowId} successfully deactivated (StateCode: 0 - Draft, StatusCode: 1)";
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Invalid parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Failed to deactivate flow: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Deactivate flow operation failed: {ex.Message}", ex);
        }
    }

    private static string GetUserFriendlyHttpError(HttpRequestException ex)
    {
        var message = ex.Message;

        if (message.Contains("404") || message.Contains("Not Found"))
            return "Flow not found. Verify the workflow ID is correct.";
        if (message.Contains("401") || message.Contains("Unauthorized"))
            return "Authentication failed. Verify Azure CLI is logged in with correct permissions.";

        return $"HTTP error occurred: {message}";
    }
}
