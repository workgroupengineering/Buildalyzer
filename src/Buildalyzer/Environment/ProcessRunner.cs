using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Buildalyzer.Environment;

internal class ProcessRunner : IDisposable
{
    private readonly ILogger Logger;

    public List<string> Output { get; } = new List<string>();
    public List<string> Error { get; } = new List<string>();

    public int ExitCode => Process.ExitCode;

    private Process Process { get; }

    public Action Exited { get; set; }

    public ProcessRunner(
        string fileName,
        string arguments,
        string workingDirectory,
        Dictionary<string, string?> environmentVariables,
        ILoggerFactory? loggerFactory)
    {
        Logger = loggerFactory?.CreateLogger<ProcessRunner>() ?? NullLogger<ProcessRunner>.Instance;
        Process = new Process
        {
            StartInfo =
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        // Copy over environment variables
        if (environmentVariables != null)
        {
            foreach (KeyValuePair<string, string> variable in environmentVariables)
            {
                Process.StartInfo.Environment[variable.Key] = variable.Value;
                Process.StartInfo.EnvironmentVariables[variable.Key] = variable.Value;
            }
        }

        Process.EnableRaisingEvents = true;  // Raises Process.Exited immediately instead of when checked via .WaitForExit() or .HasExited
        Process.Exited += ProcessExited;

        Process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Output.Add(e.Data);
                Logger.LogDebug("{Data}{NewLine}", e.Data, System.Environment.NewLine);
            }
        };
        Process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Error.Add(e.Data);
                Logger.LogDebug("{Data}{NewLine}", e.Data, System.Environment.NewLine);
            }
        };
    }

    public ProcessRunner Start()
    {
        Process.Start();
        Process.BeginOutputReadLine();
        Process.BeginErrorReadLine();
        Logger.LogDebug(
            "Started process {ProcessId}: \"{FileName}\" {Arguments}{NewLine}",
            Process.Id,
            Process.StartInfo.FileName,
            Process.StartInfo.Arguments,
            System.Environment.NewLine);
        return this;
    }

    private void ProcessExited(object? sender, EventArgs e)
    {
        Exited?.Invoke();
        Logger.LogDebug(
            "Process {Id} exited with code {ExitCode}{NewLine}",
            Process.Id,
            Process.ExitCode,
            System.Environment.NewLine);
    }

    public void WaitForExit() => Process.WaitForExit();

    public bool WaitForExit(int timeout)
    {
        bool exited = Process.WaitForExit(timeout);
        if (exited)
        {
            // To ensure that asynchronous event handling has been completed, call the WaitForExit() overload that takes no parameter after receiving a true from this overload.
            // From https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?redirectedfrom=MSDN&view=netcore-3.1#System_Diagnostics_Process_WaitForExit_System_Int32_
            // See also https://github.com/dotnet/runtime/issues/27128
            Process.WaitForExit();
        }
        return exited;
    }

    public void Dispose()
    {
        Process.Exited -= ProcessExited;
        Process.Close();
    }
}
