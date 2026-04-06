using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Replay;

public interface IVideoAnalysisTool
{
    Task<VideoAnalysisResult> AnalyzeAsync(ReplaySegment segment, GameSkillPack pack, CancellationToken ct = default);
    Task<VideoSearchResult> SearchAsync(IReadOnlyList<ReplaySegment> segments, GameSkillPack pack, string query, CancellationToken ct = default);
    bool IsCircuitBroken { get; }
}
