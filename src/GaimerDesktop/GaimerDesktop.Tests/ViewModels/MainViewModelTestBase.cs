using System.Threading.Channels;
using Moq;
using SkiaSharp;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.Services.Conversation;
using GaimerDesktop.ViewModels;

namespace GaimerDesktop.Tests.ViewModels;

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
    protected Mock<IBrainContextService> MockBrainContext { get; }
    protected Mock<ISessionManager> MockSession { get; }
    protected Mock<ITimelineFeed> MockTimeline { get; }
    protected Mock<IBrainEventRouter> MockRouter { get; }
    protected Mock<IGhostModeService> MockGhost { get; }
    protected Mock<IBrainService> MockBrain { get; }
    protected Mock<IFrameDiffService> MockDiff { get; }
    protected Mock<IStockfishService> MockStockfish { get; }

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
        MockBrainContext = new Mock<IBrainContextService>();
        MockSession = new Mock<ISessionManager>();
        MockTimeline = new Mock<ITimelineFeed>();
        MockRouter = new Mock<IBrainEventRouter>();
        MockGhost = new Mock<IGhostModeService>();
        MockBrain = new Mock<IBrainService>();
        MockDiff = new Mock<IFrameDiffService>();
        MockStockfish = new Mock<IStockfishService>();

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

        // IBrainService defaults
        MockBrain.Setup(b => b.ProviderName).Returns("Mock Brain");
        MockBrain.Setup(b => b.IsBusy).Returns(false);
        MockBrain.Setup(b => b.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mock brain reply for testing.");

        // IFrameDiffService: default to frame-changed (so brain submissions proceed)
        MockDiff.Setup(d => d.HasChanged(It.IsAny<byte[]>(), It.IsAny<int>())).Returns(true);

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
        MockBrainContext.Object,
        MockSession.Object,
        MockTimeline.Object,
        MockRouter.Object,
        MockGhost.Object,
        MockBrain.Object,
        MockDiff.Object,
        MockStockfish.Object);

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
        MockConversation.Raise(c => c.ConnectionStateChanged += null, MockConversation.Object, state);
    }

    /// <summary>
    /// Raises the TextReceived event on MockConversation.
    /// </summary>
    protected void RaiseTextReceived(string text)
    {
        MockConversation.Raise(c => c.TextReceived += null, MockConversation.Object, text);
    }
}
