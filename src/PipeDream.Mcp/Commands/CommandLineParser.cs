namespace PipeDream.Mcp.Commands;

/// <summary>
/// Parses command-line arguments for different subcommands
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Parse Dataverse subcommand arguments
    /// </summary>
    public static DataverseOptions ParseDataverseArgs(string[] args)
    {
        var options = new DataverseOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dataverse-url" when i + 1 < args.Length:
                case "-u" when i + 1 < args.Length:
                    options.DataverseUrl = args[++i];
                    break;
                case "--config-file" when i + 1 < args.Length:
                case "-c" when i + 1 < args.Length:
                    options.ConfigFile = args[++i];
                    break;
                case "--api-version" when i + 1 < args.Length:
                case "-a" when i + 1 < args.Length:
                    options.ApiVersion = args[++i];
                    break;
                case "--timeout" when i + 1 < args.Length:
                case "-t" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out int timeout))
                    {
                        options.Timeout = timeout;
                    }
                    break;
                case "--enable-write-operations":
                    options.EnableWriteOperations = true;
                    break;
                case "--enable-delete-operations":
                    options.EnableDeleteOperations = true;
                    break;
                case "--version":
                case "-v":
                    options.ShowVersion = true;
                    break;
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
            }
        }

        return options;
    }
}

/// <summary>
/// Command-line options for Dataverse subcommand
/// </summary>
public class DataverseOptions
{
    public string? DataverseUrl { get; set; }
    public string? ConfigFile { get; set; }
    public string? ApiVersion { get; set; }
    public int? Timeout { get; set; }
    public bool EnableWriteOperations { get; set; }
    public bool EnableDeleteOperations { get; set; }
    public bool ShowVersion { get; set; }
    public bool ShowHelp { get; set; }
    public bool Verbose { get; set; }
}
