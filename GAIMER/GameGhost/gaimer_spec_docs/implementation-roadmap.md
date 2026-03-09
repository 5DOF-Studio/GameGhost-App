# Gaimer Desktop Brain — Implementation Roadmap

**Created:** 2026-02-24
**Strategy:** Cloud-first deployment (V1), local layer underneath (V2)
**Principle:** Ship with cloud APIs, validate the product, then swap providers behind the abstraction layer

---

## V1: Cloud-Based Prototype (Current Sprint)

**Goal:** Deployed product with full Brain functionality via cloud APIs.
**Stack:** .NET MAUI + OpenAI Live Preview API (voice) + Gemini Live (reasoning) + cloud LLM (chat)

### Phase 1: Core Chat — COMPLETE
> Implemented in `gAImer_desktop/src/GaimerDesktop/GaimerDesktop/`

- [x] SessionContext model + state machine
- [x] ChatMessage + BrainMetadata + ToolCallInfo data structures
- [x] TimelineFeed + TimelineCheckpoint + EventLine + TimelineEvent models
- [x] BrainEventRouter (Brain pipeline → timeline + voice + top strip)
- [x] ChatPromptBuilder (system prompt assembly with session context)
- [x] Tool definitions with session-state gating
- [x] LLM API integration (Gemini, OpenAI providers)
- [x] Timeline UI (agent-plan pattern adapted)
- [x] 16 unique event icons, urgency-based styling
- [x] ProactiveAlertView with DataTriggers

### Phase 1.5: Capture & Vision Pipeline — RESEARCH UNDERWAY
> No spec yet. Research to be conducted from desktop project context.

**Status:** Scoping — define what to build before building it.

**Research topics to cover:**
- [ ] OS-level capture APIs: Windows Graphics Capture (Win10+) vs macOS ScreenCaptureKit
- [ ] Game window detection: identifying and locking onto the active game window
- [ ] Capture service architecture: threading model, lifecycle, error recovery
- [ ] Adaptive frame frequency: 1-5s intervals based on game activity / genre
- [ ] Frame preprocessing: resize, format (PNG vs JPEG vs raw), quality tradeoffs
- [ ] Capture → Brain pipeline: how a raw frame becomes a `BrainEventRouter.OnScreenCapture()` call
- [ ] Connector system: game-specific adapters (Chess.com, Steam overlay) vs generic screen capture
- [ ] Privacy & permissions: macOS screen recording consent, Windows notification, user opt-in flow
- [ ] V1 cloud path: frame → cloud vision API (GPT-4o-mini, ~$0.09/session at 1 per 3s)
- [ ] V2 local path: frame → MiniCPM-o vision encoder on CPU (~200-500ms per frame)
- [ ] Change detection: reduce redundant captures (frame differencing, keyframe triggers)
- [ ] Rolling context buffer: timestamped text summaries, FIFO with semantic compression, 4K token cap

**Output:** `capture-architecture.md` spec in this folder, companion to `chat-brain-design.md`

### Phase 2: Persistence & Memory (Cloud) — NEXT
> Ship cross-session recall using cloud infrastructure before optimizing for local

**2A: Session Persistence (SQLite)**
- [ ] EF Core + SQLite schema: ChatSessions, ChatMessages, TimelineEvents, BrainHints
- [ ] Save/restore TimelineFeed from SQLite on session start/end
- [ ] Session replay (scroll through past game timelines)

**2B: Player Memory (Mem0 Pattern — Cloud LLM)**
- [ ] Memory extraction prompt: post-session, send transcript to cloud LLM → structured facts
- [ ] Memory storage: SQLite table with embedded vectors (BLOB column)
- [ ] CPU-only embeddings via ONNX Runtime (`all-MiniLM-L6-v2`, ~50MB, ~10ms)
- [ ] Memory retrieval: cosine similarity scan → inject into ChatPromptBuilder system prompt
- [ ] Conflict resolution: >0.85 similarity on store → update existing memory, don't duplicate
- [ ] Graph store: `nodes` + `edges` tables in same SQLite DB for relationship traversal
- [ ] Recursive CTE queries for multi-hop recall ("What should I practice?")

