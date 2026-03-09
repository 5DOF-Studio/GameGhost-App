# OpenRouter Integration Research

**Date:** March 2, 2026
**Status:** Research Complete

---

## Executive Summary

OpenRouter is a unified API gateway providing access to 400+ LLM models (GPT-4o, Claude, Gemini, Llama, Mistral, etc.) through a single OpenAI-compatible REST endpoint. It is **REST/SSE only** — NOT WebSocket. This means it **cannot replace** `IConversationProvider` for real-time voice streaming, but it is ideal as a separate `IModelProvider` for brain/worker text+vision+tools.

---

## Key Findings

### API Compatibility
- Base URL: `https://openrouter.ai/api/v1`
- Drop-in replacement for OpenAI SDK — change base URL and API key
- Supports: chat completions, streaming (SSE), tool/function calling, vision (base64 + URL), JSON mode
- Does NOT support: WebSocket connections, real-time audio streaming

### Model Selection
OpenRouter enables dynamic model selection at request time via the `model` field:

```json
{
  "model": "anthropic/claude-sonnet-4-20250514",
  "messages": [...]
}
```

**Recommended model tiers for gAImer:**

| Role | Models | Use Case |
|------|--------|----------|
| Brain (analysis) | `anthropic/claude-sonnet-4-20250514`, `openai/gpt-4o`, `google/gemini-2.0-flash` | Screen analysis, chess evaluation, strategy |
| Worker (fast) | `openai/gpt-4o-mini`, `anthropic/claude-haiku-4-5-20251001`, `google/gemini-2.0-flash-lite` | Quick classification, formatting, lightweight tasks |

### Tool Calling Support
OpenRouter passes through tool definitions to providers that support them:

```json
{
  "model": "anthropic/claude-sonnet-4-20250514",
  "messages": [...],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "capture_screen",
        "description": "Capture the current game screen",
        "parameters": { "type": "object", "properties": {} }
      }
    }
  ]
}
```

Tool calls are returned in the standard OpenAI format with `tool_calls` array in the assistant message.

### Authentication Options

1. **Server-side API key** — Standard `Authorization: Bearer sk-or-...` header
2. **OAuth PKCE** — For user-managed keys. Users authenticate with their own OpenRouter account and bring their own credits. Flow:
   - App redirects to `https://openrouter.ai/auth?callback_url=...`
   - User approves, gets redirected back with auth code
   - Exchange code for API key via `https://openrouter.ai/api/v1/auth/keys`
   - Key stored locally, used for all subsequent requests

### Pricing
- Pay-per-token, varies by model
- No monthly subscription required
- Credits system with usage tracking via `https://openrouter.ai/api/v1/auth/key`
- Provider routing preferences (allow fallback to cheaper providers)

### Streaming
Server-Sent Events (SSE) for streaming responses:

```
data: {"choices":[{"delta":{"content":"Hello"}}]}
data: {"choices":[{"delta":{"content":" world"}}]}
data: [DONE]
```

---

## Recommended Architecture

### New Interface: `IModelProvider`

```csharp
public interface IModelProvider
{
    Task<ChatResponse> CompleteAsync(
        ChatRequest request,
        CancellationToken ct = default);

    IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        ChatRequest request,
        CancellationToken ct = default);

    string CurrentModel { get; set; }
    IReadOnlyList<ModelInfo> AvailableModels { get; }
}
```

### Integration Points

```
IConversationProvider (VOICE)          IModelProvider (BRAIN)
├── Gemini Live WebSocket              ├── OpenRouter REST API
├── OpenAI Realtime WebSocket          ├── Model selection at runtime
├── Bidirectional audio                ├── Tool calling
└── Always connected                   ├── Vision (image analysis)
                                       └── Per-request, stateless
```

### DI Registration

```csharp
// Voice (existing, unchanged)
services.AddSingleton<IConversationProvider>(...);

// Brain (NEW — via OpenRouter)
services.AddSingleton<IModelProvider>(sp =>
    new OpenRouterModelProvider(apiKey, defaultModel));
```

---

## Implementation Notes

- Use `HttpClient` with `System.Net.Http.Json` for REST calls
- Use `System.Text.Json` source generators for request/response serialization
- Streaming uses `HttpCompletionOption.ResponseHeadersRead` + line-by-line SSE parsing
- Rate limits: 200 req/min free tier, higher for paid
- Error handling: 429 (rate limit) with `Retry-After` header, 402 (insufficient credits)
- Model availability: check `https://openrouter.ai/api/v1/models` for current list

---

## What This Does NOT Cover

- Real-time voice streaming (use Gemini Live / OpenAI Realtime directly)
- Audio processing (not supported by OpenRouter)
- Local model inference (OpenRouter is cloud-only)

---

## Sources

- https://openrouter.ai/docs/quickstart
- https://openrouter.ai/docs/requests
- https://openrouter.ai/docs/features/tool-calling
- https://openrouter.ai/docs/features/streaming
- https://openrouter.ai/docs/use-cases/oauth-pkce
