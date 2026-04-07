using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.Services.History;
using WitnessDesktop.Services.Replay;
using WitnessDesktop.Models.Exchange;
using System.Collections.Generic;
using System.Threading;

namespace WitnessDesktop.ViewModels;

public partial class MainViewModel : ObservableObject, IQueryAttributable
{
    private sealed record GhostNotificationRequest(
        FabCardVariant Variant,
        string? Title,
        string? Text,
        string? ImagePath,
        bool IsAlert,
        bool IsVoiceDelivered,
        TimeSpan DisplayDuration);

    private readonly IAudioService _audioService;
    private readonly IWindowCaptureService _captureService;
    private readonly IConversationProvider _conversationProvider;
    private readonly IVisualReelService _visualReelService;
    private readonly IObservationStore? _observationStore;
    private readonly IObservationAdmissionGate _observationAdmissionGate;
    private readonly IBrainContextService _brainContextService;
    private readonly ISessionManager _sessionManager;
    private readonly ITimelineFeed _timelineFeed;
    private readonly IBrainEventRouter _brainEventRouter;
    private readonly IGhostModeService _ghostModeService;
    private readonly IBrainService _brainService;
    private readonly IFrameDiffService _frameDiffService;
    private readonly IStockfishService _stockfishService;
    private readonly IStructuralSettingsTracker _structuralSettingsTracker;
    private readonly ITelemetryService? _telemetry;
    private readonly ISessionTraceService? _sessionTrace;
    private readonly IVoiceGroundingCoordinator? _voiceGrounding;
    private readonly IVoiceTranscriptStore _voiceTranscriptStore;
    private readonly ISessionHistoryService? _historyService;
    private readonly IGameSkillPackService? _packService;
    private readonly IReplayRecordingService? _replayRecording;
    private readonly IReplayAnalysisOrchestrator? _replayAnalysisOrchestrator;
    private readonly IExchangeManager? _exchangeManager;
    private readonly Services.Audio.IWakePhraseDetector? _wakePhraseDetector;
    private readonly Services.Audio.IPorcupineWakeDetector? _porcupineWakeDetector;
    private readonly Services.Audio.IAgentSpeechTracker? _agentSpeechTracker;
    private readonly Services.Audio.IUserSpeechDetector? _userSpeechDetector;
    private readonly Services.Audio.ISfxPlayer? _sfxPlayer;
    private readonly IReminderQueue? _reminderQueue;
    private readonly IBargeInPolicyService? _bargeInPolicyService;
    private readonly IBrainRequestChannel? _brainRequestChannel;
    private readonly IGaimerTeamService? _gaimerTeam;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    private readonly SemaphoreSlim _stopSessionLock = new(1, 1);
    private CancellationTokenSource? _sessionCts;
    private DateTime _sessionStartedAt = DateTime.UtcNow;
    private ChatMessage? _pendingUserMessage;

    /// <summary>
    /// Suppresses auto-response audio when the user speaks without the wake phrase.
    /// Set true on transcript without wake phrase, cleared when wake phrase detected
    /// or exchange opens. Prevents the AI from responding to ambient speech.
    /// </summary>
    private volatile bool _suppressUnwokenResponse;
    private DateTime _pendingUserMessageAt = DateTime.MinValue;
    private static readonly TimeSpan PendingMessageTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TypedLegacyBridgeDedupWindow = TimeSpan.FromMilliseconds(250);
    private volatile bool _navigateToMinimalOnConnected;
    private bool _hasTappedGhostButton;
    private string _lastSystemError = string.Empty;
    private DateTime _lastSystemErrorAt = DateTime.MinValue;
    private DateTime _lastVadForward = DateTime.MinValue;
    private string? _lastTypedProviderMessageContent;
    private string? _lastTypedProviderName;
    private DateTime _lastTypedProviderMessageAt = DateTime.MinValue;
    private int _lastGhostAudioToggleIndex = -1;
    private bool _lastGhostAudioToggleValue;
    private DateTime _lastGhostAudioToggleAt = DateTime.MinValue;
    private readonly object _ghostNotificationLock = new();
    private readonly Queue<GhostNotificationRequest> _ghostNotificationQueue = new();
    private CancellationTokenSource? _ghostNotificationCts;
    private Task? _ghostNotificationTask;
    private static readonly TimeSpan GhostAudioToggleDedupWindow = TimeSpan.FromMilliseconds(250);

    [ObservableProperty]
    private Agent? _selectedAgent;

    [ObservableProperty]
    private IReadOnlyList<CaptureTarget> _captureTargets = [];

    [ObservableProperty]
    private CaptureTarget? _selectedTarget;

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    private float _inputVolume;

    [ObservableProperty]
    private float _outputVolume;

    // Used by simple UI visualizers (e.g., MinimalView bars).
    public float ActivityVolume => Math.Max(InputVolume, OutputVolume);

    [ObservableProperty]
    private bool _isGameSelectorCollapsed;

    [ObservableProperty]
    private bool _isWindowPickerOpen;

    [ObservableProperty]
    private bool _isPageReady;

    [ObservableProperty]
    private SlidingPanelContent? _slidingPanelContent;

    [ObservableProperty]
    private bool _isFabActive;

    [ObservableProperty]
    private FabCardVariant _fabCardVariant = FabCardVariant.None;

    [ObservableProperty]
    private bool _isVoiceChatActive;

    [ObservableProperty]
    private bool _isCommentaryActive;

    private bool _isBargeInActive;
    public bool IsBargeInActive
    {
        get => _isBargeInActive;
        set
        {
            if (_isBargeInActive == value) return;
            _isBargeInActive = value;
            OnPropertyChanged();
            _bargeInPolicyService?.SetEnabled(value);
        }
    }

    [ObservableProperty]
    private bool _isAiMicActive;

    [ObservableProperty]
    private bool _isAudioInActive;

    [ObservableProperty]
    private bool _isVoiceChatPending;

    private bool _suppressVoiceChatToggle;
    private bool _suppressGhostAudioSync;
    private bool _suppressUnsupportedToggle;
    private bool _suppressCloseExchangeSfx;

    /// <summary>Fired when user toggles an audio feature the current agent doesn't support. Arg is the toggle display name.</summary>
    public event EventHandler<string>? UnsupportedAudioFeatureToggled;

    [ObservableProperty]
    private byte[]? _previewImage;

    private ImageSource? _previewImageSource;

    public bool HasPreviewImage => PreviewImage is { Length: > 0 };

    public ImageSource? PreviewImageSource => _previewImageSource;

    [ObservableProperty]
    private AiDisplayContent? _aiDisplayContent;

    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
    
    public ITimelineFeed? TimelineFeed => _timelineFeed;

    [ObservableProperty]
    private string _messageDraftText = string.Empty;

    public string ConnectionBadgeText => ConnectionState switch
    {
        ConnectionState.Disconnected => "OFFLINE",
        ConnectionState.Connecting => "CONNECTING",
        ConnectionState.Connected => "CONNECTED",
        ConnectionState.Disconnecting => "DISCONNECTING",
        ConnectionState.Reconnecting => "RECONNECTING",
        ConnectionState.Error => "ERROR",
        _ => "UNKNOWN"
    };

    public Color ConnectionBadgeColor => ConnectionState switch
    {
        ConnectionState.Disconnected => Color.FromArgb("#6b7280"),
        ConnectionState.Connecting => Color.FromArgb("#eab308"),
        ConnectionState.Connected => Color.FromArgb("#22c55e"),
        ConnectionState.Disconnecting => Color.FromArgb("#eab308"),
        ConnectionState.Reconnecting => Color.FromArgb("#eab308"),
        ConnectionState.Error => Color.FromArgb("#ef4444"),
        _ => Color.FromArgb("#6b7280")
    };

    public string ConnectButtonText => ConnectionState switch
    {
        ConnectionState.Disconnected => "CONNECT",
        ConnectionState.Connecting => "CONNECTING...",
        ConnectionState.Connected => "DISCONNECT",
        ConnectionState.Disconnecting => "DISCONNECTING...",
        ConnectionState.Reconnecting => "RECONNECTING...",
        _ => "CONNECT"
    };

    // Connect requires BOTH agent AND game selection
    public bool CanConnect => SelectedAgent != null && SelectedTarget != null && ConnectionState == ConnectionState.Disconnected;

    /// <summary>True when agent selected and input has non-empty text. Supports out-game chat.</summary>
    public bool CanSendTextMessage => SelectedAgent != null && !string.IsNullOrWhiteSpace(MessageDraftText);

    /// <summary>Placeholder for chat input. Changes based on agent selection.</summary>
    public string ChatInputPlaceholder => SelectedAgent != null ? $"Ask {SelectedAgent.Name}..." : "Select an agent to chat";
    public bool HasSelectedTarget => SelectedTarget != null;
    public bool IsConnected => ConnectionState == ConnectionState.Connected;
    public bool IsConnecting => ConnectionState == ConnectionState.Connecting;
    public bool RequiresAppRebootstrap => _structuralSettingsTracker.RequiresRebootstrap;
    public bool CanRestartSessionShallow => !RequiresAppRebootstrap;

    /// <summary>True when using a real API backend (not mock). Controls power button green/gray state.</summary>
    public bool IsLive => _brainService.ProviderName != "Mock Brain";

    /// <summary>Name of the active brain provider for diagnostics display.</summary>
    public string BrainProviderName => _brainService.ProviderName;
    public bool HasAiContent => AiDisplayContent != null;
    public bool HasNoAiContent => AiDisplayContent == null;
    public bool HasPanelContent => SlidingPanelContent != null;
    public bool HasTextPanelContent => SlidingPanelContent != null && !SlidingPanelContent.IsToolCall;

    // FAB overlay computed properties
    public bool IsFabEnabled => IsConnected;
    public bool IsGhostActive => IsConnected && IsFabActive;

    /// <summary>True when connected and user hasn't tapped ghost button yet. Appends hint to live messages.</summary>
    public bool ShowGhostHint => IsConnected && !_hasTappedGhostButton;
    public bool IsFabCardVisible => IsFabActive && FabCardVariant != FabCardVariant.None;
    public bool ShowVoiceCard => FabCardVariant == FabCardVariant.Voice;
    public bool ShowTextCard => FabCardVariant == FabCardVariant.Text || FabCardVariant == FabCardVariant.TextWithImage;
    public bool ShowCardImage => FabCardVariant == FabCardVariant.TextWithImage;

    // Alias for SelectedTarget to match MinimalView binding
    public CaptureTarget? CurrentTarget => SelectedTarget;

    public string GeminiBackendText => _conversationProvider.ProviderName;

