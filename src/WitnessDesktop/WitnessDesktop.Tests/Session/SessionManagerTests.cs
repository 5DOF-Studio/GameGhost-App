using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Session;

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
            "web_search", "search_replay", "delegate_to_team");
    }

    [Fact]
    public void GetAvailableTools_InGame_Returns7Tools()
    {
        var sut = CreateSut();
        sut.TransitionToInGame("game-1", "chess", "lichess");

        var tools = sut.GetAvailableTools();

        tools.Should().HaveCount(8);
        tools.Select(t => t.Name).Should().Contain("capture_screen")
            .And.Contain("get_game_state")
            .And.Contain("analyze_position_engine")
            .And.Contain("analyze_position_strategic")
            .And.Contain("game_journal")
            .And.Contain("delegate_to_team");
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

    [Fact]
    public void GetAvailableTools_InGame_RasaAgent_ExcludesChessTools()
    {
        var sut = CreateSut();
        sut.Context.AgentKey = "general";
        sut.TransitionToInGame("game-1", "rpg", "gaimer");

        var tools = sut.GetAvailableTools();
        var toolNames = tools.Select(t => t.Name).ToList();

        toolNames.Should().Contain("capture_screen");
        toolNames.Should().Contain("web_search");
        toolNames.Should().Contain("game_journal");
        toolNames.Should().NotContain("analyze_position_engine");
        toolNames.Should().NotContain("analyze_position_strategic");
        toolNames.Should().NotContain("get_game_state");
    }

    [Fact]
    public void GetAvailableTools_InGame_ChessAgent_IncludesChessTools()
    {
        var sut = CreateSut();
        sut.Context.AgentKey = "chess";
        sut.TransitionToInGame("game-1", "chess", "lichess");

        var tools = sut.GetAvailableTools();
        var toolNames = tools.Select(t => t.Name).ToList();

        toolNames.Should().Contain("analyze_position_engine");
        toolNames.Should().Contain("analyze_position_strategic");
        toolNames.Should().Contain("capture_screen");
        toolNames.Should().Contain("get_game_state");
        toolNames.Should().NotContain("game_journal");
    }

    [Fact]
    public void GetAvailableTools_InGame_NoAgentKey_ReturnsAllInGameTools()
    {
        var sut = CreateSut();
        sut.TransitionToInGame("game-1", "chess", "lichess");
        // AgentKey intentionally not set — backward compat path

        var tools = sut.GetAvailableTools();

        tools.Should().HaveCount(8);
        tools.Select(t => t.Name).Should().Contain("analyze_position_engine")
            .And.Contain("analyze_position_strategic")
            .And.Contain("capture_screen")
            .And.Contain("get_game_state")
            .And.Contain("game_journal")
            .And.Contain("delegate_to_team");
    }
}
