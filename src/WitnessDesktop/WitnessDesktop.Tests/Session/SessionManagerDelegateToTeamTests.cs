using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Session;

public class SessionManagerDelegateToTeamTests
{
    [Fact]
    public void GetAvailableTools_OutGame_IncludesDelegateToTeam()
    {
        var sm = new SessionManager();
        var tools = sm.GetAvailableTools();
        tools.Should().Contain(t => t.Name == "delegate_to_team");
    }

    [Fact]
    public void GetAvailableTools_InGame_IncludesDelegateToTeam()
    {
        var sm = new SessionManager();
        sm.TransitionToInGame("game-1", "chess", "lichess");
        sm.Context.AgentKey = "chess";
        var tools = sm.GetAvailableTools();
        tools.Should().Contain(t => t.Name == "delegate_to_team");
    }
}
