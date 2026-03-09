using GaimerDesktop.ViewModels;

namespace GaimerDesktop.Views;

public partial class AgentSelectionPage : ContentPage
{
    private int _currentIndex = 0;
    private const int TotalAgents = 3;

    public AgentSelectionPage()
    {
        InitializeComponent();
        BindingContext = Application.Current?.Handler?.MauiContext?.Services.GetService<AgentSelectionViewModel>()
            ?? new AgentSelectionViewModel();
        UpdateCardVisibility();
    }

    private void OnGeneralSelectClicked(object? sender, EventArgs e)
    {
        if (BindingContext is AgentSelectionViewModel vm)
        {
            vm.SelectAgentCommand.Execute("general");
        }
    }

    private void OnChessSelectClicked(object? sender, EventArgs e)
    {
        if (BindingContext is AgentSelectionViewModel vm)
        {
            vm.SelectAgentCommand.Execute("chess");
        }
    }

    private void OnWaspSelectClicked(object? sender, EventArgs e)
    {
        if (BindingContext is AgentSelectionViewModel vm)
        {
            vm.SelectAgentCommand.Execute("wasp");
        }
    }

    private void OnDownloadClicked(object? sender, TappedEventArgs e)
    {
        if (BindingContext is AgentSelectionViewModel vm)
        {
            vm.DownloadStockfishCommand.Execute(null);
        }
    }

    private void OnSkipDownloadClicked(object? sender, TappedEventArgs e)
    {
        if (BindingContext is AgentSelectionViewModel vm)
        {
            vm.SkipDownloadCommand.Execute(null);
        }
    }

    private void OnPreviousAgent(object? sender, TappedEventArgs e)
    {
        _currentIndex = (_currentIndex - 1 + TotalAgents) % TotalAgents;
        UpdateCardVisibility();
    }

    private void OnNextAgent(object? sender, TappedEventArgs e)
    {
        _currentIndex = (_currentIndex + 1) % TotalAgents;
        UpdateCardVisibility();
    }

    private void UpdateCardVisibility()
    {
        DerekCard.IsVisible = _currentIndex == 0;
        LeroyCard.IsVisible = _currentIndex == 1;
        WaspCard.IsVisible = _currentIndex == 2;

        // Update chevron colors: white = has content in that direction, gray = at boundary
        LeftChevronLabel.TextColor = _currentIndex == 0 ? Color.FromArgb("#555555") : Color.FromArgb("#ffffff");
        RightChevronLabel.TextColor = _currentIndex == TotalAgents - 1 ? Color.FromArgb("#555555") : Color.FromArgb("#ffffff");
    }
}
