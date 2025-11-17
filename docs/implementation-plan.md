# MCP Server Implementation Plan
## Dataverse & DevOps Access via C# Client

### Overview
Build Model Context Protocol (MCP) server in C# for Dataverse access with Azure CLI authentication, supporting multiple environments. Future extensibility for Azure DevOps integration.

---

## Implementation Progress

### ✅ Completed
- [x] **Phase 1: MCP Protocol Foundation** (100%)
  - [x] .NET 8 console project created
  - [x] MCP protocol message handling (stdio)
  - [x] Initialize/tools/list handlers
  - [x] Stderr logging
  - [x] Test suite created (3/3 tests passing)
- [x] **Phase 2: Auth & Config** (100%)
  - [x] EnvironmentConfig classes created
  - [x] ConfigLoader with multi-source priority
  - [x] AzureAuthProvider with token caching
  - [x] CLI argument parsing (--environment, --config-dir, --help, --version)
  - [x] Program.cs integration with validation
  - [x] Config files created (dev/test/prod)
  - [x] Auth flow verified with Azure CLI

### 🚧 In Progress
- [ ] **Phase 3: Dataverse Client & Tools** (0%)

### 📋 Pending
- [ ] Phase 4: Error Handling & Resilience
- [ ] Phase 5: Integration & Testing
- [ ] Phase 6: Release & Distribution
- [ ] Phase 7: DevOps Integration (Future)

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

### Phase 5: Integration & Testing
**Goal**: Wire all components and validate end-to-end

**Tasks**:
1. Integrate auth, config, and Dataverse client in `Program.cs`
2. Add startup validation (az CLI, config, network)
3. Implement graceful shutdown
4. End-to-end testing from VS Code Copilot
5. Update documentation with usage examples

**Deliverable**: Fully functional MCP server ready for distribution

---

### Phase 6: Release & Distribution
**Goal**: Package and publish for users

**Tasks**:
1. Configure `.csproj` for NuGet global tool packaging
2. Set up GitHub Actions workflow for automated publishing
3. Configure NuGet API key as GitHub secret
4. Create release documentation (installation, upgrade, troubleshooting)
5. Tag first release and verify NuGet publish
6. Update README with installation instructions

**Build order:**
1. Add NuGet package metadata to `.csproj` → **Test local pack**
2. Create `.github/workflows/publish-nuget.yml` → **Test workflow syntax**
3. Add `NUGET_API_KEY` to GitHub secrets → **Verify secret access**
4. Create version tag (`v0.1.0`) → **Test auto-publish**
5. Verify package on NuGet.org → **Test user installation**
6. Update README and docs → **Publish final docs**

**Release process:**
```powershell
# 1. Update version in .csproj
# 2. Commit changes
git commit -am "Release v0.1.0"

# 3. Create and push tag
git tag v0.1.0
git push origin v0.1.0

# 4. GitHub Actions automatically publishes to NuGet
```

**Deliverable**: Published NuGet package available for global installation

---

### Phase 7: DevOps Integration (Future)
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

## Distribution & Packaging

### NuGet Global Tool (Recommended)

**Setup .csproj for global tool:**
```xml
<PropertyGroup>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>pipe-dream-mcp</ToolCommandName>
  <PackageId>PipeDreamMcp</PackageId>
  <Version>0.1.0</Version>
  <Authors>Ryan Michael James</Authors>
  <Description>MCP server for read-only access to Microsoft Dataverse and Azure DevOps</Description>
  <PackageProjectUrl>https://github.com/ryanmichaeljames/pipe-dream-mcp</PackageProjectUrl>
  <RepositoryUrl>https://github.com/ryanmichaeljames/pipe-dream-mcp</RepositoryUrl>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageTags>mcp;dataverse;devops;copilot</PackageTags>
</PropertyGroup>
```

**Automated publishing via GitHub Actions:**

Create `.github/workflows/publish-nuget.yml`:
```yaml
name: Publish to NuGet

on:
  push:
    tags:
      - 'v*.*.*'  # Triggers on version tags like v0.1.0

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    - run: dotnet restore
    - run: dotnet build --configuration Release --no-restore
    - run: dotnet test
    - run: dotnet pack src/PipeDreamMcp --configuration Release --output ./nupkg
    - run: dotnet nuget push ./nupkg/*.nupkg --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }}
```

**Setup:**
1. Get NuGet API key from https://www.nuget.org/account/apikeys
2. Add as GitHub secret: Settings > Secrets > Actions > `NUGET_API_KEY`
3. Tag release: `git tag v0.1.0 && git push origin v0.1.0`
4. Workflow automatically publishes to NuGet.org

**Manual testing locally:**
```powershell
# Pack locally
dotnet pack src/PipeDreamMcp -c Release -o ./nupkg

# Test local install
dotnet tool install --global --add-source ./nupkg PipeDreamMcp
```

**Users install:**
```powershell
# Install
dotnet tool install --global PipeDreamMcp

# Update
dotnet tool update --global PipeDreamMcp

# Uninstall
dotnet tool uninstall --global PipeDreamMcp
```

**VS Code configuration:**
```json
{
  "github.copilot.chat.mcp.servers": {
    "pipe-dream-dev": {
      "command": "pipe-dream-mcp",
      "args": ["--environment", "dev"]
    }
  }
}
```

### Release Process

**Publishing new version:**
1. Update version in `PipeDreamMcp.csproj`
2. Commit changes: `git commit -am "Release v0.1.0"`
3. Create tag: `git tag v0.1.0`
4. Push tag: `git push origin v0.1.0`
5. GitHub Actions automatically publishes to NuGet.org

**Users upgrade:**
```powershell
dotnet tool update --global PipeDreamMcp
```

### Alternative: Self-Contained Executable (Future)

For users without .NET SDK, consider GitHub Releases with binaries:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/win-x64
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

### Why NuGet Global Tool?
- Easy install/update for users
- Version management built-in
- Works from any directory
- Standard .NET distribution
- Automatic PATH configuration

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
- Connect from GitHub Copilot in VS Code
- Test with multiple environments
- Verify config switching
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

## Next Steps

1. Complete Phase 2: Auth & Config
2. Implement Phase 3: Dataverse Client & Tools
3. Add Phase 4: Error Handling & Resilience
4. Test Phase 5: Integration & Testing
5. Release Phase 6: Distribution via NuGet
6. Future Phase 7: DevOps Integration

---

## References

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
- [Azure.Identity Library](https://learn.microsoft.com/dotnet/api/azure.identity)
- [Azure CLI Authentication](https://learn.microsoft.com/cli/azure/authenticate-azure-cli)
