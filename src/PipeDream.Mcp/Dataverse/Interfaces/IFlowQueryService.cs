using System.Text.Json;

namespace PipeDream.Mcp.Dataverse.Interfaces;

/// <summary>
/// Service for querying Power Automate flows
/// </summary>
internal interface IFlowQueryService
{
    /// <summary>
    /// Query Power Automate flows with optional solution filtering
    /// </summary>
    Task<JsonDocument> QueryFlowsAsync(
        string? solutionId = null,
        string? solutionUniqueName = null,
        string? customFilter = null,
        string[]? additionalFields = null,
        string? orderBy = null,
        int? top = null,
        bool count = true,
        int? maxPageSize = null,
        CancellationToken cancellationToken = default);
}
