// GaimerScreenRecording.swift
// Continuous HEVC screen recording via SCStream + AVAssetWriter.
//
// Exports four C-callable functions via @_cdecl:
//   - sck_start_recording(windowID, outputPath, width, height) -> Bool
//   - sck_stop_recording(completion)
//   - sck_recording_status() -> Int32  (0=idle, 1=recording, 2=error)
//   - sck_rotate_segment(newOutputPath, completion) — gapless segment rotation
//
// Threading contract:
//   - All SCStream delegate callbacks arrive on recordingQueue
//   - All AVAssetWriter operations happen on recordingQueue
//   - @_cdecl wrappers dispatch async to recordingQueue for thread safety
//   - Status reads use OSAtomicCompareAndSwap32Barrier for lock-free atomics

import Foundation
import AVFoundation
import CoreMedia
import CoreVideo
import VideoToolbox

#if canImport(ScreenCaptureKit)
import ScreenCaptureKit
#endif

// MARK: - Status constants

private let kStatusIdle: Int32      = 0
private let kStatusRecording: Int32 = 1
private let kStatusError: Int32     = 2

// MARK: - ScreenRecorder singleton

#if canImport(ScreenCaptureKit)
@available(macCatalyst 18.2, macOS 14.0, *)
final class ScreenRecorder: NSObject, SCStreamOutput, SCStreamDelegate {

    // MARK: Singleton

    static let shared = ScreenRecorder()

    // MARK: Private state

    /// Dedicated serial queue for all recording operations including delegate callbacks.
    let recordingQueue = DispatchQueue(label: "com.gaimer.screen-recording", qos: .userInitiated)

    /// Atomic status: 0=idle, 1=recording, 2=error.
    private var _status: Int32 = kStatusIdle

    private var stream: SCStream?
    private var assetWriter: AVAssetWriter?
    private var assetWriterInput: AVAssetWriterInput?
    private var pixelBufferAdaptor: AVAssetWriterInputPixelBufferAdaptor?
    private var sessionStarted = false

    // Double-buffer state for gapless rotation
    private var pendingWriter: AVAssetWriter?
    private var pendingInput: AVAssetWriterInput?
    private var pendingAdaptor: AVAssetWriterInputPixelBufferAdaptor?
    private var rotationPending = false
    private var rotationCompletion: (@convention(c) () -> Void)?

    /// Activity assertion to prevent macOS energy-efficient scheduling from throttling frame delivery.
    private var activityToken: NSObjectProtocol?

    /// Frame counter for FPS logging.
    private var frameCount: Int = 0
    private var lastLogTime: CFAbsoluteTime = 0

    // MARK: Status (lock-free read)

    var status: Int32 {
        return OSAtomicAdd32(0, &_status)
    }

    private func setStatus(_ newValue: Int32) {
        var current = _status
        while !OSAtomicCompareAndSwap32Barrier(current, newValue, &_status) {
            current = _status
        }
    }

    // MARK: - start

