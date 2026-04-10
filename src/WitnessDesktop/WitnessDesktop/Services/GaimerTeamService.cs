using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public sealed class GaimerTeamService : IGaimerTeamService
{
    private readonly IGaimerPipeClient _pipe;
    private readonly IClaudeProcessManager _process;
    private readonly ISettingsService _settings;
    private readonly ILogger<GaimerTeamService> _logger;
    private readonly ConcurrentDictionary<string, GaimerTeamTask> _pendingTasks = new();

    private bool _ownsProcess;
    private int _restartAttempts;
    private int _restarting;
    private int _missedPings;
    private Timer? _healthTimer;
    private bool _disposed;

    private const int MaxRestartAttempts = 3;
    private const int MaxMissedPings = 3;
    private const int HealthIntervalMs = 30_000;
    private const int PipeConnectTimeoutMs = 10_000;
    private const int PipeConnectPollMs = 500;
    private const int PingTimeoutMs = 5_000;

    internal static readonly string SessionDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "Gaimer", "team-session");

    internal static readonly string SocketPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "Gaimer", "gaimer-team.sock");

    private volatile bool _isConnected; // PB-M3
    private bool? _isConfiguredCache; // H2: avoid spawning 3 subprocesses per access
    public bool IsConnected => _isConnected;
    public bool IsConfigured => _isConfiguredCache ??= EvaluateIsConfigured();
    public string? LastError { get; private set; }

    public event EventHandler<GaimerTeamResultEventArgs>? TaskCompleted;
    public event EventHandler<GaimerTeamProgressEventArgs>? TaskProgress;
    public event EventHandler<GaimerTeamPermissionEventArgs>? PermissionRequested;

    public GaimerTeamService(
        IGaimerPipeClient pipe,
        IClaudeProcessManager process,
        ISettingsService settings,
        ILogger<GaimerTeamService> logger)
    {
        _pipe = pipe;
        _process = process;
        _settings = settings;
        _logger = logger;

        // Invalidate cached IsConfigured when team settings change (H2)
        _settings.SettingChanged += (_, name) =>
        {
            if (name is nameof(ISettingsService.ClaudeCliPath)
                or nameof(ISettingsService.BunPath)
                or nameof(ISettingsService.PluginDirPath))
                _isConfiguredCache = null;
        };
    }

    private bool EvaluateIsConfigured() =>
        ResolvePluginPath() != null
        && ResolveClaudeCliPath() != null
        && ResolveBunPath() != null;

    // ── Pre-flight Checks ─────────────────────────────────────────

    private bool RunPreFlightChecks()
    {
        LastError = null;

        var pluginPath = ResolvePluginPath();
        if (pluginPath == null || !File.Exists(Path.Combine(pluginPath, "server.ts")))
        {
            LastError = "Channel plugin not found — check plugin directory in Settings > Team";
            _logger.LogError("[GaimerTeam] Pre-flight failed: {Error}", LastError);
            return false;
        }

        var cliPath = ResolveClaudeCliPath();
        if (cliPath == null)
        {
            LastError = "Claude CLI not found — install Claude Code or set path in Settings > Team";
            _logger.LogError("[GaimerTeam] Pre-flight failed: {Error}", LastError);
            return false;
        }

        var bunPath = ResolveBunPath();
        if (bunPath == null)
        {
            LastError = "Bun runtime not found — install Bun or set path in Settings > Team";
            _logger.LogError("[GaimerTeam] Pre-flight failed: {Error}", LastError);
            return false;
        }

        var socketDir = Path.GetDirectoryName(SocketPath)!;
        try { Directory.CreateDirectory(socketDir); }
        catch
        {
            LastError = "Cannot write to application support directory";
            _logger.LogError("[GaimerTeam] Pre-flight failed: {Error}", LastError);
            return false;
        }

        return true;
    }

    private string? ResolveClaudeCliPath()
    {
        var configured = _settings.ClaudeCliPath;
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
            return configured;
        return DiscoverBinaryPath("claude");
    }

    private string? ResolveBunPath()
    {
        var configured = _settings.BunPath;
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
            return configured;
        return DiscoverBinaryPath("bun");
    }

    internal string? ResolvePluginPath()
    {
        var configured = _settings.PluginDirPath;
        if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured))
            return configured;
        return GetPluginPath();
    }

    private static string? DiscoverBinaryPath(string binary)
    {
        try
        {
            var discoveryCommand = System.Runtime.InteropServices.RuntimeInformation
                .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "where.exe" : "which";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = discoveryCommand,
                Arguments = binary,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            if (!proc.WaitForExit(3000)) // M4: timeout to avoid hanging on misconfigured env
            {
                try { proc.Kill(); } catch { /* best-effort */ }
                return null;
            }
            return proc.ExitCode == 0 && File.Exists(output) ? output : null;
        }
        catch { return null; }
    }

    // ── Session Lifecycle ────────────────────────────────────────

    public async Task<bool> LaunchSessionAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // 0. Pre-flight checks
            if (!RunPreFlightChecks())
                return false;

            // 1. Create session directory
            Directory.CreateDirectory(SessionDir);

            // 2. Copy CLAUDE.md core template if available
            var templatePath = GetCoreTemplatePath();
            if (templatePath != null)
            {
                var destPath = Path.Combine(SessionDir, "CLAUDE.md");
                File.Copy(templatePath, destPath, overwrite: true);
            }

            // 3. Delete stale socket
            if (File.Exists(SocketPath))
                File.Delete(SocketPath);

            // 4. Launch process (use resolved plugin path from pre-flight)
            var pluginPath = ResolvePluginPath()!;

            var launched = await _process.LaunchAsync(SessionDir, pluginPath, ct);
            if (!launched)
            {
                _logger.LogError("[GaimerTeam] Failed to launch Claude process");
                return false;
            }

            // 5. Poll for socket file
            var deadline = Environment.TickCount64 + PipeConnectTimeoutMs;
            while (!File.Exists(SocketPath) && Environment.TickCount64 < deadline)
                await Task.Delay(PipeConnectPollMs, ct);

            if (!File.Exists(SocketPath))
            {
                _logger.LogError("[GaimerTeam] Socket file not created within timeout");
                await _process.TerminateAsync();
                return false;
            }

            // 6. Connect pipe
            var connected = await _pipe.ConnectAsync(SocketPath, ct);
            if (!connected)
            {
                _logger.LogError("[GaimerTeam] Failed to connect to pipe");
                await _process.TerminateAsync();
                return false;
            }

            // 7. Wire events (before ping to avoid message drop window — PB-H3)
            WireEvents();

            // 8. Ping/pong handshake
            var pong = await PingPongAsync(ct);
            if (!pong)
            {
                _logger.LogWarning("[GaimerTeam] Ping/pong handshake failed during launch");
                UnwireEvents();
                _pipe.Disconnect();
                await _process.TerminateAsync();
                return false;
            }

            // 9. Set state (_restartAttempts reset on pong, not here — PB-C3)
            _ownsProcess = true;
            Interlocked.Exchange(ref _missedPings, 0);
            _restartAttempts = 0;

            // 10. Start health timer
            StartHealthTimer();

            // 11. IsConnected
            _isConnected = true;
            _logger.LogInformation("[GaimerTeam] Session launched and connected");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GaimerTeam] Failed to launch session");
            return false;
        }
    }

    public async Task<bool> ConnectExistingAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // 1. Check socket exists
            if (!File.Exists(SocketPath))
            {
                _logger.LogError("[GaimerTeam] Socket file not found at {Path}", SocketPath);
                return false;
            }

            // 2. Connect pipe
            var connected = await _pipe.ConnectAsync(SocketPath, ct);
            if (!connected)
            {
                _logger.LogError("[GaimerTeam] Failed to connect to existing session pipe");
                return false;
            }

            // 3. Wire events (before ping to avoid message drop window — PB-H3)
            WireEvents();

            // 4. Ping/pong
            var pong = await PingPongAsync(ct);
            if (!pong)
            {
                _logger.LogWarning("[GaimerTeam] Ping/pong handshake failed on existing session");
                UnwireEvents();
                _pipe.Disconnect();
                return false;
            }

            // 5. Not owned
            _ownsProcess = false;
            Interlocked.Exchange(ref _missedPings, 0);
            _restartAttempts = 0;

            // 6. Start health timer
            StartHealthTimer();

            // 7. IsConnected
            _isConnected = true;
            _logger.LogInformation("[GaimerTeam] Connected to existing session");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GaimerTeam] Failed to connect to existing session");
            return false;
        }
    }

    public async Task DisconnectAsync(bool terminateOwnedSession = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        StopHealthTimer();
        UnwireEvents();
        ErrorOutPendingTasks("Disconnected from Gaimer Team session.");

        _pipe.Disconnect();

        if (_ownsProcess && terminateOwnedSession)
            await _process.TerminateAsync();

        _isConnected = false;
        _logger.LogInformation("[GaimerTeam] Disconnected (owned={Owned}, terminated={Terminated})",
            _ownsProcess, _ownsProcess && terminateOwnedSession);
    }

    // ── Task Operations ──────────────────────────────────────────

    public async Task<string> SubmitTaskAsync(GaimerTeamTask task, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected)
            throw new InvalidOperationException("Gaimer Team is not connected. Call LaunchSessionAsync or ConnectExistingAsync first.");

        _pendingTasks[task.Id] = task;

        // Wire protocol: sends "id" here; plugin maps to "task_id" in channel
        // message meta. Responses from plugin use "task_id" (see HandleTaskResult). (PB-M5)
        var wireMessage = JsonSerializer.Serialize(new
        {
            type = "task_request",
            id = task.Id,
            task = task.Task,
            timestamp = DateTimeOffset.UtcNow.ToString("o"),
            context = new
            {
                game = task.Context.Game,
                agent = task.Context.Agent,
                session_id = task.Context.SessionId,
                recent_activity = task.Context.RecentActivity,
                l1_context = task.Context.L1Context,
                l2_context = task.Context.L2Context
            },
            response_format = task.ResponseFormat
        });

        await _pipe.SendAsync(wireMessage, ct); // PB-M4: await instead of ContinueWith
        return task.Id;
    }

    public Task CancelTaskAsync(string taskId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pendingTasks.TryRemove(taskId, out _);
        _logger.LogInformation("[GaimerTeam] Task cancelled: {TaskId}", taskId);
        return Task.CompletedTask;
    }

    public Task RespondToPermissionAsync(string permissionId, bool approved, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected)
            throw new InvalidOperationException("Gaimer Team is not connected.");

        var wireMessage = JsonSerializer.Serialize(new
        {
            type = "permission_response",
            id = permissionId,
            approved
        });

        return _pipe.SendAsync(wireMessage, ct);
    }

    // ── Message Routing ──────────────────────────────────────────

    private void OnMessageReceived(object? sender, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
            {
                _logger.LogWarning("[GaimerTeam] Message received without 'type' field");
                return;
            }

            var type = typeProp.GetString();

            switch (type)
            {
                case "task_result":
                    HandleTaskResult(root);
                    break;

                case "error":
                    HandleError(root);
                    break;

                case "status_update":
                    HandleStatusUpdate(root);
                    break;

                case "permission_request":
                    HandlePermissionRequest(root);
                    break;

                case "pong":
                    Interlocked.Exchange(ref _missedPings, 0);
                    _restartAttempts = 0; // Reset only on sustained healthy connection (PB-C3)
                    break;

                default:
                    _logger.LogWarning("[GaimerTeam] Unknown message type: {Type}", type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GaimerTeam] Error processing message: {Json}", json);
        }
    }

    private void HandleTaskResult(JsonElement root)
    {
        // Wire protocol: plugin sends "task_id" (mapped from request "id") (PB-M5/M7)
        if (!root.TryGetProperty("task_id", out var tidProp) || tidProp.GetString() is not { } taskId)
        {
            _logger.LogWarning("[GaimerTeam] task_result missing 'task_id' field");
            return;
        }

        // Drop results for cancelled/unknown tasks
        if (!_pendingTasks.TryRemove(taskId, out var originalTask))
        {
            _logger.LogDebug("[GaimerTeam] Dropping result for unknown/cancelled task: {TaskId}", taskId);
            return;
        }

        if (!root.TryGetProperty("status", out var statusProp) || !root.TryGetProperty("response", out var responseProp))
        {
            _logger.LogWarning("[GaimerTeam] task_result missing required fields for task: {TaskId}", taskId);
            return;
        }

        var result = new GaimerTeamResult
        {
            TaskId = taskId,
            Status = statusProp.GetString()!,
            Response = responseProp.GetString()!,
            ActionsTaken = root.TryGetProperty("actions_taken", out var actions)
                ? actions.EnumerateArray().Select(a => a.GetString()!).ToList()
                : [],
            FollowUp = root.TryGetProperty("follow_up", out var fu) ? fu.GetString() : null,
            Artifacts = root.TryGetProperty("artifacts", out var arts)
                ? arts.EnumerateArray().Select(a => new GaimerTeamArtifact
                {
                    Type = a.GetProperty("type").GetString()!,
                    Title = a.GetProperty("title").GetString()!,
                    Content = a.GetProperty("content").GetString()!
                }).ToList()
                : [],
            Surface = root.TryGetProperty("surface", out var surf) ? surf.GetString() : null
        };

        TaskCompleted?.Invoke(this, new GaimerTeamResultEventArgs
        {
            Result = result,
            ResponseFormat = originalTask?.ResponseFormat ?? "voice"
        });
    }

    private void HandleError(JsonElement root)
    {
        var taskId = root.TryGetProperty("task_id", out var tid) ? tid.GetString() : null;

        if (taskId == null || !_pendingTasks.TryRemove(taskId, out var originalTask))
        {
            _logger.LogWarning("[GaimerTeam] Error for unknown/cancelled task: {TaskId}", taskId);
            return;
        }

        var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";

        var result = new GaimerTeamResult
        {
            TaskId = taskId,
            Status = "error",
            Response = message ?? "Unknown error",
            ErrorCode = root.TryGetProperty("error_code", out var ec) ? ec.GetString() : null
        };

        TaskCompleted?.Invoke(this, new GaimerTeamResultEventArgs
        {
            Result = result,
            ResponseFormat = originalTask?.ResponseFormat ?? "voice" // NEW-1
        });
    }

    private void HandleStatusUpdate(JsonElement root)
    {
        if (!root.TryGetProperty("task_id", out var tidProp) || tidProp.GetString() is not { } taskId
            || !root.TryGetProperty("message", out var msgProp) || msgProp.GetString() is not { } message)
        {
            _logger.LogWarning("[GaimerTeam] status_update missing required fields");
            return;
        }

        TaskProgress?.Invoke(this, new GaimerTeamProgressEventArgs
        {
            TaskId = taskId,
            Message = message
        });
    }

    private void HandlePermissionRequest(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var idProp) || idProp.GetString() is not { } id
            || !root.TryGetProperty("task_id", out var tidProp) || tidProp.GetString() is not { } taskId
            || !root.TryGetProperty("action", out var actionProp) || actionProp.GetString() is not { } action
            || !root.TryGetProperty("risk", out var riskProp) || riskProp.GetString() is not { } risk)
        {
            _logger.LogWarning("[GaimerTeam] permission_request missing required fields");
            return;
        }

        var request = new GaimerTeamPermissionRequest
        {
            Id = id,
            TaskId = taskId,
            Action = action,
            Risk = risk,
            TimeoutSeconds = root.TryGetProperty("timeout_seconds", out var ts) ? ts.GetInt32() : 60
        };

        PermissionRequested?.Invoke(this, new GaimerTeamPermissionEventArgs { Request = request });
    }

    // ── Health Monitoring ────────────────────────────────────────

    private void StartHealthTimer()
    {
        _healthTimer = new Timer(OnHealthTick, null, HealthIntervalMs, HealthIntervalMs);
    }

    private void StopHealthTimer()
    {
        _healthTimer?.Dispose();
        _healthTimer = null;
    }

    private async void OnHealthTick(object? state)
    {
        try
        {
            // Skip ticks during active restart to prevent overlapping triggers (PB-H4)
            if (Volatile.Read(ref _restarting) != 0) return;

            var missed = Interlocked.Increment(ref _missedPings);

            // Send ping
            try
            {
                await _pipe.SendAsync("{\"type\":\"ping\"}", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GaimerTeam] Failed to send ping");
            }

            if (missed >= MaxMissedPings)
            {
                StopHealthTimer(); // Stop before triggering restart (PB-H4)
                _logger.LogWarning("[GaimerTeam] {Missed} missed pings, connection appears unhealthy", missed);

                if (_ownsProcess)
                    await AttemptRestartAsync();
                else
                    MarkDisconnected();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GaimerTeam] Error in health tick");
        }
    }

    // ── Restart Logic ────────────────────────────────────────────

    private async Task AttemptRestartAsync()
    {
        // Re-entrancy guard
        if (Interlocked.CompareExchange(ref _restarting, 1, 0) != 0)
            return;

        try
        {
            _restartAttempts++;
            if (_restartAttempts > MaxRestartAttempts)
            {
                _logger.LogError("[GaimerTeam] Max restart attempts ({Max}) exceeded, giving up", MaxRestartAttempts);
                MarkDisconnected();
                return;
            }

            _logger.LogInformation("[GaimerTeam] Attempting restart ({Attempt}/{Max})",
                _restartAttempts, MaxRestartAttempts);

            // Disconnect existing
            StopHealthTimer();
            UnwireEvents();
            _pipe.Disconnect();

            try
            {
                await _process.TerminateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GaimerTeam] Error terminating process during restart");
            }

            // Error-out pending tasks from the old session — new session has no memory of them
            ErrorOutPendingTasks("Session restarted. Please retry.");

            // Re-launch
            _isConnected = false;
            Interlocked.Exchange(ref _missedPings, 0);

            var success = await LaunchSessionAsync();
            if (!success)
            {
                _logger.LogWarning("[GaimerTeam] Restart attempt {Attempt} failed", _restartAttempts);
                // Will retry on next health tick or connection lost event
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GaimerTeam] Error during restart attempt");
        }
        finally
        {
            Interlocked.Exchange(ref _restarting, 0);
        }
    }

    private void OnConnectionLost(object? sender, EventArgs e)
    {
        _logger.LogWarning("[GaimerTeam] Connection lost");

        if (_ownsProcess)
            _ = AttemptRestartAsync();
        else
            MarkDisconnected();
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        _logger.LogWarning("[GaimerTeam] Process exited");

        if (_ownsProcess)
            _ = AttemptRestartAsync();
    }

    private void MarkDisconnected()
    {
        StopHealthTimer();
        UnwireEvents();
        _pipe.Disconnect();
        _isConnected = false;
        ErrorOutPendingTasks("Connection to Gaimer Team session lost.");
        _logger.LogWarning("[GaimerTeam] Marked as disconnected");
    }

    // ── Event Wiring ─────────────────────────────────────────────

    private void WireEvents()
    {
        _pipe.MessageReceived += OnMessageReceived;
        _pipe.ConnectionLost += OnConnectionLost;
        _process.ProcessExited += OnProcessExited;
    }

    private void UnwireEvents()
    {
        _pipe.MessageReceived -= OnMessageReceived;
        _pipe.ConnectionLost -= OnConnectionLost;
        _process.ProcessExited -= OnProcessExited;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private void ErrorOutPendingTasks(string reason)
    {
        var taskIds = _pendingTasks.Keys.ToList();
        foreach (var taskId in taskIds)
        {
            if (_pendingTasks.TryRemove(taskId, out _))
            {
                TaskCompleted?.Invoke(this, new GaimerTeamResultEventArgs
                {
                    Result = new GaimerTeamResult
                    {
                        TaskId = taskId,
                        Status = "error",
                        Response = reason,
                        ErrorCode = "disconnected"
                    }
                });
            }
        }
    }

    private async Task<bool> PingPongAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "pong")
                    tcs.TrySetResult(true);
            }
            catch { /* ignore parse errors during handshake */ }
        }

        _pipe.MessageReceived += Handler;
        try
        {
            await _pipe.SendAsync("{\"type\":\"ping\"}", ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(PingTimeoutMs);

            try
            {
                return await tcs.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[GaimerTeam] Ping/pong timed out after {Ms}ms", PingTimeoutMs);
                return false;
            }
        }
        finally
        {
            _pipe.MessageReceived -= Handler;
        }
    }

    internal static string? GetPluginPath()
    {
        // Try relative paths from app base directory
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "gaimer-channel-plugin"),
            Path.Combine(baseDir, "..", "gaimer-channel-plugin"),
            Path.Combine(baseDir, "..", "..", "gaimer-channel-plugin"),
            Path.Combine(baseDir, "..", "..", "..", "gaimer-channel-plugin"),
            // Well-known dev path
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Developer", "5DOF-Studio", "Gaimer-app", "gaimer-channel-plugin")
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    internal static string? GetCoreTemplatePath()
    {
        var pluginPath = GetPluginPath();
        if (pluginPath == null) return null;

        var candidate = Path.Combine(pluginPath, "CLAUDE.md");
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Test helper: sets internal connected state and wires events
    /// without going through the full launch/connect flow.
    /// </summary>
    internal void SetConnectedForTest(bool owned)
    {
        _ownsProcess = owned;
        _isConnected = true;
        Interlocked.Exchange(ref _missedPings, 0);
        WireEvents();
    }

    // ── IDisposable ──────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopHealthTimer();
        UnwireEvents();
        ErrorOutPendingTasks("Service disposed.");

        _pipe.Disconnect();

        // ClaudeProcessManager.Dispose() handles Kill + WaitForExit(5000) synchronously (PB-H5)
        _pipe.Dispose();
        _process.Dispose();
    }
}
