using System.ComponentModel;
using System.Runtime.CompilerServices;
using GaimerDesktop.Models;
using GaimerDesktop.Services;

namespace GaimerDesktop.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settings;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
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
        }
    }

    public bool IsGeminiSelected => VoiceProvider == "gemini";
    public bool IsOpenAiSelected => VoiceProvider == "openai";
    public bool IsMaleSelected => VoiceGender == "male";
    public bool IsFemaleSelected => VoiceGender == "female";

    public string CurrentVoiceName => VoiceConfig.GetVoiceName(VoiceProvider, VoiceGender);

    public string BrainModel => "Claude Sonnet 4";
    public string CaptureRate => "Every 30s + on every move";
    public string VoiceEngine => IsGeminiSelected ? "Gemini Live" : "OpenAI Realtime";
    public string AppVersion => "1.0.0-alpha";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
