using System.Diagnostics;
using System.Text;

namespace ErCharExport;

public sealed record ProcessResult(int ExitCode, string Output);

public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        Path? workingDirectory = null,
        Path? logPath = null)
    {
        var output = new StringBuilder();
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (workingDirectory is not null)
            startInfo.WorkingDirectory = workingDirectory.Value.FullName;

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) => Append(args.Data);
        process.ErrorDataReceived += (_, args) => Append(args.Data);

        if (!process.Start())
            throw new CliException($"Could not start process: {fileName}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        string text = output.ToString();
        if (logPath is not null)
        {
            Directory.CreateDirectory(logPath.Value.Parent.FullName);
            await File.WriteAllTextAsync(logPath.Value.FullName, text);
        }

        return new ProcessResult(process.ExitCode, text);

        void Append(string? line)
        {
            if (line is null)
                return;
            Console.WriteLine(line);
            output.AppendLine(line);
        }
    }
}
