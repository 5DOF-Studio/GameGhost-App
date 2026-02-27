using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Conversation;
using System.Threading;

namespace WitnessDesktop.ViewModels;

public partial class MainViewModel : ObservableObject, IQueryAttributable
{
    private readonly IAudioService _audioService;
    private readonly IWindowCaptureService _captureService;
    private readonly IConversationProvider _conversationProvider;
    private readonly IVisualReelService _visualReelService;
    private readonly IBrainContextService _brainContextService;
    private readonly ISessionManager _sessionManager;
    private readonly ITimelineFeed _timelineFeed;
    private readonly IBrainEventRouter _brainEventRouter;
    private readonly IGhostModeService _ghostModeService;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    private readonly SemaphoreSlim _stopSessionLock = new(1, 1);
    private CancellationTokenSource? _sessionCts;
    private DateTime _sessionStartedAt = DateTime.UtcNow;
    private ChatMessage? _pendingUserMessage;
    private DateTime _pendingUserMessageAt = DateTime.MinValue;
    private static readonly TimeSpan PendingMessageTimeout = TimeSpan.FromSeconds(30);
    private volatile bool _navigateToMinimalOnConnected;
    private string _lastSystemError = string.Empty;
    private DateTime _lastSystemErrorAt = DateTime.MinValue;

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
    private SlidingPanelContent? _slidingPanelContent;

    [ObservableProperty]
    private bool _isFabActive;

    [ObservableProperty]
    private FabCardVariant _fabCardVariant = FabCardVariant.None;

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
    public string ChatInputPlaceholder => SelectedAgent != null ? "Ask Game Ghost..." : "Select an agent to chat";
    public bool HasSelectedTarget => SelectedTarget != null;
    public bool IsConnected => ConnectionState == ConnectionState.Connected;
    public bool IsConnecting => ConnectionState == ConnectionState.Connecting;
    public bool HasAiContent => AiDisplayContent != null;
    public bool HasNoAiContent => AiDisplayContent == null;
    public bool HasPanelContent => SlidingPanelContent != null;

