using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Context;

public class BrainContextTranscriptTests
{
    private readonly Mock<IVisualReelService> _mockReel;
    private readonly BrainContextService _sut;

    public BrainContextTranscriptTests()
    {
        _mockReel = new Mock<IVisualReelService>();
        _mockReel.Setup(r => r.GetRecent(It.IsAny<int>()))
            .Returns(new List<ReelMoment>());
        _sut = new BrainContextService(_mockReel.Object);
    }

    [Fact]
    public async Task BuildEnvelope_IncludesRecentVoiceTranscript_WhenProvided()
    {
        var now = DateTime.UtcNow;
        var inputs = new ContextAssemblyInputs
        {
            RecentTranscript = new List<VoiceTranscriptTurn>
            {
                new() { TimestampUtc = now.AddSeconds(-5), Role = TranscriptRole.User, Text = "Should I take that bishop?" },
                new() { TimestampUtc = now.AddSeconds(-3), Role = TranscriptRole.Assistant, Text = "The bishop exchange looks favorable." }
            }
        };

        var envelope = await _sut.GetContextForChatAsync(now, inputs: inputs);

        envelope.RecentVoiceTranscript.Should().NotBeNullOrWhiteSpace();
        envelope.RecentVoiceTranscript.Should().Contain("Should I take that bishop?");
        envelope.RecentVoiceTranscript.Should().Contain("bishop exchange looks favorable");
    }

    [Fact]
    public async Task BuildEnvelope_EmptyTranscript_WhenNoneProvided()
    {
        var now = DateTime.UtcNow;
        var envelope = await _sut.GetContextForChatAsync(now);

        envelope.RecentVoiceTranscript.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildEnvelope_TranscriptRespectsBudget()
    {
        var now = DateTime.UtcNow;
        var turns = new List<VoiceTranscriptTurn>();
        for (int i = 0; i < 30; i++)
        {
            turns.Add(new VoiceTranscriptTurn
            {
                TimestampUtc = now.AddSeconds(-30 + i),
                Role = TranscriptRole.User,
                Text = $"This is a fairly long voice message number {i} that should consume token budget space in the envelope."
            });
        }

        // Use a tight budget (30 tokens) to force transcript truncation
        var inputs = new ContextAssemblyInputs { RecentTranscript = turns };
        var envelope = await _sut.GetContextForChatAsync(now, budgetTokens: 30, inputs: inputs);

        envelope.TruncationReport.Should().Contain("transcript");
    }

    [Fact]
    public async Task BuildEnvelope_TranscriptFormattedWithRoles()
    {
        var now = DateTime.UtcNow;
        var inputs = new ContextAssemblyInputs
        {
            RecentTranscript = new List<VoiceTranscriptTurn>
            {
                new() { TimestampUtc = now.AddSeconds(-3), Role = TranscriptRole.User, Text = "Am I winning?" },
                new() { TimestampUtc = now.AddSeconds(-1), Role = TranscriptRole.Assistant, Text = "You have a slight edge." }
            }
        };

        var envelope = await _sut.GetContextForChatAsync(now, inputs: inputs);

        envelope.RecentVoiceTranscript.Should().Contain("User (voice):");
        envelope.RecentVoiceTranscript.Should().Contain("AI (voice):");
    }
}
