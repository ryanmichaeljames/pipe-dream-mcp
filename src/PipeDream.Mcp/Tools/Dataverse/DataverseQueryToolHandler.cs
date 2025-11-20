using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Common;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.Dataverse;

/// <summary>
/// Handler for dataverse_query tool
/// </summary>
public class DataverseQueryToolHandler : IToolHandler
{
    private readonly IDataverseQueryService _queryService;
    private readonly ILogger<DataverseQueryToolHandler> _logger;

    public string ToolName => "dataverse_query";
    public ToolDefinition Definition => DataverseTools.Query;

    public DataverseQueryToolHandler(IDataverseQueryService queryService, ILogger<DataverseQueryToolHandler> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        try
        {
            var entity = arguments?.GetProperty("entity").GetString() ?? throw new ArgumentException("entity parameter required");
            InputValidator.ValidateEntityName(entity);

            var select = arguments.Value.TryGetProperty("select", out var selectProp)
                ? selectProp.EnumerateArray().Select(e => e.GetString()!).ToArray()
                : null;
            InputValidator.ValidateFieldNames(select);

            var filter = arguments.Value.TryGetProperty("filter", out var filterProp) ? filterProp.GetString() : null;
            InputValidator.ValidateFilterExpression(filter);

            var orderBy = arguments.Value.TryGetProperty("orderby", out var orderByProp) ? orderByProp.GetString() : null;

            var top = arguments.Value.TryGetProperty("top", out var topProp) ? topProp.GetInt32() : (int?)null;
            var maxPageSize = arguments.Value.TryGetProperty("maxpagesize", out var maxPageSizeProp) ? maxPageSizeProp.GetInt32() : (int?)null;

            // Validate top and maxpagesize are not used together
            if (top.HasValue && maxPageSize.HasValue)
                throw new ArgumentException("Cannot use both 'top' and 'maxpagesize' parameters. Use 'top' to limit total results, or 'maxpagesize' for pagination.");

            // Only default 'top' to 50 when maxPageSize is NOT being used
            int? validatedTop = maxPageSize.HasValue ? null : InputValidator.ValidateTopCount(top);
            var count = arguments.Value.TryGetProperty("count", out var countProp) ? countProp.GetBoolean() : true;

            var result = await _queryService.QueryAsync(entity, select, filter, validatedTop, expand: null, orderBy: orderBy, count: count, maxPageSize: maxPageSize, includeFormattedValues: false, cancellationToken: cancellationToken);
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
            throw new InvalidOperationException($"Failed to query Dataverse: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Query operation failed: {ex.Message}", ex);
        }
    }

    private static string GetUserFriendlyHttpError(HttpRequestException ex)
    {
        var message = ex.Message;

        if (message.Contains("400") || message.Contains("Bad Request"))
            return "Invalid request format. Check entity name and filter syntax.";
        if (message.Contains("401") || message.Contains("Unauthorized"))
            return "Authentication failed. Verify Azure CLI is logged in with correct permissions.";
        if (message.Contains("403") || message.Contains("Forbidden"))
            return "Access denied. Ensure your account has permissions to access this Dataverse resource.";
        if (message.Contains("404") || message.Contains("Not Found"))
            return "Resource not found. Verify the entity name is correct.";
        if (message.Contains("429") || message.Contains("Too Many Requests"))
            return "Rate limit exceeded. The server will automatically retry with appropriate delays.";
        if (message.Contains("500") || message.Contains("Internal Server Error"))
            return "Dataverse server error. This is a temporary issue and will be retried automatically.";
        if (message.Contains("503") || message.Contains("Service Unavailable"))
            return "Dataverse service temporarily unavailable. Will retry automatically.";
        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "Request timed out. Try again or reduce the query complexity.";

        return $"HTTP error occurred: {message}";
    }
}
