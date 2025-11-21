---
applyTo: '**/*'
---

# MCP Server Implementation Instructions

## Context
You are implementing a Model Context Protocol (MCP) server in C# for access to Microsoft Dataverse, Power Platform, and Azure DevOps. The server uses Azure CLI authentication with inline or file-based configuration.

## Core Principles

1. **Test as you build** - Verify each component immediately after creation
2. **Keep it simple** - Prefer clarity over cleverness
3. **AI-friendly code** - Clear naming, single responsibility, minimal abstraction
4. **Fail fast** - Validate early, provide clear error messages

---

## Technology Stack
- **.NET 10.0** - Target framework
- **Azure CLI** - Authentication via `az login` (not Azure.Identity library)
- **System.Text.Json** - JSON serialization with `JsonDocument` for API responses
- **HttpClient** - Direct API calls with retry logic
- **File-based logging** - Serilog for operational logs; stderr reserved for fatal errors only per MCP protocol

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
- Config files in JSON format (no `environment` property needed)
- Use strongly-typed config classes (`EnvironmentConfig`, `DataverseConfig`, etc.)
- Dataverse: Config file optional (can use `--dataverse-url` inline)
- Power Platform: Config file required (uses fixed API URL https://api.powerplatform.com)
- Azure DevOps: Config file required
- Validate required fields on load, fail fast with clear errors

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

### Tool Handler Pattern
```csharp
public class DataverseQueryToolHandler : IToolHandler
{
    public string ToolName => "dataverse_query";
    public ToolDefinition Definition => DataverseTools.Query;
    
    public async Task<string> ExecuteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        // Extract and validate arguments
        var entity = arguments?.GetProperty("entity").GetString();
        if (string.IsNullOrWhiteSpace(entity))
            throw new InvalidOperationException("entity parameter required");
        
        // Call service and return JSON string
        var result = await _service.QueryAsync(entity, cancellationToken);
        return result.RootElement.GetRawText();
    }
}
```
Why: Consistent pattern, throws exceptions for errors (not McpMessage)

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
- [ ] Config switches with --config-file flag
- [ ] Dataverse API calls work with real environment

### Manual Test from VS Code GitHub Copilot
Configure in VS Code settings (requires published package or local build):
```json
{
  "servers": {
    "pipe-dream": {
      "command": "pipedream-mcp",
      "args": ["dataverse", "--config-file", "path/to/config.json"]
    }
  }
}
```

---

## Common Pitfalls to Avoid

❌ **Don't** hardcode URLs or credentials
✅ **Do** use config file

❌ **Don't** swallow exceptions silently
✅ **Do** log to files (Serilog), only fatal errors to stderr

❌ **Don't** use complex inheritance hierarchies
✅ **Do** use simple interfaces and composition

❌ **Don't** implement all tools before testing one
✅ **Do** test each tool individually as you build

❌ **Don't** assume az CLI is logged in
✅ **Do** validate and provide helpful error messages

❌ **Don't** use Console.WriteLine for logging
✅ **Do** use file-based logging (Serilog)

❌ **Don't** return `McpMessage` from tool handlers
✅ **Do** throw exceptions for errors, return JSON strings for success

---

## Debugging Tips

### Test MCP Protocol Manually

**IMPORTANT: PowerShell String Escaping**

Always use here-strings (`@' ... '@`) for JSON messages. PowerShell treats semicolons (`;`) as command separators, which breaks JSON containing filters or complex arguments.

**Parse JSON responses** using `ConvertFrom-Json | ConvertTo-Json` to get readable output instead of escaped Unicode characters.

```powershell
# Use here-strings to avoid escaping issues
$msg = @'
{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"dataverse_query","arguments":{"entity":"solutions","select":["uniquename","friendlyname"],"filter":"ismanaged eq false","top":10}}}
'@
$msg | dotnet run --project src/PipeDream.Mcp -- --dataverse-url https://your-org.crm.dynamics.com | ConvertFrom-Json | ConvertTo-Json -Depth 10

# For simpler parsing of tool results:
$response = $msg | dotnet run --project src/PipeDream.Mcp -- --dataverse-url https://your-org.crm.dynamics.com | ConvertFrom-Json
$response.result.content[0].text | ConvertFrom-Json | ConvertTo-Json
```

### Check Azure CLI Auth
```powershell
az account show
az account get-access-token --resource https://org.crm.dynamics.com
```

### Enable Verbose Logging
Add to config: `"logging": { "level": "debug" }` or use `--verbose` flag

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
- **Power Platform API:** https://learn.microsoft.com/en-us/rest/api/power-platform/
- **Azre DevOps API:** https://learn.microsoft.com/en-us/rest/api/azure/devops/?view=azure-devops-rest-7.2
- **Azure CLI:** https://learn.microsoft.com/en-us/cli/azure/reference-index?view=azure-cli-latest
- **GitHub Copilot MCP:** https://docs.github.com/en/copilot/how-tos/provide-context/use-mcp/extend-copilot-chat-with-mcp
- **Keep a Changelog:** https://keepachangelog.com/en/1.0.0/
- **Semantic Versioning:** https://semver.org/spec/v2.0.0.html

---

## Documentation Requirements

### README.md Maintenance
**Always keep README.md current** - Update after implementing features or making significant changes.

The README should include:
- **Project overview** - Brief description of the MCP server and its purpose
- **Prerequisites** - Required tools (az CLI, .NET 10, active Azure subscription)
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

### CHANGELOG.md Maintenance
**Always keep CHANGELOG.md current** - Follow Keep a Changelog standard (https://keepachangelog.com/en/1.0.0/).

**Structure:**
- `## [Unreleased]` - Changes not yet released
- `## [Version] - YYYY-MM-DD` - Released versions in descending order

**Change Categories (in order):**
- `### Added` - New features
- `### Changed` - Changes in existing functionality
- `### Deprecated` - Soon-to-be removed features
- `### Removed` - Removed features
- `### Fixed` - Bug fixes
- `### Security` - Vulnerability fixes

**When to update:**
- Add new MCP tools → `### Added`
- Modify existing tool behavior → `### Changed`
- Remove tools or features → `### Removed`
- Fix bugs → `### Fixed`
- Change config format → `### Changed`
- Breaking changes → Note in `### Changed` with **BREAKING:** prefix

**Example:**
```markdown
## [Unreleased]

### Added
- Power Platform environment management tools
- `dataverse_whoami` tool for authentication verification

### Changed
- **BREAKING:** Removed `environment` property from config files
- Log file naming simplified to `pipedream-mcp-{subcommand}-{org}-{date}.log`

### Fixed
- Authentication token caching for improved performance
```

---

## Output Requirements

When implementing:
- Create clean, well-structured C# code
- Follow .NET naming conventions (PascalCase for public members)
- Include XML documentation comments for public APIs
- Use async/await for all I/O operations
- Log events to file (Serilog), only fatal startup errors to stderr
- Return structured error messages in MCP responses
- Test each component immediately after creation
- **Update README.md and CHANGELOG.md** when adding features

**File naming:** Use descriptive names matching class names (e.g., `DataverseClient.cs`, `AzureAuthProvider.cs`)
