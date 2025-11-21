using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Auth;
using PipeDream.Mcp.Common;
using PipeDream.Mcp.Config;

namespace PipeDream.Mcp.PowerPlatform;

/// <summary>
/// HTTP client for Power Platform Management API
/// </summary>
public class PowerPlatformClient
{
    private readonly HttpClient _httpClient;
    private readonly AzureAuthProvider _authProvider;
    private readonly PowerPlatformConfig _config;
    private readonly ILogger<PowerPlatformClient> _logger;
    private const string BaseUrl = "https://api.powerplatform.com";

    public PowerPlatformClient(
        AzureAuthProvider authProvider,
        PowerPlatformConfig config,
        ILogger<PowerPlatformClient> logger)
    {
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure HttpClient with optimized settings
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 20
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(_config.Timeout)
        };

        _logger.LogInformation("PowerPlatformClient initialized (timeout: {Timeout}s)", _config.Timeout);
    }

    /// <summary>
    /// List all environments
    /// </summary>
    public async Task<JsonDocument> ListEnvironmentsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"/environmentmanagement/environments?api-version={_config.ApiVersion}";
        _logger.LogDebug("Listing environments: {Url}", url);
        return await ExecuteRequestAsync(url, cancellationToken);
    }

    /// <summary>
    /// List environment operations
    /// </summary>
    public async Task<JsonDocument> ListEnvironmentOperationsAsync(
        string environmentId,
        int? limit = null,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"/environmentmanagement/environments/{environmentId}/operations?api-version={_config.ApiVersion}";
        
        var queryParams = new List<string>();
        if (limit.HasValue)
        {
            queryParams.Add($"limit={limit.Value}");
        }
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            queryParams.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
        }
        
        if (queryParams.Count > 0)
        {
            url += "&" + string.Join("&", queryParams);
        }

        _logger.LogDebug("Listing environment operations: {Url}", url);
        return await ExecuteRequestAsync(url, cancellationToken);
    }

    /// <summary>
    /// Get operation by ID
    /// </summary>
    public async Task<JsonDocument> GetOperationByIdAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/environmentmanagement/operations/{operationId}?api-version={_config.ApiVersion}";
        _logger.LogDebug("Getting operation: {Url}", url);
        return await ExecuteRequestAsync(url, cancellationToken);
    }

    /// <summary>
    /// Execute HTTP GET request with authentication and retry logic
    /// </summary>
    private async Task<JsonDocument> ExecuteRequestAsync(string url, CancellationToken cancellationToken)
    {
        return await RetryHelper.ExecuteWithRetryAsync(
            async () =>
            {
                // Get access token
                var token = await _authProvider.GetPowerPlatformTokenAsync(cancellationToken);

                // Create request
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                _logger.LogDebug("Sending request: {Method} {Url}", request.Method, url);

                // Send request
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                // Log response status
                _logger.LogDebug("Response: {StatusCode} {ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);

                // Handle non-success status codes
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("API error: {StatusCode} - {Content}", response.StatusCode, errorContent);

                    throw response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => new UnauthorizedAccessException(
                            "Authentication failed. Please run 'az login' and ensure you have access to Power Platform."),
                        HttpStatusCode.Forbidden => new UnauthorizedAccessException(
                            $"Access denied. You may not have permission to access this resource."),
                        HttpStatusCode.NotFound => new InvalidOperationException(
                            $"Resource not found: {url}"),
                        HttpStatusCode.TooManyRequests => new InvalidOperationException(
                            "Rate limit exceeded. Please wait before retrying."),
                        _ => new HttpRequestException(
                            $"Power Platform API error: {response.StatusCode} - {errorContent}")
                    };
                }

                // Parse and return JSON response
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonDocument.Parse(content);
            },
            maxRetries: 3,
            cancellationToken: cancellationToken);
    }
}
