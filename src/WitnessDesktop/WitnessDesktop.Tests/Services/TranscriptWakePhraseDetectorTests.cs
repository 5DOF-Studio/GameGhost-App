using FluentAssertions;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Tests.Services;

public class TranscriptWakePhraseDetectorTests
{
    private readonly TranscriptWakePhraseDetector _sut = new();

    // ── Exact Matches (Tier 1) ──────────────────────────────────────

    [Theory]
    [InlineData("Hey Leroy", "Leroy")]
    [InlineData("hey leroy what's up", "Leroy")]
    [InlineData("Hey Wasp", "Wasp")]
    [InlineData("hey wasp can you help", "Wasp")]
    [InlineData("HEY LEROY", "Leroy")]
    [InlineData("Hey RASA what happened", "RASA")]
    public void TryDetectWake_ValidWakePhrase_ReturnsTrue(string transcript, string agentName)
    {
        _sut.TryDetectWake(transcript, agentName, out var matched).Should().BeTrue();
        matched.Should().NotBeNullOrEmpty();
    }

    // ── No Match ────────────────────────────────────────────────────

    [Theory]
    [InlineData("What's happening Leroy", "Leroy")]
    [InlineData("Hey there", "Leroy")]
    [InlineData("Leroy do something", "Leroy")]
    [InlineData("", "Leroy")]
    public void TryDetectWake_NoWakePhrase_ReturnsFalse(string transcript, string agentName)
    {
        _sut.TryDetectWake(transcript, agentName, out var matched).Should().BeFalse();
        matched.Should().BeNull();
    }

    [Fact]
    public void TryDetectWake_NullTranscript_ReturnsFalse()
    {
        _sut.TryDetectWake(null!, "Leroy", out _).Should().BeFalse();
    }

    [Fact]
    public void TryDetectWake_NullAgentName_ReturnsFalse()
    {
        _sut.TryDetectWake("Hey Leroy", null!, out _).Should().BeFalse();
    }

    // ── Mid-Sentence Detection ──────────────────────────────────────

    [Theory]
    [InlineData("okay so hey Leroy what do you think", "Leroy")]
    [InlineData("um hey wasp is that a good move", "Wasp")]
    public void TryDetectWake_MidSentence_StillDetects(string transcript, string agentName)
    {
        _sut.TryDetectWake(transcript, agentName, out _).Should().BeTrue();
    }

    // ── Fuzzy Matches — STT Variants (Tier 2 + 3) ──────────────────

    [Theory]
    [InlineData("hey Larry what's up", "Leroy")]       // Common STT mishear (dist 2)
    [InlineData("hey Laroy analyze", "Leroy")]          // Vowel swap (dist 1)
    [InlineData("hey wasp help", "Wasp")]               // Exact (still works via tier 1)
    [InlineData("hey waasp what", "Wasp")]              // Elongated vowel (dist 1)
    [InlineData("HeyLeroy", "Leroy")]                   // STT merged into single token (tier 3)
    public void TryDetectWake_FuzzyMatch_DetectsSTTVariants(string transcript, string agentName)
    {
        _sut.TryDetectWake(transcript, agentName, out var matched).Should().BeTrue();
        matched.Should().NotBeNullOrEmpty();
    }

    // ── Fuzzy Rejection — Too Distant ───────────────────────────────

    [Theory]
    [InlineData("hey Bobby", "Leroy")]                  // Completely different name (dist 4)
    [InlineData("play some music", "Leroy")]             // No wake phrase at all
    [InlineData("hey there friend", "Leroy")]            // "there" too far from "Leroy" (dist 4)
    [InlineData("hey le roy help me", "Leroy")]          // Space insertion — "hey le" vs "hey leroy" (dist 4), "le roy" vs "hey leroy" (dist 3) — both exceed threshold 2
    [InlineData("a Leroy check this", "Leroy")]          // "a Leroy" vs "hey leroy" (dist 3) — exceeds threshold 2
    [InlineData("hey le roy", "Leroy")]                  // Same as above — both word-pair candidates exceed dist 2
    [InlineData("hey what", "Wasp")]                     // C1: "hey what" → "hey wasp" (dist 3) — false positive at old threshold
    [InlineData("hey there", "Derek")]                   // C1: "hey there" → "hey derek" (dist 3) — false positive at old threshold
    public void TryDetectWake_FuzzyReject_TooDistant(string transcript, string agentName)
    {
        _sut.TryDetectWake(transcript, agentName, out _).Should().BeFalse();
    }

    // ── Levenshtein Distance Unit Tests ─────────────────────────────

    [Theory]
    [InlineData("hey leroy", "hey leroy", 0)]
    [InlineData("hey larry", "hey leroy", 2)]
    [InlineData("hey le roy", "hey leroy", 1)]
    [InlineData("hey laroy", "hey leroy", 1)]
    [InlineData("", "hey leroy", 9)]
    [InlineData("hey leroy", "", 9)]
    [InlineData("a leroy", "hey leroy", 3)]
    [InlineData("hey bobby", "hey leroy", 4)]
    [InlineData("hey there", "hey leroy", 4)]
    [InlineData("hey wait", "hey wasp", 2)]              // C1: close enough at threshold 2 (known limitation)
    [InlineData("hey what", "hey wasp", 3)]              // C1: rejected at threshold 2
    [InlineData("hey there", "hey derek", 3)]            // C1: rejected at threshold 2
    public void LevenshteinDistance_ComputesCorrectly(string a, string b, int expected)
    {
        TranscriptWakePhraseDetector.LevenshteinDistance(a, b).Should().Be(expected);
    }
}