    /// Enumerate SCShareableContent, find the target window, configure SCStream and
    /// AVAssetWriter, then start recording. Returns immediately; recording begins async.
    func start(windowID: UInt32, outputPath: String, width: Int, height: Int) async -> Bool {
        // Only start if currently idle
        guard status == kStatusIdle else {
            NSLog("[GaimerScreenRecording] start() ignored — status=%d", status)
            return false
        }

        do {
            // --- 1. Enumerate windows ---
            let content = try await SCShareableContent.excludingDesktopWindows(
                false, onScreenWindowsOnly: true
            )

            guard let window = content.windows.first(where: { $0.windowID == windowID }) else {
                NSLog("[GaimerScreenRecording] Window %u not found", windowID)
                return false
            }

            // --- 2. Configure SCStream ---
            let filter = SCContentFilter(desktopIndependentWindow: window)
            let streamConfig = SCStreamConfiguration()
            streamConfig.width  = width
            streamConfig.height = height
            streamConfig.pixelFormat          = kCVPixelFormatType_32BGRA
            streamConfig.minimumFrameInterval = CMTime(value: 1, timescale: 30)
            streamConfig.showsCursor          = false
            streamConfig.captureResolution    = .nominal
            streamConfig.queueDepth           = 15

            // --- 3. Set up AVAssetWriter ---
            let outputURL = URL(fileURLWithPath: outputPath)

            // Remove any pre-existing file so AVAssetWriter can create cleanly
            let fm = FileManager.default
            if fm.fileExists(atPath: outputPath) {
                try fm.removeItem(at: outputURL)
            }

            let writer = try AVAssetWriter(outputURL: outputURL, fileType: .mp4)
            writer.movieFragmentInterval = CMTime(seconds: 10.0, preferredTimescale: 600)
            writer.shouldOptimizeForNetworkUse = false

            let videoSettings: [String: Any] = [
                AVVideoCodecKey:  AVVideoCodecType.hevc,
                AVVideoWidthKey:  width,
                AVVideoHeightKey: height,
                AVVideoCompressionPropertiesKey: [
                    AVVideoAverageBitRateKey:            5_000_000,
                    AVVideoExpectedSourceFrameRateKey:   30,
                    AVVideoProfileLevelKey:              kVTProfileLevel_HEVC_Main_AutoLevel,
                    AVVideoAllowFrameReorderingKey:      false
                ] as [String: Any]
            ]

            let input = AVAssetWriterInput(
                mediaType: .video,
                outputSettings: videoSettings
            )
            input.expectsMediaDataInRealTime = true

            // Use pixel buffer adaptor to handle IOSurface-backed buffers from SCStream.
            // Direct CMSampleBuffer append fails under Catalyst with -16122
            // (kVTPixelTransferNotSupportedErr) because the hardware encoder can't
            // consume IOSurface pixel buffers directly.
            let adaptorAttrs: [String: Any] = [
                kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA,
                kCVPixelBufferWidthKey as String: width,
                kCVPixelBufferHeightKey as String: height
            ]
            let adaptor = AVAssetWriterInputPixelBufferAdaptor(
                assetWriterInput: input,
                sourcePixelBufferAttributes: adaptorAttrs
            )

            guard writer.canAdd(input) else {
                NSLog("[GaimerScreenRecording] AVAssetWriter cannot add video input")
                return false
            }
            writer.add(input)

            // --- 4. Start writing BEFORE frames arrive ---
            guard writer.startWriting() else {
                NSLog("[GaimerScreenRecording] AVAssetWriter.startWriting() failed: %@",
                      writer.error?.localizedDescription ?? "unknown")
                return false
            }

            // Persist state
            self.assetWriter        = writer
            self.assetWriterInput   = input
            self.pixelBufferAdaptor = adaptor
            self.sessionStarted     = false

            // --- 5. Create and start SCStream ---
            let newStream = SCStream(filter: filter, configuration: streamConfig, delegate: self)
            try newStream.addStreamOutput(self, type: .screen, sampleHandlerQueue: recordingQueue)
            try await newStream.startCapture()

            self.stream = newStream

            // Prevent macOS energy-efficient scheduling from throttling SCStream frame delivery
            self.activityToken = ProcessInfo.processInfo.beginActivity(
                options: [.userInitiated, .latencyCritical],
                reason: "Gaimer screen recording requires sustained 30fps frame delivery"
            )

            setStatus(kStatusRecording)
            NSLog("[GaimerScreenRecording] Recording started → %@", outputPath)
            return true

        } catch {
            NSLog("[GaimerScreenRecording] start() failed: %@", error.localizedDescription)
            setStatus(kStatusError)
            return false
        }
    }

    // MARK: - stop

