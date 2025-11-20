using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Common;
using PipeDream.Mcp.Dataverse;
using PipeDream.Mcp.Dataverse.Interfaces;

namespace PipeDream.Mcp.Protocol;

/// <summary>
/// Handles MCP protocol message exchange via stdin/stdout
/// </summary>
internal class McpServer
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IDataverseQueryService? _queryService;
    private readonly IDataverseMetadataService? _metadataService;
    private readonly IFlowQueryService? _flowQueryService;
    private readonly IFlowStateService? _flowStateService;
    private readonly ILogger<McpServer> _logger;

    public McpServer(
        ILogger<McpServer> logger,
        IDataverseQueryService? queryService = null,
        IDataverseMetadataService? metadataService = null,
        IFlowQueryService? flowQueryService = null,
        IFlowStateService? flowStateService = null)
    {
        _logger = logger;
        _queryService = queryService;
        _metadataService = metadataService;
        _flowQueryService = flowQueryService;
        _flowStateService = flowStateService;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Start the MCP server and process messages
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PipeDream MCP Server starting...");

        using var stdin = Console.OpenStandardInput();
        using var reader = new StreamReader(stdin);
        
        int emptyMethodCount = 0;
        const int MaxEmptyMethodErrors = 5;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                _logger.LogInformation("stdin closed, shutting down");
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var request = JsonSerializer.Deserialize<McpMessage>(line, _jsonOptions);
                if (request == null)
                {
                    _logger.LogWarning("Failed to parse message: {Line}", line);
                    continue;
                }

                // Ignore response messages (they have result or error but we're a server, not a client)
                if (request.Result != null || request.Error != null)
                {
                    // This is a response message, not a request - ignore it
                    // (Likely echo from stdout or client sending back our own responses)
                    continue;
                }

                // Validate method name for requests/notifications
                if (string.IsNullOrWhiteSpace(request.Method))
                {
                    emptyMethodCount++;
                    
                    if (emptyMethodCount == 1)
                    {
                        _logger.LogWarning("Received message with empty method (id: {RequestId}). Ignoring silently", request.Id);
                    }
                    else if (emptyMethodCount == MaxEmptyMethodErrors)
                    {
                        _logger.LogWarning("Suppressing further empty method warnings (received {Count} total)", emptyMethodCount);
                    }
                    
                    // Don't respond to malformed messages - just ignore them
                    continue;
                }

                // Reset counter when valid message received
                emptyMethodCount = 0;

                // Log requests normally, but only log non-standard notifications
                if (request.Id != null)
                {
                    _logger.LogDebug("Received request: {Method} (id: {RequestId})", request.Method, request.Id);
                }
                else if (!IsStandardNotification(request.Method))
                {
                    _logger.LogDebug("Received notification: {Method}", request.Method);
                }

                var response = await HandleMessageAsync(request, cancellationToken);
                if (response != null)
                {
                    await WriteResponseAsync(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
            }
        }

        _logger.LogInformation("PipeDream MCP Server stopped");
    }

    /// <summary>
    /// Handle incoming MCP message and route to appropriate handler
    /// </summary>
    private async Task<McpMessage?> HandleMessageAsync(McpMessage request, CancellationToken cancellationToken)
    {
        return request.Method switch
        {
            "initialize" => await HandleInitializeAsync(request, cancellationToken),
            "initialized" => HandleInitialized(request),
            "tools/list" => await HandleToolsListAsync(request, cancellationToken),
            "tools/call" => await HandleToolsCallAsync(request, cancellationToken),
            
            // Handle known notifications silently
            "notifications/initialized" => HandleNotification(request, "Client initialized"),
            "notifications/cancelled" => HandleNotification(request, "Request cancelled"),
            "notifications/progress" => HandleNotification(request, "Progress update"),
            
            // Unknown method - only log as error if it's a request (has id), notifications can be safely ignored
            _ => request.Id != null 
                ? CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
                : HandleNotification(request, $"Ignoring unknown notification: {request.Method}")
        };
    }

    /// <summary>
    /// Handle initialize request
    /// </summary>
    private Task<McpMessage> HandleInitializeAsync(McpMessage request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling initialize request");

        var result = new InitializeResult
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability()
            },
            ServerInfo = new ServerInfo
            {
                Name = "pipe-dream-mcp",
                Version = "0.1.0"
            }
        };

        return Task.FromResult(new McpMessage
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = result
        });
    }

    /// <summary>
    /// Handle initialized notification (no response needed)
    /// </summary>
    private McpMessage HandleInitialized(McpMessage request)
    {
        return HandleNotification(request, "Client initialized");
    }

    /// <summary>
    /// Handle generic notification (no response needed)
    /// </summary>
    private McpMessage HandleNotification(McpMessage request, string logMessage)
    {
        _logger.LogDebug("{Message}", logMessage);
        // Notifications don't get responses - return null to signal no response needed
        return null!;
    }

    /// <summary>
    /// Check if a method is a standard MCP notification that doesn't need logging
    /// </summary>
    private static bool IsStandardNotification(string? method)
    {
        return method switch
        {
            "initialized" => true,
            "notifications/initialized" => true,
            "notifications/cancelled" => true,
            "notifications/progress" => true,
            _ => false
        };
    }

    /// <summary>
    /// Handle tools/list request
    /// </summary>
    private Task<McpMessage> HandleToolsListAsync(McpMessage request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling tools/list request");

        var tools = new List<ToolDefinition>();
        if (_queryService != null && _metadataService != null && _flowQueryService != null && _flowStateService != null)
        {
            tools.AddRange(DataverseTools.All);
            tools.AddRange(FlowTools.All);
        }

        var result = new ToolsListResult
        {
            Tools = tools
        };

        return Task.FromResult(new McpMessage
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = result
        });
    }

    /// <summary>
    /// Handle tools/call request
    /// </summary>
    private async Task<McpMessage> HandleToolsCallAsync(McpMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
            var callParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, _jsonOptions);

            if (callParams == null || string.IsNullOrWhiteSpace(callParams.Name))
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid tool call parameters");
            }

            _logger.LogDebug("Handling tools/call: {ToolName}", callParams.Name);

            // Route to appropriate handler based on tool name
            var result = callParams.Name switch
            {
                "dataverse_query" => await HandleDataverseQueryAsync(callParams.Arguments, cancellationToken),
                "dataverse_query_nextlink" => await HandleDataverseQueryNextLinkAsync(callParams.Arguments, cancellationToken),
                "dataverse_retrieve" => await HandleDataverseRetrieveAsync(callParams.Arguments, cancellationToken),
                "dataverse_metadata" => await HandleDataverseMetadataAsync(callParams.Arguments, cancellationToken),
                "dataverse_query_flows" => await HandleDataverseQueryFlowsAsync(callParams.Arguments, cancellationToken),
                "dataverse_activate_flow" => await HandleDataverseActivateFlowAsync(callParams.Arguments, cancellationToken),
                "dataverse_deactivate_flow" => await HandleDataverseDeactivateFlowAsync(callParams.Arguments, cancellationToken),
                _ => throw new InvalidOperationException($"Unknown tool: {callParams.Name}")
            };

            return new McpMessage
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Result = new { content = new[] { new { type = "text", text = result } } }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tools/call");
            return CreateErrorResponse(request.Id, -32603, $"Tool execution error: {ex.Message}");
        }
    }

    private async Task<string> HandleDataverseQueryAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_queryService == null)
            throw new InvalidOperationException("Dataverse query service not configured");

        try
        {
            var entity = arguments?.GetProperty("entity").GetString() ?? throw new ArgumentException("entity parameter required");
            InputValidator.ValidateEntityName(entity);
            
            var select = arguments.Value.TryGetProperty("select", out var selectProp) 
                ? selectProp.EnumerateArray().Select(e => e.GetString()!).ToArray() 
                : null;
            InputValidator.ValidateFieldNames(select);
            
            var filter = arguments.Value.TryGetProperty("filter", out var filterProp) ? filterProp.GetString() : null;
            InputValidator.ValidateFilterExpression(filter);
            
            var orderBy = arguments.Value.TryGetProperty("orderby", out var orderByProp) ? orderByProp.GetString() : null;
            
            var top = arguments.Value.TryGetProperty("top", out var topProp) ? topProp.GetInt32() : (int?)null;
            var maxPageSize = arguments.Value.TryGetProperty("maxpagesize", out var maxPageSizeProp) ? maxPageSizeProp.GetInt32() : (int?)null;
            
            // Validate top and maxpagesize are not used together
            if (top.HasValue && maxPageSize.HasValue)
                throw new ArgumentException("Cannot use both 'top' and 'maxpagesize' parameters. Use 'top' to limit total results, or 'maxpagesize' for pagination.");
            
            // Only default 'top' to 50 when maxPageSize is NOT being used
            int? validatedTop = maxPageSize.HasValue ? null : InputValidator.ValidateTopCount(top);
            var count = arguments.Value.TryGetProperty("count", out var countProp) ? countProp.GetBoolean() : true;

            var result = await _queryService.QueryAsync(entity, select, filter, validatedTop, expand: null, orderBy: orderBy, count: count, maxPageSize: maxPageSize, includeFormattedValues: false, cancellationToken: cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in dataverse_query");
            throw new InvalidOperationException($"Invalid query parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in dataverse_query");
            throw new InvalidOperationException($"Failed to query Dataverse: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in dataverse_query");
            throw new InvalidOperationException($"Query operation failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleDataverseQueryNextLinkAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_queryService == null)
            throw new InvalidOperationException("Dataverse query service not configured");

        try
        {
            var nextLink = arguments?.GetProperty("nextlink").GetString() ?? throw new ArgumentException("nextlink parameter required");
            
            if (string.IsNullOrWhiteSpace(nextLink))
                throw new ArgumentException("nextlink parameter cannot be empty");

            var maxPageSize = arguments.Value.TryGetProperty("maxpagesize", out var maxPageSizeProp) ? maxPageSizeProp.GetInt32() : (int?)null;

            var result = await _queryService.QueryNextLinkAsync(nextLink, maxPageSize, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in dataverse_query_nextlink");
            throw new InvalidOperationException($"Invalid nextLink: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in dataverse_query_nextlink");
            throw new InvalidOperationException($"Failed to query nextLink: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in dataverse_query_nextlink");
            throw new InvalidOperationException($"NextLink query failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleDataverseRetrieveAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_queryService == null)
            throw new InvalidOperationException("Dataverse query service not configured");

        try
        {
            var entity = arguments?.GetProperty("entity").GetString() ?? throw new ArgumentException("entity parameter required");
            InputValidator.ValidateEntityName(entity);
            
            var idString = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("id parameter required");
            var id = InputValidator.ValidateGuid(idString, "id");
            
            var select = arguments.Value.TryGetProperty("select", out var selectProp)
                ? selectProp.EnumerateArray().Select(e => e.GetString()!).ToArray()
                : null;
            InputValidator.ValidateFieldNames(select);

            var result = await _queryService.RetrieveAsync(entity, id, select, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in dataverse_retrieve");
            throw new InvalidOperationException($"Invalid retrieve parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in dataverse_retrieve");
            throw new InvalidOperationException($"Failed to retrieve record: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in dataverse_retrieve");
            throw new InvalidOperationException($"Retrieve operation failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleDataverseMetadataAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_metadataService == null)
            throw new InvalidOperationException("Dataverse metadata service not configured");

        try
        {
            var entity = arguments?.TryGetProperty("entity", out var entityProp) == true ? entityProp.GetString() : null;

            var result = await _metadataService.GetMetadataAsync(entity, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in dataverse_metadata");
            throw new InvalidOperationException($"Failed to retrieve metadata: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in dataverse_metadata");
            throw new InvalidOperationException($"Metadata operation failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleDataverseQueryFlowsAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_flowQueryService == null)
            throw new InvalidOperationException("Dataverse flow query service not configured");

        try
        {
            // Extract parameters
            var solutionId = arguments?.TryGetProperty("solutionId", out var solIdProp) == true 
                ? solIdProp.GetString() 
                : null;
            var solutionUniqueName = arguments?.TryGetProperty("solutionUniqueName", out var solNameProp) == true 
                ? solNameProp.GetString() 
                : null;
            var customFilter = arguments?.TryGetProperty("filter", out var filterProp) == true 
                ? filterProp.GetString() 
                : null;
            
            // Additional fields from arguments (if provided)
            var additionalFields = arguments?.TryGetProperty("select", out var selectProp) == true 
                ? selectProp.EnumerateArray().Select(e => e.GetString()!).Where(f => !string.IsNullOrWhiteSpace(f)).ToArray()
                : Array.Empty<string>();
            
            var orderBy = arguments?.TryGetProperty("orderby", out var orderByProp) == true ? orderByProp.GetString() : null;
            var top = arguments?.TryGetProperty("top", out var topProp) == true ? topProp.GetInt32() : (int?)null;
            var maxPageSize = arguments?.TryGetProperty("maxpagesize", out var maxPageSizeProp) == true ? maxPageSizeProp.GetInt32() : (int?)null;
            var count = arguments?.TryGetProperty("count", out var countProp) == true ? countProp.GetBoolean() : true;

            // Delegate to flow query service (validation happens there)
            var result = await _flowQueryService.QueryFlowsAsync(
                solutionId, 
                solutionUniqueName, 
                customFilter, 
                additionalFields,
                orderBy,
                top,
                count,
                maxPageSize,
                cancellationToken);
            
            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in dataverse_query_flows");
            throw new InvalidOperationException($"Invalid query parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in dataverse_query_flows");
            throw new InvalidOperationException($"Failed to query flows: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in dataverse_query_flows");
            throw new InvalidOperationException($"Query flows operation failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleDataverseActivateFlowAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_flowStateService == null)
            throw new InvalidOperationException("Dataverse flow state service not configured");

        try
        {
            var workflowId = arguments?.GetProperty("workflowId").GetString() 
                ?? throw new ArgumentException("workflowId parameter required");

            var validateConnectionReferences = arguments.Value.TryGetProperty("validateConnectionReferences", out var validateProp) 
                && validateProp.GetBoolean();

            // Validate workflowId is a GUID
            if (!Guid.TryParse(workflowId, out var guid))
                throw new ArgumentException("workflowId must be a valid GUID");

            // Delegate to flow state service
            if (validateConnectionReferences)
                _logger.LogDebug("Validating connection references for workflow {WorkflowId}", workflowId);
            
            await _flowStateService.ActivateFlowAsync(guid, validateConnectionReferences, cancellationToken);

            return $"Flow {workflowId} successfully activated (StateCode: 1 - Activated, StatusCode: 2)";
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in dataverse_activate_flow");
            throw new InvalidOperationException($"Invalid parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in dataverse_activate_flow");
            throw new InvalidOperationException($"Failed to activate flow: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in dataverse_activate_flow");
            throw new InvalidOperationException($"Activate flow operation failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleDataverseDeactivateFlowAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_flowStateService == null)
            throw new InvalidOperationException("Dataverse flow state service not configured");

        try
        {
            var workflowId = arguments?.GetProperty("workflowId").GetString() 
                ?? throw new ArgumentException("workflowId parameter required");

            // Validate workflowId is a GUID
            if (!Guid.TryParse(workflowId, out var guid))
                throw new ArgumentException("workflowId must be a valid GUID");

            // Delegate to flow state service
            await _flowStateService.DeactivateFlowAsync(guid, cancellationToken);

            return $"Flow {workflowId} successfully deactivated (StateCode: 0 - Draft, StatusCode: 1)";
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in dataverse_deactivate_flow");
            throw new InvalidOperationException($"Invalid parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error in dataverse_deactivate_flow");
            throw new InvalidOperationException($"Failed to deactivate flow: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error in dataverse_deactivate_flow");
            throw new InvalidOperationException($"Deactivate flow operation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Write response to stdout
    /// </summary>
    private async Task WriteResponseAsync(McpMessage response)
    {
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await Console.Out.WriteLineAsync(json);
        await Console.Out.FlushAsync();

        _logger.LogDebug("Sent response (id: {ResponseId})", response.Id);
    }

    /// <summary>
    /// Get user-friendly error message from HTTP exception
    /// </summary>
    private static string GetUserFriendlyHttpError(HttpRequestException ex)
    {
        var message = ex.Message;

        // Network connectivity issues
        if (message.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
            return "Cannot connect to Dataverse. Verify the URL is correct and the service is accessible.";

        if (message.Contains("no such host", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("name resolution", StringComparison.OrdinalIgnoreCase))
            return "Cannot resolve Dataverse hostname. Check your network connection and the configured URL.";

        if (message.Contains("connection reset", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase))
            return "Network connection was interrupted. The server will retry automatically.";

        if (message.Contains("network is unreachable", StringComparison.OrdinalIgnoreCase))
            return "Network is unreachable. Check your internet connection.";

        // HTTP status codes
        if (message.Contains("400") || message.Contains("Bad Request"))
            return "Invalid request format. Check entity name and filter syntax.";

        if (message.Contains("401") || message.Contains("Unauthorized"))
            return "Authentication failed. Verify Azure CLI is logged in with correct permissions.";

        if (message.Contains("403") || message.Contains("Forbidden"))
            return "Access denied. Ensure your account has permissions to access this Dataverse resource.";

        if (message.Contains("404") || message.Contains("Not Found"))
            return "Resource not found. Verify the entity name and record ID are correct.";

        if (message.Contains("429") || message.Contains("Too Many Requests"))
            return "Rate limit exceeded. The server will automatically retry with appropriate delays.";

        if (message.Contains("500") || message.Contains("Internal Server Error"))
            return "Dataverse server error. This is a temporary issue and will be retried automatically.";

        if (message.Contains("502") || message.Contains("Bad Gateway"))
            return "Gateway error connecting to Dataverse. Will retry automatically.";

        if (message.Contains("503") || message.Contains("Service Unavailable"))
            return "Dataverse service temporarily unavailable. Will retry automatically.";

        if (message.Contains("504") || message.Contains("Gateway Timeout"))
            return "Gateway timeout connecting to Dataverse. Will retry automatically.";

        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "Request timed out. Try again or reduce the query complexity.";

        return $"HTTP error occurred: {message}";
    }

    /// <summary>
    /// Create error response
    /// </summary>
    private McpMessage CreateErrorResponse(object? id, int code, string message)
    {
        _logger.LogError("Error: {Message}", message);

        return new McpMessage
        {
            JsonRpc = "2.0",
            Id = id,
            Error = new McpError
            {
                Code = code,
                Message = message
            }
        };
    }
}
