using System.Diagnostics;
using System.Threading.Channels;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Services.Brain;

/// <summary>
/// Local IBrainService implementation backed by MiniCPM-o via ILocalVisionInferenceClient.
/// Drop-in replacement for OpenRouterBrainService — preserves frame-slot pattern,
/// BrainResult channel contract, and prompt builder integration.
///
/// Tool execution remains in C# — this plan does not delegate tool ownership to the local runtime.
/// </summary>
public sealed class LocalMiniCpmBrainService : IBrainService
{
    private readonly ILocalVisionInferenceClient _client;
    private readonly ISessionManager _sessionManager;
    private readonly IBrainPromptBuilder _brainPromptBuilder;
    private readonly IGameJournalService? _gameJournal;
    private readonly IBrainContextService? _brainContext;
    private readonly ITelemetryService? _telemetry;
    private readonly Channel<BrainResult> _channel;
    private readonly Channel<FrameSubmission> _frameSlot;
    private readonly Task _consumerTask;
    private CancellationTokenSource _globalCts = new();
    private int _activeTasks;
    private bool _disposed;

    private string? _previousGameSummary;

    public LocalMiniCpmBrainService(
        ILocalVisionInferenceClient client,
        ISessionManager sessionManager,
        IBrainPromptBuilder? brainPromptBuilder = null,
        ITelemetryService? telemetry = null,
        IGameJournalService? gameJournal = null,
        IBrainContextService? brainContext = null)
    {
        _client = client;
        _sessionManager = sessionManager;
        _brainPromptBuilder = brainPromptBuilder ?? new BrainPromptBuilder();
        _telemetry = telemetry;
        _gameJournal = gameJournal;
        _brainContext = brainContext;

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
    }

    public void SetPreviousGameSummary(string? summary) => _previousGameSummary = summary;

    // ── IBrainService Properties ─────────────────────────────────────────────

    public ChannelReader<BrainResult> Results => _channel.Reader;

    public bool IsBusy => Volatile.Read(ref _activeTasks) > 0;

    public string ProviderName => "Local MiniCPM";

    // ── Frame Slot (Channel-based) ──────────────────────────────────────────

    public bool TrySubmitFrame(byte[] imageData, string context)
    {
        var submission = new FrameSubmission(imageData, context, DateTime.UtcNow);
        return _frameSlot.Writer.TryWrite(submission);
    }

