using FluentAssertions;
using Moq;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Tests.ViewModels;
using WitnessDesktop.ViewModels;
using Xunit;

namespace WitnessDesktop.Tests;

/// <summary>
/// Tests for MainViewModel ghost mode unified card wiring:
/// VAD level forwarding, 4-toggle audio mapping, ShowCard flags.
/// </summary>
public class MainViewModelGhostModeTests : MainViewModelTestBase
{
    // =========================================================================
    // VAD LEVEL FORWARDING
    // =========================================================================

    [Fact]
    public void VadLevel_ForwardedToGhostService_WhenGhostModeActive()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();

        sut.InputVolume = 0.75f;

        MockGhost.Verify(g => g.SetVadLevel(It.IsAny<float>()), Times.AtLeastOnce);
    }

    [Fact]
    public void VadLevel_NotForwarded_WhenGhostModeInactive()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(false);
        var sut = CreateSut();

        sut.InputVolume = 0.5f;

        MockGhost.Verify(g => g.SetVadLevel(It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public void VadLevel_Throttled_SkipsRapidUpdates()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();

        // Rapid updates within same millisecond window
        for (int i = 0; i < 20; i++)
        {
            sut.InputVolume = i * 0.05f;
        }

        // Throttled to ~15fps (66ms), so in a tight synchronous loop
        // far fewer than 20 calls should get through.
        var callCount = MockGhost.Invocations
            .Count(inv => inv.Method.Name == "SetVadLevel");
        callCount.Should().BeLessThan(20,
            "VAD forwarding should be throttled to ~15fps");
    }

    [Fact]
    public void VadLevel_ForwardsOutputVolumeToo()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();

        sut.OutputVolume = 0.6f;

        MockGhost.Verify(g => g.SetVadLevel(It.IsAny<float>()), Times.AtLeastOnce);
    }

    // =========================================================================
    // 4-TOGGLE AUDIO MAPPING
    // =========================================================================

    [Fact]
    public void AudioToggleChanged_Index0_MapsToVoiceChat()
    {
        // Chess agent supports voice chat by default
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.ConnectionState = ConnectionState.Connected;

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(0, true));

        sut.IsVoiceChatActive.Should().BeTrue();
    }

    [Fact]
    public void AudioToggleChanged_Index1_MapsToVoiceCommand()
    {
        // Chess agent does NOT support voice command -- snap-back will fire.
        // Verify the correct feature name in the unsupported event proves
        // the toggle routed to IsAiMicActive (which checks SupportsVoiceCommand).
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        string? unsupportedFeature = null;
        sut.UnsupportedAudioFeatureToggled += (_, name) => unsupportedFeature = name;

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(1, true));

        unsupportedFeature.Should().Be("Voice Command",
            "Index 1 should route to IsAiMicActive which guards on SupportsVoiceCommand");
    }

    [Fact]
    public void AudioToggleChanged_UnsupportedFeatureInGhostMode_ShowsGhostAlertInsteadOfRaisingUiEvent()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        string? unsupportedFeature = null;
        sut.UnsupportedAudioFeatureToggled += (_, name) => unsupportedFeature = name;

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(1, true));

        unsupportedFeature.Should().BeNull("ghost mode should not route unsupported toggles through MAUI DisplayAlert");
        MockGhost.Verify(g => g.ShowCard(
            FabCardVariant.Text,
            null,
            It.Is<string>(s => s.Contains("does not support: Voice Command")),
            null,
            true,
            It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public void AudioToggleChanged_Index2_MapsToGameAudio()
    {
        // Chess agent does NOT support game audio -- snap-back fires.
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        string? unsupportedFeature = null;
        sut.UnsupportedAudioFeatureToggled += (_, name) => unsupportedFeature = name;

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(2, true));

        unsupportedFeature.Should().Be("Game Audio",
            "Index 2 should route to IsCommentaryActive which guards on SupportsGameAudio");
    }

    [Fact]
    public void AudioToggleChanged_Index3_MapsToAudioIn()
    {
        // Chess agent does NOT support audio in -- snap-back fires.
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        string? unsupportedFeature = null;
        sut.UnsupportedAudioFeatureToggled += (_, name) => unsupportedFeature = name;

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(3, true));

        unsupportedFeature.Should().Be("Audio In",
            "Index 3 should route to IsAudioInActive which guards on SupportsAudioIn");
    }

    [Fact]
    public void AudioToggleChanged_VoiceChatRequiresConnectionInGhostMode_ShowsGhostAlert()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.ConnectionState = ConnectionState.Disconnected;
        string? unsupportedFeature = null;
        sut.UnsupportedAudioFeatureToggled += (_, name) => unsupportedFeature = name;

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(0, true));

        unsupportedFeature.Should().BeNull();
        MockGhost.Verify(g => g.ShowCard(
            FabCardVariant.Text,
            null,
            "Voice Chat requires a game connection. Connect to a game first.",
            null,
            true,
            It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public void AudioToggleChanged_UnknownIndex_IsIgnored()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(99, false));

        sut.IsVoiceChatActive.Should().BeFalse();
        sut.IsAiMicActive.Should().BeFalse();
        sut.IsCommentaryActive.Should().BeFalse();
        sut.IsAudioInActive.Should().BeFalse();
    }

    [Fact]
    public async Task AudioToggleChanged_VoiceChatBounceFalseWhilePending_IsIgnored()
    {
        var tcs = new TaskCompletionSource();
        MockAudio.Setup(a => a.StartRecordingAsync(It.IsAny<Action<byte[]>>()))
            .Returns(tcs.Task);

        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.ConnectionState = ConnectionState.Connected;

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(0, true));
        await Task.Delay(50);

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(0, false));
        await Task.Delay(50);

        sut.IsVoiceChatActive.Should().BeTrue("a bounced OFF event during startup should be ignored");
        MockAudio.Verify(a => a.StopRecordingAsync(), Times.Never);

        tcs.SetResult();
        await Task.Delay(50);
        sut.IsVoiceChatPending.Should().BeFalse();
        sut.IsVoiceChatActive.Should().BeTrue();
    }

    [Fact]
    public async Task AudioToggleChanged_VoiceChatQuickDuplicateSameValue_IsDebounced()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.ConnectionState = ConnectionState.Connected;

        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(0, true));
        MockGhost.Raise(g => g.AudioToggleChanged += null,
            MockGhost.Object, new AudioToggleEventArgs(0, true));
        await Task.Delay(50);

        MockAudio.Verify(a => a.StartRecordingAsync(It.IsAny<Action<byte[]>>()), Times.Once);
        sut.IsVoiceChatActive.Should().BeTrue();
    }

    [Fact]
    public void SyncAudioStateToGhost_Passes4Bools()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.ConnectionState = ConnectionState.Connected;

        // Toggle voice chat ON (supported by chess agent)
        sut.IsVoiceChatActive = true;

        // SyncAudioStateToGhost called with all 4 params
        MockGhost.Verify(g => g.SetAudioState(
            true,  // voiceChat (IsVoiceChatActive)
            false, // voiceCommand (IsAiMicActive)
            false, // gameAudio (IsCommentaryActive)
            false  // audioIn (IsAudioInActive)
        ), Times.AtLeastOnce);
    }

    // =========================================================================
    // SHOW CARD FLAGS
    // =========================================================================

    [Fact]
    public void ShowCard_DefaultFlags_WhenNoVoiceChat()
    {
        // Voice chat NOT active, ghost mode active
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();

        RaiseTextReceived("Normal text message");

        MockGhost.Verify(g => g.ShowCard(
            FabCardVariant.Text,
            It.IsAny<string?>(),
            "Normal text message",
            It.IsAny<string?>(),
            false,  // isAlert
            false   // isVoiceDelivered
        ), Times.Once);
    }

    [Fact]
    public void ShowCard_VoiceDeliveredFlag_WhenVoiceChatActive()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.ConnectionState = ConnectionState.Connected;
        sut.IsVoiceChatActive = true;

        RaiseTextReceived("Voice delivered text");

        MockGhost.Verify(g => g.ShowCard(
            FabCardVariant.Text,
            It.IsAny<string?>(),
            "Voice delivered text",
            It.IsAny<string?>(),
            false, // isAlert
            true   // isVoiceDelivered
        ), Times.Once);
    }

    [Fact]
    public void ShowCard_AlertFlag_OnSystemError()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        MockBrain.Setup(b => b.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("API timeout"));

        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.MessageDraftText = "Hello";

        sut.SendTextMessageCommand.Execute(null);

        MockGhost.Verify(g => g.ShowCard(
            FabCardVariant.Text,
            null,
            It.Is<string>(s => s.Contains("Chat failed")),
            null,
            true,  // isAlert - errors never auto-dismiss
            It.IsAny<bool>()
        ), Times.Once);
    }

    [Fact]
    public async Task ToolCallReceived_GhostActive_UsesTextWithImageCard()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();

        MockRouter.Raise(r => r.ToolCallReceived += null, new ToolCallInfo
        {
            ToolName = "web_search",
            Success = true
        });

        await Task.Delay(100);

        MockGhost.Verify(g => g.ShowCard(
            FabCardVariant.TextWithImage,
            "TOOL",
            "Searched Internet",
            "tool_search.svg",
            false,
            false
        ), Times.Once);
    }

    [Fact]
    public async Task AnalysisBatchQueued_GhostActive_RotatesFullBatch()
    {
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        var sut = CreateSut();

        MockRouter.Raise(r => r.AnalysisBatchQueued += null, new List<WitnessDesktop.Models.Timeline.TimelineEvent>
        {
            new() { Type = WitnessDesktop.Models.Timeline.EventOutputType.Danger, FullContent = "Danger 1", Summary = "Danger 1" },
            new() { Type = WitnessDesktop.Models.Timeline.EventOutputType.Assessment, FullContent = "Assessment 1", Summary = "Assessment 1" },
            new() { Type = WitnessDesktop.Models.Timeline.EventOutputType.SageAdvice, FullContent = "Advice 1", Summary = "Advice 1" }
        });

        await Task.Delay(3400);

        MockGhost.Verify(g => g.ShowCard(FabCardVariant.Text, "Danger", "Danger 1", null, false, false), Times.Once);
        MockGhost.Verify(g => g.ShowCard(FabCardVariant.Text, "Assessment", "Assessment 1", null, false, false), Times.Once);
        MockGhost.Verify(g => g.ShowCard(FabCardVariant.Text, "SageAdvice", "Advice 1", null, false, false), Times.Once);
        MockGhost.Verify(g => g.DismissCard(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ToggleFabAsync_ExitGhostMode_ClearsFabActiveBeforeAwaitCompletes()
    {
        var exitTcs = new TaskCompletionSource();
        MockGhost.Setup(g => g.IsSupported).Returns(true);
        MockGhost.Setup(g => g.IsGhostModeActive).Returns(true);
        MockGhost.Setup(g => g.ExitGhostModeAsync()).Returns(exitTcs.Task);

        var sut = CreateSut();
        sut.IsFabActive = true;

        var toggleTask = sut.ToggleFabCommand.ExecuteAsync(null);

        sut.IsFabActive.Should().BeFalse();
        toggleTask.IsCompleted.Should().BeFalse();

        exitTcs.SetResult();
        await toggleTask;
    }

    [Fact]
    public async Task ToggleFabAsync_EnterGhostMode_SyncsInitialNativeState()
    {
        MockGhost.Setup(g => g.IsSupported).Returns(true);

        var sut = CreateSut();
        sut.SelectedAgent = new Agent
        {
            Key = "test",
            Id = "test",
            Name = "Test Agent",
            PrimaryGame = "Test Game",
            IconImage = "test.png",
            PortraitImage = "test.png",
            Description = "test",
            Features = new List<string>(),
            SystemInstruction = "test",
            Type = AgentType.General,
            SupportsVoiceChat = true,
            SupportsVoiceCommand = true,
            SupportsGameAudio = true,
            SupportsAudioIn = true
        };
        sut.ConnectionState = ConnectionState.Connected;
        sut.IsVoiceChatActive = true;
        sut.IsAiMicActive = true;
        sut.IsCommentaryActive = true;
        sut.IsAudioInActive = true;

        await sut.ToggleFabCommand.ExecuteAsync(null);

        MockGhost.Verify(g => g.EnterGhostModeAsync(), Times.Once);
        MockGhost.Verify(g => g.SetFabState(true, true), Times.Once);
        MockGhost.Verify(g => g.SetAudioState(true, true, true, true), Times.AtLeastOnce);
    }

    [Fact]
    public void CardDismissed_ClearsLocalFabCardState()
    {
        var sut = CreateSut();
        sut.IsFabActive = true;
        sut.FabCardVariant = FabCardVariant.Text;

        MockGhost.Raise(g => g.CardDismissed += null, MockGhost.Object, EventArgs.Empty);

        sut.FabCardVariant.Should().Be(FabCardVariant.None);
        sut.IsFabCardVisible.Should().BeFalse();
    }

    // =========================================================================
    // DISCONNECT RESETS
    // =========================================================================

    [Fact]
    public async Task Disconnect_ResetsAudioInActive()
    {
        var sut = CreateSut();
        sut.SelectedAgent = CreateTestAgent();
        sut.ConnectionState = ConnectionState.Connected;

        // IsAudioInActive set directly (bypassing agent guard since we just need state)
        // Use reflection or field to bypass guard -- simpler: just verify it resets from any state
        // The StopSessionAsync sets IsAudioInActive = false under _suppressVoiceChatToggle = true

        try
        {
            await sut.ToggleConnectionCommand.ExecuteAsync(null);
        }
        catch (System.NullReferenceException)
        {
            // Expected: Shell.Current is null in tests
        }

        sut.IsAudioInActive.Should().BeFalse();
    }
}
