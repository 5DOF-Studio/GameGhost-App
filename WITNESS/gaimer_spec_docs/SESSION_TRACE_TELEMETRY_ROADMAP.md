# Session Trace / Telemetry Roadmap

**Status:** Proposed  
**Date:** 2026-03-16  
**Purpose:** Define a practical implementation roadmap for post-run observability in Gaimer so live sessions can be debugged from a reliable event trail instead of scattered console noise.

## Problem

Current logging is not sufficient for answering basic post-run questions such as:

- What provider/runtime path was actually selected?
- What tool calls happened during the session?
- What events were emitted into the timeline?
- What errors happened, in what order, and under which session state?
- What processed image was actually analyzed?
- Was the analyzed image legible enough for the model?
- Was the image too large, too compressed, or missing important visual detail?

There are currently fragments of useful diagnostics, but not a coherent session record.

## Goal

Build a lightweight operational tracing foundation that:

- captures the true sequence of runtime events
- is readable after a session ends
- supports local debugging first
- can later evolve into product telemetry
- supports optional image-analysis artifact capture
- avoids noisy, ad hoc logging

## Non-Goals

This roadmap is not initially about:

- analytics dashboards
- remote telemetry ingestion
- user behavior aggregation
- long-term data warehousing
- full observability platform work

Start with a local session trace foundation.

## Target Outcome

Every live session should be able to produce:

1. A structured session event log
2. Optional saved visual artifacts for analyzed images
3. Enough metadata to reconstruct what happened during the run

## Design Principles

### 1. Operational tracing first

The first job is to help developers answer:

- what happened?
- in what order?
- with what inputs?
- under what provider/runtime state?

### 2. Structured over free-form

Prefer structured JSONL events over handwritten console text.

### 3. Session-scoped

Every trace entry should belong to a concrete session or app run.

### 4. Artifact-aware

Image analysis is too important to debug through text alone. The system should support saved processed-image artifacts when enabled.

### 5. Debug-gated

High-volume tracing and image artifact capture should be controllable by config so normal runs do not become noisy or expensive.

## Recommended Deliverables

### Deliverable A: Session Trace Log

A structured append-only log file, preferably JSONL.

Suggested location:

- `/tmp/gaimer-session-trace.jsonl` for local debug builds, or
- app-scoped session trace directory under application data

Each event should include:

- `timestamp`
- `session_id`
- `run_id`
- `event_name`
- `category`
- `level`
- `payload`

### Deliverable B: Image Analysis Artifacts

Optional sampled artifacts for processed frames that were actually sent to analysis.

Suggested structure:

```text
artifacts/<session-id>/<capture-id>.jpg
artifacts/<session-id>/<capture-id>.json
```

The sidecar metadata should include:

- capture timestamp
- raw image dimensions if known
- processed image dimensions
- byte size
- mime type
- compression/scaling settings
- diff-gate decision
- model/provider used
- analysis request id if available

### Deliverable C: Trace-Friendly Event Naming

Adopt a stable event naming scheme.

Examples:

- `app.bootstrap`
- `provider.selected`
- `session.connect.start`
- `session.connect.success`
- `session.connect.failure`
- `capture.frame.received`
- `capture.frame.skipped`
- `capture.frame.selected_for_analysis`
- `capture.frame.artifact_saved`
- `brain.request.start`
- `brain.request.success`
- `brain.request.failure`
- `brain.tool_call`
- `brain.tool_result`
- `timeline.event_emitted`
- `voice.ws.connected`
- `voice.ws.disconnected`
- `voice.ws.error`

## Priority Slices

### Slice 1: Session Trace Foundation

Goal:

- create a local structured trace writer
- create a simple event schema
- write traces for app bootstrap, provider selection, session connect/disconnect, and major errors

Acceptance:

- one session log exists after a run
- provider/runtime path can be reconstructed from that log
- session lifecycle can be reconstructed from that log

### Slice 2: Brain / Tool / Timeline Trace Coverage

Goal:

- add trace events for brain requests, tool calls/results, and timeline emission

Acceptance:

- a post-run reviewer can identify:
  - what the model was asked to do
  - what tools ran
  - what timeline events were emitted
  - where the failure occurred if the run degraded

### Slice 3: Capture + Image Artifact Debug Mode

Goal:

- optionally save the processed image actually sent for analysis
- log dimensions, size, and processing metadata

Acceptance:

- the post-processed analyzed image can be inspected after a run
- the team can evaluate visibility, legibility, scale, and compression tradeoffs

Key questions this slice must answer:

- Is the image visually legible?
- Are important interest points visible?
- Is the processed image too compressed?
- Is the frame too large or too small?
- Did preprocessing remove useful information?

### Slice 4: Sampling / Retention / Cleanup Policy

Goal:

- prevent local trace/artifact growth from becoming unbounded

Acceptance:

- trace and artifact capture can be sampled or capped
- retention policy is explicit
- debug mode remains practical for repeated live tests

### Slice 5: Telemetry Service Consolidation

Goal:

- consolidate scattered telemetry/logging paths behind one coherent service seam

Acceptance:

- new runtime events use the shared trace service
- low-value duplicate logging is removed or deprecated

## Proposed Service Shape

Introduce a shared trace seam, for example:

- `ISessionTraceService`

Possible responsibilities:

- start/end run
- start/end session
- append structured event
- optionally persist image artifact
- expose current `run_id` / `session_id`

Possible methods:

- `StartRun(...)`
- `StartSession(...)`
- `TrackEvent(...)`
- `TrackError(...)`
- `SaveImageArtifact(...)`
- `EndSession(...)`
- `EndRun(...)`

## Suggested First Event Payloads

### Provider selection

Should capture:

- inference mode
- voice provider
- brain provider
- mock vs non-mock
- relevant key presence booleans only

Do not log secrets.

### Tool call

Should capture:

- tool name
- duration
- success/failure
- result class
- optional summarized payload size

### Timeline emission

Should capture:

- output type
- source path
- bucket minute
- whether current or older bucket

### Image artifact

Should capture:

- capture id
- dimensions
- final byte size
- file path
- whether actually sent to the model

## Safety / Hygiene Rules

- Never log raw API keys or secrets.
- Avoid logging full user audio payloads.
- Be careful with full text capture where unnecessary.
- Make artifact capture opt-in or debug-gated.
- Keep trace output useful enough to read manually.

## Cleanup Philosophy

If existing telemetry-like code is present but not useful, replace it rather than preserving noise for compatibility.

The goal is not “more logging.”

The goal is:

- one coherent event ledger
- one coherent artifact path
- one coherent service boundary

## Recommended Sequencing

1. Session Trace Foundation
2. Brain / Tool / Timeline Trace Coverage
3. Capture + Image Artifact Debug Mode
4. Sampling / Retention / Cleanup
5. Telemetry Service Consolidation

## Ready-To-Use Future Rotation Seeds

### Rotation Seed 1

`Implement ISessionTraceService with local JSONL output for bootstrap/provider/session lifecycle events.`

### Rotation Seed 2

`Add trace coverage for BrainEventRouter, tool-call execution, and timeline emission.`

### Rotation Seed 3

`Add debug-gated processed-image artifact capture for analyzed frames with metadata sidecars.`

### Rotation Seed 4

`Consolidate scattered telemetry/logging into the shared session trace service and remove low-value duplicate logging.`

## Exit Condition

This roadmap is successful when post-run debugging no longer depends on guessing from console fragments and screenshots, and a live session can be reconstructed from a coherent trace plus optional analyzed-image artifacts.
