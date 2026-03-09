using GaimerDesktop.Models;
using GaimerDesktop.Services;

namespace GaimerDesktop.Tests.Integration;

public class EndToEndTests
{
    [Fact]
    public async Task BrainContextService_FullEnvelope_EndToEnd()
    {
        var mockReel = new Mock<IVisualReelService>();
        mockReel.Setup(r => r.GetRecent(It.IsAny<int>()))
            .Returns(new List<ReelMoment>());
        var sut = new BrainContextService(mockReel.Object);

        // Ingest events across different categories
        await sut.IngestEventAsync(new BrainEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Type = BrainEventType.VisionObservation,
            Category = "vision",
            Text = "Knight on f3 attacking e5 pawn",
            Confidence = 0.9
        });
        await sut.IngestEventAsync(new BrainEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Type = BrainEventType.GameplayState,
            Category = "threat",
            Text = "Opponent preparing kingside attack",
            Confidence = 0.7
        });

        // Build envelope
        var envelope = await sut.GetContextForVoiceAsync(DateTime.UtcNow);

        envelope.Should().NotBeNull();
        envelope.BudgetTokens.Should().BeGreaterThan(0);

        // Format as text block
        var textBlock = sut.FormatAsPrefixedContextBlock(envelope);
        textBlock.Should().NotBeNullOrWhiteSpace();
        textBlock.Should().Contain("vision");
    }

    [Fact]
    public void SessionManager_TransitionCycle_ToolsReflectState()
    {
        var sut = new SessionManager();

        // Start OutGame
        sut.CurrentState.Should().Be(SessionState.OutGame);
        var outGameTools = sut.GetAvailableTools();
        outGameTools.Should().HaveCount(3);

        // Transition to InGame
        sut.TransitionToInGame("game-1", "chess", "lichess");
        sut.CurrentState.Should().Be(SessionState.InGame);
        var inGameTools = sut.GetAvailableTools();
        inGameTools.Should().HaveCount(7);
        inGameTools.Select(t => t.Name).Should().Contain("capture_screen");

        // Back to OutGame
        sut.TransitionToOutGame();
        sut.CurrentState.Should().Be(SessionState.OutGame);
        sut.Context.GameId.Should().BeNull();
        var backToOutGame = sut.GetAvailableTools();
        backToOutGame.Should().HaveCount(3);
        backToOutGame.Select(t => t.Name).Should().NotContain("capture_screen");
    }
}
