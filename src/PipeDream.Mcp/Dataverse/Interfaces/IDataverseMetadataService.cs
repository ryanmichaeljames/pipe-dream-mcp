using System.Text.Json;

namespace PipeDream.Mcp.Dataverse.Interfaces;

/// <summary>
/// Service for retrieving Dataverse metadata
/// </summary>
public interface IDataverseMetadataService
{
    /// <summary>
    /// Get metadata for entities (list of entities and their properties)
    /// </summary>
    Task<JsonDocument> GetMetadataAsync(
        string? entityLogicalName = null,
        CancellationToken cancellationToken = default);
}
