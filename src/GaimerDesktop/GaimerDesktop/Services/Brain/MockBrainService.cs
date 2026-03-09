using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using GaimerDesktop.Models;

namespace GaimerDesktop.Services.Brain;

/// <summary>
/// Mock IBrainService for development and testing without an OpenRouter API key.
/// Produces demo BrainResults after simulated latency, matching the pattern of
/// MockConversationProvider, MockGhostModeService, and MockWindowCaptureService.
/// </summary>
public sealed class MockBrainService : IBrainService
{
    private readonly Channel<BrainResult> _channel;
    private readonly ILogger<MockBrainService> _logger;
    private CancellationTokenSource _cts = new();
    private int _activeTasks;
    private bool _disposed;

    private static readonly string[] MockAnalyses =
    [
        "Black's knight on f6 is well-placed but the bishop on c8 remains undeveloped. Consider fianchetto with g6-Bg7 to control the long diagonal.",
        "White has a slight space advantage on the queenside. The pawn structure suggests a Carlsbad formation — typical plans include minority attack with b4-b5.",
        "The position is roughly equal. Both sides have completed development. Key tension point is the d4-d5 pawn chain — look for pawn breaks with c5 or f5.",
        "Interesting sacrifice opportunity: Nxe5 followed by Qh5+ could expose the king. Calculate carefully — the knight is protected by the d-pawn.",
        "Endgame approaching. Your rook is more active than the opponent's. Centralize the king and push the passed a-pawn."
    ];

    private static readonly string[] MockVoiceNarrations =
    [
        "Bishop's still stuck at home. Time to fianchetto, maybe?",
        "Queenside looks promising. Classic minority attack setup here.",
        "Position's dead even. Look for a break with c5 or f5.",
        "Ooh, there might be a knight sacrifice on e5. Worth a look.",
        "Endgame time. Get that king to the center and push the a-pawn."
    ];

    private int _analysisIndex;

    public MockBrainService(ILogger<MockBrainService> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<BrainResult>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
        _logger.LogInformation("[MockBrain] Initialized — demo mode, no API key required");
    }

    public ChannelReader<BrainResult> Results => _channel.Reader;

    public bool IsBusy => Volatile.Read(ref _activeTasks) > 0;

    public string ProviderName => "Mock Brain";

    public Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        _logger.LogDebug("[MockBrain] Image submitted ({Bytes} bytes, correlation={Id})",
            imageData.Length, correlationId);

        _ = Task.Run(async () =>
        {
            Interlocked.Increment(ref _activeTasks);
            try
            {
                await Task.Delay(500, linkedCts.Token);

                var index = Interlocked.Increment(ref _analysisIndex) % MockAnalyses.Length;

                var result = new BrainResult
                {
                    Type = BrainResultType.ImageAnalysis,
                    AnalysisText = MockAnalyses[index],
                    VoiceNarration = MockVoiceNarrations[index],
                    Priority = BrainResultPriority.WhenIdle,
                    CorrelationId = correlationId,
                    Hint = new BrainHint
                    {
                        Signal = "position_analysis",
                        Urgency = "low",
                        Summary = $"Mock analysis of {imageData.Length} byte frame",
                        Evaluation = 15 // Slight white advantage (centipawns)
                    }
                };

                await _channel.Writer.WriteAsync(result, linkedCts.Token);
                _logger.LogDebug("[MockBrain] ImageAnalysis result written (correlation={Id})", correlationId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("[MockBrain] Image analysis cancelled (correlation={Id})", correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MockBrain] Error during image analysis (correlation={Id})", correlationId);
                TryWriteError(correlationId, ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
                linkedCts.Dispose();
            }
        }, linkedCts.Token);

        return Task.CompletedTask;
    }

    public Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        _logger.LogDebug("[MockBrain] Query submitted: \"{Query}\" (correlation={Id})",
            userQuery.Length > 50 ? userQuery[..50] + "..." : userQuery, correlationId);

        _ = Task.Run(async () =>
        {
            Interlocked.Increment(ref _activeTasks);
            try
            {
                await Task.Delay(300, linkedCts.Token);

                var result = new BrainResult
                {
                    Type = BrainResultType.ToolResult,
                    AnalysisText = $"[Mock Brain Response] You asked: \"{userQuery}\"\n\n" +
                                   "Based on the current game state, I'd recommend focusing on piece " +
                                   "development and king safety. The position calls for patient play " +
                                   "with gradual improvement of your worst-placed piece.",
                    VoiceNarration = "Good question. Focus on development and king safety for now.",
                    Priority = BrainResultPriority.WhenIdle,
                    CorrelationId = correlationId
                };

                await _channel.Writer.WriteAsync(result, linkedCts.Token);
                _logger.LogDebug("[MockBrain] ToolResult written (correlation={Id})", correlationId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("[MockBrain] Query cancelled (correlation={Id})", correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MockBrain] Error during query processing (correlation={Id})", correlationId);
                TryWriteError(correlationId, ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
                linkedCts.Dispose();
            }
        }, linkedCts.Token);

        return Task.CompletedTask;
    }

    public async Task<string> ChatAsync(string userQuery, IReadOnlyList<ChatMessage> chatHistory, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Task.Delay(300, ct);

        return $"[Mock Brain] You said: \"{userQuery}\"\n\n" +
               "I'd love to chat about strategy! Once we're connected to a game, " +
               "I can give you real-time analysis. For now, ask me anything about openings, " +
               "endgames, or general chess strategy.";
    }

    public void CancelAll()
    {
        _logger.LogInformation("[MockBrain] CancelAll — cancelling all in-flight requests");
        var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        try { oldCts.Cancel(); } catch { /* already disposed */ }
        oldCts.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("[MockBrain] Disposing");
        CancelAll();
        _channel.Writer.TryComplete();
        _cts.Dispose();
    }

    private void TryWriteError(string correlationId, string errorMessage)
    {
        var errorResult = new BrainResult
        {
            Type = BrainResultType.Error,
            AnalysisText = $"Mock brain error: {errorMessage}",
            Priority = BrainResultPriority.Silent,
            CorrelationId = correlationId
        };

        _channel.Writer.TryWrite(errorResult);
    }
}
