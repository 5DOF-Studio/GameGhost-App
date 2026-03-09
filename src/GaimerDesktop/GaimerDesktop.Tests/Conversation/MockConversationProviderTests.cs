using GaimerDesktop.Models;
using GaimerDesktop.Services.Conversation;
using GaimerDesktop.Services.Conversation.Providers;

namespace GaimerDesktop.Tests.Conversation;

/// <summary>
/// Tests for MockConversationProvider — state machine, events, and idempotent disposal.
/// Note: ConnectAsync has a built-in 1500ms simulated delay.
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
    public void Dispose_IsIdempotent()
    {
        var provider = new MockConversationProvider();

        // Multiple disposes should not throw
        provider.Dispose();
        provider.Dispose();
        provider.Dispose();
    }
}
