using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WitnessDesktop.Services;

public sealed class ClaudeProcessManager : IClaudeProcessManager
{
    private readonly ILogger<ClaudeProcessManager> _logger;
    private Process? _process;
    private bool _intentionalTermination;
    private bool _disposed;

    public ClaudeProcessManager(ILogger<ClaudeProcessManager> logger)
    {
        _logger = logger;
    }

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>
    /// PID of the managed process. 0 if not running. Used for testing only.
    /// </summary>
    internal int ProcessId => _process?.Id ?? 0;

    public event EventHandler? ProcessExited;

    public Task<bool> LaunchAsync(string workingDirectory, string pluginPath, CancellationToken ct = default)
        => LaunchAsync(workingDirectory, pluginPath, ct, cliOverride: null, argsOverride: null);

    internal async Task<bool> LaunchAsync(string workingDirectory, string pluginPath,
        CancellationToken ct = default, string? cliOverride = null, string? argsOverride = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cliName = cliOverride ?? "claude";

        // Verify CLI is on PATH
        try
        {
            var which = new ProcessStartInfo("which", cliName)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var whichProc = Process.Start(which);
            if (whichProc != null)
            {
                await whichProc.WaitForExitAsync(ct);
                if (whichProc.ExitCode != 0)
                {
                    _logger.LogError("[ClaudeProcess] '{Cli}' not found on PATH", cliName);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClaudeProcess] Failed to check for '{Cli}'", cliName);
            return false;
        }

        var arguments = argsOverride ??
            $"--plugin-dir \"{pluginPath}\" --dangerously-skip-permissions -p \"You are operating as the Gaimer Team agent. Wait for channel messages and respond using the submit_result and send_status tools.\"";

        var psi = new ProcessStartInfo
        {
            FileName = cliName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            _intentionalTermination = false;
            _process = Process.Start(psi);
            if (_process == null)
            {
                _logger.LogError("[ClaudeProcess] Process.Start returned null");
                return false;
            }

            _process.EnableRaisingEvents = true;
            _process.Exited += OnProcessExited;

            // Drain stdout/stderr to prevent buffer deadlocks
            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) _logger.LogDebug("[Claude stdout] {Line}", e.Data);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) _logger.LogDebug("[Claude stderr] {Line}", e.Data);
            };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _logger.LogInformation("[ClaudeProcess] Launched PID {Pid} in {Dir}", _process.Id, workingDirectory);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClaudeProcess] Failed to launch '{Cli}'", cliName);
            return false;
        }
    }

    public async Task TerminateAsync()
    {
        if (_process is not { HasExited: false }) return;

        _intentionalTermination = true;
        try
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
            _logger.LogInformation("[ClaudeProcess] Terminated PID {Pid}", _process.Id);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            _logger.LogDebug("[ClaudeProcess] Process already exited during terminate: {Message}", ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_process is { HasExited: false })
        {
            _intentionalTermination = true;
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
            catch { /* best-effort cleanup */ }
        }
        _process?.Dispose();
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_intentionalTermination) return;
        _logger.LogWarning("[ClaudeProcess] Unexpected exit (code {Code})", _process?.ExitCode);
        ProcessExited?.Invoke(this, EventArgs.Empty);
    }
}
