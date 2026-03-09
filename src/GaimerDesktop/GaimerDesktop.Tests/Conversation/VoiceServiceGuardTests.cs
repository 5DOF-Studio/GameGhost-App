using FluentAssertions;
using Microsoft.Extensions.Configuration;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Conversation.Providers;
using Xunit;

namespace GaimerDesktop.Tests.Conversation;

/// <summary>
/// Tests for voice service guard paths that do NOT require WebSocket connections.
/// Covers null guards, empty API key handling, disconnected-state guards, and dispose.
/// </summary>
public class VoiceServiceGuardTests
{
    /// <summary>Creates an IConfiguration with no API keys (all empty).</summary>
    private static IConfiguration EmptyConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
    }

    /// <summary>A valid chess agent for testing.</summary>
    private static Agent TestAgent => Agents.Chess;

    #region GeminiLiveService Guards

    [Fact]
    public async Task Gemini_ConnectAsync_NullAgent_ThrowsArgumentNull()
    {
        using var service = new GeminiLiveService(EmptyConfig());

        var act = () => service.ConnectAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("agent");
    }

    [Fact]
    public async Task Gemini_ConnectAsync_EmptyApiKey_FiresErrorAndSetsErrorState()
    {
        using var service = new GeminiLiveService(EmptyConfig());
        string? errorMessage = null;
        ConnectionState? lastState = null;

        service.ErrorOccurred += (_, msg) => errorMessage = msg;
        service.ConnectionStateChanged += (_, state) => lastState = state;

        await service.ConnectAsync(TestAgent);

        service.State.Should().Be(ConnectionState.Error);
        lastState.Should().Be(ConnectionState.Error);
        errorMessage.Should().NotBeNullOrEmpty();
        errorMessage.Should().Contain("API key");
    }

    [Fact]
    public async Task Gemini_SendAudioAsync_WhenDisconnected_ReturnsImmediately()
    {
        using var service = new GeminiLiveService(EmptyConfig());

        service.State.Should().Be(ConnectionState.Disconnected);

        // Should not throw -- just returns early
        await service.SendAudioAsync(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Gemini_SendTextAsync_WhenDisconnected_ReturnsImmediately()
    {
        using var service = new GeminiLiveService(EmptyConfig());

        service.State.Should().Be(ConnectionState.Disconnected);

        // Should not throw -- just returns early
        await service.SendTextAsync("test message");
    }

    [Fact]
    public void Gemini_Dispose_IsIdempotent()
    {
        var service = new GeminiLiveService(EmptyConfig());

        // Double dispose should not throw
        service.Dispose();
        var act = () => service.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Gemini_InitialState_IsDisconnected()
    {
        using var service = new GeminiLiveService(EmptyConfig());

        service.State.Should().Be(ConnectionState.Disconnected);
        service.IsConnected.Should().BeFalse();
    }

    #endregion

    #region OpenAIRealtimeService Guards

    [Fact]
    public async Task OpenAI_ConnectAsync_NullAgent_ThrowsArgumentNull()
    {
        using var service = new OpenAIRealtimeService(EmptyConfig());

        var act = () => service.ConnectAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("agent");
    }

    [Fact]
    public async Task OpenAI_ConnectAsync_EmptyApiKey_FiresErrorAndSetsErrorState()
    {
        using var service = new OpenAIRealtimeService(EmptyConfig());
        string? errorMessage = null;
        ConnectionState? lastState = null;

        service.ErrorOccurred += (_, msg) => errorMessage = msg;
        service.ConnectionStateChanged += (_, state) => lastState = state;

        await service.ConnectAsync(TestAgent);

        service.State.Should().Be(ConnectionState.Error);
        lastState.Should().Be(ConnectionState.Error);
        errorMessage.Should().NotBeNullOrEmpty();
        errorMessage.Should().Contain("API key");
    }

    [Fact]
    public async Task OpenAI_SendAudioAsync_WhenDisconnected_ReturnsImmediately()
    {
        using var service = new OpenAIRealtimeService(EmptyConfig());

        service.State.Should().Be(ConnectionState.Disconnected);

        await service.SendAudioAsync(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task OpenAI_CommitAudioBuffer_WhenDisconnected_ReturnsImmediately()
    {
        using var service = new OpenAIRealtimeService(EmptyConfig());

        service.State.Should().Be(ConnectionState.Disconnected);

        await service.CommitAudioBufferAsync();
    }

    [Fact]
    public async Task OpenAI_CancelResponse_WhenDisconnected_ReturnsImmediately()
    {
        using var service = new OpenAIRealtimeService(EmptyConfig());

        service.State.Should().Be(ConnectionState.Disconnected);

        await service.CancelResponseAsync();
    }

    [Fact]
    public void OpenAI_Dispose_IsIdempotent()
    {
        var service = new OpenAIRealtimeService(EmptyConfig());

        service.Dispose();
        var act = () => service.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void OpenAI_InitialState_IsDisconnected()
    {
        using var service = new OpenAIRealtimeService(EmptyConfig());

        service.State.Should().Be(ConnectionState.Disconnected);
        service.IsConnected.Should().BeFalse();
    }

    #endregion

    #region Provider Property Tests

    [Fact]
    public void GeminiProvider_SupportsVideo_IsTrue()
    {
        using var provider = new GeminiConversationProvider(EmptyConfig());

        provider.SupportsVideo.Should().BeTrue();
    }

    [Fact]
    public void GeminiProvider_ProviderName_IsGeminiLive()
    {
        using var provider = new GeminiConversationProvider(EmptyConfig());

        provider.ProviderName.Should().Be("Gemini Live");
    }

    [Fact]
    public void OpenAIProvider_SupportsVideo_IsFalse()
    {
        using var provider = new OpenAIConversationProvider(EmptyConfig());

        provider.SupportsVideo.Should().BeFalse();
    }

    [Fact]
    public void OpenAIProvider_ProviderName_IsOpenAIRealtime()
    {
        using var provider = new OpenAIConversationProvider(EmptyConfig());

        provider.ProviderName.Should().Be("OpenAI Realtime");
    }

    #endregion

    #region Provider Guard Forwarding

    [Fact]
    public async Task GeminiProvider_ConnectAsync_EmptyApiKey_ForwardsError()
    {
        using var provider = new GeminiConversationProvider(EmptyConfig());
        string? errorMessage = null;
        provider.ErrorOccurred += (_, msg) => errorMessage = msg;

        await provider.ConnectAsync(TestAgent);

        provider.State.Should().Be(ConnectionState.Error);
        errorMessage.Should().Contain("API key");
    }

    [Fact]
    public async Task OpenAIProvider_ConnectAsync_EmptyApiKey_ForwardsError()
    {
        using var provider = new OpenAIConversationProvider(EmptyConfig());
        string? errorMessage = null;
        provider.ErrorOccurred += (_, msg) => errorMessage = msg;

        await provider.ConnectAsync(TestAgent);

        provider.State.Should().Be(ConnectionState.Error);
        errorMessage.Should().Contain("API key");
    }

    #endregion

    #region SendTextAsync Disconnected Guards

    [Fact]
    public async Task OpenAI_SendTextAsync_WhenDisconnected_ReturnsImmediately()
    {
        using var service = new OpenAIRealtimeService(EmptyConfig());

        service.State.Should().Be(ConnectionState.Disconnected);

        // The guard checks IsConnected first, then also _webSocket state -- should not throw
        await service.SendTextAsync("hello");
    }

    [Fact]
    public async Task Gemini_SendImageAsync_WhenDisconnected_ReturnsImmediately()
    {
        using var service = new GeminiLiveService(EmptyConfig());

        service.State.Should().Be(ConnectionState.Disconnected);

        await service.SendImageAsync(new byte[] { 1, 2, 3 });
    }

    #endregion
}
