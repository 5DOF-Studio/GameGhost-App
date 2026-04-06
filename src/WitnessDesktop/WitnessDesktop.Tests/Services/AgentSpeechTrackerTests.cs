using FluentAssertions;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Tests.Services;

public class AgentSpeechTrackerTests
{
    [Fact]
    public void InitialState_IsNotSpeaking()
    {
        var sut = new AgentSpeechTracker();
        sut.IsSpeaking.Should().BeFalse();
    }

    [Fact]
    public void OnAudioReceived_TransitionsToSpeaking()
    {
        var sut = new AgentSpeechTracker();
        sut.OnAudioReceived();
        sut.IsSpeaking.Should().BeTrue();
    }

    [Fact]
    public void OnAudioReceived_FiresSpeakingStartedOnce()
    {
        var sut = new AgentSpeechTracker();
        var fired = 0;
        sut.SpeakingStarted += (_, _) => fired++;
        sut.OnAudioReceived();
        sut.OnAudioReceived();
        fired.Should().Be(1);
    }

    [Fact]
    public void AfterSilenceGap_TransitionsToNotSpeaking()
    {
        var sut = new AgentSpeechTracker(silenceGap: TimeSpan.FromMilliseconds(50));
        sut.OnAudioReceived();
        sut.IsSpeaking.Should().BeTrue();
        Thread.Sleep(100);
        sut.IsSpeaking.Should().BeFalse();
    }

    [Fact]
    public void AfterSilenceGap_FiresSpeakingStopped()
    {
        var sut = new AgentSpeechTracker(silenceGap: TimeSpan.FromMilliseconds(50));
        var stopped = false;
        sut.SpeakingStopped += (_, _) => stopped = true;
        sut.OnAudioReceived();
        Thread.Sleep(100);
        stopped.Should().BeTrue();
    }

    [Fact]
    public void ContinuousAudio_StaysSpeaking()
    {
        var sut = new AgentSpeechTracker(silenceGap: TimeSpan.FromMilliseconds(100));
        sut.OnAudioReceived();
        Thread.Sleep(50);
        sut.OnAudioReceived();
        Thread.Sleep(50);
        sut.IsSpeaking.Should().BeTrue();
    }

    [Fact]
    public void Reset_StopsTrackingImmediately()
    {
        var sut = new AgentSpeechTracker();
        sut.OnAudioReceived();
        sut.Reset();
        sut.IsSpeaking.Should().BeFalse();
    }

    [Fact]
    public void Reset_WhenSpeaking_FiresSpeakingStopped()
    {
        var sut = new AgentSpeechTracker();
        var stopped = false;
        sut.SpeakingStopped += (_, _) => stopped = true;
        sut.OnAudioReceived();
        sut.Reset();
        stopped.Should().BeTrue();
    }

    [Fact]
    public void Reset_WhenNotSpeaking_DoesNotFireSpeakingStopped()
    {
        var sut = new AgentSpeechTracker();
        var stopped = false;
        sut.SpeakingStopped += (_, _) => stopped = true;
        sut.Reset();
        stopped.Should().BeFalse();
    }

    [Fact]
    public void Dispose_StopsTimer()
    {
        var sut = new AgentSpeechTracker(silenceGap: TimeSpan.FromMilliseconds(50));
        sut.OnAudioReceived();
        sut.Dispose();
        Thread.Sleep(100);
    }
}
