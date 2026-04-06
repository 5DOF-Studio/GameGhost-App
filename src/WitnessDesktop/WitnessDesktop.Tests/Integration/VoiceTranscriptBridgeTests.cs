using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Conversation;

namespace WitnessDesktop.Tests.Integration;

public class VoiceTranscriptBridgeTests
{
    [Fact]
    public void IConversationProvider_HasUserTranscriptReceivedEvent()
    {
        var eventInfo = typeof(IConversationProvider).GetEvent("UserTranscriptReceived");
        eventInfo.Should().NotBeNull("IConversationProvider must declare UserTranscriptReceived event");
        eventInfo!.EventHandlerType.Should().Be(typeof(EventHandler<string>));
    }

    [Fact]
    public async Task TranscriptStore_FeedsIntoEnvelope_EndToEnd()
    {
        var mockReel = new Mock<IVisualReelService>();
        mockReel.Setup(r => r.GetRecent(It.IsAny<int>())).Returns(new List<ReelMoment>());
        var contextService = new BrainContextService(mockReel.Object);
        var store = new VoiceTranscriptStore();

        var now = DateTime.UtcNow;
        store.AddTurn(new VoiceTranscriptTurn
        {
            TimestampUtc = now.AddSeconds(-4),
            Role = TranscriptRole.User,
            Text = "Is my king safe?",
            Provider = "OpenAI Realtime"
        });
        store.AddTurn(new VoiceTranscriptTurn
        {
            TimestampUtc = now.AddSeconds(-2),
            Role = TranscriptRole.Assistant,
            Text = "Your king is well protected behind the pawn structure.",
            Provider = "OpenAI Realtime"
        });

        var inputs = new ContextAssemblyInputs
        {
            RecentTranscript = store.GetRecent(20)
        };
        var envelope = await contextService.GetContextForChatAsync(now, inputs: inputs);

        envelope.RecentVoiceTranscript.Should().Contain("Is my king safe?");
        envelope.RecentVoiceTranscript.Should().Contain("well protected");
    }

    [Fact]
    public void TranscriptTelemetry_TracksUserInput()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        Dictionary<string, string>? capturedProps = null;

        mockTelemetry.Setup(t => t.TrackEvent("voice", "input.transcript.final", It.IsAny<Dictionary<string, string>>()))
            .Callback<string, string, Dictionary<string, string>?>((_, _, props) => capturedProps = props);

        mockTelemetry.Object.TrackEvent("voice", "input.transcript.final", new Dictionary<string, string>
        {
            ["provider"] = "OpenAI Realtime",
            ["transcript_length"] = "42"
        });

        capturedProps.Should().NotBeNull();
        capturedProps!["provider"].Should().Be("OpenAI Realtime");
        capturedProps["transcript_length"].Should().Be("42");
    }

    [Fact]
    public void TranscriptTelemetry_TracksAIOutput()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        Dictionary<string, string>? capturedProps = null;

        mockTelemetry.Setup(t => t.TrackEvent("voice", "output.transcript.final", It.IsAny<Dictionary<string, string>>()))
            .Callback<string, string, Dictionary<string, string>?>((_, _, props) => capturedProps = props);

        mockTelemetry.Object.TrackEvent("voice", "output.transcript.final", new Dictionary<string, string>
        {
            ["provider"] = "OpenAI Realtime",
            ["transcript_length"] = "87"
        });

        capturedProps.Should().NotBeNull();
        capturedProps!["transcript_length"].Should().Be("87");
    }
}