    public MainViewModel(
        IAudioService audioService,
        IWindowCaptureService captureService,
        IConversationProvider conversationProvider,
        IVisualReelService visualReelService,
        IObservationAdmissionGate observationAdmissionGate,
        IBrainContextService brainContextService,
        ISessionManager sessionManager,
        ITimelineFeed timelineFeed,
        IBrainEventRouter brainEventRouter,
        IGhostModeService ghostModeService,
        IBrainService brainService,
        IFrameDiffService frameDiffService,
        IStockfishService stockfishService,
        IStructuralSettingsTracker structuralSettingsTracker,
        ITelemetryService? telemetry = null,
        ISessionTraceService? sessionTrace = null,
        IVoiceGroundingCoordinator? voiceGrounding = null,
        IVoiceTranscriptStore? voiceTranscriptStore = null,
        IObservationStore? observationStore = null,
        ISessionHistoryService? historyService = null,
        IGameSkillPackService? packService = null,
        IReplayRecordingService? replayRecording = null,
        IReplayAnalysisOrchestrator? replayAnalysisOrchestrator = null,
        IExchangeManager? exchangeManager = null,
        Services.Audio.IWakePhraseDetector? wakePhraseDetector = null,
        Services.Audio.IPorcupineWakeDetector? porcupineWakeDetector = null,
        Services.Audio.IAgentSpeechTracker? agentSpeechTracker = null,
        Services.Audio.IUserSpeechDetector? userSpeechDetector = null,
        Services.Audio.ISfxPlayer? sfxPlayer = null,
        IReminderQueue? reminderQueue = null,
        IBargeInPolicyService? bargeInPolicyService = null,
        IBrainRequestChannel? brainRequestChannel = null,
        IGaimerTeamService? gaimerTeam = null)
    {
        _audioService = audioService;
        _captureService = captureService;
        _conversationProvider = conversationProvider;
        _visualReelService = visualReelService;
        _observationStore = observationStore;
        _observationAdmissionGate = observationAdmissionGate;
        _brainContextService = brainContextService;
        _sessionManager = sessionManager;
        _timelineFeed = timelineFeed;
        _brainEventRouter = brainEventRouter;
        _ghostModeService = ghostModeService;
        _brainService = brainService;
        _frameDiffService = frameDiffService;
        _stockfishService = stockfishService;
        _structuralSettingsTracker = structuralSettingsTracker;
        _telemetry = telemetry;
        _sessionTrace = sessionTrace;
        _voiceGrounding = voiceGrounding;
        _voiceTranscriptStore = voiceTranscriptStore ?? new VoiceTranscriptStore();
        _historyService = historyService;
        _packService = packService;
        _replayRecording = replayRecording;
        _replayAnalysisOrchestrator = replayAnalysisOrchestrator;
        _exchangeManager = exchangeManager;
        _wakePhraseDetector = wakePhraseDetector;
        _porcupineWakeDetector = porcupineWakeDetector;
        _agentSpeechTracker = agentSpeechTracker;
        _userSpeechDetector = userSpeechDetector;
        _sfxPlayer = sfxPlayer;
        _reminderQueue = reminderQueue;
        _bargeInPolicyService = bargeInPolicyService;
        _brainRequestChannel = brainRequestChannel;
        _gaimerTeam = gaimerTeam;

        // Wire replay analysis: forward completed segments to the orchestrator
        if (_replayRecording != null && _replayAnalysisOrchestrator != null)
        {
            _replayRecording.SegmentCompleted += (_, e) => _replayAnalysisOrchestrator.EnqueueSegment(e.Segment);
        }

        // Wire exchange state changes to ghost FAB (Phase 12B)
        if (_exchangeManager != null)
        {
            // Default to TextOnly until voice connects (12E graceful degradation)
            _exchangeManager.SetMode(AudioIntelligenceMode.TextOnly);

            _exchangeManager.ExchangeStateChanged += (_, state) =>
            {
                _ghostModeService?.SetExchangeState((int)state);
            };
        }

        // Play wake confirmation ping when an exchange opens (Phase 12C)
        if (_exchangeManager != null)
        {
            _exchangeManager.ExchangeOpened += (_, session) =>
            {
                _ = _sfxPlayer?.PlayAsync("affirmation_ping.mp3", volume: 0.25f);
            };
        }

        // Surface one queued reminder when an exchange opens (Phase 12C, spec Section 8.3)
        if (_exchangeManager != null)
        {
            _exchangeManager.ExchangeOpened += (_, session) =>
            {
                _reminderQueue?.PruneStale(TimeSpan.FromMinutes(5));

                var reminder = _reminderQueue?.Dequeue();
                if (reminder != null && _conversationProvider?.IsConnected == true)
                {
                    _ = _conversationProvider.SendContextualUpdateAsync(
                        $"[REMINDER from earlier: {reminder.Content}]");
                }
            };
        }

        // Play close SFX when an exchange closes (silence timeout or manual)
        if (_exchangeManager != null)
        {
            _exchangeManager.ExchangeClosed += (_, session) =>
            {
                if (!_suppressCloseExchangeSfx)
                    _ = _sfxPlayer?.PlayAsync("close_exchange.wav", volume: 0.25f);
            };
        }

        // Wire Porcupine audio-based wake word detection (primary — D-AI-7)
        if (_porcupineWakeDetector?.IsAvailable == true)
        {
            _porcupineWakeDetector.WakeWordDetected += (_, keyword) =>
            {
                if (_exchangeManager != null && !_exchangeManager.IsExchangeActive && _selectedAgent != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Porcupine] Wake detected for '{keyword}' — opening exchange");
                    _exchangeManager.OnWakeDetected(_selectedAgent.Name);
                }
            };
        }

        // Wire user speech detector to exchange manager (Phase 12B)
        if (_userSpeechDetector != null)
        {
            _userSpeechDetector.UserSpeechStarted += (_, _) =>
            {
                _exchangeManager?.OnUserSpeech();
            };
        }

        // Wire Gaimer Team result handler (Phase A, C2/H1/M6 fixes)
        if (_gaimerTeam != null)
        {
            _gaimerTeam.TaskCompleted += async (_, e) =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        if (e.Result.Status == "complete")
                        {
                            if (_conversationProvider?.IsConnected == true)
                            {
                                _ = _conversationProvider.SendContextualUpdateWithResponseAsync(
                                    $"The team's back. {e.Result.Response}");
                            }

                            _timelineFeed.AddEvent(new Models.Timeline.TimelineEvent
                            {
                                Type = Models.Timeline.EventOutputType.TeamResult,
                                Summary = e.Result.Response,
                                Icon = Models.Timeline.EventIconMap.GetIcon(Models.Timeline.EventOutputType.TeamResult),
                                CapsuleColorHex = Models.Timeline.EventIconMap.GetCapsuleColorHex(Models.Timeline.EventOutputType.TeamResult),
                                CapsuleStrokeHex = Models.Timeline.EventIconMap.GetCapsuleStrokeHex(Models.Timeline.EventOutputType.TeamResult),
                            });

                            _sessionTrace?.TrackEvent("gaimer_team.task_completed", new Dictionary<string, string>
                            {
                                ["task_id"] = e.Result.TaskId,
                                ["status"] = "complete"
                            });
                        }
                        else
                        {
                            if (_conversationProvider?.IsConnected == true)
                            {
                                _ = _conversationProvider.SendContextualUpdateWithResponseAsync(
                                    "The team ran into an issue with that one.");
                            }

                            _timelineFeed.AddEvent(new Models.Timeline.TimelineEvent
                            {
                                Type = Models.Timeline.EventOutputType.TeamResult,
                                Summary = $"Team error: {e.Result.Response}",
                                Icon = Models.Timeline.EventIconMap.GetIcon(Models.Timeline.EventOutputType.TeamResult),
                                CapsuleColorHex = "#30808080",
                                CapsuleStrokeHex = "#50808080",
                            });

                            _sessionTrace?.TrackEvent("gaimer_team.task_completed", new Dictionary<string, string>
                            {
                                ["task_id"] = e.Result.TaskId,
                                ["status"] = "error",
                                ["error_code"] = e.Result.ErrorCode ?? "unknown"
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GaimerTeam] TaskCompleted handler error: {ex.Message}");
                }
            };

            _gaimerTeam.TaskProgress += (_, e) =>
            {
                try
                {
                    _sessionTrace?.TrackEvent("gaimer_team.task_progress", new Dictionary<string, string>
                    {
                        ["task_id"] = e.TaskId,
                        ["message"] = e.Message
                    });
                    System.Diagnostics.Debug.WriteLine(
                        $"[GaimerTeam] Progress on {e.TaskId}: {e.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GaimerTeam] TaskProgress handler error: {ex.Message}");
                }
            };
        }

        _structuralSettingsTracker.StateChanged += OnStructuralSettingsStateChanged;

        // Subscribe to brain chat replies for in-game text routing (A2+A4)
        _brainEventRouter.BrainChatReplyReceived += OnBrainChatReply;

