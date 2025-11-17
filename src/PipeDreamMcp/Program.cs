using PipeDreamMcp.Protocol;

namespace PipeDreamMcp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var server = new McpServer();
            var cts = new CancellationTokenSource();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await server.RunAsync(cts.Token);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
