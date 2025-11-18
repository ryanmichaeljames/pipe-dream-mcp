using System.Text.Json;
using PipeDreamMcp.Dataverse;

namespace PipeDreamMcp.Protocol;

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

                LogToStderr($"Received: {request.Method} (id: {request.Id})");

                var response = await HandleMessageAsync(request, cancellationToken);
                await WriteResponseAsync(response);
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
    private async Task<McpMessage> HandleMessageAsync(McpMessage request, CancellationToken cancellationToken)
    {
        return request.Method switch
        {
            "initialize" => await HandleInitializeAsync(request, cancellationToken),
            "initialized" => HandleInitialized(request),
            "tools/list" => await HandleToolsListAsync(request, cancellationToken),
            "tools/call" => await HandleToolsCallAsync(request, cancellationToken),
            _ => CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
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
        LogToStderr("Client initialized");
        // Notification - no response needed, but return empty message to simplify flow
        return new McpMessage { JsonRpc = "2.0" };
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

        var entity = arguments?.GetProperty("entity").GetString() ?? throw new ArgumentException("entity parameter required");
        var select = arguments.Value.TryGetProperty("select", out var selectProp) 
            ? selectProp.EnumerateArray().Select(e => e.GetString()!).ToArray() 
            : null;
        var filter = arguments.Value.TryGetProperty("filter", out var filterProp) ? filterProp.GetString() : null;
        var top = arguments.Value.TryGetProperty("top", out var topProp) ? topProp.GetInt32() : (int?)null;

        var result = await _dataverseClient.QueryAsync(entity, select, filter, top, cancellationToken);
        return result.RootElement.GetRawText();
    }

    private async Task<string> HandleDataverseRetrieveAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_dataverseClient == null)
            throw new InvalidOperationException("Dataverse client not configured");

        var entity = arguments?.GetProperty("entity").GetString() ?? throw new ArgumentException("entity parameter required");
        var idString = arguments.Value.GetProperty("id").GetString() ?? throw new ArgumentException("id parameter required");
        var id = Guid.Parse(idString);
        var select = arguments.Value.TryGetProperty("select", out var selectProp)
            ? selectProp.EnumerateArray().Select(e => e.GetString()!).ToArray()
            : null;

        var result = await _dataverseClient.RetrieveAsync(entity, id, select, cancellationToken);
        return result.RootElement.GetRawText();
    }

    private async Task<string> HandleDataverseMetadataAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_dataverseClient == null)
            throw new InvalidOperationException("Dataverse client not configured");

        var entity = arguments?.TryGetProperty("entity", out var entityProp) == true ? entityProp.GetString() : null;

        var result = await _dataverseClient.GetMetadataAsync(entity, cancellationToken);
        return result.RootElement.GetRawText();
    }

    private async Task<string> HandleDataverseListAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (_dataverseClient == null)
            throw new InvalidOperationException("Dataverse client not configured");

        var entity = arguments?.GetProperty("entity").GetString() ?? throw new ArgumentException("entity parameter required");
        var pageSize = arguments.Value.TryGetProperty("pageSize", out var pageSizeProp) ? pageSizeProp.GetInt32() : 50;
        var pagingCookie = arguments.Value.TryGetProperty("pagingCookie", out var cookieProp) ? cookieProp.GetString() : null;

        var result = await _dataverseClient.ListAsync(entity, pageSize, pagingCookie, cancellationToken);
        return result.RootElement.GetRawText();
    }

    /// <summary>
    /// Write response to stdout
    /// </summary>
    private async Task WriteResponseAsync(McpMessage response)
    {
        // Don't write anything for notifications (no id)
        if (response.Id == null && response.Method == null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await Console.Out.WriteLineAsync(json);
        await Console.Out.FlushAsync();

        LogToStderr($"Sent response (id: {response.Id})");
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
