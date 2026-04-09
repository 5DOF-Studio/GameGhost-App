using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Brain;

/// <summary>
/// Production IBrainService backed by OpenRouter REST API.
/// Analyzes captured game images via vision models and handles multi-turn tool calling.
/// Results are published to a bounded Channel for downstream consumption by BrainEventRouter.
///
/// Frame submission uses a Channel(1, DropOldest) "frame slot" pattern:
/// - TrySubmitFrame always succeeds (never blocks the capture pipeline)
/// - When a new frame arrives while the previous is queued, the old one is replaced
/// - A single consumer loop processes frames sequentially with correct counter management
/// </summary>
public sealed class OpenRouterBrainService : IBrainService
{
    private readonly OpenRouterClient _client;
    private readonly ToolExecutor _toolExecutor;
    private readonly ISessionManager _sessionManager;
    private readonly IBrainPromptBuilder _brainPromptBuilder;
    private readonly IGameJournalService? _gameJournal;
    private readonly IBrainContextService? _brainContext;
    private readonly Channel<BrainResult> _channel;
    private readonly Channel<FrameSubmission> _frameSlot;
    private readonly Task _consumerTask;
    private readonly string _brainModel;
    private readonly string _workerModel;
    private readonly ITelemetryService? _telemetry;
    private readonly ISessionTraceService? _sessionTrace;
    private readonly IVoiceTranscriptStore? _voiceTranscriptStore;
    private readonly TimeSpan _imageAnalysisMinInterval;
    private readonly int _maxOpenRouterRetries;
    private readonly TimeSpan _openRouterRetryBaseDelay;
    private readonly SemaphoreSlim _imageAnalysisGate = new(1, 1);
    private CancellationTokenSource _globalCts = new();
    private int _activeTasks;
    private DateTime _lastImageAnalysisCompletedAtUtc = DateTime.MinValue;

    /// <summary>Summary of the previous game, injected by auto new-game detection (Plan 06).</summary>
    private string? _previousGameSummary;

