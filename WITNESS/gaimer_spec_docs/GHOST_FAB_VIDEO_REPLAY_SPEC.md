# Ghost FAB Video Replay Spec

**Date:** 2026-04-06
**Status:** Brainstorm → Ready for Design
**Depends on:** Replay Recording Phase 1 (complete), Ghost FAB spine card (complete)

---

## Overview

Enable gaming agents to trigger short video replays directly in the Ghost FAB overlay. The agent calls `show_replay(timestamp, duration)` and the spine card renders an inline video player — same visual slot as image cards, but with AVPlayer-backed playback.

## Motivation

Agents already analyze gameplay via Gemini and can reference moments via `search_replay`. But the result is text. A visual replay closes the loop: the agent says "look at this" and the user sees the actual moment. This is the difference between "you died to a flanker at 2:15" and showing the 30-second clip of it happening.

## Design Principles

1. **Quick glance, not a media player** — Ghost FAB is an overlay. Replays are 30s max, auto-play, minimal controls.
2. **Zero extraction cost** — Play directly from existing segment files with AVPlayer seek + forwardPlaybackEndTime. No ffmpeg, no temp files, no re-encoding.
3. **Same card slot** — Video uses the same spine card area as text/image. One content type visible at a time.
4. **Muted by default** — The overlay shouldn't blast game audio over the agent's voice. User can unmute via tap-and-hold or future control.

## Architecture

### Data Flow

```
Agent calls show_replay(timestamp, duration)
    → ToolExecutor resolves timestamp to segment file + seek offset
    → IGhostModeService.ShowVideoCard(filePath, startTime, duration, title)
    → P/Invoke: ghost_panel_show_video(path, start, duration, title)
    → SpineCardView.showSpineVideo() — AVPlayerLayer in spine card area
```

### Timestamp Resolution

The tool accepts three timestamp forms:

| Form | Example | Resolution |
|---|---|---|
| Absolute | `"2:15"` | Session-relative time → segment file + offset via ReplayRecordingService segment index |
| Relative | `"now-30s"` | Current session time minus offset → same resolution |
| Anchor | `"last_kill"` | Query SqliteSegmentAnalysisStore for most recent matching event → timestamp → same resolution |

