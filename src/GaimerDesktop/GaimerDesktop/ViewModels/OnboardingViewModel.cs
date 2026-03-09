using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Auth;
using GaimerDesktop.Services.Chess;

namespace GaimerDesktop.ViewModels;

public enum OnboardingState
{
    SignIn,
    AgentBrowse,
    Downloading,
    Ready
}

public partial class OnboardingViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IStockfishService? _stockfishService;
    private readonly ISettingsService? _settingsService;

    private readonly IReadOnlyList<Agent> _allAgents = Agents.All;

    // ── Sign-In State ────────────────────────────────────────────────────────

    [ObservableProperty]
    private OnboardingState _state = OnboardingState.SignIn;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private bool _isSigningIn;

    [ObservableProperty]
    private string? _signInError;

    // ── Agent Browse State ───────────────────────────────────────────────────

    [ObservableProperty]
    private int _currentAgentIndex;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string? _downloadStatusText;

    [ObservableProperty]
    private bool _isConnecting;

    public Agent CurrentAgent => _allAgents[CurrentAgentIndex];
    public bool IsCurrentAgentAvailable => CurrentAgent.IsAvailable;
    public int AgentCount => _allAgents.Count;

    // ── Computed UI Properties ───────────────────────────────────────────────

    public bool IsSignInState => State == OnboardingState.SignIn;
    public bool IsAgentBrowseState => State == OnboardingState.AgentBrowse;
    public bool IsDownloadingState => State == OnboardingState.Downloading;
    public bool IsReadyState => State == OnboardingState.Ready;
    public bool ShowChevrons => State == OnboardingState.AgentBrowse;
    public bool ShowAgentImage => State != OnboardingState.SignIn;

    public OnboardingViewModel() : this(new MockAuthService(), null, null) { }

    public OnboardingViewModel(IAuthService authService, IStockfishService? stockfishService, ISettingsService? settingsService)
    {
        _authService = authService;
        _stockfishService = stockfishService;
        _settingsService = settingsService;

        // Start on first available agent (Leroy = index 1)
        _currentAgentIndex = _allAgents.ToList().FindIndex(a => a.IsAvailable);
        if (_currentAgentIndex < 0) _currentAgentIndex = 0;
    }

    partial void OnStateChanged(OnboardingState value)
    {
        OnPropertyChanged(nameof(IsSignInState));
        OnPropertyChanged(nameof(IsAgentBrowseState));
        OnPropertyChanged(nameof(IsDownloadingState));
        OnPropertyChanged(nameof(IsReadyState));
        OnPropertyChanged(nameof(ShowChevrons));
        OnPropertyChanged(nameof(ShowAgentImage));
    }

    partial void OnCurrentAgentIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentAgent));
        OnPropertyChanged(nameof(IsCurrentAgentAvailable));
    }

    // ── Sign-In Command ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Username))
        {
            SignInError = "Please enter both username and email.";
            return;
        }

        IsSigningIn = true;
        SignInError = null;

        try
        {
            var result = await _authService.SignInWithEmailAsync(Email.Trim(), Username.Trim());

            if (result.Authorized)
            {
                // Fetch and inject API keys (production flow)
                var keys = await _authService.FetchApiKeysAsync();
                if (keys is not null)
                {
                    if (!string.IsNullOrEmpty(keys.GeminiKey))
                        Environment.SetEnvironmentVariable("GEMINI_APIKEY", keys.GeminiKey);
                    if (!string.IsNullOrEmpty(keys.OpenAiKey))
                        Environment.SetEnvironmentVariable("OPENAI_APIKEY", keys.OpenAiKey);
                    if (!string.IsNullOrEmpty(keys.OpenRouterKey))
                        Environment.SetEnvironmentVariable("OPENROUTER_APIKEY", keys.OpenRouterKey);

                    System.Diagnostics.Debug.WriteLine("[Onboarding] API keys injected from auth");
                }

                State = OnboardingState.AgentBrowse;
            }
            else
            {
                SignInError = result.Reason ?? "Sign-in failed. Check your invite.";
            }
        }
        catch (Exception ex)
        {
            SignInError = "Connection error. Please try again.";
            System.Diagnostics.Debug.WriteLine($"[Onboarding] Sign-in error: {ex.Message}");
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    // ── Agent Navigation ─────────────────────────────────────────────────────

    [RelayCommand]
    private void PreviousAgent()
    {
        CurrentAgentIndex = (CurrentAgentIndex - 1 + _allAgents.Count) % _allAgents.Count;
    }

    [RelayCommand]
    private void NextAgent()
    {
        CurrentAgentIndex = (CurrentAgentIndex + 1) % _allAgents.Count;
    }

    // ── Download Command ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DownloadAsync()
    {
        State = OnboardingState.Downloading;
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatusText = "Preparing...";

        try
        {
            if (_stockfishService is null || _stockfishService.IsInstalled)
            {
                // Fake download animation — consistent onboarding experience
                DownloadStatusText = "Installing chess tools...";
                for (int i = 0; i <= 100; i += 2)
                {
                    DownloadProgress = i / 100.0;
                    await Task.Delay(50);
                }
            }
            else
            {
                // Real download
                DownloadStatusText = "Downloading chess engine...";
                var progress = new Progress<double>(p =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DownloadProgress = p;
                        DownloadStatusText = p < 1.0
                            ? $"Downloading... {p:P0}"
                            : "Installing...";
                    });
                });

                var success = await _stockfishService.EnsureInstalledAsync(progress);
                if (!success)
                {
                    DownloadStatusText = "Download failed. Please try again.";
                    State = OnboardingState.AgentBrowse;
                    return;
                }
            }

            // Start Stockfish engine if available (non-fatal — engine can start later)
            if (_stockfishService is { IsInstalled: true, IsReady: false })
            {
                try
                {
                    DownloadStatusText = "Starting engine...";
                    await _stockfishService.StartAsync();
                }
                catch (Exception startEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[Onboarding] Engine start deferred: {startEx.Message}");
                }
            }

            DownloadStatusText = null;
            State = OnboardingState.Ready;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Onboarding] Download error: {ex.Message}");
            DownloadStatusText = "Download failed. Please try again.";
            State = OnboardingState.AgentBrowse;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    // ── Connect Command ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ConnectAsync()
    {
        IsConnecting = true;
        var agent = CurrentAgent;

        // Set voice gender
        if (_settingsService != null)
            _settingsService.VoiceGender = agent.VoiceGender;

        // Set username on agent
        agent.UserId = _authService.UserName;

        // Brief delay for flip animation to play
        await Task.Delay(1200);

        try
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(nameof(MainPage), new Dictionary<string, object>
                {
                    ["agentId"] = agent.Key
                });
            }
        }
        catch
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.Navigation.PushAsync(new MainPage
                {
                    AgentId = agent.Key
                });
            }
        }
    }
}