    private const int MaxToolTurns = 5;
    private static readonly TimeSpan DefaultImageAnalysisMinInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultOpenRouterRetryBaseDelay = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// Creates a new OpenRouterBrainService.
    /// </summary>
    /// <param name="client">OpenRouter HTTP client for API calls.</param>
    /// <param name="toolExecutor">Local tool call executor for multi-turn interactions.</param>
    /// <param name="sessionManager">Session state for game context.</param>
    /// <param name="brainPromptBuilder">Structured prompt builder for system/user message split.</param>
    /// <param name="brainModel">Vision model for image analysis (default: google/gemini-2.5-flash).</param>
    /// <param name="workerModel">Lighter model for text queries (default: gpt-4o-mini).</param>
    /// <param name="telemetry">Optional telemetry service for structured pipeline observability.</param>
    /// <param name="gameJournal">Optional game journal for move-by-move context.</param>
    /// <param name="brainContext">Optional brain context service for L1/L2 context layers.</param>
    /// <param name="sessionTrace">Optional structured JSONL session trace for post-run debugging.</param>
    public OpenRouterBrainService(
        OpenRouterClient client,
        ToolExecutor toolExecutor,
        ISessionManager sessionManager,
        IBrainPromptBuilder? brainPromptBuilder = null,
        // IMPORTANT: this service-level brainModel overrides the OpenRouterClient
        // default model on every vision request. Keep DI and this fallback aligned.
        string brainModel = "google/gemini-2.5-flash",
        string workerModel = "openai/gpt-4o-mini",
        ITelemetryService? telemetry = null,
        IGameJournalService? gameJournal = null,
        IBrainContextService? brainContext = null,
        ISessionTraceService? sessionTrace = null,
        IVoiceTranscriptStore? voiceTranscriptStore = null,
        TimeSpan? imageAnalysisMinInterval = null,
        int maxOpenRouterRetries = 4,
        TimeSpan? openRouterRetryBaseDelay = null)
    {
        _client = client;
        _toolExecutor = toolExecutor;
        _sessionManager = sessionManager;
        _brainPromptBuilder = brainPromptBuilder ?? new BrainPromptBuilder();
        _gameJournal = gameJournal;
        _brainContext = brainContext;
        _brainModel = brainModel;
        _workerModel = workerModel;
        _telemetry = telemetry;
        _sessionTrace = sessionTrace;
        _voiceTranscriptStore = voiceTranscriptStore;
        _imageAnalysisMinInterval = imageAnalysisMinInterval ?? DefaultImageAnalysisMinInterval;
        _maxOpenRouterRetries = Math.Max(0, maxOpenRouterRetries);
        _openRouterRetryBaseDelay = openRouterRetryBaseDelay ?? DefaultOpenRouterRetryBaseDelay;

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
            },
            (FrameSubmission dropped) =>
            {
                _telemetry?.TrackEvent("brain", "frame_replaced", new Dictionary<string, string>
                {
                    ["age_ms"] = ((int)(DateTime.UtcNow - dropped.CapturedAt).TotalMilliseconds).ToString(),
                    ["bytes"] = dropped.ImageData.Length.ToString()
                });
                Debug.WriteLine($"[Brain] Stale frame replaced ({dropped.ImageData.Length} bytes, age={(DateTime.UtcNow - dropped.CapturedAt).TotalMilliseconds:F0}ms)");
            }
        );
        _consumerTask = Task.Run(ConsumeFramesAsync);
    }

    /// <summary>
    /// Set the previous game summary for context injection. Called by auto new-game detection (Plan 06).
    /// </summary>
    public void SetPreviousGameSummary(string? summary) => _previousGameSummary = summary;

    // ── IBrainService Properties ─────────────────────────────────────────────

    public ChannelReader<BrainResult> Results => _channel.Reader;

    public bool IsBusy => Volatile.Read(ref _activeTasks) > 0;

    public string ProviderName => $"OpenRouter ({_brainModel})";

    // ── Frame Slot (Channel-based) ──────────────────────────────────────────

    /// <inheritdoc />
    public bool TrySubmitFrame(byte[] imageData, string context)
    {
        var submission = new FrameSubmission(imageData, context, DateTime.UtcNow);
        var written = _frameSlot.Writer.TryWrite(submission);
        if (written)
        {
            _telemetry?.TrackEvent("capture", "frame_queued", new Dictionary<string, string>
            {
                ["bytes"] = imageData.Length.ToString()
            });
        }
        return written;
    }

    /// <summary>
    /// Background consumer loop: reads frames from the slot channel sequentially.
    /// Counter management (Increment/Decrement) is INSIDE this loop, not before Task.Run,
    /// which prevents the critical counter leak bug.
    ///
    /// IMPORTANT: ReadAllAsync uses NO cancellation token. The loop exits only when
    /// _frameSlot.Writer.TryComplete() is called in Dispose(). CancelAll() cancels
    /// in-flight ProcessFrameInternalAsync via _globalCts.Token (read per-iteration),
    /// but does NOT kill the loop — so the service survives session disconnects.
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
                    var queueWaitMs = (int)(DateTime.UtcNow - frame.CapturedAt).TotalMilliseconds;
                    _telemetry?.TrackEvent("brain", "frame_processing_start", new Dictionary<string, string>
                    {
                        ["correlationId"] = correlationId,
                        ["queue_wait_ms"] = queueWaitMs.ToString()
                    });

                    await ExecuteImageAnalysisWithPacingAsync(
                            frame.ImageData,
                            frame.Context,
                            correlationId,
                            source: "frame_slot",
                            _globalCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown/session disconnect — still emit terminal event
                    _sessionTrace?.TrackEvent("brain.request.failure", new Dictionary<string, string>
                    {
                        ["request_type"] = "frame_consumer",
                        ["error_type"] = "cancelled"
                    });
                }
                catch (BrainRequestFailedException ex)
                {
                    Console.WriteLine($"[Brain] Frame processing failed: {ex.Fingerprint}");
                    _telemetry?.TrackEvent("brain", "frame_error", new Dictionary<string, string>
                    {
                        ["error"] = ex.Fingerprint
                    });
                    _sessionTrace?.TrackEvent("brain.request.failure", new Dictionary<string, string>
                    {
                        ["request_type"] = "frame_consumer",
                        ["error_type"] = ex.Fingerprint,
                        ["attempt_count"] = ex.AttemptCount.ToString()
                    });
                    try
                    {
                        await _channel.Writer.WriteAsync(new BrainResult
                        {
                            Type = BrainResultType.Error,
                            AnalysisText = ex.Message,
                            Priority = BrainResultPriority.Silent,
                            ErrorFingerprint = ex.Fingerprint,
                            AttemptCount = ex.AttemptCount,
                            RequestDisconnect = true,
                        }, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Brain] Frame processing error: {ex.Message}");
                    _telemetry?.TrackEvent("brain", "frame_error", new Dictionary<string, string>
                    {
                        ["error"] = ex.GetType().Name
                    });
                    _sessionTrace?.TrackEvent("brain.request.failure", new Dictionary<string, string>
                    {
                        ["request_type"] = "frame_consumer",
                        ["error_type"] = ex.GetType().Name
                    });
                    // Write error to channel so downstream consumers are aware
                    try
                    {
                        await _channel.Writer.WriteAsync(new BrainResult
                        {
                            Type = BrainResultType.Error,
                            AnalysisText = "Brain failed unexpectedly. Brain analysis paused to stop repeated failures.",
                            Priority = BrainResultPriority.Silent,
                            ErrorFingerprint = $"brain_unhandled:{ex.GetType().Name}",
                            AttemptCount = 1,
                            RequestDisconnect = true,
                        }, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { /* channel may be completed */ }
                }
                finally
                {
                    Interlocked.Decrement(ref _activeTasks);
                }
            }
        }
        catch (OperationCanceledException) { /* channel closed or global cancel */ }
    }

    /// <summary>
    /// Core image analysis logic extracted from SubmitImageAsync.
    /// Used by both the consumer loop (TrySubmitFrame path) and on-demand SubmitImageAsync.
    /// </summary>
    private async Task ProcessFrameInternalAsync(byte[] imageData, string context, string correlationId, CancellationToken ct)
    {
        Console.WriteLine($"[Brain] SubmitImage started (correlation={correlationId}, {imageData.Length} bytes)");
        _telemetry?.TrackEvent("brain", "submit_image", new Dictionary<string, string>
        {
            ["correlationId"] = correlationId,
            ["bytes"] = imageData.Length.ToString()
        });
        _sessionTrace?.TrackEvent("brain.request.start", new Dictionary<string, string>
        {
            ["correlation_id"] = correlationId,
            ["request_type"] = "image_analysis",
            ["model"] = _brainModel,
            ["image_bytes"] = imageData.Length.ToString()
        });

        // Gather context for system message
        var agent = !string.IsNullOrEmpty(_sessionManager.Context.AgentKey)
            ? Agents.GetByKey(_sessionManager.Context.AgentKey) : null;

        IReadOnlyList<BrainEvent> l1Events = [];
        string? rollingSummary = null;
        if (_brainContext != null)
        {
            var inputs = _voiceTranscriptStore != null
                ? new ContextAssemblyInputs { RecentTranscript = _voiceTranscriptStore.GetRecent(10) }
                : null;
            var envelope = await _brainContext.GetContextForChatAsync(DateTime.UtcNow, inputs: inputs).ConfigureAwait(false);
            l1Events = envelope.ImmediateEvents;
            rollingSummary = envelope.RollingSummary;
        }

        var journalSummary = _gameJournal?.GetSummary();
        var moveNumber = _gameJournal?.EntryCount ?? 0;
        var isConnected = _sessionManager.Context.State == Models.SessionState.InGame;

        // Build system + user prompts
        var systemPrompt = agent != null
            ? _brainPromptBuilder.BuildSystemPrompt(agent, l1Events, journalSummary, rollingSummary, _previousGameSummary, isConnected)
            : GetBrainPersonality();  // fallback for unknown agents

        var gameType = _sessionManager.Context.GameType ?? "chess";
        var userPrompt = _brainPromptBuilder.BuildUserPrompt(gameType, moveNumber + 1);

        // Build request with vision and system message
        var request = _client.CreateImageAnalysisRequest(imageData, userPrompt, _brainModel, systemPrompt);

        // Add tool definitions if available
        var tools = ToolExecutor.GetAvailableToolDefinitions(_sessionManager);
        if (tools.Count > 0)
        {
            request.Tools = tools;
            request.ToolChoice = "auto";
        }

        // Execute initial request
        var messages = new List<OpenRouterMessage>(request.Messages);
        var response = await ChatCompletionWithRetryAsync(
            request, "image_analysis", correlationId, allowRetries: true, ct).ConfigureAwait(false);

        // Tool call loop (max MaxToolTurns iterations)
        int toolTurns = 0;
        var toolCallInfos = new List<ToolCallInfo>();
        while (response.Choices?.Count > 0
               && response.Choices[0].FinishReason == "tool_calls"
               && toolTurns < MaxToolTurns)
        {
            var assistantMsg = response.Choices[0].Message;
            messages.Add(assistantMsg);

            // Execute each tool call
            foreach (var toolCall in assistantMsg.ToolCalls ?? [])
            {
                Console.WriteLine($"[Brain] Tool call: {toolCall.Function.Name} (turn {toolTurns + 1})");
                var sw = Stopwatch.StartNew();
                var toolResult = await _toolExecutor.ExecuteToolAsync(
                    toolCall.Function.Name, toolCall.Function.Arguments, ct)
                    .ConfigureAwait(false);
                sw.Stop();
                toolCallInfos.Add(new ToolCallInfo
                {
                    ToolName = toolCall.Function.Name,
                    InputJson = toolCall.Function.Arguments,
                    OutputJson = toolResult,
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Success = IsSuccessfulToolResult(toolResult)
                });
                messages.Add(new OpenRouterMessage
                {
                    Role = "tool",
                    Content = toolResult,
                    ToolCallId = toolCall.Id
                });
            }

            // Send tool results back to LLM
            request.Messages = messages;
            request.Tools = tools; // Keep tools available
            response = await ChatCompletionWithRetryAsync(
                request, "image_analysis", correlationId, allowRetries: true, ct).ConfigureAwait(false);
            toolTurns++;
        }

        // Extract final response
        var finalContent = response.Choices?.FirstOrDefault()?.Message?.Content?.ToString();
        Console.WriteLine($"[Brain] Image analysis complete (correlation={correlationId}, toolTurns={toolTurns})");
        _telemetry?.TrackEvent("brain", "response_received", new Dictionary<string, string>
        {
            ["correlationId"] = correlationId,
            ["toolTurns"] = toolTurns.ToString()
        });
        _sessionTrace?.TrackEvent("brain.request.success", new Dictionary<string, string>
        {
            ["correlation_id"] = correlationId,
            ["request_type"] = "image_analysis",
            ["model"] = _brainModel,
            ["tool_turns"] = toolTurns.ToString(),
            ["response_length"] = (finalContent?.Length ?? 0).ToString()
        });

        // Write result to channel
        await _channel.Writer.WriteAsync(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = finalContent ?? "No analysis available",
            VoiceNarration = TruncateForVoice(finalContent, 200),
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = correlationId,
            ToolCalls = toolCallInfos.Count > 0 ? toolCallInfos : null,
        }, ct).ConfigureAwait(false);
    }

    private async Task ExecuteImageAnalysisWithPacingAsync(
        byte[] imageData,
        string context,
        string correlationId,
        string source,
        CancellationToken ct)
    {
        await _imageAnalysisGate.WaitAsync(ct).ConfigureAwait(false);
        var shouldAdvanceCooldown = false;
        try
        {
            var nowUtc = DateTime.UtcNow;
            var earliestNextStartUtc = _lastImageAnalysisCompletedAtUtc + _imageAnalysisMinInterval;
            if (_imageAnalysisMinInterval > TimeSpan.Zero && earliestNextStartUtc > nowUtc)
            {
                var wait = earliestNextStartUtc - nowUtc;
                _telemetry?.TrackEvent("brain", "cooldown_wait", new Dictionary<string, string>
                {
                    ["correlationId"] = correlationId,
                    ["source"] = source,
                    ["wait_ms"] = ((int)wait.TotalMilliseconds).ToString()
                });
                _sessionTrace?.TrackEvent("brain.cooldown_wait", new Dictionary<string, string>
                {
                    ["correlation_id"] = correlationId,
                    ["source"] = source,
                    ["wait_ms"] = ((int)wait.TotalMilliseconds).ToString()
                });
                await Task.Delay(wait, ct).ConfigureAwait(false);
            }

            await ProcessFrameInternalAsync(imageData, context, correlationId, ct).ConfigureAwait(false);
            shouldAdvanceCooldown = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            shouldAdvanceCooldown = true;
            throw;
        }
        finally
        {
            if (shouldAdvanceCooldown)
                _lastImageAnalysisCompletedAtUtc = DateTime.UtcNow;
            _imageAnalysisGate.Release();
        }
    }

    // ── Image Analysis (on-demand, kept for capture_screen tool) ────────────

    public Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);
        var linkedCt = linkedCts.Token;

        // FIX: Increment INSIDE delegate, pass CancellationToken.None to Task.Run
        // This prevents the critical counter leak when the token is pre-cancelled
        _ = Task.Run(async () =>
        {
            Interlocked.Increment(ref _activeTasks);
            var correlationId = ConsoleTelemetryService.NewCorrelationId();
            try
            {
                linkedCt.ThrowIfCancellationRequested();
                await ExecuteImageAnalysisWithPacingAsync(
                        imageData,
                        context,
                        correlationId,
                        source: "submit_image",
                        linkedCt)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[Brain] Image analysis cancelled");
                _sessionTrace?.TrackEvent("brain.request.failure", new Dictionary<string, string>
                {
                    ["correlation_id"] = correlationId,
                    ["request_type"] = "image_analysis",
                    ["error_type"] = "cancelled"
                });
            }
            catch (BrainRequestFailedException ex)
            {
                Console.WriteLine($"[Brain] Image analysis failed: {ex.Fingerprint}");
                _telemetry?.TrackEvent("brain", "response_error", new Dictionary<string, string>
                {
                    ["correlationId"] = correlationId,
                    ["error"] = ex.Fingerprint
                });
                _sessionTrace?.TrackEvent("brain.request.failure", new Dictionary<string, string>
                {
                    ["correlation_id"] = correlationId,
                    ["request_type"] = "image_analysis",
                    ["error_type"] = ex.Fingerprint,
                    ["attempt_count"] = ex.AttemptCount.ToString()
                });
                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.Error,
                    AnalysisText = ex.Message,
                    Priority = BrainResultPriority.Silent,
                    CorrelationId = correlationId,
                    ErrorFingerprint = ex.Fingerprint,
                    AttemptCount = ex.AttemptCount,
                    RequestDisconnect = true,
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Brain] Image analysis failed: {ex.GetType().Name} — {ex.Message}");
                _telemetry?.TrackEvent("brain", "response_error", new Dictionary<string, string>
                {
                    ["correlationId"] = correlationId,
                    ["error"] = ex.GetType().Name
                });
                _sessionTrace?.TrackEvent("brain.request.failure", new Dictionary<string, string>
                {
                    ["correlation_id"] = correlationId,
                    ["request_type"] = "image_analysis",
                    ["error_type"] = ex.GetType().Name
                });
                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.Error,
                    AnalysisText = "Brain failed unexpectedly. Brain analysis paused to stop repeated failures.",
                    Priority = BrainResultPriority.Silent,
                    CorrelationId = correlationId,
                    ErrorFingerprint = $"brain_unhandled:{ex.GetType().Name}",
                    AttemptCount = 1,
                    RequestDisconnect = true,
                }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
                linkedCts.Dispose();
            }
        }, CancellationToken.None);  // CancellationToken.None: never skip the delegate

        return Task.CompletedTask;
    }

    // ── Text Query ───────────────────────────────────────────────────────────

    public Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);
        var linkedCt = linkedCts.Token;

        // FIX: Increment INSIDE delegate, pass CancellationToken.None to Task.Run
        _ = Task.Run(async () =>
        {
            Interlocked.Increment(ref _activeTasks);
            var correlationId = ConsoleTelemetryService.NewCorrelationId();
            try
            {
                linkedCt.ThrowIfCancellationRequested();

                Console.WriteLine($"[Brain] SubmitQuery started (correlation={correlationId})");
                _telemetry?.TrackEvent("brain", "submit_query", new Dictionary<string, string>
                {
                    ["correlationId"] = correlationId
                });
                _sessionTrace?.TrackEvent("brain.request.start", new Dictionary<string, string>
                {
                    ["correlation_id"] = correlationId,
                    ["request_type"] = "query",
                    ["model"] = _workerModel
                });

                // Build system message with brain prompt builder when agent available
                var agent = !string.IsNullOrEmpty(_sessionManager.Context.AgentKey)
                    ? Agents.GetByKey(_sessionManager.Context.AgentKey) : null;

                string systemContext;
                if (agent != null)
                {
                    var journalSummary = _gameJournal?.GetSummary();
                    var isConnected = _sessionManager.Context.State == Models.SessionState.InGame;
                    systemContext = _brainPromptBuilder.BuildSystemPrompt(
                        agent, context.ImmediateEvents, journalSummary,
                        context.RollingSummary, _previousGameSummary, isConnected);
                }
                else
                {
                    var personalityPrefix = GetBrainPersonality();
                    systemContext = $"{personalityPrefix} Game state: {_sessionManager.Context.State}. " +
                                    $"Game: {_sessionManager.Context.GameType ?? "unknown"}. " +
                                    $"Intent: {context.Intent}. " +
                                    (string.IsNullOrEmpty(context.RollingSummary)
                                        ? ""
                                        : $"Summary: {context.RollingSummary}");
                }

                var messages = new List<OpenRouterMessage>
                {
                    new() { Role = "system", Content = systemContext },
                    new() { Role = "user", Content = userQuery }
                };

                var request = new OpenRouterRequest
                {
                    Model = _workerModel,
                    Messages = messages,
                    MaxTokens = 512
                };

                // Add tool definitions if available
                var tools = ToolExecutor.GetAvailableToolDefinitions(_sessionManager);
                if (tools.Count > 0)
                {
                    request.Tools = tools;
                    request.ToolChoice = "auto";
                }

                var response = await ChatCompletionWithRetryAsync(
                    request, "query", correlationId, allowRetries: true, linkedCt).ConfigureAwait(false);

                // Tool call loop
                int toolTurns = 0;
                var toolCallInfos = new List<ToolCallInfo>();
                while (response.Choices?.Count > 0
                       && response.Choices[0].FinishReason == "tool_calls"
                       && toolTurns < MaxToolTurns)
                {
                    var assistantMsg = response.Choices[0].Message;
                    messages.Add(assistantMsg);

                    foreach (var toolCall in assistantMsg.ToolCalls ?? [])
                    {
                        Console.WriteLine($"[Brain] Query tool call: {toolCall.Function.Name} (turn {toolTurns + 1})");
                        var sw = Stopwatch.StartNew();
                        var toolResult = await _toolExecutor.ExecuteToolAsync(
                            toolCall.Function.Name, toolCall.Function.Arguments, linkedCt)
                            .ConfigureAwait(false);
                        sw.Stop();
                        toolCallInfos.Add(new ToolCallInfo
                        {
                            ToolName = toolCall.Function.Name,
                            InputJson = toolCall.Function.Arguments,
                            OutputJson = toolResult,
                            DurationMs = (int)sw.ElapsedMilliseconds,
                            Success = IsSuccessfulToolResult(toolResult)
                        });
                        messages.Add(new OpenRouterMessage
                        {
                            Role = "tool",
                            Content = toolResult,
                            ToolCallId = toolCall.Id
                        });
                    }

                    request.Messages = messages;
                    request.Tools = tools;
                    response = await ChatCompletionWithRetryAsync(
                        request, "query", correlationId, allowRetries: true, linkedCt).ConfigureAwait(false);
                    toolTurns++;
                }

                var finalContent = response.Choices?.FirstOrDefault()?.Message?.Content?.ToString();
                Console.WriteLine($"[Brain] Query complete (correlation={correlationId}, toolTurns={toolTurns})");
                _telemetry?.TrackEvent("brain", "response_received", new Dictionary<string, string>
                {
                    ["correlationId"] = correlationId,
                    ["toolTurns"] = toolTurns.ToString()
                });
                _sessionTrace?.TrackEvent("brain.request.success", new Dictionary<string, string>
                {
                    ["correlation_id"] = correlationId,
                    ["request_type"] = "query",
                    ["model"] = _workerModel,
                    ["tool_turns"] = toolTurns.ToString(),
                    ["response_length"] = (finalContent?.Length ?? 0).ToString()
                });

                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.ToolResult,
                    AnalysisText = finalContent ?? "No response available",
                    VoiceNarration = TruncateForVoice(finalContent, 200),
                    Priority = BrainResultPriority.WhenIdle,
                    IsDeferredAnswer = true,
                    CorrelationId = correlationId,
                    ToolCalls = toolCallInfos.Count > 0 ? toolCallInfos : null,
                }, linkedCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[Brain] Query cancelled");
                _sessionTrace?.TrackEvent("brain.request.failure", new Dictionary<string, string>
                {
                    ["correlation_id"] = correlationId,
                    ["request_type"] = "query",
                    ["error_type"] = "cancelled"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Brain] Query failed: {ex.Message}");
                _telemetry?.TrackEvent("brain", "response_error", new Dictionary<string, string>
                {
                    ["correlationId"] = correlationId,
                    ["error"] = ex.GetType().Name
                });
                _sessionTrace?.TrackEvent("brain.request.failure", new Dictionary<string, string>
                {
                    ["correlation_id"] = correlationId,
                    ["request_type"] = "query",
                    ["error_type"] = ex.GetType().Name
                });
                await _channel.Writer.WriteAsync(new BrainResult
                {
                    Type = BrainResultType.Error,
                    AnalysisText = ex is BrainRequestFailedException retryEx
                        ? retryEx.Message
                        : "Query processing failed. Brain is temporarily unavailable.",
                    Priority = BrainResultPriority.Silent,
                    CorrelationId = correlationId,
                    ErrorFingerprint = ex is BrainRequestFailedException retryEx2
                        ? retryEx2.Fingerprint
                        : $"query_unhandled:{ex.GetType().Name}",
                    AttemptCount = ex is BrainRequestFailedException retryEx3 ? retryEx3.AttemptCount : 1,
                    RequestDisconnect = ex is BrainRequestFailedException,
                }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
                linkedCts.Dispose();
            }
        }, CancellationToken.None);  // CancellationToken.None: never skip the delegate

        return Task.CompletedTask;
    }

    // ── Direct Chat (request-reply) ────────────────────────────────────────

    public async Task<string> ChatAsync(string userQuery, IReadOnlyList<ChatMessage> chatHistory, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);
        Interlocked.Increment(ref _activeTasks);
        try
        {
            var personalityPrefix = GetBrainPersonality();
            var sessionContext = _sessionManager.Context.State == SessionState.InGame
                ? "You are chatting with the user during an active live game session that you are currently watching. " +
                  "Speak as a live board observer. Do not claim you cannot see the board."
                : "You are chatting with the user outside of a game session. " +
                  "Be helpful, conversational, and stay in character. " +
                  "You can discuss strategy, past games, or general gaming topics.";
            var systemPrompt = $"{personalityPrefix} {sessionContext}";

            var messages = new List<OpenRouterMessage>
            {
                new() { Role = "system", Content = systemPrompt }
            };

            // Add recent chat history for conversational context
            foreach (var msg in chatHistory.TakeLast(10))
            {
                if (msg.Role == MessageRole.User)
                    messages.Add(new OpenRouterMessage { Role = "user", Content = msg.Content });
                else if (msg.Role == MessageRole.Assistant)
                    messages.Add(new OpenRouterMessage { Role = "assistant", Content = msg.Content });
            }

            messages.Add(new OpenRouterMessage { Role = "user", Content = userQuery });

            var request = new OpenRouterRequest
            {
                Model = _workerModel,
                Messages = messages,
                MaxTokens = 512
            };

            var response = await _client.ChatCompletionAsync(request, linkedCts.Token).ConfigureAwait(false);
            var reply = response.Choices?.FirstOrDefault()?.Message?.Content?.ToString();

            Debug.WriteLine($"[Brain] ChatAsync complete — {reply?.Length ?? 0} chars");
            return reply ?? "I'm not sure how to respond to that right now.";
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[Brain] ChatAsync cancelled");
            return "Chat was cancelled.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Brain] ChatAsync failed: {ex.Message}");
            return "Sorry, I couldn't process that right now. Try again in a moment.";
        }
        finally
        {
            Interlocked.Decrement(ref _activeTasks);
            linkedCts.Dispose();
        }
    }

    private async Task<OpenRouterResponse> ChatCompletionWithRetryAsync(
        OpenRouterRequest request,
        string requestType,
        string correlationId,
        bool allowRetries,
        CancellationToken ct)
    {
        var maxAttempts = allowRetries ? _maxOpenRouterRetries + 1 : 1;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await _client.ChatCompletionAsync(request, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsRetryableOpenRouterFailure(ex) && attempt < maxAttempts)
            {
                lastException = ex;
                var delay = GetRetryDelay(attempt);
                _telemetry?.TrackEvent("brain", "request_retry", new Dictionary<string, string>
                {
                    ["correlationId"] = correlationId,
                    ["request_type"] = requestType,
                    ["attempt"] = attempt.ToString(),
                    ["delay_ms"] = ((int)delay.TotalMilliseconds).ToString(),
                    ["error"] = GetErrorFingerprint(ex)
                });
                _sessionTrace?.TrackEvent("brain.request.retry", new Dictionary<string, string>
                {
                    ["correlation_id"] = correlationId,
                    ["request_type"] = requestType,
                    ["attempt"] = attempt.ToString(),
                    ["delay_ms"] = ((int)delay.TotalMilliseconds).ToString(),
                    ["error_type"] = ex.GetType().Name
                });
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw CreateBrainRequestFailedException(ex, attempt);
            }
        }

        throw CreateBrainRequestFailedException(lastException ?? new Exception("Unknown OpenRouter failure"), maxAttempts);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void CancelAll()
    {
        Console.WriteLine("[Brain] CancelAll — cancelling all in-flight requests");
        var oldCts = Interlocked.Exchange(ref _globalCts, new CancellationTokenSource());
        try { oldCts.Cancel(); } catch { /* already disposed */ }
        oldCts.Dispose();
    }

    public void Dispose()
    {
        CancelAll();
        _frameSlot.Writer.TryComplete();
        _channel.Writer.TryComplete();
        // Wait briefly for consumer to finish
        _consumerTask.Wait(TimeSpan.FromMilliseconds(500));
        _globalCts.Dispose();
        _imageAnalysisGate.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current agent's BrainPersonalityPrefix, or a generic fallback.
    /// </summary>
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

    private TimeSpan GetRetryDelay(int attempt)
    {
        if (_openRouterRetryBaseDelay <= TimeSpan.Zero)
            return TimeSpan.Zero;

        var multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
        var delayMs = _openRouterRetryBaseDelay.TotalMilliseconds * multiplier;
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, 5000));
    }

    private static bool IsRetryableOpenRouterFailure(Exception ex)
    {
        return ex switch
        {
            OpenRouterException openRouterEx => openRouterEx.StatusCode is
                HttpStatusCode.RequestTimeout or
                (HttpStatusCode)429 or
                HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout ||
                (int)openRouterEx.StatusCode >= 500,
            HttpRequestException => true,
            TimeoutException => true,
            _ => false
        };
    }

    private BrainRequestFailedException CreateBrainRequestFailedException(Exception ex, int attemptCount)
    {
        var fingerprint = GetErrorFingerprint(ex);
        var message = ex switch
        {
            OpenRouterException openRouterEx when openRouterEx.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "Brain authentication failed. Check the OpenRouter key, then reconnect.",
            OpenRouterException openRouterEx when (int)openRouterEx.StatusCode == 429 =>
                $"Brain is rate-limited after {attemptCount} attempts. Brain analysis paused to stop repeated failures.",
            OpenRouterException openRouterEx when (int)openRouterEx.StatusCode >= 500 =>
                $"Brain service is temporarily unavailable after {attemptCount} attempts. Brain analysis paused to stop repeated failures.",
            HttpRequestException =>
                $"Brain network request failed after {attemptCount} attempts. Brain analysis paused to stop repeated failures.",
            TimeoutException =>
                $"Brain request timed out after {attemptCount} attempts. Brain analysis paused to stop repeated failures.",
            _ =>
                $"Brain request failed after {attemptCount} attempts. Brain analysis paused to stop repeated failures."
        };

        return new BrainRequestFailedException(message, fingerprint, attemptCount, ex);
    }

    private static string GetErrorFingerprint(Exception ex)
    {
        return ex switch
        {
            OpenRouterException openRouterEx => $"openrouter:http_{(int)openRouterEx.StatusCode}",
            HttpRequestException => "openrouter:network",
            TimeoutException => "openrouter:timeout",
            _ => $"openrouter:{ex.GetType().Name.ToLowerInvariant()}"
        };
    }

    /// <summary>
    /// Truncates text for voice narration. Prefers sentence boundary if possible.
    /// </summary>
    private static string TruncateForVoice(string? text, int maxLen)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= maxLen)
            return text;

        // Try to truncate at a sentence boundary
        var searchRegion = text[..maxLen];
        var lastPeriod = searchRegion.LastIndexOf(". ", StringComparison.Ordinal);
        if (lastPeriod > maxLen / 2) // Only if we keep at least half the content
            return text[..(lastPeriod + 1)];

        var lastQuestion = searchRegion.LastIndexOf('?');
        if (lastQuestion > maxLen / 2)
            return text[..(lastQuestion + 1)];

        var lastExclaim = searchRegion.LastIndexOf('!');
        if (lastExclaim > maxLen / 2)
            return text[..(lastExclaim + 1)];

        // Fall back to hard truncate
        return text[..(maxLen - 3)] + "...";
    }

    private static bool IsSuccessfulToolResult(string? toolResult)
    {
        if (string.IsNullOrWhiteSpace(toolResult))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(toolResult);
            return !doc.RootElement.TryGetProperty("error", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

internal sealed class BrainRequestFailedException : Exception
{
    public BrainRequestFailedException(string message, string fingerprint, int attemptCount, Exception innerException)
        : base(message, innerException)
    {
        Fingerprint = fingerprint;
        AttemptCount = attemptCount;
    }

    public string Fingerprint { get; }
    public int AttemptCount { get; }
}
