namespace PipeDream.Mcp.Common;

/// <summary>
/// Helper for retrying operations with exponential backoff
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Execute an async operation with retry logic
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        int initialDelayMs = 1000,
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        var retryCount = 0;
        var delay = initialDelayMs;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (retryCount < maxRetries && (shouldRetry?.Invoke(ex) ?? IsTransientError(ex)))
            {
                retryCount++;
                
                // Check for rate limiting (429) and parse Retry-After
                if (ex is HttpRequestException httpEx && httpEx.Message.Contains("429"))
                {
                    Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RetryHelper: Rate limit encountered (429 Too Many Requests)");
                    
                    var retryAfter = ParseRetryAfter(httpEx);
                    if (retryAfter.HasValue)
                    {
                        delay = retryAfter.Value;
                        Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RetryHelper: Retry-After header indicates waiting {delay / 1000} seconds");
                    }
                    else
                    {
                        Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RetryHelper: No Retry-After header, using exponential backoff");
                    }
                }
                
                Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RetryHelper: Retry {retryCount}/{maxRetries} after {delay}ms due to: {ex.Message}");

                await Task.Delay(delay, cancellationToken);
                
                // Only apply exponential backoff if not using Retry-After
                if (!(ex is HttpRequestException httpEx2 && httpEx2.Message.Contains("429") && ParseRetryAfter(httpEx2).HasValue))
                {
                    delay *= 2; // Exponential backoff
                }
            }
        }
    }

    /// <summary>
    /// Determine if an exception represents a transient error worth retrying
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        // Network-level errors
        if (ex is HttpRequestException httpEx)
        {
            var message = httpEx.Message.ToLowerInvariant();
            
            // Connection failures
            if (message.Contains("connection refused") ||
                message.Contains("no such host") ||
                message.Contains("name resolution") ||
                message.Contains("network is unreachable") ||
                message.Contains("connection reset") ||
                message.Contains("broken pipe"))
            {
                Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RetryHelper: Network connection error detected");
                return true;
            }
            
            return IsTransientStatusCode(httpEx);
        }
        
        // Timeout errors
        if (ex is TaskCanceledException or OperationCanceledException)
        {
            Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RetryHelper: Operation timeout detected");
            return true;
        }
        
        return false;
    }

    private static bool IsTransientStatusCode(HttpRequestException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("408") ||  // Request Timeout
               message.Contains("429") ||  // Too Many Requests
               message.Contains("500") ||  // Internal Server Error
               message.Contains("502") ||  // Bad Gateway
               message.Contains("503") ||  // Service Unavailable
               message.Contains("504");    // Gateway Timeout
    }

    /// <summary>
    /// Parse Retry-After header from HttpRequestException
    /// Returns delay in milliseconds if found, otherwise null
    /// </summary>
    private static int? ParseRetryAfter(HttpRequestException ex)
    {
        // Check for Retry-After in exception message
        var message = ex.Message;
        
        // Format: "Response status code does not indicate success: 429 (Too Many Requests)."
        // Sometimes includes "Retry after XXX seconds" or "retry-after: XXX"
        if (message.Contains("retry", StringComparison.OrdinalIgnoreCase) && 
            message.Contains("after", StringComparison.OrdinalIgnoreCase))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                message, 
                @"retry[- ]after[:\s]+(\d+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success && int.TryParse(match.Groups[1].Value, out var seconds))
            {
                return seconds * 1000; // Convert to milliseconds
            }
        }
        
        return null;
    }
}
