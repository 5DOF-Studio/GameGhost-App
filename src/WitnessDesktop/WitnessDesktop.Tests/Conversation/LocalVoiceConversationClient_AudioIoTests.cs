using WitnessDesktop.Models;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Tests.Conversation;

public class LocalVoiceConversationClient_AudioIoTests
{
    private const string PausedLocalFirstReason =
        "LocalFirst voice path is paused on post-mini-dev; maintain this suite on the local-first branch.";

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

    // ─── Audio-input happy path ───

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_WithSttAndTts_EmitsAudioReceived()
    {
        var stt = new FakeSpeechToTextProvider { Transcript = "hello world" };
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01, 0x02, 0x03 } };
        var backend = new FakeTextConversationBackend { Response = "I see a chess position!" };
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        byte[]? receivedAudio = null;
        sut.AudioReceived += (_, audio) => receivedAudio = audio;

        await sut.SendAudioAsync(new byte[] { 0xFF, 0xFE });

        receivedAudio.Should().NotBeNull();
        receivedAudio.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_SttTranscriptForwardedToBackend()
    {
        var stt = new FakeSpeechToTextProvider { Transcript = "what is the best move?" };
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01 } };
        var backend = new FakeTextConversationBackend { Response = "Play e4" };
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        await sut.SendAudioAsync(new byte[] { 0xFF });

        backend.SendCallCount.Should().Be(1);
        backend.LastHistory.Should().NotBeNull();
        backend.LastHistory!.Last().Content.Should().Be("what is the best move?");
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_BackendResponsePassedToTts()
    {
        var stt = new FakeSpeechToTextProvider { Transcript = "hello" };
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01 } };
        var backend = new FakeTextConversationBackend { Response = "Greetings, player!" };
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        await sut.SendAudioAsync(new byte[] { 0xFF });

        tts.LastSynthesizedText.Should().Be("Greetings, player!");
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_AlsoEmitsTextReceivedWithBackendResponse()
    {
        var stt = new FakeSpeechToTextProvider { Transcript = "hello" };
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01 } };
        var backend = new FakeTextConversationBackend { Response = "response text" };
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        string? receivedText = null;
        sut.TextReceived += (_, text) => receivedText = text;

        await sut.SendAudioAsync(new byte[] { 0xFF });

        receivedText.Should().Be("response text");
    }

    // ─── Graceful degradation ───

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_SttUnavailable_EmitsErrorOccurred()
    {
        var stt = new FakeSpeechToTextProvider { Available = false };
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01 } };
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        string? error = null;
        sut.ErrorOccurred += (_, msg) => error = msg;

        await sut.SendAudioAsync(new byte[] { 0xFF });

        error.Should().NotBeNull();
        error.Should().Contain("Speech recognition");
        backend.SendCallCount.Should().Be(0);
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_SttReturnsNull_EmitsErrorOccurred()
    {
        var stt = new FakeSpeechToTextProvider { Transcript = null };
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01 } };
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        string? error = null;
        sut.ErrorOccurred += (_, msg) => error = msg;

        await sut.SendAudioAsync(new byte[] { 0xFF });

        error.Should().NotBeNull();
        error.Should().Contain("transcri");
        backend.SendCallCount.Should().Be(0);
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_SttReturnsEmpty_EmitsErrorOccurred()
    {
        var stt = new FakeSpeechToTextProvider { Transcript = "" };
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01 } };
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        string? error = null;
        sut.ErrorOccurred += (_, msg) => error = msg;

        await sut.SendAudioAsync(new byte[] { 0xFF });

        error.Should().NotBeNull();
        backend.SendCallCount.Should().Be(0);
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_TtsUnavailable_StillEmitsTextButNoAudio()
    {
        var stt = new FakeSpeechToTextProvider { Transcript = "hello" };
        var tts = new FakeTextToSpeechProvider { Available = false };
        var backend = new FakeTextConversationBackend { Response = "text response" };
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        string? receivedText = null;
        byte[]? receivedAudio = null;
        sut.TextReceived += (_, text) => receivedText = text;
        sut.AudioReceived += (_, audio) => receivedAudio = audio;

        await sut.SendAudioAsync(new byte[] { 0xFF });

        receivedText.Should().Be("text response");
        receivedAudio.Should().BeNull();
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_TtsReturnsNull_StillEmitsTextButNoAudio()
    {
        var stt = new FakeSpeechToTextProvider { Transcript = "hello" };
        var tts = new FakeTextToSpeechProvider { Audio = null };
        var backend = new FakeTextConversationBackend { Response = "text response" };
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        byte[]? receivedAudio = null;
        string? receivedText = null;
        sut.AudioReceived += (_, audio) => receivedAudio = audio;
        sut.TextReceived += (_, text) => receivedText = text;

        await sut.SendAudioAsync(new byte[] { 0xFF });

        receivedText.Should().Be("text response");
        receivedAudio.Should().BeNull();
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_BackendThrows_EmitsErrorOccurredNoAudio()
    {
        var stt = new FakeSpeechToTextProvider { Transcript = "hello" };
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01 } };
        var backend = new FakeTextConversationBackend { ShouldThrow = true };
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        string? error = null;
        byte[]? receivedAudio = null;
        sut.ErrorOccurred += (_, msg) => error = msg;
        sut.AudioReceived += (_, audio) => receivedAudio = audio;

        await sut.SendAudioAsync(new byte[] { 0xFF });

        error.Should().NotBeNull();
        error.Should().Contain("Local voice error");
        receivedAudio.Should().BeNull();
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task SendAudioAsync_SttThrows_EmitsErrorOccurred()
    {
        var stt = new FakeSpeechToTextProvider { ShouldThrow = true };
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01 } };
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        string? error = null;
        sut.ErrorOccurred += (_, msg) => error = msg;

        await sut.SendAudioAsync(new byte[] { 0xFF });

        error.Should().NotBeNull();
        error.Should().Contain("Local voice error");
    }

    // ─── Backward compatibility: no STT/TTS constructors ───

    [Fact]
    public async Task SendTextAsync_StillWorksWithSttAndTts()
    {
        var stt = new FakeSpeechToTextProvider();
        var tts = new FakeTextToSpeechProvider { Audio = new byte[] { 0x01 } };
        var backend = new FakeTextConversationBackend { Response = "reply" };
        var sut = new LocalVoiceConversationClient(backend, stt, tts);
        await sut.ConnectAsync(CreateTestAgent());

        string? receivedText = null;
        sut.TextReceived += (_, text) => receivedText = text;

        await sut.SendTextAsync("hello");

        receivedText.Should().Be("reply");
    }

    [Fact(Skip = PausedLocalFirstReason)]
    public async Task Constructor_WithoutSttTts_SendAudioEmitsError()
    {
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend);
        await sut.ConnectAsync(CreateTestAgent());

        string? error = null;
        sut.ErrorOccurred += (_, msg) => error = msg;

        await sut.SendAudioAsync(new byte[] { 0xFF });

        error.Should().NotBeNull();
        error.Should().Contain("Speech recognition");
    }

    // ─── Speech capability properties ───

    [Fact]
    public void SpeechInputAvailable_ReflectsSttProvider()
    {
        var stt = new FakeSpeechToTextProvider { Available = true };
        var tts = new FakeTextToSpeechProvider();
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend, stt, tts);

        sut.SpeechInputAvailable.Should().BeTrue();
    }

    [Fact]
    public void SpeechInputAvailable_FalseWhenSttUnavailable()
    {
        var stt = new FakeSpeechToTextProvider { Available = false };
        var tts = new FakeTextToSpeechProvider();
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend, stt, tts);

        sut.SpeechInputAvailable.Should().BeFalse();
    }

    [Fact]
    public void SpeechOutputAvailable_ReflectsTtsProvider()
    {
        var stt = new FakeSpeechToTextProvider();
        var tts = new FakeTextToSpeechProvider { Available = true };
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend, stt, tts);

        sut.SpeechOutputAvailable.Should().BeTrue();
    }

    [Fact]
    public void SpeechOutputAvailable_FalseWhenNoTts()
    {
        var backend = new FakeTextConversationBackend();
        var sut = new LocalVoiceConversationClient(backend);

        sut.SpeechOutputAvailable.Should().BeFalse();
    }

    // ─── Fakes ───

    private sealed class FakeTextConversationBackend : ILocalTextConversationBackend
    {
        public string Response { get; set; } = "default response";
        public bool ShouldThrow { get; set; }
        public IReadOnlyList<ConversationMessage>? LastHistory { get; private set; }
        public int SendCallCount { get; private set; }
        public string RuntimeName => "fake";

        public Task<string> SendAsync(IReadOnlyList<ConversationMessage> history, CancellationToken ct = default)
        {
            SendCallCount++;
            LastHistory = history.ToList();
            if (ShouldThrow) throw new HttpRequestException("Backend unavailable");
            return Task.FromResult(Response);
        }
    }

    private sealed class FakeSpeechToTextProvider : ISpeechToTextProvider
    {
        public bool Available { get; set; } = true;
        public string? Transcript { get; set; } = "transcribed text";
        public bool ShouldThrow { get; set; }

        public bool IsAvailable => Available;
        public string EngineName => "Fake STT";

        public Task<string?> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("STT engine failed");
            return Task.FromResult(Transcript);
        }
    }

    private sealed class FakeTextToSpeechProvider : ITextToSpeechProvider
    {
        public bool Available { get; set; } = true;
        public byte[]? Audio { get; set; } = new byte[] { 0x01 };
        public string? LastSynthesizedText { get; private set; }

        public bool IsAvailable => Available;
        public string EngineName => "Fake TTS";

        public Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default)
        {
            LastSynthesizedText = text;
            return Task.FromResult(Audio);
        }
    }
}