    /// Stop the active SCStream and finish writing the asset file.
    /// completion is called on recordingQueue once the file is finalized.
    func stop(completion: @escaping @convention(c) () -> Void) {
        recordingQueue.async { [weak self] in
            guard let self = self else {
                completion()
                return
            }

            guard self.status == kStatusRecording, let activeStream = self.stream else {
                NSLog("[GaimerScreenRecording] stop() called but not recording (status=%d)", self.status)
                completion()
                return
            }

            // Use a detached Task so we can await async SCStream / AVAssetWriter APIs
            Task.detached(priority: .userInitiated) {
                // Stop capture (async, waits for in-flight callbacks to drain)
                do {
                    try await activeStream.stopCapture()
                } catch {
                    NSLog("[GaimerScreenRecording] stopCapture error: %@", error.localizedDescription)
                }

                // Finish writing on recordingQueue so it's serialized with any late frames
                await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
                    self.recordingQueue.async {
                        // Mark input as finished so no more samples are accepted
                        self.assetWriterInput?.markAsFinished()

                        guard let writer = self.assetWriter,
                              writer.status == .writing else {
                            NSLog("[GaimerScreenRecording] AVAssetWriter not in writing state; skipping finishWriting")
                            self.cleanupState()
                            continuation.resume()
                            return
                        }

                        // finishWriting(completionHandler:) can take 500ms+; bridge via continuation
                        writer.finishWriting {
                            if let err = writer.error {
                                NSLog("[GaimerScreenRecording] finishWriting error: %@",
                                      err.localizedDescription)
                            } else {
                                NSLog("[GaimerScreenRecording] Recording finalized at: %@",
                                      writer.outputURL.path)
                            }
                            self.cleanupState()
                            continuation.resume()
                        }
                    }
                }

                // Back to recordingQueue for the external completion callback
                self.recordingQueue.async {
                    completion()
                }
            }
        }
    }

    // MARK: - rotate (gapless segment swap)

    /// Prepare a new AVAssetWriter for the given path; the actual swap happens in the
    /// frame callback so no frames are dropped.  `completion` fires once the OLD segment
    /// has been finalized on disk.
    func rotate(newOutputPath: String, completion: @escaping @convention(c) () -> Void) {
        recordingQueue.async { [weak self] in
            guard let self = self else {
                completion()
                return
            }

            guard self.status == kStatusRecording else {
                NSLog("[GaimerScreenRecording] rotate() ignored — not recording (status=%d)", self.status)
                completion()
                return
            }

            guard !self.rotationPending else {
                NSLog("[GaimerScreenRecording] rotate() ignored — rotation already pending")
                completion()
                return
            }

            do {
                let outputURL = URL(fileURLWithPath: newOutputPath)
                let fm = FileManager.default
                if fm.fileExists(atPath: newOutputPath) {
                    try fm.removeItem(at: outputURL)
                }

                let writer = try AVAssetWriter(outputURL: outputURL, fileType: .mp4)
                writer.movieFragmentInterval = CMTime(seconds: 10.0, preferredTimescale: 600)
                writer.shouldOptimizeForNetworkUse = false

                // Derive width/height from the active input; fall back to 1920x1080
                var width  = 1920
                var height = 1080
                if let currentInput = self.assetWriterInput {
                    let settings = currentInput.outputSettings ?? [:]
                    if let w = settings[AVVideoWidthKey] as? Int,
                       let h = settings[AVVideoHeightKey] as? Int {
                        width  = w
                        height = h
                    }
                }

                let videoSettings: [String: Any] = [
                    AVVideoCodecKey:  AVVideoCodecType.hevc,
                    AVVideoWidthKey:  width,
                    AVVideoHeightKey: height,
                    AVVideoCompressionPropertiesKey: [
                        AVVideoAverageBitRateKey:            5_000_000,
                        AVVideoExpectedSourceFrameRateKey:   30,
                        AVVideoProfileLevelKey:              kVTProfileLevel_HEVC_Main_AutoLevel,
                        AVVideoAllowFrameReorderingKey:      false
                    ] as [String: Any]
                ]

                let input = AVAssetWriterInput(mediaType: .video, outputSettings: videoSettings)
                input.expectsMediaDataInRealTime = true

                let adaptorAttrs: [String: Any] = [
                    kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA,
                    kCVPixelBufferWidthKey  as String: width,
                    kCVPixelBufferHeightKey as String: height
                ]
                let adaptor = AVAssetWriterInputPixelBufferAdaptor(
                    assetWriterInput: input,
                    sourcePixelBufferAttributes: adaptorAttrs
                )

                guard writer.canAdd(input) else {
                    NSLog("[GaimerScreenRecording] rotate: AVAssetWriter cannot add video input")
                    completion()
                    return
                }
                writer.add(input)

                guard writer.startWriting() else {
                    NSLog("[GaimerScreenRecording] rotate: startWriting() failed: %@",
                          writer.error?.localizedDescription ?? "unknown")
                    completion()
                    return
                }

                // Stash pending state — the frame callback will perform the atomic swap
                self.pendingWriter   = writer
                self.pendingInput    = input
                self.pendingAdaptor  = adaptor
                self.rotationCompletion = completion
                self.rotationPending = true

                NSLog("[GaimerScreenRecording] Rotation prepared → %@", newOutputPath)

            } catch {
                NSLog("[GaimerScreenRecording] rotate() failed: %@", error.localizedDescription)
                completion()
            }
        }
    }

    // MARK: - SCStreamOutput

    /// Called on recordingQueue for every captured video frame.
    func stream(
        _ stream: SCStream,
        didOutputSampleBuffer sampleBuffer: CMSampleBuffer,
        of outputType: SCStreamOutputType
    ) {
        guard outputType == .screen else { return }

        // --- Gapless rotation swap ---
        // If a pending writer is ready, atomically swap it in before appending.
        if rotationPending,
           let newWriter  = pendingWriter,
           let newInput   = pendingInput,
           let newAdaptor = pendingAdaptor {

            // Capture old writer refs for async finalization
            let oldWriter = assetWriter
            let oldInput  = assetWriterInput
            let oldCompletion = rotationCompletion

            // Atomic swap: pending → active
            assetWriter        = newWriter
            assetWriterInput   = newInput
            pixelBufferAdaptor = newAdaptor
            sessionStarted     = false

            // Clear pending state
            pendingWriter      = nil
            pendingInput       = nil
            pendingAdaptor     = nil
            rotationPending    = false
            rotationCompletion = nil

            NSLog("[GaimerScreenRecording] Rotation swap performed")

            // Finalize old writer asynchronously
            Task.detached(priority: .utility) {
                oldInput?.markAsFinished()
                if let ow = oldWriter, ow.status == .writing {
                    await withCheckedContinuation { (cont: CheckedContinuation<Void, Never>) in
                        ow.finishWriting {
                            if let err = ow.error {
                                NSLog("[GaimerScreenRecording] Old segment finishWriting error: %@",
                                      err.localizedDescription)
                            } else {
                                NSLog("[GaimerScreenRecording] Old segment finalized: %@",
                                      ow.outputURL.path)
                            }
                            cont.resume()
                        }
                    }
                }
                oldCompletion?()
            }
        }

        guard let input = assetWriterInput,
              let writer = assetWriter,
              let adaptor = pixelBufferAdaptor,
              writer.status == .writing else { return }

        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        let pts = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)

        // Start the writer session anchored to the first frame's presentation time
        if !sessionStarted {
            writer.startSession(atSourceTime: pts)
            sessionStarted = true
            lastLogTime = CFAbsoluteTimeGetCurrent()
            frameCount = 0
            let w = CVPixelBufferGetWidth(pixelBuffer)
            let h = CVPixelBufferGetHeight(pixelBuffer)
            NSLog("[GaimerScreenRecording] Writer session started at PTS %.3f (%dx%d)",
                  CMTimeGetSeconds(pts), w, h)
        }

        // Append via pixel buffer adaptor (handles IOSurface → encoder transfer)
        if input.isReadyForMoreMediaData {
            if !adaptor.append(pixelBuffer, withPresentationTime: pts) {
                NSLog("[GaimerScreenRecording] Failed to append; writer status=%d, error=%@",
                      writer.status.rawValue,
                      writer.error?.localizedDescription ?? "nil")
            } else {
                // FPS logging every 5 seconds
                frameCount += 1
                let now = CFAbsoluteTimeGetCurrent()
                let elapsed = now - lastLogTime
                if elapsed >= 5.0 {
                    let fps = Double(frameCount) / elapsed
                    NSLog("[GaimerScreenRecording] FPS: %.1f (%d frames in %.1fs)",
                          fps, frameCount, elapsed)
                    frameCount = 0
                    lastLogTime = now
                }
            }
        }
    }

    // MARK: - SCStreamDelegate

    /// Called if the stream stops unexpectedly (e.g. permission revoked, window closed).
    func stream(_ stream: SCStream, didStopWithError error: Error) {
        NSLog("[GaimerScreenRecording] Stream stopped with error: %@", error.localizedDescription)
        setStatus(kStatusError)
        // Attempt graceful teardown so the partial file is usable
        self.assetWriterInput?.markAsFinished()
        self.assetWriter?.finishWriting { [weak self] in
            self?.cleanupState()
        }
    }

    // MARK: - Private helpers

    private func cleanupState() {
        // Release energy assertion so macOS can resume normal scheduling
        if let token = activityToken {
            ProcessInfo.processInfo.endActivity(token)
            activityToken = nil
        }

        stream              = nil
        assetWriter         = nil
        assetWriterInput    = nil
        pixelBufferAdaptor  = nil
        sessionStarted      = false
        frameCount          = 0
        lastLogTime         = 0

        // Clear pending rotation state
        pendingWriter?.cancelWriting()
        pendingWriter       = nil
        pendingInput        = nil
        pendingAdaptor      = nil
        rotationPending     = false
        if let c = rotationCompletion { c() }
        rotationCompletion  = nil

        // Only reset to idle if we weren't already in error state
        let current = _status
        if current == kStatusRecording {
            OSAtomicCompareAndSwap32Barrier(current, kStatusIdle, &_status)
        }
    }
}
#endif // canImport(ScreenCaptureKit)

