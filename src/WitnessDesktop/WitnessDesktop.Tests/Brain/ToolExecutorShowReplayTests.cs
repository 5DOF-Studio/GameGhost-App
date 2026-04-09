using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Services.Replay;
using WitnessDesktop.Tests.Helpers;
using Xunit;

namespace WitnessDesktop.Tests.Brain;

public class ToolExecutorShowReplayTests
{
    // ── Segment factory helper ──────────────────────────────────────────

    private static ReplaySegment MakeSegment(
        int index, int startOffsetSec, int endOffsetSec,
        DateTimeOffset sessionStart, string sessionId = "test-session")
    {
        return new ReplaySegment
        {
            FilePath = $"/tmp/replays/{sessionId}/segment-{index}.mp4",
            SessionId = sessionId,
            StartUtc = sessionStart.AddSeconds(startOffsetSec),
            EndUtc = sessionStart.AddSeconds(endOffsetSec),
            ByteSize = 10_000_000,
            SegmentIndex = index
        };
    }

    // ── Absolute timestamp "M:SS" ───────────────────────────────────────

    [Theory]
    [InlineData("2:15", 135.0)]     // 2 min 15 sec = 135s
    [InlineData("0:30", 30.0)]      // 30 seconds
    [InlineData("9:59", 599.0)]     // final in-range second
    [InlineData("0:05", 5.0)]       // 5 seconds
    public void ResolveTimestamp_AbsoluteFormat_ParsesSessionRelativeSeconds(
        string timestamp, double expectedSessionSeconds)
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        var segments = new[]
        {
            MakeSegment(0, 0, 150, sessionStart),
            MakeSegment(1, 150, 300, sessionStart),
            MakeSegment(2, 300, 450, sessionStart),
            MakeSegment(3, 450, 600, sessionStart),
        };

        var result = ToolExecutor.ResolveTimestamp(timestamp, segments, sessionStart);

