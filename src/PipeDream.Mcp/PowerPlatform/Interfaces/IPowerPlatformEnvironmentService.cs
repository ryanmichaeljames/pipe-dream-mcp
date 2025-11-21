using System.Text.Json;

namespace PipeDream.Mcp.PowerPlatform.Interfaces;

/// <summary>
/// Service for Power Platform Environment Management operations
/// </summary>
public interface IPowerPlatformEnvironmentService
{
    /// <summary>
    /// List all Power Platform environments the user has access to
    /// </summary>
    Task<JsonDocument> ListEnvironmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// List operations (lifecycle events) for an environment
    /// </summary>
    /// <param name="environmentId">The environment ID (GUID)</param>
    /// <param name="limit">Optional: Maximum number of operations to return</param>
    /// <param name="continuationToken">Optional: Token for pagination</param>
    Task<JsonDocument> ListEnvironmentOperationsAsync(
        string environmentId,
        int? limit = null,
        string? continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get details of a specific operation by ID
    /// </summary>
    /// <param name="operationId">The operation ID (GUID)</param>
    Task<JsonDocument> GetOperationByIdAsync(
        string operationId,
        CancellationToken cancellationToken = default);
}
