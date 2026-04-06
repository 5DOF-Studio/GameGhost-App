using WitnessDesktop.Platforms.MacCatalyst;

namespace WitnessDesktop.Tests.Services.Local;

/// <summary>
/// Tests for the Mac speech interop seam and provider behavior.
/// Uses a mock IGaimerSpeechInterop to verify:
/// - transcript string marshaling
/// - PCM buffer marshaling
/// - native error surfaced cleanly
/// - provider availability when native helper unavailable
/// - MacSpeechToTextProvider and MacTextToSpeechProvider behavior
/// </summary>
public class MacSpeechInteropTests
{
    // ─── STT Provider Tests ───

    [Fact]
    public async Task SttProvider_TranscribeAsync_ReturnsTranscriptFromInterop()
    {
        var interop = new FakeGaimerSpeechInterop
        {
            SttAvailable = true,
            TranscriptResult = "hello world"
        };
        var sut = new MacSpeechToTextProvider(interop);

        var result = await sut.TranscribeAsync(new byte[] { 0x01, 0x02 });

        result.Should().Be("hello world");
    }

    [Fact]
    public async Task SttProvider_TranscribeAsync_ReturnsNullWhenInteropReturnsNull()
    {
        var interop = new FakeGaimerSpeechInterop
        {
            SttAvailable = true,
            TranscriptResult = null
        };
        var sut = new MacSpeechToTextProvider(interop);

        var result = await sut.TranscribeAsync(new byte[] { 0x01 });

        result.Should().BeNull();
    }

    [Fact]
    public async Task SttProvider_TranscribeAsync_ReturnsNullWhenUnavailable()
    {
        var interop = new FakeGaimerSpeechInterop { SttAvailable = false };
        var sut = new MacSpeechToTextProvider(interop);

        var result = await sut.TranscribeAsync(new byte[] { 0x01 });

        result.Should().BeNull();
        interop.TranscribeCallCount.Should().Be(0, "should not call native when unavailable");
    }

    [Fact]
    public async Task SttProvider_TranscribeAsync_ReturnsNullOnEmptyAudio()
    {
        var interop = new FakeGaimerSpeechInterop { SttAvailable = true };
        var sut = new MacSpeechToTextProvider(interop);

        var result = await sut.TranscribeAsync(Array.Empty<byte>());

        result.Should().BeNull();
        interop.TranscribeCallCount.Should().Be(0);
    }

