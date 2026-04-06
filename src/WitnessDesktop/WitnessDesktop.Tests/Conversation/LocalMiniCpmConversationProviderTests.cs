using WitnessDesktop.Models;
using WitnessDesktop.Services.Conversation.Providers;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Tests.Conversation;

public class LocalMiniCpmConversationProviderTests
{
    private static Agent CreateTestAgent() => new()
    {
        Key = "test",
        Id = "test-agent",
        Name = "Test Agent",
        PrimaryGame = "Test",
        IconImage = "test.png",
        PortraitImage = "test.png",
        Description = "Test agent",
        Features = ["testing"],
        SystemInstruction = "You are a test agent.",
        Type = AgentType.General
    };

    [Fact]
    public async Task ConnectAsync_ForwardsStateChanges()
    {
        using var client = new FakeLocalAudioConversationClient();
        using var provider = new LocalMiniCpmConversationProvider(client);
        var states = new List<ConnectionState>();
        provider.ConnectionStateChanged += (_, state) => states.Add(state);

        await provider.ConnectAsync(CreateTestAgent());

        states.Should().ContainInOrder(ConnectionState.Connecting, ConnectionState.Connected);
        provider.State.Should().Be(ConnectionState.Connected);
        provider.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectAsync_ForwardsStateChanges()
    {
        using var client = new FakeLocalAudioConversationClient();
        using var provider = new LocalMiniCpmConversationProvider(client);
        await provider.ConnectAsync(CreateTestAgent());

        var states = new List<ConnectionState>();
        provider.ConnectionStateChanged += (_, state) => states.Add(state);

        await provider.DisconnectAsync();

        states.Should().ContainInOrder(ConnectionState.Disconnecting, ConnectionState.Disconnected);
        provider.State.Should().Be(ConnectionState.Disconnected);
        provider.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task AudioReceived_IsForwarded()
    {
        using var client = new FakeLocalAudioConversationClient();
        using var provider = new LocalMiniCpmConversationProvider(client);
        byte[]? received = null;
        provider.AudioReceived += (_, audio) => received = audio;

        await client.EmitAudioAsync([0x01, 0x02, 0x03]);

        received.Should().Equal(0x01, 0x02, 0x03);
    }

    [Fact]
    public async Task TextReceived_IsForwarded()
    {
        using var client = new FakeLocalAudioConversationClient();
        using var provider = new LocalMiniCpmConversationProvider(client);
        string? received = null;
        provider.TextReceived += (_, text) => received = text;

        await client.EmitTextAsync("local reply");

        received.Should().Be("local reply");
    }

    [Fact]
    public async Task SendContextualUpdateAsync_ForwardsToClient()
    {
        using var client = new FakeLocalAudioConversationClient();
        using var provider = new LocalMiniCpmConversationProvider(client);

        await provider.SendContextualUpdateAsync("watch the flank");

        client.ContextUpdates.Should().ContainSingle().Which.Should().Be("watch the flank");
    }

    [Fact]
    public async Task SendTextAsync_ForwardsToClient()
    {
        using var client = new FakeLocalAudioConversationClient();
        using var provider = new LocalMiniCpmConversationProvider(client);

        await provider.SendTextAsync("hello");

        client.SentTexts.Should().ContainSingle().Which.Should().Be("hello");
    }

    [Fact]
    public async Task SendAudioAsync_ForwardsToClient()
    {
        using var client = new FakeLocalAudioConversationClient();
        using var provider = new LocalMiniCpmConversationProvider(client);

        await provider.SendAudioAsync([0x10, 0x20]);

        client.SentAudio.Should().ContainSingle();
        client.SentAudio.Single().Should().Equal(0x10, 0x20);
    }

    [Fact]
    public async Task SendImageAsync_IsNoOp()
    {
        using var client = new FakeLocalAudioConversationClient();
        using var provider = new LocalMiniCpmConversationProvider(client);

        await provider.SendImageAsync([0x01]);

        client.SentAudio.Should().BeEmpty();
        client.SentTexts.Should().BeEmpty();
        client.ContextUpdates.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var client = new FakeLocalAudioConversationClient();
        var provider = new LocalMiniCpmConversationProvider(client);

        provider.Dispose();
        provider.Dispose();

        client.DisposeCount.Should().Be(1);
    }

    private sealed class FakeLocalAudioConversationClient : ILocalAudioConversationClient
    {
        private bool _disposed;

        public event EventHandler<ConnectionState>? ConnectionStateChanged;
        public event EventHandler<byte[]>? AudioReceived;
        public event EventHandler<string>? TextReceived;
        public event EventHandler? Interrupted;
        public event EventHandler<string>? ErrorOccurred;

        public bool IsConnected { get; private set; }
        public string RuntimeName => "FakeRuntime";
        public List<byte[]> SentAudio { get; } = [];
        public List<string> SentTexts { get; } = [];
        public List<string> ContextUpdates { get; } = [];
        public int DisposeCount { get; private set; }

        public Task ConnectAsync(Agent agent, CancellationToken cancellationToken = default)
        {
            ConnectionStateChanged?.Invoke(this, ConnectionState.Connecting);
            IsConnected = true;
            ConnectionStateChanged?.Invoke(this, ConnectionState.Connected);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectionStateChanged?.Invoke(this, ConnectionState.Disconnecting);
            IsConnected = false;
            ConnectionStateChanged?.Invoke(this, ConnectionState.Disconnected);
            return Task.CompletedTask;
        }

        public Task SendAudioAsync(byte[] audioData, CancellationToken cancellationToken = default)
        {
            SentAudio.Add(audioData);
            return Task.CompletedTask;
        }

        public Task SendTextAsync(string text, CancellationToken cancellationToken = default)
        {
            SentTexts.Add(text);
            return Task.CompletedTask;
        }

        public Task SendContextualUpdateAsync(string contextText, CancellationToken cancellationToken = default)
        {
            ContextUpdates.Add(contextText);
            return Task.CompletedTask;
        }

        public Task EmitAudioAsync(byte[] audio)
        {
            AudioReceived?.Invoke(this, audio);
            return Task.CompletedTask;
        }

        public Task EmitTextAsync(string text)
        {
            TextReceived?.Invoke(this, text);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeCount++;
        }
    }
}
