using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Dataverse.Constants;
using PipeDream.Mcp.Dataverse.Constants.Workflow;
using PipeDream.Mcp.Dataverse.Interfaces;
using SolutionFields = PipeDream.Mcp.Dataverse.Constants.Solution.Fields;
using SolutionComponentFields = PipeDream.Mcp.Dataverse.Constants.SolutionComponent.Fields;

namespace PipeDream.Mcp.Dataverse.Services;

/// <summary>
/// Service for querying Power Automate flows
/// </summary>
internal sealed class FlowQueryService : IFlowQueryService
{
    private readonly DataverseClient _client;
    private readonly ILogger<FlowQueryService> _logger;

    public FlowQueryService(DataverseClient client, ILogger<FlowQueryService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Query Power Automate flows with optional solution filtering
    /// </summary>
    public async Task<JsonDocument> QueryFlowsAsync(
        string? solutionId = null,
        string? solutionUniqueName = null,
        string? customFilter = null,
        string[]? additionalFields = null,
        string? orderBy = null,
        int? top = null,
        bool count = true,
        int? maxPageSize = null,
        CancellationToken cancellationToken = default)
    {
        // Validate top parameter if provided
        if (top.HasValue && (top.Value < 1 || top.Value > 5000))
            throw new ArgumentException("top must be between 1 and 5000");
        
        // Validate maxPageSize parameter if provided
        if (maxPageSize.HasValue && (maxPageSize.Value < 1 || maxPageSize.Value > 250))
            throw new ArgumentException("maxPageSize must be between 1 and 250");
        
        // Validate top and maxpagesize are not used together
        if (top.HasValue && maxPageSize.HasValue)
            throw new ArgumentException("Cannot use both 'top' and 'maxPageSize' parameters. Use 'top' to limit total results, or 'maxPageSize' for pagination.");
        
        // Default top to 50 only if maxPageSize not used
        int? effectiveTop = maxPageSize.HasValue ? null : (top ?? 50);

        // Core fields always included
        var coreFields = new[]
        {
            Fields.WorkflowId,
            Fields.Name,
            Fields.ModifiedOn,
            Fields.CreatedOn,
            Fields.Description,
            Fields.StateCode,
            Fields.StatusCode,
            Fields.ModifiedByValue,
            Fields.CreatedByValue,
            Fields.OwnerIdValue,
            Fields.Category
        };

        // Combine core fields with additional fields (deduplicate)
        var select = additionalFields != null
            ? coreFields.Concat(additionalFields.Where(f => !string.IsNullOrWhiteSpace(f))).Distinct().ToArray()
            : coreFields;

        // Build filter: always start with category eq 5 (modern flows)
        var filters = new List<string> { $"{Fields.Category} eq {Category.ModernFlow}" };

        // If solution specified, use optimized approach based on identifier type
        if (!string.IsNullOrWhiteSpace(solutionId) || !string.IsNullOrWhiteSpace(solutionUniqueName))
        {
            var identifier = solutionId ?? solutionUniqueName!;

            // If GUID provided, use direct filter (single API call)
            if (Guid.TryParse(identifier, out var solutionGuid))
            {
                filters.Add($"{SolutionFields.SolutionId} eq {solutionGuid}");
            }
            else
            {
                // Solution unique name requires lookup - fall back to two-query approach
                var workflowIds = await QuerySolutionComponentsAsync(identifier, cancellationToken);

                if (workflowIds.Length == 0)
                {
                    // No flows in solution, return empty result
                    return JsonDocument.Parse(@"{""value"":[]}");
                }

                // Build workflowid in (guid1 or guid2 or ...) filter
                var orConditions = string.Join(" or ",
                    workflowIds.Select(id => $"{Fields.WorkflowId} eq {id}"));
                filters.Add($"({orConditions})");
            }
        }

        // Add custom filter if provided
        if (!string.IsNullOrWhiteSpace(customFilter))
        {
            filters.Add($"({customFilter})");
        }

        // Combine filters with 'and'
        var filter = string.Join(" and ", filters);

        // Query workflows with formatted values (which includes lookup display names)
        return await _client.QueryAsync(
            Entities.Workflows,
            select,
            filter,
            effectiveTop,
            expand: null,
            orderBy: orderBy,
            count: count,
            maxPageSize: maxPageSize,
            includeFormattedValues: true,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Query solution components by solution unique name
    /// </summary>
    private async Task<Guid[]> QuerySolutionComponentsAsync(
        string solutionUniqueName,
        CancellationToken cancellationToken)
    {
        // Resolve unique name to GUID
        _logger.LogDebug("Resolving solution unique name '{SolutionUniqueName}' to GUID", solutionUniqueName);

        var solutionQuery = await _client.QueryAsync(
            Entities.Solutions,
            select: new[] { SolutionFields.SolutionId, SolutionFields.UniqueName },
            filter: $"{SolutionFields.UniqueName} eq '{solutionUniqueName}'",
            top: 1,
            cancellationToken: cancellationToken);

        var solutionValues = solutionQuery.RootElement.GetProperty("value");
        if (solutionValues.GetArrayLength() == 0)
            throw new ArgumentException($"Solution with unique name '{solutionUniqueName}' not found");

        var solutionIdString = solutionValues[0].GetProperty(SolutionFields.SolutionId).GetString();
        if (string.IsNullOrEmpty(solutionIdString) || !Guid.TryParse(solutionIdString, out var solutionId))
            throw new InvalidOperationException($"Invalid solution ID returned for unique name '{solutionUniqueName}'");

        // Query solution components
        var filter = $"{SolutionFields.SolutionIdValue} eq {solutionId:D} and {SolutionComponentFields.ComponentType} eq {ComponentTypes.Workflow}";
        _logger.LogDebug("Querying solution components (solutionId={SolutionId}, componentType={ComponentType})", solutionId, ComponentTypes.Workflow);

        var result = await _client.QueryAsync(
            Entities.SolutionComponents,
            select: new[] { SolutionComponentFields.ObjectId },
            filter: filter,
            top: 5000,
            cancellationToken: cancellationToken);

        var components = result.RootElement.GetProperty("value");
        var objectIds = new List<Guid>();

        foreach (var component in components.EnumerateArray())
        {
            if (component.TryGetProperty(SolutionComponentFields.ObjectId, out var objectIdProp))
            {
                var objectIdString = objectIdProp.GetString();
                if (!string.IsNullOrEmpty(objectIdString) && Guid.TryParse(objectIdString, out var objectId))
                {
                    objectIds.Add(objectId);
                }
            }
        }

        _logger.LogDebug("Found {Count} components in solution", objectIds.Count);
        return objectIds.ToArray();
    }
}
