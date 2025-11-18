---
applyTo: '**/*'
---

# MCP Server Implementation Instructions

## Context
You are implementing a Model Context Protocol (MCP) server in C# for access to Microsoft Dataverse and Azure DevOps. The server uses Azure CLI authentication and supports multiple environments.

## Core Principles

1. **Test as you build** - Verify each component immediately after creation
2. **Keep it simple** - Prefer clarity over cleverness
3. **AI-friendly code** - Clear naming, single responsibility, minimal abstraction
4. **Fail fast** - Validate early, provide clear error messages

---

## Technology Stack
- **.NET 8.0** - Target framework
- **Azure.Identity** - For Azure CLI credential (`AzureCliCredential`)
- **System.Text.Json** - JSON serialization
- **Microsoft.Extensions.Configuration** - Config file loading
- **HttpClient** - Dataverse/DevOps API calls

## Implementation Rules

### Code Style
- **Explicit over implicit** - No magic strings, use constants
- **One responsibility per class** - Easy to understand and modify
- **Descriptive names** - `DataverseQueryTool` not `Tool1`
- **Minimal nesting** - Early returns, guard clauses
- **Comments only when non-obvious** - Code should self-document

### Testing Discipline
After implementing each component:
1. Write minimal test to verify functionality
2. Run test immediately
3. Fix any issues before proceeding
4. Only move to next component when current works

### Error Handling Pattern
```csharp
// Always validate inputs first
if (string.IsNullOrEmpty(entity))
    return Error("entity parameter required");

// Try-catch with specific error messages
try {
    return await client.QueryAsync(entity);
}
catch (HttpRequestException ex) {
    return Error($"Dataverse API error: {ex.Message}");
}
```

### Configuration
- Load config on startup, fail if invalid
- Use strongly-typed config classes
- Validate all required fields present
- Log config used (except secrets)

---

## Code Structure Guidelines

### Keep Files Small
- Max 200 lines per file
- Split large classes into multiple files
- Use partial classes if needed

### Dependency Injection Pattern
```csharp
public class McpServer
{
    private readonly IAuthProvider _auth;
    private readonly IDataverseClient _client;
    
    public McpServer(IAuthProvider auth, IDataverseClient client)
    {
        _auth = auth;
        _client = client;
    }
}
```
Why: Easy to mock for testing, clear dependencies

### Tool Schema Pattern
```csharp
public static ToolDefinition QueryTool => new()
{
    Name = "dataverse_query",
    Description = "Query Dataverse entities using OData",
    InputSchema = new()
    {
        Type = "object",
        Properties = new()
        {
            ["entity"] = new() { Type = "string", Description = "Entity logical name" },
            ["select"] = new() { Type = "array", Description = "Fields to return" },
            ["filter"] = new() { Type = "string", Description = "OData filter" }
        },
        Required = new[] { "entity" }
    }
};
```
Why: Self-documenting, validates parameters automatically

---

## Testing Checklist

### Unit Tests (Optional but Recommended)
- Config loading with valid/invalid JSON
- Auth provider with mocked Azure CLI
- Tool parameter validation
- Error message formatting

### Integration Tests (Required)
- [ ] MCP server starts and responds to initialize
- [ ] tools/list returns all tools
- [ ] tools/call executes dataverse_query successfully
- [ ] Invalid tool call returns proper error
- [ ] Auth token acquired from az CLI
- [ ] Config switches with --environment flag
- [ ] Dataverse API calls work with real environment

### Manual Test from VS Code GitHub Copilot
1. Add server to settings:
```json
{
  "github.copilot.chat.mcp.servers": {
    "pipe-dream-dev": {
      "command": "dotnet",
      "args": ["run", "--project", "c:/repo/ryanmichaeljames/pipe-dream-mcp/src/PipeDream.Mcp", "--environment", "dev"]
    }
  }
}
```
2. Reload VS Code window
3. Open Copilot Chat
4. Ask: "What Dataverse entities are available?"
5. Verify tool execution and response

**Note:** Server must be running and responding to MCP protocol for Copilot to use it.

