using System.Text.Json;

namespace PipeDreamMcp.Protocol;

/// <summary>
/// Handles MCP protocol message exchange via stdin/stdout
/// </summary>
public class McpServer
{
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer()
    {
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

        var result = new ToolsListResult
        {
            Tools = new List<ToolDefinition>()
        };

        return Task.FromResult(new McpMessage
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = result
        });
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
