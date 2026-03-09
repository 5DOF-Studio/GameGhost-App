using SkiaSharp;
using GaimerDesktop.Services;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.FrameAnalysis;

public class ImageProcessorTests
{
    [Fact]
    public void ScaleAndCompress_HalvesResolution()
    {
        var png = TestImageFactory.CreateSolidPng(200, 100, SKColors.Green);
        var result = ImageProcessor.ScaleAndCompress(png, scale: 0.5f);

        result.Should().NotBeEmpty();
        using var bitmap = SKBitmap.Decode(result);
        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(100);
        bitmap.Height.Should().Be(50);
    }

    [Fact]
    public void ScaleAndCompress_OutputIsJpeg()
    {
        var png = TestImageFactory.CreateSolidPng(100, 100, SKColors.Green);
        var result = ImageProcessor.ScaleAndCompress(png);

        result.Should().NotBeEmpty();
        // JPEG magic bytes: FF D8
        result[0].Should().Be(0xFF);
        result[1].Should().Be(0xD8);
    }

    [Fact]
    public void ScaleToHeight_MaintainsAspectRatio()
    {
        var png = TestImageFactory.CreateSolidPng(400, 200, SKColors.Blue);
        var result = ImageProcessor.ScaleToHeight(png, targetHeight: 100);

        result.Should().NotBeEmpty();
        using var bitmap = SKBitmap.Decode(result);
        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(200);
        bitmap.Height.Should().Be(100);
    }

    [Fact]
    public void GenerateThumbnail_CenterCropsToSquare()
    {
        var png = TestImageFactory.CreateSolidPng(200, 100, SKColors.Red);
        var result = ImageProcessor.GenerateThumbnail(png, size: 64);

        result.Should().NotBeEmpty();
        using var bitmap = SKBitmap.Decode(result);
        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(64);
        bitmap.Height.Should().Be(64);
    }
}
