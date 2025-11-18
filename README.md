# PipeDream MCP

Model Context Protocol (MCP) server for read-only access to Microsoft Dataverse and Azure DevOps.

## Overview

PipeDream MCP is a C# server implementing the Model Context Protocol, enabling AI agents (like GitHub Copilot) to query Microsoft Dataverse safely. Uses Azure CLI authentication and supports multiple environments.

**Key Features:**
- ✅ **Read-only operations** - Query, retrieve, list, and browse Dataverse data safely
- ✅ **Azure CLI authentication** - No credential storage, token caching with automatic refresh
- ✅ **Multi-environment support** - Switch between dev/test/prod configs
- ✅ **4 Dataverse tools** - Query, retrieve, metadata, and list operations
- ✅ **OData support** - Full OData query capabilities (select, filter, top, pagination)
- ✅ **Production-ready error handling** - Retry logic, rate limiting, validation
- ✅ **stdio transport** - Standard MCP protocol over stdin/stdout

## Prerequisites

- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Azure CLI** - [Install](https://learn.microsoft.com/cli/azure/install-azure-cli)
- **Active Azure subscription** with access to Dataverse
- **Authenticated az CLI session** - Run `az login`

## Installation

1. Clone the repository:
```powershell
git clone https://github.com/ryanmichaeljames/pipe-dream-mcp.git
cd pipe-dream-mcp
```

2. Build the project:
```powershell
dotnet build
```

3. Verify installation:
```powershell
dotnet run --project src/PipeDreamMcp
```

## Configuration

### Environment Config Files

Create JSON config files in the `config/` directory for each environment:

**config/dev.json:**
```json
{
  "environment": "dev",
  "dataverse": {
    "url": "https://your-org.crm.dynamics.com",
    "apiVersion": "v9.2",
    "timeout": 30
  },
  "logging": {
    "level": "info"
  }
}
```

**Configuration Options:**
- `environment` - Environment name (matches filename)
- `dataverse.url` - Dataverse organization URL (trailing slash optional)
- `dataverse.apiVersion` - Web API version (default: v9.2)
- `dataverse.timeout` - HTTP timeout in seconds (default: 30)
- `logging.level` - Log level: `debug`, `info`, `warn`, `error`

### Azure CLI Authentication

Ensure you're logged in with appropriate permissions:
```powershell
az login
az account show
```

## Usage

### Running the Server

Start the MCP server for a specific environment:
```powershell
dotnet run --project src/PipeDreamMcp/PipeDreamMcp.csproj -- --environment dev
```

**Command-line options:**
- `--environment <name>` - Environment to use (required)
- `--config-dir <path>` - Custom config directory (optional)
- `--version` - Show version information
- `--help` - Show help message

### VS Code GitHub Copilot Integration

Add to your VS Code settings (`.vscode/settings.json` or User Settings):

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
    }
  }
}
```

Reload VS Code window and open GitHub Copilot Chat to start using the MCP tools.

## Available Tools

### Dataverse Tools (✅ Implemented)

#### `dataverse_query`
Execute OData queries against Dataverse entities.

**Parameters:**
- `entity` (required) - Entity logical name (e.g., `accounts`, `contacts`)
- `select` (optional) - Array of field names to return
- `filter` (optional) - OData filter expression
- `top` (optional) - Maximum number of records

**Examples:**
```json
// Basic query
{
  "entity": "accounts",
  "select": ["name", "accountid"],
  "filter": "statecode eq 0",
  "top": 10
}

