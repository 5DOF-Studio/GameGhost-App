using FluentAssertions;
using Moq;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Replay;
using Xunit;

namespace WitnessDesktop.Tests.Services;

public class ReplayAnalysisOrchestratorTests : IDisposable
{
    private readonly Mock<IVideoAnalysisTool> _mockTool;
    private readonly Mock<ISegmentAnalysisStore> _mockStore;
    private readonly Mock<IGameSkillPackService> _mockPackService;
    private readonly Mock<ISessionTraceService> _mockTrace;
    private readonly ReplayAnalysisOrchestrator _sut;
    private readonly CancellationTokenSource _cts;

    public ReplayAnalysisOrchestratorTests()
    {
        _mockTool = new Mock<IVideoAnalysisTool>();
        _mockStore = new Mock<ISegmentAnalysisStore>();
        _mockPackService = new Mock<IGameSkillPackService>();
        _mockTrace = new Mock<ISessionTraceService>();
        _cts = new CancellationTokenSource();
        _sut = new ReplayAnalysisOrchestrator(_mockTool.Object, _mockStore.Object, _mockPackService.Object, _mockTrace.Object);
    }

    public void Dispose()
    {
        _sut.Stop();
        _cts.Dispose();
    }

    private static ReplaySegment MakeSegment(int index = 0) => new()
    {
        FilePath = $"/tmp/seg-{index}.mp4",
        SessionId = "sess-abc",
        StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
        EndUtc = DateTimeOffset.UtcNow,
        ByteSize = 90_000_000,
        SegmentIndex = index
    };

    private static GameSkillPack MakePack() => new()
    {
        Id = "chess",
        Name = "Chess",
        Genre = "strategy",
        BrainInstructionsContent = "Analyze chess."
    };

    [Fact]
    public async Task EnqueueSegment_AnalyzesWhenPackActive()
    {
        var pack = MakePack();
        _mockPackService.Setup(p => p.ActivePack).Returns(pack);
        _mockTool.Setup(t => t.AnalyzeAsync(It.IsAny<ReplaySegment>(), pack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoAnalysisResult
            {
                SegmentId = "seg-0",
                StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
                EndUtc = DateTimeOffset.UtcNow,
                RawJson = "{}",
                Beats = [],
                NarrativeSummary = "Test summary"
            });

        _sut.Start(_cts.Token);
        _sut.EnqueueSegment(MakeSegment());
        await Task.Delay(500); // Allow consumer to process
        _sut.Stop();

        _mockTool.Verify(t => t.AnalyzeAsync(It.IsAny<ReplaySegment>(), pack, It.IsAny<CancellationToken>()), Times.Once);
        _mockStore.Verify(s => s.IngestAsync(It.IsAny<VideoAnalysisResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueSegment_SkipsWhenNoPackActive()
    {
        _mockPackService.Setup(p => p.ActivePack).Returns((GameSkillPack?)null);

        _sut.Start(_cts.Token);
        _sut.EnqueueSegment(MakeSegment());
        await Task.Delay(300);
        _sut.Stop();

        _mockTool.Verify(t => t.AnalyzeAsync(It.IsAny<ReplaySegment>(), It.IsAny<GameSkillPack>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnqueueSegment_ContinuesAfterAnalysisFailure()
    {
        var pack = MakePack();
        _mockPackService.Setup(p => p.ActivePack).Returns(pack);

        var callCount = 0;
        _mockTool.Setup(t => t.AnalyzeAsync(It.IsAny<ReplaySegment>(), pack, It.IsAny<CancellationToken>()))
            .Returns<ReplaySegment, GameSkillPack, CancellationToken>((seg, _, _) =>
            {
                callCount++;
                if (callCount == 1) throw new TimeoutException("Gemini timeout");
                return Task.FromResult(new VideoAnalysisResult
                {
                    SegmentId = "seg-1",
                    StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
                    EndUtc = DateTimeOffset.UtcNow,
                    RawJson = "{}",
                    Beats = [],
                    NarrativeSummary = "Recovered"
                });
            });

        _sut.Start(_cts.Token);
        _sut.EnqueueSegment(MakeSegment(0)); // This will fail
        _sut.EnqueueSegment(MakeSegment(1)); // This should succeed
        await Task.Delay(500);
        _sut.Stop();

        _mockStore.Verify(s => s.IngestAsync(It.IsAny<VideoAnalysisResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueSegment_DropsOldestWhenQueueFull()
    {
        var pack = MakePack();
        _mockPackService.Setup(p => p.ActivePack).Returns(pack);

        // Make analysis very slow so queue fills up
        _mockTool.Setup(t => t.AnalyzeAsync(It.IsAny<ReplaySegment>(), pack, It.IsAny<CancellationToken>()))
            .Returns(async (ReplaySegment seg, GameSkillPack _, CancellationToken ct) =>
            {
                await Task.Delay(2000, ct);
                return new VideoAnalysisResult
                {
                    SegmentId = $"seg-{seg.SegmentIndex}",
                    StartUtc = seg.StartUtc,
                    EndUtc = seg.EndUtc,
                    RawJson = "{}",
                    Beats = [],
                    NarrativeSummary = "Slow"
                };
            });

        _sut.Start(_cts.Token);

        // Enqueue 5 segments rapidly — channel(2) should drop oldest
        for (int i = 0; i < 5; i++)
            _sut.EnqueueSegment(MakeSegment(i));

        await Task.Delay(300);
        _sut.Stop();

        // Not all 5 should have been analyzed — some dropped
        _mockTool.Verify(t => t.AnalyzeAsync(It.IsAny<ReplaySegment>(), pack, It.IsAny<CancellationToken>()), Times.AtMost(3));
    }

    [Fact]
    public async Task Stop_CancelsInFlightWork()
    {
        var pack = MakePack();
        _mockPackService.Setup(p => p.ActivePack).Returns(pack);
        _mockTool.Setup(t => t.AnalyzeAsync(It.IsAny<ReplaySegment>(), pack, It.IsAny<CancellationToken>()))
            .Returns(async (ReplaySegment _, GameSkillPack _, CancellationToken ct) =>
            {
                await Task.Delay(10000, ct); // Very long — should be cancelled
                return new VideoAnalysisResult
                {
                    SegmentId = "x", StartUtc = default, EndUtc = default,
                    RawJson = "{}", Beats = [], NarrativeSummary = ""
                };
            });

        _sut.Start(_cts.Token);
        _sut.EnqueueSegment(MakeSegment());
        await Task.Delay(200); // Let consumer start
        _sut.Stop(); // Should cancel

        // Should not hang — test completes within timeout
    }

    [Fact]
    public async Task EnqueueSegment_EmitsTraceOnSuccess()
    {
        var pack = MakePack();
        _mockPackService.Setup(p => p.ActivePack).Returns(pack);
        _mockTool.Setup(t => t.AnalyzeAsync(It.IsAny<ReplaySegment>(), pack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoAnalysisResult
            {
                SegmentId = "seg-0", StartUtc = default, EndUtc = default,
                RawJson = "{}", Beats = new[] { new AnalyzedBeat { StartTime = "0:00", EndTime = "0:03", Assessment = "test" } },
                NarrativeSummary = "Test"
            });

        _sut.Start(_cts.Token);
        _sut.EnqueueSegment(MakeSegment());
        await Task.Delay(500);
        _sut.Stop();

        _mockTrace.Verify(t => t.TrackEvent("replay.analysis.completed", It.Is<Dictionary<string, string>>(
            d => d.ContainsKey("segmentIndex") && d.ContainsKey("beatCount"))), Times.Once);
    }
}
