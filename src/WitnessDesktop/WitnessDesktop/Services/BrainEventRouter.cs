using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.Services.History;

namespace WitnessDesktop.Services;

public class BrainEventRouter : IBrainEventRouter
{
    private static readonly TimeSpan GroundedIdleVoiceCooldown = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan ErrorDedupWindow = TimeSpan.FromSeconds(15);

    private readonly ITimelineFeed _timeline;
    private readonly IConversationProvider? _voiceAgent;
    private readonly Action<string>? _topStrip;
    private readonly IBrainContextService? _brainContext;
    private readonly ITelemetryService? _telemetry;
    private readonly IGameJournalService? _gameJournal;
    private readonly IFrameDiffService? _frameDiffService;
    private readonly Action<string>? _onNewGameDetected;
    private readonly IVoiceGroundingCoordinator? _voiceGrounding;
    private readonly IVoiceTranscriptStore? _voiceTranscriptStore;
    private readonly ISessionHistoryService? _historyService;
    private readonly ISessionTraceService? _sessionTrace;
    private readonly IGameSkillPackService? _packService;
    private readonly IVoiceDeliveryGate? _voiceDeliveryGate;
    private readonly IReminderQueue? _reminderQueue;
    private readonly IExchangeManager? _exchangeManager;

    // FEN pattern: 8 ranks separated by /, side to move, castling, en passant, halfmove, fullmove
    private static readonly Regex FenRegex = new(
        @"[rnbqkpRNBQKP1-8]{1,8}(/[rnbqkpRNBQKP1-8]{1,8}){7}\s+[wb]\s+[KQkq-]+\s+(?:[a-h][1-8]|-)\s+\d+\s+\d+",
        RegexOptions.Compiled);

    private CancellationTokenSource? _consumeCts;
    private Task? _consumeTask;
    private readonly object _voiceDedupLock = new();
    private string? _lastGroundingPrefixSent;
    private DateTime _lastGroundingPrefixSentAtUtc = DateTime.MinValue;
    private readonly object _errorDedupLock = new();
    private string? _lastErrorFingerprint;
    private DateTime _lastErrorFingerprintAtUtc = DateTime.MinValue;

    // D-040: Emission queue — events enqueued by OnStructuredAnalysis, drained one-at-a-time
    private readonly Queue<TimelineEvent> _emissionQueue = new();
    private readonly object _emissionLock = new();
    private PeriodicTimer? _emissionTimer;
    private Task? _emissionTask;
    private CancellationTokenSource? _emissionCts;

    public BrainEventRouter(
        ITimelineFeed timeline,
        IConversationProvider? voiceAgent = null,
        Action<string>? topStrip = null,
        IBrainContextService? brainContext = null,
        ITelemetryService? telemetry = null,
        IGameJournalService? gameJournal = null,
        IFrameDiffService? frameDiffService = null,
        Action<string>? onNewGameDetected = null,
        IVoiceGroundingCoordinator? voiceGrounding = null,
        IVoiceTranscriptStore? voiceTranscriptStore = null,
        ISessionHistoryService? historyService = null,
        ISessionTraceService? sessionTrace = null,
        IGameSkillPackService? packService = null,
        IVoiceDeliveryGate? voiceDeliveryGate = null,
        IReminderQueue? reminderQueue = null,
        IExchangeManager? exchangeManager = null)
    {
        _timeline = timeline;
        _voiceAgent = voiceAgent;
        _topStrip = topStrip;
        _brainContext = brainContext;
        _telemetry = telemetry;
        _gameJournal = gameJournal;
        _frameDiffService = frameDiffService;
        _onNewGameDetected = onNewGameDetected;
        _voiceGrounding = voiceGrounding;
        _voiceTranscriptStore = voiceTranscriptStore;
        _historyService = historyService;
        _sessionTrace = sessionTrace;
        _packService = packService;
        _voiceDeliveryGate = voiceDeliveryGate;
        _reminderQueue = reminderQueue;
        _exchangeManager = exchangeManager;
    }
    
    /// <summary>
    /// Persists a timeline event to the history database if a session is active.
    /// Uses the current checkpoint ID from the timeline feed when none is explicitly provided.
    /// Fire-and-forget: SessionHistoryService has its own try-catch.
    /// </summary>
    private void PersistEventIfActive(TimelineEvent evt, string? checkpointId = null, int displayOrder = 0)
    {
        if (_historyService is null || _sessionTrace?.SessionId is not { } sid) return;
        _ = _historyService.PersistTimelineEventAsync(sid, evt, checkpointId, displayOrder);
    }

    public void OnScreenCapture(string screenshotRef, TimeSpan gameTime, string method)
    {
        var captureText = $"Analyzing capture at {gameTime:m\\:ss}...";
        _topStrip?.Invoke(captureText);
        TopStripUpdated?.Invoke(captureText);
    }
    
