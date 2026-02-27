using CommunityToolkit.Mvvm.ComponentModel;

namespace WitnessDesktop.Models;

public partial class CaptureTarget : ObservableObject
{
    public required nint Handle { get; init; }
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public byte[]? Thumbnail { get; set; }
    public string? ChessBadge { get; set; }
    public bool IsDisabled { get; set; }

    /// <summary>Window bounds on screen (X, Y, Width, Height) for crop-based capture.</summary>
    public double BoundsX { get; set; }
    public double BoundsY { get; set; }
    public double BoundsWidth { get; set; }
    public double BoundsHeight { get; set; }

    [ObservableProperty]
    private bool _isSelected;
}
