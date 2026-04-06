// GaimerSpeech.swift
// Native Swift helper wrapping SFSpeechRecognizer (STT) and AVSpeechSynthesizer (TTS)
// for P/Invoke consumption from .NET MAUI on Mac Catalyst.
//
// Exports C-callable functions via @_cdecl:
//   - speech_is_stt_available() -> Bool
//   - speech_is_tts_available() -> Bool
//   - speech_transcribe(pcmData, pcmLength, sampleRate, callback)
//   - speech_synthesize(text, callback)
//   - speech_free_buffer(pointer)
//
// STT input format: 16kHz, 16-bit, mono PCM
// TTS output format: 24kHz, 16-bit, mono PCM
//
// Uses Task.detached for async work (never @MainActor with callbacks to C#).
// Converts UnsafePointer<CChar> to String immediately before any dispatch.

import Foundation
import AVFoundation
import Speech

// MARK: - Availability checks

/// Returns true if SFSpeechRecognizer is available and the locale has on-device support.
@_cdecl("speech_is_stt_available")
public func speechIsSttAvailable() -> Bool {
    guard let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "en-US")) else {
        NSLog("[GaimerSpeech] STT unavailable: SFSpeechRecognizer init failed for en-US")
        return false
    }
    let available = recognizer.isAvailable
    NSLog("[GaimerSpeech] speech_is_stt_available: %@", available ? "TRUE" : "FALSE")
    return available
}

/// Returns true if AVSpeechSynthesizer can produce speech.
@_cdecl("speech_is_tts_available")
public func speechIsTtsAvailable() -> Bool {
    // AVSpeechSynthesizer is always available on macOS 10.14+ / Mac Catalyst 13+.
    // Check that we have at least one voice for en-US.
    let voices = AVSpeechSynthesisVoice.speechVoices().filter { $0.language.hasPrefix("en") }
    let available = !voices.isEmpty
    NSLog("[GaimerSpeech] speech_is_tts_available: %@ (%d English voices)", available ? "TRUE" : "FALSE", voices.count)
    return available
}

// MARK: - STT: Transcribe PCM audio

/// Callback type for transcription results.
/// Parameters: (transcriptUTF8, transcriptLength) or (nil, 0) on failure.
/// The transcript pointer is valid only during the callback invocation.
public typealias TranscribeCallback = @convention(c) (UnsafePointer<CChar>?, Int32) -> Void

/// Transcribe PCM audio data into text using SFSpeechRecognizer.
///
/// - Parameters:
///   - pcmData: Pointer to raw PCM audio bytes (16-bit, mono).
///   - pcmLength: Length of pcmData in bytes.
///   - sampleRate: Sample rate in Hz (expected: 16000).
///   - callback: Called with (UTF-8 transcript, length) or (nil, 0) on failure.
@_cdecl("speech_transcribe")
public func speechTranscribe(
    pcmData: UnsafePointer<UInt8>,
    pcmLength: Int32,
    sampleRate: Int32,
    callback: TranscribeCallback
) {
    // Copy PCM data immediately before dispatching (pointer may become invalid)
    let length = Int(pcmLength)
    let rate = Double(sampleRate)
    let dataCopy = Data(bytes: pcmData, count: length)

    Task.detached(priority: .userInitiated) {
        do {
            let transcript = try await transcribePCM(data: dataCopy, sampleRate: rate)
            if let transcript = transcript, !transcript.isEmpty {
                transcript.withCString { ptr in
                    callback(ptr, Int32(transcript.utf8.count))
                }
            } else {
                NSLog("[GaimerSpeech] Transcription returned empty/nil")
                callback(nil, 0)
            }
        } catch {
            NSLog("[GaimerSpeech] Transcription error: %@", error.localizedDescription)
            callback(nil, 0)
        }
    }
}

