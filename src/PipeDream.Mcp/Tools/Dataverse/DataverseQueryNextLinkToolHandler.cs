using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.Dataverse;

/// <summary>
/// Handler for dataverse_query_nextlink tool
/// </summary>
public class DataverseQueryNextLinkToolHandler : IToolHandler
{
    private readonly IDataverseQueryService _queryService;
    private readonly ILogger<DataverseQueryNextLinkToolHandler> _logger;

    public string ToolName => "dataverse_query_nextlink";
    public ToolDefinition Definition => DataverseTools.QueryNextLink;

    public DataverseQueryNextLinkToolHandler(IDataverseQueryService queryService, ILogger<DataverseQueryNextLinkToolHandler> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        try
        {
            var nextLink = arguments?.GetProperty("nextlink").GetString() ?? throw new ArgumentException("nextlink parameter required");

            if (string.IsNullOrWhiteSpace(nextLink))
                throw new ArgumentException("nextlink parameter cannot be empty");

            var maxPageSize = arguments.Value.TryGetProperty("maxpagesize", out var maxPageSizeProp) ? maxPageSizeProp.GetInt32() : (int?)null;

            var result = await _queryService.QueryNextLinkAsync(nextLink, maxPageSize, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Invalid nextLink: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Failed to query nextLink: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in {ToolName}", ToolName);
            throw new InvalidOperationException($"NextLink query failed: {ex.Message}", ex);
        }
    }

    private static string GetUserFriendlyHttpError(HttpRequestException ex)
    {
        var message = ex.Message;

        if (message.Contains("400") || message.Contains("Bad Request"))
            return "Invalid nextLink URL format.";
        if (message.Contains("401") || message.Contains("Unauthorized"))
            return "Authentication failed. Verify Azure CLI is logged in with correct permissions.";
        if (message.Contains("404") || message.Contains("Not Found"))
            return "NextLink expired or invalid.";
        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "Request timed out. Try again.";

        return $"HTTP error occurred: {message}";
    }
}
