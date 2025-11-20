using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.Flow;

/// <summary>
/// Handler for dataverse_query_flows tool
/// </summary>
public class FlowQueryToolHandler : IToolHandler
{
    private readonly IFlowQueryService _flowQueryService;
    private readonly ILogger<FlowQueryToolHandler> _logger;

    public string ToolName => "dataverse_query_flows";
    public ToolDefinition Definition => FlowTools.QueryFlows;

    public FlowQueryToolHandler(IFlowQueryService flowQueryService, ILogger<FlowQueryToolHandler> logger)
    {
        _flowQueryService = flowQueryService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        try
        {
            // Extract parameters
            var solutionId = arguments?.TryGetProperty("solutionId", out var solIdProp) == true
                ? solIdProp.GetString()
                : null;
            var solutionUniqueName = arguments?.TryGetProperty("solutionUniqueName", out var solNameProp) == true
                ? solNameProp.GetString()
                : null;
            var customFilter = arguments?.TryGetProperty("filter", out var filterProp) == true
                ? filterProp.GetString()
                : null;

            // Additional fields from arguments (if provided)
            var additionalFields = arguments?.TryGetProperty("select", out var selectProp) == true
                ? selectProp.EnumerateArray().Select(e => e.GetString()!).Where(f => !string.IsNullOrWhiteSpace(f)).ToArray()
                : Array.Empty<string>();

            var orderBy = arguments?.TryGetProperty("orderby", out var orderByProp) == true ? orderByProp.GetString() : null;
            var top = arguments?.TryGetProperty("top", out var topProp) == true ? topProp.GetInt32() : (int?)null;
            var maxPageSize = arguments?.TryGetProperty("maxpagesize", out var maxPageSizeProp) == true ? maxPageSizeProp.GetInt32() : (int?)null;
            var count = arguments?.TryGetProperty("count", out var countProp) == true ? countProp.GetBoolean() : true;

            // Delegate to flow query service (validation happens there)
            var result = await _flowQueryService.QueryFlowsAsync(
                solutionId,
                solutionUniqueName,
                customFilter,
                additionalFields,
                orderBy,
                top,
                count,
                maxPageSize,
                cancellationToken);

            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Invalid query parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Failed to query flows: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Query flows operation failed: {ex.Message}", ex);
        }
    }

    private static string GetUserFriendlyHttpError(HttpRequestException ex)
    {
        var message = ex.Message;

        if (message.Contains("400") || message.Contains("Bad Request"))
            return "Invalid request. Check filter syntax and parameters.";
        if (message.Contains("401") || message.Contains("Unauthorized"))
            return "Authentication failed. Verify Azure CLI is logged in with correct permissions.";
        if (message.Contains("404") || message.Contains("Not Found"))
            return "Solution not found. Verify solution ID or unique name.";

        return $"HTTP error occurred: {message}";
    }
}
