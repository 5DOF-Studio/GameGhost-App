using FluentAssertions;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Tests.Services;

public class UserSpeechDetectorTests
{
    [Fact]
    public void InitialState_IsNotSpeaking()
    {
        var sut = new UserSpeechDetector();
        sut.IsUserSpeaking.Should().BeFalse();
    }

    [Fact]
    public void OnLevelChanged_AboveThreshold_TransitionsToSpeaking()
    {
        var sut = new UserSpeechDetector(speechThreshold: 0.02f, debounceMs: 0);
        sut.OnLevelChanged(0.05f);
        sut.IsUserSpeaking.Should().BeTrue();
    }

    [Fact]
    public void OnLevelChanged_BelowThreshold_StaysNotSpeaking()
    {
        var sut = new UserSpeechDetector(speechThreshold: 0.02f);
        sut.OnLevelChanged(0.01f);
        sut.IsUserSpeaking.Should().BeFalse();
    }

    [Fact]
    public void OnLevelChanged_AboveThreshold_FiresUserSpeechStarted()
    {
        var sut = new UserSpeechDetector(speechThreshold: 0.02f, debounceMs: 0);
        var fired = false;
        sut.UserSpeechStarted += (_, _) => fired = true;
        sut.OnLevelChanged(0.05f);
        fired.Should().BeTrue();
    }

    [Fact]
    public void OnLevelChanged_AboveThresholdTwice_FiresStartedOnce()
    {
        var sut = new UserSpeechDetector(speechThreshold: 0.02f, debounceMs: 0);
        var count = 0;
        sut.UserSpeechStarted += (_, _) => count++;
        sut.OnLevelChanged(0.05f);
        sut.OnLevelChanged(0.06f);
        count.Should().Be(1);
    }

    [Fact]
    public void OnLevelChanged_DropsBelowThreshold_FiresUserSpeechStopped()
    {
        var sut = new UserSpeechDetector(speechThreshold: 0.02f, debounceMs: 10);
        sut.OnLevelChanged(0.05f);
        var stopped = false;
        sut.UserSpeechStopped += (_, _) => stopped = true;
        sut.OnLevelChanged(0.001f);
        Thread.Sleep(50);
        stopped.Should().BeTrue();
    }

    [Fact]
    public void CurrentLevel_ReflectsLastInput()
    {
        var sut = new UserSpeechDetector();
        sut.OnLevelChanged(0.123f);
        sut.CurrentLevel.Should().BeApproximately(0.123f, 0.001f);
    }
}
