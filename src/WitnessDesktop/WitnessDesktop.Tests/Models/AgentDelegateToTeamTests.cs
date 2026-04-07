using WitnessDesktop.Models;

namespace WitnessDesktop.Tests.Models;

public class AgentDelegateToTeamTests
{
    [Theory]
    [InlineData("general")]
    [InlineData("chess")]
    [InlineData("wasp")]
    public void AllAgents_HaveDelegateToTeam(string agentKey)
    {
        var agent = Agents.GetByKey(agentKey);
        agent.Should().NotBeNull();
        agent!.Tools.Should().Contain("delegate_to_team");
    }
}
