using FluentAssertions;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Tests.Services;

public class SfxPlayerTests
{
    [Fact]
    public async Task PlayAsync_NonExistentFile_DoesNotThrow()
    {
        var sut = new SfxPlayer();
        // Should not throw even with a bad filename — graceful degradation
        await sut.PlayAsync("nonexistent_file.mp3");
    }

    [Fact]
    public async Task PlayAsync_EmptyFileName_DoesNotThrow()
    {
        var sut = new SfxPlayer();
        await sut.PlayAsync("");
    }
}
