using System.Text.Json;
using PipeDream.Mcp.Dataverse.Interfaces;

namespace PipeDream.Mcp.Dataverse.Services;

/// <summary>
/// Service for querying Dataverse entities
/// </summary>
internal sealed class DataverseQueryService : IDataverseQueryService
{
    private readonly DataverseClient _client;

    public DataverseQueryService(DataverseClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Execute OData query against Dataverse entity
    /// </summary>
    public async Task<JsonDocument> QueryAsync(
        string entityLogicalName,
        string[]? select = null,
        string? filter = null,
        int? top = null,
        string? expand = null,
        string? orderBy = null,
        bool count = true,
        int? maxPageSize = null,
        bool includeFormattedValues = false,
        CancellationToken cancellationToken = default)
    {
        return await _client.QueryAsync(
            entityLogicalName,
            select,
            filter,
            top,
            expand,
            orderBy,
            count,
            maxPageSize,
            includeFormattedValues,
            cancellationToken);
    }

    /// <summary>
    /// Query using nextLink URL from previous paginated query
    /// </summary>
    public async Task<JsonDocument> QueryNextLinkAsync(
        string nextLinkUrl,
        int? maxPageSize = null,
        CancellationToken cancellationToken = default)
    {
        return await _client.QueryNextLinkAsync(nextLinkUrl, maxPageSize, cancellationToken);
    }

    /// <summary>
    /// Retrieve a single record by ID
    /// </summary>
    public async Task<JsonDocument> RetrieveAsync(
        string entityLogicalName,
        Guid recordId,
        string[]? select = null,
        CancellationToken cancellationToken = default)
    {
        return await _client.RetrieveAsync(
            entityLogicalName,
            recordId,
            select,
            cancellationToken);
    }

    /// <summary>
    /// Get information about the currently authenticated user
    /// </summary>
    public async Task<JsonDocument> WhoAmIAsync(CancellationToken cancellationToken = default)
    {
        return await _client.WhoAmIAsync(cancellationToken);
    }
}
