using System.Threading.Channels;
using Moq;
using SkiaSharp;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.Services.Replay;
using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Tests.ViewModels;

/// <summary>
/// Base class for MainViewModel tests. Creates all 12 mocked dependencies
/// with sensible defaults and provides a CreateSut() factory.
/// </summary>
public abstract class MainViewModelTestBase
{
    protected Mock<IAudioService> MockAudio { get; }
    protected Mock<IWindowCaptureService> MockCapture { get; }
    protected Mock<IConversationProvider> MockConversation { get; }
    protected Mock<IVisualReelService> MockReel { get; }
    protected Mock<IObservationAdmissionGate> MockObservationAdmissionGate { get; }
    protected Mock<IObservationStore> MockObservationStore { get; }
    protected Mock<IBrainContextService> MockBrainContext { get; }
    protected Mock<ISessionManager> MockSession { get; }
    protected Mock<ITimelineFeed> MockTimeline { get; }
    protected Mock<IBrainEventRouter> MockRouter { get; }
    protected Mock<IGhostModeService> MockGhost { get; }
    protected Mock<IBrainService> MockBrain { get; }
    protected Mock<IFrameDiffService> MockDiff { get; }
    protected Mock<IStockfishService> MockStockfish { get; }
    protected Mock<IStructuralSettingsTracker> MockStructuralSettingsTracker { get; }
    protected Mock<ISessionTraceService>? MockSessionTrace { get; set; }
    protected Mock<IVoiceGroundingCoordinator>? MockVoiceGrounding { get; set; }
    protected Mock<IVoiceTranscriptStore>? MockVoiceTranscriptStore { get; set; }
    protected Mock<IReplayRecordingService>? MockReplayRecording { get; set; }

    /// <summary>
    /// Optional telemetry mock. Set before calling CreateSut() to inject telemetry.
    /// </summary>
    protected Mock<ITelemetryService>? MockTelemetry { get; set; }

    /// <summary>
    /// Optional exchange manager mock. Set before calling CreateSut() to inject exchange gating.
    /// When null, MainViewModel receives no exchange manager (pre-12A behavior — all audio blocked).
    /// </summary>
    protected Mock<IExchangeManager>? MockExchangeManager { get; set; }

    /// <summary>
    /// Optional Gaimer Team mock. Set before calling CreateSut() to inject team service.
    /// When null, MainViewModel receives no team service (team features disabled).
    /// </summary>
    protected Mock<IGaimerTeamService>? MockGaimerTeam { get; set; }

    /// <summary>
    /// Channel used to provide a valid ChannelReader for IBrainService.Results.
    /// Tests can write to this channel to simulate brain results.
    /// </summary>
    protected Channel<BrainResult> BrainChannel { get; }

    protected MainViewModelTestBase()
    {
        MockAudio = new Mock<IAudioService>();
        MockCapture = new Mock<IWindowCaptureService>();
        MockConversation = new Mock<IConversationProvider>();
        MockReel = new Mock<IVisualReelService>();
        MockObservationAdmissionGate = new Mock<IObservationAdmissionGate>();
        MockObservationStore = new Mock<IObservationStore>();
        MockBrainContext = new Mock<IBrainContextService>();
        MockSession = new Mock<ISessionManager>();
        MockTimeline = new Mock<ITimelineFeed>();
        MockRouter = new Mock<IBrainEventRouter>();
        MockGhost = new Mock<IGhostModeService>();
        MockBrain = new Mock<IBrainService>();
        MockDiff = new Mock<IFrameDiffService>();
        MockStockfish = new Mock<IStockfishService>();
        MockStructuralSettingsTracker = new Mock<IStructuralSettingsTracker>();

        // IBrainService.Results must return a valid ChannelReader (constructor calls StartConsuming)
        BrainChannel = Channel.CreateBounded<BrainResult>(1);
        MockBrain.Setup(b => b.Results).Returns(BrainChannel.Reader);

        // IConversationProvider defaults to Disconnected state
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Disconnected);
        MockConversation.Setup(c => c.IsConnected).Returns(false);
        MockConversation.Setup(c => c.ProviderName).Returns("Mock Provider");

        // ISessionManager.Context returns default SessionContext
        MockSession.Setup(s => s.Context).Returns(new SessionContext());
        MockSession.Setup(s => s.CurrentState).Returns(SessionState.OutGame);

        // IGhostModeService defaults (not active, not supported)
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(false);
        MockGhost.Setup(g => g.IsSupported).Returns(false);

        // IStockfishService defaults
        MockStockfish.Setup(s => s.IsReady).Returns(false);
        MockStockfish.Setup(s => s.IsInstalled).Returns(false);
        MockStructuralSettingsTracker.Setup(s => s.RequiresRebootstrap).Returns(false);
        MockStructuralSettingsTracker.Setup(s => s.PendingSettings).Returns(Array.Empty<string>());

