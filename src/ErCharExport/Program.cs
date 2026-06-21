namespace ErCharExport;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await Cli.RunAsync(args);
        }
        catch (CliException ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

