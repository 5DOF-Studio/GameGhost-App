using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Integration;

public class GaimerTeamIntegrationTests
{
    [Fact]
    public void ToolDefinitions_All_IncludesDelegateToTeam()
    {
        ToolDefinitions.All.Should().Contain(t => t.Name == "delegate_to_team");
    }

    [Fact]
    public void SessionManager_OutGame_ExposesDelegateToTeam()
    {
        var sm = new SessionManager();
        var tools = sm.GetAvailableTools();
        tools.Should().Contain(t => t.Name == "delegate_to_team");
    }

    [Fact]
    public void SessionManager_InGame_ExposesDelegateToTeam()
    {
        var sm = new SessionManager();
        sm.TransitionToInGame("g1", "chess", "lichess");
        sm.Context.AgentKey = "chess";
        var tools = sm.GetAvailableTools();
        tools.Should().Contain(t => t.Name == "delegate_to_team");
    }

    [Fact]
    public void EventIconMap_HasTeamResultEntries()
    {
        var icon = EventIconMap.GetIcon(EventOutputType.TeamResult);
        var color = EventIconMap.GetCapsuleColorHex(EventOutputType.TeamResult);
        var stroke = EventIconMap.GetCapsuleStrokeHex(EventOutputType.TeamResult);

        icon.Should().NotBe("info.png", "TeamResult must have an explicit icon mapping");
        color.Should().NotBe("#30808080", "TeamResult must have an explicit capsule color");
        stroke.Should().NotBe("#50808080", "TeamResult must have an explicit capsule stroke");
    }

    [Theory]
    [InlineData("general")]
    [InlineData("chess")]
    [InlineData("wasp")]
    public void AllAgents_IncludeDelegateToTeam(string agentKey)
    {
        var agent = Agents.GetByKey(agentKey);
        agent.Should().NotBeNull();
        agent!.Tools.Should().Contain("delegate_to_team");
    }

    [Fact]
    public void TimelineEvent_TeamResult_CanBeCreated()
    {
        var evt = new TimelineEvent
        {
            Type = EventOutputType.TeamResult,
            Summary = "Test team result",
            Icon = EventIconMap.GetIcon(EventOutputType.TeamResult),
            CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.TeamResult),
            CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.TeamResult),
        };

        evt.Type.Should().Be(EventOutputType.TeamResult);
        evt.CapsuleColorHex.Should().NotBeNullOrEmpty();
        evt.CapsuleStrokeHex.Should().NotBeNullOrEmpty();
    }
}