    [Fact]
    public async Task SttProvider_TranscribeAsync_ReturnsNullOnNullAudio()
    {
        var interop = new FakeGaimerSpeechInterop { SttAvailable = true };
        var sut = new MacSpeechToTextProvider(interop);

        var result = await sut.TranscribeAsync(null!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SttProvider_TranscribeAsync_HandlesInteropException()
    {
        var interop = new FakeGaimerSpeechInterop
        {
            SttAvailable = true,
            ShouldThrowOnTranscribe = true
        };
        var sut = new MacSpeechToTextProvider(interop);

        var result = await sut.TranscribeAsync(new byte[] { 0x01 });

        result.Should().BeNull("should return null on interop error, not throw");
    }

    [Fact]
    public void SttProvider_IsAvailable_ReflectsInterop()
    {
        var interop = new FakeGaimerSpeechInterop { SttAvailable = true };
        var sut = new MacSpeechToTextProvider(interop);

        sut.IsAvailable.Should().BeTrue();
        sut.EngineName.Should().Be("Apple Speech");
    }

    [Fact]
    public void SttProvider_IsAvailable_FalseWhenNativeUnavailable()
    {
        var interop = new FakeGaimerSpeechInterop { SttAvailable = false };
        var sut = new MacSpeechToTextProvider(interop);

        sut.IsAvailable.Should().BeFalse();
    }

    // ─── TTS Provider Tests ───

    [Fact]
    public async Task TtsProvider_SynthesizeAsync_ReturnsPcmFromInterop()
    {
        var pcm = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var interop = new FakeGaimerSpeechInterop
        {
            TtsAvailable = true,
            SynthesisResult = pcm
        };
        var sut = new MacTextToSpeechProvider(interop);

        var result = await sut.SynthesizeAsync("hello");

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(pcm);
    }

    [Fact]
    public async Task TtsProvider_SynthesizeAsync_ReturnsNullWhenInteropReturnsNull()
    {
        var interop = new FakeGaimerSpeechInterop
        {
            TtsAvailable = true,
            SynthesisResult = null
        };
        var sut = new MacTextToSpeechProvider(interop);

        var result = await sut.SynthesizeAsync("hello");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TtsProvider_SynthesizeAsync_ReturnsNullWhenUnavailable()
    {
        var interop = new FakeGaimerSpeechInterop { TtsAvailable = false };
        var sut = new MacTextToSpeechProvider(interop);

        var result = await sut.SynthesizeAsync("hello");

        result.Should().BeNull();
        interop.SynthesizeCallCount.Should().Be(0, "should not call native when unavailable");
    }

    [Fact]
    public async Task TtsProvider_SynthesizeAsync_ReturnsNullOnEmptyText()
    {
        var interop = new FakeGaimerSpeechInterop { TtsAvailable = true };
        var sut = new MacTextToSpeechProvider(interop);

        var result = await sut.SynthesizeAsync("");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TtsProvider_SynthesizeAsync_ReturnsNullOnWhitespaceText()
    {
        var interop = new FakeGaimerSpeechInterop { TtsAvailable = true };
        var sut = new MacTextToSpeechProvider(interop);

        var result = await sut.SynthesizeAsync("   ");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TtsProvider_SynthesizeAsync_HandlesInteropException()
    {
        var interop = new FakeGaimerSpeechInterop
        {
            TtsAvailable = true,
            ShouldThrowOnSynthesize = true
        };
        var sut = new MacTextToSpeechProvider(interop);

        var result = await sut.SynthesizeAsync("hello");

        result.Should().BeNull("should return null on interop error, not throw");
    }

    [Fact]
    public void TtsProvider_IsAvailable_ReflectsInterop()
    {
        var interop = new FakeGaimerSpeechInterop { TtsAvailable = true };
        var sut = new MacTextToSpeechProvider(interop);

        sut.IsAvailable.Should().BeTrue();
        sut.EngineName.Should().Be("Apple TTS");
    }

    [Fact]
    public void TtsProvider_IsAvailable_FalseWhenNativeUnavailable()
    {
        var interop = new FakeGaimerSpeechInterop { TtsAvailable = false };
        var sut = new MacTextToSpeechProvider(interop);

        sut.IsAvailable.Should().BeFalse();
    }

    // ─── Interop seam: text forwarded correctly ───

    [Fact]
    public async Task TtsProvider_ForwardsExactTextToInterop()
    {
        var interop = new FakeGaimerSpeechInterop
        {
            TtsAvailable = true,
            SynthesisResult = new byte[] { 0x01 }
        };
        var sut = new MacTextToSpeechProvider(interop);

        await sut.SynthesizeAsync("Play e4 to win the game!");

        interop.LastSynthesizedText.Should().Be("Play e4 to win the game!");
    }

    [Fact]
    public async Task SttProvider_ForwardsExactAudioToInterop()
    {
        var inputAudio = new byte[] { 0xFF, 0xFE, 0xFD };
        var interop = new FakeGaimerSpeechInterop
        {
            SttAvailable = true,
            TranscriptResult = "test"
        };
        var sut = new MacSpeechToTextProvider(interop);

        await sut.TranscribeAsync(inputAudio);

        interop.LastTranscribedAudio.Should().BeEquivalentTo(inputAudio);
    }

    // ─── Cancellation ───

    [Fact]
    public async Task SttProvider_TranscribeAsync_ReturnsNullOnCancellation()
    {
        var interop = new FakeGaimerSpeechInterop
        {
            SttAvailable = true,
            ShouldCancelOnTranscribe = true
        };
        var sut = new MacSpeechToTextProvider(interop);

        var result = await sut.TranscribeAsync(new byte[] { 0x01 });

        result.Should().BeNull();
    }

    [Fact]
    public async Task TtsProvider_SynthesizeAsync_ReturnsNullOnCancellation()
    {
        var interop = new FakeGaimerSpeechInterop
        {
            TtsAvailable = true,
            ShouldCancelOnSynthesize = true
        };
        var sut = new MacTextToSpeechProvider(interop);

        var result = await sut.SynthesizeAsync("hello");

        result.Should().BeNull();
    }

    // ─── Real PCM bytes (not marker) ───

    [Fact]
    public async Task TtsProvider_ReturnsRealPcmNotMarkerByte()
    {
        // Previous placeholder returned new byte[] { 0x01 } as a "marker".
        // Real provider must return actual multi-byte PCM data from native.
        var realPcm = new byte[4800]; // 100ms of 24kHz 16-bit mono = 4800 bytes
        new Random(42).NextBytes(realPcm);

        var interop = new FakeGaimerSpeechInterop
        {
            TtsAvailable = true,
            SynthesisResult = realPcm
        };
        var sut = new MacTextToSpeechProvider(interop);

        var result = await sut.SynthesizeAsync("This is a real speech synthesis test.");

        result.Should().NotBeNull();
        result!.Length.Should().BeGreaterThan(1, "real PCM must be more than a 1-byte marker");
        result.Should().BeEquivalentTo(realPcm);
    }

    [Fact]
    public async Task SttProvider_ReturnsRealTranscriptNotNull()
    {
        // Previous placeholder returned null.
        // Real provider must return actual transcript from native.
        var interop = new FakeGaimerSpeechInterop
        {
            SttAvailable = true,
            TranscriptResult = "What is the best move in this position?"
        };
        var sut = new MacSpeechToTextProvider(interop);

        var result = await sut.TranscribeAsync(new byte[] { 0x01 });

        result.Should().NotBeNullOrEmpty("real STT must return actual transcript");
        result.Should().Be("What is the best move in this position?");
    }

    // ─── Fake ───

    private sealed class FakeGaimerSpeechInterop : IGaimerSpeechInterop
    {
        public bool SttAvailable { get; set; }
        public bool TtsAvailable { get; set; }
        public string? TranscriptResult { get; set; }
        public byte[]? SynthesisResult { get; set; }
        public bool ShouldThrowOnTranscribe { get; set; }
        public bool ShouldThrowOnSynthesize { get; set; }
        public bool ShouldCancelOnTranscribe { get; set; }
        public bool ShouldCancelOnSynthesize { get; set; }

        public int TranscribeCallCount { get; private set; }
        public int SynthesizeCallCount { get; private set; }
        public byte[]? LastTranscribedAudio { get; private set; }
        public string? LastSynthesizedText { get; private set; }

        public bool IsSttAvailable => SttAvailable;
        public bool IsTtsAvailable => TtsAvailable;

        public Task<string?> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default)
        {
            TranscribeCallCount++;
            LastTranscribedAudio = pcmAudio;
            if (ShouldThrowOnTranscribe) throw new InvalidOperationException("Native STT error");
            if (ShouldCancelOnTranscribe) throw new OperationCanceledException();
            return Task.FromResult(TranscriptResult);
        }

        public Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default)
        {
            SynthesizeCallCount++;
            LastSynthesizedText = text;
            if (ShouldThrowOnSynthesize) throw new InvalidOperationException("Native TTS error");
            if (ShouldCancelOnSynthesize) throw new OperationCanceledException();
            return Task.FromResult(SynthesisResult);
        }
    }
}
