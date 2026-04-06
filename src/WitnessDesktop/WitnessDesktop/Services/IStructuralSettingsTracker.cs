using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// Tracks settings that cannot be applied through a shallow session reconnect.
/// This is the service-layer seam behind D-028: structural settings require app rebootstrap.
/// </summary>
public interface IStructuralSettingsTracker
{
    bool RequiresRebootstrap { get; }
    BootstrapSettingsSnapshot AppliedSettings { get; }
    IReadOnlyList<string> PendingSettings { get; }

    event EventHandler? StateChanged;

    bool IsStructuralSetting(string settingName);
    void MarkCurrentSettingsApplied();
}

public sealed record BootstrapSettingsSnapshot(
    InferenceMode InferenceMode,
    string VoiceProvider);