        // Subscribe to brain activity updates for live activity bar
        _brainEventRouter.TopStripUpdated += (text) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    AiDisplayContent = new AiDisplayContent { Text = text };
                    OnPropertyChanged(nameof(HasAiContent));
                    OnPropertyChanged(nameof(HasNoAiContent));
                }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("TopStripUpdated", ex); }
            });

        // Subscribe to tool-call events for ghost card rendering
        _brainEventRouter.ToolCallReceived += (toolCall) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (!_ghostModeService.IsGhostModeActive && !IsFabActive) return;

                    SlidingPanelContent = new SlidingPanelContent
                    {
                        Title = "TOOL",
                        Text = toolCall.SummaryText,
                        ToolCall = toolCall,
                        AutoDismissMs = 3000
                    };
                    OnPropertyChanged(nameof(HasPanelContent));
                    OnPropertyChanged(nameof(HasTextPanelContent));

                    if (IsFabActive)
                        FabCardVariant = FabCardVariant.TextWithImage;

                    if (_ghostModeService.IsGhostModeActive)
                    {
                        EnqueueGhostNotification(new GhostNotificationRequest(
                            Variant: FabCardVariant.TextWithImage,
                            Title: "TOOL",
                            Text: toolCall.SummaryText,
                            ImagePath: toolCall.Icon,
                            IsAlert: false,
                            IsVoiceDelivered: false,
                            // Tool cards carry an icon plus text, so they get a slightly longer dwell.
                            DisplayDuration: TimeSpan.FromSeconds(1.2)));
                    }
                }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("ToolCallReceived", ex); }
            });

        // Subscribe to drip-fed analysis events for ghost/FAB card rendering
        _brainEventRouter.AnalysisEventEmitted += (evt) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (!_ghostModeService.IsGhostModeActive && !IsFabActive) return;

                    if (_ghostModeService.IsGhostModeActive)
                    {
                        return;
                    }

                    SlidingPanelContent = new SlidingPanelContent
                    {
                        Title = evt.Type.ToString(),
                        Text = evt.FullContent ?? evt.Summary,
                        AutoDismissMs = 0 // Analysis cards stay until replaced
                    };
                    OnPropertyChanged(nameof(HasPanelContent));
                    OnPropertyChanged(nameof(HasTextPanelContent));
                }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("AnalysisEventEmitted", ex); }
            });

        _brainEventRouter.AnalysisBatchQueued += (events) =>
        {
            if (!_ghostModeService.IsGhostModeActive || events.Count == 0)
                return;

            EnqueueGhostNotifications(events.Select(evt => new GhostNotificationRequest(
                Variant: FabCardVariant.Text,
                Title: evt.Type.ToString(),
                Text: evt.FullContent ?? evt.Summary,
                ImagePath: null,
                IsAlert: false,
                IsVoiceDelivered: false,
                DisplayDuration: TimeSpan.FromSeconds(1))));
        };

        _brainEventRouter.TerminalBrainErrorReceived += result =>
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await HandleTerminalBrainErrorAsync(result); }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("TerminalBrainErrorReceived", ex); }
            });

        // Audio callbacks can arrive on background threads; marshal to UI thread.
        _audioService.VolumeChanged += (_, e) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    InputVolume = e.InputVolume;
                    OutputVolume = e.OutputVolume;
                    _userSpeechDetector?.OnLevelChanged(e.InputVolume);
                }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("VolumeChanged", ex); }
            });
        _audioService.ErrorOccurred += (_, e) =>
        {
            // Keep UI stable; consumers can surface these later.
            System.Diagnostics.Debug.WriteLine($"[Audio] {e.Message} {e.Exception}");
        };
        _captureService.FrameCaptured += (_, rawFrame) =>
        {
            var sourceTarget = _captureService.CurrentTarget != null
                ? $"{_captureService.CurrentTarget.ProcessName}|{_captureService.CurrentTarget.WindowTitle}"
                : "unknown";
            var capturedAtUtc = DateTime.UtcNow;

            // 1. Build the cheap preview-sized artifact first.
            // Use this smaller JPEG for preview and diffing so we only pay the
            // full analysis/store compression cost when admission says the
            // observation is worth keeping.
            var previewFrame = Services.ImageProcessor.ScaleToHeight(rawFrame, 360);

            // 2. Diff/admission on the preview-sized bytes.
            var diffThreshold = SelectedAgent?.CaptureConfig.DiffThreshold ?? 10;
            var diffHashWidth = SelectedAgent?.CaptureConfig.DiffHashWidth ?? 9;
            var diffDistance = _frameDiffService.GetDistanceFromLast(previewFrame, diffHashWidth);
            var hasMeaningfulChange = _frameDiffService.HasChanged(previewFrame, diffThreshold, diffHashWidth);
            var admission = _observationAdmissionGate.Evaluate(hasMeaningfulChange, capturedAtUtc);

            _telemetry?.TrackEvent("capture", "admission_decision", new Dictionary<string, string>
            {
                ["reason"] = admission.Reason,
                ["changed"] = hasMeaningfulChange.ToString(),
                ["distance"] = diffDistance.ToString()
            });

            if (!admission.StoreObservation)
            {
                Console.WriteLine(
                    $"[Capture] Admission gate decided {admission.Reason} (dHash distance={diffDistance}, threshold={diffThreshold})");
                return;
            }

            var frameRef = Guid.NewGuid().ToString();
            var gameTime = DateTime.UtcNow - _sessionStartedAt;

            // 3. Preview + timeline checkpoint only for admitted observations.
            // Rejected frames produce zero main-thread work.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    PreviewImage = previewFrame;
                    _brainEventRouter.OnScreenCapture(frameRef, gameTime, "auto");
                }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("FrameCaptured.Preview", ex); }
            });

            var moment = new ReelMoment
            {
                TimestampUtc = capturedAtUtc,
                SourceTarget = sourceTarget,
                FrameRef = frameRef,
                Confidence = 1.0
            };
            _visualReelService.Append(moment);

            // 4. Only admitted observations pay the full compression cost.
            var compressed = Services.ImageProcessor.ScaleAndCompress(rawFrame);
            if (compressed.Length == 0) return;

            _ = _observationStore?.StoreAsync(new ObservationWriteRequest
            {
                Id = frameRef,
                CapturedAtUtc = capturedAtUtc,
                SourceTarget = sourceTarget,
                ArtifactBytes = compressed,
                AgentKey = SelectedAgent?.Key,
                SessionId = _sessionTrace?.SessionId,
                Kind = ObservationKind.Frame,
                FileExtension = ".jpg"
            }).ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[ObservationStore] StoreAsync failed: {t.Exception?.GetBaseException().Message}"),
                TaskContinuationOptions.OnlyOnFaulted);

            if (!admission.SendToBrain)
            {
                Console.WriteLine(
                    $"[Capture] Admission gate decided {admission.Reason} (dHash distance={diffDistance}, threshold={diffThreshold})");
                return;
            }

            // 5. Brain submission (sole consumer of visual data -- GOLDEN RULE)
            // Voice NEVER receives raw images. Brain analyzes, router distributes text.
            // TrySubmitFrame uses Channel(1, DropOldest) — always succeeds, never blocks.
            // The observation admission gate controls whether a stored observation also
            // deserves immediate brain attention.
            var contextStr = _captureService.CurrentTarget != null
                ? $"Watching: {_captureService.CurrentTarget.ProcessName} - {_captureService.CurrentTarget.WindowTitle}"
                : "No active target";

            if (_brainService.TrySubmitFrame(compressed, contextStr))
            {
                Console.WriteLine($"[Capture] Admission gate sent frame to brain ({admission.Reason}, dHash distance={diffDistance}, threshold={diffThreshold})");
                Console.WriteLine($"[Capture] Frame submitted to brain ({compressed.Length} bytes)");
                _telemetry?.TrackEvent("capture", "frame_queued", new Dictionary<string, string>
                {
                    ["bytes"] = compressed.Length.ToString()
                });
            }
            else
            {
                // Channel closed — session ending
                Console.WriteLine("[Capture] Brain channel closed, frame not submitted");
                _telemetry?.TrackEvent("capture", "frame_dropped", new Dictionary<string, string>
                {
                    ["reason"] = "channel_closed"
                });
            }

            // NOTE: SendImageAsync to voice is REMOVED.
            // Voice receives brain output via BrainEventRouter.SendContextualUpdateAsync (text only).
            // See BRAIN_VOICE_PIPELINE_RULES.md Section 7.1 (Anti-Pattern: voice receives raw images).
        };
        _conversationProvider.ConnectionStateChanged += (_, state) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // Skip stale state changes — the provider may have moved on
                    // This prevents rapid state cycling from overwhelming the UI
                    if (state != _conversationProvider.State)
                    {
                        Services.CrashLogger.LogLifecycle("MainViewModel",
                            $"ConnectionStateChanged SKIPPED stale {state} (current={_conversationProvider.State})");
                        return;
                    }

                    Services.CrashLogger.LogLifecycle("MainViewModel", $"ConnectionStateChanged -> {state}");
                    ConnectionState = state;
                    OnPropertyChanged(nameof(ConnectionBadgeText));
                    OnPropertyChanged(nameof(ConnectionBadgeColor));
                    OnPropertyChanged(nameof(ConnectButtonText));
                    OnPropertyChanged(nameof(CanConnect));
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(IsConnecting));
                    OnPropertyChanged(nameof(CanSendTextMessage));
                    OnPropertyChanged(nameof(ChatInputPlaceholder));
                    OnPropertyChanged(nameof(IsFabEnabled));
                    OnPropertyChanged(nameof(IsGhostActive));
                    OnPropertyChanged(nameof(ShowGhostHint));

                    // Update exchange mode based on voice connectivity (12E degradation)
                    if (state == ConnectionState.Connected)
                        _exchangeManager?.SetMode(
                            _brainService != null ? AudioIntelligenceMode.Full : AudioIntelligenceMode.VoiceOnly);
                    else if (state is ConnectionState.Disconnected or ConnectionState.Error)
                        _exchangeManager?.SetMode(AudioIntelligenceMode.TextOnly);

                    // Sync FAB connected state to native panel
                    if (_ghostModeService.IsGhostModeActive)
                        _ghostModeService.SetFabState(IsFabActive, state == ConnectionState.Connected);

                    if (state == ConnectionState.Connected && _navigateToMinimalOnConnected)
                    {
                        _navigateToMinimalOnConnected = false;
                    }
                    else if (state is ConnectionState.Disconnected or ConnectionState.Error or ConnectionState.Disconnecting)
                    {
                        _navigateToMinimalOnConnected = false;

                        // Exit ghost mode on disconnect (must restore MAUI window)
                        if (_ghostModeService.IsGhostModeActive)
                        {
                            StopGhostNotificationLoop(dismissNativeCard: true);
                            _ = _ghostModeService.ExitGhostModeAsync()
                                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                                    $"[GhostMode] ExitGhostModeAsync on disconnect failed: {t.Exception?.GetBaseException().Message}"),
                                    TaskContinuationOptions.OnlyOnFaulted);
                        }

                        IsFabActive = false;
                    }
                }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("ConnectionStateChanged", ex); }
            });
        _conversationProvider.MessageReceived += (_, incomingMessage) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    HandleConversationMessageReceived(incomingMessage, isLegacyFallback: false);
                }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("TextReceived", ex); }
            });
        };
        _conversationProvider.TextReceived += (_, text) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    HandleConversationMessageReceived(new ChatMessage
                    {
                        Role = MessageRole.Assistant,
                        Intent = MessageIntent.GeneralChat,
                        Content = text,
                        Source = _conversationProvider.ProviderName
                    }, isLegacyFallback: true);
                }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("TextReceivedLegacy", ex); }
            });
        };
        _conversationProvider.AudioReceived += (_, pcmData) =>
        {
            // Suppress auto-response audio when user spoke without wake phrase
            if (_suppressUnwokenResponse)
                return;

            // Queue audio for playback
            _ = _audioService.PlayAudioAsync(pcmData)
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[Audio] PlayAudioAsync failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);

            // Exchange: agent producing audio = agent speaking, reset silence timer
            _exchangeManager?.OnAgentSpeech();
            _agentSpeechTracker?.OnAudioReceived();
        };
        _conversationProvider.Interrupted += (_, _) =>
        {
            // User spoke during AI response - stop playback immediately
            _ = _audioService.InterruptPlaybackAsync()
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[Audio] InterruptPlaybackAsync failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
        };
        _conversationProvider.ErrorOccurred += (_, message) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { AddSystemMessage(message, debounce: true); }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("ErrorOccurred", ex); }
            });
        };
        _conversationProvider.UserTranscriptReceived += (_, transcript) =>
        {
            _voiceTranscriptStore.AddTurn(new VoiceTranscriptTurn
            {
                Role = TranscriptRole.User,
                Text = transcript,
                Provider = _conversationProvider.ProviderName
            });
            _telemetry?.TrackEvent("voice", "input.transcript.final", new Dictionary<string, string>
            {
                ["provider"] = _conversationProvider.ProviderName,
                ["transcript_length"] = transcript.Length.ToString()
            });

            // Exchange: wake phrase detection + unwoken response suppression
            if (_exchangeManager != null && _wakePhraseDetector != null
                && !_exchangeManager.IsExchangeActive
                && _selectedAgent != null)
            {
                if (_wakePhraseDetector.TryDetectWake(transcript, _selectedAgent.Name, out string? _))
                {
                    _suppressUnwokenResponse = false; // Wake phrase found — allow audio
                    _exchangeManager.OnWakeDetected(_selectedAgent.Name);
                }
                else
                {
                    // No wake phrase, no active exchange — suppress the auto-response
                    _suppressUnwokenResponse = true;
                    _ = _audioService.InterruptPlaybackAsync() // Stop any audio that already started
                        .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                            $"[Voice] Unwoken response interrupt failed: {t.Exception?.GetBaseException().Message}"),
                            TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            else if (_exchangeManager?.IsExchangeActive == true)
            {
                // Exchange is active — always allow audio
                _suppressUnwokenResponse = false;
            }

            // Exchange: user speech resets silence timer
            _exchangeManager?.OnUserSpeech();

            // Exchange: voice deferral — detect questions that need brain (Phase 12D)
            if (_exchangeManager?.IsExchangeActive == true && _brainRequestChannel != null)
            {
                var grounding = _voiceGrounding?.Evaluate(transcript, _sessionManager?.Context.State == Models.SessionState.InGame);
                if (grounding?.ResponseMode == VoiceResponseMode.DeferToBrain)
                {
                    // Determine which brain capability is needed
                    var capability = BrainCapabilityManifest.Default.FindMatchingCapability(transcript);

                    // Give stock deferral acknowledgment
                    var ackText = capability?.Name switch
                    {
                        "search_replay" => "Let me check the footage on that",
                        "analyze_position_engine" => "Running the engine on this position",
                        "game_journal" => "Let me check the journal",
                        _ => "Let me look into that"
                    };

                    if (_conversationProvider?.IsConnected == true)
                    {
                        _ = _conversationProvider.SendContextualUpdateAsync(ackText)
                            .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                                $"[Voice] Deferral ack failed: {t.Exception?.GetBaseException().Message}"),
                                TaskContinuationOptions.OnlyOnFaulted);
                    }

                    // Write priority request to brain
                    var request = new BrainRequest
                    {
                        ExchangeId = _exchangeManager.CurrentExchange?.ExchangeId,
                        UserQuestion = transcript,
                        LikelyCapability = capability?.Name,
                        HasDeferralBeenSpoken = true,
                    };
                    _ = _brainRequestChannel.WriteAsync(request);

                    // Transition to AwaitingBrain
                    _exchangeManager.TransitionToAwaitingBrain();
                }
            }
        };

        // Ghost mode callbacks — fired from native code on main thread.
        // BeginInvokeOnMainThread with async lambda creates async void delegate;
        // must catch to prevent unhandled exception crash.
        _ghostModeService.FabTapped += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await ToggleFabAsync(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GhostMode] ToggleFab failed: {ex.Message}"); }
            });
        };
        _ghostModeService.CardDismissed += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { FabCardVariant = FabCardVariant.None; }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("CardDismissed", ex); }
            });
        };
        _ghostModeService.GearTapped += (_, _) =>
        {
            System.Diagnostics.Debug.WriteLine("[GhostMode] Gear tapped — audio card shown natively");
        };
        _ghostModeService.AudioToggleChanged += (_, args) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[GhostAudio] AudioToggleChanged: index={args.ToggleIndex} newValue={args.NewValue}");

                    if (ShouldIgnoreGhostAudioToggle(args))
                    {
                        System.Diagnostics.Debug.WriteLine("[GhostAudio] IGNORED by dedup");
                        return;
                    }

                    _suppressGhostAudioSync = true;
                    switch (args.ToggleIndex)
                    {
                        case 0: // VOICE CHAT
                            System.Diagnostics.Debug.WriteLine($"[GhostAudio] Setting IsVoiceChatActive = {args.NewValue}");
                            IsVoiceChatActive = args.NewValue;
                            break;
                        case 1: // VOICE COMMAND
                            IsAiMicActive = args.NewValue;
                            break;
                        case 2: // GAME AUDIO
                            IsCommentaryActive = args.NewValue;
                            break;
                        case 3: // AUDIO IN
                            IsAudioInActive = args.NewValue;
                            break;
                        default:
                            System.Diagnostics.Debug.WriteLine(
                                $"[GhostMode] Ignoring unknown audio toggle index: {args.ToggleIndex}");
                            break;
                    }
                    _suppressGhostAudioSync = false;

                    // Sync back to native state controller so the next apply(panelState:)
                    // doesn't reset toggles to stale values. The suppress flag is already
                    // cleared, so SyncAudioStateToGhost will call SetAudioState with the
                    // correct managed values.
                    SyncAudioStateToGhost();
                }
                catch (Exception ex) { Services.CrashLogger.LogMainThreadException("AudioToggleChanged", ex); }
            });
        };

        // Start brain result consumer loop (app-lifetime, not session-scoped).
        // Consumer runs continuously so brain results route to timeline/voice/ghost even across sessions.
        _brainEventRouter.StartConsuming(_brainService.Results, CancellationToken.None);

        // Start brain request consumer (Phase 12D) — reads voice deferral requests
        // and forwards them to the brain as priority queries. Responses flow back through
        // Channel<BrainResult> -> BrainEventRouter.RouteBrainResult as normal.
        // V1 LIMITATION: SubmitQueryAsync doesn't tag the resulting BrainResult with
        // DeferredRequestId — full request-response correlation requires OpenRouterBrainService
        // changes and is deferred to a future task.
        if (_brainRequestChannel != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // App-lifetime consumer — no cancellation token intentionally.
                    // The channel is never completed, so this loop runs for the app lifetime.
                    // Session-scoped cancellation is handled per-request via _sessionCts below.
                    await foreach (var request in _brainRequestChannel.Reader.ReadAllAsync(CancellationToken.None))
                    {
                        // Wait for session CTS to be available (session must be active)
                        if (_sessionCts?.IsCancellationRequested != false) continue;

                        try
                        {
                            var context = await _brainContextService.GetContextForVoiceAsync(
                                DateTime.UtcNow, intent: "voice_deferral");

                            await _brainService.SubmitQueryAsync(
                                $"[PRIORITY VOICE REQUEST — user asked: \"{request.UserQuestion}\"] " +
                                $"Use the {request.LikelyCapability ?? "most appropriate"} tool to answer this question. " +
                                "Provide a concise voice narration of the answer.",
                                context,
                                _sessionCts!.Token);

                            System.Diagnostics.Debug.WriteLine(
                                $"[Voice→Brain] Forwarded request: {request.UserQuestion} (capability: {request.LikelyCapability})");
                        }
                        catch (OperationCanceledException) { /* Session ended, keep consumer alive */ }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[Voice→Brain] Request forwarding failed: {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException) { /* Normal shutdown */ }
            });
        }

        // Load persisted ghost hint state
