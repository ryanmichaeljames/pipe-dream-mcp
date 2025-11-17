# PipeDream MCP

Model Context Protocol (MCP) server for read-only access to Microsoft Dataverse and Azure DevOps.

## Overview

PipeDream MCP is a C# server implementing the Model Context Protocol, enabling AI agents (like GitHub Copilot) to query Microsoft Dataverse and Azure DevOps data safely. Uses Azure CLI authentication and supports multiple environments.

**Key Features:**
- Read-only operations (query, retrieve, list metadata)
- Azure CLI authentication (no credential storage)
- Multi-environment support (dev/test/prod)
- stdio transport for MCP protocol
- Comprehensive error handling and logging

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
    "apiVersion": "v9.2"
  },
  "tenantId": "00000000-0000-0000-0000-000000000000"
}
```

**Supported environments:** `dev`, `test`, `prod`

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
dotnet run --project src/PipeDreamMcp --environment dev
```

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

### Phase 1 (Complete)
✓ **MCP Protocol Foundation** - Basic JSON-RPC message handling over stdio

### Planned Tools

**Dataverse:**
- `dataverse_query` - Execute OData queries
- `dataverse_retrieve` - Get single record by ID
- `dataverse_metadata` - List entities and attributes
- `dataverse_list` - Browse entity collections

**Azure DevOps (Future):**
- `devops_workitems_query` - Query work items
- `devops_repos_list` - List repositories
- `devops_pipelines_list` - List pipelines
- `devops_pullrequests_list` - List pull requests

## Testing

Run the test suite:
```powershell
.\tests\run-tests.ps1
```

**Current tests:**
- Initialize handshake
- Tools/list response
- Invalid method error handling

## Development

### Project Structure

```
pipe-dream-mcp/
├── src/
│   └── PipeDreamMcp/
│       ├── Program.cs              # Entry point
│       ├── Protocol/
│       │   ├── McpMessage.cs       # Protocol models
│       │   └── McpServer.cs        # MCP handler
│       ├── Auth/                   # Future: Azure auth
│       ├── Dataverse/              # Future: Dataverse client
│       └── Config/                 # Future: Configuration
├── config/                         # Environment configs
├── tests/                          # Test scripts and data
└── README.md
```

### Implementation Status

- [x] Phase 1: MCP Protocol Foundation
- [ ] Phase 2: Auth & Config
- [ ] Phase 3: Dataverse Client
- [ ] Phase 4: MCP Tool Registration
- [ ] Phase 5: Integration

See [docs/implementation-plan.md](docs/implementation-plan.md) for details.

## Troubleshooting

### Server won't start
- Verify .NET 8 SDK installed: `dotnet --version`
- Check project builds: `dotnet build src/PipeDreamMcp/PipeDreamMcp.csproj`

### Azure CLI authentication fails
- Run `az login` and authenticate
- Verify account: `az account show`
- Check Dataverse access: `az account get-access-token --resource https://org.crm.dynamics.com`

### GitHub Copilot can't find tools
- Verify server path in settings is correct (absolute path)
- Reload VS Code window after settings change
- Check stderr logs for errors: Server logs to `Console.Error`

### MCP protocol errors
- Test manually: `echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | dotnet run --project src/PipeDreamMcp`
- Review logs in stderr output
- Verify JSON-RPC message format

## Contributing

This is a personal project for learning MCP and Dataverse integration. Contributions welcome!

## License

MIT License - See LICENSE file for details.

## References

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
- [Azure.Identity Library](https://learn.microsoft.com/dotnet/api/azure.identity)
- [GitHub Copilot MCP Support](https://docs.github.com/copilot)