// Advanced filtering
{
  "entity": "solutions",
  "select": ["uniquename", "friendlyname", "version"],
  "filter": "startswith(uniquename, 'Contoso') and ismanaged eq false",
  "top": 50
}
```

#### `dataverse_retrieve`
Retrieve a single record by its ID.

**Parameters:**
- `entity` (required) - Entity logical name
- `id` (required) - GUID of the record
- `select` (optional) - Array of field names to return

#### `dataverse_metadata`
Get metadata about Dataverse entities.

**Parameters:**
- `entity` (optional) - Specific entity to get metadata for (omit to list all entities)

#### `dataverse_list`
List records from an entity with pagination.

**Parameters:**
- `entity` (required) - Entity logical name
- `pageSize` (optional) - Records per page (default: 50, max: 250)
- `pagingCookie` (optional) - Paging token from previous response

### Azure DevOps Tools (📋 Planned)
- `devops_workitems_query` - Query work items
- `devops_repos_list` - List repositories
- `devops_pipelines_list` - List pipelines

## Testing

Run the test suite:
```powershell
.\tests\run-tests.ps1
```

**Test Coverage:**
- ✅ Initialize handshake
- ✅ Tools/list returns 4 Dataverse tools with full schemas
- ✅ Invalid method error handling
- ✅ Real Dataverse environment tested (teaureka-coredev)
- ✅ OData queries validated (filter, select, top)
- ✅ Entity name validation (rejects invalid characters)
- ✅ GUID format validation (clear error messages)
- ✅ SQL injection prevention (blocks --, /*, exec patterns)
- ✅ Metadata queries (returns full entity list)
- ✅ Azure CLI authentication with token caching

**Manual Testing Examples:**
```powershell
# Test tools/list
$msg = '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
$msg | dotnet run --project src/PipeDreamMcp -- --environment dev

# Test query operation
$msg = '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"dataverse_query","arguments":{"entity":"solutions","select":["uniquename","friendlyname"],"top":5}}}'
$msg | dotnet run --project src/PipeDreamMcp -- --environment dev

# Test retrieve operation
$msg = '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"dataverse_retrieve","arguments":{"entity":"solutions","id":"12345678-1234-1234-1234-123456789012"}}}'
$msg | dotnet run --project src/PipeDreamMcp -- --environment dev

