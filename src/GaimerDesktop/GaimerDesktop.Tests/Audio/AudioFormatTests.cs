using GaimerDesktop.Services.Audio;

namespace GaimerDesktop.Tests.Audio;

public class AudioFormatTests
{
    [Fact]
    public void InputBytesForDuration_1Second_Returns32000()
    {
        var bytes = AudioFormat.InputBytesForDuration(1000);

        bytes.Should().Be(32000); // 16000 * 2 * 1 = 32000 bytes/sec
    }

    [Fact]
    public void OutputBytesForDuration_1Second_Returns48000()
    {
        var bytes = AudioFormat.OutputBytesForDuration(1000);

        bytes.Should().Be(48000); // 24000 * 2 * 1 = 48000 bytes/sec
    }

    [Fact]
    public void DurationCalculations_LinearScaling()
    {
        var full = AudioFormat.OutputBytesForDuration(1000);
        var half = AudioFormat.OutputBytesForDuration(500);

        half.Should().Be(full / 2);
    }
}
