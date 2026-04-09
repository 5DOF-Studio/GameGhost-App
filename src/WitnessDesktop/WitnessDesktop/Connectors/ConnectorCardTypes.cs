namespace WitnessDesktop.Connectors;

// ── Card Kind ──────────────────────────────────────────────
public enum ConnectorCardKind
{
    ScreenConnect,
    AuthFlow
}

// ── Provider ───────────────────────────────────────────────
public enum ConnectorCardProvider
{
    Chess,
    Team,
    Discord,
    Twitch,
    AppleMusic,
    Spotify
}

// ── Image source ───────────────────────────────────────────
public abstract record ConnectorCardImageSource
{
    public sealed record Asset(string Name)               : ConnectorCardImageSource;
    public sealed record SystemIcon(string Name)           : ConnectorCardImageSource;
    public sealed record PngData(byte[] Data)              : ConnectorCardImageSource;
}

// ── Window metadata ────────────────────────────────────────
public record ConnectorCardWindowMetadata(
    string WindowTitle,
    string? SceneLabel = null,
    string? Detail = null
);

// ── Match payload ──────────────────────────────────────────
public record ConnectorCardMatch(
    string Title,
    string? Subtitle,
    ConnectorCardImageSource? PreviewImage,
    string? BadgeText,
    ConnectorCardWindowMetadata Metadata,
    string PrimaryCta = "Connect"
);

// ── Card status ────────────────────────────────────────────
public abstract record ConnectorCardStatus
{
    public sealed record Searching(string Message) : ConnectorCardStatus;
    public sealed record Match(ConnectorCardMatch MatchData) : ConnectorCardStatus;
    public sealed record Connecting(ConnectorCardMatch MatchData) : ConnectorCardStatus;
    public sealed record Connected(ConnectorCardMatch MatchData) : ConnectorCardStatus;
    public sealed record Empty(string Message)     : ConnectorCardStatus;
}

// ── Configuration ──────────────────────────────────────────
public record ConnectorCardConfiguration
{
    public ConnectorCardKind Kind       { get; init; } = ConnectorCardKind.ScreenConnect;
    public ConnectorCardProvider Provider { get; init; } = ConnectorCardProvider.Chess;
    public string Title          { get; init; } = "Connector Card";
    public string SearchTitle    { get; init; } = "Searching for chess game";
    public string EmptyTitle     { get; init; } = "No Chess game found on window.";
    public string DismissTitle   { get; init; } = "Dismiss";
    public string RefreshTitle   { get; init; } = "Refresh";
}

// ── Card state ─────────────────────────────────────────────
public record ConnectorCardState
{
    public bool IsPresented { get; init; }
    public ConnectorCardStatus Status { get; init; }
        = new ConnectorCardStatus.Searching("Searching for chess game");
}

// ── Events ─────────────────────────────────────────────────
public abstract record ConnectorCardEvent
{
    public sealed record DismissTapped                           : ConnectorCardEvent;
    public sealed record RefreshTapped                           : ConnectorCardEvent;
    public sealed record ConnectTapped(ConnectorCardMatch Match) : ConnectorCardEvent;
}