/// Internal async transcription using SFSpeechRecognizer offline recognition.
private func transcribePCM(data: Data, sampleRate: Double) async throws -> String? {
    guard let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "en-US")),
          recognizer.isAvailable else {
        throw NSError(domain: "GaimerSpeech", code: 1,
                      userInfo: [NSLocalizedDescriptionKey: "SFSpeechRecognizer unavailable"])
    }

    // Check authorization status
    let authStatus = await withCheckedContinuation { continuation in
        SFSpeechRecognizer.requestAuthorization { status in
            continuation.resume(returning: status)
        }
    }

    guard authStatus == .authorized else {
        throw NSError(domain: "GaimerSpeech", code: 2,
                      userInfo: [NSLocalizedDescriptionKey: "Speech recognition not authorized (status=\(authStatus.rawValue))"])
    }

    // Create audio format matching the input PCM (16-bit, mono, given sample rate)
    guard let audioFormat = AVAudioFormat(
        commonFormat: .pcmFormatInt16,
        sampleRate: sampleRate,
        channels: 1,
        interleaved: true
    ) else {
        throw NSError(domain: "GaimerSpeech", code: 3,
                      userInfo: [NSLocalizedDescriptionKey: "Failed to create AVAudioFormat"])
    }

    // Create PCM buffer from raw data
    let frameCount = UInt32(data.count / 2) // 16-bit = 2 bytes per frame
    guard let pcmBuffer = AVAudioPCMBuffer(pcmFormat: audioFormat, frameCapacity: frameCount) else {
        throw NSError(domain: "GaimerSpeech", code: 4,
                      userInfo: [NSLocalizedDescriptionKey: "Failed to create AVAudioPCMBuffer"])
    }
    pcmBuffer.frameLength = frameCount

    // Copy PCM data into buffer
    data.withUnsafeBytes { rawBuffer in
        guard let src = rawBuffer.baseAddress else { return }
        if let int16Data = pcmBuffer.int16ChannelData {
            memcpy(int16Data[0], src, data.count)
        }
    }

    // Write PCM buffer to a temporary file for SFSpeechURLRecognitionRequest
    let tempURL = FileManager.default.temporaryDirectory
        .appendingPathComponent("gaimer_stt_\(UUID().uuidString).wav")
    defer { try? FileManager.default.removeItem(at: tempURL) }

    // Write WAV file
    guard let audioFile = try? AVAudioFile(
        forWriting: tempURL,
        settings: audioFormat.settings
    ) else {
        throw NSError(domain: "GaimerSpeech", code: 5,
                      userInfo: [NSLocalizedDescriptionKey: "Failed to create temp audio file"])
    }
    try audioFile.write(from: pcmBuffer)

    NSLog("[GaimerSpeech] Wrote %d frames to temp WAV: %@", frameCount, tempURL.lastPathComponent)

    // Use URL-based recognition request (simpler than buffer-based for turn-based)
    let request = SFSpeechURLRecognitionRequest(url: tempURL)
    request.shouldReportPartialResults = false
    request.requiresOnDeviceRecognition = true

    let result = try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<SFSpeechRecognitionResult?, Error>) in
        recognizer.recognitionTask(with: request) { result, error in
            if let error = error {
                continuation.resume(throwing: error)
            } else if let result = result, result.isFinal {
                continuation.resume(returning: result)
            }
            // Ignore non-final partial results
        }
    }

    let transcript = result?.bestTranscription.formattedString
    NSLog("[GaimerSpeech] Transcription result: '%@'", transcript ?? "<nil>")
    return transcript
}

// MARK: - TTS: Synthesize text to PCM

/// Callback type for synthesis results.
/// Parameters: (pcmData, pcmLength) or (nil, 0) on failure.
/// The pcmData pointer must be freed by the caller via speech_free_buffer.
public typealias SynthesizeCallback = @convention(c) (UnsafeMutablePointer<UInt8>?, Int32) -> Void

/// Synthesize text into PCM audio using AVSpeechSynthesizer.write().
///
/// Output format: 24kHz, 16-bit, mono PCM.
///
/// - Parameters:
///   - text: Null-terminated UTF-8 text to synthesize.
///   - callback: Called with (pcmData, length) or (nil, 0) on failure.
///               The caller must free pcmData via speech_free_buffer().
@_cdecl("speech_synthesize")
public func speechSynthesize(
    text: UnsafePointer<CChar>,
    callback: SynthesizeCallback
) {
    // Convert string immediately before dispatching
    let textString = String(cString: text)

    guard !textString.isEmpty else {
        NSLog("[GaimerSpeech] speech_synthesize called with empty text")
        callback(nil, 0)
        return
    }

    Task.detached(priority: .userInitiated) {
        do {
            let pcmData = try await synthesizeText(textString)
            if let pcmData = pcmData, !pcmData.isEmpty {
                // Allocate unmanaged memory for the result (caller must free via speech_free_buffer)
                let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: pcmData.count)
                pcmData.copyBytes(to: buffer, count: pcmData.count)
                NSLog("[GaimerSpeech] Synthesis complete: %d bytes PCM", pcmData.count)
                callback(buffer, Int32(pcmData.count))
            } else {
                NSLog("[GaimerSpeech] Synthesis returned empty/nil")
                callback(nil, 0)
            }
        } catch {
            NSLog("[GaimerSpeech] Synthesis error: %@", error.localizedDescription)
            callback(nil, 0)
        }
    }
}

