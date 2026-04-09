namespace WitnessDesktop.Connectors;

public static class ConnectorCardProviderExtensions
{
    public static string Title(this ConnectorCardProvider provider) => provider switch
    {
        ConnectorCardProvider.Chess      => "Chess",
        ConnectorCardProvider.Team       => "Ghost Team",
        ConnectorCardProvider.Discord    => "Discord",
        ConnectorCardProvider.Twitch     => "Twitch",
        ConnectorCardProvider.AppleMusic => "Apple Music",
        ConnectorCardProvider.Spotify    => "Spotify",
        _ => "Unknown"
    };

    public static string Subtitle(this ConnectorCardProvider provider) => provider switch
    {
        ConnectorCardProvider.Chess      => "Exclusive Screen Connect",
        ConnectorCardProvider.Team       => "AI Task Engine",
        ConnectorCardProvider.Discord    => "Community Auth",
        ConnectorCardProvider.Twitch     => "Stream Auth",
        ConnectorCardProvider.AppleMusic => "Music Auth",
        ConnectorCardProvider.Spotify    => "Music Auth",
        _ => ""
    };

    public static Color AccentColor(this ConnectorCardProvider provider) => provider switch
    {
        ConnectorCardProvider.Chess      => Color.FromArgb("#D9B16E"),
        ConnectorCardProvider.Team       => Color.FromArgb("#00d4ff"),
        ConnectorCardProvider.Discord    => Color.FromArgb("#5865F2"),
        ConnectorCardProvider.Twitch     => Color.FromArgb("#9146FF"),
        ConnectorCardProvider.AppleMusic => Color.FromArgb("#FA243C"),
        ConnectorCardProvider.Spotify    => Color.FromArgb("#1ED760"),
        _ => Colors.White
    };

    public static string? IconAsset(this ConnectorCardProvider provider) => provider switch
    {
        ConnectorCardProvider.Chess => "chess_game_icon.png",
        ConnectorCardProvider.Team  => "team_icon.png",
        _ => null
    };

    public static string? BadgeAsset(this ConnectorCardProvider provider) => provider switch
    {
        ConnectorCardProvider.Chess => "chess_badge.png",
        _ => null
    };

    public static string SystemImage(this ConnectorCardProvider provider) => provider switch
    {
        ConnectorCardProvider.Chess      => "♟",
        ConnectorCardProvider.Team       => "⚡",
        ConnectorCardProvider.Discord    => "👥",
        ConnectorCardProvider.Twitch     => "📡",
        ConnectorCardProvider.AppleMusic => "♪",
        ConnectorCardProvider.Spotify    => "♫",
        _ => "?"
    };
}
