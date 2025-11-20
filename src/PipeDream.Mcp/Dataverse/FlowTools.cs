using PipeDream.Mcp.Protocol;

namespace PipeDream.Mcp.Dataverse;

/// <summary>
/// MCP tool definitions for Power Automate flow operations
/// </summary>
public static class FlowTools
{
    public static ToolDefinition QueryFlows => new()
    {
        Name = "dataverse_query_flows",
        Description = @"Query Power Automate cloud flows. Returns formatted values with human-readable names. 
Core fields always included: workflowid, name, modifiedon, createdon, description, statecode, statuscode, modifiedby, createdby, ownerid.

Common queries:
- Find by exact name: filter=""name eq 'Flow Name'""
- Search by partial name: filter=""contains(name, 'keyword')""
- Active/enabled flows: filter=""statecode eq 1""
- Draft/inactive/disabled flows: filter=""statecode eq 0""
- In solution: solutionUniqueName='SolutionName' or solutionId='00000000-0000-0000-0000-000000000000'
- Draft/inactive/disabled flows in solution: solutionUniqueName='MySolution' and filter=""statecode eq 0""
- Recently modified: filter=""modifiedon gt 2025-01-01T00:00:00Z""",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["solutionUniqueName"] = new()
                {
                    Type = "string",
                    Description = "Solution unique name to filter flows by solution membership. More efficient than filtering by solutionid."
                },
                ["solutionId"] = new()
                {
                    Type = "string",
                    Description = "Solution GUID to filter flows. Prefer solutionUniqueName when possible."
                },
                ["filter"] = new()
                {
                    Type = "string",
                    Description = "OData filter expression. Examples: \"name eq 'FlowName'\", \"contains(name, 'Test')\", \"statecode eq 1\", \"ismanaged eq false\". For date/time filters (createdon, modifiedon): MUST detect user's timezone first (run PowerShell command or ask), then convert to UTC. Do NOT assume local time is UTC. Combined with solution filter using AND."
                },
                ["select"] = new()
                {
                    Type = "array",
                    Description = "Additional fields beyond core set. Common: clientdata, primaryentity, triggeroncreate, category, solutionid.",
                    Items = new() { Type = "string" }
                },
                ["orderby"] = new()
                {
                    Type = "string",
                    Description = "Order results by field(s). Examples: 'modifiedon desc', 'name asc', 'createdon desc,name asc'. Provides stable ordering for consistent results."
                },
                ["top"] = new()
                {
                    Type = "integer",
                    Description = "Limit total records returned (default: 50, max: 5000). Use when you want a fixed number of results in a single response. Do NOT use with maxpagesize."
                },
                ["count"] = new()
                {
                    Type = "boolean",
                    Description = "Include total count of matching records (default: true). Returns @odata.count in response."
                },
                ["maxpagesize"] = new()
                {
                    Type = "integer",
                    Description = "Records per page for server-driven pagination (e.g., 10, 50, 100). Use when you need to paginate through results. Returns @odata.nextLink for fetching next page. Do NOT use with top."
                }
            },
            Required = Array.Empty<string>()
        }
    };

    public static ToolDefinition ActivateFlow => new()
    {
        Name = "dataverse_activate_flow",
        Description = "Activate a Power Automate flow by setting its state to Activated. Optionally validates connection references are configured before activating. Requires EnableWriteOperations=true in config.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["workflowId"] = new()
                {
                    Type = "string",
                    Description = "Workflow GUID to activate"
                },
                ["validateConnectionReferences"] = new()
                {
                    Type = "boolean",
                    Description = "Check connection references are configured before activating (default: false)"
                }
            },
            Required = new[] { "workflowId" }
        }
    };

    public static ToolDefinition DeactivateFlow => new()
    {
        Name = "dataverse_deactivate_flow",
        Description = "Deactivate a Power Automate flow by setting its state to Draft. Requires EnableWriteOperations=true in config.",
        InputSchema = new InputSchema
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["workflowId"] = new()
                {
                    Type = "string",
                    Description = "Workflow GUID to deactivate"
                }
            },
            Required = new[] { "workflowId" }
        }
    };

    public static IEnumerable<ToolDefinition> All => new[]
    {
        QueryFlows,
        ActivateFlow,
        DeactivateFlow
    };
}