#if ANDROID || IOS || MACCATALYST || WINDOWS
        _hasTappedGhostButton = Preferences.Get("hasTappedGhostButton", false);
#endif

        OnPropertyChanged(nameof(GeminiBackendText));
    }

    partial void OnInputVolumeChanged(float value)
    {
        OnPropertyChanged(nameof(ActivityVolume));
        UpdateFabVoiceState();
        ForwardVadLevelToGhost();
    }
    partial void OnOutputVolumeChanged(float value)
    {
        OnPropertyChanged(nameof(ActivityVolume));
        UpdateFabVoiceState();
        ForwardVadLevelToGhost();
    }

    /// <summary>
    /// Forward VAD level to ghost mode panel, throttled to ~15fps (66ms) to avoid
    /// overwhelming the native animation layer with per-sample updates.
    /// </summary>
    private void ForwardVadLevelToGhost()
    {
        if (!_ghostModeService.IsGhostModeActive) return;
        var now = DateTime.UtcNow;
        if ((now - _lastVadForward).TotalMilliseconds < 66) return;
        _lastVadForward = now;
        _ghostModeService.SetVadLevel((float)ActivityVolume);
    }
    partial void OnIsFabActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(IsGhostActive));
        OnPropertyChanged(nameof(IsFabCardVisible));
        OnPropertyChanged(nameof(ShowVoiceCard));
        OnPropertyChanged(nameof(ShowTextCard));
        OnPropertyChanged(nameof(ShowCardImage));
        if (!value)
            FabCardVariant = FabCardVariant.None;
    }
    partial void OnFabCardVariantChanged(FabCardVariant value)
    {
        OnPropertyChanged(nameof(IsFabCardVisible));
        OnPropertyChanged(nameof(ShowVoiceCard));
        OnPropertyChanged(nameof(ShowTextCard));
        OnPropertyChanged(nameof(ShowCardImage));
    }
    partial void OnPreviewImageChanged(byte[]? value)
    {
        // Cancel-Swap-Notify: release old StreamImageSource before creating new one
        // Mitigates MAUI StreamImageSource leak (dotnet/maui#23574)
        _previewImageSource?.Cancel();
        _previewImageSource = value is { Length: > 0 }
            ? ImageSource.FromStream(() => new MemoryStream(value))
            : null;

        OnPropertyChanged(nameof(HasPreviewImage));
        OnPropertyChanged(nameof(PreviewImageSource));
    }
    partial void OnSelectedAgentChanged(Agent? value)
    {
        OnPropertyChanged(nameof(CanSendTextMessage));
        OnPropertyChanged(nameof(ChatInputPlaceholder));
        _sessionManager.Context.AgentKey = value?.Key;

        // Activate the agent's default game skill pack
        _packService?.SetActivePack(value?.GamePacks.FirstOrDefault());
    }
    partial void OnMessageDraftTextChanged(string value) => OnPropertyChanged(nameof(CanSendTextMessage));

    partial void OnIsVoiceChatActiveChanged(bool value)
    {
        if (_suppressVoiceChatToggle || _suppressUnsupportedToggle) return;
        if (value && SelectedAgent?.SupportsVoiceChat != true)
        {
            SnapBackUnsupported(() =>
            {
                _suppressVoiceChatToggle = true;
                IsVoiceChatActive = false;
                _suppressVoiceChatToggle = false;
            }, "Voice Chat");
            return;
        }
        SyncAudioStateToGhost();
        _ = HandleVoiceChatToggleAsync(value);
    }

    private async Task HandleVoiceChatToggleAsync(bool isOn)
    {
        System.Diagnostics.Debug.WriteLine($"[VoiceToggle] HandleVoiceChatToggleAsync({isOn}) ConnectionState={ConnectionState}");
        if (isOn)
        {
            // Voice Chat ON requires active connection
            if (ConnectionState != ConnectionState.Connected)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceToggle] SNAP BACK — not connected (state={ConnectionState})");
                // Snap toggle back OFF — can't voice chat without connection
                _suppressVoiceChatToggle = true;
                IsVoiceChatActive = false;
                _suppressVoiceChatToggle = false;
                ShowUnsupportedAudioFeedback(
                    featureName: "Voice Chat",
                    ghostModeMessage: "Voice Chat requires a game connection. Connect to a game first.");
                return;
            }
            // Toggle moves immediately; pending indicator shows async work in flight
            IsVoiceChatPending = true;
            try
            {
                var sessionToken = _sessionCts?.Token ?? CancellationToken.None;
                await _audioService.StartRecordingAsync(pcm =>
                {
                    if (sessionToken.IsCancellationRequested) return;

                    // Porcupine wake word detection — processes raw PCM before STT (D-AI-7)
                    _porcupineWakeDetector?.ProcessAudio(pcm);

                    if (_conversationProvider.IsConnected && !_audioService.IsPlaying)
                    {
                        _ = _conversationProvider.SendAudioAsync(pcm)
                            .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                                $"[Audio] SendAudioAsync failed: {t.Exception?.GetBaseException().Message}"),
                                TaskContinuationOptions.OnlyOnFaulted);
                    }
                });
                System.Diagnostics.Debug.WriteLine("[VoiceToggle] StartRecordingAsync SUCCEEDED — mic active");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceToggle] StartRecordingAsync FAILED: {ex.Message}");
                // Snap toggle back OFF — mic start failed
                _suppressVoiceChatToggle = true;
                IsVoiceChatActive = false;
                _suppressVoiceChatToggle = false;
                SyncAudioStateToGhost();
            }
            finally
            {
                IsVoiceChatPending = false;
            }
        }
        else
        {
            // Voice Chat OFF — pending while stopping mic
            IsVoiceChatPending = true;
            try
            {
                await _audioService.StopRecordingAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Audio] StopRecordingAsync failed: {ex.Message}");
            }
            finally
            {
                IsVoiceChatPending = false;
            }
        }
    }

    partial void OnIsCommentaryActiveChanged(bool value)
    {
        if (_suppressUnsupportedToggle) return;
        if (value && SelectedAgent?.SupportsGameAudio != true)
        {
            SnapBackUnsupported(() => IsCommentaryActive = false, "Game Audio");
            return;
        }
        System.Diagnostics.Debug.WriteLine($"[Audio] Commentary toggle: {value}");
        SyncAudioStateToGhost();
    }

    partial void OnIsAiMicActiveChanged(bool value)
    {
        if (_suppressUnsupportedToggle) return;
        if (value && SelectedAgent?.SupportsVoiceCommand != true)
        {
            SnapBackUnsupported(() => IsAiMicActive = false, "Voice Command");
            return;
        }
        System.Diagnostics.Debug.WriteLine($"[Audio] AI Mic toggle: {value}");
        SyncAudioStateToGhost();
    }

    partial void OnIsAudioInActiveChanged(bool value)
    {
        if (_suppressUnsupportedToggle) return;
        if (value && SelectedAgent?.SupportsAudioIn != true)
        {
            SnapBackUnsupported(() => IsAudioInActive = false, "Audio In");
            return;
        }
        System.Diagnostics.Debug.WriteLine($"[Audio] Audio In toggle: {value}");
        SyncAudioStateToGhost();
    }

    private void SnapBackUnsupported(Action snapBack, string featureName)
    {
        _suppressUnsupportedToggle = true;
        snapBack();
        _suppressUnsupportedToggle = false;
        ShowUnsupportedAudioFeedback(
            featureName,
            ghostModeMessage: $"{SelectedAgent?.Name ?? "This agent"} does not support: {featureName}");
    }

    private void ShowUnsupportedAudioFeedback(string featureName, string ghostModeMessage)
    {
        if (_ghostModeService.IsGhostModeActive)
        {
            AddSystemMessage(ghostModeMessage, debounce: true, routeToTimeline: false);
            return;
        }

        UnsupportedAudioFeatureToggled?.Invoke(this, featureName);
    }

    private void SyncAudioStateToGhost()
    {
        if (_suppressGhostAudioSync || !_ghostModeService.IsGhostModeActive) return;
        _ghostModeService.SetAudioState(IsVoiceChatActive, IsAiMicActive, IsCommentaryActive, IsAudioInActive);
    }

    private bool ShouldIgnoreGhostAudioToggle(AudioToggleEventArgs args)
    {
        var nowUtc = DateTime.UtcNow;
        if (args.ToggleIndex == _lastGhostAudioToggleIndex &&
            args.NewValue == _lastGhostAudioToggleValue &&
            nowUtc - _lastGhostAudioToggleAt <= GhostAudioToggleDedupWindow)
        {
            return true;
        }

        _lastGhostAudioToggleIndex = args.ToggleIndex;
        _lastGhostAudioToggleValue = args.NewValue;
        _lastGhostAudioToggleAt = nowUtc;

        return args.ToggleIndex == 0 &&
               IsVoiceChatPending &&
               args.NewValue != IsVoiceChatActive;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("agentId", out var agentIdObj) && agentIdObj is string agentId)
        {
            SelectedAgent = Agents.GetByKey(agentId);
        }
        IsPageReady = true;
    }

    [RelayCommand]
    private async Task LoadCaptureTargetsAsync()
    {
        CaptureTargets = await _captureService.GetCaptureTargetsAsync();
    }

    [RelayCommand]
    private async Task SelectTargetAsync(CaptureTarget target)
    {
        if (SelectedTarget != null)
        {
            SelectedTarget.IsSelected = false;
            await _captureService.StopCaptureAsync();
        }

        SelectedTarget = target;
        target.IsSelected = true;
        OnPropertyChanged(nameof(HasSelectedTarget));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CurrentTarget));

        if (_ghostModeService.IsGhostModeActive)
            PositionGhostPanelOnTargetScreen();

        var captureInterval = SelectedAgent?.CaptureConfig.CaptureIntervalMs ?? 5000;
        await _captureService.StartCaptureAsync(target, captureInterval);
    }

    [RelayCommand]
    private async Task ShowWindowPickerAsync()
    {
        if (ConnectionState == ConnectionState.Connected)
        {
            // Already connected — disconnect
            await ToggleConnectionAsync();
            return;
        }

        // Load real windows and open the picker tray
        CaptureTargets = await _captureService.GetCaptureTargetsAsync();
        IsWindowPickerOpen = true;
    }

    [RelayCommand]
    private async Task SelectTargetAndConnectAsync(CaptureTarget target)
    {
        // Select the target, close picker, then connect
        await SelectTargetAsync(target);
        IsWindowPickerOpen = false;
        await ToggleConnectionAsync();
    }

    [RelayCommand]
    private void CloseWindowPicker()
    {
        IsWindowPickerOpen = false;
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (ConnectionState == ConnectionState.Connected)
        {
            _navigateToMinimalOnConnected = false;
            await _conversationProvider.DisconnectAsync();
            await StopSessionAsync();
            
            // If we're in MinimalView, navigate back to MainView and resize window
            var currentRoute = Shell.Current?.CurrentState?.Location?.OriginalString;
            if (currentRoute?.Contains("MinimalView") == true)
            {
                await ExpandToMainViewAsync();
            }
        }
        else if (ConnectionState == ConnectionState.Disconnected && SelectedAgent != null && SelectedTarget != null)
        {
            _sessionCts?.Dispose();
            _sessionCts = new CancellationTokenSource();

            _sessionTrace?.StartSession();
            _sessionTrace?.TrackEvent("session.connect.start", new Dictionary<string, string>
            {
                ["agent"] = SelectedAgent.Name ?? "unknown",
                ["target"] = SelectedTarget.WindowTitle ?? "unknown",
                ["voice_provider"] = _conversationProvider.ProviderName ?? "unknown"
            });

            try
            {
                await _conversationProvider.ConnectAsync(SelectedAgent);
            }
            catch (Exception ex)
            {
                _sessionTrace?.TrackSessionResult(success: false, error: ex.Message);
                _sessionTrace?.EndSession();
                System.Diagnostics.Debug.WriteLine($"[Gaimer] ConnectAsync threw: {ex.Message}");
                return;
            }

            if (!_conversationProvider.IsConnected)
            {
                _sessionTrace?.TrackSessionResult(success: false, error: "ConnectAsync returned not connected");
                _sessionTrace?.EndSession();
                System.Diagnostics.Debug.WriteLine("[Gaimer] ConnectAsync returned but Provider is not connected; skipping microphone start.");
                return;
            }

            _sessionTrace?.TrackSessionResult(success: true);

#if DEBUG
            // Fire demo events when using mock provider (exercises all timeline event types)
            if (_conversationProvider.ProviderName == "Mock Provider")
            {
                _ = RunMockDemoSequenceAsync();
            }
#endif

            // Launch Gaimer Team background session (C1 fix — fire-and-forget)
            if (_gaimerTeam != null && !_gaimerTeam.IsConnected)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var launched = await _gaimerTeam.LaunchSessionAsync();
                        System.Diagnostics.Debug.WriteLine(
                            $"[GaimerTeam] LaunchSessionAsync: {(launched ? "connected" : "unavailable")}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GaimerTeam] Launch failed: {ex.Message}");
                    }
                });
            }

            // Transition session to InGame state
            var gameId = CurrentTarget?.Handle.ToString() ?? "unknown";
            var gameType = CurrentTarget?.WindowTitle?.Contains("chess", StringComparison.OrdinalIgnoreCase) == true
                ? "chess"
                : SelectedAgent?.Type.ToString().ToLowerInvariant() ?? "general";
            var connectorName = CurrentTarget?.WindowTitle ?? "Unknown";
            _sessionManager.TransitionToInGame(gameId, gameType, connectorName);
            _sessionStartedAt = DateTime.UtcNow;

            // Update voice provider with in-game context so it knows the conversation class
            if (_conversationProvider?.IsConnected == true && _selectedAgent != null)
            {
                var inGameContext = $"\n\n[GAME STATE: IN-GAME]\nYou are now watching a live {gameType} session on '{connectorName}'.\n" +
                    "You can reference what you see on screen. Use game-appropriate language.\n" +
                    "Your tools are available. Call out patterns, log events, answer game questions.";
                _ = _conversationProvider.UpdateInstructionsAsync(_selectedAgent.ComposedPersonality + inGameContext);
            }

            // W5: Single session ID shared by history + replay (avoids divergence if trace is null)
            var sessionId = _sessionTrace?.SessionId ?? Guid.NewGuid().ToString("N")[..12];

            // Persist session start to history DB (fire-and-forget — has its own try-catch)
            _ = _historyService?.StartSessionAsync(
                sessionId,
                SelectedAgent?.Key,
                gameType,
                connectorName,
                SelectedTarget?.WindowTitle,
                SelectedTarget?.ProcessName);

            // Start replay recording alongside brain capture (fire-and-forget)
            if (SelectedTarget?.Handle is { } windowHandle)
            {
                _ = _replayRecording?.StartAsync((uint)windowHandle, sessionId);
            }

            // Start video analysis orchestrator (processes completed segments in background)
            _replayAnalysisOrchestrator?.Start(_sessionCts!.Token);

            // Voice chat is activated separately via IsVoiceChatActive toggle.
            // Connection establishes text-only API session; mic starts independently.
        }
    }

    private async Task NavigateToMinimalViewIfOnMainAsync()
    {
        var currentRoute = Shell.Current?.CurrentState?.Location?.OriginalString;
        if (currentRoute is null || !currentRoute.Contains("MainPage", StringComparison.OrdinalIgnoreCase))
            return;

        await NavigateToMinimalViewAsync();
    }

    private async Task StopSessionAsync()
    {
        if (!await _stopSessionLock.WaitAsync(0)) return; // Already stopping
        try
        {
            // Finalize history record before session trace ends (preserves SessionId)
            if (_sessionTrace?.SessionId is { } sessionId)
                _ = _historyService?.FinalizeSessionAsync(sessionId);

            _sessionTrace?.EndSession();

            // Cancel in-flight brain work (consumer loop keeps running for next session)
            _brainService.CancelAll();

            // Cancel session-scoped operations (e.g. pending audio sends)
            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = null;

            // Transition session to OutGame state
            _sessionManager.TransitionToOutGame();
            _observationAdmissionGate.Reset();

            // Explicitly close any active exchange before audio teardown (skip close SFX)
            _suppressCloseExchangeSfx = true;
            if (_exchangeManager?.IsExchangeActive == true)
                _exchangeManager.CloseExchange();
            _suppressCloseExchangeSfx = false;
            _reminderQueue?.PruneStale(TimeSpan.Zero);

            // Update voice provider with out-game context
            if (_conversationProvider?.IsConnected == true && _selectedAgent != null)
            {
                var outGameContext = "\n\n[GAME STATE: OUT-OF-GAME]\nNo game session is active.\n" +
                    "You cannot see any gameplay. Do NOT reference game state, positions, or in-game events.\n" +
                    "Keep conversation casual — discuss strategy, answer general questions, or just chat.";
                _ = _conversationProvider.UpdateInstructionsAsync(_selectedAgent.ComposedPersonality + outGameContext);
            }

            // Stop audio (idempotent) and clear pending state
            await _audioService.StopRecordingAsync();
            await _audioService.StopPlaybackAsync();
            IsVoiceChatPending = false;

            // Stop capture so "LIVE" and preview stop when disconnected
            await _captureService.StopCaptureAsync();

            // Stop replay recording (finalizes last segment, fires SegmentCompleted — keeps files)
            if (_replayRecording?.IsRecording == true)
                await _replayRecording.StopAsync();

            // Drain orchestrator — processes remaining segments while files still exist
            _replayAnalysisOrchestrator?.Stop();

            // Now safe to delete ephemeral replay files
            _replayRecording?.CleanupSessionFiles();

            // Reset audio speech trackers (Phase 12B)
            _agentSpeechTracker?.Reset();

            // Stop Stockfish engine on disconnect (restarts on next chess agent selection)
            if (_stockfishService.IsReady)
                await _stockfishService.StopAsync();

            // Clear game selection on disconnect so the Connect button returns to its disabled/dim state
            // until the user explicitly re-selects a target.
            if (SelectedTarget != null)
                SelectedTarget.IsSelected = false;
            SelectedTarget = null;
            OnPropertyChanged(nameof(HasSelectedTarget));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CurrentTarget));

            // Clear UI state that should not persist after disconnect
            IsFabActive = false;
            _suppressVoiceChatToggle = true;
            IsVoiceChatActive = false;
            IsCommentaryActive = false;
            IsAiMicActive = false;
            IsAudioInActive = false;
            _suppressVoiceChatToggle = false;
            InputVolume = 0f;
            OutputVolume = 0f;
            PreviewImage = null;

            AiDisplayContent = null;
            SlidingPanelContent = null;
            ChatMessages.Clear();

            OnPropertyChanged(nameof(HasAiContent));
            OnPropertyChanged(nameof(HasNoAiContent));
            OnPropertyChanged(nameof(HasPanelContent));
                    OnPropertyChanged(nameof(HasTextPanelContent));
        }
        finally
        {
            _stopSessionLock.Release();
        }
    }

    [RelayCommand]
    private async Task ChangeAgentAsync()
    {
        if (ConnectionState == ConnectionState.Connected)
        {
            await _conversationProvider.DisconnectAsync();
            await _audioService.StopRecordingAsync();
        }

        await _captureService.StopCaptureAsync();
        SelectedAgent = null;
        SelectedTarget = null;
        OnPropertyChanged(nameof(CurrentTarget));
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task NavigateToSettingsAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("Settings");
    }

    /// <summary>
    /// Disconnects the current session and reconnects with the same agent and target.
    /// Called after voice provider/gender settings change mid-session.
    /// The ConversationProviderFactory reads new settings on reconnect.
    /// </summary>
    public async Task RestartSessionAsync()
    {
        if (ConnectionState != ConnectionState.Connected) return;
        if (_structuralSettingsTracker.RequiresRebootstrap)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MainViewModel] RestartSessionAsync skipped: structural settings changed ({string.Join(", ", _structuralSettingsTracker.PendingSettings)}), app rebootstrap required.");
            return;
        }

        var agent = SelectedAgent;
        var target = SelectedTarget;

        // Disconnect current session
        await _conversationProvider.DisconnectAsync();
        await StopSessionAsync();

        // Brief pause for cleanup
        await Task.Delay(100);

        // Reconnect with same agent + target (provider factory reads new settings)
        if (agent != null && target != null)
        {
            SelectedAgent = agent;
            SelectedTarget = target;
            await _conversationProvider.ConnectAsync(agent);
        }
    }

    private void OnStructuralSettingsStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(RequiresAppRebootstrap));
            OnPropertyChanged(nameof(CanRestartSessionShallow));
        });
    }

    [RelayCommand]
    private async Task OpenMinimalViewAsync()
    {
        if (ConnectionState == ConnectionState.Connected)
        {
            await NavigateToMinimalViewAsync();
        }
    }
    
    private async Task NavigateToMinimalViewAsync()
    {
        await _navigationLock.WaitAsync();
        try
        {
            // Resize window to compact size BEFORE navigation
            await ResizeWindowAsync(960, 350, isMinimalView: true);
            if (Shell.Current is not null)
                await Shell.Current.GoToAsync("MinimalView");
        }
        finally
        {
            _navigationLock.Release();
        }
    }
    
    [RelayCommand]
    private async Task ExpandToMainViewAsync()
    {
        await _navigationLock.WaitAsync();
        try
        {
            // Resize window back to a larger default BEFORE navigation (debug-friendly, resizable).
            // Minimum is still enforced to the spec floor.
            await ResizeWindowAsync(1200, 900, isMinimalView: false);
            // Be explicit: return to MainPage (avoid popping back to AgentSelection due to route stack quirks)
            if (Shell.Current is not null)
                await Shell.Current.GoToAsync("MainPage");
        }
        finally
        {
            _navigationLock.Release();
        }
    }
    
    private static Task ResizeWindowAsync(double width, double height, bool isMinimalView)
    {
        // Window sizing must run on the UI thread on MacCatalyst.
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current?.Windows.FirstOrDefault() is not Window window)
                return;

            if (isMinimalView)
            {
                // For MinimalView: lock to compact size
                window.MinimumWidth = width;
                window.MinimumHeight = height;
                window.MaximumWidth = width;
                window.MaximumHeight = height;
            }
            else
            {
                // For MainView: allow user resizing; enforce a minimum floor.
                // NOTE: MacCatalyst can behave oddly with PositiveInfinity here; use a large bound instead.
                window.MinimumWidth = 900;
                window.MinimumHeight = 720;
                window.MaximumWidth = 10000;
                window.MaximumHeight = 10000;
            }

            // Set dimensions
            window.Width = width;
            window.Height = height;
        });
    }

    [RelayCommand]
    private void DismissSlidingPanel()
    {
        SlidingPanelContent = null;
        OnPropertyChanged(nameof(HasPanelContent));
                    OnPropertyChanged(nameof(HasTextPanelContent));
        FabCardVariant = FabCardVariant.None;
    }

