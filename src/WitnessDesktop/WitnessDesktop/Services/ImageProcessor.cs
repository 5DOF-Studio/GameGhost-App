using SkiaSharp;

namespace WitnessDesktop.Services;

/// <summary>
/// Cross-platform image processing helper using SkiaSharp.
/// Provides scaling, JPEG compression, and thumbnail generation for screen capture frames.
/// </summary>
public static class ImageProcessor
{
    private const float DefaultScale = 0.5f;
    private const int DefaultJpegQuality = 60;
    private const int ThumbnailSize = 64;

    /// <summary>
    /// Scales an image by the given factor and compresses to JPEG.
    /// </summary>
    /// <param name="pngData">Raw PNG image data.</param>
    /// <param name="scale">Scale factor (0.0–1.0). Default 0.5 (50%).</param>
    /// <param name="jpegQuality">JPEG quality (0–100). Default 60.</param>
    /// <returns>JPEG-compressed image bytes, or empty array if decoding fails.</returns>
    public static byte[] ScaleAndCompress(byte[] pngData, float scale = DefaultScale, int jpegQuality = DefaultJpegQuality)
    {
        using var original = SKBitmap.Decode(pngData);
        if (original is null) return [];

        var newWidth = (int)(original.Width * scale);
        var newHeight = (int)(original.Height * scale);

        if (newWidth <= 0 || newHeight <= 0) return [];

        using var scaled = original.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (scaled is null) return [];

        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);
        return data.ToArray();
    }

    /// <summary>
    /// Scales an image to a target height (maintaining aspect ratio) and compresses to JPEG.
    /// Used for preview display where full-resolution capture is wasteful.
    /// </summary>
    public static byte[] ScaleToHeight(byte[] imageData, int targetHeight, int jpegQuality = DefaultJpegQuality)
    {
        using var original = SKBitmap.Decode(imageData);
        if (original is null || original.Height <= 0) return imageData;

        var scale = (float)targetHeight / original.Height;
        if (scale >= 1.0f) return imageData; // Already small enough

        var newWidth = (int)(original.Width * scale);
        var newHeight = targetHeight;

        if (newWidth <= 0 || newHeight <= 0) return imageData;

        using var scaled = original.Resize(
            new SKImageInfo(newWidth, newHeight),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (scaled is null) return imageData;

        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);
        return data.ToArray();
    }

    /// <summary>
    /// Generates a square thumbnail from image data.
    /// Centers and crops the source to a square, then scales to <paramref name="size"/>x<paramref name="size"/>.
    /// </summary>
    /// <param name="imageData">Source image data (any format SkiaSharp can decode).</param>
    /// <param name="size">Thumbnail edge length in pixels. Default 64.</param>
    /// <returns>JPEG thumbnail bytes, or empty array if decoding fails.</returns>
    public static byte[] GenerateThumbnail(byte[] imageData, int size = ThumbnailSize)
    {
        using var original = SKBitmap.Decode(imageData);
        if (original is null) return [];

        // Center-crop to square
        var side = Math.Min(original.Width, original.Height);
        var cropX = (original.Width - side) / 2;
        var cropY = (original.Height - side) / 2;
        var cropRect = new SKRectI(cropX, cropY, cropX + side, cropY + side);

        using var cropped = new SKBitmap(side, side);
        if (!original.ExtractSubset(cropped, cropRect)) return [];

        using var scaled = cropped.Resize(new SKImageInfo(size, size), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (scaled is null) return [];

        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, DefaultJpegQuality);
        return data.ToArray();
    }
}
