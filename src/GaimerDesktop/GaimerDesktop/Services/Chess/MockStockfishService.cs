namespace GaimerDesktop.Services.Chess;

/// <summary>
/// Mock Stockfish service for development and testing.
/// Returns deterministic canned results — no process spawning, no network.
/// </summary>
public sealed class MockStockfishService : IStockfishService
{
    private static readonly Dictionary<string, EngineAnalysis> CannedResults = new()
    {
        // Standard starting position — e2e4 with slight advantage
        ["rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"] = new EngineAnalysis(
            BestMove: "e2e4",
            PonderMove: "e7e5",
            Depth: 20,
            Variations: new List<EngineVariation>
            {
                new(MultiPvIndex: 1, Move: "e2e4", CentipawnEval: 30, MateIn: null,
                    PrincipalVariation: new[] { "e2e4", "e7e5", "g1f3", "b8c6" }, Depth: 20)
            }.AsReadOnly(),
            Nodes: 1_500_000,
            TimeMs: 350),

        // Sicilian Defense position
        ["rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2"] = new EngineAnalysis(
            BestMove: "g1f3",
            PonderMove: "d7d6",
            Depth: 22,
            Variations: new List<EngineVariation>
            {
                new(MultiPvIndex: 1, Move: "g1f3", CentipawnEval: 45, MateIn: null,
                    PrincipalVariation: new[] { "g1f3", "d7d6", "d2d4", "c5d4", "f3d4" }, Depth: 22)
            }.AsReadOnly(),
            Nodes: 2_000_000,
            TimeMs: 500),

        // Scholar's mate threat (mate in 1 for white)
        ["r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4"] = new EngineAnalysis(
            BestMove: "h5f7",
            PonderMove: null,
            Depth: 25,
            Variations: new List<EngineVariation>
            {
                new(MultiPvIndex: 1, Move: "h5f7", CentipawnEval: null, MateIn: 1,
                    PrincipalVariation: new[] { "h5f7" }, Depth: 25)
            }.AsReadOnly(),
            Nodes: 50_000,
            TimeMs: 10),
    };

    // Default fallback for unknown positions
    private static readonly EngineAnalysis DefaultAnalysis = new(
        BestMove: "e2e4",
        PonderMove: "e7e5",
        Depth: 18,
        Variations: new List<EngineVariation>
        {
            new(MultiPvIndex: 1, Move: "e2e4", CentipawnEval: 30, MateIn: null,
                PrincipalVariation: new[] { "e2e4", "e7e5" }, Depth: 18)
        }.AsReadOnly(),
        Nodes: 1_000_000,
        TimeMs: 250);

    public bool IsReady { get; private set; }
    public bool IsInstalled => true;

    public Task<bool> EnsureInstalledAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(1.0);
        return Task.FromResult(true);
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        IsReady = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsReady = false;
        return Task.CompletedTask;
    }

    public Task<EngineAnalysis> AnalyzePositionAsync(
        string fen,
        AnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        if (!IsReady)
            throw new InvalidOperationException("Engine is not ready. Call StartAsync first.");

        if (!FenValidator.IsValid(fen, out var errors))
            throw new ArgumentException($"Invalid FEN: {string.Join("; ", errors)}", nameof(fen));

        ct.ThrowIfCancellationRequested();

        var result = CannedResults.TryGetValue(fen, out var canned) ? canned : DefaultAnalysis;
        return Task.FromResult(result);
    }

    public void Dispose() { }
}
