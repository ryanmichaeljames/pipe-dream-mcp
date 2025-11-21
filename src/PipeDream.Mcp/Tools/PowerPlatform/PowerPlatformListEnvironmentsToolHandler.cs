using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.PowerPlatform;
using PipeDream.Mcp.PowerPlatform.Interfaces;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Protocol.Messages;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Tools.PowerPlatform;

/// <summary>
/// Tool handler for listing Power Platform environments
/// </summary>
public class PowerPlatformListEnvironmentsToolHandler : IToolHandler
{
    private readonly IPowerPlatformEnvironmentService _service;
    private readonly ILogger<PowerPlatformListEnvironmentsToolHandler> _logger;

    public string ToolName => "powerplatform_environmentmanagement_list_environments";
    public ToolDefinition Definition => PowerPlatformTools.ListEnvironments;

    public PowerPlatformListEnvironmentsToolHandler(
        IPowerPlatformEnvironmentService service,
        ILogger<PowerPlatformListEnvironmentsToolHandler> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Listing Power Platform environments");

            var result = await _service.ListEnvironmentsAsync(cancellationToken);
            var json = result.RootElement.GetRawText();

            _logger.LogInformation("Successfully listed environments");
            
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list environments");
            throw new InvalidOperationException($"Failed to list environments: {ex.Message}", ex);
        }
    }
}
