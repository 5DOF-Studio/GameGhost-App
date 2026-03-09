using GaimerDesktop.Models;

namespace GaimerDesktop.Tests.Chess;

public class AgentChessTests
{
    // ── System Instruction Content ──────────────────────────────────────────

    [Fact]
    public void Leroy_SystemInstruction_ContainsEngineToolGuidance()
    {
        Agents.Chess.SystemInstruction.Should().Contain("analyze_position_engine");
    }

    [Fact]
    public void Leroy_SystemInstruction_ContainsStrategicToolGuidance()
    {
        Agents.Chess.SystemInstruction.Should().Contain("analyze_position_strategic");
    }

    [Fact]
    public void Leroy_SystemInstruction_ContainsFenExtractionGuidance()
    {
        Agents.Chess.SystemInstruction.Should().Contain("FEN EXTRACTION");
    }

    [Fact]
    public void Leroy_SystemInstruction_ContainsVoiceOutputRules()
    {
        Agents.Chess.SystemInstruction.Should().Contain("VOICE OUTPUT");
    }

    [Fact]
    public void Wasp_SystemInstruction_ContainsEngineToolGuidance()
    {
        Agents.Wasp.SystemInstruction.Should().Contain("analyze_position_engine");
    }

    [Fact]
    public void Wasp_SystemInstruction_ContainsFenExtractionGuidance()
    {
        Agents.Wasp.SystemInstruction.Should().Contain("FEN EXTRACTION");
    }

    // ── Tools List ──────────────────────────────────────────────────────────

    [Fact]
    public void Leroy_Tools_IncludesNewChessTools()
    {
        Agents.Chess.Tools.Should().Contain("analyze_position_engine");
        Agents.Chess.Tools.Should().Contain("analyze_position_strategic");
    }

    [Fact]
    public void Wasp_Tools_IncludesNewChessTools()
    {
        Agents.Wasp.Tools.Should().Contain("analyze_position_engine");
        Agents.Wasp.Tools.Should().Contain("analyze_position_strategic");
    }

    [Fact]
    public void Leroy_Tools_StillIncludesCaptureAndGameState()
    {
        Agents.Chess.Tools.Should().Contain("capture_screen");
        Agents.Chess.Tools.Should().Contain("get_game_state");
    }

    [Fact]
    public void BothChessAgents_HaveIdenticalToolGuidance()
    {
        // Both agents should have the same CHESS TOOLS section
        Agents.Chess.SystemInstruction.Should().Contain("CHESS TOOLS:");
        Agents.Wasp.SystemInstruction.Should().Contain("CHESS TOOLS:");
    }

    [Fact]
    public void Derek_Tools_DoesNotContainChessTools()
    {
        // RPG agent should NOT have chess tools
        var tools = Agents.General.Tools;
        if (tools is not null)
        {
            tools.Should().NotContain("analyze_position_engine");
            tools.Should().NotContain("analyze_position_strategic");
        }
    }
}
