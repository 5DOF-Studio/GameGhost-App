using SkiaSharp;

namespace GaimerDesktop.Tests.Helpers;

/// <summary>
/// Generates test PNG images programmatically via SkiaSharp.
/// </summary>
public static class TestImageFactory
{
    public static byte[] CreateSolidPng(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static byte[] CreateGradientPng(int width, int height, SKColor left, SKColor right)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, 0),
                new[] { left, right },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, width, height, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static byte[] CreateCheckerboardPng(int width, int height, int cellSize, SKColor c1, SKColor c2)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(c1);

        using var paint = new SKPaint { Color = c2 };
        for (var y = 0; y < height; y += cellSize)
        {
            for (var x = 0; x < width; x += cellSize)
            {
                if (((x / cellSize) + (y / cellSize)) % 2 == 1)
                    canvas.DrawRect(x, y, cellSize, cellSize, paint);
            }
        }
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
