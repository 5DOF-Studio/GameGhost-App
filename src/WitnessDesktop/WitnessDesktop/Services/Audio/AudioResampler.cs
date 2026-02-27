namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Utility for resampling PCM audio between different sample rates.
/// </summary>
/// <remarks>
/// <para>
/// Use this utility in conversation providers that output audio at non-standard sample rates
/// to convert to the standard output format (24kHz/16-bit/mono) before emitting <c>AudioReceived</c>.
/// </para>
/// <para>
/// <b>Supported conversions:</b>
/// <list type="bullet">
/// <item>Downsampling (e.g., 44.1kHz → 24kHz, 48kHz → 24kHz)</item>
/// <item>Upsampling (e.g., 22kHz → 24kHz, 16kHz → 24kHz)</item>
/// </list>
/// </para>
/// <para>
/// <b>Algorithm:</b> Linear interpolation for upsampling, averaging for downsampling.
/// This is a simple implementation suitable for voice audio; for music or high-fidelity
/// use cases, consider a more sophisticated resampling library.
/// </para>
/// </remarks>
public static class AudioResampler
{
    /// <summary>
    /// Resamples 16-bit mono PCM audio from one sample rate to another.
    /// </summary>
    /// <param name="inputPcm">Input PCM audio bytes (16-bit signed, little-endian, mono).</param>
    /// <param name="inputSampleRate">Sample rate of input audio in Hz.</param>
    /// <param name="outputSampleRate">Desired output sample rate in Hz.</param>
    /// <returns>Resampled PCM audio bytes at the target sample rate.</returns>
    /// <exception cref="ArgumentNullException">Thrown if inputPcm is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if sample rates are invalid.</exception>
    public static byte[] Resample(byte[] inputPcm, int inputSampleRate, int outputSampleRate)
    {
        if (inputPcm is null) throw new ArgumentNullException(nameof(inputPcm));
        if (inputSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(inputSampleRate), "Sample rate must be positive.");
        if (outputSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(outputSampleRate), "Sample rate must be positive.");

        // Fast path: no conversion needed
        if (inputSampleRate == outputSampleRate)
            return inputPcm;

        if (inputPcm.Length < 2)
            return inputPcm;

        // Convert bytes to samples
        int inputSampleCount = inputPcm.Length / 2;
        var inputSamples = new short[inputSampleCount];
        for (int i = 0; i < inputSampleCount; i++)
        {
            inputSamples[i] = (short)(inputPcm[i * 2] | (inputPcm[i * 2 + 1] << 8));
        }

        // Calculate output sample count
        double ratio = (double)outputSampleRate / inputSampleRate;
        int outputSampleCount = (int)(inputSampleCount * ratio);
        var outputSamples = new short[outputSampleCount];

        // Resample using linear interpolation
        for (int i = 0; i < outputSampleCount; i++)
        {
            double srcIndex = i / ratio;
            int srcIndexInt = (int)srcIndex;
            double fraction = srcIndex - srcIndexInt;

            if (srcIndexInt >= inputSampleCount - 1)
            {
                // Last sample - no interpolation possible
                outputSamples[i] = inputSamples[inputSampleCount - 1];
            }
            else
            {
                // Linear interpolation between two adjacent samples
                double sample = inputSamples[srcIndexInt] * (1 - fraction) +
                               inputSamples[srcIndexInt + 1] * fraction;
                outputSamples[i] = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
            }
        }

        // Convert samples back to bytes
        var outputPcm = new byte[outputSampleCount * 2];
        for (int i = 0; i < outputSampleCount; i++)
        {
            outputPcm[i * 2] = (byte)(outputSamples[i] & 0xFF);
            outputPcm[i * 2 + 1] = (byte)((outputSamples[i] >> 8) & 0xFF);
        }

        return outputPcm;
    }

    /// <summary>
    /// Resamples audio to the standard output sample rate (24kHz).
    /// </summary>
    /// <param name="inputPcm">Input PCM audio bytes (16-bit signed, little-endian, mono).</param>
    /// <param name="inputSampleRate">Sample rate of input audio in Hz.</param>
    /// <returns>Resampled PCM audio bytes at 24kHz.</returns>
    public static byte[] ResampleToStandardOutput(byte[] inputPcm, int inputSampleRate)
        => Resample(inputPcm, inputSampleRate, AudioFormat.StandardOutputSampleRate);

    /// <summary>
    /// Resamples audio to the standard input sample rate (16kHz).
    /// Useful for normalizing microphone input from devices with non-standard rates.
    /// </summary>
    /// <param name="inputPcm">Input PCM audio bytes (16-bit signed, little-endian, mono).</param>
    /// <param name="inputSampleRate">Sample rate of input audio in Hz.</param>
    /// <returns>Resampled PCM audio bytes at 16kHz.</returns>
    public static byte[] ResampleToStandardInput(byte[] inputPcm, int inputSampleRate)
        => Resample(inputPcm, inputSampleRate, AudioFormat.StandardInputSampleRate);

    /// <summary>
    /// Converts stereo PCM to mono by averaging left and right channels.
    /// </summary>
    /// <param name="stereoPcm">Stereo PCM audio bytes (16-bit signed, little-endian, interleaved L/R).</param>
    /// <returns>Mono PCM audio bytes.</returns>
    public static byte[] StereoToMono(byte[] stereoPcm)
    {
        if (stereoPcm is null) throw new ArgumentNullException(nameof(stereoPcm));
        if (stereoPcm.Length < 4) return stereoPcm;

        // Stereo: 4 bytes per frame (2 bytes left + 2 bytes right)
        int stereoFrameCount = stereoPcm.Length / 4;
        var monoPcm = new byte[stereoFrameCount * 2];

        for (int i = 0; i < stereoFrameCount; i++)
        {
            int offset = i * 4;
            short left = (short)(stereoPcm[offset] | (stereoPcm[offset + 1] << 8));
            short right = (short)(stereoPcm[offset + 2] | (stereoPcm[offset + 3] << 8));

            // Average the two channels
            short mono = (short)((left + right) / 2);

            monoPcm[i * 2] = (byte)(mono & 0xFF);
            monoPcm[i * 2 + 1] = (byte)((mono >> 8) & 0xFF);
        }

        return monoPcm;
    }

    /// <summary>
    /// Converts 32-bit float PCM to 16-bit integer PCM.
    /// </summary>
    /// <param name="floatPcm">Float PCM audio bytes (32-bit float, little-endian).</param>
    /// <returns>16-bit integer PCM audio bytes.</returns>
    public static byte[] Float32ToInt16(byte[] floatPcm)
    {
        if (floatPcm is null) throw new ArgumentNullException(nameof(floatPcm));
        if (floatPcm.Length < 4) return Array.Empty<byte>();

        int sampleCount = floatPcm.Length / 4;
        var int16Pcm = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            // Read 32-bit float (little-endian)
            float sample = BitConverter.ToSingle(floatPcm, i * 4);

            // Convert to 16-bit integer range [-32768, 32767]
            int intSample = (int)(sample * 32767f);
            intSample = Math.Clamp(intSample, short.MinValue, short.MaxValue);

            int16Pcm[i * 2] = (byte)(intSample & 0xFF);
            int16Pcm[i * 2 + 1] = (byte)((intSample >> 8) & 0xFF);
        }

        return int16Pcm;
    }
}

