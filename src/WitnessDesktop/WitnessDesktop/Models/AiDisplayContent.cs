namespace WitnessDesktop.Models;

public class AiDisplayContent
{
    public string? Text { get; set; }
    public byte[]? Image { get; set; }
    public ImageSource? ImageSource { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

