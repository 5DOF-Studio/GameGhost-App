using System.Threading.Channels;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.ViewModels;
using WitnessDesktop.Tests.ViewModels;

namespace WitnessDesktop.Tests.Services;

/// <summary>
/// Tests for in-game text chat routing through brain service (A2)
/// and user message/brain reply appearance on timeline (A4).
/// </summary>
public class InGameChatRoutingTests : MainViewModelTestBase
{
    private MainViewModel CreateInGameSut()
    {
        // Set session to InGame and conversation to Connected
        MockSession.Setup(s => s.CurrentState).Returns(SessionState.InGame);
        MockSession.Setup(s => s.Context).Returns(new SessionContext { State = SessionState.InGame });
        MockConversation.Setup(c => c.IsConnected).Returns(true);
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Connected);

        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        return sut;
    }

    // ── In-game routing to brain (A2) ────────────────────────────────────

    [Fact]
    public async Task InGame_SendTextMessage_CallsSubmitQueryAsync()
    {
        var sut = CreateInGameSut();
        sut.MessageDraftText = "What should I play?";

        MockBrain.Setup(b => b.SubmitQueryAsync(
            It.IsAny<string>(),
            It.IsAny<SharedContextEnvelope>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        MockBrain.Verify(b => b.SubmitQueryAsync(
            "What should I play?",
            It.IsAny<SharedContextEnvelope>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InGame_SendTextMessage_DoesNotCallSendTextAsync()
    {
        var sut = CreateInGameSut();
        sut.MessageDraftText = "What should I play?";

        MockBrain.Setup(b => b.SubmitQueryAsync(
            It.IsAny<string>(),
            It.IsAny<SharedContextEnvelope>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        MockConversation.Verify(c => c.SendTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InGame_SendTextMessage_CallsOnUserMessage()
    {
        var sut = CreateInGameSut();
        sut.MessageDraftText = "What should I play?";

        MockBrain.Setup(b => b.SubmitQueryAsync(
            It.IsAny<string>(),
            It.IsAny<SharedContextEnvelope>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        MockRouter.Verify(r => r.OnUserMessage(
            It.Is<ChatMessage>(m => m.Role == MessageRole.User && m.Content == "What should I play?")),
            Times.Once);
    }

    [Fact]
    public async Task InGame_SendTextMessage_AddsUserMessageToChatMessages()
    {
        var sut = CreateInGameSut();
        sut.MessageDraftText = "What should I play?";

        MockBrain.Setup(b => b.SubmitQueryAsync(
            It.IsAny<string>(),
            It.IsAny<SharedContextEnvelope>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        sut.ChatMessages.Should().Contain(m => m.Role == MessageRole.User && m.Content == "What should I play?");
    }

    [Fact]
    public async Task InGame_SendTextMessage_SetsDeliveryStateToSent()
    {
        var sut = CreateInGameSut();
        sut.MessageDraftText = "What should I play?";

        MockBrain.Setup(b => b.SubmitQueryAsync(
            It.IsAny<string>(),
            It.IsAny<SharedContextEnvelope>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        var userMsg = sut.ChatMessages.FirstOrDefault(m => m.Role == MessageRole.User);
        userMsg.Should().NotBeNull();
        userMsg!.DeliveryState.Should().Be(DeliveryState.Sent);
    }

    [Fact]
    public async Task InGame_SendTextMessage_OnFailure_SetsDeliveryStateToFailed()
    {
        var sut = CreateInGameSut();
        sut.MessageDraftText = "What should I play?";

        MockBrain.Setup(b => b.SubmitQueryAsync(
            It.IsAny<string>(),
            It.IsAny<SharedContextEnvelope>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API timeout"));

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        var userMsg = sut.ChatMessages.FirstOrDefault(m => m.Role == MessageRole.User);
        userMsg.Should().NotBeNull();
        userMsg!.DeliveryState.Should().Be(DeliveryState.Failed);
    }

    // ── Out-game still uses ChatAsync (regression) ───────────────────────

    [Fact]
    public async Task OutGame_SendTextMessage_StillUsesChatAsync()
    {
        // Default setup is out-game + disconnected
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Hello!";

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        MockBrain.Verify(b => b.ChatAsync("Hello!", It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()), Times.Once);
        MockBrain.Verify(b => b.SubmitQueryAsync(It.IsAny<string>(), It.IsAny<SharedContextEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Context envelope built before submit ─────────────────────────────

    [Fact]
    public async Task InGame_SendTextMessage_BuildsContextEnvelope()
    {
        var sut = CreateInGameSut();
        sut.MessageDraftText = "What should I play?";

        MockBrain.Setup(b => b.SubmitQueryAsync(
            It.IsAny<string>(),
            It.IsAny<SharedContextEnvelope>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.SendTextMessageCommand.ExecuteAsync(null);

        MockBrainContext.Verify(bc => bc.GetContextForChatAsync(
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<ContextAssemblyInputs?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
