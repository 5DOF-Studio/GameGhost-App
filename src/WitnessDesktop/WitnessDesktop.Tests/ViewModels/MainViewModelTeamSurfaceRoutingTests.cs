using Moq;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Tests.ViewModels;

public class MainViewModel_TeamSurfaceRouting_Tests : MainViewModelTestBase
{
    private void SetupGaimerTeam()
    {
        MockGaimerTeam = new Mock<IGaimerTeamService>();
        MockGaimerTeam.Setup(t => t.IsConfigured).Returns(true);
        MockGaimerTeam.Setup(t => t.IsConnected).Returns(true);
    }

    private void SetupVoiceConnected()
    {
        MockConversation.Setup(c => c.IsConnected).Returns(true);
        MockConversation.Setup(c => c.SendContextualUpdateWithResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private GaimerTeamResultEventArgs CreateResultArgs(
        string status = "complete",
        string response = "The Sicilian Najdorf is a sharp opening choice.",
        string responseFormat = "voice",
        string? surface = null)
    {
        return new GaimerTeamResultEventArgs
        {
            Result = new GaimerTeamResult
            {
                TaskId = "gt_test123",
                Status = status,
                Response = response,
                Surface = surface
            },
            ResponseFormat = responseFormat
        };
    }

    // ── Surface = "both" (default) ──────────────────────────────────

    [Fact]
    public void TaskCompleted_SurfaceBoth_AddsTimelineAndSpeaks()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(surface: "both"));

        MockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(
            e => e.Type == EventOutputType.TeamResult)), Times.Once);
        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.Is<string>(s => s.StartsWith("The team's back.")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Surface = "timeline" ────────────────────────────────────────

    [Fact]
    public void TaskCompleted_SurfaceTimeline_AddsTimelineOnly()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(surface: "timeline"));

        MockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(
            e => e.Type == EventOutputType.TeamResult)), Times.Once);
        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Surface = "voice" ───────────────────────────────────────────

    [Fact]
    public void TaskCompleted_SurfaceVoice_SpeaksOnly_NoTimeline()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(surface: "voice"));

        MockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(
            e => e.Type == EventOutputType.TeamResult)), Times.Never);
        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.Is<string>(s => s.StartsWith("The team's back.")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Surface = null, ResponseFormat fallback ─────────────────────

    [Fact]
    public void TaskCompleted_NoSurface_VoiceFormat_DefaultsToBoth()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(responseFormat: "voice", surface: null));

        MockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(
            e => e.Type == EventOutputType.TeamResult)), Times.Once);
        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TaskCompleted_NoSurface_DetailedFormat_DefaultsToTimeline()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(responseFormat: "detailed", surface: null));

        MockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(
            e => e.Type == EventOutputType.TeamResult)), Times.Once);
        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Voice truncation ────────────────────────────────────────────

    [Fact]
    public void TaskCompleted_LongResponse_TruncatesForVoice()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        var longResponse = "First sentence. Second sentence. Third sentence. Fourth sentence. Fifth sentence.";
        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(response: longResponse, surface: "both"));

        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.Is<string>(s =>
                s.Contains("First sentence.") &&
                s.Contains("Third sentence.") &&
                !s.Contains("Fourth sentence")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TaskCompleted_LongResponse_PreservesPunctuation()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        var mixedResponse = "What should I do? Try the Sicilian! It leads to sharp play. Trust me. Go for it.";
        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(response: mixedResponse, surface: "voice"));

        // Original punctuation (? and !) should be preserved, not flattened to periods
        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.Is<string>(s =>
                s.Contains("What should I do?") &&
                s.Contains("Try the Sicilian!") &&
                s.Contains("It leads to sharp play.") &&
                !s.Contains("Trust me")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TaskCompleted_ShortResponse_NotTruncated()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        var shortResponse = "Done. File saved.";
        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(response: shortResponse, surface: "voice"));

        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.Is<string>(s => s.Contains("Done") && s.Contains("File saved")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Voice disconnected ──────────────────────────────────────────

    [Fact]
    public void TaskCompleted_VoiceDisconnected_SkipsVoice()
    {
        SetupGaimerTeam();
        // Voice NOT connected (default mock state)
        var sut = CreateSut();

        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(surface: "both"));

        MockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(
            e => e.Type == EventOutputType.TeamResult)), Times.Once);
        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Error routing ───────────────────────────────────────────────

    [Fact]
    public void TaskCompleted_Error_AlwaysTimeline_ShortVoiceNotice()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(status: "error", response: "Permission denied for file write"));

        MockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(
            e => e.Type == EventOutputType.TeamResult &&
                 e.Summary!.Contains("Team error"))), Times.Once);
        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.Is<string>(s => s.Contains("ran into an issue")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Surface overrides ResponseFormat ────────────────────────────

    [Fact]
    public void TaskCompleted_SurfaceOverridesResponseFormat()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        // ResponseFormat says "detailed" (would default to timeline-only),
        // but Claude explicitly chose "voice"
        MockGaimerTeam!.Raise(t => t.TaskCompleted += null, this,
            CreateResultArgs(responseFormat: "detailed", surface: "voice"));

        // Surface wins: voice only, no timeline
        MockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(
            e => e.Type == EventOutputType.TeamResult)), Times.Never);
        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── TaskProgress ────────────────────────────────────────────────

    [Fact]
    public void TaskProgress_AddsTimelineEvent()
    {
        SetupGaimerTeam();
        var sut = CreateSut();

        MockGaimerTeam!.Raise(t => t.TaskProgress += null, this,
            new GaimerTeamProgressEventArgs { TaskId = "gt_prog1", Message = "Searching the web..." });

        MockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(
            e => e.Type == EventOutputType.TeamProgress &&
                 e.Summary == "Searching the web...")), Times.Once);
    }

    [Fact]
    public void TaskProgress_NeverSpeaks()
    {
        SetupGaimerTeam();
        SetupVoiceConnected();
        var sut = CreateSut();

        MockGaimerTeam!.Raise(t => t.TaskProgress += null, this,
            new GaimerTeamProgressEventArgs { TaskId = "gt_prog2", Message = "Reading files..." });

        MockConversation.Verify(c => c.SendContextualUpdateWithResponseAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
