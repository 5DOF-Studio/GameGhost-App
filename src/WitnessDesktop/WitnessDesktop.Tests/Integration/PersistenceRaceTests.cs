using Microsoft.EntityFrameworkCore;
using WitnessDesktop.Data;
using WitnessDesktop.Data.Entities;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.Services.History;
using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Tests.Integration;

/// <summary>
/// Race-condition and edge-case integration tests for the Phase 07
/// persistence layer. Verifies the hardened behavior introduced by
/// Codex review findings: session-ready gate, WAL mode, anchor
/// session validation, and fire-and-forget resilience.
/// </summary>
[Collection("PersistenceRace")]
public sealed class PersistenceRaceTests : IDisposable
{
    private readonly string _dbDir;
    private readonly string _dbPath;

    public PersistenceRaceTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), "gaimer-race-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dbDir);
        _dbPath = Path.Combine(_dbDir, "history.db");
    }

    public void Dispose()
    {
        foreach (var ext in new[] { "", "-journal", "-wal", "-shm" })
        {
            try { File.Delete(_dbPath + ext); } catch { }
        }
        try { Directory.Delete(_dbDir, true); } catch { }
    }

    // ── 1. Concurrent child writes during session startup ────────────

    [Fact]
    public async Task ConcurrentChildWrites_DuringSessionStartup_AllSucceed()
    {
        // Arrange — start session, then immediately fire 15 concurrent writes.
        // The session-ready gate should hold them until the session row exists.
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-race-burst";
        const int writeCount = 15;

        // Fire session start (do NOT await first — simulates fire-and-forget)
        var startTask = sut.StartSessionAsync(sessionId, "leroy", "chess", null, null, null);

        // Immediately fire concurrent child writes
        var writeTasks = Enumerable.Range(0, writeCount).Select(i =>
            sut.PersistChatMessageAsync(sessionId, new ChatMessage
            {
                Id = $"msg-burst-{i:D3}",
                Role = MessageRole.User,
                Intent = MessageIntent.GeneralChat,
                Content = $"Concurrent message #{i}",
                Timestamp = DateTime.UtcNow
            })
        ).ToList();

        // Wait for everything
        await Task.WhenAll(writeTasks.Prepend(startTask));

        // Assert — all 15 messages persisted
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var count = await ctx.ChatMessages.CountAsync(m => m.SessionId == sessionId);
        count.Should().Be(writeCount, $"all {writeCount} concurrent child writes should succeed after gate opens");
    }

    // ── 2. Rapid session start + finalize ────────────────────────────

    [Fact]
    public async Task RapidStartAndFinalize_BothTimestampsSet()
    {
        // Arrange — start then immediately finalize
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-rapid-fin";

        // Act — start and finalize back-to-back
        await sut.StartSessionAsync(sessionId, "wasp", "chess", null, null, null);
        await sut.FinalizeSessionAsync(sessionId);

        // Assert
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var session = await ctx.Sessions.FindAsync(sessionId);
        session.Should().NotBeNull();
        session!.StartedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        session.EndedAtUtc.Should().NotBeNull("finalize should set EndedAtUtc");
        session.EndedAtUtc!.Value.Should().BeOnOrAfter(session.StartedAtUtc);
    }

    // ── 3. Concurrent event persistence from multiple threads ────────

    [Fact]
    public async Task ConcurrentEventPersistence_NoLockedErrors()
    {
        // Arrange — simulate BrainEventRouter firing multiple PersistEventIfActive
        // calls concurrently (like the real emission queue does)
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-concurrent-events";
        const int eventCount = 20;

        await sut.StartSessionAsync(sessionId, "leroy", "chess", null, null, null);

        // Act — fire all event writes concurrently from multiple threads
        var tasks = Enumerable.Range(0, eventCount).Select(i => Task.Run(async () =>
        {
            await sut.PersistTimelineEventAsync(sessionId,
                new TimelineEvent
                {
                    Id = $"evt-conc-{i:D3}",
                    Type = EventOutputType.Assessment,
                    Summary = $"Concurrent event #{i}",
                    Timestamp = DateTime.UtcNow
                },
                checkpointId: null,
                displayOrder: i);
        })).ToArray();

        // This should NOT throw "database is locked" thanks to WAL + busy_timeout
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        ex.Should().BeNull("WAL mode + busy_timeout should prevent 'database is locked' errors");

        // Assert — all events persisted
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var count = await ctx.TimelineEvents.CountAsync(e => e.SessionId == sessionId);
        count.Should().Be(eventCount);
    }

    // ── 4. Write after finalize ──────────────────────────────────────

    [Fact]
    public async Task WriteAfterFinalize_DoesNotThrow()
    {
        // Arrange — start and finalize a session, then attempt to write more
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-post-final";

        await sut.StartSessionAsync(sessionId, null, null, null, null, null);
        await sut.FinalizeSessionAsync(sessionId);

        // Act — write a chat message after finalization (fire-and-forget resilience)
        var ex = await Record.ExceptionAsync(() =>
            sut.PersistChatMessageAsync(sessionId, new ChatMessage
            {
                Id = "msg-post-final",
                Role = MessageRole.User,
                Intent = MessageIntent.GeneralChat,
                Content = "Message after finalize",
                Timestamp = DateTime.UtcNow
            }));

        // Assert — should not throw (fire-and-forget safe)
        ex.Should().BeNull("persistence layer must never throw, even after session finalization");

        // The message may or may not be persisted (depending on whether the
        // session gate is still satisfied), but the important thing is no crash.
    }

    [Fact]
    public async Task WriteAfterFinalize_EventDoesNotThrow()
    {
        // Same test but for timeline events
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-post-final-evt";

        await sut.StartSessionAsync(sessionId, null, null, null, null, null);
        await sut.FinalizeSessionAsync(sessionId);

        // Act
        var ex = await Record.ExceptionAsync(() =>
            sut.PersistTimelineEventAsync(sessionId,
                new TimelineEvent
                {
                    Id = "evt-post-final",
                    Type = EventOutputType.Assessment,
                    Summary = "Event after finalize",
                    Timestamp = DateTime.UtcNow
                },
                checkpointId: null,
                displayOrder: 0));

        // Assert
        ex.Should().BeNull("timeline event persistence must be fire-and-forget safe after finalize");
    }

    // ── 5. Anchor lookup with wrong session ──────────────────────────

    [Fact]
    public async Task AnchorLookup_WrongSession_ReturnsNull()
    {
        // Arrange — create events in two separate sessions
        const string sessionA = "sess-anchor-a";
        const string sessionB = "sess-anchor-b";

        var sut = new SessionHistoryService(_dbPath);
        await sut.StartSessionAsync(sessionA, "leroy", "chess", null, null, null);
        await sut.StartSessionAsync(sessionB, "wasp", "chess", null, null, null);

        await sut.PersistTimelineEventAsync(sessionA,
            new TimelineEvent
            {
                Id = "evt-in-session-a",
                Type = EventOutputType.Danger,
                Summary = "Knight fork in session A",
                Timestamp = DateTime.UtcNow
            }, null, 0);

        await sut.PersistTimelineEventAsync(sessionB,
            new TimelineEvent
            {
                Id = "evt-in-session-b",
                Type = EventOutputType.SageAdvice,
                Summary = "Develop pieces in session B",
                Timestamp = DateTime.UtcNow
            }, null, 0);

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var anchor = new ReplayAnchorService(_dbPath, retrieval);

        // Act — look up session A's event using session B's ID
        var result = await anchor.GetAroundEventAsync(sessionB, "evt-in-session-a", TimeSpan.FromSeconds(30));

        // Assert — should return null due to session ID mismatch
        result.Should().BeNull("event belongs to session A, not session B");
    }

    [Fact]
    public async Task AnchorLookup_CorrectSession_ReturnsContext()
    {
        // Arrange
        const string sessionA = "sess-anchor-correct";

        var sut = new SessionHistoryService(_dbPath);
        await sut.StartSessionAsync(sessionA, "leroy", "chess", null, null, null);

        await sut.PersistTimelineEventAsync(sessionA,
            new TimelineEvent
            {
                Id = "evt-anchor-correct",
                Type = EventOutputType.Danger,
                Summary = "Correct session event",
                Timestamp = DateTime.UtcNow
            }, null, 0);

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var anchor = new ReplayAnchorService(_dbPath, retrieval);

        // Act
        var result = await anchor.GetAroundEventAsync(sessionA, "evt-anchor-correct", TimeSpan.FromSeconds(30));

        // Assert
        result.Should().NotBeNull("event belongs to the requested session");
        result!.Items.Should().Contain(i => i.EventSummary == "Correct session event");
    }

    // ── 6. Replay retrieval with concurrent writes ───────────────────

    [Fact]
    public async Task ReplayRetrieval_WithConcurrentWrites_NoCrashOrDeadlock()
    {
        // Arrange — seed a session with some events, then read and write concurrently
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-concurrent-rw";
        const int writerCount = 10;
        const int readerCount = 5;

        await sut.StartSessionAsync(sessionId, "leroy", "chess", null, null, null);

        // Seed a few initial events so readers have something to find
        for (int i = 0; i < 5; i++)
        {
            await sut.PersistTimelineEventAsync(sessionId,
                new TimelineEvent
                {
                    Id = $"evt-seed-{i:D3}",
                    Type = EventOutputType.Assessment,
                    Summary = $"Seed event #{i}",
                    Timestamp = DateTime.UtcNow.AddSeconds(-10 + i)
                }, null, i);
        }

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);

        // Act — fire writers and readers concurrently
        var writers = Enumerable.Range(0, writerCount).Select(i => Task.Run(async () =>
        {
            await sut.PersistChatMessageAsync(sessionId, new ChatMessage
            {
                Id = $"msg-rw-{i:D3}",
                Role = MessageRole.User,
                Intent = MessageIntent.GeneralChat,
                Content = $"Concurrent write #{i}",
                Timestamp = DateTime.UtcNow
            });
        }));

        var readers = Enumerable.Range(0, readerCount).Select(_ => Task.Run(async () =>
        {
            var result = await retrieval.GetRecentAsync(sessionId, TimeSpan.FromMinutes(1));
            return result;
        }));

        // Neither readers nor writers should crash or deadlock
        var allTasks = writers.Concat(readers.Select(async r => { await r; })).ToArray();
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(allTasks));
        ex.Should().BeNull("concurrent reads and writes should not crash or deadlock");
    }

    // ── 7. Session-ready gate timeout ────────────────────────────────

    [Fact]
    public async Task SessionGateTimeout_ChildWriteDoesNotHangForever()
    {
        // Arrange — create a service but NEVER call StartSessionAsync.
        // Child writes should hit the gate timeout (10s) and bail gracefully.
        var sut = new SessionHistoryService(_dbPath);

        var msg = new ChatMessage
        {
            Id = "msg-timeout-01",
            Role = MessageRole.User,
            Intent = MessageIntent.GeneralChat,
            Content = "Should timeout gracefully",
            Timestamp = DateTime.UtcNow
        };

        // Act — child write with no session gate should return quickly (no gate => immediate skip)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ex = await Record.ExceptionAsync(() =>
            sut.PersistChatMessageAsync("sess-never-started", msg));
        sw.Stop();

        // Assert — should not throw and should not hang
        ex.Should().BeNull("child write without session gate must not throw");
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "without a session gate, the write should bail immediately, not wait for timeout");

        // The message should NOT be persisted (no session row)
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var loaded = await ctx.ChatMessages.FindAsync("msg-timeout-01");
        loaded.Should().BeNull("write was skipped because no session was started");
    }

    // ── 8. Mixed concurrent writes: chat + checkpoint + event ────────

    [Fact]
    public async Task MixedConcurrentWrites_AllTypesPersist()
    {
        // Arrange — exercise all three child-write paths concurrently
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-mixed-write";

        var startTask = sut.StartSessionAsync(sessionId, "leroy", "chess", null, null, null);

        // Fire mixed writes immediately (gate should hold them)
        var chatTasks = Enumerable.Range(0, 5).Select(i =>
            sut.PersistChatMessageAsync(sessionId, new ChatMessage
            {
                Id = $"msg-mix-{i:D2}",
                Role = MessageRole.User,
                Content = $"Mixed chat #{i}",
                Timestamp = DateTime.UtcNow
            }));

        var eventTasks = Enumerable.Range(0, 5).Select(i =>
            sut.PersistTimelineEventAsync(sessionId,
                new TimelineEvent
                {
                    Id = $"evt-mix-{i:D2}",
                    Type = EventOutputType.Assessment,
                    Summary = $"Mixed event #{i}",
                    Timestamp = DateTime.UtcNow
                }, null, i));

        // Wait for everything
        await Task.WhenAll(
            chatTasks
                .Concat(eventTasks)
                .Prepend(startTask));

        // Assert — both types persisted
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var chatCount = await ctx.ChatMessages.CountAsync(m => m.SessionId == sessionId);
        var evtCount = await ctx.TimelineEvents.CountAsync(e => e.SessionId == sessionId);

        chatCount.Should().Be(5, "all chat messages should be persisted");
        evtCount.Should().Be(5, "all timeline events should be persisted");
    }

    // ── 9. Double finalize is idempotent ─────────────────────────────

    [Fact]
    public async Task DoubleFinalizeSession_IsIdempotent()
    {
        // Arrange
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-double-fin";

        await sut.StartSessionAsync(sessionId, null, null, null, null, null);

        // Act — finalize twice
        await sut.FinalizeSessionAsync(sessionId);
        var ex = await Record.ExceptionAsync(() => sut.FinalizeSessionAsync(sessionId));

        // Assert — no crash, EndedAtUtc still set
        ex.Should().BeNull("double finalize must not throw");

        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var session = await ctx.Sessions.FindAsync(sessionId);
        session.Should().NotBeNull();
        session!.EndedAtUtc.Should().NotBeNull();
    }

    // ── 10. Duplicate message ID is fire-and-forget safe ─────────────

    [Fact]
    public async Task DuplicateMessageId_DoesNotThrow()
    {
        // Arrange — insert a message, then try to insert another with the same ID
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-dup-msg";

        await sut.StartSessionAsync(sessionId, null, null, null, null, null);

        var msg = new ChatMessage
        {
            Id = "msg-duplicate",
            Role = MessageRole.User,
            Content = "First write",
            Timestamp = DateTime.UtcNow
        };

        await sut.PersistChatMessageAsync(sessionId, msg);

        // Act — duplicate ID write (PK conflict)
        var duplicate = new ChatMessage
        {
            Id = "msg-duplicate",
            Role = MessageRole.Assistant,
            Content = "Second write with same ID",
            Timestamp = DateTime.UtcNow
        };

        var ex = await Record.ExceptionAsync(() =>
            sut.PersistChatMessageAsync(sessionId, duplicate));

        // Assert — should not throw (fire-and-forget resilience)
        ex.Should().BeNull("duplicate PK should be swallowed by the exception handler");
    }

    // ── 11. Concurrent start + finalize race ─────────────────────────

    [Fact]
    public async Task ConcurrentStartAndFinalize_NoDeadlock()
    {
        // Arrange — fire start and finalize nearly simultaneously
        var sut = new SessionHistoryService(_dbPath);
        const string sessionId = "sess-start-fin-race";

        // Act — start and finalize in parallel
        var startTask = sut.StartSessionAsync(sessionId, "leroy", "chess", null, null, null);
        var finalizeTask = sut.FinalizeSessionAsync(sessionId);

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(startTask, finalizeTask));

        // Assert — should not throw or deadlock
        ex.Should().BeNull("concurrent start + finalize must not deadlock or throw");
    }

    // ── Shared fake observation store ────────────────────────────────

    private sealed class FakeObservationStore : IObservationStore
    {
        public Task<ObservationRecord> StoreAsync(ObservationWriteRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ObservationRecord>> GetRecentAsync(int count = 50, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ObservationRecord>> GetByTimeRangeAsync(
            string sessionId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ObservationRecord>>(Array.Empty<ObservationRecord>());
    }
}
