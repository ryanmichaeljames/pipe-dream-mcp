using Azure.Core;
using Azure.Identity;

namespace PipeDreamMcp.Auth;

/// <summary>
/// Provides Azure authentication using Azure CLI credentials
/// </summary>
public class AzureAuthProvider
{
    private readonly AzureCliCredential _credential;
    private readonly Dictionary<string, AccessToken> _tokenCache;
    private readonly SemaphoreSlim _lock;

    public AzureAuthProvider()
    {
        _credential = new AzureCliCredential();
        _tokenCache = new Dictionary<string, AccessToken>();
        _lock = new SemaphoreSlim(1, 1);
        
        LogToStderr("AzureAuthProvider initialized");
    }

    /// <summary>
    /// Get access token for Dataverse
    /// </summary>
    public async Task<string> GetDataverseTokenAsync(string dataverseUrl, CancellationToken cancellationToken = default)
    {
        // Dataverse resource format: https://org.crm.dynamics.com
        var uri = new Uri(dataverseUrl);
        var resource = $"{uri.Scheme}://{uri.Host}";
        
        return await GetTokenAsync(resource, cancellationToken);
    }

    /// <summary>
    /// Get access token for Azure DevOps
    /// </summary>
    public async Task<string> GetDevOpsTokenAsync(CancellationToken cancellationToken = default)
    {
        const string resource = "499b84ac-1321-427f-aa17-267ca6975798"; // Azure DevOps resource ID
        return await GetTokenAsync(resource, cancellationToken);
    }

    /// <summary>
    /// Get access token for specified resource with caching
    /// </summary>
    private async Task<string> GetTokenAsync(string resource, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Check cache for valid token
            if (_tokenCache.TryGetValue(resource, out var cachedToken))
            {
                // Refresh if token expires in less than 5 minutes
                if (cachedToken.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
                {
                    LogToStderr($"Using cached token for {resource}");
                    return cachedToken.Token;
                }
            }

            // Get new token from Azure CLI
            LogToStderr($"Requesting new token for {resource}");
            
            var tokenRequestContext = new TokenRequestContext(new[] { $"{resource}/.default" });
            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            
            // Cache the token
            _tokenCache[resource] = token;
            
            LogToStderr($"Token acquired, expires: {token.ExpiresOn:yyyy-MM-dd HH:mm:ss}");
            return token.Token;
        }
        catch (AuthenticationFailedException ex)
        {
            LogToStderr($"Authentication failed: {ex.Message}");
            throw new InvalidOperationException(
                "Azure CLI authentication failed. Please run 'az login' and ensure you have access to the resource.", 
                ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Verify Azure CLI is available and authenticated
    /// </summary>
    public async Task<bool> VerifyAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LogToStderr("Verifying Azure CLI authentication");
            
            // Try to get a token for Azure Resource Manager as a test
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            
            LogToStderr("Azure CLI authentication verified");
            return true;
        }
        catch (Exception ex)
        {
            LogToStderr($"Azure CLI authentication verification failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Clear token cache (useful for testing)
    /// </summary>
    public void ClearCache()
    {
        _lock.Wait();
        try
        {
            _tokenCache.Clear();
            LogToStderr("Token cache cleared");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Log to stderr for debugging
    /// </summary>
    private void LogToStderr(string message)
    {
        Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] AzureAuthProvider: {message}");
    }
}
