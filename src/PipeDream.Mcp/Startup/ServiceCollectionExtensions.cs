using Microsoft.Extensions.DependencyInjection;
using PipeDream.Mcp.Auth;
using PipeDream.Mcp.Config;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Dataverse.Services;
using PipeDream.Mcp.Protocol;

namespace PipeDream.Mcp.Startup;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Dataverse-related services with dependency injection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The Dataverse configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDataverseServices(
        this IServiceCollection services,
        DataverseConfig config)
    {
        // Register configuration
        services.AddSingleton(config);

        // Register authentication provider
        services.AddSingleton<AzureAuthProvider>();

        // Register Dataverse client
        services.AddSingleton<DataverseClient>();

        // Register Dataverse services
        services.AddSingleton<IDataverseQueryService, DataverseQueryService>();
        services.AddSingleton<IDataverseMetadataService, DataverseMetadataService>();
        services.AddSingleton<IFlowQueryService, FlowQueryService>();
        services.AddSingleton<IFlowStateService, FlowStateService>();

        // Register MCP server
        services.AddSingleton<McpServer>();

        return services;
    }
}