#if MACCATALYST
    // Cache resolved agent image path to avoid repeated NSBundle lookups
    private string? _cachedAgentImagePath;
    private string? _cachedAgentImageKey;
#endif

    [RelayCommand]
    private async Task ToggleFabAsync()
    {
        // Mark ghost button as learned (suppresses hint in live messages)
        if (!_hasTappedGhostButton)
        {
            _hasTappedGhostButton = true;
            OnPropertyChanged(nameof(ShowGhostHint));
#if ANDROID || IOS || MACCATALYST || WINDOWS
            Preferences.Set("hasTappedGhostButton", true);
#endif
        }

        if (!_ghostModeService.IsSupported)
        {
            AddSystemMessage("Ghost mode is unavailable because the native ghost panel is not loaded.");
            return;
        }

        if (_ghostModeService.IsGhostModeActive)
        {
            // Exit ghost mode
            IsFabActive = false;
            StopGhostNotificationLoop(dismissNativeCard: true);
            await _ghostModeService.ExitGhostModeAsync();
        }
        else
        {
            // Enter ghost mode — hide window first for instant visual feedback,
            // but place the panel on the target screen before showing it.
            IsFabActive = true;
            PositionGhostPanelOnTargetScreen();
            await _ghostModeService.EnterGhostModeAsync();

            // Configure panel state after window is already hidden
            _ghostModeService.SetFabState(active: true, connected: IsConnected);
            _ghostModeService.SetAudioState(IsVoiceChatActive, IsAiMicActive, IsCommentaryActive, IsAudioInActive);

            // Resolve and set agent image path for native FAB (cached)
#if MACCATALYST
            if (SelectedAgent?.PortraitImage != null)
            {
                var agentKey = SelectedAgent.Key;
                if (_cachedAgentImageKey != agentKey)
                {
                    var imageName = SelectedAgent.PortraitImage
                        .Replace(".png", "")
                        .Replace(".jpg", "")
                        .ToLowerInvariant();
                    _cachedAgentImagePath = Foundation.NSBundle.MainBundle.PathForResource(imageName, "png");
                    _cachedAgentImageKey = agentKey;
                }
                if (!string.IsNullOrEmpty(_cachedAgentImagePath))
                    _ghostModeService.SetAgentImage(_cachedAgentImagePath);
            }
#endif
        }
    }

    /// <summary>
    /// Position the ghost panel on the same screen as the capture target window.
    /// Uses the target window's CG bounds to find the containing screen,
    /// then places the panel at that screen's top-right corner (AppKit coords).
    /// </summary>
    private void PositionGhostPanelOnTargetScreen()
    {
        var target = CurrentTarget;
        if (target is null || target.BoundsWidth == 0) return;

#if MACCATALYST
        try
        {
            // Target bounds are CG coordinates (top-left origin).
            // Find the NSScreen containing the target window's center point.
            var targetCenterX = target.BoundsX + target.BoundsWidth / 2;
            var targetCenterY = target.BoundsY + target.BoundsHeight / 2;

            // Get NSScreen.screens to find the matching screen
            var nsScreenClass = ObjCRuntime.Class.GetHandle("NSScreen");
            var screensArray = ObjCRuntime_msgSend(nsScreenClass, ObjCRuntime.Selector.GetHandle("screens"));
            if (screensArray == IntPtr.Zero) return;

            var count = (long)ObjCRuntime_msgSend(screensArray, ObjCRuntime.Selector.GetHandle("count"));

            // Get primary screen height for CG -> AppKit conversion
            var primaryScreen = ObjCRuntime_msgSend_IntPtr(screensArray, ObjCRuntime.Selector.GetHandle("objectAtIndex:"), IntPtr.Zero);
            ObjCRuntime_msgSend_stret(out CoreGraphics.CGRect primaryFrame, primaryScreen, ObjCRuntime.Selector.GetHandle("frame"));
            var primaryHeight = primaryFrame.Height;

            // Convert target center from CG (top-left) to AppKit (bottom-left)
            var appKitCenterY = primaryHeight - targetCenterY;

            for (int i = 0; i < count; i++)
            {
                var screen = ObjCRuntime_msgSend_IntPtr(screensArray, ObjCRuntime.Selector.GetHandle("objectAtIndex:"), (IntPtr)i);
                ObjCRuntime_msgSend_stret(out CoreGraphics.CGRect frame, screen, ObjCRuntime.Selector.GetHandle("frame"));
                ObjCRuntime_msgSend_stret(out CoreGraphics.CGRect visibleFrame, screen, ObjCRuntime.Selector.GetHandle("visibleFrame"));

                // Check if target center is within this screen (AppKit coords)
                if (targetCenterX >= frame.X && targetCenterX <= frame.X + frame.Width &&
                    appKitCenterY >= frame.Y && appKitCenterY <= frame.Y + frame.Height)
                {
                    // Match the native overlay frame: a 420pt-wide strip spanning the
                    // visible height of the target screen, anchored to its right edge.
                    var panelWidth = 420.0;
                    var panelHeight = visibleFrame.Height;
                    var panelX = visibleFrame.X + visibleFrame.Width - panelWidth;
                    var panelY = visibleFrame.Y;
                    _ghostModeService.SetSize(panelWidth, panelHeight);
                    _ghostModeService.SetPosition(panelX, panelY);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GhostMode] PositionGhostPanelOnTargetScreen error: {ex.Message}");
        }
#endif
    }

#if MACCATALYST
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjCRuntime_msgSend(IntPtr receiver, IntPtr selector);

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjCRuntime_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend_stret")]
    private static extern void ObjCRuntime_msgSend_stret(out CoreGraphics.CGRect retval, IntPtr receiver, IntPtr selector);
#endif

    [RelayCommand]
    private void ToggleGameSelector()
    {
        IsGameSelectorCollapsed = !IsGameSelectorCollapsed;
    }

    [RelayCommand]
    private async Task SendTextMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageDraftText))
            return;

        var text = MessageDraftText.Trim();
        MessageDraftText = string.Empty;

        var message = new ChatMessage
        {
            Role = MessageRole.User,
            Content = text,
            DeliveryState = DeliveryState.Pending
        };
        ChatMessages.Insert(0, message);
        PersistChatIfActive(message);

        if (!_conversationProvider.IsConnected)
        {
            // Out-game chat: route through brain service for real LLM response
            message.DeliveryState = DeliveryState.Sent;
            _brainEventRouter.OnUserMessage(message);

            try
            {
                var history = ChatMessages.ToList();
                var replyText = await _brainService.ChatAsync(text, history);

                var reply = new ChatMessage
                {
                    Role = MessageRole.Assistant,
                    Content = replyText
                };
                ChatMessages.Insert(0, reply);
                PersistChatIfActive(reply);
                _brainEventRouter.OnAssistantMessage(reply);
            }
            catch (Exception ex)
            {
                AddSystemMessage($"Chat failed: {ex.Message}");
            }
            return;
        }

        // In-game chat: route to brain service (not voice WebSocket)
        // Brain has full game context (journal, vision, L1 events)
        message.DeliveryState = DeliveryState.Sent;
        _brainEventRouter.OnUserMessage(message); // Show user msg on timeline immediately (A4)

        try
        {
            // Build context envelope for brain query
            var recentChat = ChatMessages.Take(Math.Max(0, ChatMessages.Count - 1)).ToList();
            var envelope = await _brainContextService.GetContextForChatAsync(
                DateTime.UtcNow,
                intent: "general",
                budgetTokens: BrainContextService.DefaultChatBudget,
                inputs: new ContextAssemblyInputs
                {
                    RecentChat = recentChat,
                    ActiveTarget = SelectedTarget,
                    RecentTranscript = _voiceTranscriptStore.GetRecent(20)
                });

            // Submit to brain — response arrives on Channel -> BrainEventRouter -> Timeline
            // and in ChatMessages via BrainChatReplyReceived event
            await _brainService.SubmitQueryAsync(text, envelope);
        }
        catch (Exception ex)
        {
            message.DeliveryState = DeliveryState.Failed;
            AddSystemMessage($"Send failed: {ex.Message}");
        }
    }

    private void OnBrainChatReply(string replyText)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var reply = new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = replyText
            };
            ChatMessages.Insert(0, reply);
            PersistChatIfActive(reply);

            // Surface in ghost/FAB when active — ensures deferred answers are visible
            if (_ghostModeService.IsGhostModeActive || IsFabActive)
            {
                EnqueueGhostNotification(new GhostNotificationRequest(
                    Variant: FabCardVariant.Text,
                    Title: "Answer",
                    Text: replyText.Length > 200 ? replyText[..200] + "…" : replyText,
                    ImagePath: null,
                    IsAlert: false,
                    IsVoiceDelivered: false,
                    DisplayDuration: TimeSpan.FromSeconds(8)));
            }
        });
    }

    private void EnqueueGhostNotification(GhostNotificationRequest request)
    {
        EnqueueGhostNotifications([request]);
    }

    private void EnqueueGhostNotifications(IEnumerable<GhostNotificationRequest> requests)
    {
        var addedAny = false;

        lock (_ghostNotificationLock)
        {
            foreach (var request in requests)
            {
                _ghostNotificationQueue.Enqueue(request);
                addedAny = true;
            }

            if (!addedAny || (_ghostNotificationTask != null && !_ghostNotificationTask.IsCompleted))
                return;

            _ghostNotificationCts?.Cancel();
            _ghostNotificationCts?.Dispose();
            _ghostNotificationCts = new CancellationTokenSource();
            var ct = _ghostNotificationCts.Token;
            _ghostNotificationTask = Task.Run(() => RunGhostNotificationLoopAsync(ct), ct);
        }
    }

    private async Task RunGhostNotificationLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                GhostNotificationRequest? next;
                lock (_ghostNotificationLock)
                {
                    if (!_ghostNotificationQueue.TryDequeue(out next))
                        break;
                }

                if (!_ghostModeService.IsGhostModeActive)
                    continue;

                await MainThread.InvokeOnMainThreadAsync(() =>
                    _ghostModeService.ShowCard(
                        next.Variant,
                        next.Title,
                        next.Text,
                        next.ImagePath,
                        next.IsAlert,
                        next.IsVoiceDelivered));

                if (next.IsAlert)
                    break;

                await Task.Delay(next.DisplayDuration, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_ghostModeService.IsGhostModeActive && !cancellationToken.IsCancellationRequested)
            {
                await MainThread.InvokeOnMainThreadAsync(() => _ghostModeService.DismissCard());
            }

            lock (_ghostNotificationLock)
            {
                _ghostNotificationTask = null;
            }
        }
    }

    private void StopGhostNotificationLoop(bool dismissNativeCard)
    {
        lock (_ghostNotificationLock)
        {
            _ghostNotificationQueue.Clear();
            _ghostNotificationCts?.Cancel();
            _ghostNotificationCts?.Dispose();
            _ghostNotificationCts = null;
            _ghostNotificationTask = null;
        }

        if (dismissNativeCard && _ghostModeService.IsGhostModeActive)
        {
            MainThread.BeginInvokeOnMainThread(() => _ghostModeService.DismissCard());
        }
    }

    private bool HasPendingGhostNotifications()
    {
        lock (_ghostNotificationLock)
        {
            return _ghostNotificationQueue.Count > 0 || (_ghostNotificationTask != null && !_ghostNotificationTask.IsCompleted);
        }
    }

    private void UpdateFabVoiceState()
    {
        if (!IsFabActive || !IsConnected) return;
        if (ActivityVolume > 0.01f && SlidingPanelContent == null)
        {
            FabCardVariant = FabCardVariant.Voice;
            if (_ghostModeService.IsGhostModeActive && !HasPendingGhostNotifications())
                _ghostModeService.ShowCard(FabCardVariant.Voice, null, "is talking...", null);
        }
        else if (FabCardVariant == FabCardVariant.Voice && ActivityVolume <= 0.01f)
        {
            FabCardVariant = FabCardVariant.None;
            if (_ghostModeService.IsGhostModeActive)
                _ghostModeService.DismissCard();
        }
    }

    private void HandleConversationMessageReceived(ChatMessage incomingMessage, bool isLegacyFallback)
    {
        if (incomingMessage.Role == MessageRole.System)
        {
            AddSystemMessage(incomingMessage.Content, debounce: true);
            return;
        }

        if (isLegacyFallback &&
            string.Equals(incomingMessage.Content, _lastTypedProviderMessageContent, StringComparison.Ordinal) &&
            string.Equals(incomingMessage.Source, _lastTypedProviderName, StringComparison.Ordinal) &&
            (DateTime.UtcNow - _lastTypedProviderMessageAt) <= TypedLegacyBridgeDedupWindow)
        {
            return;
        }

        if (!isLegacyFallback)
        {
            _lastTypedProviderMessageContent = incomingMessage.Content;
            _lastTypedProviderName = incomingMessage.Source;
            _lastTypedProviderMessageAt = DateTime.UtcNow;
        }

        var text = incomingMessage.Content;

        // Store AI voice response as transcript turn for brain context
        if (incomingMessage.Role == MessageRole.Assistant && !string.IsNullOrWhiteSpace(text))
        {
            _voiceTranscriptStore.AddTurn(new VoiceTranscriptTurn
            {
                Role = TranscriptRole.Assistant,
                Text = text,
                Provider = incomingMessage.Source
            });
            _telemetry?.TrackEvent("voice", "output.transcript.final", new Dictionary<string, string>
            {
                ["provider"] = incomingMessage.Source ?? "unknown",
                ["transcript_length"] = text.Length.ToString()
            });
        }

        // Expire stale pending message to avoid misrouting unsolicited AI text
        var isDirectChatReply = _pendingUserMessage != null
            && (DateTime.UtcNow - _pendingUserMessageAt) < PendingMessageTimeout;
        if (_pendingUserMessage != null && !isDirectChatReply)
        {
            System.Diagnostics.Debug.WriteLine("[Chat] Pending user message expired — treating AI text as unsolicited.");
            _pendingUserMessage = null;
        }

        // Evaluate grounding first so the decision can annotate displayed text.
        VoiceGroundingDecision? groundingDecision = null;
        var displayableText = text;
        if (!isDirectChatReply && _voiceGrounding != null)
        {
            var isInGame = _sessionManager.Context.State == SessionState.InGame;
            groundingDecision = _voiceGrounding.Evaluate(text, isInGame);
            _sessionTrace?.TrackEvent("voice.grounding.evaluated", new Dictionary<string, string>
            {
                ["turn_class"] = groundingDecision.TurnClass.ToString(),
                ["response_mode"] = groundingDecision.ResponseMode.ToString(),
                ["has_fresh_context"] = groundingDecision.HasFreshGroundedContext.ToString(),
                ["reason"] = groundingDecision.Reason ?? "none"
            });

            // Grounding correction: pack-driven language replaces hardcoded chess terms.
            // Each GameSkillPack defines its own GroundingLanguage (board/footage/intel/etc.).
            // Falls back to generic defaults when no pack is active.
            var grounding = _packService?.ActivePack?.GroundingLanguage ?? new GroundingLanguage();

            if (groundingDecision.TurnClass == VoiceTurnClass.BoardSensitive &&
                !groundingDecision.HasFreshGroundedContext)
            {
                displayableText = groundingDecision.ResponseMode switch
                {
                    VoiceResponseMode.AcknowledgeAndRefresh =>
                        $"⚠ {text}\n[{grounding.RefreshDisplay}]",
                    VoiceResponseMode.AcknowledgeUncertainty =>
                        $"⚠ {text}\n[{grounding.StaleDisplay}]",
                    VoiceResponseMode.DeclineBoardCertainty =>
                        $"⚠ {text}\n[{grounding.UnavailableDisplay}]",
                    _ => text
                };

                if (_conversationProvider.IsConnected)
                {
                    var correctionText = groundingDecision.ResponseMode switch
                    {
                        VoiceResponseMode.AcknowledgeAndRefresh => grounding.RefreshCorrection,
                        VoiceResponseMode.AcknowledgeUncertainty => grounding.StaleCorrection,
                        VoiceResponseMode.DeclineBoardCertainty => grounding.UnavailableCorrection,
                        _ => null
                    };
                    if (correctionText != null)
                    {
                        _ = _conversationProvider.SendContextualUpdateAsync(correctionText)
                            .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                                $"[VoiceGrounding] Correction injection failed: {t.Exception?.GetBaseException().Message}"),
                                TaskContinuationOptions.OnlyOnFaulted);
                    }
                }
            }
        }

        var message = new ChatMessage
        {
            Role = incomingMessage.Role,
            Intent = incomingMessage.Intent,
            Content = displayableText,
            Source = incomingMessage.Source,
            Timestamp = incomingMessage.Timestamp
        };
        ChatMessages.Insert(0, message);
        PersistChatIfActive(message);

        if (isDirectChatReply)
        {
            _brainEventRouter.OnDirectMessage(_pendingUserMessage!, message);
            _pendingUserMessage = null;
            return;
        }

        var agentName = SelectedAgent?.Name ?? "Ghost";
        var displayText = ShowGhostHint
            ? displayableText + $"\n\nHint: Tap {agentName} for Ghost Mode"
            : displayableText;
        AiDisplayContent = new AiDisplayContent
        {
            Text = displayText
        };
        OnPropertyChanged(nameof(HasAiContent));
        OnPropertyChanged(nameof(HasNoAiContent));

        SlidingPanelContent = new SlidingPanelContent
        {
            Title = "AI INSIGHT",
            Text = displayableText
        };
        OnPropertyChanged(nameof(HasPanelContent));
        OnPropertyChanged(nameof(HasTextPanelContent));

        if (IsFabActive)
            FabCardVariant = FabCardVariant.Text;

        if (_ghostModeService.IsGhostModeActive)
        {
            _ghostModeService.ShowCard(FabCardVariant.Text, null, displayableText, null,
                isAlert: false, isVoiceDelivered: IsVoiceChatActive);
        }

        _brainEventRouter.OnGeneralChat(displayableText);
    }

    private void AddSystemMessage(string text, bool debounce = false, bool routeToTimeline = true)
    {
        const int debounceMs = 3000;
        if (debounce && text == _lastSystemError && (DateTime.UtcNow - _lastSystemErrorAt).TotalMilliseconds < debounceMs)
            return;
        _lastSystemError = text;
        _lastSystemErrorAt = DateTime.UtcNow;

        var message = new ChatMessage { Role = MessageRole.System, Content = text };
        ChatMessages.Insert(0, message);

        if (routeToTimeline)
        {
            // Route to timeline as error event
            _brainEventRouter.OnError(text);
        }
        _sessionTrace?.TrackError(text, "system");

        // Show on ghost card if ghost mode is active (errors are alerts — no auto-dismiss)
        if (_ghostModeService.IsGhostModeActive)
        {
            StopGhostNotificationLoop(dismissNativeCard: false);
            _ghostModeService.ShowCard(FabCardVariant.Text, null, text, null, isAlert: true);
        }
    }

    /// <summary>
    /// Persists a chat message to the history database if a session is active.
    /// Skips System/Proactive messages — only User and Assistant messages are persisted.
    /// Fire-and-forget: SessionHistoryService has its own try-catch.
    /// </summary>
    private void PersistChatIfActive(ChatMessage message)
    {
        if (message.Role is MessageRole.System or MessageRole.Proactive) return;
        if (_sessionTrace?.SessionId is { } sid)
            _ = _historyService?.PersistChatMessageAsync(sid, message);
    }

    private async Task HandleTerminalBrainErrorAsync(BrainResult result)
    {
        if (!result.RequestDisconnect || ConnectionState != ConnectionState.Connected)
            return;

        var visibleMessage = result.AnalysisText ?? "Brain analysis paused after repeated failures.";

        _sessionTrace?.TrackEvent("brain.paused", new Dictionary<string, string>
        {
            ["fingerprint"] = result.ErrorFingerprint ?? "unknown",
            ["attempt_count"] = result.AttemptCount.ToString()
        });

        // Pause the brain only — do NOT tear down the session.
        // The connector, capture, voice, and chat all stay alive.
        // The user's intent to be connected is honored.
        _brainService.CancelAll();

        AddSystemMessage(visibleMessage, debounce: true, routeToTimeline: false);
    }

    [RelayCommand]
    private async Task ShowChessInfoAsync()
    {
        var toast = CommunityToolkit.Maui.Alerts.Toast.Make(
            "Start up a chess app on your machine then tap connect.",
            CommunityToolkit.Maui.Core.ToastDuration.Long,
            14);
        await toast.Show();
    }

    [RelayCommand]
    private void ClearChat()
    {
        ChatMessages.Clear();
        AiDisplayContent = null;
        OnPropertyChanged(nameof(HasAiContent));
        OnPropertyChanged(nameof(HasNoAiContent));
    }

