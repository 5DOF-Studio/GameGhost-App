using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// P/Invoke declarations for GaimerSpeech native Swift helper.
/// Exported C functions from the GaimerSpeech.xcframework:
///   - speech_is_stt_available, speech_is_tts_available
///   - speech_transcribe, speech_synthesize
///   - speech_free_buffer
///
/// The DllImportResolver in NativeMethods.cs handles library resolution.
/// </summary>
internal static class GaimerSpeechNativeMethods
{
    private const string LibName = "GaimerSpeech";

    /// <summary>
    /// Ensures the DllImportResolver in NativeMethods.cs is registered before
    /// any DllImport in this class is resolved.
    /// </summary>
    static GaimerSpeechNativeMethods()
    {
        RuntimeHelpers.RunClassConstructor(typeof(NativeMethods).TypeHandle);
    }

    // --- Availability ---

    /// <summary>Returns true if SFSpeechRecognizer is available for en-US.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool speech_is_stt_available();

    /// <summary>Returns true if AVSpeechSynthesizer has English voices.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool speech_is_tts_available();

    // --- STT ---

    /// <summary>
    /// Callback for transcription results.
    /// Parameters: (transcriptUTF8, transcriptLength) or (IntPtr.Zero, 0) on failure.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void TranscribeCallback(IntPtr transcriptUtf8, int transcriptLength);

    /// <summary>
    /// Transcribe PCM audio data (16kHz, 16-bit, mono) into text.
    /// Result delivered asynchronously via callback.
    /// </summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void speech_transcribe(
        IntPtr pcmData, int pcmLength, int sampleRate,
        TranscribeCallback callback);

    // --- TTS ---

    /// <summary>
    /// Callback for synthesis results.
    /// Parameters: (pcmData, pcmLength) or (IntPtr.Zero, 0) on failure.
    /// The pcmData must be freed via speech_free_buffer after copying.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void SynthesizeCallback(IntPtr pcmData, int pcmLength);

    /// <summary>
    /// Synthesize text into PCM audio (24kHz, 16-bit, mono).
    /// Result delivered asynchronously via callback.
    /// </summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void speech_synthesize(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        SynthesizeCallback callback);

    // --- Memory ---

    /// <summary>Free a buffer previously allocated by speech_synthesize.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void speech_free_buffer(IntPtr pointer);
}
