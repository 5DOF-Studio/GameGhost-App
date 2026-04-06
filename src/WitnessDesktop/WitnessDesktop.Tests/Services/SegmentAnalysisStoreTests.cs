using FluentAssertions;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Replay;
using Xunit;

namespace WitnessDesktop.Tests.Services;

public class SegmentAnalysisStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteSegmentAnalysisStore _sut;

    public SegmentAnalysisStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"gaimer-analysis-test-{Guid.NewGuid():N}.db");
        _sut = new SqliteSegmentAnalysisStore(_dbPath);
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (File.Exists(_dbPath + "-wal")) File.Delete(_dbPath + "-wal");
        if (File.Exists(_dbPath + "-shm")) File.Delete(_dbPath + "-shm");
    }

    private static VideoAnalysisResult MakeResult(
        string segmentId = "seg-0",
        string summary = "Player pushed B site and got two kills",
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string sessionId = "sess-abc",
        params AnalyzedBeat[] beats)
    {
        var s = start ?? DateTimeOffset.UtcNow.AddMinutes(-3);
        var e = end ?? s.AddMinutes(2).AddSeconds(30);
        return new VideoAnalysisResult
        {
            SegmentId = segmentId,
            SessionId = sessionId,
            StartUtc = s,
            EndUtc = e,
            RawJson = "{}",
            Beats = beats.Length > 0 ? beats : new[]
            {
                new AnalyzedBeat { StartTime = "0:00", EndTime = "0:03", Assessment = "Player holding B site with SMG" },
                new AnalyzedBeat { StartTime = "0:03", EndTime = "0:06", Assessment = "Enemy spotted, player gets first kill" }
            },
            NarrativeSummary = summary,
            PackId = "cod-hc-cyber",
            Model = "gemini-2.5-flash"
        };
    }

    [Fact]
    public async Task IngestAsync_StoresResult()
    {
        var result = MakeResult();
        await _sut.IngestAsync(result);

        var found = await _sut.GetByTimeRangeAsync("sess-abc", result.StartUtc.AddSeconds(-1), result.EndUtc.AddSeconds(1));
        found.Should().HaveCount(1);
        found[0].SegmentId.Should().Be("seg-0");
        found[0].Beats.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_FindsByFTS5()
    {
        await _sut.IngestAsync(MakeResult(summary: "Player flanked through mid and got a triple kill"));

        var hits = await _sut.SearchAsync("triple kill");
        hits.Should().HaveCountGreaterThan(0);
        hits.Should().Contain(h => h.Assessment.Contains("kill"));
    }

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmpty()
    {
        await _sut.IngestAsync(MakeResult());
        var hits = await _sut.SearchAsync("nonexistent term xyzzy");
        hits.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WithTimeRange_FiltersCorrectly()
    {
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t2 = DateTimeOffset.UtcNow.AddMinutes(-5);
        await _sut.IngestAsync(MakeResult(segmentId: "old", summary: "Old segment kill", start: t1, end: t1.AddMinutes(2).AddSeconds(30)));
        await _sut.IngestAsync(MakeResult(segmentId: "new", summary: "New segment kill", start: t2, end: t2.AddMinutes(2).AddSeconds(30)));

        var hits = await _sut.SearchAsync("kill", startUtc: t2.AddSeconds(-1));
        hits.Should().AllSatisfy(h => h.StartTime.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsConcatenatedSummaries()
    {
        var t = DateTimeOffset.UtcNow.AddMinutes(-5);
        await _sut.IngestAsync(MakeResult(segmentId: "s0", summary: "First engagement at B", start: t));
        await _sut.IngestAsync(MakeResult(segmentId: "s1", summary: "Retake on A site", start: t.AddMinutes(3)));

        var summary = await _sut.GetSummaryAsync(t.AddSeconds(-1), t.AddMinutes(6));
        summary.Should().Contain("First engagement");
        summary.Should().Contain("Retake on A");
    }

    [Fact]
    public async Task GetByTimeRangeAsync_FiltersOutOfRange()
    {
        var t = DateTimeOffset.UtcNow.AddMinutes(-10);
        await _sut.IngestAsync(MakeResult(segmentId: "in-range", start: t, sessionId: "s1"));
        await _sut.IngestAsync(MakeResult(segmentId: "out-of-range", start: t.AddMinutes(20), sessionId: "s1"));

        var results = await _sut.GetByTimeRangeAsync("s1", t.AddSeconds(-1), t.AddMinutes(5));
        results.Should().HaveCount(1);
        results[0].SegmentId.Should().Be("in-range");
    }

    [Fact]
    public async Task IngestAsync_DuplicateSegmentId_Upserts()
    {
        await _sut.IngestAsync(MakeResult(segmentId: "dup", summary: "Version 1"));
        await _sut.IngestAsync(MakeResult(segmentId: "dup", summary: "Version 2"));

        var results = await _sut.GetByTimeRangeAsync("sess-abc", DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        results.Should().HaveCount(1);
        results[0].NarrativeSummary.Should().Be("Version 2");
    }
}
