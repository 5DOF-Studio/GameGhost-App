using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Replay;

public interface ISegmentAnalysisStore
{
    Task IngestAsync(VideoAnalysisResult result, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyzedBeat>> SearchAsync(string query, DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null, CancellationToken ct = default);
    Task<string?> GetSummaryAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default);
    Task<IReadOnlyList<VideoAnalysisResult>> GetByTimeRangeAsync(string sessionId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default);
}
