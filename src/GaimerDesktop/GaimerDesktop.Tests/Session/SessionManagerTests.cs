using GaimerDesktop.Models;
using GaimerDesktop.Services;

namespace GaimerDesktop.Tests.Session;

public class SessionManagerTests
{
    private SessionManager CreateSut() => new();

    [Fact]
    public void TransitionToInGame_SetsStateAndContext()
    {
        var sut = CreateSut();

        sut.TransitionToInGame("game-1", "chess", "lichess");

        sut.CurrentState.Should().Be(SessionState.InGame);
        sut.Context.GameId.Should().Be("game-1");
        sut.Context.GameType.Should().Be("chess");
        sut.Context.ConnectorName.Should().Be("lichess");
    }

    [Fact]
    public void TransitionToOutGame_ClearsState()
    {
        var sut = CreateSut();
        sut.TransitionToInGame("game-1", "chess", "lichess");

        sut.TransitionToOutGame();

        sut.CurrentState.Should().Be(SessionState.OutGame);
        sut.Context.GameId.Should().BeNull();
        sut.Context.GameType.Should().BeNull();
        sut.Context.ConnectorName.Should().BeNull();
    }

    [Fact]
    public void TransitionToInGame_SetsGameStartedAt()
    {
        var sut = CreateSut();
        var before = DateTime.UtcNow;

        sut.TransitionToInGame("game-1", "chess", "lichess");

        sut.Context.GameStartedAt.Should().NotBeNull();
        sut.Context.GameStartedAt!.Value.Should().BeCloseTo(before, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void TransitionToOutGame_ClearsGameStartedAt()
    {
        var sut = CreateSut();
        sut.TransitionToInGame("game-1", "chess", "lichess");

        sut.TransitionToOutGame();

        sut.Context.GameStartedAt.Should().BeNull();
    }

    [Fact]
    public void StateChanged_FiresOnInGameTransition()
    {
        var sut = CreateSut();
        SessionState? received = null;
        sut.StateChanged += (_, state) => received = state;

        sut.TransitionToInGame("game-1", "chess", "lichess");

        received.Should().Be(SessionState.InGame);
    }

    [Fact]
    public void StateChanged_FiresOnOutGameTransition()
    {
        var sut = CreateSut();
        sut.TransitionToInGame("game-1", "chess", "lichess");
        SessionState? received = null;
        sut.StateChanged += (_, state) => received = state;

        sut.TransitionToOutGame();

        received.Should().Be(SessionState.OutGame);
    }

    [Fact]
    public void GetAvailableTools_OutGame_Returns3Tools()
    {
        var sut = CreateSut();

        var tools = sut.GetAvailableTools();

        tools.Should().HaveCount(3);
        tools.Select(t => t.Name).Should().BeEquivalentTo(
            "web_search", "player_history", "player_analytics");
    }

    [Fact]
    public void GetAvailableTools_InGame_Returns7Tools()
    {
        var sut = CreateSut();
        sut.TransitionToInGame("game-1", "chess", "lichess");

        var tools = sut.GetAvailableTools();

        tools.Should().HaveCount(7);
        tools.Select(t => t.Name).Should().Contain("capture_screen")
            .And.Contain("get_game_state")
            .And.Contain("analyze_position_engine")
            .And.Contain("analyze_position_strategic");
    }

    [Fact]
    public void GetAvailableTools_InGame_DoesNotContainLegacyGetBestMove()
    {
        var sut = CreateSut();
        sut.TransitionToInGame("game-1", "chess", "lichess");

        var tools = sut.GetAvailableTools();

        tools.Select(t => t.Name).Should().NotContain("get_best_move");
    }

    [Fact]
    public void GetAvailableTools_OutGame_ExcludesChessTools()
    {
        var sut = CreateSut();

        var tools = sut.GetAvailableTools();

        tools.Select(t => t.Name).Should().NotContain("analyze_position_engine");
        tools.Select(t => t.Name).Should().NotContain("analyze_position_strategic");
    }
}
