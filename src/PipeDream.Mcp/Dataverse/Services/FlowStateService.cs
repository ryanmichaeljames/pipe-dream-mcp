using System.Text.Json;
using PipeDream.Mcp.Dataverse.Constants;
using PipeDream.Mcp.Dataverse.Constants.Workflow;
using PipeDream.Mcp.Dataverse.Interfaces;
using ConnectionReferenceFields = PipeDream.Mcp.Dataverse.Constants.ConnectionReference.Fields;

namespace PipeDream.Mcp.Dataverse.Services;

/// <summary>
/// Service for managing Power Automate flow state
/// </summary>
internal sealed class FlowStateService : IFlowStateService
{
    private readonly DataverseClient _client;

    public FlowStateService(DataverseClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Activate a Power Automate flow
    /// </summary>
    public async Task<string> ActivateFlowAsync(
        Guid workflowId,
        bool validateConnectionReferences = false,
        CancellationToken cancellationToken = default)
    {
        if (workflowId == Guid.Empty)
            throw new ArgumentException("Workflow ID is required", nameof(workflowId));

        // Validate connection references if requested
        if (validateConnectionReferences)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] FlowStateService: Validating connection references for workflow {workflowId}");
            var validationResult = await ValidateConnectionReferencesAsync(workflowId, cancellationToken);

            if (!validationResult.IsValid)
            {
                var errorMessage = "Cannot activate flow - connection reference validation failed:\n";

                if (validationResult.MissingReferences.Any())
                    errorMessage += $"Missing references: {string.Join(", ", validationResult.MissingReferences)}\n";

                if (validationResult.UnconfiguredReferences.Any())
                    errorMessage += $"Unconfigured references: {string.Join(", ", validationResult.UnconfiguredReferences)}";

                throw new InvalidOperationException(errorMessage.TrimEnd());
            }

            Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] FlowStateService: Connection validation passed for workflow {workflowId}");
        }

        // Activate flow
        await _client.UpdateStateAsync(
            Entities.Workflows,
            workflowId,
            State.Activated,
            Status.Activated,
            cancellationToken);

        return $"Flow {workflowId} successfully activated (StateCode: {State.Activated} - Activated, StatusCode: {Status.Activated})";
    }

    /// <summary>
    /// Deactivate a Power Automate flow
    /// </summary>
    public async Task<string> DeactivateFlowAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        if (workflowId == Guid.Empty)
            throw new ArgumentException("Workflow ID is required", nameof(workflowId));

        // Deactivate flow
        await _client.UpdateStateAsync(
            Entities.Workflows,
            workflowId,
            State.Draft,
            Status.Draft,
            cancellationToken);

        return $"Flow {workflowId} successfully deactivated (StateCode: {State.Draft} - Draft, StatusCode: {Status.Draft})";
    }

    /// <summary>
    /// Validate connection references for a workflow
    /// </summary>
    public async Task<ConnectionValidationResult> ValidateConnectionReferencesAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        if (workflowId == Guid.Empty)
            throw new ArgumentException("Workflow ID is required", nameof(workflowId));

        Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] FlowStateService: Validating connection references for workflow {workflowId:D}");

        // Retrieve workflow with clientdata field
        var workflow = await _client.RetrieveAsync(
            Entities.Workflows,
            workflowId,
            new[] { Fields.ClientData },
            cancellationToken);

        var result = new ConnectionValidationResult { IsValid = true };
        var missingRefs = new List<string>();
        var unconfiguredRefs = new List<string>();

        // Try to parse clientdata JSON
        if (workflow.RootElement.TryGetProperty(Fields.ClientData, out var clientDataProp))
        {
            var clientDataString = clientDataProp.GetString();
            if (!string.IsNullOrEmpty(clientDataString))
            {
                try
                {
                    using var clientData = JsonDocument.Parse(clientDataString);

                    if (clientData.RootElement.TryGetProperty("connectionReferences", out var connRefs))
                    {
                        // Connection references can be an object or array
                        if (connRefs.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var connRef in connRefs.EnumerateObject())
                            {
                                var logicalName = connRef.Name;
                                await ValidateSingleConnectionReferenceAsync(logicalName, unconfiguredRefs, cancellationToken);
                            }
                        }
                        else if (connRefs.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var connRef in connRefs.EnumerateArray())
                            {
                                if (connRef.TryGetProperty("logicalName", out var logicalNameProp))
                                {
                                    var logicalName = logicalNameProp.GetString();
                                    if (!string.IsNullOrEmpty(logicalName))
                                    {
                                        await ValidateSingleConnectionReferenceAsync(logicalName, unconfiguredRefs, cancellationToken);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] FlowStateService: Warning - Could not parse clientdata JSON: {ex.Message}");
                }
            }
        }

        result.MissingReferences = missingRefs.ToArray();
        result.UnconfiguredReferences = unconfiguredRefs.ToArray();
        result.IsValid = missingRefs.Count == 0 && unconfiguredRefs.Count == 0;

        Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] FlowStateService: Connection validation complete - IsValid={result.IsValid}, Missing={missingRefs.Count}, Unconfigured={unconfiguredRefs.Count}");

        return result;
    }

    /// <summary>
    /// Validate a single connection reference
    /// </summary>
    private async Task ValidateSingleConnectionReferenceAsync(
        string logicalName,
        List<string> unconfiguredRefs,
        CancellationToken cancellationToken)
    {
        try
        {
            var connRefQuery = await _client.QueryAsync(
                Entities.ConnectionReferences,
                select: new[] { ConnectionReferenceFields.ConnectionId, ConnectionReferenceFields.ConnectionReferenceLogicalName },
                filter: $"{ConnectionReferenceFields.ConnectionReferenceLogicalName} eq '{logicalName}'",
                top: 1,
                cancellationToken: cancellationToken);

            var connRefValues = connRefQuery.RootElement.GetProperty("value");
            if (connRefValues.GetArrayLength() > 0)
            {
                var connRef = connRefValues[0];
                if (!connRef.TryGetProperty(ConnectionReferenceFields.ConnectionId, out var connectionIdProp) ||
                    string.IsNullOrEmpty(connectionIdProp.GetString()))
                {
                    unconfiguredRefs.Add(logicalName);
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] FlowStateService: Connection reference '{logicalName}' is not configured");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] FlowStateService: Warning - Could not validate connection reference '{logicalName}': {ex.Message}");
        }
    }
}
