using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Common;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.Dataverse;

/// <summary>
/// Handler for dataverse_retrieve tool
/// </summary>
public class DataverseRetrieveToolHandler : IToolHandler
{
    private readonly IDataverseQueryService _queryService;
    private readonly ILogger<DataverseRetrieveToolHandler> _logger;

    public string ToolName => "dataverse_retrieve";
    public ToolDefinition Definition => DataverseTools.Retrieve;

    public DataverseRetrieveToolHandler(IDataverseQueryService queryService, ILogger<DataverseRetrieveToolHandler> logger)
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

            var idString = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("id parameter required");
            var id = InputValidator.ValidateGuid(idString, "id");

            var select = arguments.Value.TryGetProperty("select", out var selectProp)
                ? selectProp.EnumerateArray().Select(e => e.GetString()!).ToArray()
                : null;
            InputValidator.ValidateFieldNames(select);

            var result = await _queryService.RetrieveAsync(entity, id, select, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Invalid retrieve parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Failed to retrieve record: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Retrieve operation failed: {ex.Message}", ex);
        }
    }

    private static string GetUserFriendlyHttpError(HttpRequestException ex)
    {
        var message = ex.Message;

        if (message.Contains("404") || message.Contains("Not Found"))
            return "Record not found. Verify the entity name and record ID are correct.";
        if (message.Contains("401") || message.Contains("Unauthorized"))
            return "Authentication failed. Verify Azure CLI is logged in with correct permissions.";
        if (message.Contains("403") || message.Contains("Forbidden"))
            return "Access denied. Ensure your account has permissions to access this record.";

        return $"HTTP error occurred: {message}";
    }
}
