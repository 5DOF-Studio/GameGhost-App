namespace WitnessDesktop.Models;

public class MediaContent
{
    public MediaContentType Type { get; set; }
    public string? FilePath { get; set; }
    public string? Url { get; set; }
    public byte[]? ImageBytes { get; set; }
    public double StartTime { get; set; }
    public double Duration { get; set; }
    public string? Title { get; set; }
}

public enum MediaContentType
{
    Image,
    Video
}