    public void OnBrainHint(BrainHint hint)
    {
        var outputType = MapSignalToOutputType(hint.Signal);

        var evt = new TimelineEvent
        {
            Type = outputType,
            Icon = EventIconMap.GetIcon(outputType),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(outputType),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(outputType),
            Summary = hint.Summary,
            Brain = new BrainMetadata
            {
                Signal = hint.Signal,
                Urgency = hint.Urgency,
                Evaluation = hint.Evaluation,
                EvalDelta = hint.EvalDelta,
                SuggestedAction = hint.SuggestedMove,
            },
        };
        
        _timeline.AddEvent(evt);
        PersistEventIfActive(evt);
        _topStrip?.Invoke(hint.Summary);
        TopStripUpdated?.Invoke(hint.Summary);

        if (_voiceAgent?.IsConnected == true
            && (_voiceDeliveryGate?.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert) ?? DeliveryDecision.Deliver) == DeliveryDecision.Deliver)
        {
            var voiceText = PrefixWithGrounding(FormatForVoice(hint));
            _ = _voiceAgent.SendContextualUpdateAsync(voiceText)
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[BrainEventRouter] Voice update failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    public void OnImageAnalysis(string analysisText)
    {
        // D-039: ImageAnalysis is diagnostic only — TopStrip, never timeline/ghost.
        _topStrip?.Invoke(analysisText);
        TopStripUpdated?.Invoke(analysisText);
    }

    /// <summary>
    /// Distributes a structured brain analysis into queued timeline events.
    /// Each non-null section is enqueued with the appropriate output type.
    /// ImageAnalysis is diagnostic only — routes to TopStrip (D-039), never timeline.
    /// Emission order: Danger, Assessment, SageAdvice (D-041).
    /// New batches append to the pending queue instead of clearing it (Mar 24, 2026).
    /// </summary>
    internal void OnStructuredAnalysis(BrainAnalysisResult analysis)
    {
        // D-039: ImageAnalysis routes to TopStrip only — handled by RouteBrainResult.

        var batch = new List<TimelineEvent>();

        if (!string.IsNullOrWhiteSpace(analysis.Threats))
            batch.Add(CreateEvent(EventOutputType.Danger, analysis.Threats));

        if (!string.IsNullOrWhiteSpace(analysis.PositionAssessment))
            batch.Add(CreateEvent(EventOutputType.Assessment, analysis.PositionAssessment));

        if (!string.IsNullOrWhiteSpace(analysis.SuggestedAction))
            batch.Add(CreateEvent(EventOutputType.SageAdvice, analysis.SuggestedAction));

        EnqueueBatch(batch);
    }

    private void EmitEvent(EventOutputType type, string content)
    {
        var evt = new TimelineEvent
        {
            Type = type,
            Icon = EventIconMap.GetIcon(type),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(type),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(type),
            Summary = Truncate(content, 80),
            FullContent = content,
        };
        _timeline.AddEvent(evt);
        PersistEventIfActive(evt);
    }
    
    public event Action<string>? TopStripUpdated;
    public event Action<string>? BrainChatReplyReceived;
    public event Action<ToolCallInfo>? ToolCallReceived;
    public event Action<TimelineEvent>? AnalysisEventEmitted;
    public event Action<IReadOnlyList<TimelineEvent>>? AnalysisBatchQueued;
    public event Action<BrainResult>? TerminalBrainErrorReceived;

    public void OnUserMessage(ChatMessage userMsg)
    {
        var evt = new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Icon = EventIconMap.GetIcon(EventOutputType.DirectMessage),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.DirectMessage),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.DirectMessage),
            Summary = Truncate(userMsg.Content, 60),
            FullContent = userMsg.Content,
            Role = MessageRole.User,
            LinkedMessage = userMsg,
        };
        _timeline.AddEvent(evt);
        PersistEventIfActive(evt);
    }

    public void OnDirectMessage(ChatMessage userMsg, ChatMessage brainResponse)
    {
        var userEvt = new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Icon = EventIconMap.GetIcon(EventOutputType.DirectMessage),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.DirectMessage),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.DirectMessage),
            Summary = Truncate(userMsg.Content, 60),
            FullContent = userMsg.Content,
            Role = MessageRole.User,
            LinkedMessage = userMsg,
        };
        _timeline.AddEvent(userEvt);
        PersistEventIfActive(userEvt);

        var replyEvt = new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Icon = EventIconMap.GetIcon(EventOutputType.DirectMessage),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.DirectMessage),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.DirectMessage),
            Summary = Truncate(brainResponse.Content, 60),
            FullContent = brainResponse.Content,
            Role = MessageRole.Assistant,
            LinkedMessage = brainResponse,
        };
        _timeline.AddEvent(replyEvt);
        PersistEventIfActive(replyEvt);
    }

    public void OnAssistantMessage(ChatMessage assistantMsg)
    {
        var evt = new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Icon = EventIconMap.GetIcon(EventOutputType.DirectMessage),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.DirectMessage),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.DirectMessage),
            Summary = Truncate(assistantMsg.Content, 60),
            FullContent = assistantMsg.Content,
            Role = MessageRole.Assistant,
            LinkedMessage = assistantMsg,
        };
        _timeline.AddEvent(evt);
        PersistEventIfActive(evt);
    }
    
    public void OnProactiveAlert(BrainHint hint, string commentary)
    {
        var outputType = MapSignalToOutputType(hint.Signal);

        var evt = new TimelineEvent
        {
            Type = outputType,
            Icon = EventIconMap.GetIcon(outputType),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(outputType),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(outputType),
            Summary = commentary,
            FullContent = commentary,
            Role = MessageRole.Proactive,
            Brain = new BrainMetadata
            {
                Signal = hint.Signal,
                Urgency = hint.Urgency,
                Evaluation = hint.Evaluation,
                EvalDelta = hint.EvalDelta,
                SuggestedAction = hint.SuggestedMove,
            },
        };
        _timeline.AddEvent(evt);
        PersistEventIfActive(evt);

        _topStrip?.Invoke(commentary);
        TopStripUpdated?.Invoke(commentary);

        if (_voiceAgent?.IsConnected == true && hint.Urgency == "high"
            && (_voiceDeliveryGate?.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert) ?? DeliveryDecision.Deliver) == DeliveryDecision.Deliver)
        {
            var voiceText = PrefixWithGrounding(FormatForVoice(hint));
            _ = _voiceAgent.SendContextualUpdateAsync(voiceText)
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[BrainEventRouter] Voice update failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
        }
        else if (_reminderQueue != null && hint.Urgency == "high")
        {
            var voiceText = PrefixWithGrounding(FormatForVoice(hint));
            _reminderQueue.Supersede(Models.Exchange.BargeInCategory.CallOut,
                new Models.Exchange.ReminderItem
                {
                    Content = voiceText,
                    Category = Models.Exchange.BargeInCategory.CallOut,
                });
        }
    }

    public void OnToolCall(ToolCallInfo toolCall)
    {
        if (TryRouteShowReplay(toolCall))
        {
            ToolCallReceived?.Invoke(toolCall);
            return;
        }

        var evt = new TimelineEvent
        {
            Type = EventOutputType.ToolCall,
            Icon = toolCall.Icon ?? EventIconMap.GetIcon(EventOutputType.ToolCall),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.ToolCall),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.ToolCall),
            Summary = toolCall.SummaryText,
            FullContent = toolCall.DurationMs > 0
                ? $"{toolCall.SummaryText} ({toolCall.DurationLabel})"
                : toolCall.SummaryText,
            ToolCall = toolCall,
        };
        _timeline.AddEvent(evt);
        PersistEventIfActive(evt);
        ToolCallReceived?.Invoke(toolCall);
    }

    private bool TryRouteShowReplay(ToolCallInfo toolCall)
    {
        if (!string.Equals(toolCall.ToolName, "show_replay", StringComparison.Ordinal) ||
            !toolCall.Success ||
            string.IsNullOrWhiteSpace(toolCall.OutputJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(toolCall.OutputJson);
            var root = doc.RootElement;
            if (!string.Equals(root.GetProperty("status").GetString(), "success", StringComparison.Ordinal))
                return false;

            var filePath = root.GetProperty("filePath").GetString();
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var startTime = root.GetProperty("startTime").GetDouble();
            var duration = root.GetProperty("duration").GetDouble();
            var title = root.TryGetProperty("title", out var titleProp)
                ? titleProp.GetString()
                : null;

            var evt = new TimelineEvent
            {
                Type = EventOutputType.VideoCard,
                Summary = title ?? "Replay",
                Media = new MediaContent
                {
                    Type = MediaContentType.Video,
                    FilePath = filePath,
                    StartTime = startTime,
                    Duration = duration,
                    Title = title
                }
            };

            _timeline.AddEvent(evt);
            PersistEventIfActive(evt);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BrainEventRouter] show_replay routing failed: {ex.Message}");
            return false;
        }
    }

    public void OnGeneralChat(string text)
    {
        var evt = new TimelineEvent
        {
            Type = EventOutputType.GeneralChat,
            Icon = EventIconMap.GetIcon(EventOutputType.GeneralChat),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.GeneralChat),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.GeneralChat),
            Summary = Truncate(text, 80),
            FullContent = text,
        };
        _timeline.AddEvent(evt);
        PersistEventIfActive(evt);
        _topStrip?.Invoke(text);
        TopStripUpdated?.Invoke(text);
    }

    public void OnError(string message)
    {
        var evt = new TimelineEvent
        {
            Type = EventOutputType.SystemError,
            Icon = EventIconMap.GetIcon(EventOutputType.SystemError),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.SystemError),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.SystemError),
            Summary = Truncate(message, 80),
            FullContent = message,
            Role = MessageRole.System,
        };
        _timeline.AddEvent(evt);
        PersistEventIfActive(evt);
    }

    #region Channel Consumer

    public void StartConsuming(ChannelReader<BrainResult> reader, CancellationToken ct)
    {
        StopConsuming(); // Idempotent — stop any prior loop
        _consumeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _consumeTask = Task.Run(() => ConsumeBrainResultsAsync(reader, _consumeCts.Token));
    }

    public void StopConsuming()
    {
        _consumeCts?.Cancel();
        _consumeCts?.Dispose();
        _consumeCts = null;
        // Don't await _consumeTask — it's fire-and-forget with internal error handling
        _consumeTask = null;

        // Stop emission queue timer
        _emissionCts?.Cancel();
        _emissionCts?.Dispose();
        _emissionCts = null;
        _emissionTimer?.Dispose();
        _emissionTimer = null;
        _emissionTask = null;
    }

    private async Task ConsumeBrainResultsAsync(ChannelReader<BrainResult> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var result in reader.ReadAllAsync(ct))
            {
                try
                {
                    Console.WriteLine($"[Router] <<< RECEIVED result type={result.Type} corr={result.CorrelationId ?? "none"} narration={(result.VoiceNarration?[..Math.Min(50, result.VoiceNarration.Length)] ?? "null")}");
                    RouteBrainResult(result);
                    Console.WriteLine($"[Router] --- ROUTED result corr={result.CorrelationId ?? "none"}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Router] !!! ERROR routing result {result.Type}: {ex.Message}\n{ex.StackTrace}");
                    System.Diagnostics.Debug.WriteLine(
                        $"[BrainEventRouter] Error routing result {result.Type}: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { /* Normal shutdown */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BrainEventRouter] Consumer loop crashed: {ex.Message}");
        }
    }

    private void RouteBrainResult(BrainResult result)
    {
        _telemetry?.TrackEvent("router", "result_routed", new Dictionary<string, string>
        {
            ["type"] = result.Type.ToString(),
            ["correlationId"] = result.CorrelationId ?? "none"
        });

        // Phase 12D: Tagged deferred response — bypass VoiceDeliveryGate intentionally.
        // The user explicitly asked a question and received a deferral ack.
        // Delivery must not be suppressed by barge-in or exchange state checks.
        // If user starts speaking, the provider's own interrupt handler (Interrupted event) handles it.
        if (result.IsDeferredAnswer && result.VoiceNarration != null)
        {
            if (_exchangeManager?.IsExchangeActive == true)
            {
                // Exchange still active (including AwaitingBrain) — deliver immediately + prompt voice to speak
                if (_voiceAgent?.IsConnected == true)
                {
                    var voiceText = PrefixWithGrounding(result.VoiceNarration);
                    _ = _voiceAgent.SendContextualUpdateWithResponseAsync(voiceText)
                        .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                            $"[BrainEventRouter] Deferred response delivery failed: {t.Exception?.GetBaseException().Message}"),
                            TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            else
            {
                // Exchange expired — queue as high-priority reminder
                _reminderQueue?.Supersede(BargeInCategory.ToolExecution,
                    new ReminderItem
                    {
                        Content = result.VoiceNarration,
                        Category = BargeInCategory.ToolExecution,
                    });
            }
            // Don't return — continue processing for timeline/context routing
        }

        if (result.ToolCalls is { Count: > 0 })
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    foreach (var toolCall in result.ToolCalls)
                    {
                        OnToolCall(toolCall);
                    }
                }
                catch (Exception ex)
                {
                    CrashLogger.LogMainThreadException("RouteBrainResult.ToolCalls", ex);
                }
            });
        }

        // Parse structured analysis once, reuse for display/confidence/journal
        // Layer 1: strict JSON. Layer 2: heuristic labeled-text recovery.
        BrainAnalysisResult? structured = null;
        var packRouted = false;

        if (result.Type == BrainResultType.ImageAnalysis && result.AnalysisText != null)
        {
            structured = TryParseStructuredAnalysis(result.AnalysisText)
                      ?? StructuredBrainAnalysisParser.TryParseLabeledText(result.AnalysisText);

            // Pack-driven routing — takes precedence when an active pack is set
            var pack = _packService?.ActivePack;
            Console.WriteLine($"[Router] Pack check: _packService={(_packService != null ? "present" : "NULL")}, ActivePack={pack?.Id ?? "NULL"}");
            if (pack != null)
            {
                try
                {
                    var doc = JsonDocument.Parse(result.AnalysisText);
                    var root = doc.RootElement.Clone();
                    doc.Dispose();

                    // Populate Observations dictionary on structured result
                    var observations = new Dictionary<string, JsonElement>();
                    foreach (var prop in root.EnumerateObject())
                        observations[prop.Name] = prop.Value.Clone();

                    if (structured != null)
                        structured.Observations = observations;

                    // TopStrip from pack field
                    if (!string.IsNullOrEmpty(pack.TopStripField) && root.TryGetProperty(pack.TopStripField, out var stripValue))
                    {
                        var stripText = stripValue.GetString() ?? "";
                        _topStrip?.Invoke(Truncate(stripText, 80));
                        TopStripUpdated?.Invoke(Truncate(stripText, 80));
                    }

                    // Visual events: follow eventMapping order (D-041 emission priority)
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            var batch = new List<TimelineEvent>();
                            foreach (var mapping in pack.EventMapping)
                            {
                                if (!root.TryGetProperty(mapping.Field, out var val)) continue;
                                // Skip empty arrays/objects (e.g., "system_alerts": [], "player_kills": [])
                                if (val.ValueKind == JsonValueKind.Array && val.GetArrayLength() == 0) continue;
                                if (val.ValueKind == JsonValueKind.Object && !val.EnumerateObject().Any()) continue;
                                var text = val.ValueKind == JsonValueKind.String ? val.GetString() : val.GetRawText();
                                if (string.IsNullOrWhiteSpace(text)) continue;
                                if (!Enum.TryParse<EventOutputType>(mapping.EventType, out var eventType)) continue;

                                batch.Add(CreateEvent(eventType, text!));
                            }
                            Console.WriteLine($"[Router] Pack batch: {batch.Count} events from {pack.EventMapping.Count} mappings");
                            EnqueueBatch(batch);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Router] !!! Pack routing error: {ex.Message}");
                            CrashLogger.LogMainThreadException("RouteBrainResult.PackRouting", ex);
                        }
                    });

                    // Temporal events: create BrainEvents for L1 context
                    var fieldLookup = pack.ObservationSchema.Fields.ToDictionary(f => f.Key);
                    if (_brainContext != null)
                    {
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (!fieldLookup.TryGetValue(prop.Name, out var field)) continue;
                            if (field.Route != "temporal") continue;
                            var text = prop.Value.ValueKind == JsonValueKind.String
                                ? prop.Value.GetString()
                                : prop.Value.GetRawText();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                _ = _brainContext.IngestEventAsync(new BrainEvent
                                {
                                    Category = prop.Name,
                                    Text = text!,
                                    TimestampUtc = DateTime.UtcNow
                                }).ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                                    $"[BrainEventRouter] Pack L1 ingest failed: {t.Exception?.GetBaseException().Message}"),
                                    TaskContinuationOptions.OnlyOnFaulted);
                            }
                        }
                    }

                    // Voice grounding: populate generic Observations
                    if (_voiceGrounding != null)
                    {
                        var groundingObs = new Dictionary<string, string>();
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (!fieldLookup.TryGetValue(prop.Name, out var field)) continue;
                            if (field.Route is "visual" or "temporal")
                            {
                                var text = prop.Value.ValueKind == JsonValueKind.String
                                    ? prop.Value.GetString()
                                    : prop.Value.GetRawText();
                                if (!string.IsNullOrWhiteSpace(text))
                                    groundingObs[prop.Name] = text!;
                            }
                        }

                        _voiceGrounding.UpdateGroundedContext(new GroundedVoiceContext
                        {
                            Observations = groundingObs,
                            // Also populate legacy typed fields for backward compat
                            PositionAssessment = observations.TryGetValue("position_assessment", out var pa) && pa.ValueKind == JsonValueKind.String ? pa.GetString() : null,
                            Threats = observations.TryGetValue("threats", out var th) && th.ValueKind == JsonValueKind.String ? th.GetString() : null,
                            SuggestedAction = observations.TryGetValue("suggested_action", out var sa) && sa.ValueKind == JsonValueKind.String ? sa.GetString() : null,
                            Fen = observations.TryGetValue("fen", out var fen) && fen.ValueKind == JsonValueKind.String ? fen.GetString() : null,
                            Confidence = observations.TryGetValue("confidence", out var conf) && conf.ValueKind == JsonValueKind.String ? conf.GetString() : null,
                            CapturedAtUtc = result.CreatedAt.UtcDateTime,
                        });
                    }

                    packRouted = true;
                }
                catch (JsonException)
                {
                    // JSON parse failed — fall through to legacy path
                }
            }
        }

        if (!packRouted) switch (result.Type)
        {
            case BrainResultType.ImageAnalysis:
                if (result.AnalysisText != null)
                {
                    if (structured != null)
                    {
                        // Structured output: distribute sections into distinct timeline events
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try { OnStructuredAnalysis(structured); }
                            catch (Exception ex) { CrashLogger.LogMainThreadException("RouteBrainResult.StructuredAnalysis", ex); }
                        });
                    }
                    else
                    {
                        // Unstructured fallback: sanitize before display to strip markdown artifacts
                        var sanitized = StructuredBrainAnalysisParser.SanitizeFallbackText(result.AnalysisText);
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try { OnImageAnalysis(sanitized); }
                            catch (Exception ex) { CrashLogger.LogMainThreadException("RouteBrainResult.ImageAnalysis", ex); }
                        });
                    }
                    // TopStrip gets a concise summary regardless
                    var stripText = structured?.PositionAssessment
                        ?? Truncate(StructuredBrainAnalysisParser.SanitizeFallbackText(result.AnalysisText), 80);
                    _topStrip?.Invoke(stripText);
                    TopStripUpdated?.Invoke(stripText);

                    // Update voice grounding cache with latest brain-derived facts
                    if (_voiceGrounding != null)
                    {
                        _voiceGrounding.UpdateGroundedContext(new GroundedVoiceContext
                        {
                            PositionAssessment = structured?.PositionAssessment,
                            Threats = structured?.Threats,
                            SuggestedAction = structured?.SuggestedAction,
                            Fen = structured?.Fen ?? ExtractFenFromAnalysis(result.AnalysisText),
                            Confidence = structured?.Confidence,
                            CapturedAtUtc = result.CreatedAt.UtcDateTime,
                        });
                    }
                }
                break;

            case BrainResultType.ProactiveAlert:
                if (result.Hint != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try { OnProactiveAlert(result.Hint, result.AnalysisText ?? result.Hint.Summary); }
                        catch (Exception ex) { CrashLogger.LogMainThreadException("RouteBrainResult.ProactiveAlert", ex); }
                    });
                }
                break;

            case BrainResultType.ToolResult:
                if (result.AnalysisText != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try { OnGeneralChat(result.AnalysisText); }
                        catch (Exception ex) { CrashLogger.LogMainThreadException("RouteBrainResult.ToolResult", ex); }
                    });
                    BrainChatReplyReceived?.Invoke(result.AnalysisText);
                }
                break;

            case BrainResultType.Error:
                System.Diagnostics.Debug.WriteLine(
                    $"[BrainEventRouter] Brain error: {result.AnalysisText}");
                if (result.AnalysisText != null)
                {
                    if (ShouldRouteError(result))
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try { OnError(result.AnalysisText); }
                            catch (Exception ex) { CrashLogger.LogMainThreadException("RouteBrainResult.Error", ex); }
                        });
                    }

                    if (result.RequestDisconnect)
                    {
                        TerminalBrainErrorReceived?.Invoke(result);
                    }
                }
                break;
        }

        // Ingest as L1 event for context pipeline (BRAIN_VOICE_PIPELINE_RULES Section 4)
        if (_brainContext != null && result.AnalysisText != null)
        {
            var brainEvent = new BrainEvent
            {
                TimestampUtc = result.CreatedAt.UtcDateTime,
                Type = result.Type switch
                {
                    BrainResultType.ImageAnalysis => BrainEventType.VisionObservation,
                    BrainResultType.ProactiveAlert => BrainEventType.GameplayState,
                    BrainResultType.ToolResult => BrainEventType.GameplayState,
                    BrainResultType.Error => BrainEventType.SystemSignal,
                    _ => BrainEventType.SystemSignal
                },
                Category = result.Type switch
                {
                    BrainResultType.ImageAnalysis => "vision",
                    BrainResultType.ProactiveAlert => result.Hint?.Signal ?? "alert",
                    BrainResultType.ToolResult => "tool",
                    BrainResultType.Error => "error",
                    _ => "unknown"
                },
                Text = result.AnalysisText,
                Confidence = result.Type == BrainResultType.Error ? 0.0
                    : structured?.ConfidenceScore ?? 0.8,
                ValidFor = result.Type == BrainResultType.ImageAnalysis
                    ? TimeSpan.FromSeconds(30)
                    : TimeSpan.FromMinutes(2)
            };
            _ = _brainContext.IngestEventAsync(brainEvent)
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[BrainEventRouter] L1 ingest failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
        }

        // Journal ingestion — record every ImageAnalysis for move-by-move history
        if (_gameJournal != null && result.Type == BrainResultType.ImageAnalysis && result.AnalysisText != null)
        {
            var fen = structured?.Fen ?? ExtractFenFromAnalysis(result.AnalysisText);

            // Auto new-game detection (B2) — check BEFORE adding current entry
            if (fen != null)
            {
                CheckForNewGame(fen);
            }

            // Temporal consistency validation — flag but still record (no data loss)
            var validation = _gameJournal.ValidateTemporalConsistency(fen);
            if (!validation.IsConsistent)
            {
                _telemetry?.TrackEvent("journal", "temporal_inconsistency", new Dictionary<string, string>
                {
                    ["warning"] = validation.Warning ?? "unknown",
                    ["reason"] = validation.Reason,
                    ["fen"] = fen ?? "null"
                });
                System.Diagnostics.Debug.WriteLine($"[BrainEventRouter] Temporal inconsistency: {validation.Warning} — {validation.Reason}");
            }

            var journalDescription = structured?.ToDisplayText()
                ?? StructuredBrainAnalysisParser.SanitizeFallbackText(result.AnalysisText);
            _gameJournal.AddEntry(new GameJournalEntry(
                MoveNumber: _gameJournal.EntryCount + 1,
                Fen: fen,
                MoveNotation: structured?.LastMove,
                Description: journalDescription.Length > 200 ? journalDescription[..200] : journalDescription,
                Evaluation: null,
                Timestamp: result.CreatedAt
            ));
        }

        // Voice forwarding based on priority — all paths use PrefixWithGrounding + VoiceDeliveryGate
        // Skip if already delivered via deferred answer bypass (prevents double voice delivery)
        if (_voiceAgent?.IsConnected == true && result.VoiceNarration != null && !result.IsDeferredAnswer)
        {
            var decision = _voiceDeliveryGate?.ShouldDeliver(result.Priority, result.Type) ?? DeliveryDecision.Deliver;
            if (decision == DeliveryDecision.Deliver)
            {
                switch (result.Priority)
                {
                    case BrainResultPriority.Interrupt:
                        _ = _voiceAgent.SendContextualUpdateWithResponseAsync(PrefixWithGrounding($"[URGENT] {result.VoiceNarration}"))
                            .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                                $"[BrainEventRouter] Voice interrupt failed: {t.Exception?.GetBaseException().Message}"),
                                TaskContinuationOptions.OnlyOnFaulted);
                        break;
                    case BrainResultPriority.WhenIdle:
                        if (TryBuildVoiceUpdate(result.VoiceNarration, result.Priority, out var idleVoiceText))
                        {
                            _ = _voiceAgent.SendContextualUpdateAsync(idleVoiceText)
                                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                                    $"[BrainEventRouter] Voice update failed: {t.Exception?.GetBaseException().Message}"),
                                    TaskContinuationOptions.OnlyOnFaulted);
                        }
                        break;
                    // Silent: no voice forwarding
                }
            }
            else if (decision == DeliveryDecision.QueueReminder && _reminderQueue != null)
            {
                var category = result.Type switch
                {
                    BrainResultType.ProactiveAlert => BargeInCategory.CallOut,
                    BrainResultType.ToolResult => BargeInCategory.ToolExecution,
                    BrainResultType.ImageAnalysis => BargeInCategory.FreeCommentary,
                    _ => BargeInCategory.FreeCommentary,
                };
                _reminderQueue.Supersede(category, new ReminderItem
                {
                    Content = result.VoiceNarration,
                    Category = category,
                });
            }
        }
    }

    #endregion

    #region Emission Queue (D-040)

    private TimelineEvent CreateEvent(EventOutputType type, string content) => new()
    {
        Type = type,
        Icon = EventIconMap.GetIcon(type),
        CapsuleColorHex = EventIconMap.GetCapsuleColorHex(type),
        CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(type),
        Summary = Truncate(content, 80),
        FullContent = content,
    };

    /// <summary>
    /// Enqueues a batch of timeline events into the emission queue for drip-fed display.
    /// Fires AnalysisBatchQueued and starts the emission loop if needed.
    /// Must be called on the main thread.
    /// </summary>
    private void EnqueueBatch(List<TimelineEvent> batch)
    {
        if (batch.Count == 0) return;

        AnalysisBatchQueued?.Invoke(batch);

        lock (_emissionLock)
        {
            foreach (var evt in batch)
                _emissionQueue.Enqueue(evt);

            StartEmissionLoopIfNeeded();
        }
    }

    /// <summary>
    /// Dequeue and emit one event. Returns false if queue is empty.
    /// Called by the emission timer loop; exposed internal for testing.
    /// </summary>
    internal bool EmitNextQueued()
    {
        TimelineEvent? next;
        lock (_emissionLock)
        {
            if (!_emissionQueue.TryDequeue(out next))
                return false;
        }
        _timeline.AddEvent(next);
        PersistEventIfActive(next);
        AnalysisEventEmitted?.Invoke(next);
        return true;
    }

    private void StartEmissionLoopIfNeeded()
    {
        // Must be called inside _emissionLock
        if (_emissionTask != null && !_emissionTask.IsCompleted)
            return;

        _emissionCts?.Cancel();
        _emissionCts?.Dispose();
        _emissionCts = new CancellationTokenSource();
        _emissionTimer?.Dispose();
        _emissionTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(2500));
        var ct = _emissionCts.Token;

        _emissionTask = Task.Run(async () =>
        {
            try
            {
                while (await _emissionTimer!.WaitForNextTickAsync(ct))
                {
                    MainThread.BeginInvokeOnMainThread(() => EmitNextQueued());
                    lock (_emissionLock)
                    {
                        if (_emissionQueue.Count == 0)
                        {
                            _emissionTimer?.Dispose();
                            _emissionTimer = null;
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    #endregion

    #region Helpers
    
    private static EventOutputType MapSignalToOutputType(string signal) => signal switch
    {
        "danger" => EventOutputType.Danger,
        "blunder" => EventOutputType.Danger,
        "opportunity" => EventOutputType.Opportunity,
        "brilliant" => EventOutputType.Opportunity,
        "sage" => EventOutputType.SageAdvice,
        "assessment" => EventOutputType.Assessment,
        "detection" => EventOutputType.Detection,
        _ => EventOutputType.SageAdvice,
    };
    
    private static string FormatForVoice(BrainHint hint)
    {
        var parts = new List<string>
        {
            $"[BRAIN SIGNAL] {hint.Signal}",
            hint.Urgency,
            $"Eval: {hint.Evaluation}cp",
        };
        
        if (hint.EvalDelta.HasValue)
            parts.Add($"Delta: {hint.EvalDelta}cp");
        if (hint.SuggestedMove != null)
            parts.Add($"Suggested: {hint.SuggestedMove}");
        
        return string.Join(" | ", parts);
    }
    
    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..(maxLen - 1)] + "…";

    /// <summary>
    /// Prepends the voice grounding prefix to text sent to the conversation provider.
    /// Ensures all voice update paths carry grounding state consistently.
    /// </summary>
    private string PrefixWithGrounding(string text)
    {
        var prefix = _voiceGrounding?.GetGroundingPrefix();
        return prefix != null ? $"{prefix}\n{text}" : text;
    }

    /// <summary>
    /// Applies grounding prefix and suppresses repeated grounded idle updates when the
    /// factual board-state prefix has not changed recently. This keeps voice grounding
    /// strong while reducing repetitive context churn from near-identical analyses.
    /// </summary>
    private bool TryBuildVoiceUpdate(string text, BrainResultPriority priority, out string voiceText)
    {
        var prefix = _voiceGrounding?.GetGroundingPrefix();
        voiceText = prefix != null ? $"{prefix}\n{text}" : text;

        if (priority != BrainResultPriority.WhenIdle || string.IsNullOrWhiteSpace(prefix))
            return true;

        lock (_voiceDedupLock)
        {
            var nowUtc = DateTime.UtcNow;
            if (string.Equals(prefix, _lastGroundingPrefixSent, StringComparison.Ordinal) &&
                nowUtc - _lastGroundingPrefixSentAtUtc < GroundedIdleVoiceCooldown)
            {
                _telemetry?.TrackEvent("voice", "grounded_update_suppressed", new Dictionary<string, string>
                {
                    ["reason"] = "duplicate_grounding_prefix",
                    ["cooldown_seconds"] = ((int)GroundedIdleVoiceCooldown.TotalSeconds).ToString()
                });
                return false;
            }

            _lastGroundingPrefixSent = prefix;
            _lastGroundingPrefixSentAtUtc = nowUtc;
            return true;
        }
    }

    /// <summary>
    /// Attempts to parse brain analysis text as structured JSON.
    /// Returns null if text is not valid JSON or doesn't match schema.
    /// </summary>
    internal static BrainAnalysisResult? TryParseStructuredAnalysis(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text[0] != '{') return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<BrainAnalysisResult>(text);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Best-effort FEN extraction from analysis text using regex.
    /// Returns null if no FEN-like string is found.
    /// </summary>
    internal static string? ExtractFenFromAnalysis(string text)
    {
        var match = FenRegex.Match(text);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Auto new-game detection. Checks if the detected FEN is a starting position
    /// after previously seeing non-starting positions with meaningful history.
    /// On detection: stores previous game summary, clears journal, flushes L1, resets diff hash.
    /// </summary>
    private void CheckForNewGame(string detectedFen)
    {
        var isStartingPosition = IsStartingFen(detectedFen);
        var hasHistory = _gameJournal!.EntryCount > 2; // Need meaningful history
        var lastFen = _gameJournal.GetLatestFen();
        var lastWasNotStarting = lastFen != null && !IsStartingFen(lastFen);

        if (isStartingPosition && hasHistory && lastWasNotStarting)
        {
            // NEW GAME DETECTED
            System.Diagnostics.Debug.WriteLine("[BrainEventRouter] NEW GAME DETECTED — resetting context");

            // 1. Get summary of previous game before clearing
            var previousSummary = _gameJournal.GetSummary();

            // 2. Store summary via callback (e.g., brain service for next game's system prompt)
            _onNewGameDetected?.Invoke(previousSummary);

            // 3. Clear journal
            _gameJournal.Clear();

            // 4. Flush L1 events + voice transcript store
            _brainContext?.FlushEvents();
            _voiceTranscriptStore?.Flush();

            // 5. Reset frame diff hash
            _frameDiffService?.ResetHash();

            _telemetry?.TrackEvent("game", "new_game_detected",
                new Dictionary<string, string> { ["previous_moves"] = previousSummary.Length.ToString() });
        }
    }

    /// <summary>
    /// Checks if a FEN string represents the standard chess starting position.
    /// Compares board position only (first field before space), ignoring move counters.
    /// </summary>
    internal static bool IsStartingFen(string fen)
    {
        var boardPart = fen.Split(' ')[0];
        return boardPart == "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
    }

    /// <summary>Test-only access to IsStartingFen.</summary>
    internal static bool IsStartingFenForTest(string fen) => IsStartingFen(fen);

    /// <summary>Test-only access to RouteBrainResult (normally called from Channel consumer).</summary>
    internal void RouteBrainResultForTest(BrainResult result) => RouteBrainResult(result);

    private bool ShouldRouteError(BrainResult result)
    {
        var fingerprint = result.ErrorFingerprint ?? result.AnalysisText;
        if (string.IsNullOrWhiteSpace(fingerprint))
            return true;

        lock (_errorDedupLock)
        {
            var nowUtc = DateTime.UtcNow;
            if (string.Equals(fingerprint, _lastErrorFingerprint, StringComparison.Ordinal) &&
                nowUtc - _lastErrorFingerprintAtUtc <= ErrorDedupWindow)
            {
                return false;
            }

            _lastErrorFingerprint = fingerprint;
            _lastErrorFingerprintAtUtc = nowUtc;
            return true;
        }
    }

    #endregion
}
