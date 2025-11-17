# MCP Server Implementation Plan
## Dataverse & DevOps Access via C# Client

### Overview
Build Model Context Protocol (MCP) server in C# for Dataverse access with Azure CLI authentication, supporting multiple environments. Future extensibility for Azure DevOps integration.

---

## Architecture

### Components
1. **MCP Server Host** - C# console application implementing MCP protocol
2. **Authentication Layer** - Azure CLI (`az login`) token acquisition
3. **Dataverse Client** - Service layer for Dataverse operations
4. **Configuration Manager** - Per-environment settings (dev/test/prod)
5. **DevOps Client** - Future: Azure DevOps API integration

### Technology Stack
- **Runtime**: .NET 8.0
- **MCP Protocol**: stdio transport
- **HTTP Client**: HttpClient with Azure token bearer auth
- **Config**: JSON files per environment
- **Auth**: Azure.Identity library (AzureCliCredential)

---

## Project Structure

```
pipe-dream-mcp/
├── src/
│   ├── PipeDreamMcp/
│   │   ├── Program.cs              # MCP server entry point
│   │   ├── McpServer.cs            # MCP protocol handler
│   │   ├── Auth/
│   │   │   └── AzureAuthProvider.cs
│   │   ├── Dataverse/
│   │   │   ├── DataverseClient.cs
│   │   │   └── DataverseTools.cs   # MCP tools definition
│   │   ├── DevOps/                 # Future
│   │   │   ├── DevOpsClient.cs
│   │   │   └── DevOpsTools.cs
│   │   └── Config/
│   │       └── EnvironmentConfig.cs
├── config/
│   ├── dev.json
│   ├── test.json
│   └── prod.json
├── README.md
└── PipeDreamMcp.sln
```

---

## Implementation Phases

### Phase 1: Foundation (Core MCP Server)
**Goal**: Working MCP server with stdio transport

**Tasks**:
1. Create .NET 8 console project
2. Add dependencies:
   - `Azure.Identity`
   - `System.Text.Json`
   - `Microsoft.Extensions.Configuration`
3. Implement MCP protocol basics:
   - JSON-RPC message handling (stdio)
   - `initialize` / `initialized` handshake
   - `tools/list` capability
   - `tools/call` execution
4. Basic logging to stderr

**Deliverable**: MCP server responds to protocol messages

---

### Phase 2: Authentication & Configuration
**Goal**: Azure CLI auth + environment config

**Tasks**:
1. Implement `AzureAuthProvider`:
   - Use `AzureCliCredential` from Azure.Identity
   - Cache tokens per environment
   - Handle token refresh
2. Create `EnvironmentConfig`:
   - Load from JSON files (dev/test/prod)
   - Properties: `DataverseUrl`, `TenantId`, `ClientId` (optional)
3. CLI argument: `--environment <env>` to select config
4. Validate az CLI installed and logged in

**Config Schema**:
```json
{
  "environment": "dev",
  "dataverse": {
    "url": "https://org.crm.dynamics.com",
    "apiVersion": "v9.2"
  },
  "tenantId": "00000000-0000-0000-0000-000000000000"
}
```

**Deliverable**: Server authenticates with Dataverse APIs

---

### Phase 3: Dataverse Client & Tools
**Goal**: Functional Dataverse operations via MCP tools

**Core Operations** (Read-Only for Safety):
1. **Query** - FetchXML or OData queries
2. **Retrieve** - Get single record by ID
3. **Metadata** - List entities/attributes
4. **List** - Browse entity collections with pagination

**MCP Tools Definition**:
```json
{
  "name": "dataverse_query",
  "description": "Execute OData query against Dataverse",
  "inputSchema": {
    "type": "object",
    "properties": {
      "entity": {"type": "string"},
      "select": {"type": "array"},
      "filter": {"type": "string"},
      "top": {"type": "number"}
    }
  }
}
```

**Implementation**:
1. Create `DataverseClient` with HttpClient
2. Implement Web API operations (OData)
3. Register tools in MCP server
4. Map tool calls to Dataverse operations
5. Return structured responses

**Deliverable**: AI agents can read and query Dataverse records safely

---

### Phase 4: Error Handling & Resilience
**Goal**: Production-ready error handling

**Tasks**:
1. Retry logic with exponential backoff
2. Token expiration handling
3. Rate limiting awareness
4. Detailed error messages in MCP responses
5. Validation of inputs before API calls
6. Handle network failures gracefully

**Deliverable**: Robust operation under various failure modes

