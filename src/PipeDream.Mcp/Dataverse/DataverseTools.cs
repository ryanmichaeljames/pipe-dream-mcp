using System.Text.Json.Serialization;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Dataverse;

/// <summary>
/// MCP tool definitions for Dataverse operations
/// </summary>
public static class DataverseTools
{
    public static ToolDefinition Query => new()
    {
        Name = "dataverse_query",
        Description = @"Query Dataverse entities using OData. Use entity plural names (e.g., 'accounts', 'contacts'). For Power Automate flows, prefer dataverse_query_flows tool instead.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["entity"] = new()
                {
                    Type = "string",
                    Description = "Entity plural name: 'accounts', 'contacts', 'solutions', etc. Must be plural form used in Web API."
                },
                ["select"] = new()
                {
                    Type = "array",
                    Description = "Field names to return. Example: ['name', 'emailaddress1', 'createdon']",
                    Items = new() { Type = "string" }
                },
                ["filter"] = new()
                {
                    Type = "string",
                    Description = "OData filter. Examples: \"statecode eq 0\", \"name eq 'Contoso'\", \"contains(name, 'test')\". For dates, convert to UTC first."
                },
                ["orderby"] = new()
                {
                    Type = "string",
                    Description = "Order results by field(s). Examples: 'createdon desc', 'name asc', 'modifiedon desc,name asc'. Provides stable ordering for consistent results."
                },
                ["top"] = new()
                {
                    Type = "integer",
                    Description = "Limit total records returned (default: 50, max: 5000). Use when you want a fixed number of results in a single response. Example: 'Get top 10 accounts'. Do NOT use with maxpagesize."
                },
                ["count"] = new()
                {
                    Type = "boolean",
                    Description = "Include total count of matching records (default: true). Returns @odata.count in response."
                },
                ["maxpagesize"] = new()
                {
                    Type = "integer",
                    Description = "Records per page for server-driven pagination (e.g., 10, 50, 100). Use when user wants to paginate through results or expects many records. Returns @odata.nextLink for fetching next page. Example: 'Show me solutions, 5 at a time'. Do NOT use with top."
                }
            },
            Required = new[] { "entity" }
        }
    };

    public static ToolDefinition Retrieve => new()
    {
        Name = "dataverse_retrieve",
        Description = "Retrieve a single Dataverse record by ID.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["entity"] = new()
                {
                    Type = "string",
                    Description = "Entity plural name: 'accounts', 'contacts', etc."
                },
                ["id"] = new()
                {
                    Type = "string",
                    Description = "Record GUID"
                },
                ["select"] = new()
                {
                    Type = "array",
                    Description = "Field names to return (optional). Example: ['name', 'createdon']",
                    Items = new() { Type = "string" }
                }
            },
            Required = new[] { "entity", "id" }
        }
    };

    public static ToolDefinition Metadata => new()
    {
        Name = "dataverse_metadata",
        Description = "Get Dataverse entity metadata including available entities and their attributes.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["entity"] = new()
                {
                    Type = "string",
                    Description = "Entity plural name for specific metadata (optional). If omitted, returns all entities."
                }
            },
            Required = Array.Empty<string>()
        }
    };

    public static ToolDefinition QueryNextLink => new()
    {
        Name = "dataverse_query_nextlink",
        Description = "Fetch next page of results using @odata.nextLink from a previous query. Use this after calling dataverse_query with maxpagesize parameter.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["nextlink"] = new()
                {
                    Type = "string",
                    Description = "The full @odata.nextLink URL from the previous query response. Example: 'https://org.crm.dynamics.com/api/data/v9.2/contacts?$skiptoken=...'"
                },
                ["maxpagesize"] = new()
                {
                    Type = "integer",
                    Description = "Optional: Same maxpagesize value from the original query to maintain consistent page size (e.g., 10, 50, 100). Recommended to match the original query's maxpagesize."
                }
            },
            Required = new[] { "nextlink" }
        }
    };

    public static ToolDefinition WhoAmI => new()
    {
        Name = "dataverse_whoami",
        Description = "Get information about the currently authenticated user including UserId, BusinessUnitId, and OrganizationId.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>(),
            Required = Array.Empty<string>()
        }
    };

    public static IEnumerable<ToolDefinition> All => new[]
    {
        Query,
        QueryNextLink,
        Retrieve,
        Metadata,
        WhoAmI
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
