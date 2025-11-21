using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace PipeDream.Mcp.Auth;

/// <summary>
/// Provides Azure authentication using Azure CLI credentials
/// </summary>
public class AzureAuthProvider
{
    private readonly AzureCliCredential _credential;
    private readonly Dictionary<string, AccessToken> _tokenCache;
    private readonly SemaphoreSlim _lock;
    private readonly ILogger<AzureAuthProvider> _logger;

    public AzureAuthProvider(ILogger<AzureAuthProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credential = new AzureCliCredential();
        _tokenCache = new Dictionary<string, AccessToken>();
        _lock = new SemaphoreSlim(1, 1);
        
        _logger.LogInformation("AzureAuthProvider initialized");
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
    /// Get access token for Power Platform
    /// </summary>
    public async Task<string> GetPowerPlatformTokenAsync(CancellationToken cancellationToken = default)
    {
        const string resource = "https://api.powerplatform.com";
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
                    var timeRemaining = cachedToken.ExpiresOn - DateTimeOffset.UtcNow;
                    _logger.LogDebug("Using cached token for {Resource} (expires in {Minutes:F1} minutes)", resource, timeRemaining.TotalMinutes);
                    return cachedToken.Token;
                }
                else
                {
                    _logger.LogDebug("Cached token expired or expiring soon, refreshing...");
                    _tokenCache.Remove(resource);
                }
            }

            // Get new token from Azure CLI with retry logic
            return await AcquireNewTokenAsync(resource, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Acquire a new token from Azure CLI with retry logic
    /// </summary>
    private async Task<string> AcquireNewTokenAsync(string resource, CancellationToken cancellationToken, int retryCount = 0)
    {
        const int maxRetries = 2;
        
        try
        {
            _logger.LogDebug("Requesting new token for {Resource} (attempt {Attempt}/{MaxAttempts})", resource, retryCount + 1, maxRetries + 1);
            
            var tokenRequestContext = new TokenRequestContext(new[] { $"{resource}/.default" });
            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            
            // Cache the token
            _tokenCache[resource] = token;
            
            var expiresIn = token.ExpiresOn - DateTimeOffset.UtcNow;
            _logger.LogInformation("Token acquired successfully, expires in {Minutes:F1} minutes ({ExpiresOn:yyyy-MM-dd HH:mm:ss} UTC)", expiresIn.TotalMinutes, token.ExpiresOn);
            return token.Token;
        }
        catch (AuthenticationFailedException ex)
        {
            var errorMessage = $"Azure CLI authentication failed: {ex.Message}";
            _logger.LogError(ex, "Azure CLI authentication failed");

            // Retry for transient failures
            if (retryCount < maxRetries && IsTransientAuthError(ex))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                _logger.LogWarning("Retrying after {Seconds} seconds...", delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                return await AcquireNewTokenAsync(resource, cancellationToken, retryCount + 1);
            }

            // Provide detailed error message based on exception
            var userMessage = GetUserFriendlyAuthError(ex, resource);
            throw new InvalidOperationException(userMessage, ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Token acquisition cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token acquisition");
            throw new InvalidOperationException(
                $"Failed to acquire access token for {resource}. Error: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Determine if an authentication error is transient and worth retrying
    /// </summary>
    private static bool IsTransientAuthError(AuthenticationFailedException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("timeout") ||
               message.Contains("network") ||
               message.Contains("connection") ||
               message.Contains("unavailable");
    }

    /// <summary>
    /// Get user-friendly error message based on authentication failure
    /// </summary>
    private static string GetUserFriendlyAuthError(AuthenticationFailedException ex, string resource)
    {
        var message = ex.Message.ToLowerInvariant();

        if (message.Contains("az: command not found") || message.Contains("az' is not recognized"))
        {
            return "Azure CLI is not installed. Please install it from https://learn.microsoft.com/cli/azure/install-azure-cli";
        }

        if (message.Contains("not logged in") || message.Contains("no accounts") || message.Contains("az login"))
        {
            return "Azure CLI is not authenticated. Please run 'az login' to authenticate.";
        }

        if (message.Contains("access denied") || message.Contains("unauthorized") || message.Contains("forbidden"))
        {
            return $"Access denied to resource '{resource}'. Ensure your Azure account has the necessary permissions.";
        }

        if (message.Contains("subscription"))
        {
            return "No active Azure subscription found. Please run 'az account set --subscription <subscription-id>' to select a subscription.";
        }

        return $"Azure CLI authentication failed for resource '{resource}'. Please run 'az login' and ensure you have access. Error: {ex.Message}";
    }

    /// <summary>
    /// Verify Azure CLI is available and authenticated
    /// </summary>
    public async Task<bool> VerifyAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Verifying Azure CLI authentication");
            
            // Try to get a token for Azure Resource Manager as a test
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            
            var expiresIn = token.ExpiresOn - DateTimeOffset.UtcNow;
            _logger.LogInformation("Azure CLI authentication verified (token expires in {Minutes:F1} minutes)", expiresIn.TotalMinutes);
            return true;
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogError(ex, "Azure CLI authentication verification failed");
            Console.Error.WriteLine($"\nAuthentication Error: {GetUserFriendlyAuthError(ex, "Azure Resource Manager")}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during authentication verification");
            Console.Error.WriteLine($"\nError: Failed to verify Azure CLI authentication. {ex.Message}");
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
            _logger.LogDebug("Token cache cleared");
        }
        finally
        {
            _lock.Release();
        }
    }
}
