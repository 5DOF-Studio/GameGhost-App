using FluentAssertions;
using Moq;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Tests.ViewModels;

/// <summary>
/// Verifies mic audio (SendAudioAsync) behavior relative to exchange state.
/// Current architecture: audio always flows to provider (prompt-level wake enforcement).
/// Provider needs audio to produce transcripts for fuzzy wake detection.
/// When Porcupine (audio-level wake detection) is available, the audio gate
/// can be re-enabled and these tests updated to enforce it.
/// </summary>
public class MainViewModelExchangeGateTests : MainViewModelTestBase
{
    // ── Shared test setup ──────────────────────────────────────────────────

    /// <summary>
    /// Starts voice recording on the SUT and captures the PCM callback that
    /// MainViewModel registers with IAudioService.StartRecordingAsync.
    /// Returns the captured delegate so individual tests can invoke it with
    /// any audio payload they like.
    /// </summary>
    private async Task<Action<byte[]>> StartRecordingAndCaptureCallback(MainViewModel sut)
    {
        Action<byte[]>? capturedCallback = null;

        MockAudio
            .Setup(a => a.StartRecordingAsync(It.IsAny<Action<byte[]>>()))
            .Callback<Action<byte[]>>(cb => capturedCallback = cb)
            .Returns(Task.CompletedTask);

        // Put the view-model into Connected state so the voice toggle is accepted
        sut.ConnectionState = ConnectionState.Connected;
        sut.SelectedAgent = CreateTestAgent();

        // Flip the toggle — this fires StartRecordingAsync and captures the lambda
        sut.IsVoiceChatActive = true;

        // Give the fire-and-forget async path a moment to complete
        await Task.Delay(50);

        capturedCallback.Should().NotBeNull("StartRecordingAsync must have been called");
        return capturedCallback!;
    }

    // ── Exchange-dormant: audio still flows (prompt-level enforcement) ──────
    // Current architecture: audio always sent to provider so it can produce
    // transcripts for fuzzy wake phrase detection. Provider is instructed via
    // system prompt to stay silent unless wake phrase is detected.
    // When Porcupine (audio-level wake detection) is available, the audio gate
    // can be re-enabled here and these tests updated to assert Times.Never.

    [Fact]
    public async Task RecordingCallback_ExchangeDormant_AudioStillFlows_PromptEnforcement()
    {
        // Arrange
        MockExchangeManager = new Mock<IExchangeManager>();
        MockExchangeManager.Setup(e => e.IsExchangeActive).Returns(false);
        MockExchangeManager.Setup(e => e.SetMode(It.IsAny<AudioIntelligenceMode>()));

        MockConversation.Setup(c => c.IsConnected).Returns(true);
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Connected);
        MockConversation.Setup(c => c.SendAudioAsync(It.IsAny<byte[]>())).Returns(Task.CompletedTask);
        MockAudio.Setup(a => a.IsPlaying).Returns(false);

        var sut = CreateSut();
        var callback = await StartRecordingAndCaptureCallback(sut);

        // Act
        callback(new byte[] { 0x01, 0x02, 0x03 });
        await Task.Delay(50);

