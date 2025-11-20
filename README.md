![PipeDream Logo](https://raw.githubusercontent.com/ryanmichaeljames/pipe-dream-mcp/main/assets/logo_banner.png)

![GitHub Release](https://img.shields.io/github/v/release/ryanmichaeljames/pipe-dream-mcp?style=flat&logo=github&color=33CE57)
![NuGet Version](https://img.shields.io/nuget/v/PipeDream.Mcp?logo=nuget&color=33CE57)
[![Build](https://github.com/ryanmichaeljames/pipe-dream-mcp/actions/workflows/build.yml/badge.svg)](https://github.com/ryanmichaeljames/pipe-dream-mcp/actions/workflows/build.yml)
[![CodeQL](https://github.com/ryanmichaeljames/pipe-dream-mcp/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/ryanmichaeljames/pipe-dream-mcp/actions/workflows/github-code-scanning/codeql)

> Model Context Protocol (MCP) server for Microsoft Dataverse and Azure DevOps (coming soon).

## Overview

PipeDream MCP enables AI agents (like GitHub Copilot) to interact with Microsoft Dataverse using Azure CLI authentication. This MCP server provides secure access to Dataverse data through a standardized protocol interface.

**Key Features:**
- **Dataverse operations** - Query, retrieve, metadata with full OData support
- **Power Automate flow management** - Query, activate, and deactivate cloud flows
- **Azure CLI authentication** - Secure token-based auth with automatic caching and refresh
- **Flexible configuration** - Inline arguments or JSON config files
- **Safety controls** - Opt-in flags for write and delete operations (default: disabled)
- **File-based logging** - Per-environment log files with automatic 30-day cleanup
- **AI-friendly** - Enhanced tool descriptions and guidance for better agent behavior
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
dotnet run --project src/PipeDream.Mcp -- dataverse --dataverse-url https://your-org.crm.dynamics.com/

# Or build and publish
dotnet publish src/PipeDream.Mcp/PipeDream.Mcp.csproj -c Release -o ./publish
./publish/PipeDream.Mcp dataverse --dataverse-url https://your-org.crm.dynamics.com/
```

## Quick Start

1. **Authenticate with Azure CLI:**
   ```powershell
   az login
   ```

2. **Configure VS Code MCP settings** (`%APPDATA%\Code\User\mcp.json` on Windows):
   ```json
   {
     "servers": {
       "pipedream": {
         "type": "stdio",
         "command": "pipedream-mcp",
         "args": [
           "dataverse",
           "--dataverse-url",
           "https://your-org.crm.dynamics.com/"
         ]
       }
     }
   }
   ```

3. **Reload VS Code** (`Ctrl+Shift+P` → "Developer: Reload Window")

4. **Use in Copilot Chat:** `#pipedream What Dataverse entities are available?`

## Command Reference

### Synopsis

```
pipedream-mcp <subcommand> [options]
pipedream-mcp --help
pipedream-mcp --version
```

### Subcommands

- `dataverse` - Run Dataverse MCP server
- `azure-devops` - Run Azure DevOps MCP server (coming soon)

### Dataverse Subcommand

```
pipedream-mcp dataverse --dataverse-url <url> [options]
pipedream-mcp dataverse --config-file <path> [options]
pipedream-mcp dataverse --help
```

### Parameters

| Parameter | Alias | Required | Description | Default |
|-----------|-------|----------|-------------|---------|
| `--dataverse-url` | `-u` | Yes* | Dataverse instance URL | - |
| `--config-file` | `-c` | Yes* | Path to JSON config file | - |
| `--api-version` | `-a` | No | Dataverse Web API version | `v9.2` |
| `--timeout` | `-t` | No | Request timeout (seconds) | `30` |
| `--enable-write-operations` | - | No | Enable Create/Update operations | `false` |
| `--enable-delete-operations` | - | No | Enable Delete operations | `false` |
| `--verbose` | - | No | Enable debug logging (logs all requests/responses) | `false` |
| `--help` | `-h` | No | Show help message | - |
| `--version` | `-v` | No | Show version information | - |

\* Either `--dataverse-url` or `--config-file` is required.

### Examples

**Basic usage with inline configuration:**
```powershell
pipedream-mcp --dataverse-url https://org.crm.dynamics.com/
```

**Enable write operations:**
```powershell
pipedream-mcp --dataverse-url https://org.crm.dynamics.com/ --enable-write-operations
```

**Enable both write and delete operations:**
```powershell
pipedream-mcp --dataverse-url https://org.crm.dynamics.com/ --enable-write-operations --enable-delete-operations
```

**Using a config file:**
```powershell
pipedream-mcp --config-file C:/configs/prod.json
```

**Override config file settings:**
```powershell
pipedream-mcp --config-file C:/configs/prod.json --enable-write-operations
```

**Enable verbose logging for troubleshooting:**
```powershell
pipedream-mcp --dataverse-url https://org.crm.dynamics.com/ --verbose
```

### Configuration File Format

Create JSON config files for reusable environment settings:

```json
{
  "environment": "prod",
  "dataverse": {
    "url": "https://your-org.crm.dynamics.com",
    "apiVersion": "v9.2",
    "timeout": 30,
    "enableWriteOperations": false,
    "enableDeleteOperations": false
  }
}
```

**Note:** Command-line parameters always override config file values.

### Logging

PipeDream MCP uses file-based logging following MCP protocol best practices (stderr reserved for fatal errors only).

**Log Files:**
- Location: `{AppDirectory}/logs/pipedream-mcp-{subcommand}-{orgname}-{yyyyMMdd}.log`
- Example: `logs/pipedream-mcp-dataverse-teaureka-coredev-20251121.log`
- One log file per environment per day
- Automatic cleanup after 30 days

**Log Levels:**
- **Default (Information):** Server lifecycle, auth status, connectivity tests
- **Verbose (Debug):** All requests/responses, detailed message content

**Usage:**
```powershell
# Information logging (default)
pipedream-mcp dataverse --dataverse-url https://org.crm.dynamics.com/

# Debug logging (verbose)
pipedream-mcp dataverse --dataverse-url https://org.crm.dynamics.com/ --verbose
```

**Log Path Display:**
On startup, the log file path is displayed on stderr:
```
Logs: C:/path/to/logs/pipedream-mcp-dataverse-orgname-{yyyyMMdd}.log
```

### VS Code MCP Configuration

Add to your VS Code MCP settings file:

**Settings file location:**
- **Windows**: `%APPDATA%\Code\User\mcp.json`
- **macOS**: `~/Library/Application Support/Code/User/mcp.json`
- **Linux**: `~/.config/Code/User/mcp.json`

**Inline configuration (recommended):**
```json
{
  "servers": {
    "pipedream": {
      "type": "stdio",
      "command": "pipedream-mcp",
      "args": [
        "--dataverse-url", "https://your-org.crm.dynamics.com/",
        "--enable-write-operations"
      ]
    }
  }
}
```

**Config file approach:**
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

## Available Tools

### `dataverse_query`
Query Dataverse entities using OData. Use entity plural names (e.g., 'accounts', 'contacts', 'workflows').

**Parameters:**
- `entity` (required) - Entity plural name: 'accounts', 'contacts', 'workflows', 'solutions', etc.
- `select` (optional) - Field names to return. Example: ['name', 'emailaddress1', 'createdon']
- `filter` (optional) - OData filter. Examples: "statecode eq 0", "name eq 'Contoso'", "contains(name, 'test')"
- `orderby` (optional) - Order results by field(s). Examples: "createdon desc", "name asc", "modifiedon desc,name asc"
- `top` (optional) - Max records (default: 50, max: 5000). Use 5-10 for specific searches.
- `count` (optional) - Include total count of matching records (default: true). Returns @odata.count in response.
- `maxpagesize` (optional) - Maximum records per page. When set, Dataverse returns @odata.nextLink for pagination. **Do not use with `top`** - they conflict.

**Pagination:**
- Use `maxpagesize` (e.g., 10, 50, 100) to enable server-driven pagination
- **Important:** Cannot use `top` and `maxpagesize` together - they conflict
- Dataverse returns `@odata.nextLink` when more pages exist
- Follow the `@odata.nextLink` URL to get subsequent pages (requires HTTP GET with auth headers)
- Each page includes the same `@odata.count` (total records) and a new `@odata.nextLink`

**Parameter Usage:**
- Use **`top`** when you want to limit total results (e.g., "get me 10 contacts")
- Use **`maxpagesize`** when you need pagination (e.g., "page through all contacts, 50 at a time")
- No default page size - if neither parameter is set, `top` defaults to 50 records

**Example 1:** Query recently created contacts (limit to 10 records)
```json
{
  "entity": "contacts",
  "select": ["fullname", "emailaddress1", "createdon"],
  "orderby": "createdon desc",
  "top": 10
}
```

**Example 2:** Enable pagination with small page size
```json
{
  "entity": "contacts",
  "select": ["contactid", "fullname"],
  "orderby": "fullname asc",
  "maxpagesize": 10
}
```

Response with pagination:
```json
{
  "@odata.context": "...",
  "@odata.count": 247,
  "@odata.nextLink": "https://org.crm.dynamics.com/api/data/v9.2/contacts?$skiptoken=...",
  "value": [
    {"contactid": "...", "fullname": "..."}
  ]
}
```

### `dataverse_query_nextlink`

Fetch the next page of results using the `@odata.nextLink` URL from a previous query.

**Parameters:**
- `nextlink` (required) - The full `@odata.nextLink` URL from the previous response
- `maxpagesize` (optional) - Records per page, should match original query for consistent page sizes (e.g., 10, 50, 100)

**Example:** Get page 2 with consistent page size
```json
{
  "nextlink": "https://org.crm.dynamics.com/api/data/v9.2/contacts?$select=contactid,fullname&$skiptoken=...",
  "maxpagesize": 10
}
```

**Pagination flow:**
1. Query with `maxpagesize`: `dataverse_query` → Get page 1 + `@odata.nextLink`
2. Use nextLink **with same maxpagesize**: `dataverse_query_nextlink` → Get page 2 + new `@odata.nextLink`
3. Repeat step 2 until no `@odata.nextLink` in response (last page)

**Important:** Pass the same `maxpagesize` value to all pages for consistent results per page.

### `dataverse_query_flows`
Query Power Automate cloud flows with optimized filtering and human-readable output.

**Parameters:**
- `solutionUniqueName` (optional) - Filter flows by solution unique name
- `solutionId` (optional) - Filter flows by solution GUID
- `filter` (optional) - OData filter for advanced queries (e.g., "name eq 'FlowName'", "contains(name, 'Test')")
- `select` (optional) - Additional fields beyond core set (clientdata, primaryentity, etc.)
- `top` (optional) - Limit total records (default: 50, max: 5000). **Do not use with `maxpagesize`**
- `orderby` (optional) - Order results by field(s) (e.g., "createdon desc", "name asc")
- `count` (optional) - Include total count (default: true). Returns @odata.count in response
- `maxpagesize` (optional) - Records per page for pagination (e.g., 10, 50, 100). Enables @odata.nextLink. **Do not use with `top`**

**Example:** Find draft flows in a solution
```json
{
  "solutionUniqueName": "MySolution",
  "filter": "statecode eq 0",
  "top": 10
}
```

**Example:** Paginate through all flows, 5 at a time
```json
{
  "maxpagesize": 5,
  "orderby": "createdon desc"
}
```

### `dataverse_retrieve`
Retrieve a single Dataverse record by ID. Use entity plural names.

**Parameters:**
- `entity` (required) - Entity plural name: 'accounts', 'contacts', 'workflows', etc.
- `id` (required) - Record GUID
- `select` (optional) - Field names to return (optional). Example: ['name', 'createdon']

**Example:** Get specific account by ID

### `dataverse_metadata`
Get Dataverse entity metadata including available entities and their attributes.

**Parameters:**
- `entity` (optional) - Entity plural name for specific metadata. If omitted, returns all entities.

**Example:** Get schema information for workflows entity

### `dataverse_activate_flow`
Activate a Power Automate flow. Requires `--enable-write-operations` flag.

**Parameters:**
- `workflowId` (required) - Workflow GUID to activate
- `validateConnectionReferences` (optional) - Check connection references before activating (default: false)

**Example:** Activate a flow with connection validation

### `dataverse_deactivate_flow`
Deactivate a Power Automate flow (set to Draft state). Requires `--enable-write-operations` flag.

**Parameters:**
- `workflowId` (required) - Workflow GUID to deactivate

**Example:** Deactivate a flow for maintenance

## Contributing

Contributions welcome! Please see the [wiki](https://github.com/ryanmichaeljames/pipe-dream-mcp/wiki) for detailed development documentation.

## License

Apache 2.0 - See [LICENSE](LICENSE) file for details.

## References

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
- [Azure.Identity Library](https://learn.microsoft.com/dotnet/api/azure.identity)
- [GitHub Copilot MCP Support](https://docs.github.com/copilot)
