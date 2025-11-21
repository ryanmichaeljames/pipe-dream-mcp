using System.Text.Json;
using PipeDream.Mcp.Dataverse.Interfaces;

namespace PipeDream.Mcp.Dataverse.Services;

/// <summary>
/// Service for retrieving Dataverse metadata
/// </summary>
internal sealed class DataverseMetadataService : IDataverseMetadataService
{
    private readonly DataverseClient _client;

    public DataverseMetadataService(DataverseClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Get metadata for entities (list of entities and their properties)
    /// </summary>
    public async Task<JsonDocument> GetMetadataAsync(
        string? entityLogicalName = null,
        CancellationToken cancellationToken = default)
    {
        return await _client.GetMetadataAsync(entityLogicalName, cancellationToken);
    }
}
