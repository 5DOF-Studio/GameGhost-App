using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public interface IGaimerTeamService : IDisposable
{
    Task<string> SubmitTaskAsync(GaimerTeamTask task, CancellationToken ct = default);
    Task CancelTaskAsync(string taskId, CancellationToken ct = default);
    Task RespondToPermissionAsync(string permissionId, bool approved, CancellationToken ct = default);

    event EventHandler<GaimerTeamResultEventArgs>? TaskCompleted;
    event EventHandler<GaimerTeamProgressEventArgs>? TaskProgress;
    event EventHandler<GaimerTeamPermissionEventArgs>? PermissionRequested;

    bool IsConnected { get; }
    bool IsConfigured { get; }

    Task<bool> LaunchSessionAsync(CancellationToken ct = default);
    Task<bool> ConnectExistingAsync(CancellationToken ct = default);
    Task DisconnectAsync(bool terminateOwnedSession = true);
}
