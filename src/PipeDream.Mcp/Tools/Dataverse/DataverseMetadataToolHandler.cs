using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.Dataverse;

/// <summary>
/// Handler for dataverse_metadata tool
/// </summary>
public class DataverseMetadataToolHandler : IToolHandler
{
    private readonly IDataverseMetadataService _metadataService;
    private readonly ILogger<DataverseMetadataToolHandler> _logger;

    public string ToolName => "dataverse_metadata";
    public ToolDefinition Definition => DataverseTools.Metadata;

    public DataverseMetadataToolHandler(IDataverseMetadataService metadataService, ILogger<DataverseMetadataToolHandler> logger)
    {
        _metadataService = metadataService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        try
        {
            var entity = arguments?.TryGetProperty("entity", out var entityProp) == true ? entityProp.GetString() : null;

            var result = await _metadataService.GetMetadataAsync(entity, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Failed to retrieve metadata: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in {ToolName}", ToolName);
            throw new InvalidOperationException($"Metadata operation failed: {ex.Message}", ex);
        }
    }

    private static string GetUserFriendlyHttpError(HttpRequestException ex)
    {
        var message = ex.Message;

        if (message.Contains("401") || message.Contains("Unauthorized"))
            return "Authentication failed. Verify Azure CLI is logged in with correct permissions.";
        if (message.Contains("403") || message.Contains("Forbidden"))
            return "Access denied. Ensure your account has permissions to access metadata.";

        return $"HTTP error occurred: {message}";
    }
}
