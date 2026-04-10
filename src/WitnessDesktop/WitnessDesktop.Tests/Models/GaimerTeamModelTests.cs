using WitnessDesktop.Models;

namespace WitnessDesktop.Tests.Models;

public class GaimerTeamModelTests
{
    [Fact]
    public void GaimerTeamTask_Id_HasGtPrefix()
    {
        var task = new GaimerTeamTask
        {
            Task = "Look up chess opening theory for the Sicilian",
            Context = new GaimerTeamContext
            {
                Game = "Chess",
                Agent = "Leroy",
                SessionId = "s_test123"
            }
        };

        task.Id.Should().StartWith("gt_");
        task.Id.Length.Should().Be(15);
    }

    [Fact]
    public void GaimerTeamTask_ResponseFormat_DefaultsToVoice()
    {
        var task = new GaimerTeamTask
        {
            Task = "test",
            Context = new GaimerTeamContext
            {
                Game = "Chess",
                Agent = "Leroy",
                SessionId = "s_test"
            }
        };

        task.ResponseFormat.Should().Be("voice");
    }

    [Fact]
    public void GaimerTeamResult_Artifacts_DefaultsToEmptyList()
    {
        var result = new GaimerTeamResult
        {
            TaskId = "gt_abc123",
            Status = "complete",
            Response = "Here's what I found."
        };

        result.Artifacts.Should().NotBeNull();
        result.Artifacts.Should().BeEmpty();
        result.ActionsTaken.Should().NotBeNull();
        result.ActionsTaken.Should().BeEmpty();
    }

    [Fact]
    public void GaimerTeamResult_Surface_DefaultsToNull()
    {
        var result = new GaimerTeamResult
        {
            TaskId = "gt_surf1",
            Status = "complete",
            Response = "Done."
        };

        result.Surface.Should().BeNull();
    }

    [Theory]
    [InlineData("voice")]
    [InlineData("timeline")]
    [InlineData("both")]
    public void GaimerTeamResult_Surface_AcceptsValidValues(string surface)
    {
        var result = new GaimerTeamResult
        {
            TaskId = "gt_surf2",
            Status = "complete",
            Response = "Done.",
            Surface = surface
        };

        result.Surface.Should().Be(surface);
    }

    [Fact]
    public void GaimerTeamPermissionRequest_TimeoutSeconds_DefaultsTo60()
    {
        var perm = new GaimerTeamPermissionRequest
        {
            Id = "perm_1",
            TaskId = "gt_abc",
            Action = "Delete old replays",
            Risk = "low"
        };

        perm.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void GaimerTeamResultEventArgs_WrapsResult()
    {
        var result = new GaimerTeamResult
        {
            TaskId = "gt_x",
            Status = "complete",
            Response = "Done"
        };
        var args = new GaimerTeamResultEventArgs { Result = result };

        args.Result.Should().BeSameAs(result);
    }

    [Fact]
    public void GaimerTeamProgressEventArgs_CarriesFields()
    {
        var args = new GaimerTeamProgressEventArgs
        {
            TaskId = "gt_y",
            Message = "Working on it..."
        };

        args.TaskId.Should().Be("gt_y");
        args.Message.Should().Be("Working on it...");
    }

    [Fact]
    public void GaimerTeamPermissionEventArgs_CarriesRequest()
    {
        var req = new GaimerTeamPermissionRequest
        {
            Id = "perm_z",
            TaskId = "gt_z",
            Action = "Run script",
            Risk = "medium"
        };
        var args = new GaimerTeamPermissionEventArgs { Request = req };

        args.Request.Should().BeSameAs(req);
    }
}