/// Internal async synthesis using AVSpeechSynthesizer.write().
private func synthesizeText(_ text: String) async throws -> Data? {
    let synthesizer = AVSpeechSynthesizer()
    let utterance = AVSpeechUtterance(string: text)
    utterance.voice = AVSpeechSynthesisVoice(language: "en-US")
    utterance.rate = 0.5  // AVSpeechUtteranceDefaultSpeechRate
    utterance.pitchMultiplier = 1.0
    utterance.volume = 1.0

    // Target output format: 24kHz, 16-bit, mono PCM
    let targetSampleRate: Double = 24000.0

    return try await withCheckedThrowingContinuation { continuation in
        var collectedData = Data()
        var hasResumed = false

        synthesizer.write(utterance) { buffer in
            guard let pcmBuffer = buffer as? AVAudioPCMBuffer else {
                // nil buffer signals completion
                if !hasResumed {
                    hasResumed = true
                    if collectedData.isEmpty {
                        continuation.resume(throwing: NSError(
                            domain: "GaimerSpeech", code: 10,
                            userInfo: [NSLocalizedDescriptionKey: "TTS produced no audio data"]))
                    } else {
                        // Convert collected float32 data to 16-bit PCM at target sample rate
                        let converted = convertToInt16PCM(
                            floatData: collectedData,
                            sourceSampleRate: 22050.0, // AVSpeechSynthesizer default
                            targetSampleRate: targetSampleRate
                        )
                        continuation.resume(returning: converted)
                    }
                }
                return
            }

            // AVSpeechSynthesizer.write() delivers float32 buffers
            if let floatData = pcmBuffer.floatChannelData {
                let frameCount = Int(pcmBuffer.frameLength)
                let byteCount = frameCount * MemoryLayout<Float>.size
                let rawData = Data(bytes: floatData[0], count: byteCount)
                collectedData.append(rawData)
            }
        }
    }
}

/// Convert float32 PCM data to 16-bit PCM, resampling if needed.
private func convertToInt16PCM(floatData: Data, sourceSampleRate: Double, targetSampleRate: Double) -> Data {
    let floatCount = floatData.count / MemoryLayout<Float>.size
    var floatSamples = [Float](repeating: 0, count: floatCount)
    floatData.withUnsafeBytes { raw in
        guard let src = raw.baseAddress?.assumingMemoryBound(to: Float.self) else { return }
        for i in 0..<floatCount {
            floatSamples[i] = src[i]
        }
    }

    // Resample if source and target rates differ
    let outputSamples: [Float]
    if abs(sourceSampleRate - targetSampleRate) > 1.0 {
        let ratio = targetSampleRate / sourceSampleRate
        let outputCount = Int(Double(floatCount) * ratio)
        var resampled = [Float](repeating: 0, count: outputCount)
        for i in 0..<outputCount {
            let srcIndex = Double(i) / ratio
            let srcIndexInt = Int(srcIndex)
            let frac = Float(srcIndex - Double(srcIndexInt))
            let s0 = floatSamples[min(srcIndexInt, floatCount - 1)]
            let s1 = floatSamples[min(srcIndexInt + 1, floatCount - 1)]
            resampled[i] = s0 + frac * (s1 - s0) // Linear interpolation
        }
        outputSamples = resampled
    } else {
        outputSamples = floatSamples
    }

    // Convert float32 [-1.0, 1.0] to Int16
    var int16Data = Data(capacity: outputSamples.count * 2)
    for sample in outputSamples {
        let clamped = max(-1.0, min(1.0, sample))
        let int16Value = Int16(clamped * Float(Int16.max))
        withUnsafeBytes(of: int16Value) { int16Data.append(contentsOf: $0) }
    }

    NSLog("[GaimerSpeech] Converted %d float samples -> %d int16 samples (%d bytes), %.0f Hz -> %.0f Hz",
          floatCount, outputSamples.count, int16Data.count, sourceSampleRate, targetSampleRate)
    return int16Data
}

// MARK: - Memory management

/// Free a buffer previously allocated by speech_synthesize.
/// Must be called from C# after copying the PCM data.
@_cdecl("speech_free_buffer")
public func speechFreeBuffer(pointer: UnsafeMutablePointer<UInt8>?) {
    guard let pointer = pointer else { return }
    pointer.deallocate()
}
