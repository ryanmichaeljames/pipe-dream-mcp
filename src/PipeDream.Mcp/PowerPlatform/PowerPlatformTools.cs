using System.Text.Json.Serialization;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.PowerPlatform;

/// <summary>
/// Tool definitions for Power Platform Environment Management API
/// </summary>
public static class PowerPlatformTools
{
    /// <summary>
    /// Tool to list all Power Platform environments
    /// </summary>
    public static ToolDefinition ListEnvironments => new()
    {
        Name = "powerplatform_environmentmanagement_list_environments",
        Description = "List all Power Platform environments the authenticated user has access to. Returns environment details including ID, name, type, region, and state.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>(),
            Required = Array.Empty<string>()
        }
    };

    /// <summary>
    /// Tool to list environment operations
    /// </summary>
    public static ToolDefinition ListEnvironmentOperations => new()
    {
        Name = "powerplatform_environmentmanagement_list_environment_operations",
        Description = "List lifecycle operations (create, update, delete, backup, restore, etc.) for a specific environment. Returns operation history with status and timestamps. IMPORTANT: Always confirm the environment name with the user before calling this tool to ensure you're accessing the correct environment.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["environmentId"] = new()
                {
                    Type = "string",
                    Description = "The environment ID (GUID). Use list_environments tool first to discover environment IDs and names, then confirm with the user which environment to query."
                },
                ["limit"] = new()
                {
                    Type = "integer",
                    Description = "Optional: Maximum number of operations to return per page. Default is determined by the API."
                },
                ["continuationToken"] = new()
                {
                    Type = "string",
                    Description = "Optional: Token for retrieving the next page of results. Obtained from the 'continuationToken' field in a previous response."
                }
            },
            Required = new[] { "environmentId" }
        }
    };

    /// <summary>
    /// Tool to get operation details by ID
    /// </summary>
    public static ToolDefinition GetOperation => new()
    {
        Name = "powerplatform_environmentmanagement_get_operation",
        Description = "Get detailed information about a specific operation by its ID. Returns operation type, status, timestamps, error details (if failed), and related environment information.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["operationId"] = new()
                {
                    Type = "string",
                    Description = "The operation ID (GUID). Obtained from list_environment_operations results."
                }
            },
            Required = new[] { "operationId" }
        }
    };
}

/// <summary>
/// JSON Schema for tool input parameters
/// </summary>
public class InputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, PropertySchema> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public string[] Required { get; set; } = Array.Empty<string>();
}

/// <summary>
/// JSON Schema property definition
/// </summary>
public class PropertySchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PropertySchema? Items { get; set; }
}
