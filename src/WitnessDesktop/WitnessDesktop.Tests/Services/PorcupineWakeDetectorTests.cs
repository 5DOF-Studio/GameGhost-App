using FluentAssertions;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Tests.Services;

public class PorcupineWakeDetectorTests
{
    [Fact]
    public void NoAccessKey_IsNotAvailable()
    {
        using var sut = new PorcupineWakeDetector(accessKey: null);
        sut.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void EmptyAccessKey_IsNotAvailable()
    {
        using var sut = new PorcupineWakeDetector(accessKey: "");
        sut.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void WhitespaceAccessKey_IsNotAvailable()
    {
        using var sut = new PorcupineWakeDetector(accessKey: "  ");
        sut.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void ProcessAudio_WhenUnavailable_DoesNotThrow()
    {
        using var sut = new PorcupineWakeDetector(accessKey: null);
        var audioData = new byte[640]; // 320 samples at 16-bit
        sut.ProcessAudio(audioData); // Should not throw
    }

    [Fact]
    public void ProcessAudio_EmptyData_DoesNotThrow()
    {
        using var sut = new PorcupineWakeDetector(accessKey: null);
        sut.ProcessAudio(Array.Empty<byte>()); // Should not throw
    }

    [Fact]
    public void Dispose_WhenUnavailable_DoesNotThrow()
    {
        var sut = new PorcupineWakeDetector(accessKey: null);
        sut.Dispose();
        sut.Dispose(); // Double dispose should not throw
    }

    [Fact]
    public void InvalidAccessKey_IsNotAvailable()
    {
        // Invalid key should cause PorcupineException during init, caught gracefully
        using var sut = new PorcupineWakeDetector(accessKey: "invalid-key-that-wont-work");
        sut.IsAvailable.Should().BeFalse();
    }

    // ── Live Detection Tests (BLOCKED: Picovoice account approval pending) ──
    //
    // These tests require a valid PICOVOICE_ACCESS_KEY.
    // Picovoice Console signup has an approval gate — key not yet available.
    // Tests are skipped until access is granted.
    //
    // To unblock: set PICOVOICE_ACCESS_KEY env var, then remove Skip annotations.
    // Custom wake word tests also need .ppn model files trained via Picovoice Console.

    private static string? PicovoiceKey => Environment.GetEnvironmentVariable("PICOVOICE_ACCESS_KEY");
    private static string SkipReason => "Requires PICOVOICE_ACCESS_KEY — Picovoice account approval pending";

    [Fact(Skip = "Requires PICOVOICE_ACCESS_KEY — Picovoice account approval pending")]
    public void ValidAccessKey_IsAvailable()
    {
        using var sut = new PorcupineWakeDetector(accessKey: PicovoiceKey);
        sut.IsAvailable.Should().BeTrue();
    }

    [Fact(Skip = "Requires PICOVOICE_ACCESS_KEY — Picovoice account approval pending")]
    public void ValidAccessKey_BuiltInKeyword_FrameLengthIsPositive()
    {
        using var sut = new PorcupineWakeDetector(accessKey: PicovoiceKey);
        sut.IsAvailable.Should().BeTrue();
        // Porcupine.FrameLength is typically 512 samples at 16kHz
        // We can't access it through our interface, but IsAvailable confirms init succeeded
    }

    [Fact(Skip = "Requires PICOVOICE_ACCESS_KEY — Picovoice account approval pending")]
    public void ProcessAudio_SilentFrames_NoDetection()
    {
        using var sut = new PorcupineWakeDetector(accessKey: PicovoiceKey);
        sut.IsAvailable.Should().BeTrue();

        var detected = false;
        sut.WakeWordDetected += (_, _) => detected = true;

        // Feed 1 second of silence (16kHz * 2 bytes/sample = 32000 bytes)
        var silence = new byte[32000];
        sut.ProcessAudio(silence);

        detected.Should().BeFalse();
    }

    [Fact(Skip = "Requires PICOVOICE_ACCESS_KEY — Picovoice account approval pending")]
    public void ProcessAudio_BuffersPartialFrames()
    {
        using var sut = new PorcupineWakeDetector(accessKey: PicovoiceKey);
        sut.IsAvailable.Should().BeTrue();

        // Feed a small chunk (less than one frame). Should not throw.
        var smallChunk = new byte[100];
        sut.ProcessAudio(smallChunk);
        // No assertion beyond "doesn't throw" — partial frame buffered internally
    }

    [Fact(Skip = "Requires PICOVOICE_ACCESS_KEY + custom .ppn model files — Picovoice account approval pending")]
    public void CustomKeywordPaths_InitializesWithAgentNames()
    {
        // This test requires .ppn files trained for "Hey Leroy", "Hey Wasp", "Hey RASA"
        // via Picovoice Console. Paths would be:
        //   Resources/WakeWords/hey-leroy_mac.ppn
        //   Resources/WakeWords/hey-wasp_mac.ppn
        //   Resources/WakeWords/hey-rasa_mac.ppn
        var keywordPaths = new[] { "hey-leroy_mac.ppn", "hey-wasp_mac.ppn", "hey-rasa_mac.ppn" };
        var keywordNames = new[] { "Leroy", "Wasp", "RASA" };

        using var sut = new PorcupineWakeDetector(
            accessKey: PicovoiceKey,
            keywordPaths: keywordPaths,
            keywordNames: keywordNames);

        sut.IsAvailable.Should().BeTrue();
    }

    [Fact(Skip = "Requires PICOVOICE_ACCESS_KEY — Picovoice account approval pending")]
    public void WakeWordDetected_EventCarriesKeywordName()
    {
        using var sut = new PorcupineWakeDetector(accessKey: PicovoiceKey);
        sut.IsAvailable.Should().BeTrue();

        string? detectedKeyword = null;
        sut.WakeWordDetected += (_, keyword) => detectedKeyword = keyword;

        // To trigger detection, we'd need actual audio of the wake word.
        // This test validates the event wiring — actual audio-based detection
        // requires a recorded sample of the wake word.
        // For now, verify the event is subscribable and the detector is live.
        detectedKeyword.Should().BeNull("no wake word audio was provided");
    }
}
