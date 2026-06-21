namespace ErCharExport;

public sealed class CliException : Exception
{
    public int ExitCode { get; }

    public CliException(string message, int exitCode = 1) : base(message)
    {
        ExitCode = exitCode;
    }
}

