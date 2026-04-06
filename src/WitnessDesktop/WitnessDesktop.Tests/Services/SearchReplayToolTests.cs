using System.Text.Json;
using FluentAssertions;
using Moq;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Services.Replay;
using Xunit;

namespace WitnessDesktop.Tests.Services;

public class SearchReplayToolTests
{
    private readonly Mock<ISegmentAnalysisStore> _mockStore;
    private readonly Mock<IVideoAnalysisTool> _mockTool;
    private readonly Mock<IReplayRecordingService> _mockRecording;
    private readonly Mock<IGameSkillPackService> _mockPackService;
    private readonly Mock<IWindowCaptureService> _mockCapture;
    private readonly Mock<ISessionManager> _mockSession;
    private readonly Mock<IStockfishService> _mockStockfish;
    private readonly OpenRouterClient _client;
    private readonly ToolExecutor _sut;

    public SearchReplayToolTests()
    {
        _mockStore = new Mock<ISegmentAnalysisStore>();
        _mockTool = new Mock<IVideoAnalysisTool>();
        _mockRecording = new Mock<IReplayRecordingService>();
        _mockPackService = new Mock<IGameSkillPackService>();
        _mockCapture = new Mock<IWindowCaptureService>();
        _mockSession = new Mock<ISessionManager>();
        _mockStockfish = new Mock<IStockfishService>();

        var httpClient = new HttpClient();
        _client = new OpenRouterClient(httpClient, "test-key", "test-model");

        _sut = new ToolExecutor(
            _mockCapture.Object,
            _mockSession.Object,
            _client,
            _mockStockfish.Object,
            "test-model",
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<ToolExecutor>(),
            segmentAnalysisStore: _mockStore.Object,
            videoAnalysisTool: _mockTool.Object,
            replayRecording: _mockRecording.Object,
            packService: _mockPackService.Object);
    }

    private static string MakeArgs(string query, string? timeHint = null)
    {
        if (timeHint != null)
            return JsonSerializer.Serialize(new { query, time_hint = timeHint });
        return JsonSerializer.Serialize(new { query });
    }

    // --- Store Hit (Tier 1) ---

