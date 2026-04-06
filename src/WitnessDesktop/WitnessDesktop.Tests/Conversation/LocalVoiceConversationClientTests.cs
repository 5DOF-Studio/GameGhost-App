using WitnessDesktop.Models;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Tests.Conversation;

public class LocalVoiceConversationClientTests
{
    private static Agent CreateTestAgent() => new()
    {
        Key = "test",
        Id = "test-001",
        Name = "TestAgent",
        PrimaryGame = "Chess",
        IconImage = "icon.png",
        PortraitImage = "portrait.png",
        Description = "Test agent",
        Features = ["test"],
        SystemInstruction = "You are a test agent.",
        Type = AgentType.Chess
    };

    [Fact]
    public async Task ConnectAsync_SetsStateAndFiresConnectionStateChanged()
    {
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend);
        ConnectionState? receivedState = null;
        sut.ConnectionStateChanged += (_, state) => receivedState = state;

        await sut.ConnectAsync(CreateTestAgent());

        sut.IsConnected.Should().BeTrue();
        receivedState.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public async Task DisconnectAsync_ClearsStateAndFiresConnectionStateChanged()
    {
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend);
        await sut.ConnectAsync(CreateTestAgent());

        ConnectionState? receivedState = null;
        sut.ConnectionStateChanged += (_, state) => receivedState = state;

        await sut.DisconnectAsync();

        sut.IsConnected.Should().BeFalse();
        receivedState.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task SendTextAsync_FiresTextReceivedWithBackendResponse()
    {
        var backend = new FakeTextConversationBackend { Response = "I see a chess position!" };
        var sut = new LocalVoiceConversationClient(backend);
        await sut.ConnectAsync(CreateTestAgent());

        string? receivedText = null;
        sut.TextReceived += (_, text) => receivedText = text;

        await sut.SendTextAsync("What do you see?");

        receivedText.Should().Be("I see a chess position!");
        backend.LastHistory.Should().NotBeNull();
        backend.LastHistory!.Count.Should().Be(2); // system + user
        backend.LastHistory[0].Role.Should().Be("system");
        backend.LastHistory[1].Role.Should().Be("user");
        backend.LastHistory[1].Content.Should().Be("What do you see?");
    }

    [Fact]
    public async Task SendTextAsync_FiresErrorOccurredWhenBackendThrows()
    {
        var backend = new FakeTextConversationBackend { ShouldThrow = true };
        var sut = new LocalVoiceConversationClient(backend);
        await sut.ConnectAsync(CreateTestAgent());

        string? errorMessage = null;
        sut.ErrorOccurred += (_, msg) => errorMessage = msg;

        await sut.SendTextAsync("trigger error");

        errorMessage.Should().NotBeNull();
        errorMessage.Should().Contain("Local voice error");
    }

    [Fact]
    public async Task SendContextualUpdateAsync_AppendsToHistoryWithoutCallingBackend()
    {
        var backend = new FakeTextConversationBackend { Response = "reply" };
        var sut = new LocalVoiceConversationClient(backend);
        await sut.ConnectAsync(CreateTestAgent());

        await sut.SendContextualUpdateAsync("Brain says: e4 is best move");

        backend.SendCallCount.Should().Be(0);

        // Verify context appears in next send
        await sut.SendTextAsync("What should I play?");

        backend.SendCallCount.Should().Be(1);
        backend.LastHistory!.Count.Should().Be(3); // system personality + system context + user
        backend.LastHistory[1].Role.Should().Be("system");
        backend.LastHistory[1].Content.Should().Be("Brain says: e4 is best move");
    }

    [Fact]
    public async Task SendAudioAsync_DoesNotThrow()
    {
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend);

        var act = () => sut.SendAudioAsync(new byte[] { 0x01, 0x02 });

        await act.Should().NotThrowAsync();
        backend.SendCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ConversationHistory_MaintainedAcrossMultipleTurns()
    {
        var turnCount = 0;
        var backend = new FakeTextConversationBackend
        {
            ResponseFunc = _ => $"reply-{++turnCount}"
        };
        var sut = new LocalVoiceConversationClient(backend);
        await sut.ConnectAsync(CreateTestAgent());

        await sut.SendTextAsync("turn 1");
        await sut.SendTextAsync("turn 2");
        await sut.SendTextAsync("turn 3");

        // LastHistory is the snapshot sent to backend for the 3rd call:
        // system + user1 + asst1 + user2 + asst2 + user3 = 6
        // (asst3 is appended AFTER backend returns, so it's not in the snapshot)
        backend.LastHistory!.Count.Should().Be(6);
        backend.LastHistory[0].Role.Should().Be("system");
        backend.LastHistory[1].Content.Should().Be("turn 1");
        backend.LastHistory[2].Content.Should().Be("reply-1");
        backend.LastHistory[5].Content.Should().Be("turn 3");
    }

    [Fact]
    public async Task HistoryTrimming_DropsOldestUserAssistantPairs_PreservesSystemMessages()
    {
        var turnCount = 0;
        var backend = new FakeTextConversationBackend
        {
            ResponseFunc = _ => $"r-{++turnCount}"
        };
        var sut = new LocalVoiceConversationClient(backend);
        await sut.ConnectAsync(CreateTestAgent());

        // Send 22 turns (exceeds 20-turn cap)
        for (var i = 1; i <= 22; i++)
        {
            await sut.SendTextAsync($"msg-{i}");
        }

        // System message should still be first
        backend.LastHistory![0].Role.Should().Be("system");

        // User/assistant count should be capped at 40 (20 turns * 2)
        var userAssistantCount = backend.LastHistory.Count(m => m.Role is "user" or "assistant");
        userAssistantCount.Should().BeLessOrEqualTo(40);

        // Oldest messages should have been trimmed — msg-1 should be gone
        backend.LastHistory.Should().NotContain(m => m.Content == "msg-1");
        // Most recent should still be present
        backend.LastHistory.Should().Contain(m => m.Content == "msg-22");
    }

    [Fact]
    public async Task IsConnected_ReflectsLifecycle()
    {
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend);

        sut.IsConnected.Should().BeFalse();

        await sut.ConnectAsync(CreateTestAgent());
        sut.IsConnected.Should().BeTrue();

        await sut.DisconnectAsync();
        sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void RuntimeName_DelegatesToBackend()
    {
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend);

        sut.RuntimeName.Should().Be("fake");
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend);

        sut.Dispose();
        sut.Dispose(); // should not throw

        sut.IsConnected.Should().BeFalse();
    }

    private sealed class FakeTextConversationBackend : ILocalTextConversationBackend
    {
        public string Response { get; set; } = "default response";
        public Func<IReadOnlyList<ConversationMessage>, string>? ResponseFunc { get; set; }
        public bool ShouldThrow { get; set; }
        public IReadOnlyList<ConversationMessage>? LastHistory { get; private set; }
        public int SendCallCount { get; private set; }

        public string RuntimeName => "fake";

        public Task<string> SendAsync(IReadOnlyList<ConversationMessage> history, CancellationToken ct = default)
        {
            SendCallCount++;
            LastHistory = history.ToList();

            if (ShouldThrow)
                throw new HttpRequestException("Backend unavailable");

            var response = ResponseFunc?.Invoke(history) ?? Response;
            return Task.FromResult(response);
        }
    }
}
