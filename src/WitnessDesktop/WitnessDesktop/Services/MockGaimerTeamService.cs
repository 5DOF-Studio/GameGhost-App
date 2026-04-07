using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public sealed class MockGaimerTeamService : IGaimerTeamService
{
    private readonly ILogger<MockGaimerTeamService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingTasks = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingPermissions = new();
    private CancellationTokenSource _cts = new();
    private bool _disposed;
    private int _responseIndex;

    private static readonly string[] CannedResponses =
    [
        "I checked the current meta and your build is solid. The MCW with JAK Heretic carbine kit is slightly faster TTK at range, but your MP5 setup is better for close-quarters on Shoothouse.",
        "Looked into the Sicilian Najdorf for you. The main line after 6.Bg5 is still e6, but there's an interesting sideline with 6...Nbd7 that's been gaining traction at the GM level.",
        "Found three recent guides on that topic. The consensus is to focus on map control in the first 30 seconds. I saved the links to your session notes.",
        "Ran a quick search on your opponent's recent games. They tend to play aggressive openings but struggle in endgames. Might be worth trading pieces early.",
        "The streaming setup looks good. OBS is detecting your game window correctly. I'd suggest switching to the Game Capture source instead of Window Capture for better frame rates."
    ];

    public double PermissionProbability { get; set; } = 0.0; // Phase G: re-enable when permission UI exists
    public int ResponseDelayMs { get; set; } = 3000;

    public MockGaimerTeamService(ILogger<MockGaimerTeamService> logger)
    {
        _logger = logger;
    }

    public bool IsConnected { get; private set; }
    public bool IsConfigured => true;

    public event EventHandler<GaimerTeamResultEventArgs>? TaskCompleted;
    public event EventHandler<GaimerTeamProgressEventArgs>? TaskProgress;
    public event EventHandler<GaimerTeamPermissionEventArgs>? PermissionRequested;

    public Task<string> SubmitTaskAsync(GaimerTeamTask task, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected)
            throw new InvalidOperationException("Gaimer Team is not connected. Call LaunchSessionAsync first.");

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
        _pendingTasks[task.Id] = linkedCts;
        _ = ProcessTaskAsync(task, linkedCts.Token);

        _logger.LogInformation("[MockGaimerTeam] Task submitted: {TaskId} — {Task}", task.Id, task.Task);
        return Task.FromResult(task.Id);
    }

    public Task CancelTaskAsync(string taskId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pendingTasks.TryRemove(taskId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _logger.LogInformation("[MockGaimerTeam] Task cancelled: {TaskId}", taskId);
        }
        return Task.CompletedTask;
    }

    public Task RespondToPermissionAsync(string permissionId, bool approved, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pendingPermissions.TryRemove(permissionId, out var tcs))
        {
            tcs.TrySetResult(approved);
            _logger.LogInformation("[MockGaimerTeam] Permission {Id} responded: {Approved}", permissionId, approved);
        }
        return Task.CompletedTask;
    }

    public Task<bool> LaunchSessionAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsConnected = true;
        _logger.LogInformation("[MockGaimerTeam] Session launched (mock)");
        return Task.FromResult(true);
    }

    public Task<bool> ConnectExistingAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsConnected = true;
        _logger.LogInformation("[MockGaimerTeam] Connected to existing session (mock)");
        return Task.FromResult(true);
    }

    public Task DisconnectAsync(bool terminateOwnedSession = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsConnected = false;
        var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        oldCts.Cancel();
        oldCts.Dispose();
        foreach (var (_, cts) in _pendingTasks)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _pendingTasks.Clear();
        _pendingPermissions.Clear();
        _logger.LogInformation("[MockGaimerTeam] Disconnected");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        foreach (var (_, cts) in _pendingTasks)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _pendingTasks.Clear();
        _pendingPermissions.Clear();
    }

    private async Task ProcessTaskAsync(GaimerTeamTask task, CancellationToken ct)
    {
        try
        {
            var random = new Random();
            if (random.NextDouble() < PermissionProbability)
            {
                var permId = $"perm_{Guid.NewGuid():N}"[..12];
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingPermissions[permId] = tcs;

                PermissionRequested?.Invoke(this, new GaimerTeamPermissionEventArgs
                {
                    Request = new GaimerTeamPermissionRequest
                    {
                        Id = permId,
                        TaskId = task.Id,
                        Action = "Mock action requiring approval",
                        Risk = "low",
                        TimeoutSeconds = 60
                    }
                });

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                try
                {
                    var approved = await tcs.Task.WaitAsync(timeoutCts.Token);
                    if (!approved)
                    {
                        RaiseCompleted(task.Id, "error", "Permission denied by user.", errorCode: "permission_denied");
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    _pendingPermissions.TryRemove(permId, out _);
                    RaiseCompleted(task.Id, "error", "Permission timed out.", errorCode: "permission_timeout");
                    return;
                }
            }

            await Task.Delay(ResponseDelayMs, ct);
            ct.ThrowIfCancellationRequested();

            var index = Interlocked.Increment(ref _responseIndex) - 1;
            var response = CannedResponses[index % CannedResponses.Length];
            RaiseCompleted(task.Id, "complete", response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[MockGaimerTeam] Task {TaskId} cancelled", task.Id);
        }
        finally
        {
            if (_pendingTasks.TryRemove(task.Id, out var cts))
                cts.Dispose();
        }
    }

    private void RaiseCompleted(string taskId, string status, string response, string? errorCode = null)
    {
        TaskCompleted?.Invoke(this, new GaimerTeamResultEventArgs
        {
            Result = new GaimerTeamResult
            {
                TaskId = taskId,
                Status = status,
                Response = response,
                ErrorCode = errorCode
            }
        });
    }
}