#if DEBUG
    /// <summary>
    /// Fires a comprehensive demo sequence through BrainEventRouter to exercise
    /// all event types in the timeline. Only runs when using MockConversationProvider.
    /// </summary>
    private async Task RunMockDemoSequenceAsync()
    {
        if (_conversationProvider.ProviderName != "Mock Provider") return;

        await Task.Delay(500); // Let UI settle after connect

        // === IN-GAME CHECKPOINT: Screen capture events ===
        var gameTime = TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(15);
        _brainEventRouter.OnScreenCapture("demo-frame-001", gameTime, "demo");

        await Task.Delay(300);

        // Danger alert (blunder)
        _brainEventRouter.OnBrainHint(new BrainHint
        {
            Signal = "danger",
            Urgency = "high",
            Summary = "Blunder! Your queen is hanging on e4, opponent can take with knight.",
            Evaluation = -320,
            EvalDelta = -450,
            SuggestedMove = "Qd3"
        });
        // Duplicate danger to test horizontal stacking
        _brainEventRouter.OnBrainHint(new BrainHint
        {
            Signal = "danger",
            Urgency = "medium",
            Summary = "Bishop pinned to king — material loss incoming.",
            Evaluation = -180,
            EvalDelta = -120,
        });

        await Task.Delay(300);

        // Opportunity
        _brainEventRouter.OnBrainHint(new BrainHint
        {
            Signal = "opportunity",
            Urgency = "medium",
            Summary = "Fork available! Knight to c7 attacks both rook and king.",
            Evaluation = 280,
            EvalDelta = 200,
            SuggestedMove = "Nc7+"
        });

        await Task.Delay(300);

        // Sage Advice — multiple to test horizontal capsule stacking
        _brainEventRouter.OnBrainHint(new BrainHint
        {
            Signal = "sage",
            Urgency = "low",
            Summary = "Consider castling kingside to improve king safety before pushing pawns.",
            Evaluation = 50,
            SuggestedMove = "O-O"
        });
        _brainEventRouter.OnBrainHint(new BrainHint
        {
            Signal = "sage",
            Urgency = "low",
            Summary = "Control the center with d4 — opens lines for your bishop.",
            Evaluation = 40,
            SuggestedMove = "d4"
        });
        _brainEventRouter.OnBrainHint(new BrainHint
        {
            Signal = "sage",
            Urgency = "low",
            Summary = "Develop your knight to f3 to support e5 push.",
            Evaluation = 35,
            SuggestedMove = "Nf3"
        });

        await Task.Delay(300);

        // Assessment
        _brainEventRouter.OnBrainHint(new BrainHint
        {
            Signal = "assessment",
            Urgency = "low",
            Summary = "Position is roughly equal. Both sides have completed development.",
            Evaluation = 15
        });

        await Task.Delay(300);

        // Detection
        _brainEventRouter.OnBrainHint(new BrainHint
        {
            Signal = "detection",
            Urgency = "medium",
            Summary = "Opponent's bishop is eyeing your kingside — potential battery forming.",
            Evaluation = -40,
            EvalDelta = -30
        });

        await Task.Delay(300);

        // Image Analysis
        _brainEventRouter.OnImageAnalysis(
            "Board shows a Sicilian Najdorf position. White has castled kingside with pawns on e4, d4. " +
            "Black has a strong pawn structure on the queenside. Material is even.");

        await Task.Delay(300);

        // Proactive Alert (high urgency — would trigger voice)
        _brainEventRouter.OnProactiveAlert(
            new BrainHint
            {
                Signal = "danger",
                Urgency = "high",
                Summary = "Checkmate threat!",
                Evaluation = -9999,
                EvalDelta = -800,
                SuggestedMove = "Kg1"
            },
            "CRITICAL: Opponent has mate in 2 with Qh2+ then Qh1#. You must move your king immediately!");

        await Task.Delay(300);

        // Proactive Alert (medium urgency)
        _brainEventRouter.OnProactiveAlert(
            new BrainHint
            {
                Signal = "opportunity",
                Urgency = "medium",
                Summary = "Winning tactic available",
                Evaluation = 550,
                EvalDelta = 400,
                SuggestedMove = "Rxe8+"
            },
            "Your opponent left their back rank weak. Rxe8+ wins the exchange and opens up a mating attack.");

        await Task.Delay(300);

        // Direct Message pair
        var userMsg = new ChatMessage
        {
            Role = MessageRole.User,
            Content = "What should I do about the pressure on my f7 pawn?",
            DeliveryState = DeliveryState.Sent
        };
        var brainReply = new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = "Your f7 pawn is a common target in the opening. You can defend it by " +
                      "developing your knight to f6 or bishop to e7. Castling also removes " +
                      "your king from the f-file danger zone."
        };
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ChatMessages.Insert(0, userMsg);
            ChatMessages.Insert(0, brainReply);
        });
        _brainEventRouter.OnDirectMessage(userMsg, brainReply);

        await Task.Delay(500);

        // === OUT-GAME CHECKPOINT: Post-game analysis events ===
        _timelineFeed.NewConversationCheckpoint();

        await Task.Delay(300);

        // GameStateChange
        _timelineFeed.AddEvent(new Models.Timeline.TimelineEvent
        {
            Type = Models.Timeline.EventOutputType.GameStateChange,
            Icon = Models.Timeline.EventIconMap.GetIcon(Models.Timeline.EventOutputType.GameStateChange),
            CapsuleColorHex = Models.Timeline.EventIconMap.GetCapsuleColorHex(Models.Timeline.EventOutputType.GameStateChange),
            CapsuleStrokeHex = Models.Timeline.EventIconMap.GetCapsuleStrokeHex(Models.Timeline.EventOutputType.GameStateChange),
            Summary = "Game ended — White wins by resignation after 34 moves."
        });

        await Task.Delay(300);

        // AnalyticsResult — duplicates to test stacking
        _timelineFeed.AddEvent(new Models.Timeline.TimelineEvent
        {
            Type = Models.Timeline.EventOutputType.AnalyticsResult,
            Icon = Models.Timeline.EventIconMap.GetIcon(Models.Timeline.EventOutputType.AnalyticsResult),
            CapsuleColorHex = Models.Timeline.EventIconMap.GetCapsuleColorHex(Models.Timeline.EventOutputType.AnalyticsResult),
            CapsuleStrokeHex = Models.Timeline.EventIconMap.GetCapsuleStrokeHex(Models.Timeline.EventOutputType.AnalyticsResult),
            Summary = "Session stats: 82% accuracy, 3 blunders detected, avg response 4.2s"
        });
        _timelineFeed.AddEvent(new Models.Timeline.TimelineEvent
        {
            Type = Models.Timeline.EventOutputType.AnalyticsResult,
            Icon = Models.Timeline.EventIconMap.GetIcon(Models.Timeline.EventOutputType.AnalyticsResult),
            CapsuleColorHex = Models.Timeline.EventIconMap.GetCapsuleColorHex(Models.Timeline.EventOutputType.AnalyticsResult),
            CapsuleStrokeHex = Models.Timeline.EventIconMap.GetCapsuleStrokeHex(Models.Timeline.EventOutputType.AnalyticsResult),
            Summary = "Average centipawn loss: 42cp — good accuracy for rapid."
        });

        await Task.Delay(300);

        // HistoryRecall
        _timelineFeed.AddEvent(new Models.Timeline.TimelineEvent
        {
            Type = Models.Timeline.EventOutputType.HistoryRecall,
            Icon = Models.Timeline.EventIconMap.GetIcon(Models.Timeline.EventOutputType.HistoryRecall),
            CapsuleColorHex = Models.Timeline.EventIconMap.GetCapsuleColorHex(Models.Timeline.EventOutputType.HistoryRecall),
            CapsuleStrokeHex = Models.Timeline.EventIconMap.GetCapsuleStrokeHex(Models.Timeline.EventOutputType.HistoryRecall),
            Summary = "Similar position in your game on Feb 20 — you played Nf3 and won in 12 moves."
        });

        await Task.Delay(300);

        // GeneralChat
        _timelineFeed.AddEvent(new Models.Timeline.TimelineEvent
        {
            Type = Models.Timeline.EventOutputType.GeneralChat,
            Icon = Models.Timeline.EventIconMap.GetIcon(Models.Timeline.EventOutputType.GeneralChat),
            CapsuleColorHex = Models.Timeline.EventIconMap.GetCapsuleColorHex(Models.Timeline.EventOutputType.GeneralChat),
            CapsuleStrokeHex = Models.Timeline.EventIconMap.GetCapsuleStrokeHex(Models.Timeline.EventOutputType.GeneralChat),
            Summary = "Great game! Your endgame technique has improved since last session."
        });
    }
#endif

    [RelayCommand]
    private async Task ShowImageModalAsync()
    {
        if (AiDisplayContent?.ImageSource != null)
        {
            if (Shell.Current is not null)
                await Shell.Current.DisplayAlert("Image", "Full image view coming soon", "OK");
        }
    }
}
