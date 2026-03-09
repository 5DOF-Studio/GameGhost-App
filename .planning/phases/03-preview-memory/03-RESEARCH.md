# Phase 03: Preview Image Memory Management - Research

**Researched:** 2026-02-25
**Domain:** .NET MAUI ImageSource lifecycle, byte array memory management, screen capture preview rendering
**Confidence:** HIGH

## Summary

The GaimerDesktop app captures screenshots every 5 seconds (MacCatalyst) or 1 second (Windows) and stores raw PNG bytes in `MainViewModel.PreviewImage` (a `byte[]` property). Each time a new frame arrives, the previous byte array reference is simply overwritten with no explicit cleanup of the old `ImageSource`. While the GC will eventually collect unreferenced byte arrays, the `ImageSource.FromStream()` pattern creates `StreamImageSource` objects that are known to leak in MAUI when rapidly updated -- this is a confirmed open issue (dotnet/maui#23574).

The current codebase has three distinct memory concerns: (1) the raw `byte[]` backing field accumulates without explicit nulling of intermediate references, (2) the computed `PreviewImageSource` property creates a new `StreamImageSource` + `MemoryStream` on every access without cancelling/disposing the previous one, and (3) the compressed JPEG copy sent to the conversation provider is fire-and-forget with no lifecycle management. The good news is that at the current capture rates (5s/1s intervals), the leak is slow -- not the 60fps scenario from the bug report. But over a 30-60 minute gaming session, this can accumulate to 50-300MB of leaked memory.

**Primary recommendation:** Implement a "swap and cancel" pattern in the ViewModel: when a new frame arrives, cancel the previous `StreamImageSource` via `ImageSource.Cancel()`, null the old byte array reference, then assign the new one. Additionally, downscale the preview image before storing (the preview box is only 180px tall, so a full-resolution capture is wasteful).

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET MAUI | 8.0 | UI framework | Already in use; ImageSource lifecycle is framework-level |
| SkiaSharp | 2.88+ | Image processing | Already in use for capture and compression |
| CommunityToolkit.Mvvm | 8.x | MVVM source generators | Already in use; `[ObservableProperty]` pattern |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| ImageSource.Cancel() | Built-in | Cancel pending stream loads | When replacing an ImageSource before the previous one finishes loading |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| StreamImageSource | FileImageSource with temp file | Avoids memory entirely but adds disk I/O; not worth it at 5s intervals |
| ImageSource.FromStream | SkiaSharp SKCanvasView | Direct rendering bypasses MAUI image pipeline; much more complex, only needed for 60fps+ |
| Manual byte[] management | ArrayPool<byte> | Reduces GC pressure for large arrays, but adds complexity; overkill at current capture rate |

**Installation:**
No additional packages needed. All required APIs are already available in the current project dependencies.

## Architecture Patterns

### Current Flow (Problem)
```
Timer tick (5s)
  -> CaptureDisplayAndCrop() returns byte[] (~580KB PNG)
  -> FrameCaptured event fires with raw byte[]
  -> MainViewModel handler:
     1. Creates ReelMoment (metadata only, no image data)
     2. Sets PreviewImage = rawFrame (overwrites old ref)
     3. OnPreviewImageChanged triggers OnPropertyChanged(PreviewImageSource)
     4. XAML binding calls PreviewImageSource getter
     5. Getter creates NEW MemoryStream + NEW StreamImageSource EVERY TIME
     6. Old StreamImageSource may still be referenced by Image control handler
     7. Compressed copy sent to conversation provider (separate byte[])
```

### Recommended Fix Pattern: "Cancel-Swap-Notify"
```
Timer tick (5s)
  -> CaptureDisplayAndCrop() returns byte[]
  -> FrameCaptured event fires
  -> MainViewModel handler:
     1. Cancel previous ImageSource (if any)
     2. Null out old PreviewImage reference
     3. Downscale rawFrame for preview (SkiaSharp, ~180px height)
     4. Assign new PreviewImage = downscaledFrame
     5. Property change triggers new PreviewImageSource
     6. Full-res rawFrame used for model send, then falls out of scope
```

### Pattern 1: Cancel-and-Replace ImageSource
**What:** Before assigning a new image, explicitly cancel the old StreamImageSource
**When to use:** Any time ImageSource is updated repeatedly on the same Image control
**Example:**
```csharp
// In MainViewModel
private ImageSource? _currentPreviewSource;

partial void OnPreviewImageChanged(byte[]? oldValue, byte[]? newValue)
{
    // Cancel the previous image source to release its stream
    _currentPreviewSource?.Cancel();

    if (newValue is { Length: > 0 })
    {
        _currentPreviewSource = ImageSource.FromStream(() => new MemoryStream(newValue));
    }
    else
    {
        _currentPreviewSource = null;
    }

    OnPropertyChanged(nameof(HasPreviewImage));
    OnPropertyChanged(nameof(PreviewImageSource));
}

public ImageSource? PreviewImageSource => _currentPreviewSource;
```
**Source:** Microsoft.Maui.Controls.ImageSource.Cancel() - inherited from ImageSource base class

### Pattern 2: Downscale for Preview
**What:** Scale the captured image to match the display size before storing
**When to use:** When the display area is significantly smaller than the capture resolution
**Example:**
```csharp
// In FrameCaptured handler, before assigning to PreviewImage
// Preview box is 180px tall; scale to ~180px height
var previewBytes = ImageProcessor.ScaleToHeight(rawFrame, 180);
PreviewImage = previewBytes; // ~15-30KB instead of ~580KB
```

### Pattern 3: Explicit Old-Frame Cleanup in Handler
**What:** Null the old reference before assigning the new one in the event handler
**When to use:** When the property setter does not automatically handle disposal
**Example:**
```csharp
_captureService.FrameCaptured += (_, rawFrame) =>
{
    // ... reel moment creation ...

    MainThread.BeginInvokeOnMainThread(() =>
    {
        // Explicitly clear old preview before assigning new
        var oldPreview = PreviewImage;
        PreviewImage = null; // Triggers property change, clears binding

        // Create downscaled preview
        var previewFrame = ImageProcessor.ScaleToHeight(rawFrame, 180);
        PreviewImage = previewFrame;

        _brainEventRouter.OnScreenCapture(frameRef, gameTime, "auto");
    });

    // rawFrame can now be used for model send and then GC'd
    if (_conversationProvider.IsConnected)
    {
        var compressed = Services.ImageProcessor.ScaleAndCompress(rawFrame);
        // ...
    }
};
```

### Anti-Patterns to Avoid
- **Creating ImageSource in a computed property getter:** The current `PreviewImageSource` getter creates a new StreamImageSource + MemoryStream every time the property is accessed (including during layout passes). This is the root cause of the leak. Store the ImageSource in a field instead.
- **Holding both raw and compressed copies simultaneously:** The current code holds the raw PNG in `PreviewImage` while also creating a compressed JPEG for the model. With downscaling for preview, both copies can be smaller.
- **Not cancelling previous StreamImageSource:** MAUI's Image control may retain a reference to the old ImageSource even after a new one is assigned. Always call `Cancel()` on the previous one.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Image scaling | Custom pixel manipulation | `ImageProcessor.ScaleAndCompress()` (already exists) or add `ScaleToHeight()` variant | SkiaSharp handles color space, DPI, format correctly |
| Stream lifecycle | Manual MemoryStream tracking | `ImageSource.Cancel()` built-in method | Framework knows its own internal references |
| Memory profiling | Manual byte counting | `dotnet-trace` + `dotnet-gcdump` | System-level GC analysis is more accurate than manual tracking |
| Weak image caching | Custom LRU cache | MAUI's built-in image caching (for FileImageSource) | Not needed for stream-based single-image preview |

**Key insight:** The preview is a single-image display (always showing the latest frame). This is dramatically simpler than a gallery or list of images. The "keep only latest" pattern means we never need caching, pooling, or ring buffers -- just clean swap-and-release.

## Common Pitfalls

### Pitfall 1: StreamImageSource Leak on Rapid Update
**What goes wrong:** Each call to `ImageSource.FromStream()` creates a new `StreamImageSource` that holds a delegate reference to the `MemoryStream` factory. If the Image control's handler retains a reference to the old source (which it does in some MAUI platform implementations), the old `MemoryStream` and its backing `byte[]` are never collected.
**Why it happens:** Known MAUI framework issue (dotnet/maui#23574). The Image control's platform handler does not always release the previous `ImageSource` before loading the new one.
**How to avoid:** (1) Call `Cancel()` on the old ImageSource before replacing. (2) Store the ImageSource in a backing field rather than creating it in a computed getter. (3) Keep capture rates reasonable (5s is fine; 60fps would be catastrophic).
**Warning signs:** Memory usage grows linearly with session duration without plateauing.

### Pitfall 2: Computed Property Creates New Objects Per Access
**What goes wrong:** The current `PreviewImageSource` is a computed property (`=> ImageSource.FromStream(...)`). This means every time XAML binds to it, a **new** StreamImageSource and MemoryStream are created. During layout passes, this property may be accessed multiple times.
**Why it happens:** MVVM pattern of returning computed values from getters seems clean but is dangerous for mutable resource-backed objects.
**How to avoid:** Change to a stored field that is updated explicitly in `OnPreviewImageChanged`.
**Warning signs:** `new MemoryStream(PreviewImage)` appearing in getter of a bound property.

### Pitfall 3: MemoryStream Not Disposed After Image Load
**What goes wrong:** `ImageSource.FromStream(() => new MemoryStream(bytes))` creates a lambda that produces a new MemoryStream. The StreamImageSource is supposed to dispose the stream after loading, but if loading is cancelled or fails, the stream may leak.
**Why it happens:** The lambda factory pattern means the caller has no handle to the created stream.
**How to avoid:** Accept this is a framework concern. Mitigate by keeping the byte arrays small (downscaled preview) so even leaked streams are cheap.
**Warning signs:** Growing `MemoryStream` count in memory profiler.

### Pitfall 4: Full-Resolution PNG Stored for 180px Preview
**What goes wrong:** A 1920x1080 screenshot captured at PNG quality (~580KB+) is stored in `PreviewImage` just to display in a 180px-tall preview box. This wastes ~500KB per frame.
**Why it happens:** The capture service returns full-res PNG; nobody downscales before assigning to preview.
**How to avoid:** Add a `ScaleToHeight` method to `ImageProcessor` and downscale before storing in `PreviewImage`. Send full-res to the model separately.
**Warning signs:** `PreviewImage` byte arrays consistently 400KB+ for a thumbnail-sized display.

### Pitfall 5: Thread Safety of byte[] Assignment
**What goes wrong:** `FrameCaptured` fires on a Timer thread. The handler marshals to `MainThread` for UI property assignment, but the raw `byte[]` reference is captured by closure from the background thread. If two captures overlap (unlikely at 5s but possible), a race could occur.
**How to avoid:** The current code already marshals to MainThread correctly. The `byte[]` itself is immutable once created (SkiaSharp `.ToArray()` returns a new array). This is currently safe.
**Warning signs:** Garbled or blank preview images (would indicate a race).

## Code Examples

Verified patterns from the current codebase and MAUI documentation:

### Current PreviewImage Pattern (PROBLEMATIC)
```csharp
// Source: MainViewModel.cs lines 65-71
[ObservableProperty]
private byte[]? _previewImage;

public ImageSource? PreviewImageSource => PreviewImage is { Length: > 0 }
    ? ImageSource.FromStream(() => new MemoryStream(PreviewImage))
    : null;
// PROBLEM: Creates new StreamImageSource + MemoryStream on EVERY access
```

### Recommended Fix: Cached ImageSource with Cancel
```csharp
// Replace computed getter with stored field
[ObservableProperty]
private byte[]? _previewImage;

private ImageSource? _previewImageSource;

public ImageSource? PreviewImageSource => _previewImageSource;

partial void OnPreviewImageChanged(byte[]? oldValue, byte[]? newValue)
{
    // Cancel and release old source
    _previewImageSource?.Cancel();
    _previewImageSource = null;

    if (newValue is { Length: > 0 })
    {
        _previewImageSource = ImageSource.FromStream(() => new MemoryStream(newValue));
    }

    OnPropertyChanged(nameof(HasPreviewImage));
    OnPropertyChanged(nameof(PreviewImageSource));
}
```

### Adding ScaleToHeight to ImageProcessor
```csharp
// New method for preview-sized images
public static byte[] ScaleToHeight(byte[] imageData, int targetHeight,
    int jpegQuality = DefaultJpegQuality)
{
    using var original = SKBitmap.Decode(imageData);
    if (original is null || original.Height <= 0) return imageData;

    // Maintain aspect ratio
    var scale = (float)targetHeight / original.Height;
    if (scale >= 1.0f) return imageData; // Already small enough

    var newWidth = (int)(original.Width * scale);
    var newHeight = targetHeight;

    using var scaled = original.Resize(
        new SKImageInfo(newWidth, newHeight),
        new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    if (scaled is null) return imageData;

    using var image = SKImage.FromBitmap(scaled);
    using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);
    return data.ToArray();
}
```

### Updated FrameCaptured Handler
```csharp
_captureService.FrameCaptured += (_, rawFrame) =>
{
    // Metadata for brain context pipeline (no image bytes stored)
    var sourceTarget = _captureService.CurrentTarget != null
        ? $"{_captureService.CurrentTarget.ProcessName}|{_captureService.CurrentTarget.WindowTitle}"
        : "unknown";
    var frameRef = Guid.NewGuid().ToString();
    var moment = new ReelMoment { /* ... */ };
    _visualReelService.Append(moment);

    var gameTime = DateTime.UtcNow - _sessionStartedAt;

    // Downscale for preview display (180px height box)
    var previewFrame = ImageProcessor.ScaleToHeight(rawFrame, 360); // 2x for Retina

    MainThread.BeginInvokeOnMainThread(() =>
    {
        PreviewImage = previewFrame;
        _brainEventRouter.OnScreenCapture(frameRef, gameTime, "auto");
    });

    // Full-res compression for AI model (separate from preview)
    if (_conversationProvider.IsConnected)
    {
        var compressed = Services.ImageProcessor.ScaleAndCompress(rawFrame);
        if (compressed.Length > 0)
        {
            _ = _conversationProvider.SendImageAsync(compressed)
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                    $"[Capture] SendImageAsync failed: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
        }
    }
    // rawFrame (full-res) falls out of scope here and becomes GC-eligible
};
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `ImageSource.FromStream` in computed getter | Store ImageSource in field, call `Cancel()` on old | Ongoing best practice (MAUI 8+) | Prevents StreamImageSource leak |
| Full-res byte[] for all purposes | Separate preview (downscaled) and model (compressed) copies | Standard for capture apps | Reduces per-frame memory from ~580KB to ~30KB for preview |
| No cleanup on session end | Null PreviewImage + Cancel ImageSource in StopSessionAsync | Already partially done (line 485) | Prevents post-session memory retention |

**Deprecated/outdated:**
- The computed `PreviewImageSource` getter pattern was never officially recommended; it was a shorthand that worked for static images but fails for frequently updated streams.

## Risk Assessment

### Low Risk
- **Fix is surgical:** Only 3 files need changes (MainViewModel.cs, ImageProcessor.cs, FrameCaptured handler). No architectural changes required.
- **No new dependencies:** All fixes use existing SkiaSharp + MAUI APIs.
- **StopSessionAsync already clears preview:** Line 485 sets `PreviewImage = null`, which is the correct cleanup on disconnect. Just needs the Cancel() call added.

### Medium Risk
- **MAUI StreamImageSource bug is unfixed:** dotnet/maui#23574 is open with no fix. Our Cancel() + field storage pattern is a mitigation, not a guaranteed fix. At 5s capture intervals, this is acceptable.
- **Retina scaling factor:** The preview box is 180px in XAML logical units. On Retina displays (2x), this is 360 physical pixels. Downscaling to 360px height covers this, but Windows HiDPI may vary. Should verify on both platforms.

### Not a Risk
- **Thread safety:** Already handled correctly via `MainThread.BeginInvokeOnMainThread`.
- **Capture rate:** 5s (Mac) / 1s (Win) intervals are slow enough that even without fixes, the app won't crash in a typical session. But the fix prevents gradual degradation.

## Future Virtualization Path

The current "keep only latest" pattern is phase-appropriate. When the Brain Context Pipeline matures, the architecture should evolve:

### Current (Phase 03): Simple Delete-Previous
```
Capture -> Preview (latest only) -> Model Send -> GC
```

### Future: Brain Consumption Pipeline
```
Capture -> Frame Queue (bounded, ~5 frames)
              |
              +-> Preview (always latest)
              +-> BrainContextService.ConsumeFrame()
                    |
                    +-> Extract insights
                    +-> Update ReelMoment with EventRef
                    +-> Release frame bytes
```

**Key design points for future:**
1. `ReelMoment` already has `FrameRef` field -- this will become a key into a frame store
2. `VisualReelService` already has time-based and count-based trimming (MaxCount=500, MaxAgeSeconds=300)
3. The brain would "consume" frames by reading them from the store, extracting insights, and then releasing the bytes
4. The `FrameRef` metadata persists (for timeline/history) even after the image bytes are released
5. `BrainContextService` already assembles context envelopes with reel refs -- it just needs the actual frame bytes to be available during assembly

**No work needed now.** The current fix (downscale preview, cancel old source, null old bytes) is the correct foundation. The frame store can be added when the brain pipeline needs to hold multiple frames for batch analysis.

## Open Questions

Things that couldn't be fully resolved:

1. **Exact memory savings from downscaling**
   - What we know: Full PNG is ~580KB; JPEG at 180px height should be ~15-30KB
   - What's unclear: Actual size depends on image complexity (busy game screenshots vs chess boards)
   - Recommendation: Add debug logging to measure actual sizes on first implementation

2. **Windows capture interval mismatch**
   - What we know: Windows `WindowCaptureService` captures at 1s intervals (line 15); Mac at 5s
   - What's unclear: Whether 1s was intentional or a placeholder. More frequent captures make the memory fix more important on Windows.
   - Recommendation: Verify the intended interval. Consider aligning both to 5s for consistency.

3. **ImageSource.Cancel() effectiveness across platforms**
   - What we know: `Cancel()` is inherited from `ImageSource` base class and is documented
   - What's unclear: Whether it actually releases platform-specific resources on MacCatalyst vs Windows consistently (given the open MAUI bug)
   - Recommendation: Implement Cancel() as belt-and-suspenders alongside nulling. Profile with `dotnet-gcdump` after implementation.

## Sources

### Primary (HIGH confidence)
- Codebase inspection: `MainViewModel.cs`, `WindowCaptureService.cs` (MacCatalyst + Windows), `ImageProcessor.cs`, `VisualReelService.cs`, `BrainContextService.cs` -- direct code analysis
- [Microsoft MAUI Performance Guide](https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance?view=net-maui-9.0) -- official guidance on image resource management, stream lifecycle
- [StreamImageSource API Reference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.maui.controls.streamimagesource?view=net-maui-10.0) -- Cancel() method, Stream property, class hierarchy

### Secondary (MEDIUM confidence)
- [dotnet/maui#23574](https://github.com/dotnet/maui/issues/23574) -- Confirmed memory leak when updating ImageSource from ViewModel with StreamImageSource. Open issue, no official fix. Verified as reproducible.
- [MAUI 2025 Memory Leak Guide](https://markaicode.com/maui-memory-leaks-2025/) -- Community guide confirming image handling as top leak source

### Tertiary (LOW confidence)
- General WebSearch findings on Camera.MAUI patterns for frame disposal -- confirms "dispose previous before assigning new" as community consensus, but specific implementations vary

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Direct codebase inspection, no new dependencies needed
- Architecture: HIGH - Surgical fix to existing pattern, well-understood ImageSource lifecycle
- Pitfalls: HIGH - Confirmed by open MAUI issue with reproduction, codebase analysis shows exact problematic pattern
- Future virtualization: MEDIUM - Architectural sketch based on existing service interfaces, not yet validated with real brain consumption

**Research date:** 2026-02-25
**Valid until:** 2026-04-25 (stable domain; MAUI ImageSource API unlikely to change significantly)
