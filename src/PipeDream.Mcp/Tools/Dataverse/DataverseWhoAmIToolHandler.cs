using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.Dataverse;

/// <summary>
/// Handler for dataverse_whoami tool
/// </summary>
public class DataverseWhoAmIToolHandler : IToolHandler
{
    private readonly IDataverseQueryService _queryService;
    private readonly ILogger<DataverseWhoAmIToolHandler> _logger;

    public string ToolName => "dataverse_whoami";
    public ToolDefinition Definition => DataverseTools.WhoAmI;

    public DataverseWhoAmIToolHandler(IDataverseQueryService queryService, ILogger<DataverseWhoAmIToolHandler> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _queryService.WhoAmIAsync(cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Failed to get user information: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in {ToolName}", ToolName);
            throw new InvalidOperationException($"WhoAmI operation failed: {ex.Message}", ex);
        }
    }

    private static string GetUserFriendlyHttpError(HttpRequestException ex)
    {
        var message = ex.Message;

        if (message.Contains("401") || message.Contains("Unauthorized"))
            return "Authentication failed. Verify Azure CLI is logged in with correct permissions.";
        if (message.Contains("403") || message.Contains("Forbidden"))
            return "Access denied. Ensure your account has permissions to access Dataverse.";

        return $"HTTP error occurred: {message}";
    }
}
