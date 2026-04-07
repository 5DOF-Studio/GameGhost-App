using System.Text.Json;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class GaimerTeamServiceTests : IDisposable
{
    private readonly Mock<IGaimerPipeClient> _mockPipe;
    private readonly Mock<IClaudeProcessManager> _mockProcess;
    private readonly GaimerTeamService _sut;

    public GaimerTeamServiceTests()
    {
        _mockPipe = new Mock<IGaimerPipeClient>();
        _mockProcess = new Mock<IClaudeProcessManager>();
        _sut = new GaimerTeamService(
            _mockPipe.Object,
            _mockProcess.Object,
            Mock.Of<ILogger<GaimerTeamService>>());
    }

    public void Dispose() => _sut.Dispose();

    private GaimerTeamTask CreateTask(string task = "Look up chess opening") => new()
    {
        Task = task,
        Context = new GaimerTeamContext
        {
            Game = "Chess",
            Agent = "Leroy",
            SessionId = "s_test"
        }
    };

    // ── SubmitTaskAsync ──────────────────────────────────────────

    [Fact]
    public async Task SubmitTaskAsync_WhenDisconnected_Throws()
    {
        var act = () => _sut.SubmitTaskAsync(CreateTask());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SubmitTaskAsync_SerializesAndSendsToPipe()
    {
        SimulateConnected();
        string? sentJson = null;
        _mockPipe.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((json, _) => sentJson = json)
            .Returns(Task.CompletedTask);

        var task = CreateTask("Look up Sicilian Najdorf");
        await _sut.SubmitTaskAsync(task);

        sentJson.Should().NotBeNull();
        var doc = JsonDocument.Parse(sentJson!);
        doc.RootElement.GetProperty("type").GetString().Should().Be("task_request");
        doc.RootElement.GetProperty("id").GetString().Should().Be(task.Id);
        doc.RootElement.GetProperty("task").GetString().Should().Be("Look up Sicilian Najdorf");
        doc.RootElement.GetProperty("timestamp").GetString().Should().NotBeNullOrEmpty();
        var ctx = doc.RootElement.GetProperty("context");
        ctx.GetProperty("game").GetString().Should().Be("Chess");
        ctx.GetProperty("agent").GetString().Should().Be("Leroy");
    }

    // ── Message Routing ──────────────────────────────────────────

    [Fact]
    public async Task OnTaskResult_FiresTaskCompleted()
    {
        SimulateConnected();
        var task = CreateTask();
        await _sut.SubmitTaskAsync(task);

        GaimerTeamResult? received = null;
        _sut.TaskCompleted += (_, e) => received = e.Result;

        var resultJson = JsonSerializer.Serialize(new
        {
            type = "task_result",
            task_id = task.Id,
            status = "complete",
            response = "The Sicilian Najdorf is a strong opening.",
            actions_taken = new[] { "web_search" },
            follow_up = (string?)null,
            artifacts = Array.Empty<object>()
        });
        SimulateMessageReceived(resultJson);

        received.Should().NotBeNull();
        received!.TaskId.Should().Be(task.Id);
        received.Status.Should().Be("complete");
        received.Response.Should().Contain("Sicilian");
    }

    [Fact]
    public async Task OnStatusUpdate_FiresTaskProgress()
    {
        SimulateConnected();

        string? progressTaskId = null;
        string? progressMessage = null;
        _sut.TaskProgress += (_, e) =>
        {
            progressTaskId = e.TaskId;
            progressMessage = e.Message;
        };

        var statusJson = JsonSerializer.Serialize(new
        {
            type = "status_update",
            task_id = "gt_test123",
            message = "Searching the web..."
        });
        SimulateMessageReceived(statusJson);

        progressTaskId.Should().Be("gt_test123");
        progressMessage.Should().Be("Searching the web...");
    }

    [Fact]
    public async Task OnPermissionRequest_FiresPermissionRequested()
    {
        SimulateConnected();

        GaimerTeamPermissionRequest? received = null;
        _sut.PermissionRequested += (_, e) => received = e.Request;

        var permJson = JsonSerializer.Serialize(new
        {
            type = "permission_request",
            id = "perm_abc123",
            task_id = "gt_task1",
            action = "Delete file ~/notes.txt",
            risk = "high",
            timeout_seconds = 30
        });
        SimulateMessageReceived(permJson);

        received.Should().NotBeNull();
        received!.Id.Should().Be("perm_abc123");
        received.Action.Should().Contain("Delete");
    }

    [Fact]
    public async Task OnPong_ResetsMissedPings()
    {
        SimulateConnected();
        SimulateMessageReceived("{\"type\":\"pong\"}");
        _sut.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task OnError_MapsToTaskCompleted()
    {
        SimulateConnected();
        var task = CreateTask();
        await _sut.SubmitTaskAsync(task);

        GaimerTeamResult? received = null;
        _sut.TaskCompleted += (_, e) => received = e.Result;

        var errorJson = JsonSerializer.Serialize(new
        {
            type = "error",
            task_id = task.Id,
            message = "Something went wrong"
        });
        SimulateMessageReceived(errorJson);

        received.Should().NotBeNull();
        received!.Status.Should().Be("error");
    }

    [Fact]
    public async Task OnUnknownType_DoesNotThrow()
    {
        SimulateConnected();
        var act = () => SimulateMessageReceived("{\"type\":\"future_type\",\"data\":1}");
        act.Should().NotThrow();
    }

    // ── CancelTaskAsync ──────────────────────────────────────────

    [Fact]
    public async Task CancelTaskAsync_DropsLateResult()
    {
        SimulateConnected();
        var task = CreateTask();
        await _sut.SubmitTaskAsync(task);
        await _sut.CancelTaskAsync(task.Id);

        GaimerTeamResult? received = null;
        _sut.TaskCompleted += (_, e) => received = e.Result;

        var resultJson = JsonSerializer.Serialize(new
        {
            type = "task_result",
            task_id = task.Id,
            status = "complete",
            response = "Late result"
        });
        SimulateMessageReceived(resultJson);

        received.Should().BeNull("cancelled task results should be silently dropped");
    }

    // ── RespondToPermissionAsync ──────────────────────────────────

    [Fact]
    public async Task RespondToPermissionAsync_SendsResponseToPipe()
    {
        SimulateConnected();
        string? sentJson = null;
        _mockPipe.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((json, _) => sentJson = json)
            .Returns(Task.CompletedTask);

        await _sut.RespondToPermissionAsync("perm_abc", true);

        sentJson.Should().NotBeNull();
        var doc = JsonDocument.Parse(sentJson!);
        doc.RootElement.GetProperty("type").GetString().Should().Be("permission_response");
        doc.RootElement.GetProperty("id").GetString().Should().Be("perm_abc");
        doc.RootElement.GetProperty("approved").GetBoolean().Should().BeTrue();
    }

    // ── DisconnectAsync ──────────────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_Owned_TerminatesProcess()
    {
        SimulateConnected(owned: true);

        await _sut.DisconnectAsync(terminateOwnedSession: true);

        _sut.IsConnected.Should().BeFalse();
        _mockProcess.Verify(p => p.TerminateAsync(), Times.Once);
        _mockPipe.Verify(p => p.Disconnect(), Times.Once);
    }

    [Fact]
    public async Task DisconnectAsync_Existing_KeepsProcess()
    {
        SimulateConnected(owned: false);

        await _sut.DisconnectAsync(terminateOwnedSession: true);

        _sut.IsConnected.Should().BeFalse();
        _mockProcess.Verify(p => p.TerminateAsync(), Times.Never);
        _mockPipe.Verify(p => p.Disconnect(), Times.Once);
    }

    [Fact]
    public async Task DisconnectAsync_ErrorsOutPendingTasks()
    {
        SimulateConnected();
        var task = CreateTask();
        await _sut.SubmitTaskAsync(task);

        var results = new List<GaimerTeamResult>();
        _sut.TaskCompleted += (_, e) => results.Add(e.Result);

        await _sut.DisconnectAsync();

        results.Should().ContainSingle();
        results[0].TaskId.Should().Be(task.Id);
        results[0].Status.Should().Be("error");
    }

    // ── Helpers ──────────────────────────────────────────────────

    private void SimulateConnected(bool owned = true)
    {
        _mockPipe.SetupGet(p => p.IsConnected).Returns(true);
        _mockPipe.Setup(p => p.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockPipe.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut.SetConnectedForTest(owned);
    }

    private void SimulateMessageReceived(string json)
    {
        _mockPipe.Raise(p => p.MessageReceived += null, _mockPipe.Object, json);
    }
}
