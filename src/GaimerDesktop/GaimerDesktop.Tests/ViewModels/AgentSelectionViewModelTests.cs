using FluentAssertions;
using Moq;
using GaimerDesktop.Models;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.ViewModels;
using Xunit;

namespace GaimerDesktop.Tests.ViewModels;

/// <summary>
/// Tests for AgentSelectionViewModel: agent initialization, Stockfish lifecycle,
/// download flow, and navigation guard behavior.
/// </summary>
public class AgentSelectionViewModelTests
{
    #region Constructor and Initialization

    [Fact]
    public void Constructor_DefaultParameterless_InitializesAvailableAgents()
    {
        var vm = new AgentSelectionViewModel();

        vm.AvailableAgents.Should().NotBeEmpty();
        vm.AvailableAgents.Should().BeEquivalentTo(Agents.Available);
    }

    [Fact]
    public void Constructor_WithStockfishService_StoresService()
    {
        var mockStockfish = new Mock<IStockfishService>();
        mockStockfish.Setup(s => s.IsInstalled).Returns(true);

        var vm = new AgentSelectionViewModel(mockStockfish.Object);

        vm.IsStockfishInstalled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NullStockfishService_IsStockfishInstalledIsFalse()
    {
        var vm = new AgentSelectionViewModel(null);

        vm.IsStockfishInstalled.Should().BeFalse();
    }

    [Fact]
    public void AvailableAgents_ContainsLeroy()
    {
        var vm = new AgentSelectionViewModel();

        vm.AvailableAgents.Should().Contain(a => a.Key == "chess" && a.Name == "Leroy");
    }

    [Fact]
    public void AvailableAgents_ContainsWasp()
    {
        var vm = new AgentSelectionViewModel();

        vm.AvailableAgents.Should().Contain(a => a.Key == "wasp" && a.Name == "Wasp");
    }

    [Fact]
    public void Wasp_Id_IsChessMaster()
    {
        Agents.Wasp.Id.Should().Be("Chess Master");
    }

    [Fact]
    public void Leroy_Id_IsChessMaster()
    {
        Agents.Chess.Id.Should().Be("Chess Master");
    }

    [Fact]
    public void AvailableAgents_DoesNotContainGatedAgents()
    {
        var vm = new AgentSelectionViewModel();

        vm.AvailableAgents.Should().NotContain(a => a.Key == "general");
    }

    #endregion

    #region SelectAgentAsync

    [Fact]
    public async Task SelectAgentAsync_NullId_DoesNothing()
    {
        var vm = new AgentSelectionViewModel();

        // Should not throw
        await vm.SelectAgentCommand.ExecuteAsync(null);

        vm.ShowDownloadOverlay.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAgentAsync_EmptyId_DoesNothing()
    {
        var vm = new AgentSelectionViewModel();

        await vm.SelectAgentCommand.ExecuteAsync("  ");

        vm.ShowDownloadOverlay.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAgentAsync_UnknownId_DoesNothing()
    {
        var vm = new AgentSelectionViewModel();

        await vm.SelectAgentCommand.ExecuteAsync("nonexistent_agent");

        vm.ShowDownloadOverlay.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAgentAsync_ChessAgent_NoStockfish_ShowsDownloadOverlay()
    {
        var mockStockfish = new Mock<IStockfishService>();
        mockStockfish.Setup(s => s.IsInstalled).Returns(false);

        var vm = new AgentSelectionViewModel(mockStockfish.Object);

        await vm.SelectAgentCommand.ExecuteAsync("chess");

        vm.ShowDownloadOverlay.Should().BeTrue();
        vm.DownloadError.Should().BeNull();
    }

    [Fact]
    public async Task SelectAgentAsync_ChessAgent_StockfishInstalled_StartsEngine()
    {
        var mockStockfish = new Mock<IStockfishService>();
        mockStockfish.Setup(s => s.IsInstalled).Returns(true);
        mockStockfish.Setup(s => s.IsReady).Returns(false);
        mockStockfish.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Shell.Current is null in net8.0 so navigation will be a no-op
        var vm = new AgentSelectionViewModel(mockStockfish.Object);

        await vm.SelectAgentCommand.ExecuteAsync("chess");

        mockStockfish.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectAgentAsync_ChessAgent_StockfishReady_DoesNotStartAgain()
    {
        var mockStockfish = new Mock<IStockfishService>();
        mockStockfish.Setup(s => s.IsInstalled).Returns(true);
        mockStockfish.Setup(s => s.IsReady).Returns(true);

        var vm = new AgentSelectionViewModel(mockStockfish.Object);

        await vm.SelectAgentCommand.ExecuteAsync("chess");

        // Should NOT call StartAsync since IsReady is already true
        mockStockfish.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DownloadStockfishAsync

    [Fact]
    public async Task DownloadStockfishAsync_NullService_IsNoop()
    {
        var vm = new AgentSelectionViewModel(null);

        await vm.DownloadStockfishCommand.ExecuteAsync(null);

        vm.IsDownloading.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadStockfishAsync_Success_SetsIsDownloadingDuringExecution()
    {
        var tcs = new TaskCompletionSource<bool>();
        var mockStockfish = new Mock<IStockfishService>();
        mockStockfish.Setup(s => s.EnsureInstalledAsync(It.IsAny<IProgress<double>?>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        mockStockfish.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var vm = new AgentSelectionViewModel(mockStockfish.Object);
        vm.ShowDownloadOverlay = true;

        var downloadTask = vm.DownloadStockfishCommand.ExecuteAsync(null);

        // While EnsureInstalledAsync is pending, IsDownloading should be true
        vm.IsDownloading.Should().BeTrue();

        tcs.SetResult(true);
        await downloadTask;

        vm.IsDownloading.Should().BeFalse();
        vm.ShowDownloadOverlay.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadStockfishAsync_Failure_SetsDownloadError()
    {
        var mockStockfish = new Mock<IStockfishService>();
        mockStockfish.Setup(s => s.EnsureInstalledAsync(It.IsAny<IProgress<double>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var vm = new AgentSelectionViewModel(mockStockfish.Object);

        await vm.DownloadStockfishCommand.ExecuteAsync(null);

        vm.DownloadError.Should().NotBeNullOrEmpty();
        vm.DownloadError.Should().Contain("failed");
        vm.IsDownloading.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadStockfishAsync_Exception_SetsDownloadError()
    {
        var mockStockfish = new Mock<IStockfishService>();
        mockStockfish.Setup(s => s.EnsureInstalledAsync(It.IsAny<IProgress<double>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("network error"));

        var vm = new AgentSelectionViewModel(mockStockfish.Object);

        await vm.DownloadStockfishCommand.ExecuteAsync(null);

        vm.DownloadError.Should().Contain("network error");
        vm.IsDownloading.Should().BeFalse();
    }

    #endregion

    #region SkipDownloadAsync

    [Fact]
    public async Task SkipDownloadAsync_HidesOverlay()
    {
        var vm = new AgentSelectionViewModel();
        vm.ShowDownloadOverlay = true;

        await vm.SkipDownloadCommand.ExecuteAsync(null);

        vm.ShowDownloadOverlay.Should().BeFalse();
    }

    #endregion

    #region IsStockfishInstalled Property

    [Fact]
    public void IsStockfishInstalled_DelegatesToService()
    {
        var mockStockfish = new Mock<IStockfishService>();
        mockStockfish.Setup(s => s.IsInstalled).Returns(false);

        var vm = new AgentSelectionViewModel(mockStockfish.Object);
        vm.IsStockfishInstalled.Should().BeFalse();

        mockStockfish.Setup(s => s.IsInstalled).Returns(true);
        vm.IsStockfishInstalled.Should().BeTrue();
    }

    #endregion

    #region Initial State

    [Fact]
    public void InitialState_AllBooleansFalse()
    {
        var vm = new AgentSelectionViewModel();

        vm.IsDownloading.Should().BeFalse();
        vm.DownloadProgress.Should().Be(0);
        vm.ShowDownloadOverlay.Should().BeFalse();
        vm.DownloadError.Should().BeNull();
    }

    #endregion
}
