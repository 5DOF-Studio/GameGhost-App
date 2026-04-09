using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WitnessDesktop.Data;
using WitnessDesktop.Data.Entities;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Services.History;

/// <summary>
/// Writes session lifecycle, chat messages, and timeline events to the
/// SQLite history database. Each method creates a short-lived DbContext,
/// performs the write, and disposes. All methods swallow exceptions —
/// write failures must NEVER break the live pipeline.
/// </summary>
public class SessionHistoryService : ISessionHistoryService
{
    private readonly string _dbPath;
    private readonly ISessionTraceService? _sessionTrace;

    /// <summary>
    /// Gate that child writes await before inserting. Ensures the session row
    /// exists in the DB before any FK-dependent child writes execute.
    /// Set in <see cref="StartSessionAsync"/> — completed on success or failure.
    /// </summary>
    private TaskCompletionSource? _sessionReady;

    /// <summary>
    /// Maximum time child writes will wait for the session row before giving up.
    /// Prevents infinite hangs if StartSessionAsync is never called.
    /// </summary>
    private static readonly TimeSpan SessionReadyTimeout = TimeSpan.FromSeconds(10);

    public SessionHistoryService(string dbPath, ISessionTraceService? sessionTrace = null)
    {
        _dbPath = dbPath;
        _sessionTrace = sessionTrace;

        // Bootstrap schema exactly once on construction.
        try
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var ctx = GaimerHistoryDbContext.CreateForPath(dbPath);
            ctx.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionHistoryService] Schema bootstrap failed: {ex.Message}");
        }
    }

    public async Task StartSessionAsync(
        string sessionId,
        string? agentKey,
        string? gameType,
        string? connectorName,
        string? targetWindowTitle,
        string? targetProcessName)
    {
        // Create the gate BEFORE the DB write so child writers can start awaiting immediately.
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _sessionReady = tcs;

        try
        {
            using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
            ctx.Sessions.Add(new SessionRecord
            {
                SessionId = sessionId,
                StartedAtUtc = DateTime.UtcNow,
                AgentKey = agentKey,
                GameType = gameType,
                ConnectorName = connectorName,
                TargetWindowTitle = targetWindowTitle,
                TargetProcessName = targetProcessName,
                AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                             ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            });
            await ctx.SaveChangesAsync();

            _sessionTrace?.TrackEvent("history.session.started", new Dictionary<string, string>
            {
                ["session_id"] = sessionId,
                ["agent_key"] = agentKey ?? "unknown",
                ["game_type"] = gameType ?? "unknown"
            });

            // Signal success — child writes may proceed.
            tcs.TrySetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionHistoryService] StartSessionAsync failed: {ex.Message}");

            // Signal failure so child writes don't block forever; they'll hit
            // their own FK errors but at least won't deadlock.
            tcs.TrySetResult();
        }
    }

    /// <summary>
    /// Waits for <see cref="StartSessionAsync"/> to complete before allowing
    /// a child write. Returns true if the gate was satisfied, false on timeout
    /// or if StartSessionAsync was never called (no gate exists).
    /// </summary>
    private async Task<bool> WaitForSessionReadyAsync()
    {
        var gate = _sessionReady;
        if (gate is null)
        {
            // No session started — child write will fail on FK but won't block.
            Console.WriteLine("[SessionHistoryService] WaitForSessionReady: no session gate — skipping write");
            return false;
        }

        try
        {
            await gate.Task.WaitAsync(SessionReadyTimeout);
            return true;
        }
        catch (TimeoutException)
        {
            Console.WriteLine("[SessionHistoryService] WaitForSessionReady: timed out waiting for session row");
            return false;
        }
    }

    public async Task FinalizeSessionAsync(string sessionId)
    {
        try
        {
            if (!await WaitForSessionReadyAsync()) return;

            using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
            var session = await ctx.Sessions.FindAsync(sessionId);
            if (session is not null)
            {
                session.EndedAtUtc = DateTime.UtcNow;
                await ctx.SaveChangesAsync();

                var durationMs = (long)(session.EndedAtUtc!.Value - session.StartedAtUtc).TotalMilliseconds;

                _sessionTrace?.TrackEvent("history.session.finalized", new Dictionary<string, string>
                {
                    ["session_id"] = sessionId,
                    ["duration_ms"] = durationMs.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionHistoryService] FinalizeSessionAsync failed: {ex.Message}");
        }
    }

    public async Task PersistChatMessageAsync(string sessionId, ChatMessage message)
    {
        try
        {
            if (!await WaitForSessionReadyAsync()) return;

            using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = message.Id,
                SessionId = sessionId,
                TimestampUtc = message.Timestamp,
                Role = message.Role.ToString(),
                Intent = message.Intent.ToString(),
                Content = message.Content,
                Source = message.Source,
                DeliveryState = message.DeliveryState.ToString(),
                CorrelationId = null
            });
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionHistoryService] PersistChatMessageAsync failed: {ex.Message}");
        }
    }

    public async Task PersistTimelineEventAsync(string sessionId, TimelineEvent evt, string? checkpointId, int displayOrder)
    {
        try
        {
            if (!await WaitForSessionReadyAsync()) return;

            using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
            ctx.TimelineEvents.Add(new TimelineEventRecord
            {
                Id = evt.Id,
                SessionId = sessionId,
                CheckpointId = checkpointId,
                CreatedAtUtc = evt.Timestamp,
                Type = evt.Type.ToString(),
                Role = evt.Role?.ToString(),
                Summary = evt.Summary,
                FullContent = evt.FullContent,
                Icon = evt.Icon,
                CapsuleColorHex = evt.CapsuleColorHex,
                CapsuleStrokeHex = evt.CapsuleStrokeHex,
                LinkedMessageId = evt.LinkedMessage?.Id,
                BrainSignal = evt.Brain?.Signal,
                BrainUrgency = evt.Brain?.Urgency,
                BrainEvaluation = evt.Brain?.Evaluation,
                BrainEvalDelta = evt.Brain?.EvalDelta,
                SuggestedAction = evt.Brain?.SuggestedAction,
                ToolName = evt.ToolCall?.ToolName,
                ToolDurationMs = evt.ToolCall?.DurationMs,
                DisplayOrder = displayOrder
            });
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionHistoryService] PersistTimelineEventAsync failed: {ex.Message}");
        }
    }
}
