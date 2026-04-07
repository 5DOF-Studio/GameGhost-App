using WitnessDesktop.Models;

namespace WitnessDesktop.Tests.Models;

public class ToolDefinitionDelegateToTeamTests
{
    [Fact]
    public void ToolDefinitions_All_ContainsDelegateToTeam()
    {
        ToolDefinitions.All.Should().Contain(t => t.Name == "delegate_to_team");
    }

    [Fact]
    public void DelegateToTeam_NotRequiresInGame()
    {
        ToolDefinitions.DelegateToTeam.RequiresInGame.Should().BeFalse();
    }

    [Fact]
    public void DelegateToTeam_HasTaskParameter()
    {
        ToolDefinitions.DelegateToTeam.ParametersSchema.Should().Contain("\"task\"");
    }

    [Fact]
    public void FindByName_ReturnsDelegateToTeam()
    {
        var tool = ToolDefinitions.FindByName("delegate_to_team");
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("delegate_to_team");
    }
}
