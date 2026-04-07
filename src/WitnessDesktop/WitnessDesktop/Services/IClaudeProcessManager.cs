namespace WitnessDesktop.Services;

public interface IClaudeProcessManager : IDisposable
{
    bool IsRunning { get; }
    event EventHandler? ProcessExited;

    Task<bool> LaunchAsync(string workingDirectory, string pluginPath, CancellationToken ct = default);
    Task TerminateAsync();
}
