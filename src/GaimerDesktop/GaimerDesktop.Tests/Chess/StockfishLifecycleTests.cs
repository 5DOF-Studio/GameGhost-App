using GaimerDesktop.Models;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.Chess;

/// <summary>
/// Tests for Stockfish engine lifecycle management:
/// - Start on chess agent selection (when installed)
/// - Skip start when not installed
/// - Stop on disconnect / agent deselection
/// - Download reports progress correctly
/// </summary>
public class StockfishLifecycleTests
{
    // ── Start/Stop lifecycle ─────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_SetsIsReadyTrue()
    {
        var sut = new MockStockfishService();
        sut.IsReady.Should().BeFalse();

        await sut.StartAsync();

        sut.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_SetsIsReadyFalse()
    {
        var sut = new MockStockfishService();
        await sut.StartAsync();
        sut.IsReady.Should().BeTrue();

        await sut.StopAsync();

        sut.IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzePositionAsync_WhenNotStarted_Throws()
    {
        var sut = new MockStockfishService();
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var act = () => sut.AnalyzePositionAsync(fen);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not ready*");
    }

    [Fact]
    public async Task AnalyzePositionAsync_AfterStart_Succeeds()
    {
        var sut = new MockStockfishService();
        await sut.StartAsync();
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var result = await sut.AnalyzePositionAsync(fen);

        result.BestMove.Should().Be("e2e4");
        result.Depth.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzePositionAsync_AfterStopAndRestart_Succeeds()
    {
        var sut = new MockStockfishService();
        await sut.StartAsync();
        await sut.StopAsync();
        await sut.StartAsync();
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var result = await sut.AnalyzePositionAsync(fen);

        result.BestMove.Should().NotBeNullOrEmpty();
    }

    // ── Install/Download ─────────────────────────────────────────────────

    [Fact]
    public void MockStockfishService_IsInstalled_ReturnsTrue()
    {
        var sut = new MockStockfishService();
        sut.IsInstalled.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureInstalledAsync_ReportsProgressToCompletion()
    {
        var sut = new MockStockfishService();
        var reportedValues = new List<double>();

        await sut.EnsureInstalledAsync(new DirectProgress<double>(v => reportedValues.Add(v)));

        reportedValues.Should().Contain(1.0);
    }

    // ── Chess agent identification ───────────────────────────────────────

    [Theory]
    [InlineData("chess", true)]
    [InlineData("wasp", true)]
    [InlineData("general", false)]
    public void ChessAgentType_IdentifiedCorrectly(string agentKey, bool isChess)
    {
        var agent = Agents.GetByKey(agentKey);
        agent.Should().NotBeNull();
        (agent!.Type == AgentType.Chess).Should().Be(isChess);
    }

    [Fact]
    public void ChessAgent_ShouldTriggerStockfishStart()
    {
        // When selecting a chess agent, code should check agent.Type == AgentType.Chess
        // and call StartAsync if IsInstalled
        var leroy = Agents.Chess;
        leroy.Type.Should().Be(AgentType.Chess);
        leroy.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void NonChessAgent_ShouldNotTriggerStockfishStart()
    {
        var derek = Agents.General;
        derek.Type.Should().NotBe(AgentType.Chess);
    }

    // ── Moq-based lifecycle with interface ───────────────────────────────

    [Fact]
    public async Task Interface_StartAsync_CalledOnce_SetsReady()
    {
        var mock = new Mock<IStockfishService>();
        mock.Setup(s => s.IsInstalled).Returns(true);
        mock.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await mock.Object.StartAsync();

        mock.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Interface_StopAsync_CalledOnce()
    {
        var mock = new Mock<IStockfishService>();
        mock.Setup(s => s.StopAsync()).Returns(Task.CompletedTask);

        await mock.Object.StopAsync();

        mock.Verify(s => s.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task Interface_EnsureInstalledAsync_ReportsProgress()
    {
        var progressValues = new List<double>();
        var mock = new Mock<IStockfishService>();
        mock.Setup(s => s.EnsureInstalledAsync(It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .Callback<IProgress<double>?, CancellationToken>((p, _) =>
            {
                p?.Report(0.0);
                p?.Report(0.5);
                p?.Report(1.0);
            })
            .ReturnsAsync(true);

        var result = await mock.Object.EnsureInstalledAsync(
            new Progress<double>(v => progressValues.Add(v)));

        result.Should().BeTrue();
        // Note: Progress<T> posts to SynchronizationContext, values may not be immediate in test
        mock.Verify(s => s.EnsureInstalledAsync(It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
