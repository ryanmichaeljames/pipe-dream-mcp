using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Protocol.Initialize;
using PipeDream.Mcp.Protocol.Messages;
using PipeDream.Mcp.Protocol.Tools;

namespace PipeDream.Mcp.Protocol;

/// <summary>
/// Handles MCP protocol message exchange via stdin/stdout
/// </summary>
internal class McpServer
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ToolRegistry _toolRegistry;
    private readonly ILogger<McpServer> _logger;

    public McpServer(
        ILogger<McpServer> logger,
        ToolRegistry toolRegistry)
    {
        _logger = logger;
        _toolRegistry = toolRegistry;
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

        var result = new ToolsListResult
        {
            Tools = _toolRegistry.GetAllDefinitions().ToList()
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

            // Look up handler in registry
            if (!_toolRegistry.TryGetHandler(callParams.Name, out var handler))
            {
                return CreateErrorResponse(request.Id, -32601, $"Unknown tool: {callParams.Name}");
            }

            // Execute handler
            var result = await handler!.ExecuteAsync(callParams.Arguments, cancellationToken);

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
