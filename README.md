# PipeDream MCP

Model Context Protocol (MCP) server for Microsoft Dataverse and Azure DevOps.

## Overview

PipeDream MCP enables AI agents (like GitHub Copilot) to interact with Microsoft Dataverse using Azure CLI authentication.

**Key Features:**
- Dataverse operations (query, retrieve, list, metadata)
- Azure CLI authentication with token caching
- Simple configuration (inline or config file)
- OData query capabilities
- Production-ready error handling

## Prerequisites

- .NET 8.0 SDK - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- Azure CLI - [Install](https://learn.microsoft.com/cli/azure/install-azure-cli)
- Azure subscription with Dataverse access
- Run `az login` to authenticate

## Installation

```powershell
git clone https://github.com/ryanmichaeljames/pipe-dream-mcp.git
cd pipe-dream-mcp

# Publish self-contained executable
dotnet publish src/PipeDreamMcp/PipeDreamMcp.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o C:/tools/pipe-dream-mcp

# Create config directory
mkdir C:/tools/pipe-dream-mcp/config

# Verify installation
C:/tools/pipe-dream-mcp/PipeDreamMcp.exe --version
```

## Configuration

### Quick Start: Inline Configuration (Recommended)

Configure directly in VS Code's `mcp.json` - no separate config files needed:

```json
{
  "servers": {
    "pipe-dream": {
      "type": "stdio",
      "command": "C:/tools/pipe-dream-mcp/PipeDreamMcp.exe",
      "args": [
        "--dataverse-url",
        "https://your-org.crm.dynamics.com/"
      ]
    }
  }
}
```

**Optional arguments:**
- `--api-version v9.2` - API version (default: v9.2)
- `--timeout 60` - Request timeout in seconds (default: 30)

### Alternative: File-Based Configuration

Create config files for specific environments:

**config/prod.json:**
```json
{
  "environment": "prod",
  "dataverse": {
    "url": "https://your-org.crm.dynamics.com",
    "apiVersion": "v9.2",
    "timeout": 30
  }
}
```

Use with `--config-file` parameter:
```json
{
  "servers": {
    "pipe-dream-prod": {
      "type": "stdio",
      "command": "C:/tools/pipe-dream-mcp/PipeDreamMcp.exe",
      "args": ["--config-file", "C:/tools/pipe-dream-mcp/config/prod.json"]
    }
  }
}
```

Ensure Azure CLI is authenticated:
```powershell
az login
```

## Usage

### VS Code GitHub Copilot Integration

1. Open VS Code user MCP settings:
   - **Windows**: `%APPDATA%\Code\User\mcp.json`
   - **macOS**: `~/Library/Application Support/Code/User/mcp.json`
   - **Linux**: `~/.config/Code/User/mcp.json`

2. Add server configuration (see Configuration section above)

3. Reload VS Code (`Ctrl+Shift+P` → "Developer: Reload Window")

4. Use in Copilot Chat: `#pipe-dream What Dataverse entities are available?`

### Command Line

**Inline configuration:**
```powershell
C:/tools/pipe-dream-mcp/PipeDreamMcp.exe --dataverse-url https://your-org.crm.dynamics.com/
```

**Config file:**
```powershell
C:/tools/pipe-dream-mcp/PipeDreamMcp.exe --config-file C:/configs/prod.json
```

**Help:**
```powershell
C:/tools/pipe-dream-mcp/PipeDreamMcp.exe --help
```

## Available Tools

### `dataverse_query`
Execute OData queries against Dataverse entities.

**Parameters:**
- `entity` (required) - Entity logical name
- `select` (optional) - Array of field names
- `filter` (optional) - OData filter expression
- `top` (optional) - Maximum records to return

### `dataverse_retrieve`
Retrieve a single record by ID.

**Parameters:**
- `entity` (required) - Entity logical name
- `id` (required) - Record GUID
- `select` (optional) - Array of field names

### `dataverse_metadata`
Get metadata about Dataverse entities.

**Parameters:**
- `entity` (optional) - Specific entity (omit for all)

### `dataverse_list`
List records with pagination.

**Parameters:**
- `entity` (required) - Entity logical name
- `pageSize` (optional) - Records per page (default: 50, max: 250)
- `pagingCookie` (optional) - Paging token from previous response



## Contributing

Contributions welcome! Please see the [wiki](https://github.com/ryanmichaeljames/pipe-dream-mcp/wiki) for detailed development documentation.

## License

Apache 2.0 - See [LICENSE](LICENSE) file for details.

## References

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
- [Azure.Identity Library](https://learn.microsoft.com/dotnet/api/azure.identity)
- [GitHub Copilot MCP Support](https://docs.github.com/copilot)
