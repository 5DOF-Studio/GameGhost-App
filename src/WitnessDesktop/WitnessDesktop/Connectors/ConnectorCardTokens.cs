namespace WitnessDesktop.Connectors;

public static class ConnectorCardTokens
{
    // ── Palette ────────────────────────────────────────────
    public static class Palette
    {
        public static readonly Color BackgroundTop    = Color.FromArgb("#0A1326");
        public static readonly Color BackgroundBottom  = Color.FromArgb("#111F36");
        public static readonly Color CardBase          = Color.FromArgb("#13233A");
        public static readonly Color CardInset         = Color.FromArgb("#0D1829");
        public static readonly Color Stroke            = Colors.White.WithAlpha(0.12f);
        public static readonly Color MutedText         = Colors.White.WithAlpha(0.64f);
        public static readonly Color Surface           = Colors.White.WithAlpha(0.08f);
        public static readonly Color Spinner           = Color.FromArgb("#D9B16E");
    }

    // ── Layout ─────────────────────────────────────────────
    public static class Layout
    {
        public const double CardWidth          = 420;
        public const double CardCornerRadius   = 28;
        public const double MediaCornerRadius  = 22;
        public const double ActionHeight       = 48;
        public const double PreviewHeight      = 184;
        public const double IconSize           = 46;
        public const double GameIconBox        = 80;
        public const double GameIconCorner     = 18;
        public const double ButtonCorner       = 14;
        public const double BadgeSize          = 168;
    }
}