---

## Common Pitfalls to Avoid

❌ **Don't** hardcode URLs or credentials
✅ **Do** use config files

❌ **Don't** swallow exceptions silently
✅ **Do** log to stderr and return error messages

❌ **Don't** use complex inheritance hierarchies
✅ **Do** use simple interfaces and composition

❌ **Don't** implement all tools before testing one
✅ **Do** test each tool individually as you build

❌ **Don't** assume az CLI is logged in
✅ **Do** validate and provide helpful error messages

❌ **Don't** use Console.WriteLine for logging
✅ **Do** write logs to stderr (Console.Error.WriteLine)

---

## Debugging Tips

### Test MCP Protocol Manually

**IMPORTANT: PowerShell String Escaping**

Always use here-strings (`@' ... '@`) for JSON messages. PowerShell treats semicolons (`;`) as command separators, which breaks JSON containing filters or complex arguments.

```powershell
# Use here-strings to avoid escaping issues
$msg = @'
{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"dataverse_query","arguments":{"entity":"solutions","select":["uniquename","friendlyname"],"filter":"ismanaged eq false","top":10}}}
'@
$msg | dotnet run --project src/PipeDream.Mcp -- --environment dev
```

### Check Azure CLI Auth
```powershell
az account show
az account get-access-token --resource https://org.crm.dynamics.com
```

### Enable Verbose Logging
Add to config: `"logLevel": "debug"` and log everything to stderr

### Test Dataverse API Directly
```powershell
$token = az account get-access-token --resource https://org.crm.dynamics.com --query accessToken -o tsv
curl -H "Authorization: Bearer $token" https://org.crm.dynamics.com/api/data/v9.2/EntityDefinitions
```

---

## Success Metrics

✅ Agent understands code within 30 seconds of reading
✅ New tool added in under 30 minutes
✅ Bugs reproducible with simple test
✅ Error messages guide to solution
✅ No component depends on more than 3 others

---

## Maintenance Philosophy

- **When adding features:** Test in isolation first
- **When fixing bugs:** Add test that reproduces issue
- **When refactoring:** Keep tests passing at each step
- **When confused:** Simplify, don't add abstraction

**Remember:** Code is read 10x more than written. Optimize for clarity.

---

## References

- **MCP Specification:** https://spec.modelcontextprotocol.io/
- **Dataverse Web API:** https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview
- **Azure.Identity Docs:** https://learn.microsoft.com/dotnet/api/azure.identity
- **GitHub Copilot MCP Support:** Configure via `github.copilot.chat.mcp.servers` in VS Code settings

---

## Documentation Requirements

### README.md Maintenance
**Always keep README.md current** - Update after implementing features or making significant changes.

The README should include:
- **Project overview** - Brief description of the MCP server and its purpose
- **Prerequisites** - Required tools (az CLI, .NET 8, active Azure subscription)
- **Installation** - Step-by-step setup instructions
- **Configuration** - How to set up environment config files
- **Usage** - How to run the server and configure in VS Code
- **Available Tools** - List of MCP tools with descriptions and examples
- **Testing** - How to run tests
- **Troubleshooting** - Common issues and solutions

Update README.md when:
- Adding new MCP tools or capabilities
- Changing configuration format or options
- Adding new environment support
- Modifying setup/installation steps
- Completing major implementation phases

Keep documentation:
- **Concise** - One sentence per concept when possible
- **Practical** - Show actual commands and examples
- **Current** - Remove outdated information immediately
- **AI-friendly** - Use clear structure with headers and code blocks

---

## Output Requirements

When implementing:
- Create clean, well-structured C# code
- Follow .NET naming conventions (PascalCase for public members)
- Include XML documentation comments for public APIs
- Use async/await for all I/O operations
- Log important events and errors to stderr
- Return structured error messages in MCP responses
- Test each component immediately after creation
- **Update README.md** when adding features or changing configuration

**File naming:** Use descriptive names matching class names (e.g., `DataverseClient.cs`, `AzureAuthProvider.cs`)
