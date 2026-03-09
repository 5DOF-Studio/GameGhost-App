using GaimerDesktop.Services.Audio;

namespace GaimerDesktop.Tests.Audio;

public class AudioResamplerTests
{
    /// <summary>
    /// Creates 16-bit mono PCM bytes for the given sample count.
    /// Each sample is a simple ramp from 0 to sampleCount-1 (clamped to short range).
    /// </summary>
    private static byte[] MakePcm16(int sampleCount)
    {
        var bytes = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            short val = (short)(i % short.MaxValue);
            bytes[i * 2] = (byte)(val & 0xFF);
            bytes[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }
        return bytes;
    }

    [Fact]
    public void Resample_SameRate_ReturnsInput()
    {
        var input = MakePcm16(100);

        var result = AudioResampler.Resample(input, 16000, 16000);

        result.Should().BeSameAs(input);
    }

    [Fact]
    public void Resample_Upsample_CorrectSampleCount()
    {
        int inputSamples = 100;
        var input = MakePcm16(inputSamples);

        var result = AudioResampler.Resample(input, 16000, 24000);

        int expectedSamples = (int)(inputSamples * (24000.0 / 16000));
        result.Length.Should().Be(expectedSamples * 2);
    }

    [Fact]
    public void Resample_Downsample_CorrectSampleCount()
    {
        int inputSamples = 150;
        var input = MakePcm16(inputSamples);

        var result = AudioResampler.Resample(input, 24000, 16000);

        int expectedSamples = (int)(inputSamples * (16000.0 / 24000));
        result.Length.Should().Be(expectedSamples * 2);
    }

    [Fact]
    public void StereoToMono_AveragesChannels()
    {
        // One stereo frame: left=100, right=200
        short left = 100, right = 200;
        var stereo = new byte[]
        {
            (byte)(left & 0xFF), (byte)((left >> 8) & 0xFF),
            (byte)(right & 0xFF), (byte)((right >> 8) & 0xFF),
        };

        var mono = AudioResampler.StereoToMono(stereo);

        short expected = (short)((left + right) / 2);
        short actual = (short)(mono[0] | (mono[1] << 8));
        actual.Should().Be(expected);
    }

    [Fact]
    public void StereoToMono_HalvesByteCount()
    {
        // 10 stereo frames = 40 bytes → 10 mono frames = 20 bytes
        var stereo = new byte[40];

        var mono = AudioResampler.StereoToMono(stereo);

        mono.Length.Should().Be(20);
    }

    [Fact]
    public void Float32ToInt16_RangesClamped()
    {
        // Create a float sample > 1.0 (should clamp to 32767)
        float overdriven = 1.5f;
        var floatBytes = BitConverter.GetBytes(overdriven);

        var result = AudioResampler.Float32ToInt16(floatBytes);

        short sample = (short)(result[0] | (result[1] << 8));
        sample.Should().Be(short.MaxValue);
    }

    [Fact]
    public void Float32ToInt16_OutputLength()
    {
        // 10 float samples (40 bytes) → 10 int16 samples (20 bytes)
        var floatPcm = new byte[40];

        var result = AudioResampler.Float32ToInt16(floatPcm);

        result.Length.Should().Be(20);
    }

    [Fact]
    public void Resample_NullInput_Throws()
    {
        var act = () => AudioResampler.Resample(null!, 16000, 24000);

        act.Should().Throw<ArgumentNullException>();
    }
}
