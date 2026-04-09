using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class ClaudeProcessManagerTests : IDisposable
{
    private readonly Mock<ISettingsService> _mockSettings;
    private readonly ClaudeProcessManager _sut;
    private readonly string _tempDir;

    public ClaudeProcessManagerTests()
    {
        _mockSettings = new Mock<ISettingsService>();
        _mockSettings.Setup(s => s.TeamPermissionMode).Returns("bypassPermissions");
        _sut = new ClaudeProcessManager(Mock.Of<ILogger<ClaudeProcessManager>>(), _mockSettings.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), $"gaimer-cpm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task LaunchAsync_WithValidCommand_SetsIsRunning()
    {
        var result = await _sut.LaunchAsync(_tempDir, "/nonexistent/plugin",
            cliOverride: "sleep", argsOverride: "999");

        result.Should().BeTrue();
        _sut.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task LaunchAsync_WhenCliNotOnPath_ReturnsFalse()
    {
        var result = await _sut.LaunchAsync(_tempDir, "/nonexistent/plugin",
            cliOverride: "nonexistent-binary-xyz-12345");

        result.Should().BeFalse();
        _sut.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task TerminateAsync_KillsProcess()
    {
        await _sut.LaunchAsync(_tempDir, "/nonexistent/plugin",
            cliOverride: "sleep", argsOverride: "999");
        _sut.IsRunning.Should().BeTrue();

        await _sut.TerminateAsync();

        _sut.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessExited_FiresOnUnexpectedExit()
    {
        var exitedFired = new TaskCompletionSource<bool>();
        _sut.ProcessExited += (_, _) => exitedFired.TrySetResult(true);

        await _sut.LaunchAsync(_tempDir, "/nonexistent/plugin",
            cliOverride: "true", argsOverride: "");

        var result = await Task.WhenAny(exitedFired.Task, Task.Delay(5000));
        result.Should().Be(exitedFired.Task, "ProcessExited should fire for unexpected exit");
    }

    [Fact]
    public async Task ProcessExited_DoesNotFireAfterTerminate()
    {
        var exitFired = false;
        _sut.ProcessExited += (_, _) => exitFired = true;

        await _sut.LaunchAsync(_tempDir, "/nonexistent/plugin",
            cliOverride: "sleep", argsOverride: "999");
        await _sut.TerminateAsync();
        await Task.Delay(500);

        exitFired.Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_TerminatesOwnedProcess()
    {
        await _sut.LaunchAsync(_tempDir, "/nonexistent/plugin",
            cliOverride: "sleep", argsOverride: "999");
        var pid = _sut.ProcessId;
        pid.Should().BeGreaterThan(0);

        _sut.Dispose();

        try
        {
            var p = Process.GetProcessById(pid);
            p.HasExited.Should().BeTrue();
        }
        catch (ArgumentException)
        {
            // Process already gone — expected
        }
    }

    // --- Permission mode flag tests ---

    [Fact]
    public void LaunchAsync_DefaultMode_PassesPermissionModeDefault()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.TeamPermissionMode).Returns("default");
        var sut = new ClaudeProcessManager(Mock.Of<ILogger<ClaudeProcessManager>>(), settings.Object);

        var args = sut.BuildArguments("/some/plugin");

        args.Should().Contain("--permission-mode default");
        args.Should().NotContain("--dangerously-skip-permissions");
        sut.Dispose();
    }

    [Fact]
    public void LaunchAsync_BypassMode_PassesBypassPermissions()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.TeamPermissionMode).Returns("bypassPermissions");
        var sut = new ClaudeProcessManager(Mock.Of<ILogger<ClaudeProcessManager>>(), settings.Object);

        var args = sut.BuildArguments("/some/plugin");

        args.Should().Contain("--permission-mode bypassPermissions");
        sut.Dispose();
    }

    [Fact]
    public void LaunchAsync_PlanMode_PassesPlanPermission()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.TeamPermissionMode).Returns("plan");
        var sut = new ClaudeProcessManager(Mock.Of<ILogger<ClaudeProcessManager>>(), settings.Object);

        var args = sut.BuildArguments("/some/plugin");

        args.Should().Contain("--permission-mode plan");
        sut.Dispose();
    }

    [Fact]
    public void BuildArguments_EmptyMode_FallsBackToDefault()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.TeamPermissionMode).Returns(string.Empty);
        var sut = new ClaudeProcessManager(Mock.Of<ILogger<ClaudeProcessManager>>(), settings.Object);

        var args = sut.BuildArguments("/some/plugin");

        args.Should().Contain("--permission-mode default");
        sut.Dispose();
    }

    [Fact]
    public void BuildArguments_NullMode_FallsBackToDefault()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.TeamPermissionMode).Returns((string)null!);
        var sut = new ClaudeProcessManager(Mock.Of<ILogger<ClaudeProcessManager>>(), settings.Object);

        var args = sut.BuildArguments("/some/plugin");

        args.Should().Contain("--permission-mode default");
        sut.Dispose();
    }

    [Fact]
    public void BuildArguments_InvalidMode_FallsBackToDefault()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.TeamPermissionMode).Returns("auto --dangerously-skip-permissions");
        var sut = new ClaudeProcessManager(Mock.Of<ILogger<ClaudeProcessManager>>(), settings.Object);

        var args = sut.BuildArguments("/some/plugin");

        args.Should().Contain("--permission-mode default");
        args.Should().NotContain("--dangerously-skip-permissions");
        sut.Dispose();
    }
}
