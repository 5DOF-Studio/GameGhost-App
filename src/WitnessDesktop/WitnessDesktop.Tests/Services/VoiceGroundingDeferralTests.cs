using FluentAssertions;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class VoiceGroundingDeferralTests
{
    private readonly VoiceGroundingCoordinator _sut = new();

    // ── HistorySensitive Classification ──────────────────────────────

    [Theory]
    [InlineData("What happened at B site?")]
    [InlineData("How did I die there?")]
    [InlineData("Show me that play")]
    [InlineData("What happened earlier?")]
    [InlineData("Last round was crazy")]
    [InlineData("Replay that")]
    public void ClassifyTurn_HistorySensitive(string text)
    {
        VoiceGroundingCoordinator.ClassifyTurn(text).Should().Be(VoiceTurnClass.HistorySensitive);
    }

    // ── ToolDependent Classification ─────────────────────────────────

    [Theory]
    [InlineData("Run the engine on this")]
    [InlineData("What does stockfish say?")]
    [InlineData("Analyze this position")]
    [InlineData("Check my journal")]
    [InlineData("Search for the opening")]
    public void ClassifyTurn_ToolDependent(string text)
    {
        VoiceGroundingCoordinator.ClassifyTurn(text).Should().Be(VoiceTurnClass.ToolDependent);
    }

    // ── DeferToBrain Response Mode ───────────────────────────────────

    [Fact]
    public void Evaluate_HistorySensitive_ReturnsDeferToBrain()
    {
        var decision = _sut.Evaluate("What happened earlier?", isInGame: true);
        decision.ResponseMode.Should().Be(VoiceResponseMode.DeferToBrain);
        decision.TurnClass.Should().Be(VoiceTurnClass.HistorySensitive);
    }

    [Fact]
    public void Evaluate_ToolDependent_ReturnsDeferToBrain()
    {
        var decision = _sut.Evaluate("Run the engine on this", isInGame: true);
        decision.ResponseMode.Should().Be(VoiceResponseMode.DeferToBrain);
        decision.TurnClass.Should().Be(VoiceTurnClass.ToolDependent);
    }

    // ── Priority: HistorySensitive beats BoardSensitive ──────────────

    [Fact]
    public void ClassifyTurn_HistorySensitive_BeforeBoardSensitive()
    {
        // "What happened" could be board-sensitive, but history takes priority
        VoiceGroundingCoordinator.ClassifyTurn("What happened on the board?")
            .Should().Be(VoiceTurnClass.HistorySensitive);
    }

    // ── FormatFreshnessPrefix ────────────────────────────────────────

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(3, "just now")]
    [InlineData(4.9, "just now")]
    [InlineData(8, "from 8 seconds ago")]
    [InlineData(14, "from 14 seconds ago")]
    [InlineData(20, "from about 20 seconds ago — checking again now")]
    [InlineData(29, "from about 29 seconds ago — checking again now")]
    [InlineData(35, "that was a while back — let me get a fresh look")]
    [InlineData(120, "that was a while back — let me get a fresh look")]
    public void FormatFreshnessPrefix_CorrectBrackets(double seconds, string expected)
    {
        VoiceGroundingCoordinator.FormatFreshnessPrefix(TimeSpan.FromSeconds(seconds))
            .Should().Be(expected);
    }
}