---

### Phase 5: DevOps Integration (Future)
**Goal**: Extend to Azure DevOps APIs

**Operations** (Read-Only for Safety):
1. Work items (query, retrieve)
2. Repositories (list, get files, get content)
3. Pipelines (list, get runs, view logs)
4. Pull requests (list, get details, view comments)

**Implementation**:
1. Add DevOps config section
2. Create `DevOpsClient` (similar to Dataverse)
3. Register DevOps tools in MCP server
4. Reuse auth provider (az CLI tokens work for DevOps)

**Deliverable**: Multi-service MCP server

---

## MCP Server Configuration (Client-side)

### VS Code GitHub Copilot Configuration

Add to VS Code settings (`.vscode/settings.json` or User Settings):

```json
{
  "github.copilot.chat.mcp.servers": {
    "pipe-dream-dev": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "c:/repo/ryanmichaeljames/pipe-dream-mcp/src/PipeDreamMcp",
        "--environment",
        "dev"
      ]
    },
    "pipe-dream-prod": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "c:/repo/ryanmichaeljames/pipe-dream-mcp/src/PipeDreamMcp",
        "--environment",
        "prod"
      ]
    }
  }
}
```

**Alternative**: Global user settings at `%APPDATA%\Code\User\settings.json`

### Other MCP Clients (Optional)

**Claude Desktop** (`claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "pipe-dream-dev": {
      "command": "dotnet",
      "args": ["run", "--project", "c:/repo/ryanmichaeljames/pipe-dream-mcp/src/PipeDreamMcp", "--environment", "dev"]
    }
  }
}
```

**Cline** (VS Code extension settings):
```json
{
  "mcpServers": {
    "pipe-dream-dev": {
      "command": "dotnet",
      "args": ["run", "--project", "c:/repo/ryanmichaeljames/pipe-dream-mcp/src/PipeDreamMcp", "--environment", "dev"]
    }
  }
}
```

---

## Key Design Decisions

### Why C#?
- Native Azure SDK support
- Strong typing for Dataverse/DevOps models
- Excellent JSON serialization
- Cross-platform with .NET 8

### Why Azure CLI Auth?
- No credential management in code
- Works across dev/CI environments
- Unified auth for Dataverse + DevOps
- MFA support automatic

### Why Per-Environment Config?
- Safe defaults per environment
- Prevent accidental prod operations
- Different tenants/orgs supported
- Easy switching without code changes

### Why stdio Transport?
- Standard MCP pattern
- Simple debugging (pipe to file)
- No networking concerns
- Direct process communication

---

## Testing Strategy

### Unit Tests
- Auth token acquisition mocking
- Config loading validation
- Tool parameter validation
- HTTP client mocking

### Integration Tests
- Actual Dataverse API calls (dev environment)
- Full MCP protocol flow
- Multi-tool scenarios

### Manual Testing
- Connect from Claude Desktop / Cline
- Execute queries via AI agent
- Verify logging and errors

---

## Security Considerations

1. **Token Handling**: Never log tokens, use memory-only storage
2. **Config Files**: Exclude sensitive data, use Azure Key Vault references if needed
3. **Input Validation**: Sanitize all user inputs before API calls
4. **RBAC**: Respect Dataverse security roles (operations run as authenticated user)
5. **Audit**: Log all operations to stderr for audit trail

---

## Success Criteria

- [ ] MCP server starts and responds to protocol
- [ ] Azure CLI authentication works
- [ ] Can switch environments via CLI argument
- [ ] Query Dataverse entities successfully
- [ ] Retrieve operations work (read-only)
- [ ] Proper error messages in MCP responses
- [ ] AI agent can use tools naturally
- [ ] Documented setup in README.md

---

## Timeline Estimate

- **Phase 1**: 4-6 hours (MCP foundation)
- **Phase 2**: 3-4 hours (Auth + Config)
- **Phase 3**: 6-8 hours (Dataverse implementation)
- **Phase 4**: 2-3 hours (Error handling)
- **Phase 5**: 6-8 hours (DevOps - future)

**Total Core Implementation**: ~15-20 hours

---

## Next Steps

1. Review and approve plan
2. Create project structure
3. Begin Phase 1 implementation
4. Iterate with testing between phases
5. Document usage in README

---

## References

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
- [Azure.Identity Library](https://learn.microsoft.com/dotnet/api/azure.identity)
- [Azure CLI Authentication](https://learn.microsoft.com/cli/azure/authenticate-azure-cli)
