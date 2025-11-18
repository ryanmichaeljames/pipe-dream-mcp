using System.Net.Http.Headers;
using System.Text.Json;
using PipeDream.Mcp.Auth;
using PipeDream.Mcp.Common;
using PipeDream.Mcp.Config;

namespace PipeDream.Mcp.Dataverse;

/// <summary>
/// Client for read-only operations against Microsoft Dataverse Web API
/// </summary>
public class DataverseClient
{
    private readonly HttpClient _httpClient;
    private readonly AzureAuthProvider _authProvider;
    private readonly DataverseConfig _config;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DataverseClient(AzureAuthProvider authProvider, DataverseConfig config)
    {
        _authProvider = authProvider;
        _config = config;
        
        // Normalize URL - remove trailing slash if present
        var baseUrl = _config.Url.TrimEnd('/');
        
        var handler = new SocketsHttpHandler
        {
            // Connection pooling and timeout settings
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(15),
            
            // Enable automatic decompression
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(_config.Timeout)
        };
        _httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        _httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.ConnectionClose = false; // Keep-alive

        Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DataverseClient: Initialized for {baseUrl}");
    }

    /// <summary>
    /// Execute OData query against Dataverse entity
    /// </summary>
    public async Task<JsonDocument> QueryAsync(
        string entityLogicalName,
        string[]? select = null,
        string? filter = null,
        int? top = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name is required", nameof(entityLogicalName));

        await EnsureAuthenticatedAsync(cancellationToken);

        // Build OData query URL
        var queryParams = new List<string>();
        if (select?.Length > 0)
            queryParams.Add($"$select={string.Join(",", select)}");
        if (!string.IsNullOrWhiteSpace(filter))
            queryParams.Add($"$filter={Uri.EscapeDataString(filter)}");
        if (top.HasValue)
            queryParams.Add($"$top={top.Value}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        var endpoint = $"/api/data/{_config.ApiVersion}/{entityLogicalName}{queryString}";

        Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DataverseClient: Query {endpoint}");

        return await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(content);
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Retrieve a single record by ID
    /// </summary>
    public async Task<JsonDocument> RetrieveAsync(
        string entityLogicalName,
        Guid recordId,
        string[]? select = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name is required", nameof(entityLogicalName));
        if (recordId == Guid.Empty)
            throw new ArgumentException("Record ID is required", nameof(recordId));

        await EnsureAuthenticatedAsync(cancellationToken);

        var queryParams = select?.Length > 0 ? $"?$select={string.Join(",", select)}" : string.Empty;
        var endpoint = $"/api/data/{_config.ApiVersion}/{entityLogicalName}({recordId:D}){queryParams}";

        Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DataverseClient: Retrieve {endpoint}");

        return await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(content);
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get metadata for entities (list of entities and their properties)
    /// </summary>
    public async Task<JsonDocument> GetMetadataAsync(
        string? entityLogicalName = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        string endpoint;
        if (!string.IsNullOrWhiteSpace(entityLogicalName))
        {
            // Get metadata for specific entity
            endpoint = $"/api/data/{_config.ApiVersion}/EntityDefinitions(LogicalName='{entityLogicalName}')?$select=LogicalName,DisplayName,PrimaryIdAttribute,PrimaryNameAttribute&$expand=Attributes($select=LogicalName,DisplayName,AttributeType)";
        }
        else
        {
            // List all entities
            endpoint = $"/api/data/{_config.ApiVersion}/EntityDefinitions?$select=LogicalName,DisplayName,PrimaryIdAttribute,PrimaryNameAttribute";
        }

        Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DataverseClient: Metadata {endpoint}");

        return await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DataverseClient: Error response: {errorContent}");
                response.EnsureSuccessStatusCode();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(content);
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// List records from an entity with pagination
    /// </summary>
    public async Task<JsonDocument> ListAsync(
        string entityLogicalName,
        int pageSize = 50,
        string? pagingCookie = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name is required", nameof(entityLogicalName));

        await EnsureAuthenticatedAsync(cancellationToken);

        var queryParams = new List<string> { $"$top={pageSize}" };
        if (!string.IsNullOrWhiteSpace(pagingCookie))
            queryParams.Add($"$skiptoken={Uri.EscapeDataString(pagingCookie)}");

        var queryString = "?" + string.Join("&", queryParams);
        var endpoint = $"/api/data/{_config.ApiVersion}/{entityLogicalName}{queryString}";

        Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DataverseClient: List {endpoint}");

        return await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(content);
        }, cancellationToken: cancellationToken);
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var token = await _authProvider.GetDataverseTokenAsync(_config.Url, cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        finally
        {
            _lock.Release();
        }
    }
}
