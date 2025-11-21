using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.PowerPlatform.Interfaces;

namespace PipeDream.Mcp.PowerPlatform.Services;

/// <summary>
/// Service implementation for Power Platform Environment Management operations
/// </summary>
public class PowerPlatformEnvironmentService : IPowerPlatformEnvironmentService
{
    private readonly PowerPlatformClient _client;
    private readonly ILogger<PowerPlatformEnvironmentService> _logger;

    public PowerPlatformEnvironmentService(
        PowerPlatformClient client,
        ILogger<PowerPlatformEnvironmentService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<JsonDocument> ListEnvironmentsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing environments");
        return await _client.ListEnvironmentsAsync(cancellationToken);
    }

    public async Task<JsonDocument> ListEnvironmentOperationsAsync(
        string environmentId,
        int? limit = null,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing environment operations for {EnvironmentId}", environmentId);
        return await _client.ListEnvironmentOperationsAsync(environmentId, limit, continuationToken, cancellationToken);
    }

    public async Task<JsonDocument> GetOperationByIdAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting operation {OperationId}", operationId);
        return await _client.GetOperationByIdAsync(operationId, cancellationToken);
    }
}
