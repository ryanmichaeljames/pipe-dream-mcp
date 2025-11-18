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

- .NET 10.0 SDK - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- Azure CLI - [Install](https://learn.microsoft.com/cli/azure/install-azure-cli)
- Azure subscription with Dataverse access
- Run `az login` to authenticate

## Installation

### Option 1: .NET Global Tool (Recommended)

```powershell
# Install globally
dotnet tool install --global PipeDream.Mcp

# Verify installation
pipedream-mcp --version

# Update to latest version
dotnet tool update --global PipeDream.Mcp
```

### Option 2: Build from Source

```powershell
git clone https://github.com/ryanmichaeljames/pipe-dream-mcp.git
cd pipe-dream-mcp

# Run directly
dotnet run --project src/PipeDreamMcp -- --dataverse-url https://your-org.crm.dynamics.com/

# Or build and publish
dotnet publish src/PipeDreamMcp/PipeDreamMcp.csproj -c Release -o ./publish
```

## Configuration

### Quick Start: Inline Configuration (Recommended)

Configure directly in VS Code's `mcp.json` - no separate config files needed:

```json
{
  "servers": {
    "pipedream": {
      "type": "stdio",
      "command": "pipedream-mcp",
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
    "pipedream-prod": {
      "type": "stdio",
      "command": "pipedream-mcp",
      "args": ["--config-file", "C:/configs/prod.json"]
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

4. Use in Copilot Chat: `#pipedream What Dataverse entities are available?`

### Command Line

**Inline configuration:**
```powershell
pipedream-mcp --dataverse-url https://your-org.crm.dynamics.com/
```

**Config file:**
```powershell
pipedream-mcp --config-file C:/configs/prod.json
```

**Help:**
```powershell
pipedream-mcp --help
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
