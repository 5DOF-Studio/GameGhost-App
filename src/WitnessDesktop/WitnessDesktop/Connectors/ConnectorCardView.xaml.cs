namespace WitnessDesktop.Connectors;

public partial class ConnectorCardView : ContentView
{
    private ConnectorCardConfiguration _config = new();
    private ConnectorCardState _state = new();
    private ConnectorCardMatch? _currentMatch;

    public event Action<ConnectorCardEvent>? OnEvent;

    public ConnectorCardView()
    {
        InitializeComponent();
        ApplyConfiguration(_config);
        ApplyState(_state);
    }

    // ── Public API ─────────────────────────────────────────

    public ConnectorCardConfiguration Configuration
    {
        get => _config;
        set { _config = value; ApplyConfiguration(value); ApplyState(_state); }
    }

    public ConnectorCardState State
    {
        get => _state;
        set { _state = value; ApplyState(value); }
    }

    // ── Configuration ──────────────────────────────────────

    private void ApplyConfiguration(ConnectorCardConfiguration config)
    {
        var accent = config.Provider.AccentColor();

        CardShell.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(ConnectorCardTokens.Palette.CardBase, 0f),
                new GradientStop(ConnectorCardTokens.Palette.CardInset, 1f)
            }
        };

        CardShell.Stroke = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Colors.White.WithAlpha(0.12f), 0f),
                new GradientStop(Colors.White.WithAlpha(0.04f), 1f)
            }
        };

        SearchSpinner.Color = accent;
        ConnectingSpinner.Color = accent;

        GameIconBorder.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(accent.WithAlpha(0.2f), 0f),
                new GradientStop(accent.WithAlpha(0.06f), 1f)
            }
        };
        GameIconBorder.Stroke = new SolidColorBrush(accent.WithAlpha(0.25f));

        // Each ConnectorCard is instantiated per-provider in SettingsPage.xaml — not recycled
        // between providers. No stale-image cleanup needed on reconfiguration.
        var iconAsset = config.Provider.IconAsset();
        if (iconAsset != null)
            GameIconImage.Source = ImageSource.FromFile(iconAsset);

        PreviewBorder.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(accent.WithAlpha(0.22f), 0f),
                new GradientStop(Colors.White.WithAlpha(0.04f), 1f)
            }
        };

        if (iconAsset != null)
            PreviewIcon.Source = ImageSource.FromFile(iconAsset);

        var badgeAsset = config.Provider.BadgeAsset();
        if (badgeAsset != null)
        {
            BadgeOverlay.Source = ImageSource.FromFile(badgeAsset);
            BadgeOverlay.IsVisible = true;
        }

        var muted = ConnectorCardTokens.Palette.MutedText;
        MatchSubtitle.TextColor = muted;
        MatchDetail.TextColor = muted;

        BtnSearchDismiss.TextColor = accent;
        BtnSearchDismiss.BackgroundColor = Colors.White.WithAlpha(0.06f);
        BtnSearchDismiss.BorderColor = accent.WithAlpha(0.3f);
        BtnSearchDismiss.Text = config.DismissTitle;

        BtnConnect.BackgroundColor = accent;
        BtnConnect.TextColor = Colors.Black.WithAlpha(0.85f);

        BtnRefresh.BackgroundColor = accent;
        BtnRefresh.TextColor = Colors.Black.WithAlpha(0.85f);
        BtnRefresh.Text = config.RefreshTitle;

        BtnEmptyDismiss.Text = config.DismissTitle;

        BadgePill.BackgroundColor = accent;
    }

    // ── State ──────────────────────────────────────────────

    private void ApplyState(ConnectorCardState state)
    {
        SearchingBody.IsVisible = false;
        MatchBody.IsVisible = false;
        EmptyBody.IsVisible = false;

        BtnSearchDismiss.IsVisible = false;
        BtnConnect.IsVisible = false;
        BtnEmptyDismiss.IsVisible = false;
        BtnRefresh.IsVisible = false;

        ConnectingSpinner.IsVisible = false;
        ConnectingSpinner.IsRunning = false;
        PreviewIcon.Opacity = 1.0;

        BtnConnect.IsEnabled = true;

        _currentMatch = null;

        switch (state.Status)
        {
            case ConnectorCardStatus.Searching s:
                SearchMessage.Text = s.Message;
                SearchingBody.IsVisible = true;
                BtnSearchDismiss.IsVisible = true;
                Grid.SetColumnSpan(BtnSearchDismiss, 2);
                break;

            case ConnectorCardStatus.Match m:
                _currentMatch = m.MatchData;
                ApplyMatch(m.MatchData);
                MatchBody.IsVisible = true;
                BtnConnect.IsVisible = true;
                BtnConnect.IsEnabled = true;
                BtnConnect.Text = m.MatchData.PrimaryCta;
                Grid.SetColumnSpan(BtnConnect, 2);
                break;

            case ConnectorCardStatus.Connecting c:
                _currentMatch = c.MatchData;
                ApplyMatch(c.MatchData);
                MatchBody.IsVisible = true;
                PreviewIcon.Opacity = 0.25;
                ConnectingSpinner.IsVisible = true;
                ConnectingSpinner.IsRunning = true;
                BtnConnect.IsVisible = true;
                BtnConnect.IsEnabled = false;
                BtnConnect.Text = "Connecting...";
                Grid.SetColumnSpan(BtnConnect, 2);
                break;

            case ConnectorCardStatus.Connected c:
                _currentMatch = c.MatchData;
                ApplyMatch(c.MatchData);
                MatchBody.IsVisible = true;
                BtnConnect.IsVisible = true;
                BtnConnect.IsEnabled = false;
                BtnConnect.Text = "Connected";
                Grid.SetColumnSpan(BtnConnect, 2);
                break;

            case ConnectorCardStatus.Empty e:
                EmptyMessage.Text = e.Message;
                EmptyBody.IsVisible = true;
                BtnEmptyDismiss.IsVisible = true;
                BtnRefresh.IsVisible = true;
                Grid.SetColumnSpan(BtnEmptyDismiss, 1);
                Grid.SetColumnSpan(BtnRefresh, 1);
                break;
        }
    }

    private void ApplyMatch(ConnectorCardMatch match)
    {
        MatchTitle.Text = match.Title;
        MatchSubtitle.Text = match.Subtitle ?? _config.Provider.Subtitle();

        MatchDetail.IsVisible = !string.IsNullOrEmpty(match.Metadata.Detail);
        MatchDetail.Text = match.Metadata.Detail ?? "";

        ApplyMetaRow(MetaWindowRow, MetaWindow, match.Metadata.WindowTitle);
        ApplyMetaRow(MetaSceneRow, MetaScene, match.Metadata.SceneLabel);
        ApplyMetaRow(MetaDetailsRow, MetaDetails, match.Metadata.Detail);

        MetadataSection.IsVisible =
            MetaWindowRow.IsVisible || MetaSceneRow.IsVisible || MetaDetailsRow.IsVisible;

        BadgePill.IsVisible = match.BadgeText != null;
        BadgeLabel.Text = match.BadgeText ?? "";
    }

    private static void ApplyMetaRow(Grid row, Label valueLabel, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            row.IsVisible = false;
        }
        else
        {
            row.IsVisible = true;
            valueLabel.Text = value;
        }
    }

    // ── Event handlers ─────────────────────────────────────

    private void OnDismissTapped(object? sender, EventArgs e)
        => OnEvent?.Invoke(new ConnectorCardEvent.DismissTapped());

    private void OnRefreshTapped(object? sender, EventArgs e)
        => OnEvent?.Invoke(new ConnectorCardEvent.RefreshTapped());

    private void OnConnectTapped(object? sender, EventArgs e)
    {
        if (_currentMatch != null)
            OnEvent?.Invoke(new ConnectorCardEvent.ConnectTapped(_currentMatch));
    }
}