**2C: Analytics Foundation**
- [ ] PlayerStats table: win rate, opening stats, streaks (per game type)
- [ ] Post-session aggregation job
- [ ] PlayerHistory + PlayerAnalytics tool implementations (currently stubs)

### Phase 3: Polish & UX
- [ ] Timeline animations (MAUI animations or Lottie)
- [ ] Proactive message throttling / deduplication
- [ ] Memory debugger UI (show what Dross remembers, let user correct/delete)
- [ ] Screenshot gallery / capture browser

### Phase 4: Deploy V1
- [ ] Package for Windows (MSIX) + macOS (.app bundle)
- [ ] First-run setup wizard (API key configuration)
- [ ] Telemetry (opt-in) for usage patterns
- [ ] Beta distribution channel

---

## V2: Local AI Layer (Post-Deployment)

**Goal:** Replace cloud API calls with local inference. Same product, zero cloud dependency.
**Stack:** MiniCPM-o 4.5 via Ollama sidecar + local embeddings + local everything

> **Key decision:** The abstraction layer from V1 (inference client interface, ChatPromptBuilder, BrainEventRouter) remains unchanged. Only the *provider* swaps.

### Phase 5: Ollama Sidecar Integration
> Reference: `PX-minicpm-o-omnimodal-model-selection.md`

- [ ] Ollama process lifecycle management (launch on app start, health checks, graceful shutdown)
- [ ] OpenAI-compatible client pointing to `localhost:11434`
- [ ] Model auto-download on first run (`openbmb/minicpm-o4.5`)
- [ ] VRAM detection (NVML on Windows, Metal API on macOS)
- [ ] `n_gpu_layers` configuration based on available VRAM budget
- [ ] Provider swap: cloud LLM → Ollama in inference client abstraction
- [ ] Cold-start management (10-30s warmup, progress indicator)

### Phase 6: Split-Offload Architecture
> Reference: ADR-1 through ADR-5 in model selection doc

- [ ] Vision encoder pinned to CPU (~200-500ms per frame, saves ~1GB VRAM)
- [ ] Dynamic `n_gpu_layers` adjustment based on detected free VRAM
- [ ] VRAM configuration profiles:
  - Light games (8.5-9.5 GB free): full LLM on GPU
  - Medium games (6.5-7.5 GB free): 70% LLM layers on GPU
  - Heavy games (3.5-5 GB free): 40% LLM layers, consider text-only mode
- [ ] User-facing quality slider mapping to VRAM budget
- [ ] MiniCPM-o 2.6 fallback for <6GB VRAM scenarios (ADR-5)
- [ ] Disable NVIDIA Sysmem Fallback detection/warning

### Phase 7: Local Memory Layer
> Reference: `PX-mem0-long-term-memory-strategy.md`

- [ ] Memory extraction migrates from cloud LLM → MiniCPM-o (same Ollama sidecar, zero extra VRAM)
- [ ] Extraction serialized: user response delivered → THEN extract memories (avoid LLM contention)
- [ ] Post-session bulk extraction when game closes and VRAM frees up
- [ ] Embeddings remain CPU-only ONNX (unchanged from V1)
- [ ] SQLite storage unchanged (same schema, same queries)
- [ ] Graph store unchanged (same tables, same CTEs)
- [ ] Verify memory quality: local 8B model extraction vs cloud extraction A/B comparison

### Phase 8: Local Voice Pipeline
- [ ] MiniCPM-o CosyVoice2 TTS replaces OpenAI Live Preview API
- [ ] Audio encoder/Whisper on GPU (or CPU fallback for heavy games)
- [ ] Token2Wav vocoder integration
- [ ] Streaming audio output (80ms chunk threshold)
- [ ] Voice + chat coexistence: parallel channels, same BrainEventRouter
- [ ] Accept 2-5s first-audio-token latency (vs cloud <1s) — document tradeoff

