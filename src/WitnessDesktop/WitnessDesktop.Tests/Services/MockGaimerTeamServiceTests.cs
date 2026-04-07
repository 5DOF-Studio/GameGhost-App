using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class MockGaimerTeamServiceTests : IDisposable
{
    private readonly MockGaimerTeamService _sut;

    public MockGaimerTeamServiceTests()
    {
        _sut = new MockGaimerTeamService(Mock.Of<ILogger<MockGaimerTeamService>>());
    }

    public void Dispose() => _sut.Dispose();

    private GaimerTeamTask CreateTask(string task = "Test task") => new()
    {
        Task = task,
        Context = new GaimerTeamContext
        {
            Game = "Chess",
            Agent = "Leroy",
            SessionId = "s_test"
        }
    };

    [Fact]
    public async Task SubmitTaskAsync_ReturnsGtPrefixedId()
    {
        await _sut.LaunchSessionAsync();
        var taskId = await _sut.SubmitTaskAsync(CreateTask());
        taskId.Should().StartWith("gt_");
    }

    [Fact]
    public async Task SubmitTaskAsync_FiresTaskCompleted()
    {
        _sut.PermissionProbability = 0.0; // No permission interruption
        await _sut.LaunchSessionAsync();
        GaimerTeamResult? received = null;
        _sut.TaskCompleted += (_, e) => received = e.Result;

        await _sut.SubmitTaskAsync(CreateTask());
        await Task.Delay(4000);

        received.Should().NotBeNull();
        received!.Status.Should().Be("complete");
        received.Response.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SubmitTaskAsync_CyclesThroughCannedResponses()
    {
        _sut.PermissionProbability = 0.0;
        await _sut.LaunchSessionAsync();
        var responses = new List<string>();
        _sut.TaskCompleted += (_, e) => responses.Add(e.Result.Response);

        for (int i = 0; i < 3; i++)
            await _sut.SubmitTaskAsync(CreateTask($"task-{i}"));
        await Task.Delay(4500);

        responses.Should().HaveCount(3);
        responses.Distinct().Count().Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task CancelTaskAsync_PreventsCompletion()
    {
        _sut.PermissionProbability = 0.0;
        await _sut.LaunchSessionAsync();
        bool completed = false;
        _sut.TaskCompleted += (_, _) => completed = true;

        var taskId = await _sut.SubmitTaskAsync(CreateTask());
        await _sut.CancelTaskAsync(taskId);
        await Task.Delay(4000);

        completed.Should().BeFalse();
    }

    [Fact]
    public async Task Permission_WhenApproved_TaskCompletes()
    {
        _sut.PermissionProbability = 1.0;
        await _sut.LaunchSessionAsync();

        GaimerTeamPermissionRequest? permReq = null;
        GaimerTeamResult? result = null;
        _sut.PermissionRequested += (_, e) => permReq = e.Request;
        _sut.TaskCompleted += (_, e) => result = e.Result;

        await _sut.SubmitTaskAsync(CreateTask());
        await Task.Delay(500);

        permReq.Should().NotBeNull();
        await _sut.RespondToPermissionAsync(permReq!.Id, approved: true);
        await Task.Delay(4000);

        result.Should().NotBeNull();
        result!.Status.Should().Be("complete");
    }

    [Fact]
    public async Task Permission_WhenDenied_TaskErrors()
    {
        _sut.PermissionProbability = 1.0;
        await _sut.LaunchSessionAsync();

        GaimerTeamPermissionRequest? permReq = null;
        GaimerTeamResult? result = null;
        _sut.PermissionRequested += (_, e) => permReq = e.Request;
        _sut.TaskCompleted += (_, e) => result = e.Result;

        await _sut.SubmitTaskAsync(CreateTask());
        await Task.Delay(500);

        permReq.Should().NotBeNull();
        await _sut.RespondToPermissionAsync(permReq!.Id, approved: false);
        await Task.Delay(1000);

        result.Should().NotBeNull();
        result!.Status.Should().Be("error");
        result.ErrorCode.Should().Be("permission_denied");
    }

    [Fact]
    public async Task DisconnectAsync_CancelsAllPending()
    {
        _sut.PermissionProbability = 0.0;
        await _sut.LaunchSessionAsync();
        int completedCount = 0;
        _sut.TaskCompleted += (_, _) => Interlocked.Increment(ref completedCount);

        await _sut.SubmitTaskAsync(CreateTask("task-1"));
        await _sut.SubmitTaskAsync(CreateTask("task-2"));
        await _sut.SubmitTaskAsync(CreateTask("task-3"));

        await _sut.DisconnectAsync();
        await Task.Delay(4000);

        completedCount.Should().Be(0);
        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchSession_SetsIsConnected()
    {
        _sut.IsConnected.Should().BeFalse();
        await _sut.LaunchSessionAsync();
        _sut.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectExisting_SetsIsConnected()
    {
        await _sut.ConnectExistingAsync();
        _sut.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void IsConfigured_AlwaysTrue()
    {
        _sut.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task AfterDispose_AllMethodsThrow()
    {
        _sut.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _sut.SubmitTaskAsync(CreateTask()));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _sut.CancelTaskAsync("gt_abc"));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _sut.RespondToPermissionAsync("perm_1", true));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _sut.LaunchSessionAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _sut.ConnectExistingAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _sut.DisconnectAsync());
    }

    [Fact]
    public async Task SubmitTask_WhenNotConnected_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SubmitTaskAsync(CreateTask()));
    }
}
