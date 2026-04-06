using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// Compares the live settings against the settings that were active when the app last bootstrapped.
/// </summary>
public sealed class StructuralSettingsTracker : IStructuralSettingsTracker, IDisposable
{
    private readonly ISettingsService _settings;
    private string[] _pendingSettings = [];

    public StructuralSettingsTracker(ISettingsService settings)
    {
        _settings = settings;
        AppliedSettings = CaptureCurrentSettings();
        _settings.SettingChanged += OnSettingChanged;
    }

    public bool RequiresRebootstrap => _pendingSettings.Length > 0;

    public BootstrapSettingsSnapshot AppliedSettings { get; private set; }

    public IReadOnlyList<string> PendingSettings => _pendingSettings;

    public event EventHandler? StateChanged;

    public bool IsStructuralSetting(string settingName)
    {
        return string.Equals(settingName, nameof(ISettingsService.InferenceMode), StringComparison.Ordinal) ||
               string.Equals(settingName, nameof(ISettingsService.VoiceProvider), StringComparison.Ordinal);
    }

    public void MarkCurrentSettingsApplied()
    {
        AppliedSettings = CaptureCurrentSettings();
        RecomputePendingSettings();
    }

    public void Dispose()
    {
        _settings.SettingChanged -= OnSettingChanged;
    }

    private void OnSettingChanged(object? sender, string settingName)
    {
        if (!IsStructuralSetting(settingName))
        {
            return;
        }

        RecomputePendingSettings();
    }

    private BootstrapSettingsSnapshot CaptureCurrentSettings()
    {
        return new BootstrapSettingsSnapshot(
            _settings.InferenceMode,
            _settings.VoiceProvider);
    }

    private void RecomputePendingSettings()
    {
        var previousPending = _pendingSettings;
        var current = CaptureCurrentSettings();
        var pending = new List<string>(capacity: 2);

        if (current.InferenceMode != AppliedSettings.InferenceMode)
        {
            pending.Add(nameof(ISettingsService.InferenceMode));
        }

        if (!string.Equals(current.VoiceProvider, AppliedSettings.VoiceProvider, StringComparison.OrdinalIgnoreCase))
        {
            pending.Add(nameof(ISettingsService.VoiceProvider));
        }

        _pendingSettings = [.. pending];

        if (!previousPending.SequenceEqual(_pendingSettings, StringComparer.Ordinal))
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