Resolution logic lives in `ToolExecutor` (C# managed side), not in Swift. The native side receives a resolved file path + start time.

### Segment File Mapping

Current recording state:
- 2:30 segments, 2-segment rolling buffer (~5 min coverage)
- HEVC Main codec, fragmented MP4, ~5Mbps, ~89MB per segment
- Stored at `~/Library/replays/{sessionId}/segment-{N}.mp4`

Mapping: `ReplayRecordingService` tracks segment start timestamps. Given a target timestamp:
1. Find which segment contains the target time
2. Calculate seek offset = target_time - segment_start_time
3. Return (segment file path, seek offset)

Edge case: if the target time spans a segment boundary, clamp to the segment that contains the start. Don't attempt cross-segment stitching in V1.

### Ghost FAB Card Content Model (Swift)

New enum case:

```swift
public enum GhostFabCardContent {
    case none
    case text(title: String?, message: String, eventIcon: NSImage?, fixedHeight: CGFloat?, isAlert: Bool)
    case image(title: String?, image: NSImage, fixedHeight: CGFloat, isAlert: Bool)
    case video(title: String?, fileURL: URL, startTime: TimeInterval, duration: TimeInterval)
}
```

### SpineCardView Additions

New subview:

```swift
// AVPlayerLayer-backed view for video playback in spine card
private var playerLayer: AVPlayerLayer?
private var player: AVPlayer?
```

New method:

```swift
public func showSpineVideo(url: URL, startTime: TimeInterval, duration: TimeInterval, height: CGFloat, completion: (() -> Void)?)
```

Behavior:
- Create AVPlayer with AVPlayerItem from URL
- Seek to CMTime(seconds: startTime)
- Set forwardPlaybackEndTime to CMTime(seconds: startTime + duration)
- Add AVPlayerLayer to spine card layer tree (above gradients, below corner mask)
- Layer contentsGravity = .resizeAspectFill (matches image behavior)
- Auto-play immediately, muted
- On playback end: freeze on last frame (no loop by default)

Cleanup:
- On card dismiss: player.pause(), remove layer, nil references
- On new card (any type): same cleanup

### P/Invoke Bridge

New C export:

```c
@_cdecl("ghost_panel_show_video")
public func ghost_panel_show_video(
    _ filePath: UnsafePointer<CChar>,
    _ startTime: Double,
    _ duration: Double,
    _ title: UnsafePointer<CChar>?
)
```

C# P/Invoke:

```csharp
[DllImport("GaimerGhostMode")]
private static extern void ghost_panel_show_video(
    string filePath, double startTime, double duration, string? title);
```

### Voice Agent Tool Definition

```json
{
    "name": "show_replay",
    "description": "Show a short video replay clip on the overlay. Use when the user asks to see something that just happened, or when pointing out a notable moment.",
    "parameters": {
        "type": "object",
        "properties": {
            "timestamp": {
                "type": "string",
                "description": "When to start the clip. Absolute session time ('2:15'), relative ('now-30s'), or anchor ('last_kill', 'last_death')."
            },
            "duration": {
                "type": "integer",
                "description": "Clip length in seconds. Default 30, max 60.",
                "default": 30
            },
            "title": {
                "type": "string",
                "description": "Optional title shown above the video (e.g. 'THAT FLANK', 'WATCH THIS')"
            }
        },
        "required": ["timestamp"]
    }
}
```

### 30-Second Cap Rationale

| Concern | Why 30s default, 60s max |
|---|---|
| Memory | 30s HEVC @ 5Mbps = ~18MB in AVPlayer buffer. Comfortable. |
| Attention | Ghost overlay = quick glance. 30s matches "look at this" attention span. |
| Segment boundaries | 2:30 segments. 30s clip never spans more than 1 segment (simplifies V1). |
| Future flexibility | Tool accepts duration param. Ghost FAB caps at 60s. Longer replays can route to main chat. |

## Open Questions (Resolve During Design Phase)

1. **Playback end behavior** — Freeze on last frame vs loop vs auto-dismiss after 2s pause?
   - Recommendation: Freeze on last frame. Auto-dismiss feels abrupt. Looping is annoying. Let agent or user dismiss.

2. **Progress indicator** — Thin bar at bottom of spine showing playback position?
   - Recommendation: Yes, minimal. 2px cyan bar that fills left-to-right. No scrubbing in V1.

3. **Tap interaction** — What does tapping the video do?
   - Recommendation: Tap = replay from start. Long press = future (unmute, expand, etc.).

4. **Audio** — Always muted, or muted-by-default with unmute gesture?
   - Recommendation: Always muted in V1. Agent is likely talking. Unmute is V2.

5. **Multiple clips** — Agent triggers show_replay twice in quick succession?
   - Recommendation: Latest wins (same as text/image cards). Previous card dismissed, new one shows.

6. **Main chat parity** — When do we build the MAUI-side media card?
   - Recommendation: Separate phase. Ghost FAB first (pure AppKit, simpler). Main chat follows same data model but renders in MAUI CollectionView with a MediaCardTemplate.

7. **Harness testing** — How to test video in GhostModeHarness?
   - Add a "Show Video Card" button that plays a bundled test clip or one of the existing replay segments.

## Implementation Plan (Rough)

### Phase A: Swift Native (Ghost FAB)
1. Add `.video` case to `GhostFabCardContent`
2. Add AVPlayerLayer to SpineCardView
3. Implement `showSpineVideo()` / cleanup
4. Add `ghost_panel_show_video` C export
5. Wire through GhostFabPanelSDK → StateController → ContentView
6. Add progress bar (2px, animated)
7. Harness: "Show Video Card" button with test segment

### Phase B: C# Managed Side
1. Add `ghost_panel_show_video` P/Invoke to GhostModeNativeMethods
2. Add `ShowVideoCard(path, start, duration, title)` to IGhostModeService
3. Wire MacGhostModeService implementation

### Phase C: Tool Integration
1. Define `show_replay` tool in tool definitions
2. Implement ToolExecutor handler:
   - Resolve timestamp → segment + offset
   - Call IGhostModeService.ShowVideoCard
3. Add to agent tool schemas (all 3 agents)

### Phase D: Main Chat Media Card (Future)
1. New EventOutputType.VideoReplay / EventOutputType.ImageSnapshot
2. MediaCardTemplate in timeline (thumbnail + play icon)
3. Inline AVPlayer on tap (MAUI native view embed)
4. Same data model as ghost, different renderer

## Test Strategy

- **Harness:** Visual verification of video playback in spine card (Phase A)
- **Unit:** Timestamp resolution logic, segment mapping, edge cases (Phase C)
- **Integration:** End-to-end tool call → video display (Phase C)
- **Manual:** Live gameplay → agent triggers replay → visual verification
