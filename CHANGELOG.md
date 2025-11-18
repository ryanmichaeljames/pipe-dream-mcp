# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2025-11-18

### Added
- Initial release of PipeDream.Mcp
- MCP protocol implementation (stdio transport, JSON-RPC 2.0)
- Azure CLI authentication support
- Dataverse Web API integration (v9.2)
- Four core MCP tools:
  - `dataverse_query` - Query entities using OData
  - `dataverse_retrieve` - Get record by ID
  - `dataverse_metadata` - List entities and attributes
  - `dataverse_list` - Browse entity collections with pagination
- Two configuration modes:
  - Inline: `--dataverse-url` parameter
  - File-based: `--config-file` parameter
- Comprehensive error handling and validation
- Retry logic with exponential backoff
- Token caching for authentication
- .NET 10.0 support
- NuGet package distribution as global tool

[Unreleased]: https://github.com/ryanmichaeljames/pipe-dream-mcp/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/ryanmichaeljames/pipe-dream-mcp/releases/tag/v0.1.0
