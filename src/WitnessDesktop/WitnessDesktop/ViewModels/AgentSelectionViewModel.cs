using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Dispatching;
using WitnessDesktop.Models;

namespace WitnessDesktop.ViewModels;

public partial class AgentSelectionViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<Agent> _availableAgents;

    public AgentSelectionViewModel()
    {
        _availableAgents = Models.Agents.Available;
    }

    [RelayCommand]
    private async Task SelectAgentAsync(string? agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return;
        }

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is null)
                {
                    return;
                }

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

