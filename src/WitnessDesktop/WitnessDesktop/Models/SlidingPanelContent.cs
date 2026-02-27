using Microsoft.Maui.Controls;

namespace WitnessDesktop.Models;

public class SlidingPanelContent
{
    public required string Title { get; init; }
    public required string Text { get; init; }
    public byte[]? Image { get; init; }
    public ImageSource? ImageSource { get; init; }
    public int AutoDismissMs { get; init; } = 5000;
}

