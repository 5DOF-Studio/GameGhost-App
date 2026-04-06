// GaimerScreenCapture.swift
// Native Swift helper wrapping ScreenCaptureKit for P/Invoke consumption from .NET MAUI.
//
// Exports two C-callable functions via @_cdecl:
//   - sck_is_available() -> Bool
//   - sck_capture_window(windowID, width, height, callback)
//
// Uses ImageIO CGImageDestination for CGImage-to-PNG conversion (Mac Catalyst compatible).
// Does NOT use AppKit (unavailable on Mac Catalyst).

import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

#if canImport(ScreenCaptureKit)
import ScreenCaptureKit
#endif

// MARK: - CGImage to PNG conversion (Mac Catalyst compatible)

/// Converts a CGImage to PNG data using ImageIO's CGImageDestination.
/// This approach works on ALL Apple platforms including Mac Catalyst,
/// unlike NSBitmapImageRep which requires AppKit.
private func cgImageToPNGData(_ cgImage: CGImage) -> Data? {
    let data = NSMutableData()
    guard let dest = CGImageDestinationCreateWithData(
        data as CFMutableData,
        UTType.png.identifier as CFString,
        1,
        nil
    ) else {
        return nil
    }
    CGImageDestinationAddImage(dest, cgImage, nil)
    guard CGImageDestinationFinalize(dest) else {
        return nil
    }
    return data as Data
}

// MARK: - Exported C-callable functions

/// Returns true if ScreenCaptureKit is available on this system.
/// C# side should call this before attempting sck_capture_window.
/// Falls back gracefully on older macOS / Mac Catalyst versions.
@_cdecl("sck_is_available")
public func sckIsAvailable() -> Bool {
    if #available(macCatalyst 18.2, macOS 14.0, *) {
        return true
    }
    return false
}

/// Captures a single window by CGWindowID and returns PNG bytes via callback.
///
/// Parameters:
///   - windowID: The CGWindowID of the window to capture
///   - width: Desired capture width in pixels
///   - height: Desired capture height in pixels
///   - callback: C function pointer called with (pngBytesPointer, byteCount) on completion.
///               Called with (nil, 0) on failure.
///
/// The callback is invoked asynchronously. The C# side should use TaskCompletionSource
/// with RunContinuationsAsynchronously to bridge this to sync Wait().
///
/// IMPORTANT: Does NOT use @MainActor. Previous version dispatched to MainActor which
/// could deadlock when the C# side blocks with Task.Wait(). Instead, we use a plain
/// Task on the global executor. SCShareableContent and SCScreenshotManager work fine
/// from background threads — they only need an active run loop for SCStream, not for
/// single-frame captures.
@_cdecl("sck_capture_window")
public func sckCaptureWindow(
    windowID: UInt32,
    width: Int32,
    height: Int32,
    callback: @convention(c) (UnsafePointer<UInt8>?, Int32) -> Void
) {
    #if canImport(ScreenCaptureKit)
    if #available(macCatalyst 18.2, macOS 14.0, *) {
        // Use a detached Task to avoid inheriting any actor context.
        // This runs on the global concurrent executor (background thread).
        // SCScreenshotManager.captureImage works fine from background threads.
        Task.detached(priority: .userInitiated) {
            do {
                // Enumerate shareable content to find the target window
                let content = try await SCShareableContent.excludingDesktopWindows(
                    false, onScreenWindowsOnly: true
                )

                guard let window = content.windows.first(where: { $0.windowID == windowID }) else {
                    NSLog("[GaimerScreenCapture] Window %u not found in SCShareableContent", windowID)
                    callback(nil, 0)
                    return
                }

                // Create filter for single window capture
                let filter = SCContentFilter(desktopIndependentWindow: window)

                // Configure capture parameters
                let config = SCStreamConfiguration()
                config.width = Int(width)
                config.height = Int(height)
                // BGRA pixel format — SkiaSharp expects BGRA, NOT default YUV
                config.pixelFormat = kCVPixelFormatType_32BGRA
                // Retina quality capture
                config.captureResolution = .best
                config.showsCursor = false

                // Single-frame capture using SCScreenshotManager (macOS 14+ API)
                let cgImage = try await SCScreenshotManager.captureImage(
                    contentFilter: filter, configuration: config
                )

                // Convert CGImage to PNG using ImageIO (Mac Catalyst compatible)
                guard let pngData = cgImageToPNGData(cgImage) else {
                    NSLog("[GaimerScreenCapture] cgImageToPNGData failed")
                    callback(nil, 0)
                    return
                }

                // Pass PNG bytes back to C# via the callback
                pngData.withUnsafeBytes { rawBuffer in
                    guard let baseAddress = rawBuffer.bindMemory(to: UInt8.self).baseAddress else {
                        NSLog("[GaimerScreenCapture] Failed to bind PNG memory")
                        callback(nil, 0)
                        return
                    }
                    callback(baseAddress, Int32(pngData.count))
                }
            } catch {
                NSLog("[GaimerScreenCapture] SCK capture FAILED: %@", error.localizedDescription)
                // Log the full error for debugging
                NSLog("[GaimerScreenCapture] Error details: %@", String(describing: error))
                callback(nil, 0)
            }
        }
        return
    }
    #endif

    // SCK not available on this platform/version
    callback(nil, 0)
}

/// Helper to convert CGRect to string for logging (Mac Catalyst compatible).
private func NSStringFromCGRect(_ rect: CGRect) -> String {
    return "(\(rect.origin.x), \(rect.origin.y), \(rect.size.width), \(rect.size.height))"
}
