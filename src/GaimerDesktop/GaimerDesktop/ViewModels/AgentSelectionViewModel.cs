using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Dispatching;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Chess;

namespace GaimerDesktop.ViewModels;

public partial class AgentSelectionViewModel : ObservableObject
{
    private readonly IStockfishService? _stockfishService;

    [ObservableProperty]
    private IReadOnlyList<Agent> _availableAgents;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _showDownloadOverlay;

    [ObservableProperty]
    private string? _downloadError;

    /// <summary>Pending agent key while download overlay is shown.</summary>
    private string? _pendingAgentKey;

    public bool IsStockfishInstalled => _stockfishService?.IsInstalled ?? false;

    public AgentSelectionViewModel() : this(null) { }

    public AgentSelectionViewModel(IStockfishService? stockfishService)
    {
        _stockfishService = stockfishService;
        _availableAgents = Models.Agents.Available;
    }

    [RelayCommand]
    private async Task SelectAgentAsync(string? agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            return;

        var agent = Agents.GetByKey(agentId);
        if (agent == null)
            return;

        // Set voice gender based on the selected agent
        var settings = Application.Current?.Handler?.MauiContext?.Services.GetService<ISettingsService>();
        if (settings != null)
        {
            settings.VoiceGender = agent.VoiceGender;
            Console.WriteLine($"[AgentSelection] Set voice gender to '{agent.VoiceGender}' for agent '{agent.Name}'");
        }

        // Chess agent: check if Stockfish needs downloading
        if (agent.Type == AgentType.Chess && _stockfishService != null && !_stockfishService.IsInstalled)
        {
            _pendingAgentKey = agentId;
            ShowDownloadOverlay = true;
            DownloadError = null;
            return;
        }

        // Start Stockfish engine if chess agent and installed
        if (agent.Type == AgentType.Chess && _stockfishService is { IsInstalled: true, IsReady: false })
        {
            try
            {
                await _stockfishService.StartAsync();
                Console.WriteLine("[AgentSelection] Stockfish engine started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AgentSelection] Stockfish start failed: {ex.Message}");
            }
        }

        await NavigateToMainPageAsync(agentId);
    }

    [RelayCommand]
    private async Task DownloadStockfishAsync()
    {
        if (_stockfishService == null || IsDownloading)
            return;

        IsDownloading = true;
        DownloadProgress = 0;
        DownloadError = null;

        try
        {
            var progress = new Progress<double>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() => DownloadProgress = p);
            });

            var success = await _stockfishService.EnsureInstalledAsync(progress);
            if (success)
            {
                await _stockfishService.StartAsync();
                Console.WriteLine("[AgentSelection] Stockfish downloaded and started");
                ShowDownloadOverlay = false;

                if (_pendingAgentKey != null)
                    await NavigateToMainPageAsync(_pendingAgentKey);
            }
            else
            {
                DownloadError = "Download failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            DownloadError = $"Download failed: {ex.Message}";
            Console.WriteLine($"[AgentSelection] Stockfish download error: {ex.Message}");
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private async Task SkipDownloadAsync()
    {
        ShowDownloadOverlay = false;
        if (_pendingAgentKey != null)
            await NavigateToMainPageAsync(_pendingAgentKey);
    }

    private async Task NavigateToMainPageAsync(string agentId)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is null)
                    return;

                await Shell.Current.GoToAsync(nameof(MainPage), new Dictionary<string, object>
                {
                    ["agentId"] = agentId
                });
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is not null)
                {
                    await Shell.Current.Navigation.PushAsync(new MainPage
                    {
                        AgentId = agentId
                    });
                }
            });
        }
    }
}