### Phase 9: Advanced Local (Future)
- [ ] Migrate from Ollama to llama.cpp-omni native sidecar (fine-grained VRAM control)
- [ ] Per-modality GGUF loading (independent head management)
- [ ] WebRTC support for streaming
- [ ] Multi-game connector support
- [ ] macOS Metal optimization (16GB+ unified memory)

---

## Architecture: Cloud → Local Swap Points

```
Component              V1 (Cloud)                    V2 (Local)
────────────────────   ────────────────────────────   ─────────────────────────
Chat LLM               Cloud API (GPT/Gemini)         Ollama → MiniCPM-o 4.5
Voice Output            OpenAI Live Preview API        MiniCPM-o CosyVoice2
Voice Input             Gemini Live                    MiniCPM-o Whisper/audio encoder
Vision Analysis         Cloud vision API               MiniCPM-o vision encoder (CPU)
Memory Extraction       Cloud LLM (post-session)       MiniCPM-o (same sidecar)
Embeddings              ONNX Runtime (CPU)             ONNX Runtime (CPU) — unchanged
Vector + Graph Store    SQLite                         SQLite — unchanged
Session Persistence     SQLite (EF Core)               SQLite (EF Core) — unchanged
BrainEventRouter        Unchanged                      Unchanged
ChatPromptBuilder       Unchanged                      Unchanged
Timeline UI             Unchanged                      Unchanged
```

**What doesn't change:** BrainEventRouter, ChatPromptBuilder, TimelineFeed, Timeline UI, SQLite schema, ONNX embeddings, graph store. These are the stable foundation.

**What swaps:** The inference provider behind the abstraction layer. One interface, two implementations.

---

## Decision Log

| # | Decision | Rationale | Date |
|---|----------|-----------|------|
| D1 | Cloud-first, local-second | Ship and validate before optimizing. Users care about utility, not where inference runs. | 2026-02-24 |
| D2 | MiniCPM-o 4.5 for local | Only omnimodal model meeting all 5 requirements (audio out, independent heads, llama.cpp, Apache 2.0, vision+audio) | 2026-02-24 |
| D3 | Mem0 pattern, not Mem0 SDK | Python SDK doesn't fit .NET stack. Implement Extract→Consolidate→Retrieve natively in C# | 2026-02-24 |
| D4 | CPU embeddings (ONNX) | VRAM fully consumed by MiniCPM-o + game. 50MB model, ~10ms inference, works on any hardware | 2026-02-24 |
| D5 | SQLite for everything | Session persistence, memory vectors (BLOB), graph store (nodes+edges). Single DB, no extra services | 2026-02-24 |
| D6 | Ollama first, llama.cpp-omni later | Ollama for rapid prototyping, migrate to native sidecar when fine-grained VRAM control needed | 2026-02-24 |
| D7 | OpenAI Live Preview API for V1 voice | Cloud voice for prototype, swap to CosyVoice2 local in V2 | 2026-02-24 |

---

## Research Sources

| Document | Location | Covers |
|----------|----------|--------|
| Chat Brain Design | `./chat-brain-design.md` | Phase 1 architecture, data models, prompts, event routing |
| MiniCPM-o Model Selection | `gaimer-build-in-public-website/docs/research/_raw/PX-minicpm-o-omnimodal-model-selection.md` | V2 model evaluation, VRAM analysis, ADRs |
| Mem0 Memory Strategy | `gaimer-build-in-public-website/docs/research/_raw/PX-mem0-long-term-memory-strategy.md` | Memory layer architecture, vector+graph hybrid, extraction loop |
| Brain & Tools Reference | `../Gaimer-Dross-ElevenLabs/brain-and-tools-reference.md` | Voice agent prompts, tool schemas (web demo) |
