using System.Text.Json;

namespace PipeDream.Mcp.Dataverse.Interfaces;

/// <summary>
/// Service for querying Dataverse entities
/// </summary>
public interface IDataverseQueryService
{
    /// <summary>
    /// Execute OData query against Dataverse entity
    /// </summary>
    Task<JsonDocument> QueryAsync(
        string entityLogicalName,
        string[]? select = null,
        string? filter = null,
        int? top = null,
        string? expand = null,
        string? orderBy = null,
        bool count = true,
        int? maxPageSize = null,
        bool includeFormattedValues = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query using full nextLink URL from previous paginated query
    /// </summary>
    Task<JsonDocument> QueryNextLinkAsync(
        string nextLinkUrl,
        int? maxPageSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a single record by ID
    /// </summary>
    Task<JsonDocument> RetrieveAsync(
        string entityLogicalName,
        Guid recordId,
        string[]? select = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about the currently authenticated user
    /// </summary>
    Task<JsonDocument> WhoAmIAsync(CancellationToken cancellationToken = default);
}
