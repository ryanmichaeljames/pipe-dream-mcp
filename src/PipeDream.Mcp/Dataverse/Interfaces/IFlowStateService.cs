namespace PipeDream.Mcp.Dataverse.Interfaces;

/// <summary>
/// Service for managing Power Automate flow state
/// </summary>
public interface IFlowStateService
{
    /// <summary>
    /// Activate a Power Automate flow
    /// </summary>
    Task<string> ActivateFlowAsync(
        Guid workflowId,
        bool validateConnectionReferences = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivate a Power Automate flow
    /// </summary>
    Task<string> DeactivateFlowAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate connection references for a workflow
    /// </summary>
    Task<ConnectionValidationResult> ValidateConnectionReferencesAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of connection reference validation
/// </summary>
public class ConnectionValidationResult
{
    public bool IsValid { get; set; }
    public string[] MissingReferences { get; set; } = Array.Empty<string>();
    public string[] UnconfiguredReferences { get; set; } = Array.Empty<string>();
}
