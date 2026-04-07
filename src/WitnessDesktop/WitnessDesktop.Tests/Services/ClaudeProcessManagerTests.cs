using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class ClaudeProcessManagerTests : IDisposable
{
    private readonly ClaudeProcessManager _sut;
    private readonly string _tempDir;

    public ClaudeProcessManagerTests()
    {
        _sut = new ClaudeProcessManager(Mock.Of<ILogger<ClaudeProcessManager>>());
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
}
