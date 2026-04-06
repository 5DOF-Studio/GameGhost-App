using WitnessDesktop.Models;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.Services.Conversation.Providers;

namespace WitnessDesktop.Tests.Conversation;

/// <summary>
/// Tests for MockConversationProvider — state machine, events, and idempotent disposal.
/// Note: ConnectAsync has a built-in 250ms simulated delay.
/// </summary>
public class MockConversationProviderTests
{
    private static Agent CreateTestAgent() => new()
    {
        Key = "test",
        Id = "Test Agent",
        Name = "Tester",
        PrimaryGame = "Test",
        IconImage = "test.png",
        PortraitImage = "test.png",
        Description = "Test agent",
        Features = ["testing"],
        SystemInstruction = "You are a test agent.",
        Type = AgentType.General
    };

    [Fact]
    public void InitialState_IsDisconnected()
    {
        using var provider = new MockConversationProvider();

        provider.State.Should().Be(ConnectionState.Disconnected);
        provider.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void ProviderName_IsMockProvider()
    {
        using var provider = new MockConversationProvider();

        provider.ProviderName.Should().Be("Mock Provider");
    }

    [Fact]
    public void SupportsVideo_IsFalse()
    {
        using var provider = new MockConversationProvider();

        provider.SupportsVideo.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_TransitionsToConnected()
    {
        using var provider = new MockConversationProvider();

        await provider.ConnectAsync(CreateTestAgent());

        provider.State.Should().Be(ConnectionState.Connected);
        provider.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_FiresConnectionStateChanged()
    {
        using var provider = new MockConversationProvider();
        var stateChanges = new List<ConnectionState>();
        provider.ConnectionStateChanged += (_, state) => stateChanges.Add(state);

        await provider.ConnectAsync(CreateTestAgent());

        // Should have transitioned through Connecting -> Connected
        stateChanges.Should().ContainInOrder(
            ConnectionState.Connecting,
            ConnectionState.Connected
        );
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_IsNoop()
    {
        using var provider = new MockConversationProvider();
        await provider.ConnectAsync(CreateTestAgent());

        var stateChanges = new List<ConnectionState>();
        provider.ConnectionStateChanged += (_, state) => stateChanges.Add(state);

        // Second connect should be a no-op (state != Disconnected)
        await provider.ConnectAsync(CreateTestAgent());

        stateChanges.Should().BeEmpty();
        provider.State.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public async Task DisconnectAsync_TransitionsToDisconnected()
    {
        using var provider = new MockConversationProvider();
        await provider.ConnectAsync(CreateTestAgent());

        await provider.DisconnectAsync();

        provider.State.Should().Be(ConnectionState.Disconnected);
        provider.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_FiresConnectionStateChanged()
    {
        using var provider = new MockConversationProvider();
        await provider.ConnectAsync(CreateTestAgent());

        var stateChanges = new List<ConnectionState>();
        provider.ConnectionStateChanged += (_, state) => stateChanges.Add(state);

        await provider.DisconnectAsync();

        // Should have transitioned through Disconnecting -> Disconnected
        stateChanges.Should().ContainInOrder(
            ConnectionState.Disconnecting,
            ConnectionState.Disconnected
        );
    }

    [Fact]
    public async Task SendAudioAsync_IsNoop()
    {
        using var provider = new MockConversationProvider();
        await provider.ConnectAsync(CreateTestAgent());

        // Should complete without throwing
        await provider.SendAudioAsync(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Fact]
    public async Task SendTextAsync_WhenConnected_EmitsTypedAndLegacyReply()
    {
        using var provider = new MockConversationProvider();
        await provider.ConnectAsync(CreateTestAgent());

        ChatMessage? typed = null;
        string? legacy = null;
        provider.MessageReceived += (_, message) => typed = message;
        provider.TextReceived += (_, text) => legacy = text;

        await provider.SendTextAsync("What should I do here?");

        await WaitForAsync(() =>
        {
            typed.Should().NotBeNull();
            legacy.Should().NotBeNullOrWhiteSpace();
        });

        typed!.Role.Should().Be(MessageRole.Assistant);
        typed.Intent.Should().Be(MessageIntent.GeneralChat);
        typed.Content.Should().Be(legacy);
        typed.Source.Should().Be("Mock Provider");
    }

    [Fact]
    public async Task SendTextAsync_WhenDisconnected_RaisesError()
    {
        using var provider = new MockConversationProvider();
        string? error = null;
        provider.ErrorOccurred += (_, message) => error = message;

        var act = () => provider.SendTextAsync("hello");

        await act.Should().ThrowAsync<InvalidOperationException>();
        error.Should().Contain("not connected");
    }

    [Fact]
    public async Task SendContextualUpdateAsync_InfluencesNextReply()
    {
        using var provider = new MockConversationProvider();
        await provider.ConnectAsync(CreateTestAgent());

        ChatMessage? reply = null;
        provider.MessageReceived += (_, message) => reply = message;

        await provider.SendContextualUpdateAsync("[CONTEXT UPDATE] White queen is pinned to the king.");
        await provider.SendTextAsync("What is the board state?");

        await WaitForAsync(() =>
        {
            reply.Should().NotBeNull();
        });

        reply!.Intent.Should().Be(MessageIntent.LiveGameInfo);
        reply.Content.Should().Contain("White queen is pinned");
    }

    [Fact]
    public async Task DisconnectAsync_DuringConnect_DoesNotReturnToConnected()
    {
        using var provider = new MockConversationProvider();

        var connectTask = provider.ConnectAsync(CreateTestAgent());
        await Task.Delay(25);
        await provider.DisconnectAsync();
        await connectTask;

        provider.State.Should().Be(ConnectionState.Disconnected);
        provider.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var provider = new MockConversationProvider();

        // Multiple disposes should not throw
        provider.Dispose();
        provider.Dispose();
        provider.Dispose();
    }

    private static async Task WaitForAsync(Action assertion, int attempts = 20, int delayMs = 25)
    {
        Exception? lastError = null;

        for (var i = 0; i < attempts; i++)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(delayMs);
        }

        throw lastError ?? new Xunit.Sdk.XunitException("Timed out waiting for assertion.");
    }
}
