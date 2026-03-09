# Phase 10 Plan 02: ConversationProviderFactory + Auth Layer Summary

**One-liner:** Factory routing tests (15) + SupabaseAuthService testability refactor + auth tests (12) = 27 new tests, 359 total

## Plan Metadata
- **Phase:** 10 (Integration & Orchestration Test Coverage)
- **Plan:** 02 of 04
- **Type:** test
- **Duration:** ~6 minutes
- **Completed:** 2026-03-03

## What Was Done

### Task 1: ConversationProviderFactory Tests (15 tests)
**File:** `src/GaimerDesktop/GaimerDesktop.Tests/Conversation/ConversationProviderFactoryTests.cs`

Comprehensive testing of the factory's provider selection logic:
- Explicit provider selection via VOICE_PROVIDER (mock, gemini, openai)
- Missing API key throws InvalidOperationException with descriptive message
- Unknown provider throws with valid options listed
- USE_MOCK_SERVICES override ("true" and "1" variants)
- Auto-detect priority: Gemini > OpenAI > Mock fallback
- Both keys present prefers Gemini
- No keys falls back to Mock
- Voice gender resolution via ISettingsService (female -> Kore/shimmer)
- Null settings defaults to male voice

Used `ConfigurationBuilder.AddInMemoryCollection()` for config injection -- no env var dependency.

### Task 2: SupabaseAuthService Refactor
**File:** `src/GaimerDesktop/GaimerDesktop/Services/Auth/SupabaseAuthService.cs`

Minimal production code change for testability:
- Added `internal` constructor accepting `HttpMessageHandler`, `supabaseUrl`, `supabaseAnonKey`
- Added `[assembly: InternalsVisibleTo("GaimerDesktop.Tests")]`
- Existing public constructor unchanged (still reads env vars)
- Enables deterministic HTTP mocking without env var coupling

### Task 3: SupabaseAuthService Tests (12 tests)
**File:** `src/GaimerDesktop/GaimerDesktop.Tests/Services/Auth/SupabaseAuthServiceTests.cs`

Full coverage of device validation and API key fetching:
- Missing config (empty URL, empty anon key, both empty) returns unauthorized
- Successful validation sets IsAuthorized=true, UserName, returns AuthResult
- Denied response (authorized=false) returns "not in allowed list"
- HTTP 500 returns unauthorized with status code in reason
- Network exception (HttpRequestException) returns unauthorized with message
- FetchApiKeysAsync returns null when not authorized
- FetchApiKeysAsync returns ApiKeyBundle with all three key fields
- FetchApiKeysAsync returns null on HTTP error

Used `MockHttpHandler` with sequential response handlers for multi-call flows (validate then fetch).

### Task 4: Full Suite Verification
- 359/359 tests passing (332 baseline + 27 new)
- 0 failures, 0 skipped

## Test Count Summary
| Category | Count |
| --- | --- |
| ConversationProviderFactory | 15 |
| SupabaseAuthService | 12 |
| **New in this plan** | **27** |
| **Total suite** | **359** |

## Commits
| Commit | Type | Description |
| --- | --- | --- |
| `e4629cd` | test | ConversationProviderFactory provider selection tests (15) |
| `9851a37` | refactor | Add testable internal constructor to SupabaseAuthService |
| `1971c2b` | test | SupabaseAuthService device validation and API key tests (12) |

## Key Files
### Created
- `src/GaimerDesktop/GaimerDesktop.Tests/Conversation/ConversationProviderFactoryTests.cs`
- `src/GaimerDesktop/GaimerDesktop.Tests/Services/Auth/SupabaseAuthServiceTests.cs`

### Modified
- `src/GaimerDesktop/GaimerDesktop/Services/Auth/SupabaseAuthService.cs` (internal constructor + InternalsVisibleTo)

## Decisions Made
| Decision | Rationale |
| --- | --- |
| Internal constructor takes URL/key params, not just handler | Env vars can't be isolated per-test in parallel xUnit; explicit params are deterministic |
| 12 auth tests instead of plan's 10 | Added MissingUrl and MissingAnonKey edge cases for complete branch coverage |
| Factory tests don't inspect voice name on provider | Voice resolution already tested in VoiceConfigTests; factory tests verify correct type selection |

## Deviations from Plan
### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Internal constructor takes URL/key params**
- **Found during:** Task 2
- **Issue:** Plan specified handler-only internal constructor, but env vars (SUPABASE_URL, SUPABASE_ANON_KEY) read in constructor can't be mocked per-test
- **Fix:** Extended internal constructor signature to accept `(ISettingsService, HttpMessageHandler, string url, string anonKey)`
- **Files modified:** `SupabaseAuthService.cs`
- **Commit:** `9851a37`

**2. [Rule 2 - Missing Critical] Additional config edge case tests**
- **Found during:** Task 3
- **Issue:** Plan listed 10 tests but missing URL-only and key-only edge cases that exercise different code paths in the config guard
- **Fix:** Added `ValidateDeviceAsync_MissingUrl_ReturnsUnauthorized` and `ValidateDeviceAsync_MissingAnonKey_ReturnsUnauthorized`
- **Files modified:** `SupabaseAuthServiceTests.cs`
- **Commit:** `1971c2b`

## Next Phase Readiness
- Plan 10-03 (BrainEventRouter + SessionManager orchestration) is next
- All foundation and factory/auth tests green
- InternalsVisibleTo now available for any internal members in GaimerDesktop assembly
