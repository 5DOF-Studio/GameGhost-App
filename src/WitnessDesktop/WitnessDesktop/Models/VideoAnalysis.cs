namespace WitnessDesktop.Models;

public record VideoAnalysisResult
{
    public required string SegmentId { get; init; }
    public string? SessionId { get; init; }
    public required DateTimeOffset StartUtc { get; init; }
    public required DateTimeOffset EndUtc { get; init; }
    public required string RawJson { get; init; }
    public required IReadOnlyList<AnalyzedBeat> Beats { get; init; }
    public required string NarrativeSummary { get; init; }
    public string? PackId { get; init; }
    public string? Model { get; init; }
}

public record AnalyzedBeat
{
    public required string StartTime { get; init; }
    public required string EndTime { get; init; }
    public string Signal { get; init; } = "none";
    public string Urgency { get; init; } = "low";
    public required string Assessment { get; init; }
    public string? TemporalContext { get; init; }
}

public record VideoSearchResult
{
    public required string Query { get; init; }
    public required IReadOnlyList<SearchHit> Hits { get; init; }
    public required string Summary { get; init; }
}

public record SearchHit
{
    public required string StartTime { get; init; }
    public required string EndTime { get; init; }
    public required string SegmentFilePath { get; init; }
    public required string Description { get; init; }
    public string Confidence { get; init; } = "LIKELY";
}
