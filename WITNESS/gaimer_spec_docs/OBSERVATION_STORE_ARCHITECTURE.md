# Observation Store Architecture

**Project:** Gaimer Desktop / Witness Desktop  
**Date:** 2026-03-19  
**Status:** Planning direction after live capture stabilization review

---

## Summary

Gaimer's recent-gameplay promise should be implemented as a bounded local observation store rather than as transient in-memory frame handling or a stream of immediate brain requests.

The observation store is the source of truth for the last five minutes of gameplay:

- recent still-image observations today
- short video clips later
- local retrieval for "what happened a moment ago?"
- bounded context input for the brain

The brain is the interpreter of selected observations, not the archive.

---

## Problem Statement

Live testing exposed three linked problems in the current capture path:

1. Too much expensive work happens before the system knows whether a frame matters.
2. Too many accepted captures turn directly into brain requests and UI fanout.
3. The current `VisualReelService` is in-memory only, which does not satisfy the product promise of a searchable recent gameplay memory.

This causes:

- stale-message flooding
- unnecessary brain cost
- higher UI/main-thread pressure
- weak foundations for later short-video multimodal retrieval

---

## Architectural Decision

Introduce a bounded local observation store with:

- **SQLite** for metadata and retrieval index
- **filesystem artifacts** for thumbnails, analysis JPEGs, and later short clips
- **explicit retention** for a five-minute hot window
- **novelty/salience gating** between capture and brain submission

This architecture should support both:

- phase 1: still images
- phase 2: short video clips

without changing the top-level retrieval contract.

---

## Core Principles

1. **Captured is not analyzed.**
   A captured frame is first evaluated for storage and novelty, not immediately sent to the brain.

2. **The observation store is local-first.**
   Recent gameplay memory lives in app-owned local storage by default.

3. **The hot window is bounded.**
   Roughly five minutes of live observations remain queryable; older material is summarized or discarded.

4. **Latest-frame-wins for inference.**
   The live brain lane must not queue an unbounded backlog.

5. **One schema for stills and clips.**
   Future video support should extend the same observation model, not fork the architecture.

---

## Proposed Runtime Pipeline

```text
Native Capture
-> Cheap Signature / Diff
-> Observation Admission Policy
-> Local Observation Store
-> Novelty / Salience Gate
-> Brain Analysis (latest wins)
-> Brain Working Set
-> Timeline / Ghost / Voice fanout
```

Separately:

```text
User asks about recent gameplay
-> Observation query by time / tags / salience
-> Return observation refs and summaries
-> Optionally escalate selected refs to the brain
```

---

## Observation Model

Each observation should support a shared metadata contract.

Suggested fields:

- `Id`
- `SessionId`
- `TargetId`
- `AgentKey`
- `Kind` (`frame`, `clip`, `summary`)
- `CapturedAtUtc`
- `StartUtc`
- `EndUtc`
- `SourcePath`
- `ThumbnailPath`
- `VisualHash`
- `SceneSignature`
- `DiffScore`
- `Salience`
- `Status` (`raw`, `indexed`, `summarized`, `expired`)
- `MetadataJson`

Optional future links:

- `ParentObservationId`
- `BrainEventRef`
- `SummaryRef`

---

## Storage Layout

Root should live under app-local storage, for example under `FileSystem.AppDataDirectory`.

Suggested layout:

```text
observations/
  session-<id>/
    observations.db
    frames/
    thumbs/
    clips/
    summaries/
```

Direction:

- SQLite stores metadata and retrieval keys.
- Filesystem stores artifacts.
- Deterministic file naming should allow cleanup by session and time window.

---

## Capture Admission Policy

The capture lane should make a cheap decision before expensive transforms.

Possible outcomes:

- `drop`
- `store_only`
- `store_and_mark_salient`
- `store_and_send_to_brain`

Initial admission inputs:

- timestamp
- source target
- cheap visual hash
- diff score
- keepalive interval
- active mode / agent profile

For chess, later domain-specific signals can be layered in without changing the pipeline contract.

---

## Brain Working Set

`BrainContextService` should remain the bounded short-term context seam.

Extend it with an app-owned live working set:

- latest accepted visual state summary
- recent emitted insights (3-5)
- timestamps
- active mode / context
- selected recent observation refs

This working set supports:

- repeat suppression
- filler/no-op behavior
- lower token cost
- cleaner voice grounding

---

## Retention Policy

The observation store should obey explicit retention from day one.

Initial direction:

- hot window: 5 minutes
- artifact budget: bounded per session
- metadata cleanup: periodic
- stale artifacts: purge on expiry

Retention should align with:

- timeline hot window
- chat trimming
- ghost/UI transient state

`T18` should be treated as the UI-facing part of the same retention architecture, not a separate concern.

---

## Implementation Slices

### P0 — Stabilization

1. Introduce `ObservationStore` seams and retention config.
2. Add background writer lane for local observations.
3. Move novelty/admission gating earlier in the capture path.
4. Stop treating accepted capture as automatic brain submission.

### P1 — Queryable Recent Gameplay

1. Add query APIs by time window and salience.
2. Replace in-memory-only reel assumptions with observation refs.
3. Bind timeline/ghost diagnostics to stored observation metadata where useful.

### P2 — Richer Retrieval

1. Add derived summaries/tags.
2. Add semantic descriptors where helpful.
3. Add short clip artifacts and video-capable worker support.

---

## External Guidance

This direction is consistent with official guidance:

- SQLite is a valid application file format with indexing and extensibility: [SQLite As An Application File Format](https://www.sqlite.org/aff_short.html)
- WAL mode supports same-machine reader/writer concurrency and should be used carefully with checkpointing and single-writer expectations: [Write-Ahead Logging](https://sqlite.org/wal.html)
- .NET MAUI guidance supports local SQLite storage and async access to keep UI responsive: [Store local data with SQLite in a .NET MAUI app](https://learn.microsoft.com/en-us/training/modules/store-local-data/)
- Microsoft.Data.Sqlite guidance favors transactions and reused commands for write performance: [Transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions)

Inference from those sources:

- prefer a narrow local writer lane
- keep the number of concurrent writers low
- use explicit retention and cleanup
- keep large media artifacts out of the hot query path when possible

---

## Open Questions

1. Should the first stored artifact be analysis JPEG only, or both thumbnail + analysis JPEG?
2. Should the hot window cap be enforced by time, count, disk budget, or all three?
3. Which observation kinds should be eligible for immediate brain escalation in baseline chess mode?
4. When video clips arrive, should clips be continuous rolling segments or event-triggered segments first?
