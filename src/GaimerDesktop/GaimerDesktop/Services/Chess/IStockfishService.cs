namespace GaimerDesktop.Services.Chess;

public interface IStockfishService : IDisposable
{
    bool IsReady { get; }
    bool IsInstalled { get; }
    Task<bool> EnsureInstalledAsync(IProgress<double>? progress = null, CancellationToken ct = default);
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    Task<EngineAnalysis> AnalyzePositionAsync(string fen, AnalysisOptions? options = null, CancellationToken ct = default);
}

public record AnalysisOptions(int MoveTimeMs = 1000, int MaxDepth = 25, int MultiPv = 1);

public record EngineAnalysis(
    string BestMove,
    string? PonderMove,
    int Depth,
    IReadOnlyList<EngineVariation> Variations,
    long Nodes,
    int TimeMs);

public record EngineVariation(
    int MultiPvIndex,
    string Move,
    int? CentipawnEval,
    int? MateIn,
    string[] PrincipalVariation,
    int Depth);
