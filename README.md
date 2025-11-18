# PipeDream MCP

Model Context Protocol (MCP) server for Microsoft Dataverse and Azure DevOps (coming soon).

## Overview

PipeDream MCP enables AI agents (like GitHub Copilot) to interact with Microsoft Dataverse using Azure CLI authentication. This MCP server provides secure access to Dataverse data through a standardized protocol interface.

**Key Features:**
- **Dataverse operations** - Query, retrieve, list, metadata, and more
- **Azure CLI authentication** - Secure token-based auth with automatic caching and refresh
- **Flexible configuration** - Inline arguments or JSON config files
- **OData query support** - Full filtering, selection, and pagination capabilities
- **Production-ready** - Comprehensive error handling, retry logic with exponential backoff, and input validation
- **MCP protocol compliant** - Implements MCP 2024-11-05 specification

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
dotnet run --project src/PipeDream.Mcp -- --dataverse-url https://your-org.crm.dynamics.com/

# Or build and publish
dotnet publish src/PipeDream.Mcp/PipeDream.Mcp.csproj -c Release -o ./publish
./publish/PipeDream.Mcp --dataverse-url https://your-org.crm.dynamics.com/
```

## Configuration

### Quick Start: Inline Configuration (Recommended)

Configure directly in VS Code's MCP settings - no separate config files needed:

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
- `--api-version` or `-a` - API version (default: v9.2)
- `--timeout` or `-t` - Request timeout in seconds (default: 30)

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

**Show help (or run with no arguments):**
```powershell
pipedream-mcp
pipedream-mcp --help
```

**Show version:**
```powershell
pipedream-mcp --version
```

**Inline configuration:**
```powershell
pipedream-mcp --dataverse-url https://your-org.crm.dynamics.com/
```

**Config file:**
```powershell
pipedream-mcp --config-file C:/configs/your-org.json
```

**Optional parameters:**
- `--api-version` or `-a` - API version (default: v9.2)
- `--timeout` or `-t` - Request timeout in seconds (default: 30)

## Available Tools

### `dataverse_query`
Execute OData queries against Dataverse entities with flexible filtering and selection.

**Parameters:**
- `entity` (required) - Entity logical name (e.g., "account", "contact")
- `select` (optional) - Array of field names to return
- `filter` (optional) - OData filter expression (e.g., "statecode eq 0")
- `top` (optional) - Maximum records to return (1-5000, default: 50)

**Example:** Query active accounts with specific fields

### `dataverse_retrieve`
Retrieve a single record by its unique identifier.

**Parameters:**
- `entity` (required) - Entity logical name
- `id` (required) - Record GUID in standard format
- `select` (optional) - Array of field names to return

**Example:** Get specific account by ID

### `dataverse_metadata`
Get comprehensive metadata about Dataverse entities and their attributes.

**Parameters:**
- `entity` (optional) - Specific entity logical name (omit to list all entities)

**Example:** Get schema information for troubleshooting or discovery

### `dataverse_list`
List records with server-side pagination for efficient data browsing.

**Parameters:**
- `entity` (required) - Entity logical name
- `pageSize` (optional) - Records per page (1-250, default: 50)
- `pagingCookie` (optional) - Paging token from previous response for next page

**Example:** Browse records in manageable pages

## Contributing

Contributions welcome! Please see the [wiki](https://github.com/ryanmichaeljames/pipe-dream-mcp/wiki) for detailed development documentation.

## License

Apache 2.0 - See [LICENSE](LICENSE) file for details.

## References

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
- [Azure.Identity Library](https://learn.microsoft.com/dotnet/api/azure.identity)
- [GitHub Copilot MCP Support](https://docs.github.com/copilot)
