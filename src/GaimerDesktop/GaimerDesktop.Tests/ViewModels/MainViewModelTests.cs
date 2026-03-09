using System.Threading.Channels;
using FluentAssertions;
using Moq;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.Services.Conversation;
using GaimerDesktop.ViewModels;
using Xunit;

namespace GaimerDesktop.Tests.ViewModels;

// ==========================================================================
// CONSTRUCTOR + PROPERTIES
// ==========================================================================

public class MainViewModel_Constructor_Tests : MainViewModelTestBase
{
    [Fact]
    public void Constructor_StartsRouterConsuming()
    {
        // Act
        var sut = CreateSut();

        // Assert
        MockRouter.Verify(r => r.StartConsuming(
            It.IsAny<ChannelReader<BrainResult>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_DefaultState_IsDisconnected()
    {
        var sut = CreateSut();

        sut.ConnectionState.Should().Be(ConnectionState.Disconnected);
        sut.IsConnected.Should().BeFalse();
        sut.IsConnecting.Should().BeFalse();
    }

    [Fact]
    public void Constructor_PassesBrainServiceResultsToRouter()
    {
        // Act
        var sut = CreateSut();

        // Assert - the channel reader passed to StartConsuming is the one from brain service
        MockRouter.Verify(r => r.StartConsuming(
            BrainChannel.Reader,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class MainViewModel_ComputedProperties_Tests : MainViewModelTestBase
{
    [Fact]
    public void CanConnect_NoAgentOrTarget_ReturnsFalse()
    {
        var sut = CreateSut();

        // No agent, no target
        sut.CanConnect.Should().BeFalse();
    }

    [Fact]
    public void CanConnect_AgentOnly_ReturnsFalse()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();

        sut.CanConnect.Should().BeFalse();
    }

    [Fact]
    public void CanConnect_WithAgentAndTarget_ReturnsTrue()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.SelectedTarget = CreateTestTarget();

        sut.CanConnect.Should().BeTrue();
    }

    [Fact]
    public void ConnectionBadgeText_Disconnected_ShowsOffline()
    {
        var sut = CreateSut();

        sut.ConnectionBadgeText.Should().Be("OFFLINE");
    }

    [Fact]
    public void ConnectionBadgeText_Connected_ShowsConnected()
    {
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;

        sut.ConnectionBadgeText.Should().Be("CONNECTED");
    }

    [Fact]
    public void ConnectButtonText_Disconnected_ShowsConnect()
    {
        var sut = CreateSut();

        sut.ConnectButtonText.Should().Be("CONNECT");
    }

    [Fact]
    public void ConnectButtonText_Connected_ShowsDisconnect()
    {
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;

        sut.ConnectButtonText.Should().Be("DISCONNECT");
    }

    [Fact]
    public void GeminiBackendText_ReturnsProviderName()
    {
        var sut = CreateSut();

        sut.GeminiBackendText.Should().Be("Mock Provider");
    }

    [Fact]
    public void CanSendTextMessage_NoAgent_ReturnsFalse()
    {
        var sut = CreateSut();
        sut.MessageDraftText = "hello";

        sut.CanSendTextMessage.Should().BeFalse();
    }

    [Fact]
    public void CanSendTextMessage_WithAgentAndText_ReturnsTrue()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "hello";

        sut.CanSendTextMessage.Should().BeTrue();
    }

    [Fact]
    public void ChatInputPlaceholder_NoAgent_ShowsSelectMessage()
    {
        var sut = CreateSut();

        sut.ChatInputPlaceholder.Should().Contain("Select an agent");
    }

    [Fact]
    public void ChatInputPlaceholder_WithAgent_ShowsAskMessage()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();

        sut.ChatInputPlaceholder.Should().Contain("Ask Leroy");
    }
}

// ==========================================================================
// TOGGLE CONNECTION
// ==========================================================================

public class MainViewModel_ToggleConnection_Tests : MainViewModelTestBase
{
    [Fact]
    public async Task ToggleConnection_Connect_CallsProviderConnectAsync()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.SelectedTarget = CreateTestTarget();

        MockConversation.Setup(c => c.ConnectAsync(It.IsAny<Agent>()))
            .Returns(Task.CompletedTask);
        MockConversation.Setup(c => c.IsConnected).Returns(true);

        await sut.ToggleConnectionCommand.ExecuteAsync(null);

        MockConversation.Verify(c => c.ConnectAsync(It.IsAny<Agent>()), Times.Once);
    }

    [Fact]
    public async Task ToggleConnection_Connect_TransitionsToInGame()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.SelectedTarget = CreateTestTarget();

        MockConversation.Setup(c => c.ConnectAsync(It.IsAny<Agent>()))
            .Returns(Task.CompletedTask);
        MockConversation.Setup(c => c.IsConnected).Returns(true);

        await sut.ToggleConnectionCommand.ExecuteAsync(null);

        MockSession.Verify(s => s.TransitionToInGame(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ToggleConnection_Connect_DoesNotStartRecording()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.SelectedTarget = CreateTestTarget();

        MockConversation.Setup(c => c.ConnectAsync(It.IsAny<Agent>()))
            .Returns(Task.CompletedTask);
        MockConversation.Setup(c => c.IsConnected).Returns(true);

        await sut.ToggleConnectionCommand.ExecuteAsync(null);

        // Connection no longer auto-starts mic — voice chat is independent
        MockAudio.Verify(a => a.StartRecordingAsync(It.IsAny<Action<byte[]>>()), Times.Never);
    }

    [Fact]
    public async Task ToggleConnection_Disconnect_CallsDisconnectAndStopSession()
    {
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;

        // ToggleConnectionAsync checks ConnectionState, not SelectedAgent
        // Shell.Current is null so ToggleConnectionAsync will NRE after StopSession on the navigation check.
        // We wrap in try/catch to verify mocks were called before NRE.
        try
        {
            await sut.ToggleConnectionCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected: Shell.Current is null in tests
        }

        MockConversation.Verify(c => c.DisconnectAsync(), Times.Once);
    }

    [Fact]
    public async Task ToggleConnection_NoAgent_DoesNotConnect()
    {
        var sut = CreateSut();
        // No agent selected, target selected
        sut.SelectedTarget = CreateTestTarget();

        await sut.ToggleConnectionCommand.ExecuteAsync(null);

        MockConversation.Verify(c => c.ConnectAsync(It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task ToggleConnection_ProviderNotConnectedAfterConnect_SkipsAudio()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.SelectedTarget = CreateTestTarget();

        MockConversation.Setup(c => c.ConnectAsync(It.IsAny<Agent>()))
            .Returns(Task.CompletedTask);
        // Provider remains not connected after ConnectAsync
        MockConversation.Setup(c => c.IsConnected).Returns(false);

        await sut.ToggleConnectionCommand.ExecuteAsync(null);

        MockAudio.Verify(a => a.StartRecordingAsync(It.IsAny<Action<byte[]>>()), Times.Never);
    }
}

// ==========================================================================
// STOP SESSION
// ==========================================================================

public class MainViewModel_StopSession_Tests : MainViewModelTestBase
{
    [Fact]
    public async Task StopSession_CancelsBrain()
    {
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;

        try
        {
            await sut.ToggleConnectionCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException) { }

        MockBrain.Verify(b => b.CancelAll(), Times.Once);
    }

    [Fact]
    public async Task StopSession_TransitionsToOutGame()
    {
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;

        try
        {
            await sut.ToggleConnectionCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException) { }

        MockSession.Verify(s => s.TransitionToOutGame(), Times.Once);
    }

    [Fact]
    public async Task StopSession_StopsAudioAndCapture()
    {
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;

        try
        {
            await sut.ToggleConnectionCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException) { }

        MockAudio.Verify(a => a.StopRecordingAsync(), Times.Once);
        MockAudio.Verify(a => a.StopPlaybackAsync(), Times.Once);
        MockCapture.Verify(c => c.StopCaptureAsync(), Times.Once);
    }

    [Fact]
    public async Task StopSession_StopsStockfish_WhenReady()
    {
        MockStockfish.Setup(s => s.IsReady).Returns(true);
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;

        try
        {
            await sut.ToggleConnectionCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException) { }

        MockStockfish.Verify(s => s.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task StopSession_SkipsStockfish_WhenNotReady()
    {
        MockStockfish.Setup(s => s.IsReady).Returns(false);
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;

        try
        {
            await sut.ToggleConnectionCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException) { }

        MockStockfish.Verify(s => s.StopAsync(), Times.Never);
    }

    [Fact]
    public async Task StopSession_ClearsUIState()
    {
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;
        sut.SelectedTarget = CreateTestTarget();

        try
        {
            await sut.ToggleConnectionCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException) { }

        sut.SelectedTarget.Should().BeNull();
        sut.IsFabActive.Should().BeFalse();
        sut.IsVoiceChatActive.Should().BeFalse();
        sut.IsAudioInActive.Should().BeFalse();
        sut.InputVolume.Should().Be(0f);
        sut.OutputVolume.Should().Be(0f);
        sut.PreviewImage.Should().BeNull();
        sut.AiDisplayContent.Should().BeNull();
        sut.SlidingPanelContent.Should().BeNull();
        sut.ChatMessages.Should().BeEmpty();
    }
}

// ==========================================================================
// SEND TEXT MESSAGE
// ==========================================================================

public class MainViewModel_SendTextMessage_Tests : MainViewModelTestBase
{
    [Fact]
    public async Task SendTextMessage_EmptyText_IsIgnored()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "   ";

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        sut.ChatMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendTextMessage_OutGame_AddsTwoMessages()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "What is a good opening?";

        // Not connected = out-game
        MockConversation.Setup(c => c.IsConnected).Returns(false);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        // Out-game adds user message + brain reply (newest first via Insert(0))
        sut.ChatMessages.Should().HaveCount(2);
        sut.ChatMessages[0].Role.Should().Be(MessageRole.Assistant);
        sut.ChatMessages[1].Role.Should().Be(MessageRole.User);
    }

    [Fact]
    public async Task SendTextMessage_OutGame_RoutesViaBrainEventRouter()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Test question";

        MockConversation.Setup(c => c.IsConnected).Returns(false);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        MockRouter.Verify(r => r.OnDirectMessage(
            It.Is<ChatMessage>(m => m.Role == MessageRole.User),
            It.Is<ChatMessage>(m => m.Role == MessageRole.Assistant)), Times.Once);
    }

    [Fact]
    public async Task SendTextMessage_OutGame_SetsDeliveryStateSent()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Hello";

        MockConversation.Setup(c => c.IsConnected).Returns(false);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        // User message is at index 1 (reply at 0 is newest)
        sut.ChatMessages[1].DeliveryState.Should().Be(DeliveryState.Sent);
    }

    [Fact]
    public async Task SendTextMessage_OutGame_CallsBrainChatAsync()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Tell me about the Sicilian Defense";

        MockConversation.Setup(c => c.IsConnected).Returns(false);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        MockBrain.Verify(b => b.ChatAsync(
            "Tell me about the Sicilian Defense",
            It.IsAny<IReadOnlyList<ChatMessage>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTextMessage_OutGame_ReplyContentFromBrain()
    {
        MockBrain.Setup(b => b.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("The Sicilian Defense is Black's most popular reply to 1.e4.");

        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Tell me about the Sicilian";

        MockConversation.Setup(c => c.IsConnected).Returns(false);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        sut.ChatMessages[0].Content.Should().Contain("Sicilian Defense");
    }

    [Fact]
    public async Task SendTextMessage_InGame_GetsContextAndSendsText()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "What is the best move?";

        MockConversation.Setup(c => c.IsConnected).Returns(true);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        // Verify context was requested
        MockBrainContext.Verify(bc => bc.GetContextForChatAsync(
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<ContextAssemblyInputs?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify text was sent to conversation provider
        MockConversation.Verify(c => c.SendTextAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTextMessage_InGame_AddsUserChatMessage()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "What is the best move?";

        MockConversation.Setup(c => c.IsConnected).Returns(true);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        sut.ChatMessages.Should().HaveCount(1);
        sut.ChatMessages[0].Role.Should().Be(MessageRole.User);
        sut.ChatMessages[0].Content.Should().Be("What is the best move?");
    }

    [Fact]
    public async Task SendTextMessage_ClearsDraftText()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Test";

        MockConversation.Setup(c => c.IsConnected).Returns(false);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        sut.MessageDraftText.Should().BeEmpty();
    }

    [Fact]
    public async Task SendTextMessage_InGame_SendFailure_SetsDeliveryFailed()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Fail me";

        MockConversation.Setup(c => c.IsConnected).Returns(true);
        MockConversation.Setup(c => c.SendTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection lost"));

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        // Error message is newest (index 0), user message (index 1) should be marked as failed
        sut.ChatMessages.Should().HaveCount(2);
        sut.ChatMessages[0].Role.Should().Be(MessageRole.System);
        sut.ChatMessages[1].DeliveryState.Should().Be(DeliveryState.Failed);
    }
}

// ==========================================================================
// FRAME CAPTURED EVENT HANDLER
// ==========================================================================

public class MainViewModel_FrameCaptured_Tests : MainViewModelTestBase
{
    [Fact]
    public void FrameCaptured_AppendsToVisualReel()
    {
        var sut = CreateSut();
        var png = CreateTestPng();

        RaiseFrameCaptured(png);

        MockReel.Verify(r => r.Append(It.IsAny<ReelMoment>()), Times.Once);
    }

    [Fact]
    public void FrameCaptured_UnchangedFrame_SkipsBrain()
    {
        // Frame has not changed per diff service
        MockDiff.Setup(d => d.HasChanged(It.IsAny<byte[]>(), It.IsAny<int>())).Returns(false);
        var sut = CreateSut();
        var png = CreateTestPng();

        RaiseFrameCaptured(png);

        MockBrain.Verify(b => b.SubmitImageAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void FrameCaptured_ChangedFrame_SubmitsToBrain()
    {
        // Frame has changed per diff service
        MockDiff.Setup(d => d.HasChanged(It.IsAny<byte[]>(), It.IsAny<int>())).Returns(true);
        MockBrain.Setup(b => b.IsBusy).Returns(false);
        MockBrain.Setup(b => b.SubmitImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var png = CreateTestPng();

        RaiseFrameCaptured(png);

        MockBrain.Verify(b => b.SubmitImageAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void FrameCaptured_BrainBusy_SkipsSubmission()
    {
        MockDiff.Setup(d => d.HasChanged(It.IsAny<byte[]>(), It.IsAny<int>())).Returns(true);
        MockBrain.Setup(b => b.IsBusy).Returns(true);

        var sut = CreateSut();
        var png = CreateTestPng();

        RaiseFrameCaptured(png);

        MockBrain.Verify(b => b.SubmitImageAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void FrameCaptured_UpdatesPreviewImage()
    {
        var sut = CreateSut();
        var png = CreateTestPng();

        RaiseFrameCaptured(png);

        // ScaleToHeight returns the original if height <= target, or scaled JPEG
        // Either way, PreviewImage should be set to something non-null
        sut.PreviewImage.Should().NotBeNull();
        sut.HasPreviewImage.Should().BeTrue();
    }

    [Fact]
    public void FrameCaptured_RoutesScreenCaptureEvent()
    {
        var sut = CreateSut();
        var png = CreateTestPng();

        RaiseFrameCaptured(png);

        MockRouter.Verify(r => r.OnScreenCapture(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.Is<string>(m => m == "auto")), Times.Once);
    }
}

// ==========================================================================
// CONNECTION STATE CHANGED EVENT HANDLER
// ==========================================================================

public class MainViewModel_ConnectionStateChanged_Tests : MainViewModelTestBase
{
    [Fact]
    public void ConnectionStateChanged_Connected_UpdatesState()
    {
        var sut = CreateSut();

        RaiseConnectionStateChanged(ConnectionState.Connected);

        sut.ConnectionState.Should().Be(ConnectionState.Connected);
        sut.IsConnected.Should().BeTrue();
        sut.ConnectionBadgeText.Should().Be("CONNECTED");
    }

    [Fact]
    public void ConnectionStateChanged_Disconnected_UpdatesState()
    {
        var sut = CreateSut();

        // First connect, then disconnect
        RaiseConnectionStateChanged(ConnectionState.Connected);
        RaiseConnectionStateChanged(ConnectionState.Disconnected);

        sut.ConnectionState.Should().Be(ConnectionState.Disconnected);
        sut.IsConnected.Should().BeFalse();
        sut.ConnectionBadgeText.Should().Be("OFFLINE");
    }

    [Fact]
    public void ConnectionStateChanged_Error_UpdatesState()
    {
        var sut = CreateSut();

        RaiseConnectionStateChanged(ConnectionState.Error);

        sut.ConnectionState.Should().Be(ConnectionState.Error);
        sut.ConnectionBadgeText.Should().Be("ERROR");
    }

    [Fact]
    public void ConnectionStateChanged_Disconnected_ExitsGhostMode()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        MockGhost.Setup(g => g.ExitGhostModeAsync()).Returns(Task.CompletedTask);

        var sut = CreateSut();

        RaiseConnectionStateChanged(ConnectionState.Disconnected);

        MockGhost.Verify(g => g.ExitGhostModeAsync(), Times.Once);
    }

    [Fact]
    public void ConnectionStateChanged_Connected_DoesNotAutoActivateVoiceChat()
    {
        var sut = CreateSut();
        sut.IsVoiceChatActive.Should().BeFalse();

        RaiseConnectionStateChanged(ConnectionState.Connected);

        // Connection no longer auto-activates voice chat — decoupled
        sut.IsVoiceChatActive.Should().BeFalse();
    }

    [Fact]
    public void ConnectionStateChanged_Disconnected_ClearsFabActive()
    {
        var sut = CreateSut();
        sut.IsFabActive = true;

        RaiseConnectionStateChanged(ConnectionState.Disconnected);

        sut.IsFabActive.Should().BeFalse();
    }
}

// ==========================================================================
// TEXT RECEIVED EVENT HANDLER
// ==========================================================================

public class MainViewModel_TextReceived_Tests : MainViewModelTestBase
{
    [Fact]
    public void TextReceived_AddsAssistantMessage()
    {
        var sut = CreateSut();

        RaiseTextReceived("Hello from AI");

        sut.ChatMessages.Should().HaveCount(1);
        sut.ChatMessages[0].Role.Should().Be(MessageRole.Assistant);
        sut.ChatMessages[0].Content.Should().Be("Hello from AI");
    }

    [Fact]
    public void TextReceived_RoutesToBrainEventRouter_AsGeneralChat()
    {
        var sut = CreateSut();

        RaiseTextReceived("Unsolicited insight");

        // Unsolicited text routes via OnGeneralChat
        MockRouter.Verify(r => r.OnGeneralChat("Unsolicited insight"), Times.Once);
    }

    [Fact]
    public void TextReceived_UpdatesAiDisplayContent()
    {
        var sut = CreateSut();

        RaiseTextReceived("New insight");

        sut.AiDisplayContent.Should().NotBeNull();
        sut.AiDisplayContent!.Text.Should().Be("New insight");
        sut.HasAiContent.Should().BeTrue();
    }

    [Fact]
    public void TextReceived_UpdatesSlidingPanelContent()
    {
        var sut = CreateSut();

        RaiseTextReceived("Panel text");

        sut.SlidingPanelContent.Should().NotBeNull();
        sut.SlidingPanelContent!.Title.Should().Be("AI INSIGHT");
        sut.SlidingPanelContent!.Text.Should().Be("Panel text");
        sut.HasPanelContent.Should().BeTrue();
    }

    [Fact]
    public void TextReceived_FabActive_ShowsTextCard()
    {
        var sut = CreateSut();
        sut.IsFabActive = true;

        RaiseTextReceived("Fab card text");

        sut.FabCardVariant.Should().Be(FabCardVariant.Text);
    }

    [Fact]
    public void TextReceived_GhostActive_ForwardsToGhost()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();

        RaiseTextReceived("Ghost panel text");

        MockGhost.Verify(g => g.ShowCard(
            FabCardVariant.Text,
            It.IsAny<string?>(),
            "Ghost panel text",
            It.IsAny<string?>()), Times.Once);
    }
}

// ==========================================================================
// VOICE CHAT DECOUPLED STATE
// ==========================================================================

public class MainViewModel_VoiceChatDecoupled_Tests : MainViewModelTestBase
{
    [Fact]
    public void VoiceChatToggle_WhenNotConnected_SnapsBackToFalse()
    {
        var sut = CreateSut();
        // Not connected
        sut.ConnectionState.Should().Be(ConnectionState.Disconnected);

        // Toggle voice chat ON — should snap back OFF
        sut.IsVoiceChatActive = true;

        sut.IsVoiceChatActive.Should().BeFalse();
    }

    [Fact]
    public async Task Connect_DoesNotStartRecording()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.SelectedTarget = CreateTestTarget();

        MockConversation.Setup(c => c.ConnectAsync(It.IsAny<Agent>()))
            .Returns(Task.CompletedTask);
        MockConversation.Setup(c => c.IsConnected).Returns(true);

        await sut.ToggleConnectionCommand.ExecuteAsync(null);

        // Connection should NOT start audio recording
        MockAudio.Verify(a => a.StartRecordingAsync(It.IsAny<Action<byte[]>>()), Times.Never);
        // Voice chat should remain OFF after connect
        sut.IsVoiceChatActive.Should().BeFalse();
    }

    [Fact]
    public void VoiceChatToggle_WhenConnected_StartsRecording()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        // Simulate connected state
        sut.ConnectionState = ConnectionState.Connected;

        // Toggle voice chat ON
        sut.IsVoiceChatActive = true;

        MockAudio.Verify(a => a.StartRecordingAsync(It.IsAny<Action<byte[]>>()), Times.Once);
    }

    [Fact]
    public void VoiceChatOff_StopsRecordingButKeepsConnection()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.ConnectionState = ConnectionState.Connected;

        // Toggle ON then OFF
        sut.IsVoiceChatActive = true;
        sut.IsVoiceChatActive = false;

        MockAudio.Verify(a => a.StopRecordingAsync(), Times.Once);
        // Connection should still be active (NOT disconnected)
        sut.ConnectionState.Should().Be(ConnectionState.Connected);
        MockConversation.Verify(c => c.DisconnectAsync(), Times.Never);
    }

    [Fact]
    public async Task Disconnect_ResetsVoiceChatActive()
    {
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;

        // Manually set voice chat active (simulating user toggle)
        sut.IsVoiceChatActive = true;

        // Disconnect
        try
        {
            await sut.ToggleConnectionCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected: Shell.Current is null in tests
        }

        sut.IsVoiceChatActive.Should().BeFalse();
    }

    // ── IsLive (Power Button State) ────────────────────────────────────────

    [Fact]
    public void IsLive_WhenMockBrain_ReturnsFalse()
    {
        MockBrain.Setup(b => b.ProviderName).Returns("Mock Brain");
        var sut = CreateSut();

        sut.IsLive.Should().BeFalse();
    }

    [Fact]
    public void IsLive_WhenRealBrain_ReturnsTrue()
    {
        MockBrain.Setup(b => b.ProviderName).Returns("OpenRouter (google/gemini-2.0-flash-001)");
        var sut = CreateSut();

        sut.IsLive.Should().BeTrue();
    }

    // ── AddSystemMessage Error Routing ─────────────────────────────────────

    [Fact]
    public async Task SendTextMessage_OutGame_OnFailure_RoutesErrorToTimeline()
    {
        MockBrain.Setup(b => b.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("API timeout"));
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Hello";

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        // Error should be routed to timeline via OnError
        MockRouter.Verify(r => r.OnError(It.Is<string>(s => s.Contains("Chat failed"))), Times.Once);
    }

    [Fact]
    public async Task SendTextMessage_OutGame_OnFailure_ShowsGhostCardWhenGhostActive()
    {
        MockBrain.Setup(b => b.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("API timeout"));
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Hello";

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        MockGhost.Verify(g => g.ShowCard(
            FabCardVariant.Text, null, It.Is<string>(s => s.Contains("Chat failed")), null), Times.Once);
    }

    [Fact]
    public async Task SendTextMessage_OutGame_OnFailure_NoGhostCardWhenGhostInactive()
    {
        MockBrain.Setup(b => b.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("API timeout"));
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(false);
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Hello";

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        MockGhost.Verify(g => g.ShowCard(
            It.IsAny<FabCardVariant>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }
}
