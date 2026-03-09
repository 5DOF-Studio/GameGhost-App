using Microsoft.Maui.Controls.Shapes;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Auth;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.ViewModels;

namespace GaimerDesktop.Views;

public partial class OnboardingPage : ContentPage
{
    private OnboardingViewModel? _vm;

    // Tool color map: (background, text, border)
    private static readonly Dictionary<string, (string bg, string text, string border)> ToolColors = new()
    {
        ["capture_screen"]             = ("#0d1a2a", "#4ea3ff", "#1a3050"), // blue
        ["analyze_position_engine"]    = ("#1a1a0d", "#d4a843", "#2a2a1a"), // amber
        ["analyze_position_strategic"] = ("#0d1a14", "#4ade80", "#1a2a22"), // green
        ["get_game_state"]             = ("#0d1a1a", "#22d3ee", "#1a2a2a"), // cyan
        ["web_search"]                 = ("#1a0d1a", "#c084fc", "#2a1a2a"), // purple
    };

    private static readonly (string bg, string text, string border) DefaultToolColor = ("#12151a", "#8a8f98", "#2a2d32");

    public OnboardingPage()
    {
        InitializeComponent();

        // Strip native Entry chrome (inner border/background) on all platforms
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, view) =>
        {
#if MACCATALYST || IOS
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
#elif WINDOWS
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = null;
#endif
        });

        var services = Application.Current?.Handler?.MauiContext?.Services;
        _vm = services != null
            ? new OnboardingViewModel(
                services.GetRequiredService<IAuthService>(),
                services.GetRequiredService<IStockfishService>(),
                services.GetService<ISettingsService>())
            : new OnboardingViewModel();

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        BindingContext = _vm;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OnboardingViewModel.CurrentAgent)
            or nameof(OnboardingViewModel.State)
            or nameof(OnboardingViewModel.IsCurrentAgentAvailable))
        {
            UpdateAgentDisplay();
        }
    }

    private void UpdateAgentDisplay()
    {
        if (_vm is null) return;

        var agent = _vm.CurrentAgent;
        var state = _vm.State;

        // Update portrait image for non-SignIn states
        if (state != OnboardingState.SignIn)
            AgentPortrait.Source = agent.PortraitImage;

        // Chevron tint: dim at boundaries
        var count = _vm.AgentCount;
        var idx = _vm.CurrentAgentIndex;
        LeftChevronLabel.TextColor = idx == 0
            ? Color.FromArgb("#3a3d42")
            : Color.FromArgb("#e0e0e0");
        RightChevronLabel.TextColor = idx == count - 1
            ? Color.FromArgb("#3a3d42")
            : Color.FromArgb("#e0e0e0");

        // Show/hide panels based on availability and state
        bool isBrowsing = state == OnboardingState.AgentBrowse;
        AgentDetailsPanel.IsVisible = isBrowsing && agent.IsAvailable;
        ComingSoonPanel.IsVisible = isBrowsing && !agent.IsAvailable;
        ComingSoonLeftOverlay.IsVisible = isBrowsing && !agent.IsAvailable;

        if (isBrowsing && agent.IsAvailable)
            PopulateAvailableAgent(agent);
        else if (isBrowsing && !agent.IsAvailable)
            PopulateComingSoonAgent(agent);

        // Download/Ready labels
        if (state == OnboardingState.Downloading)
            DownloadAgentNameLabel.Text = agent.Id.ToUpperInvariant();

        if (state == OnboardingState.Ready)
        {
            ReadyAgentNameLabel.Text = agent.Name.ToUpperInvariant();
            ReadySpecLabel.Text = agent.Id;
        }
    }

    private void PopulateAvailableAgent(Agent agent)
    {
        // Show chess badge for chess agents, hide for others
        AgentBadgeImage.IsVisible = agent.Type == AgentType.Chess;
        AgentNameLabel.Text = agent.Name.ToUpperInvariant();
        AgentSpecLabel.Text = $"{agent.PrimaryGame} Specialist";
        AgentDescLabel.Text = agent.Description;
        CaptureRateLabel.Text = agent.CaptureInfo ?? "Every 30s + on every move";

        RequirementsLabel.Text = agent.Type == AgentType.Chess
            ? "Storage: ~75 MB\nRAM: 256 MB\nmacOS / Windows"
            : "Storage: ~10 MB\nRAM: 128 MB\nmacOS / Windows";

        // Populate colored tool tags
        ToolsLayout.Children.Clear();
        var tools = agent.Tools ?? agent.Features;
        if (tools != null)
        {
            foreach (var tool in tools)
            {
                var colors = ToolColors.GetValueOrDefault(tool, DefaultToolColor);
                var chip = new Border
                {
                    BackgroundColor = Color.FromArgb(colors.bg),
                    Stroke = Color.FromArgb(colors.border),
                    StrokeThickness = 1,
                    Padding = new Thickness(12, 6),
                    Margin = new Thickness(0, 0, 8, 8),
                    StrokeShape = new RoundRectangle { CornerRadius = 6 }
                };
                chip.Content = new Label
                {
                    Text = FormatToolName(tool),
                    FontFamily = "RajdhaniSemiBold",
                    FontSize = 13,
                    TextColor = Color.FromArgb(colors.text)
                };
                ToolsLayout.Children.Add(chip);
            }
        }
    }

    private void PopulateComingSoonAgent(Agent agent)
    {
        ComingSoonNameLabel.Text = agent.Name.ToUpperInvariant();
        ComingSoonSpecLabel.Text = $"{agent.PrimaryGame} Specialist";
        ComingSoonDescLabel.Text = agent.Description;
    }

    private static string FormatToolName(string toolId)
    {
        return toolId switch
        {
            "capture_screen" => "Screen Capture",
            "analyze_position_engine" => "Chess Engine",
            "analyze_position_strategic" => "Chess Brain",
            "get_game_state" => "Game State",
            "web_search" => "Internet Search",
            _ => toolId.Replace("_", " ")
        };
    }

    // ── Event Handlers ───────────────────────────────────────────────────

    private void OnSignInClicked(object? sender, TappedEventArgs e)
    {
        _vm?.SignInCommand.Execute(null);
    }

    private void OnPreviousAgent(object? sender, TappedEventArgs e)
    {
        _vm?.PreviousAgentCommand.Execute(null);
    }

    private void OnNextAgent(object? sender, TappedEventArgs e)
    {
        _vm?.NextAgentCommand.Execute(null);
    }

    private void OnDownloadClicked(object? sender, TappedEventArgs e)
    {
        _vm?.DownloadCommand.Execute(null);
    }

    private async void OnConnectClicked(object? sender, TappedEventArgs e)
    {
        if (_vm is null) return;

        // Phase 1: Flip out (scale down on Y axis to simulate 3D flip)
        await ConnectButton.ScaleYTo(0, 150, Easing.CubicIn);

        // Phase 2: Swap content — dark background, show "CONNECTING..."
        ConnectButton.BackgroundColor = Color.FromArgb("#1a1d21");
        ConnectDefaultContent.IsVisible = false;
        ConnectingLabel.IsVisible = true;

        // Phase 3: Flip back in
        await ConnectButton.ScaleYTo(1, 150, Easing.CubicOut);

        // Phase 4: Execute the actual connect command
        _vm.ConnectCommand.Execute(null);
    }
}
