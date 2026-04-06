using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class VoiceGroundingCoordinatorTests
{
    private readonly VoiceGroundingCoordinator _sut = new();

    // ── Turn Classification ──────────────────────────────────────────────

    [Theory]
    [InlineData("What's on e4?", VoiceTurnClass.BoardSensitive)]
    [InlineData("Am I winning?", VoiceTurnClass.BoardSensitive)]
    [InlineData("Who is winning?", VoiceTurnClass.BoardSensitive)]
    [InlineData("What's happening on the board?", VoiceTurnClass.BoardSensitive)]
    [InlineData("What changed?", VoiceTurnClass.BoardSensitive)]
    [InlineData("Should I take the pawn?", VoiceTurnClass.BoardSensitive)]
    [InlineData("What's the eval?", VoiceTurnClass.BoardSensitive)]
    [InlineData("Is there a fork?", VoiceTurnClass.BoardSensitive)]
    [InlineData("What do you see?", VoiceTurnClass.BoardSensitive)]
    [InlineData("What's my position?", VoiceTurnClass.BoardSensitive)]
    [InlineData("Best move?", VoiceTurnClass.BoardSensitive)]
    public void ClassifyTurn_BoardSensitive_MatchesCorrectly(string text, VoiceTurnClass expected)
    {
        VoiceGroundingCoordinator.ClassifyTurn(text).Should().Be(expected);
    }

    [Theory]
    [InlineData("What's a Sicilian Defense?", VoiceTurnClass.GeneralGameQuestion)]
    [InlineData("How do you play the French?", VoiceTurnClass.GeneralGameQuestion)]
    [InlineData("Explain the London System", VoiceTurnClass.GeneralGameQuestion)]
    [InlineData("Tell me about the Ruy Lopez", VoiceTurnClass.GeneralGameQuestion)]
    [InlineData("What are the principles of chess?", VoiceTurnClass.GeneralGameQuestion)]
    public void ClassifyTurn_GeneralGameQuestion_MatchesCorrectly(string text, VoiceTurnClass expected)
    {
        VoiceGroundingCoordinator.ClassifyTurn(text).Should().Be(expected);
    }

    [Theory]
    [InlineData("Be quieter", VoiceTurnClass.Control)]
    [InlineData("Stop talking", VoiceTurnClass.Control)]
    [InlineData("Speak less", VoiceTurnClass.Control)]
    [InlineData("Mute", VoiceTurnClass.Control)]
    public void ClassifyTurn_Control_MatchesCorrectly(string text, VoiceTurnClass expected)
    {
        VoiceGroundingCoordinator.ClassifyTurn(text).Should().Be(expected);
    }

    [Theory]
    [InlineData("Thanks", VoiceTurnClass.Social)]
    [InlineData("That's crazy", VoiceTurnClass.Social)]
    [InlineData("haha", VoiceTurnClass.Social)]
    [InlineData("Good game", VoiceTurnClass.Social)]
    [InlineData("gg", VoiceTurnClass.Social)]
    [InlineData("Hello", VoiceTurnClass.Social)]
    public void ClassifyTurn_Social_MatchesCorrectly(string text, VoiceTurnClass expected)
    {
        VoiceGroundingCoordinator.ClassifyTurn(text).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, VoiceTurnClass.Unclear)]
    [InlineData("", VoiceTurnClass.Unclear)]
    [InlineData("  ", VoiceTurnClass.Unclear)]
    public void ClassifyTurn_EmptyOrNull_ReturnsUnclear(string? text, VoiceTurnClass expected)
    {
        VoiceGroundingCoordinator.ClassifyTurn(text).Should().Be(expected);
    }

    [Fact]
    public void ClassifyTurn_UnknownQuestionMark_DefaultsToBoardSensitive()
    {
        VoiceGroundingCoordinator.ClassifyTurn("Is that a good idea?").Should().Be(VoiceTurnClass.BoardSensitive);
    }

    [Fact]
    public void ClassifyTurn_UnknownStatement_ReturnsUnclear()
    {
        VoiceGroundingCoordinator.ClassifyTurn("I need to think about this").Should().Be(VoiceTurnClass.Unclear);
    }

    // ── Evaluate: Non-board-sensitive turns ───────────────────────────────

    [Fact]
    public void Evaluate_SocialTurn_AnswersDirectly()
    {
        var decision = _sut.Evaluate("Thanks", isInGame: true);

        decision.TurnClass.Should().Be(VoiceTurnClass.Social);
        decision.ResponseMode.Should().Be(VoiceResponseMode.AnswerDirectly);
    }

    [Fact]
    public void Evaluate_ControlTurn_AnswersDirectly()
    {
        var decision = _sut.Evaluate("Be quieter", isInGame: true);

        decision.TurnClass.Should().Be(VoiceTurnClass.Control);
        decision.ResponseMode.Should().Be(VoiceResponseMode.AnswerDirectly);
    }

    [Fact]
    public void Evaluate_GeneralGameQuestion_AnswersDirectly()
    {
        var decision = _sut.Evaluate("What's a Sicilian Defense?", isInGame: true);

        decision.TurnClass.Should().Be(VoiceTurnClass.GeneralGameQuestion);
        decision.ResponseMode.Should().Be(VoiceResponseMode.AnswerDirectly);
    }

    // ── Evaluate: Board-sensitive with no context ─────────────────────────

    [Fact]
    public void Evaluate_BoardSensitive_NoContext_InGame_AcknowledgesAndRefreshes()
    {
        var decision = _sut.Evaluate("What's happening on the board?", isInGame: true);

        decision.TurnClass.Should().Be(VoiceTurnClass.BoardSensitive);
        decision.ResponseMode.Should().Be(VoiceResponseMode.AcknowledgeAndRefresh);
        decision.HasFreshGroundedContext.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_BoardSensitive_NoContext_OutGame_DeclinesCertainty()
    {
        var decision = _sut.Evaluate("Am I winning?", isInGame: false);

        decision.TurnClass.Should().Be(VoiceTurnClass.BoardSensitive);
        decision.ResponseMode.Should().Be(VoiceResponseMode.DeclineBoardCertainty);
        decision.HasFreshGroundedContext.Should().BeFalse();
    }

    // ── Evaluate: Board-sensitive with fresh context ──────────────────────

    [Fact]
    public void Evaluate_BoardSensitive_FreshContext_RespondsFromGrounded()
    {
        _sut.UpdateGroundedContext(new GroundedVoiceContext
        {
            PositionAssessment = "White is slightly better",
            Threats = "Knight fork on f7",
            CapturedAtUtc = DateTime.UtcNow,
        });

        var decision = _sut.Evaluate("Am I winning?", isInGame: true);

        decision.TurnClass.Should().Be(VoiceTurnClass.BoardSensitive);
        decision.ResponseMode.Should().Be(VoiceResponseMode.RespondFromGroundedContext);
        decision.HasFreshGroundedContext.Should().BeTrue();
        decision.GroundedSummary.Should().Contain("White is slightly better");
    }

    // ── Evaluate: Board-sensitive with stale context ──────────────────────

    [Fact]
    public void Evaluate_BoardSensitive_StaleContext_AcknowledgesUncertainty()
    {
        _sut.UpdateGroundedContext(new GroundedVoiceContext
        {
            PositionAssessment = "Equal position",
            CapturedAtUtc = DateTime.UtcNow.AddMinutes(-2), // Well past 45s max age
        });

        var decision = _sut.Evaluate("What's happening?", isInGame: true);

        decision.TurnClass.Should().Be(VoiceTurnClass.BoardSensitive);
        decision.ResponseMode.Should().Be(VoiceResponseMode.AcknowledgeUncertainty);
        decision.HasFreshGroundedContext.Should().BeFalse();
    }

    // ── Staleness rules ──────────────────────────────────────────────────

    [Fact]
    public void GroundedVoiceContext_IsStale_ReturnsTrueWhenOld()
    {
        var ctx = new GroundedVoiceContext
        {
            CapturedAtUtc = DateTime.UtcNow.AddSeconds(-60),
        };

        ctx.IsStale(DateTime.UtcNow, VoiceGroundingCoordinator.DefaultMaxAge).Should().BeTrue();
    }

    [Fact]
    public void GroundedVoiceContext_IsStale_ReturnsFalseWhenFresh()
    {
        var ctx = new GroundedVoiceContext
        {
            CapturedAtUtc = DateTime.UtcNow.AddSeconds(-10),
        };

        ctx.IsStale(DateTime.UtcNow, VoiceGroundingCoordinator.DefaultMaxAge).Should().BeFalse();
    }

    // ── GroundedVoiceContext.ToGroundingSummary ───────────────────────────

    [Fact]
    public void ToGroundingSummary_WithAllFields_FormatsCorrectly()
    {
        var ctx = new GroundedVoiceContext
        {
            PositionAssessment = "White is better",
            Threats = "Knight fork",
            SuggestedAction = "Defend with Nf3",
            CapturedAtUtc = DateTime.UtcNow,
        };

        var summary = ctx.ToGroundingSummary();
        summary.Should().Contain("White is better");
        summary.Should().Contain("Threats: Knight fork");
        summary.Should().Contain("Suggestion: Defend with Nf3");
    }

    [Fact]
    public void ToGroundingSummary_EmptyFields_ReturnsEmpty()
    {
        var ctx = new GroundedVoiceContext { CapturedAtUtc = DateTime.UtcNow };
        ctx.ToGroundingSummary().Should().BeEmpty();
    }

    // ── GetGroundingPrefix ───────────────────────────────────────────────

    [Fact]
    public void GetGroundingPrefix_NoContext_ReturnsNull()
    {
        _sut.GetGroundingPrefix().Should().BeNull();
    }

    [Fact]
    public void GetGroundingPrefix_FreshContext_ReturnsGroundedPrefix()
    {
        _sut.UpdateGroundedContext(new GroundedVoiceContext
        {
            PositionAssessment = "Equal game",
            CapturedAtUtc = DateTime.UtcNow,
        });

        var prefix = _sut.GetGroundingPrefix();
        prefix.Should().Contain("GROUNDED GAME STATE");
        prefix.Should().Contain("Equal game");
    }

    [Fact]
    public void GetGroundingPrefix_StaleContext_ReturnsUncertaintyPrefix()
    {
        _sut.UpdateGroundedContext(new GroundedVoiceContext
        {
            PositionAssessment = "Old assessment",
            CapturedAtUtc = DateTime.UtcNow.AddMinutes(-2),
        });

        var prefix = _sut.GetGroundingPrefix();
        prefix.Should().Contain("outdated");
        prefix.Should().Contain("uncertainty");
    }

    // ── UpdateGroundedContext ─────────────────────────────────────────────

    [Fact]
    public void UpdateGroundedContext_SetsLatestContext()
    {
        _sut.LatestContext.Should().BeNull();

        var ctx = new GroundedVoiceContext
        {
            PositionAssessment = "Test",
            CapturedAtUtc = DateTime.UtcNow,
        };
        _sut.UpdateGroundedContext(ctx);

        _sut.LatestContext.Should().BeSameAs(ctx);
    }

    [Fact]
    public void UpdateGroundedContext_ReplacesOldContext()
    {
        var old = new GroundedVoiceContext
        {
            PositionAssessment = "Old",
            CapturedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        };
        var fresh = new GroundedVoiceContext
        {
            PositionAssessment = "Fresh",
            CapturedAtUtc = DateTime.UtcNow,
        };

        _sut.UpdateGroundedContext(old);
        _sut.UpdateGroundedContext(fresh);

        _sut.LatestContext.Should().BeSameAs(fresh);
    }

    // ── Custom max age ───────────────────────────────────────────────────

    [Fact]
    public void Evaluate_CustomMaxAge_UsesProvidedAge()
    {
        var shortAge = new VoiceGroundingCoordinator(maxAge: TimeSpan.FromSeconds(5));
        shortAge.UpdateGroundedContext(new GroundedVoiceContext
        {
            PositionAssessment = "Test",
            CapturedAtUtc = DateTime.UtcNow.AddSeconds(-10), // 10s old, 5s max
        });

        var decision = shortAge.Evaluate("What's happening?", isInGame: true);
        decision.ResponseMode.Should().Be(VoiceResponseMode.AcknowledgeUncertainty);
    }
}