    /// <summary>
    /// Background consumer loop — mirrors OpenRouterBrainService pattern exactly.
    /// ReadAllAsync uses NO cancellation token; loop exits only on channel completion (Dispose).
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
                    var correlationId = ConsoleTelemetryService.NewCorrelationId();
                    await ProcessFrameInternalAsync(frame.ImageData, frame.Context, correlationId, _globalCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /* expected on CancelAll */ }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LocalBrain] Frame processing error: {ex.Message}");
                    TryWriteError(ex.Message);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeTasks);
                }
            }
        }
        catch (OperationCanceledException) { /* channel closed */ }
    }

    private async Task ProcessFrameInternalAsync(byte[] imageData, string context, string correlationId, CancellationToken ct)
    {
        Debug.WriteLine($"[LocalBrain] Processing frame (correlation={correlationId}, {imageData.Length} bytes)");
        _telemetry?.TrackEvent("brain", "local_submit_image", new Dictionary<string, string>
        {
            ["correlationId"] = correlationId,
            ["bytes"] = imageData.Length.ToString()
        });

        // Gather context — same pattern as OpenRouterBrainService
        var agent = !string.IsNullOrEmpty(_sessionManager.Context.AgentKey)
            ? Agents.GetByKey(_sessionManager.Context.AgentKey) : null;

        IReadOnlyList<BrainEvent> l1Events = [];
        string? rollingSummary = null;
        if (_brainContext != null)
        {
            var envelope = await _brainContext.GetContextForChatAsync(DateTime.UtcNow).ConfigureAwait(false);
            l1Events = envelope.ImmediateEvents;
            rollingSummary = envelope.RollingSummary;
        }

        var journalSummary = _gameJournal?.GetSummary();
        var moveNumber = _gameJournal?.EntryCount ?? 0;
        var isConnected = _sessionManager.Context.State == SessionState.InGame;

        var systemPrompt = agent != null
            ? _brainPromptBuilder.BuildSystemPrompt(agent, l1Events, journalSummary, rollingSummary, _previousGameSummary, isConnected)
            : GetBrainPersonality();

        var gameType = _sessionManager.Context.GameType ?? "chess";
        var userPrompt = _brainPromptBuilder.BuildUserPrompt(gameType, moveNumber + 1);

        // Local vision request
        var request = new LocalVisionRequest
        {
            ImageData = imageData,
            UserPrompt = userPrompt,
            SystemPrompt = systemPrompt,
            CorrelationId = correlationId
        };

        var response = await _client.AnalyzeImageAsync(request, ct).ConfigureAwait(false);

        var analysisText = response.Success
            ? response.AssistantText
            : $"Local inference failed: {response.FailureReason}";

        _telemetry?.TrackEvent("brain", "local_response_received", new Dictionary<string, string>
        {
            ["correlationId"] = correlationId,
            ["success"] = response.Success.ToString(),
            ["latency_ms"] = (response.LatencyMs ?? -1).ToString()
        });

        await _channel.Writer.WriteAsync(new BrainResult
        {
            Type = response.Success ? BrainResultType.ImageAnalysis : BrainResultType.Error,
            AnalysisText = analysisText,
            VoiceNarration = response.Success ? TruncateForVoice(response.AssistantText, 200) : null,
            Priority = response.Success ? BrainResultPriority.WhenIdle : BrainResultPriority.Silent,
            CorrelationId = correlationId,
        }, ct).ConfigureAwait(false);
    }

    // ── On-Demand Image Submission ──────────────────────────────────────────

    public Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);

        _ = Task.Run(async () =>
        {
            Interlocked.Increment(ref _activeTasks);
            var correlationId = ConsoleTelemetryService.NewCorrelationId();
            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                await ProcessFrameInternalAsync(imageData, context, correlationId, linkedCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[LocalBrain] Image analysis cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalBrain] Image analysis failed: {ex.Message}");
                TryWriteError(ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
                linkedCts.Dispose();
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    // ── Text Query ───────────────────────────────────────────────────────────

    public Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);

        _ = Task.Run(async () =>
        {
            Interlocked.Increment(ref _activeTasks);
            var correlationId = ConsoleTelemetryService.NewCorrelationId();
            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                Debug.WriteLine($"[LocalBrain] SubmitQuery started (correlation={correlationId})");

                var agent = !string.IsNullOrEmpty(_sessionManager.Context.AgentKey)
                    ? Agents.GetByKey(_sessionManager.Context.AgentKey) : null;

                string systemPrompt;
                if (agent != null)
                {
                    var journalSummary = _gameJournal?.GetSummary();
                    var isConnected = _sessionManager.Context.State == SessionState.InGame;
                    systemPrompt = _brainPromptBuilder.BuildSystemPrompt(
                        agent, context.ImmediateEvents, journalSummary,
                        context.RollingSummary, _previousGameSummary, isConnected);
                }
                else
                {
                    var personality = GetBrainPersonality();
                    systemPrompt = $"{personality} Game state: {_sessionManager.Context.State}. " +
                                   $"Game: {_sessionManager.Context.GameType ?? "unknown"}. " +
                                   $"Intent: {context.Intent}.";
                    if (!string.IsNullOrEmpty(context.RollingSummary))
                        systemPrompt += $" Summary: {context.RollingSummary}";
                }

                var reply = await _client.ChatAsync(userQuery, systemPrompt, linkedCts.Token).ConfigureAwait(false);

                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.ToolResult,
                    AnalysisText = reply,
                    VoiceNarration = TruncateForVoice(reply, 200),
                    Priority = BrainResultPriority.WhenIdle,
                    CorrelationId = correlationId,
                }, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[LocalBrain] Query cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalBrain] Query failed: {ex.Message}");
                TryWriteError(ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
                linkedCts.Dispose();
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    // ── Direct Chat (request-reply, no channel) ─────────────────────────────

    public async Task<string> ChatAsync(string userQuery, IReadOnlyList<ChatMessage> chatHistory, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);
        Interlocked.Increment(ref _activeTasks);
        try
        {
            var personality = GetBrainPersonality();
            var sessionContext = _sessionManager.Context.State == SessionState.InGame
                ? "You are chatting with the user during an active live game session that you are currently watching. " +
                  "Speak as a live board observer. Do not claim you cannot see the board."
                : "You are chatting with the user outside of a game session. " +
                  "Be helpful, conversational, and stay in character. " +
                  "You can discuss strategy, past games, or general gaming topics.";
            var systemPrompt = $"{personality} {sessionContext}";

            var reply = await _client.ChatAsync(userQuery, systemPrompt, linkedCts.Token).ConfigureAwait(false);
            return reply;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[LocalBrain] ChatAsync cancelled");
            return "Local chat timed out or was cancelled.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalBrain] ChatAsync failed: {ex.Message}");
            return "Sorry, I couldn't process that right now. Try again in a moment.";
        }
        finally
        {
            Interlocked.Decrement(ref _activeTasks);
            linkedCts.Dispose();
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void CancelAll()
    {
        Debug.WriteLine("[LocalBrain] CancelAll — cancelling all in-flight requests");
        var oldCts = Interlocked.Exchange(ref _globalCts, new CancellationTokenSource());
        try { oldCts.Cancel(); } catch { /* already disposed */ }
        oldCts.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CancelAll();
        _frameSlot.Writer.TryComplete();
        _channel.Writer.TryComplete();
        _consumerTask.Wait(TimeSpan.FromMilliseconds(500));
        _globalCts.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string GetBrainPersonality()
    {
        var agentKey = _sessionManager.Context.AgentKey;
        if (!string.IsNullOrEmpty(agentKey))
        {
            var agent = Agents.GetByKey(agentKey);
            if (agent?.BrainPersonalityPrefix is not null)
                return agent.BrainPersonalityPrefix.Trim();
        }
        return "You are a gaming AI analyst.";
    }

    private static string TruncateForVoice(string? text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length <= maxLen) return text;

        var searchRegion = text[..maxLen];
        var lastPeriod = searchRegion.LastIndexOf(". ", StringComparison.Ordinal);
        if (lastPeriod > maxLen / 2) return text[..(lastPeriod + 1)];

        var lastQuestion = searchRegion.LastIndexOf('?');
        if (lastQuestion > maxLen / 2) return text[..(lastQuestion + 1)];

        var lastExclaim = searchRegion.LastIndexOf('!');
        if (lastExclaim > maxLen / 2) return text[..(lastExclaim + 1)];

        return text[..(maxLen - 3)] + "...";
    }

    private void TryWriteError(string errorMessage)
    {
        _channel.Writer.TryWrite(new BrainResult
        {
            Type = BrainResultType.Error,
            AnalysisText = $"Local brain error: {errorMessage}",
            Priority = BrainResultPriority.Silent,
        });
    }
}
