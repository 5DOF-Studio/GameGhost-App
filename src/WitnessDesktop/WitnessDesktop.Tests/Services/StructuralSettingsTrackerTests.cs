using FluentAssertions;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class StructuralSettingsTrackerTests
{
    [Fact]
    public void Constructor_CapturesCurrentSettingsAsAppliedBaseline()
    {
        var settings = new SettingsService
        {
            InferenceMode = InferenceMode.CloudOnly,
            VoiceProvider = "gemini"
        };

        var sut = new StructuralSettingsTracker(settings);

        sut.AppliedSettings.InferenceMode.Should().Be(InferenceMode.CloudOnly);
        sut.AppliedSettings.VoiceProvider.Should().Be("gemini");
        sut.RequiresRebootstrap.Should().BeFalse();
        sut.PendingSettings.Should().BeEmpty();
    }

    [Fact]
    public void InferenceModeChange_MarksRebootstrapRequired()
    {
        var settings = new SettingsService();
        var sut = new StructuralSettingsTracker(settings);

        settings.InferenceMode = InferenceMode.LocalOnly;

        sut.RequiresRebootstrap.Should().BeTrue();
        sut.PendingSettings.Should().ContainSingle(nameof(ISettingsService.InferenceMode));
    }

    [Fact]
    public void VoiceProviderChange_MarksRebootstrapRequired()
    {
        var settings = new SettingsService();
        var sut = new StructuralSettingsTracker(settings);

        settings.VoiceProvider = "openai";

        sut.RequiresRebootstrap.Should().BeTrue();
        sut.PendingSettings.Should().ContainSingle(nameof(ISettingsService.VoiceProvider));
    }

    [Fact]
    public void VoiceGenderChange_DoesNotMarkRebootstrapRequired()
    {
        var settings = new SettingsService();
        var sut = new StructuralSettingsTracker(settings);

        settings.VoiceGender = "female";

        sut.RequiresRebootstrap.Should().BeFalse();
        sut.PendingSettings.Should().BeEmpty();
    }

    [Fact]
    public void MarkCurrentSettingsApplied_ClearsPendingStructuralChanges()
    {
        var settings = new SettingsService();
        var sut = new StructuralSettingsTracker(settings);
        settings.InferenceMode = InferenceMode.LocalFirst;
        settings.VoiceProvider = "openai";

        sut.MarkCurrentSettingsApplied();

        sut.RequiresRebootstrap.Should().BeFalse();
        sut.PendingSettings.Should().BeEmpty();
        sut.AppliedSettings.InferenceMode.Should().Be(InferenceMode.LocalFirst);
        sut.AppliedSettings.VoiceProvider.Should().Be("openai");
    }

    [Fact]
    public void RevertingToAppliedSettings_ClearsPendingWithoutExplicitReset()
    {
        var settings = new SettingsService();
        var sut = new StructuralSettingsTracker(settings);

        settings.InferenceMode = InferenceMode.LocalOnly;
        settings.InferenceMode = InferenceMode.CloudOnly;

        sut.RequiresRebootstrap.Should().BeFalse();
        sut.PendingSettings.Should().BeEmpty();
    }

    [Fact]
    public void StructuralStateChanges_RaiseEventOnlyWhenPendingStateActuallyChanges()
    {
        var settings = new SettingsService();
        var sut = new StructuralSettingsTracker(settings);
        var count = 0;
        sut.StateChanged += (_, _) => count++;

        settings.VoiceGender = "female";
        settings.InferenceMode = InferenceMode.LocalOnly;
        settings.VoiceProvider = "openai";
        settings.VoiceProvider = "openai";
        settings.InferenceMode = InferenceMode.CloudOnly;
        settings.VoiceProvider = "gemini";

        count.Should().Be(4);
    }
}
