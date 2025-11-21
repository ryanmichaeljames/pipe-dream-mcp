using Microsoft.Extensions.DependencyInjection;
using PipeDream.Mcp.Auth;
using PipeDream.Mcp.Config;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;
using PipeDream.Mcp.Dataverse.Services;
using PipeDream.Mcp.PowerPlatform;
using PipeDream.Mcp.PowerPlatform.Interfaces;
using PipeDream.Mcp.PowerPlatform.Services;
using PipeDream.Mcp.Protocol;
using PipeDream.Mcp.Tools.Dataverse;
using PipeDream.Mcp.Tools.Flow;
using PipeDream.Mcp.Tools.PowerPlatform;

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

        // Register tool handlers
        services.AddSingleton<IToolHandler, DataverseQueryToolHandler>();
        services.AddSingleton<IToolHandler, DataverseQueryNextLinkToolHandler>();
        services.AddSingleton<IToolHandler, DataverseRetrieveToolHandler>();
        services.AddSingleton<IToolHandler, DataverseMetadataToolHandler>();
        services.AddSingleton<IToolHandler, DataverseWhoAmIToolHandler>();
        services.AddSingleton<IToolHandler, FlowQueryToolHandler>();
        services.AddSingleton<IToolHandler, FlowActivateToolHandler>();
        services.AddSingleton<IToolHandler, FlowDeactivateToolHandler>();

        // Register tool registry and populate it
        services.AddSingleton<ToolRegistry>(sp =>
        {
            var registry = new ToolRegistry();
            var handlers = sp.GetServices<IToolHandler>();
            foreach (var handler in handlers)
            {
                registry.Register(handler);
            }
            return registry;
        });

        // Register MCP server
        services.AddSingleton<McpServer>();

        return services;
    }

    /// <summary>
    /// Registers all Power Platform-related services with dependency injection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The Power Platform configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPowerPlatformServices(
        this IServiceCollection services,
        PowerPlatformConfig config)
    {
        // Register configuration
        services.AddSingleton(config);

        // Register authentication provider
        services.AddSingleton<AzureAuthProvider>();

        // Register Power Platform client
        services.AddSingleton<PowerPlatformClient>();

        // Register Power Platform services
        services.AddSingleton<IPowerPlatformEnvironmentService, PowerPlatformEnvironmentService>();

        // Register tool handlers
        services.AddSingleton<IToolHandler, PowerPlatformListEnvironmentsToolHandler>();
        services.AddSingleton<IToolHandler, PowerPlatformListEnvironmentOperationsToolHandler>();
        services.AddSingleton<IToolHandler, PowerPlatformGetOperationToolHandler>();

        // Register tool registry and populate it
        services.AddSingleton<ToolRegistry>(sp =>
        {
            var registry = new ToolRegistry();
            var handlers = sp.GetServices<IToolHandler>();
            foreach (var handler in handlers)
            {
                registry.Register(handler);
            }
            return registry;
        });

        // Register MCP server
        services.AddSingleton<McpServer>();

        return services;
    }
}