    [Fact]
    public async Task SearchReplay_StoreHit_ReturnsCachedResults()
    {
        _mockStore.Setup(s => s.SearchAsync("triple kill", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AnalyzedBeat>
            {
                new() { StartTime = "1:30", EndTime = "1:33", Assessment = "Player gets triple kill at B site" }
            });

        var result = await _sut.ExecuteToolAsync("search_replay", MakeArgs("triple kill"));
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("source").GetString().Should().Be("cached");
        doc.RootElement.GetProperty("matches").GetArrayLength().Should().Be(1);
    }

    // --- Store Miss + Segments Available (Tier 2) ---

    [Fact]
    public async Task SearchReplay_StoreMiss_SegmentsAvailable_RunsFreshAnalysis()
    {
        _mockStore.Setup(s => s.SearchAsync(It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AnalyzedBeat>());

        var segments = new List<ReplaySegment>
        {
            new() { FilePath = "/tmp/seg.mp4", SessionId = "s1", StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3), EndUtc = DateTimeOffset.UtcNow, SegmentIndex = 0 }
        };
        _mockRecording.Setup(r => r.GetAvailableSegments()).Returns(segments);
        _mockPackService.Setup(p => p.ActivePack).Returns(new GameSkillPack { Id = "chess", BrainInstructionsContent = "Analyze chess" });
        _mockTool.Setup(t => t.SearchAsync(It.IsAny<IReadOnlyList<ReplaySegment>>(), It.IsAny<GameSkillPack>(), "how did I die", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoSearchResult
            {
                Query = "how did I die",
                Hits = new List<SearchHit> { new() { StartTime = "0:45", EndTime = "0:48", SegmentFilePath = "/tmp/seg.mp4", Description = "Player eliminated by enemy flank" } },
                Summary = "You died at 0:45 to an enemy flanking from mid"
            });

        var result = await _sut.ExecuteToolAsync("search_replay", MakeArgs("how did I die"));
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("source").GetString().Should().Be("fresh_analysis");
        doc.RootElement.GetProperty("summary").GetString().Should().Contain("flanking");
    }

    // --- Store Miss + No Segments ---

    [Fact]
    public async Task SearchReplay_StoreMiss_NoSegments_ReturnsNoFootage()
    {
        _mockStore.Setup(s => s.SearchAsync(It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AnalyzedBeat>());
        _mockRecording.Setup(r => r.GetAvailableSegments()).Returns(new List<ReplaySegment>());

        var result = await _sut.ExecuteToolAsync("search_replay", MakeArgs("what happened"));
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("no_footage");
    }

    // --- Store Miss + No Pack ---

    [Fact]
    public async Task SearchReplay_StoreMiss_NoPack_ReturnsNoPack()
    {
        _mockStore.Setup(s => s.SearchAsync(It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AnalyzedBeat>());

        var segments = new List<ReplaySegment>
        {
            new() { FilePath = "/tmp/seg.mp4", SessionId = "s1", StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3), EndUtc = DateTimeOffset.UtcNow, SegmentIndex = 0 }
        };
        _mockRecording.Setup(r => r.GetAvailableSegments()).Returns(segments);
        _mockPackService.Setup(p => p.ActivePack).Returns((GameSkillPack?)null);

        var result = await _sut.ExecuteToolAsync("search_replay", MakeArgs("what happened"));
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("no_game_context");
    }

    // --- Missing Query ---

    [Fact]
    public async Task SearchReplay_MissingQuery_ReturnsError()
    {
        var result = await _sut.ExecuteToolAsync("search_replay", "{}");
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    // --- Fresh Analysis Failure ---

    [Fact]
    public async Task SearchReplay_FreshAnalysisFails_ReturnsError()
    {
        _mockStore.Setup(s => s.SearchAsync(It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AnalyzedBeat>());

        var segments = new List<ReplaySegment>
        {
            new() { FilePath = "/tmp/seg.mp4", SessionId = "s1", StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3), EndUtc = DateTimeOffset.UtcNow, SegmentIndex = 0 }
        };
        _mockRecording.Setup(r => r.GetAvailableSegments()).Returns(segments);
        _mockPackService.Setup(p => p.ActivePack).Returns(new GameSkillPack { Id = "chess", BrainInstructionsContent = "Analyze" });
        _mockTool.Setup(t => t.SearchAsync(It.IsAny<IReadOnlyList<ReplaySegment>>(), It.IsAny<GameSkillPack>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Gemini timeout"));

        var result = await _sut.ExecuteToolAsync("search_replay", MakeArgs("what happened"));
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("reason").GetString().Should().Contain("footage");
    }

    // --- Circuit Broken ---

    [Fact]
    public async Task SearchReplay_CircuitBroken_SkipsFreshAnalysis_ReturnsUnavailable()
    {
        _mockStore.Setup(s => s.SearchAsync(It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AnalyzedBeat>());

        var segments = new List<ReplaySegment>
        {
            new() { FilePath = "/tmp/seg.mp4", SessionId = "s1", StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3), EndUtc = DateTimeOffset.UtcNow, SegmentIndex = 0 }
        };
        _mockRecording.Setup(r => r.GetAvailableSegments()).Returns(segments);
        _mockPackService.Setup(p => p.ActivePack).Returns(new GameSkillPack { Id = "chess", BrainInstructionsContent = "Analyze" });
        _mockTool.Setup(t => t.IsCircuitBroken).Returns(true);

        var result = await _sut.ExecuteToolAsync("search_replay", MakeArgs("what happened"));
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("analysis_unavailable");
    }

    // --- Telemetry ---

    [Fact]
    public async Task SearchReplay_EmitsTraceEvent()
    {
        var mockTrace = new Mock<ISessionTraceService>();
        var httpClient = new HttpClient();
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");

        var sut = new ToolExecutor(
            _mockCapture.Object, _mockSession.Object, client,
            _mockStockfish.Object, "test-model",
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<ToolExecutor>(),
            sessionTrace: mockTrace.Object,
            segmentAnalysisStore: _mockStore.Object);

        _mockStore.Setup(s => s.SearchAsync(It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AnalyzedBeat>
            {
                new() { StartTime = "0:00", EndTime = "0:03", Assessment = "test" }
            });

        await sut.ExecuteToolAsync("search_replay", MakeArgs("test query"));

        mockTrace.Verify(t => t.TrackEvent("brain.tool_call", It.Is<Dictionary<string, string>>(
            d => d["tool_name"] == "search_replay")), Times.Once);
    }
}