# Test metadata query
$msg = '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"dataverse_metadata"}}'
$msg | dotnet run --project src/PipeDreamMcp -- --environment dev
```

## Development

### Project Structure

```
pipe-dream-mcp/
├── src/
│   └── PipeDreamMcp/
│       ├── Program.cs                      # Entry point & orchestration
│       ├── Protocol/
│       │   ├── McpMessage.cs               # MCP protocol models
│       │   └── McpServer.cs                # Message handler & tool router
│       ├── Auth/
│       │   └── AzureAuthProvider.cs        # Azure CLI token management
│       ├── Dataverse/
│       │   ├── DataverseClient.cs          # HTTP client for Web API
│       │   └── DataverseTools.cs           # Tool definitions & schemas
│       ├── Config/
│       │   ├── EnvironmentConfig.cs        # Config models
│       │   └── ConfigLoader.cs             # Multi-source config loading
│       └── Common/
│           ├── RetryHelper.cs              # Exponential backoff & rate limiting
│           └── InputValidator.cs           # Parameter validation
├── config/                                 # Environment configs (dev/test/prod)
├── tests/                                  # MCP protocol test suite
├── docs/                                   # Implementation documentation
└── README.md
```

### Implementation Status

- [x] **Phase 1: MCP Protocol Foundation** (100%)
  - JSON-RPC message handling over stdio
  - Initialize/tools/list/tools/call handlers
  - Stderr logging
- [x] **Phase 2: Auth & Config** (100%)
  - Azure CLI authentication with token caching
  - Multi-environment configuration
  - CLI argument parsing
- [x] **Phase 3: Dataverse Client & Tools** (100%)
  - 4 Dataverse tools implemented (query, retrieve, metadata, list)
  - OData query support with filters
  - Full read-only operations
  - Real environment testing validated
- [x] **Phase 4: Error Handling & Resilience** (100% - ✅ COMPLETE & TESTED)
  - ✅ Exponential backoff retry logic (3 retries, 1s/2s/4s delays)
  - ✅ Rate limiting awareness with Retry-After header parsing
  - ✅ Enhanced token expiration handling with retry (2 retries)
  - ✅ Comprehensive input validation (entities, GUIDs, filters, limits)
  - ✅ SQL injection prevention with pattern detection
  - ✅ User-friendly error messages for all HTTP status codes
  - ✅ Network failure detection (connection, DNS, timeout)
  - ✅ Connection pooling with SocketsHttpHandler (10min lifetime)
  - ✅ All validation tested with real environment (teaureka-coredev)
- [ ] **Phase 5: Integration & Testing** (Next)
  - End-to-end VS Code GitHub Copilot integration
  - Multi-environment switching validation
  - Performance testing under load
  - Complete documentation validation
- [ ] **Phase 6: Release & Distribution**
- [ ] **Phase 7: Azure DevOps Integration**

See [docs/implementation-plan.md](docs/implementation-plan.md) for details.

## Error Handling & Resilience

PipeDream MCP includes production-ready error handling:

### Automatic Retry Logic
- **Exponential backoff** - 3 retries with increasing delays (1s, 2s, 4s)
- **Transient error detection** - Automatically retries timeouts, network errors, 429/500/502/503/504 responses
- **Rate limit awareness** - Respects `Retry-After` headers from 429 responses

### Input Validation
- **Entity names** - Max 128 chars, alphanumeric + underscore only
- **GUIDs** - Validated format with clear error messages
- **Field names** - Max 50 fields, validated format
- **Filter expressions** - Max 1000 chars, SQL injection prevention
- **Page sizes** - 1-250 for list, 1-5000 for query top

### User-Friendly Error Messages
All errors return actionable guidance:
- **400 Bad Request** - "Invalid request format. Check entity name and filter syntax."
- **401 Unauthorized** - "Authentication failed. Verify Azure CLI is logged in with correct permissions."
- **404 Not Found** - "Resource not found. Verify the entity name and record ID are correct."
- **429 Rate Limited** - "Rate limit exceeded. The server will automatically retry with appropriate delays."
- **Network errors** - Specific messages for connection, DNS, timeout issues

### Token Management
- **Automatic refresh** - Tokens refreshed 5 minutes before expiration
- **Token retry** - Up to 2 retries for transient auth failures
- **Detailed logging** - Token expiration times logged for debugging

## Troubleshooting

### Server won't start
- Verify .NET 8 SDK installed: `dotnet --version`
- Check project builds: `dotnet build src/PipeDreamMcp/PipeDreamMcp.csproj`
- Review stderr logs for detailed error messages

### Azure CLI authentication fails
**Error: "Azure CLI is not installed"**
- Install from https://learn.microsoft.com/cli/azure/install-azure-cli

**Error: "Azure CLI is not authenticated"**
- Run `az login` and authenticate with your Azure account

**Error: "Access denied"**
- Verify account has permissions: `az account show`
- Check Dataverse access: `az account get-access-token --resource https://org.crm.dynamics.com`
- Ensure your Azure account has permissions to the Dataverse environment

### Query errors
**Error: "Invalid request format"**
- Verify entity name is correct (plural form, e.g., `accounts` not `account`)
- Check OData filter syntax
- Validate field names in select array

**Error: "Resource not found"**
- Confirm entity exists in Dataverse: Use `dataverse_metadata` tool
- Check entity logical name (not display name)
- Verify record GUID format for retrieve operations

### GitHub Copilot can't find tools
- Verify server path in settings is absolute (e.g., `c:/repo/.../src/PipeDreamMcp`)
- Reload VS Code window after settings change (`Ctrl+Shift+P` → "Developer: Reload Window")
- Check stderr logs in Output panel: View → Output → Select "GitHub Copilot Chat"

### Network and timeout issues
**Error: "Request timed out"**
- Increase timeout in config: `"timeout": 60`
- Reduce query complexity (fewer fields, smaller top value)
- Check network connection to Dataverse

**Error: "Network is unreachable"**
- Verify internet connection
- Check if Dataverse URL is accessible: Test in browser
- Review firewall/proxy settings

### MCP protocol errors
- Test manually: `'{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project src/PipeDreamMcp -- --environment dev`
- Review stderr logs for detailed error messages
- Verify JSON-RPC message format matches MCP specification

## Contributing

This is a personal project for learning MCP and Dataverse integration. Contributions welcome!

## License

MIT License - See LICENSE file for details.

## References

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
- [Azure.Identity Library](https://learn.microsoft.com/dotnet/api/azure.identity)
- [GitHub Copilot MCP Support](https://docs.github.com/copilot)
