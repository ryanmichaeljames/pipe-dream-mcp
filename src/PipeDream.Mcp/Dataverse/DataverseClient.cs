using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PipeDream.Mcp.Auth;
using PipeDream.Mcp.Common;
using PipeDream.Mcp.Config;
using PipeDream.Mcp.Dataverse.Constants;
using PipeDream.Mcp.Dataverse.Interfaces;
using MetadataFields = PipeDream.Mcp.Dataverse.Constants.Metadata.Fields;
using WorkflowFields = PipeDream.Mcp.Dataverse.Constants.Workflow.Fields;

namespace PipeDream.Mcp.Dataverse;

/// <summary>
/// Core HTTP client for Microsoft Dataverse Web API operations
/// </summary>
internal class DataverseClient
{
    private readonly HttpClient _httpClient;
    private readonly AzureAuthProvider _authProvider;
    private readonly DataverseConfig _config;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<DataverseClient> _logger;

    public DataverseClient(AzureAuthProvider authProvider, DataverseConfig config, ILogger<DataverseClient> logger)
    {
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
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

        _logger.LogInformation("DataverseClient initialized for {BaseUrl}", baseUrl);
    }

    /// <summary>
    /// Execute OData query against Dataverse entity
    /// </summary>
    public async Task<JsonDocument> QueryAsync(
        string entityLogicalName,
        string[]? select = null,
        string? filter = null,
        int? top = null,
        string? expand = null,
        string? orderBy = null,
        bool count = true,
        int? maxPageSize = null,
        bool includeFormattedValues = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name is required", nameof(entityLogicalName));

        await EnsureAuthenticatedAsync(cancellationToken);

        // Build OData query URL (order matters for some OData implementations)
        var queryParams = new List<string>();
        if (select?.Length > 0)
            queryParams.Add($"$select={string.Join(",", select)}");
        if (!string.IsNullOrWhiteSpace(filter))
            queryParams.Add($"$filter={Uri.EscapeDataString(filter)}");
        if (!string.IsNullOrWhiteSpace(orderBy))
            queryParams.Add($"$orderby={orderBy}");
        if (top.HasValue)
            queryParams.Add($"$top={top.Value}");
        if (!string.IsNullOrWhiteSpace(expand))
            queryParams.Add($"$expand={expand}");
        if (count)
            queryParams.Add("$count=true");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        var endpoint = $"/api/data/{_config.ApiVersion}/{entityLogicalName}{queryString}";

        _logger.LogDebug("Query {Endpoint}", endpoint);

        return await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            
            // Add Prefer headers
            var preferHeaders = new List<string>();
            if (maxPageSize.HasValue)
                preferHeaders.Add($"odata.maxpagesize={maxPageSize.Value}");
            if (includeFormattedValues)
                preferHeaders.Add("odata.include-annotations=OData.Community.Display.V1.FormattedValue");
            
            if (preferHeaders.Count > 0)
                request.Headers.Add("Prefer", string.Join(", ", preferHeaders));
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(content);
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Query using full nextLink URL from previous paginated query
    /// </summary>
    public async Task<JsonDocument> QueryNextLinkAsync(
        string nextLinkUrl,
        int? maxPageSize = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nextLinkUrl))
            throw new ArgumentException("NextLink URL is required", nameof(nextLinkUrl));

        // Validate URL is for this Dataverse instance
        if (!Uri.TryCreate(nextLinkUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid nextLink URL: {nextLinkUrl}", nameof(nextLinkUrl));

        var expectedHost = new Uri(_config.Url).Host;
        if (!uri.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"NextLink URL must be for configured Dataverse instance ({expectedHost}), got: {uri.Host}", nameof(nextLinkUrl));

        await EnsureAuthenticatedAsync(cancellationToken);

        // Extract path and query from nextLink
        var endpoint = uri.PathAndQuery;

        _logger.LogDebug("QueryNextLink {Endpoint}", endpoint);

        return await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            
            // Add Prefer header if maxPageSize specified (maintains consistent page size)
            if (maxPageSize.HasValue)
                request.Headers.Add("Prefer", $"odata.maxpagesize={maxPageSize.Value}");
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
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

        _logger.LogDebug("Retrieve {Endpoint}", endpoint);

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

        // Get metadata for specific entity or list all entities
        string endpoint = !string.IsNullOrWhiteSpace(entityLogicalName)
            ? $"/api/data/{_config.ApiVersion}/{Entities.EntityDefinitions}({MetadataFields.LogicalName}='{entityLogicalName}')?$select={MetadataFields.LogicalName},{MetadataFields.DisplayName},{MetadataFields.PrimaryIdAttribute},{MetadataFields.PrimaryNameAttribute}&$expand={MetadataFields.Attributes}($select={MetadataFields.LogicalName},{MetadataFields.DisplayName},{MetadataFields.AttributeType})"
            : $"/api/data/{_config.ApiVersion}/{Entities.EntityDefinitions}?$select={MetadataFields.LogicalName},{MetadataFields.DisplayName},{MetadataFields.PrimaryIdAttribute},{MetadataFields.PrimaryNameAttribute}";

        _logger.LogDebug("Metadata {Endpoint}", endpoint);

        return await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Error response: {ErrorContent}", errorContent);
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

        _logger.LogDebug("List {Endpoint}", endpoint);

        return await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(content);
        }, cancellationToken: cancellationToken);
    }



    /// <summary>
    /// Update a record with specified fields
    /// </summary>
    public async Task UpdateAsync(
        string entityLogicalName,
        Guid recordId,
        Dictionary<string, object> fields,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name is required", nameof(entityLogicalName));
        if (recordId == Guid.Empty)
            throw new ArgumentException("Record ID is required", nameof(recordId));
        if (fields == null || fields.Count == 0)
            throw new ArgumentException("Fields dictionary cannot be empty", nameof(fields));

        // Validate config flag
        if (!_config.EnableWriteOperations)
            throw new InvalidOperationException("Write operations are disabled. Enable with --enable-write-operations flag or set enableWriteOperations=true in config.");

        await EnsureAuthenticatedAsync(cancellationToken);

        var endpoint = $"/api/data/{_config.ApiVersion}/{entityLogicalName}({recordId:D})";
        var jsonContent = JsonSerializer.Serialize(fields);

        _logger.LogInformation("Update {Endpoint}", endpoint);
        _logger.LogDebug("Payload: {JsonContent}", jsonContent);

        await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Error response: {ErrorContent}", errorContent);
                response.EnsureSuccessStatusCode();
            }
            
            return true;
        }, cancellationToken: cancellationToken);

        _logger.LogInformation("Update successful");
    }

    /// <summary>
    /// Update the state of a record
    /// </summary>
    public async Task UpdateStateAsync(
        string entityLogicalName,
        Guid recordId,
        int stateCode,
        int statusCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name is required", nameof(entityLogicalName));
        if (recordId == Guid.Empty)
            throw new ArgumentException("Record ID is required", nameof(recordId));

        // Validate config flag
        if (!_config.EnableWriteOperations)
            throw new InvalidOperationException("Write operations are disabled. Enable with --enable-write-operations flag or set enableWriteOperations=true in config.");

        var fields = new Dictionary<string, object>
        {
            [WorkflowFields.StateCode] = stateCode,
            [WorkflowFields.StatusCode] = statusCode
        };

        _logger.LogInformation("UpdateState (entity={Entity}, id={Id}, state={State}, status={Status})", entityLogicalName, recordId, stateCode, statusCode);
        await UpdateAsync(entityLogicalName, recordId, fields, cancellationToken);
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


