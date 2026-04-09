using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Views;

public partial class SettingsPage : ContentPage
{
    // Permission mode key → (card border, check circle) for checklist UI
    private readonly record struct PermissionRow(string Key, Border Card, Border Check);
    private PermissionRow[] _permissionRows = [];

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Build permission row map after InitializeComponent
        _permissionRows =
        [
            new("default",           PermDefault, PermDefaultCheck),
            new("acceptEdits",       PermEdits,   PermEditsCheck),
            new("plan",              PermPlan,    PermPlanCheck),
            new("auto",              PermAuto,    PermAutoCheck),
            new("bypassPermissions", PermBypass,  PermBypassCheck),
        ];

        if (BindingContext is SettingsViewModel vm)
        {
            await vm.RefreshDiagnosticsAsync();
            ApplyPermissionSelection(vm.TeamPermissionMode);
        }
    }

    // ── Sidebar navigation ─────────────────────────────────

    private void OnNavVision(object? sender, TappedEventArgs e) => SwitchSection("Vision");
    private void OnNavVoice(object? sender, TappedEventArgs e)  => SwitchSection("Voice");
    private void OnNavBrain(object? sender, TappedEventArgs e)  => SwitchSection("Brain");
    private void OnNavTeam(object? sender, TappedEventArgs e)   => SwitchSection("Team");

    private void SwitchSection(string section)
    {
        VisionPanel.IsVisible = section == "Vision";
        VoicePanel.IsVisible  = section == "Voice";
        BrainPanel.IsVisible  = section == "Brain";
        TeamPanel.IsVisible   = section == "Team";

        SetNavState(NavVision, NavVisionLabel, section == "Vision");
        SetNavState(NavVoice,  NavVoiceLabel,  section == "Voice");
        SetNavState(NavBrain,  NavBrainLabel,  section == "Brain");
        SetNavState(NavTeam,   NavTeamLabel,   section == "Team");
    }

    private void SetNavState(Border nav, Label label, bool active)
    {
        nav.BackgroundColor = active
            ? (Color)Resources["NavItemActive"]
            : Colors.Transparent;
        label.TextColor = active
            ? (Color)Resources["TextPrimary"]
            : (Color)Resources["TextSecondary"];
        label.FontFamily = active ? "RajdhaniSemiBold" : "RajdhaniRegular";
    }

    // ── Back navigation ────────────────────────────────────

    private async void OnBackClicked(object? sender, TappedEventArgs e)
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("..");
    }

    // ── Permission mode checklist ──────────────────────────

    private void OnPermDefaultSelected(object? sender, TappedEventArgs e) => SelectPermission("default");
    private void OnPermEditsSelected(object? sender, TappedEventArgs e)   => SelectPermission("acceptEdits");
    private void OnPermPlanSelected(object? sender, TappedEventArgs e)    => SelectPermission("plan");
    private void OnPermAutoSelected(object? sender, TappedEventArgs e)    => SelectPermission("auto");
    private void OnPermBypassSelected(object? sender, TappedEventArgs e)  => SelectPermission("bypassPermissions");

    private void SelectPermission(string key)
    {
        if (BindingContext is SettingsViewModel vm)
            vm.TeamPermissionMode = key;

        ApplyPermissionSelection(key);
    }

    private void ApplyPermissionSelection(string activeKey)
    {
        var accentCyan = (Color)Resources["AccentCyan"];
        var cardBorder = (Color)Resources["CardBorder"];
        var checkInactive = (Color)Resources["CheckInactive"];

        foreach (var row in _permissionRows)
        {
            bool isActive = row.Key == activeKey;
            row.Card.Stroke = new SolidColorBrush(isActive ? accentCyan : cardBorder);
            row.Check.BackgroundColor = isActive ? accentCyan : checkInactive;

            // Set checkmark text on the circle's child label
            if (row.Check.Content is Label checkLabel)
                checkLabel.Text = isActive ? "✓" : "";
        }
    }
}
