using System.ComponentModel;
using System.Runtime.CompilerServices;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settings;
    private readonly ILocalModelRuntime _localRuntime;
    private readonly IBargeInPolicyService _bargeInPolicy;

    private string _localRuntimeStatus = "Unknown";
    private string _brainProvider = "Cloud";
    private string _voiceProviderDisplay = "Cloud";
    private string _fallbackReason = "";
    private bool _isFallbackActive;
    private string _sttStatus = "Unknown";
    private string _ttsStatus = "Unknown";

    /// <summary>
    /// No SettingChanged/PolicyChanged subscriptions needed — Settings page refreshes
    /// via RefreshDiagnosticsAsync on OnAppearing. No background process mutates these
    /// values while the user is viewing the page.
    /// </summary>
    public SettingsViewModel(ISettingsService settings, ILocalModelRuntime localRuntime, IBargeInPolicyService bargeInPolicy)
    {
        _settings = settings;
        _localRuntime = localRuntime;
        _bargeInPolicy = bargeInPolicy;
    }

    public string VoiceProvider
    {
        get => _settings.VoiceProvider;
        set
        {
            if (_settings.VoiceProvider == value) return;
            _settings.VoiceProvider = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGeminiSelected));
            OnPropertyChanged(nameof(IsOpenAiSelected));
            OnPropertyChanged(nameof(CurrentVoiceName));
        }
    }

    public string VoiceGender
    {
        get => _settings.VoiceGender;
        set
        {
            if (_settings.VoiceGender == value) return;
            _settings.VoiceGender = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMaleSelected));
            OnPropertyChanged(nameof(IsFemaleSelected));
            OnPropertyChanged(nameof(CurrentVoiceName));
            OnPropertyChanged(nameof(VoiceGenderDisplay));
        }
    }

    public InferenceMode InferenceMode
    {
        get => _settings.InferenceMode;
        set
        {
            if (_settings.InferenceMode == value) return;
            _settings.InferenceMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCloudOnlySelected));
            OnPropertyChanged(nameof(IsLocalOnlySelected));
            OnPropertyChanged(nameof(IsLocalFirstSelected));
        }
    }

    public bool IsGeminiSelected => VoiceProvider == "gemini";
    public bool IsOpenAiSelected => VoiceProvider == "openai";
    public bool IsMaleSelected => VoiceGender == "male";
    public bool IsFemaleSelected => VoiceGender == "female";

    public bool IsCloudOnlySelected => InferenceMode == InferenceMode.CloudOnly;
    public bool IsLocalOnlySelected => InferenceMode == InferenceMode.LocalOnly;
    public bool IsLocalFirstSelected => InferenceMode == InferenceMode.LocalFirst;

    public string CurrentVoiceName => VoiceConfig.GetVoiceName(VoiceProvider, VoiceGender);

    public string VoiceGenderDisplay => VoiceGender switch
    {
        "male" => "Male",
        "female" => "Female",
        _ => VoiceGender
    };

    public bool IsBargeInEnabled
    {
        get => _bargeInPolicy.IsBargeInEnabled;
        set
        {
            if (_bargeInPolicy.IsBargeInEnabled == value) return;
            _bargeInPolicy.SetEnabled(value);
            OnPropertyChanged();
        }
    }

    public string ClaudeCliPath
    {
        get => _settings.ClaudeCliPath;
        set
        {
            if (_settings.ClaudeCliPath == value) return;
            _settings.ClaudeCliPath = value;
            OnPropertyChanged();
        }
    }

    public string BunPath
    {
        get => _settings.BunPath;
        set
        {
            if (_settings.BunPath == value) return;
            _settings.BunPath = value;
            OnPropertyChanged();
        }
    }

    public bool TeamAutoLaunch
    {
        get => _settings.TeamAutoLaunch;
        set
        {
            if (_settings.TeamAutoLaunch == value) return;
            _settings.TeamAutoLaunch = value;
            OnPropertyChanged();
        }
    }

    public string PluginDirPath
    {
        get => _settings.PluginDirPath;
        set
        {
            if (_settings.PluginDirPath == value) return;
            _settings.PluginDirPath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Only set via hard-coded tap handlers (SelectPermission) — no free-text input.
    /// ClaudeProcessManager falls back to "default" for unknown values.</summary>
    public string TeamPermissionMode
    {
        get => _settings.TeamPermissionMode;
        set
        {
            if (_settings.TeamPermissionMode == value) return;
            _settings.TeamPermissionMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PermissionModeDisplay));
        }
    }

    public List<string> PermissionModeOptions { get; } = new()
    {
        "default", "acceptEdits", "auto", "bypassPermissions", "plan"
    };

    /// <summary>Human-readable permission mode descriptions for settings UI.</summary>
    public static readonly List<PermissionModeOption> PermissionModeDisplayOptions = new()
    {
        new("default",             "Ask for everything",  "Claude asks before every action"),
        new("acceptEdits",         "Auto-approve edits",  "Claude edits freely, asks for other actions"),
        new("plan",                "Plan only",           "Claude plans but waits for your approval to act"),
        new("auto",                "Auto-approve most",   "Claude acts freely on most tasks"),
        new("bypassPermissions",   "Full autonomy",       "Claude acts without asking"),
    };

    public string PermissionModeDisplay =>
        PermissionModeDisplayOptions.FirstOrDefault(o => o.Key == TeamPermissionMode)?.Label
        ?? "Ask for everything";

    public string BrainModel => "Gemini 2.5 Flash";
    public string CaptureRate => "On every board change + on demand";
    /// <summary>Informational only — runtime provider selection happens in ConversationProviderFactory
    /// based on env vars, API keys, and agent config. This reflects the persisted preference, not
    /// the active provider. Will need runtime injection when Settings becomes editable.</summary>
    public string VoiceEngine => IsGeminiSelected ? "Gemini Live" : "OpenAI Realtime";
    // TODO: read from assembly/package version at runtime
    public string AppVersion => "1.0.0-alpha";

    public string LocalRuntimeStatus
    {
        get => _localRuntimeStatus;
        private set { if (_localRuntimeStatus != value) { _localRuntimeStatus = value; OnPropertyChanged(); } }
    }

    public string BrainProvider
    {
        get => _brainProvider;
        private set { if (_brainProvider != value) { _brainProvider = value; OnPropertyChanged(); } }
    }

    public string VoiceProviderDisplay
    {
        get => _voiceProviderDisplay;
        private set { if (_voiceProviderDisplay != value) { _voiceProviderDisplay = value; OnPropertyChanged(); } }
    }

    public string FallbackReason
    {
        get => _fallbackReason;
        private set { if (_fallbackReason != value) { _fallbackReason = value; OnPropertyChanged(); } }
    }

    public bool IsFallbackActive
    {
        get => _isFallbackActive;
        private set { if (_isFallbackActive != value) { _isFallbackActive = value; OnPropertyChanged(); } }
    }

    public string SttStatus
    {
        get => _sttStatus;
        private set { if (_sttStatus != value) { _sttStatus = value; OnPropertyChanged(); } }
    }

    public string TtsStatus
    {
        get => _ttsStatus;
        private set { if (_ttsStatus != value) { _ttsStatus = value; OnPropertyChanged(); } }
    }

    public async Task RefreshDiagnosticsAsync()
    {
        try
        {
            var health = await _localRuntime.GetHealthAsync();
            LocalRuntimeStatus = health.RuntimeAvailable ? "Available" : "Unavailable";

            if (!health.RuntimeAvailable && !string.IsNullOrEmpty(health.FailureReason))
            {
                LocalRuntimeStatus = $"Unavailable — {health.FailureReason}";
            }

            // Speech capability diagnostics
            SttStatus = health.SpeechInputAvailable
                ? $"Available ({health.SttEngineName ?? "Unknown"})"
                : "Unavailable";
            TtsStatus = health.SpeechOutputAvailable
                ? $"Available ({health.TtsEngineName ?? "Unknown"})"
                : "Unavailable";

            // Cloud-first UX: always present cloud diagnostics regardless of
            // persisted inference mode. Local modes are not active in this build
            // and their UI toggles are hidden — prevent stale persisted state
            // from leaking local/fallback messaging into visible diagnostics.
            BrainProvider = "Cloud";
            VoiceProviderDisplay = "Cloud";
            IsFallbackActive = false;
            FallbackReason = "";
        }
        catch (Exception ex)
        {
            LocalRuntimeStatus = $"Error — {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public record PermissionModeOption(string Key, string Label, string Description);