// MARK: - @_cdecl C-callable wrappers

/// Begin recording the specified window to a file at outputPath.
/// Returns true if recording started successfully.
/// Recording is async; poll sck_recording_status() to confirm kStatusRecording.
@_cdecl("sck_start_recording")
public func sckStartRecording(
    windowID:   UInt32,
    outputPath: UnsafePointer<CChar>,
    width:      Int32,
    height:     Int32
) -> Bool {
    #if canImport(ScreenCaptureKit)
    if #available(macCatalyst 18.2, macOS 14.0, *) {
        let path = String(cString: outputPath)
        let w = Int(width)
        let h = Int(height)
        Task.detached(priority: .userInitiated) {
            _ = await ScreenRecorder.shared.start(
                windowID:   windowID,
                outputPath: path,
                width:      w,
                height:     h
            )
        }
        return true
    }
    #endif
    return false
}

/// Stop the active recording. completion is called (on an internal queue)
/// once the output file has been fully finalized.
/// completion MUST be a C function pointer (@convention(c)) so .NET P/Invoke can pass a delegate.
@_cdecl("sck_stop_recording")
public func sckStopRecording(
    completion: @escaping @convention(c) () -> Void
) {
    #if canImport(ScreenCaptureKit)
    if #available(macCatalyst 18.2, macOS 14.0, *) {
        ScreenRecorder.shared.stop(completion: completion)
        return
    }
    #endif
    completion()
}

/// Returns the current recording status:
///   0 = idle, 1 = recording, 2 = error
@_cdecl("sck_recording_status")
public func sckRecordingStatus() -> Int32 {
    #if canImport(ScreenCaptureKit)
    if #available(macCatalyst 18.2, macOS 14.0, *) {
        return ScreenRecorder.shared.status
    }
    #endif
    return kStatusIdle
}

/// Rotate to a new segment file without dropping frames.
/// completion is called (on an internal queue) once the OLD segment has been finalized.
/// completion MUST be a C function pointer (@convention(c)) so .NET P/Invoke can pass a delegate.
@_cdecl("sck_rotate_segment")
public func sckRotateSegment(
    newOutputPath: UnsafePointer<CChar>,
    completion: @escaping @convention(c) () -> Void
) {
    #if canImport(ScreenCaptureKit)
    if #available(macCatalyst 18.2, macOS 14.0, *) {
        let path = String(cString: newOutputPath)
        ScreenRecorder.shared.rotate(newOutputPath: path, completion: completion)
        return
    }
    #endif
    completion()
}
