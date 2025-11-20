# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2025-11-21

### Added
- File-based logging with Microsoft.Extensions.Logging:
  - Per-environment log files: `logs/pipedream-mcp-{subcommand}-{orgname}-{yyyyMMdd}.log`
  - Automatic 30-day cleanup of old log files
  - `--verbose` flag for Debug logging (default: Information)
  - Log levels: Information (lifecycle, auth, connectivity) and Debug (all requests/responses)
  - Stderr reserved for fatal errors only (MCP protocol compliance)
  - Structured logging throughout all components
  - Dependency injection for `ILogger<T>` across all services
- Centralized DI configuration via `ServiceCollectionExtensions`
- Logging infrastructure in `Startup/LoggingConfiguration.cs`
- OData query enhancements:
  - `orderby` parameter for predictable result ordering (e.g., "createdon desc", "name asc")
  - `count` parameter (default: true) returns total matching records via `@odata.count`
  - `maxpagesize` parameter enables server-driven pagination with `@odata.nextLink` (uses `Prefer: odata.maxpagesize` header)
  - Dataverse automatically provides `@odata.nextLink` when `maxpagesize` is set and more pages exist
  - Validation to prevent using `top` and `maxpagesize` together (returns clear error message)
- Pagination support:
  - `dataverse_query_nextlink` tool for fetching subsequent pages using `@odata.nextLink` URLs
  - Optional `maxpagesize` parameter maintains consistent page size across all pages (adds `Prefer: odata.maxpagesize` header)
  - Validates nextLink URL is for the configured Dataverse instance
  - Enables complete pagination workflow through MCP interface
- Power Automate flow management tools:
  - `dataverse_query_flows` - Query cloud flows with optimized filtering and pagination support
  - `dataverse_activate_flow` - Activate flows with optional connection validation
  - `dataverse_deactivate_flow` - Deactivate flows (set to Draft state)
- Flow query pagination:
  - `orderby` parameter for sorting flow results
  - `count` parameter (default: true) returns total flow count
  - `maxpagesize` parameter enables pagination with @odata.nextLink (e.g., 5, 10, 50 flows per page)
  - Works with existing `dataverse_query_nextlink` tool for fetching subsequent pages
  - Validation prevents using `top` and `maxpagesize` together
- Enhanced tool descriptions with AI agent guidance:
  - Plural entity naming conventions clearly documented
  - Timezone detection recommendations for date/time filtering
  - Cross-references between related tools
  - Concise examples and usage patterns
- Service-oriented architecture with dependency injection
- `DataverseConstants` class with nested static constants to eliminate magic strings

### Fixed
- `dataverse_query` no longer defaults `top=50` when using `maxpagesize` parameter (was incorrectly limiting pagination)
- Consistent page sizes now maintained when `maxpagesize` passed to `dataverse_query_nextlink`

### Changed
- **BREAKING CHANGE**: CLI structure refactored to subcommands:
  - Now requires subcommand: `pipedream-mcp dataverse --dataverse-url <url>` (previously `pipedream-mcp --dataverse-url <url>`)
  - `pipedream-mcp dataverse` - Run Dataverse MCP server
  - `pipedream-mcp azure-devops` - Run Azure DevOps MCP server (coming soon)
  - Enables future multi-provider support (Dataverse, Azure DevOps, GitHub, etc.)
  - Each subcommand has its own help: `pipedream-mcp dataverse --help`
  - Global flags: `--version`, `--help`
  - No backward compatibility with v0.1.0 command format
- Improved `dataverse_query` tool description with plural naming emphasis
- Updated `dataverse_retrieve` and `dataverse_metadata` descriptions for clarity
- Optimized `dataverse_query_flows` for solution filtering (uses direct filter when GUID provided)
- Refactored to service-oriented architecture:
  - Introduced `Microsoft.Extensions.DependencyInjection` for IoC container
  - Created specialized internal services: `DataverseQueryService`, `DataverseMetadataService`, `FlowQueryService`, `FlowStateService`
  - All services registered as Singletons for efficient resource usage
  - `DataverseClient` reduced from 460 to ~220 lines, now internal HTTP client
  - `McpServer` made internal, injects service interfaces instead of monolithic client
  - Flow validation and solution filtering logic moved to specialized services
- Restructured constants into entity-specific namespace hierarchy:
  - Replaced nested `DataverseConstants` class with flat constant classes organized by entity
  - Created namespace hierarchy: `PipeDream.Mcp.Dataverse.Constants.Workflow`, `Constants.Solution`, etc.
  - Constant files organized in entity-specific folders: `Constants\Workflow\`, `Constants\Solution\`, etc.
  - Flat class names (`Fields`, `State`, `Status`, `Category`) with entity namespace providing context
  - Example: `DataverseConstants.Fields.Workflow.Name` → `Fields.Name` (with `using PipeDream.Mcp.Dataverse.Constants.Workflow;`)
  - Improves code clarity by showing entity relationships through namespace organization
- Refactored codebase for maintainability and scalability:
  - Split `McpMessage.cs` (145 lines) into 7 focused files organized by responsibility:
    - `Protocol/Messages/` - McpMessage.cs (27 lines), McpError.cs (18 lines)
    - `Protocol/Initialize/` - InitializeParams.cs (32 lines), InitializeResult.cs (47 lines)
    - `Protocol/Tools/` - ToolDefinition.cs (17 lines), ToolsListResult.cs (13 lines), ToolCallParams.cs (16 lines)
  - Simplified `Program.cs` from 350+ lines to 59 lines using command handler pattern:
    - Created `Commands/ICommandHandler` interface for extensibility
    - Extracted `Commands/DataverseCommandHandler` (186 lines) with complete dataverse logic
    - Extracted `Commands/HelpProvider` (90 lines) centralizing all help text
    - Extracted `Commands/CommandLineParser` (82 lines) for reusable argument parsing
    - Implemented dictionary-based command routing for easy addition of new subcommands
  - Updated 13 dependent files with new Protocol namespace structure
  - All files now under 200 lines, each with single responsibility
  - Architecture prepared for Azure DevOps and other provider additions without modifying core routing

### Removed
- `dataverse_list` tool (redundant with `dataverse_query` top parameter)

## [0.1.0] - 2025-11-18

### Added
- Initial release of PipeDream.Mcp
- MCP protocol implementation (stdio transport, JSON-RPC 2.0)
- Azure CLI authentication support
- Dataverse Web API integration (v9.2)
- Core MCP tools:
  - `dataverse_query` - Query entities using OData
  - `dataverse_retrieve` - Get record by ID
  - `dataverse_metadata` - List entities and attributes
- Two configuration modes:
  - Inline: `--dataverse-url` parameter
  - File-based: `--config-file` parameter
- Comprehensive error handling and validation
- Retry logic with exponential backoff
- Token caching for authentication
- .NET 10.0 support
- NuGet package distribution as global tool

[Unreleased]: https://github.com/ryanmichaeljames/pipe-dream-mcp/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/ryanmichaeljames/pipe-dream-mcp/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/ryanmichaeljames/pipe-dream-mcp/releases/tag/v0.1.0