        // Assert — audio flows even when dormant (needed for transcript-based wake detection)
        MockConversation.Verify(
            c => c.SendAudioAsync(It.IsAny<byte[]>()),
            Times.Once,
            "Audio must flow to provider for wake phrase transcript detection");
    }

    [Fact]
    public async Task RecordingCallback_ExchangeDormant_MultipleChunks_AllForwarded()
    {
        // Arrange
        MockExchangeManager = new Mock<IExchangeManager>();
        MockExchangeManager.Setup(e => e.IsExchangeActive).Returns(false);
        MockExchangeManager.Setup(e => e.SetMode(It.IsAny<AudioIntelligenceMode>()));

        MockConversation.Setup(c => c.IsConnected).Returns(true);
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Connected);
        MockConversation.Setup(c => c.SendAudioAsync(It.IsAny<byte[]>())).Returns(Task.CompletedTask);
        MockAudio.Setup(a => a.IsPlaying).Returns(false);

        var sut = CreateSut();
        var callback = await StartRecordingAndCaptureCallback(sut);

        // Act
        for (int i = 0; i < 5; i++)
            callback(new byte[160]);
        await Task.Delay(50);

        // Assert — all chunks forwarded (provider needs continuous audio for VAD + transcription)
        MockConversation.Verify(
            c => c.SendAudioAsync(It.IsAny<byte[]>()),
            Times.Exactly(5),
            "All chunks must flow to provider for transcript-based wake detection");
    }

    // ── Exchange-active: audio must be forwarded ───────────────────────────

    [Fact]
    public async Task RecordingCallback_ExchangeActive_SendsAudio()
    {
        // Arrange
        MockExchangeManager = new Mock<IExchangeManager>();
        MockExchangeManager.Setup(e => e.IsExchangeActive).Returns(true);
        MockExchangeManager.Setup(e => e.SetMode(It.IsAny<AudioIntelligenceMode>()));

        MockConversation.Setup(c => c.IsConnected).Returns(true);
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Connected);
        MockConversation.Setup(c => c.SendAudioAsync(It.IsAny<byte[]>())).Returns(Task.CompletedTask);
        MockAudio.Setup(a => a.IsPlaying).Returns(false);

        var sut = CreateSut();
        var callback = await StartRecordingAndCaptureCallback(sut);

        // Act — deliver one PCM chunk while exchange is active
        var pcm = new byte[] { 0xAA, 0xBB, 0xCC };
        callback(pcm);

        // Give the fire-and-forget continuation a moment to schedule
        await Task.Delay(50);

        // Assert — audio must have been forwarded to the voice provider
        MockConversation.Verify(
            c => c.SendAudioAsync(It.IsAny<byte[]>()),
            Times.Once,
            "SendAudioAsync must be called once when exchange is active");
    }

    [Fact]
    public async Task RecordingCallback_ExchangeActive_MultipleChunks_AllForwarded()
    {
        // Arrange
        MockExchangeManager = new Mock<IExchangeManager>();
        MockExchangeManager.Setup(e => e.IsExchangeActive).Returns(true);
        MockExchangeManager.Setup(e => e.SetMode(It.IsAny<AudioIntelligenceMode>()));

        MockConversation.Setup(c => c.IsConnected).Returns(true);
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Connected);
        MockConversation.Setup(c => c.SendAudioAsync(It.IsAny<byte[]>())).Returns(Task.CompletedTask);
        MockAudio.Setup(a => a.IsPlaying).Returns(false);

        var sut = CreateSut();
        var callback = await StartRecordingAndCaptureCallback(sut);

        // Act — deliver three chunks while exchange is active
        callback(new byte[160]);
        callback(new byte[160]);
        callback(new byte[160]);

        await Task.Delay(100);

        // Assert — all three should have been forwarded
        MockConversation.Verify(
            c => c.SendAudioAsync(It.IsAny<byte[]>()),
            Times.Exactly(3),
            "All PCM chunks should be forwarded while exchange is active");
    }

    // ── Agent-is-playing guard: audio must be blocked even if exchange active ──

    [Fact]
    public async Task RecordingCallback_AgentIsPlaying_DoesNotSendAudio()
    {
        // Arrange — exchange active but audio output is playing (barge-in suppression)
        MockExchangeManager = new Mock<IExchangeManager>();
        MockExchangeManager.Setup(e => e.IsExchangeActive).Returns(true);
        MockExchangeManager.Setup(e => e.SetMode(It.IsAny<AudioIntelligenceMode>()));

        MockConversation.Setup(c => c.IsConnected).Returns(true);
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Connected);
        MockConversation.Setup(c => c.SendAudioAsync(It.IsAny<byte[]>())).Returns(Task.CompletedTask);
        // Simulate agent speaking
        MockAudio.Setup(a => a.IsPlaying).Returns(true);

        var sut = CreateSut();
        var callback = await StartRecordingAndCaptureCallback(sut);

        // Act
        callback(new byte[160]);

        // Assert — IsPlaying guard must block the audio
        MockConversation.Verify(
            c => c.SendAudioAsync(It.IsAny<byte[]>()),
            Times.Never,
            "SendAudioAsync must not be called when agent audio is playing");
    }

    // ── No exchange manager: audio still flows (no gate without manager) ────

    [Fact]
    public async Task RecordingCallback_NoExchangeManager_AudioStillFlows()
    {
        // Arrange — MockExchangeManager deliberately left null
        // Without exchange manager, audio flows unconditionally (pre-12A behavior preserved)

        MockConversation.Setup(c => c.IsConnected).Returns(true);
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Connected);
        MockConversation.Setup(c => c.SendAudioAsync(It.IsAny<byte[]>())).Returns(Task.CompletedTask);
        MockAudio.Setup(a => a.IsPlaying).Returns(false);

        // Do NOT set MockExchangeManager — it remains null
        var sut = CreateSut();
        var callback = await StartRecordingAndCaptureCallback(sut);

        // Act
        callback(new byte[160]);
        await Task.Delay(50);

        // Assert — no exchange manager = no gate = audio flows
        MockConversation.Verify(
            c => c.SendAudioAsync(It.IsAny<byte[]>()),
            Times.Once,
            "Audio must flow when no exchange manager is present");
    }

    // ── Disconnected provider: audio must be blocked even if exchange active ──

    [Fact]
    public async Task RecordingCallback_ProviderDisconnected_DoesNotSendAudio()
    {
        // Arrange — exchange active but conversation provider is disconnected
        MockExchangeManager = new Mock<IExchangeManager>();
        MockExchangeManager.Setup(e => e.IsExchangeActive).Returns(true);
        MockExchangeManager.Setup(e => e.SetMode(It.IsAny<AudioIntelligenceMode>()));

        // Provider disconnected mid-session
        MockConversation.Setup(c => c.IsConnected).Returns(false);
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Connected); // UI state still Connected for toggle
        MockConversation.Setup(c => c.SendAudioAsync(It.IsAny<byte[]>())).Returns(Task.CompletedTask);
        MockAudio.Setup(a => a.IsPlaying).Returns(false);

        var sut = CreateSut();
        var callback = await StartRecordingAndCaptureCallback(sut);

        // Act
        callback(new byte[160]);

        // Assert — IsConnected guard must block
        MockConversation.Verify(
            c => c.SendAudioAsync(It.IsAny<byte[]>()),
            Times.Never,
            "SendAudioAsync must not be called when provider is not connected");
    }
}
