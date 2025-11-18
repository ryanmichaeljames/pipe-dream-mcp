using System.Text.Json.Serialization;
using PipeDream.Mcp.Protocol;

namespace PipeDream.Mcp.Dataverse;

/// <summary>
/// MCP tool definitions for Dataverse operations
/// </summary>
public static class DataverseTools
{
    public static ToolDefinition Query => new()
    {
        Name = "dataverse_query",
        Description = "Query Dataverse entities using OData. Returns a collection of records matching the query criteria.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["entity"] = new()
                {
                    Type = "string",
                    Description = "Entity logical name (e.g., 'account', 'contact', 'opportunity')"
                },
                ["select"] = new()
                {
                    Type = "array",
                    Description = "Array of field names to return (e.g., ['name', 'emailaddress1'])",
                    Items = new() { Type = "string" }
                },
                ["filter"] = new()
                {
                    Type = "string",
                    Description = "OData filter expression (e.g., 'statecode eq 0' or 'name eq ''Contoso''')"
                },
                ["top"] = new()
                {
                    Type = "integer",
                    Description = "Maximum number of records to return (default: 50)"
                }
            },
            Required = new[] { "entity" }
        }
    };

    public static ToolDefinition Retrieve => new()
    {
        Name = "dataverse_retrieve",
        Description = "Retrieve a single Dataverse record by its ID.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["entity"] = new()
                {
                    Type = "string",
                    Description = "Entity logical name (e.g., 'account', 'contact')"
                },
                ["id"] = new()
                {
                    Type = "string",
                    Description = "GUID of the record to retrieve"
                },
                ["select"] = new()
                {
                    Type = "array",
                    Description = "Array of field names to return (optional)",
                    Items = new() { Type = "string" }
                }
            },
            Required = new[] { "entity", "id" }
        }
    };

    public static ToolDefinition Metadata => new()
    {
        Name = "dataverse_metadata",
        Description = "Get metadata about Dataverse entities, including available entities and their attributes.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["entity"] = new()
                {
                    Type = "string",
                    Description = "Specific entity logical name to get metadata for (optional). If not provided, returns list of all entities."
                }
            },
            Required = Array.Empty<string>()
        }
    };

    public static ToolDefinition List => new()
    {
        Name = "dataverse_list",
        Description = "List records from a Dataverse entity with pagination support.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["entity"] = new()
                {
                    Type = "string",
                    Description = "Entity logical name (e.g., 'account', 'contact')"
                },
                ["pageSize"] = new()
                {
                    Type = "integer",
                    Description = "Number of records per page (default: 50, max: 250)"
                },
                ["pagingCookie"] = new()
                {
                    Type = "string",
                    Description = "Paging cookie from previous response for next page (optional)"
                }
            },
            Required = new[] { "entity" }
        }
    };

    public static IEnumerable<ToolDefinition> All => new[]
    {
        Query,
        Retrieve,
        Metadata,
        List
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
