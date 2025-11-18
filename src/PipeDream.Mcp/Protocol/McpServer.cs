using System.Text.Json;
using PipeDream.Mcp.Common;
using PipeDream.Mcp.Dataverse;

namespace PipeDream.Mcp.Protocol;

/// <summary>
/// Handles MCP protocol message exchange via stdin/stdout
/// </summary>
public class McpServer
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly DataverseClient? _dataverseClient;

    public McpServer(DataverseClient? dataverseClient = null)
    {
        _dataverseClient = dataverseClient;
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
        LogToStderr("PipeDream MCP Server starting...");

        using var stdin = Console.OpenStandardInput();
        using var reader = new StreamReader(stdin);
        
        int emptyMethodCount = 0;
        const int MaxEmptyMethodErrors = 5;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                LogToStderr("stdin closed, shutting down");
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
                    LogToStderr($"Failed to parse message: {line}");
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
                        LogToStderr($"Warning: Received message with empty method (id: {request.Id}). Ignoring silently.");
                    }
                    else if (emptyMethodCount == MaxEmptyMethodErrors)
                    {
                        LogToStderr($"Suppressing further empty method warnings (received {emptyMethodCount} total)");
                    }
                    
                    // Don't respond to malformed messages - just ignore them
                    continue;
                }

                // Reset counter when valid message received
                emptyMethodCount = 0;

                // Log requests normally, but only log non-standard notifications
                if (request.Id != null)
                {
                    LogToStderr($"Received request: {request.Method} (id: {request.Id})");
                }
                else if (!IsStandardNotification(request.Method))
                {
                    LogToStderr($"Received notification: {request.Method}");
                }

                var response = await HandleMessageAsync(request, cancellationToken);
                if (response != null)
                {
                    await WriteResponseAsync(response);
                }
            }
            catch (Exception ex)
            {
                LogToStderr($"Error processing message: {ex.Message}");
            }
        }

        LogToStderr("PipeDream MCP Server stopped");
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
        LogToStderr("Handling initialize request");

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
        LogToStderr(logMessage);
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
        LogToStderr("Handling tools/list request");

        var tools = new List<ToolDefinition>();
        if (_dataverseClient != null)
        {
            tools.AddRange(DataverseTools.All);
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

            LogToStderr($"Handling tools/call: {callParams.Name}");

            // Route to appropriate handler based on tool name
            var result = callParams.Name switch
            {
                "dataverse_query" => await HandleDataverseQueryAsync(callParams.Arguments, cancellationToken),
                "dataverse_retrieve" => await HandleDataverseRetrieveAsync(callParams.Arguments, cancellationToken),
                "dataverse_metadata" => await HandleDataverseMetadataAsync(callParams.Arguments, cancellationToken),
                "dataverse_list" => await HandleDataverseListAsync(callParams.Arguments, cancellationToken),
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
            LogToStderr($"Error in tools/call: {ex.Message}");
            return CreateErrorResponse(request.Id, -32603, $"Tool execution error: {ex.Message}");
        }
    }

    private async Task<string> HandleDataverseQueryAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_dataverseClient == null)
            throw new InvalidOperationException("Dataverse client not configured");

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
            
            var top = arguments.Value.TryGetProperty("top", out var topProp) ? topProp.GetInt32() : (int?)null;
            var validatedTop = InputValidator.ValidateTopCount(top);

            var result = await _dataverseClient.QueryAsync(entity, select, filter, validatedTop, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            LogToStderr($"Validation error in dataverse_query: {ex.Message}");
            throw new InvalidOperationException($"Invalid query parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            LogToStderr($"HTTP error in dataverse_query: {ex.Message}");
            throw new InvalidOperationException($"Failed to query Dataverse: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            LogToStderr($"Unexpected error in dataverse_query: {ex.Message}");
            throw new InvalidOperationException($"Query operation failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleDataverseRetrieveAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_dataverseClient == null)
            throw new InvalidOperationException("Dataverse client not configured");

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

            var result = await _dataverseClient.RetrieveAsync(entity, id, select, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            LogToStderr($"Validation error in dataverse_retrieve: {ex.Message}");
            throw new InvalidOperationException($"Invalid retrieve parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            LogToStderr($"HTTP error in dataverse_retrieve: {ex.Message}");
            throw new InvalidOperationException($"Failed to retrieve record: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            LogToStderr($"Unexpected error in dataverse_retrieve: {ex.Message}");
            throw new InvalidOperationException($"Retrieve operation failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleDataverseMetadataAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_dataverseClient == null)
            throw new InvalidOperationException("Dataverse client not configured");

        try
        {
            var entity = arguments?.TryGetProperty("entity", out var entityProp) == true ? entityProp.GetString() : null;

            var result = await _dataverseClient.GetMetadataAsync(entity, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (HttpRequestException ex)
        {
            LogToStderr($"HTTP error in dataverse_metadata: {ex.Message}");
            throw new InvalidOperationException($"Failed to retrieve metadata: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            LogToStderr($"Unexpected error in dataverse_metadata: {ex.Message}");
            throw new InvalidOperationException($"Metadata operation failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleDataverseListAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_dataverseClient == null)
            throw new InvalidOperationException("Dataverse client not configured");
        if (arguments == null)
            throw new ArgumentException("Arguments parameter required", nameof(arguments));
        try
        {
            var entity = arguments.GetProperty("entity").GetString() ?? throw new ArgumentException("entity parameter required");
            InputValidator.ValidateEntityName(entity);
            
            var pageSize = arguments.TryGetProperty("pageSize", out var pageSizeProp) ? pageSizeProp.GetInt32() : 50;
            var validatedPageSize = InputValidator.ValidatePageSize(pageSize);
            
            var pagingCookie = arguments.TryGetProperty("pagingCookie", out var cookieProp) ? cookieProp.GetString() : null;

            var result = await _dataverseClient.ListAsync(entity, validatedPageSize, pagingCookie, cancellationToken);
            return result.RootElement.GetRawText();
        }
        catch (ArgumentException ex)
        {
            LogToStderr($"Validation error in dataverse_list: {ex.Message}");
            throw new InvalidOperationException($"Invalid list parameters: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            LogToStderr($"HTTP error in dataverse_list: {ex.Message}");
            throw new InvalidOperationException($"Failed to list records: {GetUserFriendlyHttpError(ex)}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            LogToStderr($"Unexpected error in dataverse_list: {ex.Message}");
            throw new InvalidOperationException($"List operation failed: {ex.Message}", ex);
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

        LogToStderr($"Sent response (id: {response.Id})");
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
        LogToStderr($"Error: {message}");

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

    /// <summary>
    /// Log to stderr for debugging
    /// </summary>
    private void LogToStderr(string message)
    {
        Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}");
    }
}