        // IBrainService defaults
        MockBrain.Setup(b => b.ProviderName).Returns("Mock Brain");
        MockBrain.Setup(b => b.IsBusy).Returns(false);
        MockBrain.Setup(b => b.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mock brain reply for testing.");

        // IFrameDiffService: default to frame-changed (so brain submissions proceed)
        MockDiff.Setup(d => d.HasChanged(It.IsAny<byte[]>(), It.IsAny<int>())).Returns(true);
        MockDiff.Setup(d => d.HasChanged(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Returns(true);
        MockDiff.Setup(d => d.GetDistanceFromLast(It.IsAny<byte[]>(), It.IsAny<int>())).Returns(10);

        MockObservationAdmissionGate.Setup(g => g.Evaluate(It.IsAny<bool>(), It.IsAny<DateTime>()))
            .Returns(new ObservationAdmissionDecision
            {
                StoreObservation = true,
                SendToBrain = true,
                Reason = "test_default"
            });

        MockObservationStore.Setup(s => s.StoreAsync(It.IsAny<ObservationWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ObservationWriteRequest request, CancellationToken _) => new ObservationRecord
            {
                Id = request.Id,
                Kind = request.Kind,
                CapturedAtUtc = request.CapturedAtUtc,
                SourceTarget = request.SourceTarget,
                AgentKey = request.AgentKey,
                SessionId = request.SessionId,
                ArtifactPath = string.Empty,
                ByteSize = request.ArtifactBytes.LongLength
            });

        // IBrainContextService: return default envelope for chat context
        MockBrainContext.Setup(bc => bc.GetContextForChatAsync(
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<ContextAssemblyInputs?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SharedContextEnvelope());
        MockBrainContext.Setup(bc => bc.FormatAsPrefixedContextBlock(It.IsAny<SharedContextEnvelope>()))
            .Returns(string.Empty);
    }

    /// <summary>
    /// Creates a MainViewModel with all mocked dependencies.
    /// Constructor will call StartConsuming on the router.
    /// </summary>
    protected MainViewModel CreateSut() => new MainViewModel(
        MockAudio.Object,
        MockCapture.Object,
        MockConversation.Object,
        MockReel.Object,
        MockObservationAdmissionGate.Object,
        MockBrainContext.Object,
        MockSession.Object,
        MockTimeline.Object,
        MockRouter.Object,
        MockGhost.Object,
        MockBrain.Object,
        MockDiff.Object,
        MockStockfish.Object,
        MockStructuralSettingsTracker.Object,
        MockTelemetry?.Object,
        MockSessionTrace?.Object,
        MockVoiceGrounding?.Object,
        MockVoiceTranscriptStore?.Object,
        MockObservationStore.Object,
        replayRecording: MockReplayRecording?.Object,
        exchangeManager: MockExchangeManager?.Object,
        gaimerTeam: MockGaimerTeam?.Object);

    /// <summary>
    /// Helper: Creates an Agent for testing.
    /// </summary>
    protected static Agent CreateTestAgent(string key = "chess") => Agents.GetByKey(key)!;

    /// <summary>
    /// Helper: Creates a CaptureTarget for testing.
    /// </summary>
    protected static CaptureTarget CreateTestTarget(string title = "Chess.com") => new CaptureTarget
    {
        Handle = 12345,
        WindowTitle = title,
        ProcessName = "chrome"
    };

    /// <summary>
    /// Creates a valid 100x100 gradient PNG for FrameCaptured tests.
    /// ImageProcessor.ScaleAndCompress needs valid PNG to not return empty.
    /// </summary>
    protected static byte[] CreateTestPng(int width = 100, int height = 100)
    {
        using var bitmap = new SKBitmap(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)(x * 2), (byte)(y * 2), 128));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Raises the FrameCaptured event on MockCapture with the given bytes.
    /// </summary>
    protected void RaiseFrameCaptured(byte[] rawFrame)
    {
        MockCapture.Raise(c => c.FrameCaptured += null, MockCapture.Object, rawFrame);
    }

    /// <summary>
    /// Raises the ConnectionStateChanged event on MockConversation.
    /// </summary>
    protected void RaiseConnectionStateChanged(ConnectionState state)
    {
        // Update the mock's State property so MainViewModel's stale-state filter passes
        MockConversation.Setup(c => c.State).Returns(state);
        MockConversation.Raise(c => c.ConnectionStateChanged += null, MockConversation.Object, state);
    }

    /// <summary>
    /// Raises the TextReceived event on MockConversation.
    /// </summary>
    protected void RaiseTextReceived(string text)
    {
        MockConversation.Raise(c => c.TextReceived += null, MockConversation.Object, text);
    }

    /// <summary>
    /// Raises the typed MessageReceived event on MockConversation.
    /// </summary>
    protected void RaiseMessageReceived(ChatMessage message)
    {
        MockConversation.Raise(c => c.MessageReceived += null, MockConversation.Object, message);
    }
}
