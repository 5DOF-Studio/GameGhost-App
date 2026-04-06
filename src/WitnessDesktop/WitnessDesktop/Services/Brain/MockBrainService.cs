using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Brain;

/// <summary>
/// Mock IBrainService for development and testing without an OpenRouter API key.
/// Produces demo BrainResults after simulated latency, matching the pattern of
/// MockConversationProvider, MockGhostModeService, and MockWindowCaptureService.
///
/// Frame submission uses the same Channel(1, DropOldest) "frame slot" pattern as
/// OpenRouterBrainService for consistent behavior in dev/test scenarios.
/// </summary>
public sealed class MockBrainService : IBrainService
{
    private readonly Channel<BrainResult> _channel;
    private readonly Channel<FrameSubmission> _frameSlot;
    private readonly Task _consumerTask;
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
        _frameSlot = Channel.CreateBounded<FrameSubmission>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        _consumerTask = Task.Run(ConsumeFramesAsync);
        _logger.LogInformation("[MockBrain] Initialized — demo mode, no API key required");
    }

    public ChannelReader<BrainResult> Results => _channel.Reader;

    public bool IsBusy => Volatile.Read(ref _activeTasks) > 0;

    public string ProviderName => "Mock Brain";

    // ── Frame Slot (Channel-based) ──────────────────────────────────────────

    /// <inheritdoc />
    public bool TrySubmitFrame(byte[] imageData, string context)
    {
        var submission = new FrameSubmission(imageData, context, DateTime.UtcNow);
        return _frameSlot.Writer.TryWrite(submission);
    }

    /// <summary>
    /// Background consumer loop: reads frames from the slot channel sequentially.
    /// Counter management (Increment/Decrement) is INSIDE this loop, not before Task.Run,
    /// matching the production OpenRouterBrainService pattern.
    ///
    /// ReadAllAsync uses NO cancellation token — loop exits only on channel completion
    /// (Dispose), not on CancelAll(). This keeps the service alive across sessions.
    /// </summary>
    private async Task ConsumeFramesAsync()
    {
        try
        {
            await foreach (var frame in _frameSlot.Reader.ReadAllAsync())
            {
                Interlocked.Increment(ref _activeTasks);
                try
                {
                    await ProcessFrameInternalAsync(frame);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("[MockBrain] Frame processing cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MockBrain] Error in frame consumer loop");
                    TryWriteError(Guid.NewGuid().ToString("N")[..8], ex.Message);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeTasks);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[MockBrain] Frame consumer loop ended (cancelled)");
        }
    }

    /// <summary>
    /// Process a single frame: simulate latency, produce mock BrainResult.
    /// Shared by the consumer loop (TrySubmitFrame path).
    /// </summary>
    private async Task ProcessFrameInternalAsync(FrameSubmission frame)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];

        _logger.LogDebug("[MockBrain] Processing frame ({Bytes} bytes, correlation={Id})",
            frame.ImageData.Length, correlationId);

        await Task.Delay(500, _cts.Token);

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
                Summary = $"Mock analysis of {frame.ImageData.Length} byte frame",
                Evaluation = 15 // Slight white advantage (centipawns)
            }
        };

        await _channel.Writer.WriteAsync(result, _cts.Token);
        _logger.LogDebug("[MockBrain] ImageAnalysis result written (correlation={Id})", correlationId);
    }

    // ── On-Demand Submission (SubmitImageAsync) ─────────────────────────────

    public Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        _logger.LogDebug("[MockBrain] Image submitted ({Bytes} bytes, correlation={Id})",
            imageData.Length, correlationId);

        // Counter inside delegate + CancellationToken.None for Task.Run scheduling
        // (prevents counter leak when token is pre-cancelled)
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
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        _logger.LogDebug("[MockBrain] Query submitted: \"{Query}\" (correlation={Id})",
            userQuery.Length > 50 ? userQuery[..50] + "..." : userQuery, correlationId);

        // Counter inside delegate + CancellationToken.None for Task.Run scheduling
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
        }, CancellationToken.None);

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
        _frameSlot.Writer.TryComplete();
        _channel.Writer.TryComplete();
        _consumerTask.Wait(TimeSpan.FromMilliseconds(500));
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
