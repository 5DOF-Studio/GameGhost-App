using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Views;

public partial class AgentSelectionPage : ContentPage
{
    private int _currentIndex = 0;
    private const int TotalAgents = 2;

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
        
        // Update chevron colors: white = has content in that direction, gray = at boundary
        // Left chevron: gray when at first (0), white when can go back
        LeftChevronLabel.TextColor = _currentIndex == 0 ? Color.FromArgb("#555555") : Color.FromArgb("#ffffff");
        
        // Right chevron: gray when at last (TotalAgents-1), white when can go forward
        RightChevronLabel.TextColor = _currentIndex == TotalAgents - 1 ? Color.FromArgb("#555555") : Color.FromArgb("#ffffff");
    }
}
