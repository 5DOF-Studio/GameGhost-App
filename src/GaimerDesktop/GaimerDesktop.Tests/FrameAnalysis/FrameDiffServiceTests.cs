using SkiaSharp;
using GaimerDesktop.Services;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.FrameAnalysis;

public class FrameDiffServiceTests
{
    private readonly FrameDiffService _sut = new();

    // Gradient images produce non-zero dHash (solid colors produce hash=0 since all adjacent pixels are equal)
    private static byte[] ImageA => TestImageFactory.CreateGradientPng(100, 100, SKColors.Red, SKColors.Blue);
    private static byte[] ImageB => TestImageFactory.CreateGradientPng(100, 100, SKColors.Green, SKColors.Yellow);
    private static byte[] ImageC => TestImageFactory.CreateCheckerboardPng(100, 100, 10, SKColors.Black, SKColors.White);

    [Fact]
    public void ComputeHash_SameImage_ReturnsDeterministicHash()
    {
        var image = ImageA;
        var hash1 = _sut.ComputeHash(image);
        var hash2 = _sut.ComputeHash(image);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentImages_ReturnsDifferentHashes()
    {
        var hashA = _sut.ComputeHash(ImageA);
        var hashC = _sut.ComputeHash(ImageC);

        // Both should be non-zero (gradient/checkerboard have pixel differences)
        hashA.Should().NotBe(0UL);
        hashC.Should().NotBe(0UL);
        hashA.Should().NotBe(hashC);
    }

    [Fact]
    public void CompareHashes_IdenticalHashes_ReturnsZero()
    {
        var hash = _sut.ComputeHash(ImageA);
        _sut.CompareHashes(hash, hash).Should().Be(0);
    }

    [Fact]
    public void CompareHashes_AllBitsDifferent_Returns64()
    {
        _sut.CompareHashes(0UL, ulong.MaxValue).Should().Be(64);
    }

    [Fact]
    public void HasChanged_FirstCall_ReturnsTrue()
    {
        // Fresh service, _lastHash=0, gradient image has non-zero hash → distance > threshold
        _sut.HasChanged(ImageA).Should().BeTrue();
    }

    [Fact]
    public void HasChanged_SameImageTwice_ReturnsFalse()
    {
        _sut.HasChanged(ImageA);
        // Same image: distance=0 < threshold
        _sut.HasChanged(ImageA).Should().BeFalse();
    }

    [Fact]
    public void HasChanged_WithinDebounce_ReturnsFalse()
    {
        // First call triggers (returns true)
        _sut.HasChanged(ImageA).Should().BeTrue();
        // Immediately call with different image — within 1.5s debounce window
        _sut.HasChanged(ImageC).Should().BeFalse();
    }

    [Fact]
    public void HasChanged_HighThreshold_ReturnsFalse()
    {
        // threshold=65 is impossible (max Hamming distance for 64-bit hash is 64)
        _sut.HasChanged(ImageA, threshold: 65).Should().BeFalse();
    }

    [Fact]
    public async Task HasChanged_DifferentImagesAfterDebounce_ReturnsTrue()
    {
        _sut.HasChanged(ImageA).Should().BeTrue();
        // Wait beyond 1.5s debounce window
        await Task.Delay(1600);
        _sut.HasChanged(ImageC).Should().BeTrue();
    }

    [Fact]
    public void HasChanged_WhenChanged_FiresFrameChangedEvent()
    {
        FrameChangeEventArgs? captured = null;
        _sut.FrameChanged += (_, args) => captured = args;

        _sut.HasChanged(ImageA);

        captured.Should().NotBeNull();
        captured!.HammingDistance.Should().BeGreaterThan(0);
        captured.CurrentHash.Should().NotBe(0UL);
    }

    // ── Constructor Parameterization Tests ────────────────────────────────

    [Fact]
    public async Task Constructor_CustomDebounce_UsesProvidedValue()
    {
        // Create with 200ms debounce (much shorter than default 1.5s)
        var sut = new FrameDiffService(debounceWindow: TimeSpan.FromMilliseconds(200));

        // First call triggers
        sut.HasChanged(ImageA).Should().BeTrue();
        // Wait beyond 200ms debounce but well within default 1.5s
        await Task.Delay(300);
        // Should trigger because custom debounce has elapsed
        sut.HasChanged(ImageC).Should().BeTrue();
    }

    [Fact]
    public void Constructor_CustomThreshold_UsesProvidedDefault()
    {
        // Create with very high threshold — requires extreme difference to trigger
        var sut = new FrameDiffService(defaultThreshold: 60);

        // ImageA has a modest dHash; distance from 0 is unlikely >= 60
        // This should return false because the threshold is too high
        // (fresh service _lastHash=0, distance to ImageA hash < 60 for typical gradient)
        var hashA = sut.ComputeHash(ImageA);
        var distance = sut.CompareHashes(0UL, hashA);

        // With threshold=60, HasChanged should only return true if distance >= 60
        if (distance < 60)
            sut.HasChanged(ImageA).Should().BeFalse();
        else
            sut.HasChanged(ImageA).Should().BeTrue();
    }

    [Fact]
    public void HasChanged_ExplicitThreshold_OverridesDefault()
    {
        // Create with impossibly high default threshold (65 > max 64-bit Hamming distance)
        var sut = new FrameDiffService(defaultThreshold: 65);

        // With default threshold=65, should never trigger
        sut.HasChanged(ImageA).Should().BeFalse();

        // But explicit low threshold=1 should override and trigger
        var sut2 = new FrameDiffService(defaultThreshold: 65);
        sut2.HasChanged(ImageA, threshold: 1).Should().BeTrue();
    }
}