        result.Should().NotBeNull();
        var (filePath, seekOffset, _) = result!.Value;
        // The absolute position within the session should map to the correct segment
        var expectedSegmentStart = segments
            .First(s => (s.StartUtc - sessionStart).TotalSeconds <= expectedSessionSeconds
                     && (s.EndUtc - sessionStart).TotalSeconds >= expectedSessionSeconds);
        filePath.Should().Be(expectedSegmentStart.FilePath);
        seekOffset.Should().BeApproximately(
            expectedSessionSeconds - (expectedSegmentStart.StartUtc - sessionStart).TotalSeconds, 0.1);
    }

    [Fact]
    public void ResolveTimestamp_AbsoluteFormat_WithDuration_ClampsDurationToSegmentBoundary()
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        // Single segment: 0s-150s
        var segments = new[] { MakeSegment(0, 0, 150, sessionStart) };

        // Request starts at 2:00 (120s) with 60s duration = would extend to 180s, past segment end
        var result = ToolExecutor.ResolveTimestamp("2:00", segments, sessionStart, requestedDuration: 60);

        result.Should().NotBeNull();
        var (_, seekOffset, clampedDuration) = result!.Value;
        seekOffset.Should().BeApproximately(120.0, 0.1);
        clampedDuration.Should().BeApproximately(30.0, 0.1); // Clamped to segment end (150 - 120 = 30)
    }

    [Fact]
    public void ResolveTimestamp_AbsoluteFormat_OnExactBoundary_SelectsLaterSegment()
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        var segments = new[]
        {
            MakeSegment(0, 0, 150, sessionStart),
            MakeSegment(1, 150, 300, sessionStart),
        };

        var result = ToolExecutor.ResolveTimestamp("2:30", segments, sessionStart, requestedDuration: 30);

        result.Should().NotBeNull();
        var (filePath, seekOffset, clampedDuration) = result!.Value;
        filePath.Should().Be(segments[1].FilePath);
        seekOffset.Should().BeApproximately(0.0, 0.1);
        clampedDuration.Should().BeApproximately(30.0, 0.1);
    }

    // ── Relative timestamp "now-Ns" ─────────────────────────────────────

    [Theory]
    [InlineData("now-30s", 30)]
    [InlineData("now-60s", 60)]
    [InlineData("now-10s", 10)]
    public void ResolveTimestamp_RelativeFormat_ResolvesFromCurrentSessionTime(
        string timestamp, int offsetSeconds)
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        // Session has been running for 300 seconds
        var segments = new[]
        {
            MakeSegment(0, 0, 150, sessionStart),
            MakeSegment(1, 150, 300, sessionStart),
        };

        // "now" is 300s into the session
        var result = ToolExecutor.ResolveTimestamp(
            timestamp, segments, sessionStart,
            nowUtc: sessionStart.AddSeconds(300));

        result.Should().NotBeNull();
        var expectedSessionSeconds = 300.0 - offsetSeconds;
        var expectedSegment = segments
            .First(s => (s.StartUtc - sessionStart).TotalSeconds <= expectedSessionSeconds
                     && (s.EndUtc - sessionStart).TotalSeconds >= expectedSessionSeconds);
        result!.Value.filePath.Should().Be(expectedSegment.FilePath);
    }

    // ── Anchor timestamp ────────────────────────────────────────────────

    [Fact]
    public async Task ResolveTimestamp_AnchorFormat_QueriesAnalysisStore()
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        var segments = new[]
        {
            MakeSegment(0, 0, 150, sessionStart),
            MakeSegment(1, 150, 300, sessionStart),
        };

        var mockStore = new Mock<ISegmentAnalysisStore>();
        mockStore.Setup(s => s.SearchAsync("last_kill", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AnalyzedBeat>
            {
                new AnalyzedBeat
                {
                    StartTime = "00:03:45",
                    EndTime = "00:03:55",
                    Assessment = "Player got a kill",
                    Signal = "opportunity"
                }
        });

        // "last_kill" anchor: store returns beat at 3:45 = 225s into session
        var result = await ToolExecutor.ResolveTimestampWithAnchorAsync(
            "last_kill", segments, sessionStart, mockStore.Object, 30, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.filePath.Should().Be(segments[1].FilePath); // 225s is in segment 1 (150-300)
        result!.Value.seekOffset.Should().BeApproximately(75.0, 0.1); // 225 - 150 = 75
    }

    [Fact]
    public async Task ResolveTimestamp_AnchorFormat_SelectsChronologicallyLatestBeat_AndHonorsRequestedDuration()
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        var segments = new[]
        {
            MakeSegment(0, 0, 150, sessionStart),
            MakeSegment(1, 150, 300, sessionStart),
        };

        var mockStore = new Mock<ISegmentAnalysisStore>();
        mockStore.Setup(s => s.SearchAsync("last_kill", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AnalyzedBeat>
            {
                new()
                {
                    StartTime = "00:03:45",
                    EndTime = "00:03:55",
                    Assessment = "Later match"
                },
                new()
                {
                    StartTime = "00:01:15",
                    EndTime = "00:01:20",
                    Assessment = "Earlier match"
                }
            });

        var result = await ToolExecutor.ResolveTimestampWithAnchorAsync(
            "last_kill", segments, sessionStart, mockStore.Object, 17, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.filePath.Should().Be(segments[1].FilePath);
        result!.Value.seekOffset.Should().BeApproximately(75.0, 0.1);
        result!.Value.clampedDuration.Should().BeApproximately(17.0, 0.1);
    }

    // ── Edge cases ──────────────────────────────────────────────────────

    [Fact]
    public void ResolveTimestamp_NoSegments_ReturnsNull()
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        var result = ToolExecutor.ResolveTimestamp(
            "1:00", Array.Empty<ReplaySegment>(), sessionStart);
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveTimestamp_TimestampBeforeAnySegment_ReturnsNull()
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        // Segment starts at 150s
        var segments = new[] { MakeSegment(1, 150, 300, sessionStart) };

        var result = ToolExecutor.ResolveTimestamp("0:30", segments, sessionStart);
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveTimestamp_TimestampAfterAllSegments_ReturnsNull()
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        var segments = new[] { MakeSegment(0, 0, 150, sessionStart) };

        var result = ToolExecutor.ResolveTimestamp("5:00", segments, sessionStart);
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveTimestamp_InvalidFormat_ReturnsNull()
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        var segments = new[] { MakeSegment(0, 0, 150, sessionStart) };

        var result = ToolExecutor.ResolveTimestamp("garbage", segments, sessionStart);
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveTimestamp_DurationClampedToMax60()
    {
        var sessionStart = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
        var segments = new[] { MakeSegment(0, 0, 600, sessionStart) };

        var result = ToolExecutor.ResolveTimestamp(
            "1:00", segments, sessionStart, requestedDuration: 120);

        result.Should().NotBeNull();
        result!.Value.clampedDuration.Should().BeLessOrEqualTo(60.0);
    }

    // ── ExecuteShowReplayAsync integration tests ────────────────────────

    private static ToolExecutor CreateSutWithReplay(
        IReplayRecordingService? replayRecording = null,
        ISegmentAnalysisStore? analysisStore = null,
        ISessionManager? sessionManager = null)
    {
        var mockCapture = new Mock<IWindowCaptureService>();
        var mockSession = sessionManager != null
            ? null
            : new Mock<ISessionManager>();

        if (mockSession != null)
        {
            mockSession.Setup(s => s.Context).Returns(new SessionContext
            {
                State = SessionState.InGame,
                GameId = "test-game-1",
                GameType = "chess",
                GameStartedAt = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc)
            });
        }

        var openRouterHttpClient = new HttpClient(new MockHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            })));
        var openRouterClient = new OpenRouterClient(openRouterHttpClient, "test-key", "test-model");
        var mockLogger = new Mock<ILogger<ToolExecutor>>();

        return new ToolExecutor(
            mockCapture.Object,
            sessionManager ?? mockSession!.Object,
            openRouterClient,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            mockLogger.Object,
            replayRecording: replayRecording,
            segmentAnalysisStore: analysisStore);
    }

    [Fact]
    public async Task ExecuteShowReplay_ValidTimestamp_ReturnsSuccessWithFilePath()
    {
        var sessionStart = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc);
        var mockReplay = new Mock<IReplayRecordingService>();
        mockReplay.Setup(r => r.IsRecording).Returns(true);
        mockReplay.Setup(r => r.GetAvailableSegments()).Returns(new[]
        {
            new ReplaySegment
            {
                FilePath = "/tmp/replays/test/segment-0.mp4",
                SessionId = "test-game-1",
                StartUtc = new DateTimeOffset(sessionStart),
                EndUtc = new DateTimeOffset(sessionStart.AddSeconds(150)),
                ByteSize = 10_000_000,
                SegmentIndex = 0
            }
        });

        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-game-1",
            GameType = "chess",
            GameStartedAt = sessionStart
        });

        var sut = CreateSutWithReplay(
            replayRecording: mockReplay.Object,
            sessionManager: mockSession.Object);

        var args = JsonSerializer.Serialize(new { timestamp = "1:00", duration = 30, title = "NICE MOVE" });
        var result = await sut.ExecuteToolAsync("show_replay", args);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("filePath").GetString().Should().Be("/tmp/replays/test/segment-0.mp4");
        doc.RootElement.GetProperty("startTime").GetDouble().Should().BeApproximately(60.0, 0.1);
        doc.RootElement.GetProperty("duration").GetDouble().Should().BeApproximately(30.0, 0.1);
        doc.RootElement.GetProperty("title").GetString().Should().Be("NICE MOVE");
    }

    [Fact]
    public async Task ExecuteShowReplay_ExactBoundary_UsesLaterSegment()
    {
        var sessionStart = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc);
        var mockReplay = new Mock<IReplayRecordingService>();
        mockReplay.Setup(r => r.IsRecording).Returns(true);
        mockReplay.Setup(r => r.GetAvailableSegments()).Returns(new[]
        {
            new ReplaySegment
            {
                FilePath = "/tmp/replays/test/segment-0.mp4",
                SessionId = "test-game-1",
                StartUtc = new DateTimeOffset(sessionStart),
                EndUtc = new DateTimeOffset(sessionStart.AddSeconds(150)),
                ByteSize = 10_000_000,
                SegmentIndex = 0
            },
            new ReplaySegment
            {
                FilePath = "/tmp/replays/test/segment-1.mp4",
                SessionId = "test-game-1",
                StartUtc = new DateTimeOffset(sessionStart.AddSeconds(150)),
                EndUtc = new DateTimeOffset(sessionStart.AddSeconds(300)),
                ByteSize = 10_000_000,
                SegmentIndex = 1
            }
        });

        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-game-1",
            GameType = "chess",
            GameStartedAt = sessionStart
        });

        var sut = CreateSutWithReplay(
            replayRecording: mockReplay.Object,
            sessionManager: mockSession.Object);

        var args = JsonSerializer.Serialize(new { timestamp = "2:30", duration = 30 });
        var result = await sut.ExecuteToolAsync("show_replay", args);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("filePath").GetString().Should().Be("/tmp/replays/test/segment-1.mp4");
        doc.RootElement.GetProperty("startTime").GetDouble().Should().BeApproximately(0.0, 0.1);
        doc.RootElement.GetProperty("duration").GetDouble().Should().BeApproximately(30.0, 0.1);
    }

    [Fact]
    public async Task ExecuteShowReplay_NotRecording_ReturnsError()
    {
        var mockReplay = new Mock<IReplayRecordingService>();
        mockReplay.Setup(r => r.IsRecording).Returns(false);

        var sut = CreateSutWithReplay(replayRecording: mockReplay.Object);

        var args = JsonSerializer.Serialize(new { timestamp = "1:00" });
        var result = await sut.ExecuteToolAsync("show_replay", args);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("no_footage");
    }

    [Fact]
    public async Task ExecuteShowReplay_NoReplayService_ReturnsError()
    {
        var sut = CreateSutWithReplay(replayRecording: null);

        var args = JsonSerializer.Serialize(new { timestamp = "1:00" });
        var result = await sut.ExecuteToolAsync("show_replay", args);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("no_footage");
    }

    [Fact]
    public async Task ExecuteShowReplay_TimestampNotInSegment_ReturnsError()
    {
        var sessionStart = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc);
        var mockReplay = new Mock<IReplayRecordingService>();
        mockReplay.Setup(r => r.IsRecording).Returns(true);
        mockReplay.Setup(r => r.GetAvailableSegments()).Returns(new[]
        {
            new ReplaySegment
            {
                FilePath = "/tmp/replays/test/segment-0.mp4",
                SessionId = "test-game-1",
                StartUtc = new DateTimeOffset(sessionStart),
                EndUtc = new DateTimeOffset(sessionStart.AddSeconds(150)),
                ByteSize = 10_000_000,
                SegmentIndex = 0
            }
        });

        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-game-1",
            GameType = "chess",
            GameStartedAt = sessionStart
        });

        var sut = CreateSutWithReplay(
            replayRecording: mockReplay.Object,
            sessionManager: mockSession.Object);

        // 10:00 = 600s, past the segment's 150s end
        var args = JsonSerializer.Serialize(new { timestamp = "10:00" });
        var result = await sut.ExecuteToolAsync("show_replay", args);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task ExecuteShowReplay_MissingTimestamp_ReturnsError()
    {
        var mockReplay = new Mock<IReplayRecordingService>();
        mockReplay.Setup(r => r.IsRecording).Returns(true);
        mockReplay.Setup(r => r.GetAvailableSegments()).Returns(Array.Empty<ReplaySegment>());

        var sut = CreateSutWithReplay(replayRecording: mockReplay.Object);

        var args = JsonSerializer.Serialize(new { duration = 30 }); // no timestamp
        var result = await sut.ExecuteToolAsync("show_replay", args);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    [Fact]
    public async Task ExecuteShowReplay_DefaultDuration_Is30()
    {
        var sessionStart = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc);
        var mockReplay = new Mock<IReplayRecordingService>();
        mockReplay.Setup(r => r.IsRecording).Returns(true);
        mockReplay.Setup(r => r.GetAvailableSegments()).Returns(new[]
        {
            new ReplaySegment
            {
                FilePath = "/tmp/replays/test/segment-0.mp4",
                SessionId = "test-game-1",
                StartUtc = new DateTimeOffset(sessionStart),
                EndUtc = new DateTimeOffset(sessionStart.AddSeconds(600)),
                ByteSize = 10_000_000,
                SegmentIndex = 0
            }
        });

        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-game-1",
            GameType = "chess",
            GameStartedAt = sessionStart
        });

        var sut = CreateSutWithReplay(
            replayRecording: mockReplay.Object,
            sessionManager: mockSession.Object);

        var args = JsonSerializer.Serialize(new { timestamp = "1:00" }); // no duration specified
        var result = await sut.ExecuteToolAsync("show_replay", args);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("duration").GetDouble().Should().BeApproximately(30.0, 0.1);
    }
}