    // FAB overlay computed properties
    public bool IsFabEnabled => IsConnected;
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
        IBrainContextService brainContextService,
        ISessionManager sessionManager,
        ITimelineFeed timelineFeed,
        IBrainEventRouter brainEventRouter,
        IGhostModeService ghostModeService)
    {
        _audioService = audioService;
        _captureService = captureService;
        _conversationProvider = conversationProvider;
        _visualReelService = visualReelService;
        _brainContextService = brainContextService;
        _sessionManager = sessionManager;
        _timelineFeed = timelineFeed;
        _brainEventRouter = brainEventRouter;
        _ghostModeService = ghostModeService;

        // Audio callbacks can arrive on background threads; marshal to UI thread.
        _audioService.VolumeChanged += (_, e) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                InputVolume = e.InputVolume;
                OutputVolume = e.OutputVolume;
            });
        _audioService.ErrorOccurred += (_, e) =>
        {
            // Keep UI stable; consumers can surface these later.
            System.Diagnostics.Debug.WriteLine($"[Audio] {e.Message} {e.Exception}");
            Console.WriteLine($"[Audio] {e.Message} {e.Exception}");
        };
        _captureService.FrameCaptured += (_, rawFrame) =>
        {
            // Append to visual reel for brain context pipeline (GMR-006)
            var sourceTarget = _captureService.CurrentTarget != null
                ? $"{_captureService.CurrentTarget.ProcessName}|{_captureService.CurrentTarget.WindowTitle}"
                : "unknown";
            var frameRef = Guid.NewGuid().ToString();
            var moment = new ReelMoment
            {
                TimestampUtc = DateTime.UtcNow,
                SourceTarget = sourceTarget,
                FrameRef = frameRef,
                Confidence = 1.0
            };
            _visualReelService.Append(moment);

            var gameTime = DateTime.UtcNow - _sessionStartedAt;

            // Downscale for preview display (360px height covers 180pt box on Retina)
            var previewFrame = Services.ImageProcessor.ScaleToHeight(rawFrame, 360);

            // Marshal UI-bound property to MainThread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PreviewImage = previewFrame;
                _brainEventRouter.OnScreenCapture(frameRef, gameTime, "auto");
            });

            // Compress for model (50% scale, JPEG 60%) and send
            if (_conversationProvider.IsConnected)
            {
                var compressed = Services.ImageProcessor.ScaleAndCompress(rawFrame);
                if (compressed.Length > 0)
                {
                    _ = _conversationProvider.SendImageAsync(compressed)
                        .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                            $"[Capture] SendImageAsync failed: {t.Exception?.GetBaseException().Message}"),
                            TaskContinuationOptions.OnlyOnFaulted);
                }
            }
        };
        _conversationProvider.ConnectionStateChanged += (_, state) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
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
                        _ = _ghostModeService.ExitGhostModeAsync()
                            .ContinueWith(t => Console.WriteLine(
                                $"[GhostMode] ExitGhostModeAsync on disconnect failed: {t.Exception?.GetBaseException().Message}"),
                                TaskContinuationOptions.OnlyOnFaulted);

                    IsFabActive = false;
                }
            });
        _conversationProvider.TextReceived += (_, text) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Expire stale pending message to avoid misrouting unsolicited AI text
                var isDirectChatReply = _pendingUserMessage != null
                    && (DateTime.UtcNow - _pendingUserMessageAt) < PendingMessageTimeout;
                if (_pendingUserMessage != null && !isDirectChatReply)
                {
                    System.Diagnostics.Debug.WriteLine("[Chat] Pending user message expired — treating AI text as unsolicited.");
                    _pendingUserMessage = null;
                }

                var message = new ChatMessage
                {
                    Role = MessageRole.Assistant,
                    Content = text,
                    Intent = MessageIntent.GeneralChat
                };
                ChatMessages.Add(message);

                // Route to BrainEventRouter and update displays based on message type
                if (isDirectChatReply)
                {
                    // Direct chat reply: goes to timeline bubbles ONLY
                    // Do NOT update Live Chat Bar or Sliding Panel
                    _brainEventRouter.OnDirectMessage(_pendingUserMessage!, message);
                    _pendingUserMessage = null;
                }
                else
                {
                    // Update Live Chat Bar + Sliding Panel
                    AiDisplayContent = new AiDisplayContent
                    {
                        Text = text
                    };
                    OnPropertyChanged(nameof(HasAiContent));
                    OnPropertyChanged(nameof(HasNoAiContent));

                    SlidingPanelContent = new SlidingPanelContent
                    {
                        Title = "AI INSIGHT",
                        Text = text
                    };
                    OnPropertyChanged(nameof(HasPanelContent));

                    // Trigger FAB card when overlay is active
                    if (IsFabActive)
                    {
                        FabCardVariant = FabCardVariant.Text;
                    }

                    // Forward to native ghost mode panel
                    if (_ghostModeService.IsGhostModeActive)
                    {
                        _ghostModeService.ShowCard(FabCardVariant.Text, null, text, null);
                    }

                    // Route unsolicited AI text as GeneralChat (commentary icon).
                    // ImageAnalysis events come only from structured tool-call responses,
                    // NOT from raw TextReceived — this prevents the LLM (or mock) from
                    // ever producing a video_reel capsule via this path.
                    _brainEventRouter.OnGeneralChat(text);
                }
            });
        };
        _conversationProvider.AudioReceived += (_, pcmData) =>
        {
            // Queue audio for playback
            _ = _audioService.PlayAudioAsync(pcmData)
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[Audio] PlayAudioAsync failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
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
                AddSystemMessage(message, debounce: true);
            });
        };

        // Ghost mode callbacks — fired from native code on main thread.
        // BeginInvokeOnMainThread with async lambda creates async void delegate;
        // must catch to prevent unhandled exception crash.
        _ghostModeService.FabTapped += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await ToggleFabAsync(); }
                catch (Exception ex) { Console.WriteLine($"[GhostMode] ToggleFab failed: {ex.Message}"); }
            });
        };
        _ghostModeService.CardDismissed += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FabCardVariant = FabCardVariant.None;
            });
        };

        OnPropertyChanged(nameof(GeminiBackendText));
    }

    partial void OnInputVolumeChanged(float value)
    {
        OnPropertyChanged(nameof(ActivityVolume));
        UpdateFabVoiceState();
    }
    partial void OnOutputVolumeChanged(float value)
    {
        OnPropertyChanged(nameof(ActivityVolume));
        UpdateFabVoiceState();
    }
    partial void OnIsFabActiveChanged(bool value)
    {
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
    }
    partial void OnMessageDraftTextChanged(string value) => OnPropertyChanged(nameof(CanSendTextMessage));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("agentId", out var agentIdObj) && agentIdObj is string agentId)
        {
            SelectedAgent = Agents.GetByKey(agentId);
        }
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

        await _captureService.StartCaptureAsync(target);
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
            var currentRoute = Shell.Current.CurrentState.Location.OriginalString;
            if (currentRoute.Contains("MinimalView"))
            {
                await ExpandToMainViewAsync();
            }
        }
        else if (ConnectionState == ConnectionState.Disconnected && SelectedAgent != null && SelectedTarget != null)
        {
            _sessionCts?.Dispose();
            _sessionCts = new CancellationTokenSource();

            await _conversationProvider.ConnectAsync(SelectedAgent);
            if (!_conversationProvider.IsConnected)
            {
                Console.WriteLine("[GameGhost] ConnectAsync returned but IGeminiService is not connected; skipping microphone start.");
                return;
            }

            // Fire demo events when using mock provider (exercises all timeline event types)
            if (_conversationProvider.ProviderName == "Mock Provider")
            {
                _ = RunMockDemoSequenceAsync();
            }

            // Transition session to InGame state
            var gameId = CurrentTarget?.Handle.ToString() ?? "unknown";
            var gameType = CurrentTarget?.WindowTitle?.Contains("chess", StringComparison.OrdinalIgnoreCase) == true 
                ? "chess" 
                : SelectedAgent?.Type.ToString().ToLowerInvariant() ?? "general";
            var connectorName = CurrentTarget?.WindowTitle ?? "Unknown";
            _sessionManager.TransitionToInGame(gameId, gameType, connectorName);
            _sessionStartedAt = DateTime.UtcNow;

            // Phase 2 contract: audio capture is callback-based; callback must be fast/non-blocking.
            var sessionToken = _sessionCts?.Token ?? CancellationToken.None;
            await _audioService.StartRecordingAsync(pcm =>
            {
                // Non-blocking send - callback must return quickly
                // ECHO SUPPRESSION: Don't send audio while AI is speaking (prevents feedback loop)
                if (sessionToken.IsCancellationRequested) return;
                if (_conversationProvider.IsConnected && !_audioService.IsPlaying)
                {
                    _ = _conversationProvider.SendAudioAsync(pcm)
                        .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                            $"[Audio] SendAudioAsync failed: {t.Exception?.GetBaseException().Message}"),
                            TaskContinuationOptions.OnlyOnFaulted);
                }
            });
        }
    }

    private async Task NavigateToMinimalViewIfOnMainAsync()
    {
        var currentRoute = Shell.Current.CurrentState.Location.OriginalString;
        if (!currentRoute.Contains("MainPage", StringComparison.OrdinalIgnoreCase))
            return;

        await NavigateToMinimalViewAsync();
    }

    private async Task StopSessionAsync()
    {
        if (!await _stopSessionLock.WaitAsync(0)) return; // Already stopping
        try
        {
            // Cancel session-scoped operations (e.g. pending audio sends)
            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = null;

            // Transition session to OutGame state
            _sessionManager.TransitionToOutGame();

            // Stop audio (idempotent)
            await _audioService.StopRecordingAsync();
            await _audioService.StopPlaybackAsync();

            // Stop capture so "LIVE" and preview stop when disconnected
            await _captureService.StopCaptureAsync();

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
            InputVolume = 0f;
            OutputVolume = 0f;
            PreviewImage = null;

            AiDisplayContent = null;
            SlidingPanelContent = null;
            ChatMessages.Clear();

            OnPropertyChanged(nameof(HasAiContent));
            OnPropertyChanged(nameof(HasNoAiContent));
            OnPropertyChanged(nameof(HasPanelContent));
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
        await Shell.Current.GoToAsync("..");
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
        FabCardVariant = FabCardVariant.None;
    }

    [RelayCommand]
    private async Task ToggleFabAsync()
    {
        if (!_ghostModeService.IsSupported)
        {
            // Fallback: just toggle FAB overlay in MAUI window (existing behavior)
            IsFabActive = !IsFabActive;
            return;
        }

        if (_ghostModeService.IsGhostModeActive)
        {
            // Exit ghost mode
            await _ghostModeService.ExitGhostModeAsync();
            IsFabActive = false;
        }
        else
        {
            // Enter ghost mode
            IsFabActive = true;
            _ghostModeService.SetFabState(active: true, connected: IsConnected);

            // Resolve and set agent image path for native FAB
#if MACCATALYST
            if (SelectedAgent?.PortraitImage != null)
            {
                var imageName = SelectedAgent.PortraitImage
                    .Replace(".png", "")
                    .Replace(".jpg", "")
                    .ToLowerInvariant();
                var path = Foundation.NSBundle.MainBundle.PathForResource(imageName, "png");
                if (!string.IsNullOrEmpty(path))
                    _ghostModeService.SetAgentImage(path);
            }
#endif

            await _ghostModeService.EnterGhostModeAsync();
        }
    }

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
        ChatMessages.Add(message);

        if (!_conversationProvider.IsConnected)
        {
            // Out-game chat: route directly through BrainEventRouter
            // In production, this would call a text-only LLM endpoint
            message.DeliveryState = DeliveryState.Sent;

            var mockReply = new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = $"[Out-game] I hear you! Once we're connected to a game, I can give you real-time analysis. " +
                          $"For now, feel free to ask me anything about strategy or past games."
            };
            ChatMessages.Add(mockReply);
            _brainEventRouter.OnDirectMessage(message, mockReply);
            return;
        }

        _pendingUserMessage = message;
        _pendingUserMessageAt = DateTime.UtcNow;

        try
        {
            // GMR-009/010: Request context before send; prepend as text block (interim path)
            // Exclude the message we just added from context (it is the payload)
            var recentChat = ChatMessages.Take(Math.Max(0, ChatMessages.Count - 1)).ToList();
            var envelope = await _brainContextService.GetContextForChatAsync(
                DateTime.UtcNow,
                intent: "general",
                budgetTokens: BrainContextService.DefaultChatBudget,
                inputs: new ContextAssemblyInputs
                {
                    RecentChat = recentChat,
                    ActiveTarget = SelectedTarget
                });

            var contextBlock = _brainContextService.FormatAsPrefixedContextBlock(envelope);
            var textToSend = string.IsNullOrEmpty(contextBlock)
                ? text
                : $"{contextBlock}\n\n{text}";

            await _conversationProvider.SendTextAsync(textToSend);
            message.DeliveryState = DeliveryState.Sent;
        }
        catch (Exception ex)
        {
            message.DeliveryState = DeliveryState.Failed;
            AddSystemMessage($"Send failed: {ex.Message}");
        }
    }

    private void UpdateFabVoiceState()
    {
        if (!IsFabActive || !IsConnected) return;
        if (ActivityVolume > 0.01f && SlidingPanelContent == null)
        {
            FabCardVariant = FabCardVariant.Voice;
            if (_ghostModeService.IsGhostModeActive)
                _ghostModeService.ShowCard(FabCardVariant.Voice, null, "is talking...", null);
        }
        else if (FabCardVariant == FabCardVariant.Voice && ActivityVolume <= 0.01f)
        {
            FabCardVariant = FabCardVariant.None;
            if (_ghostModeService.IsGhostModeActive)
                _ghostModeService.DismissCard();
        }
    }

    private void AddSystemMessage(string text, bool debounce = false)
    {
        const int debounceMs = 3000;
        if (debounce && text == _lastSystemError && (DateTime.UtcNow - _lastSystemErrorAt).TotalMilliseconds < debounceMs)
            return;
        _lastSystemError = text;
        _lastSystemErrorAt = DateTime.UtcNow;

        var message = new ChatMessage { Role = MessageRole.System, Content = text };
        ChatMessages.Add(message);
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
            ChatMessages.Add(userMsg);
            ChatMessages.Add(brainReply);
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

    [RelayCommand]
    private async Task ShowImageModalAsync()
    {
        if (AiDisplayContent?.ImageSource != null)
        {
            await Shell.Current.DisplayAlert("Image", "Full image view coming soon", "OK");
        }
    }
}

