using System.Text.Json;
using Moq;
using WitnessDesktop.Models;
using Xunit;

namespace WitnessDesktop.Tests.ViewModels;

public class ShowReplaySurfaceRoutingTests : MainViewModelTestBase
{
    [Fact]
    public void ToolCallReceived_ShowReplay_GhostModeActive_ShowsOnlyNativeVideoCard()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        CreateSut();

        MockRouter.Raise(r => r.ToolCallReceived += null, CreateSuccessfulShowReplayToolCall());

        MockGhost.Verify(g => g.ShowVideoCard(
            "/tmp/segment-0.mp4",
            60.0,
            30.0,
            "WATCH THIS"), Times.Once);
        MockGhost.Verify(g => g.ShowCard(
            FabCardVariant.TextWithImage,
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Never);
        MockGhost.Verify(g => g.DismissCard(), Times.Never);
    }

    [Fact]
    public void ToolCallReceived_ShowReplay_GhostModeInactive_SkipsGenericToolCard()
    {
        CreateSut();

        MockRouter.Raise(r => r.ToolCallReceived += null, CreateSuccessfulShowReplayToolCall());

        MockGhost.Verify(g => g.ShowCard(
            It.IsAny<FabCardVariant>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Never);
        MockGhost.Verify(g => g.ShowVideoCard(
            It.IsAny<string>(),
            It.IsAny<double>(),
            It.IsAny<double>(),
            It.IsAny<string?>()), Times.Never);
        MockGhost.Verify(g => g.DismissCard(), Times.Never);
    }

    private static ToolCallInfo CreateSuccessfulShowReplayToolCall() => new()
    {
        ToolName = "show_replay",
        Success = true,
        OutputJson = JsonSerializer.Serialize(new
        {
            status = "success",
            filePath = "/tmp/segment-0.mp4",
            startTime = 60.0,
            duration = 30.0,
            title = "WATCH THIS"
        })
    };
}
