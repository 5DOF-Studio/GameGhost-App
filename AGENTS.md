# AGENTS.md

## Cursor Cloud specific instructions

### Project overview

Gaimer (a.k.a. Witness Desktop) is a .NET MAUI desktop application — an AI gaming companion that uses real-time voice and screen analysis via Google Gemini or OpenAI Realtime APIs. See `WITNESS/AGENT_HANDOFF_INSTRUCTIONS.md` for full architecture, build commands, and testing flows.

### Linux VM constraints

This is a **desktop GUI app** targeting Windows and macOS. On the Linux cloud VM:

- **Only the `net8.0-android` target framework compiles.** iOS and macCatalyst workloads are not available on Linux.
- To build, you must override `TargetFrameworks` to avoid iOS/macCatalyst resolution errors:
  ```
  dotnet build -f net8.0-android -p:TargetFrameworks=net8.0-android
  ```
- The MAUI app cannot be *run* on the Linux VM (no display server for MAUI, no Android emulator configured).
- The **UI mockup** (`WITNESS/gaimer_spec_docs/ui_mockup/`) is an interactive HTML/CSS/JS prototype that can be served and tested in-browser:
  ```
  python3 -m http.server 8080 --directory WITNESS/gaimer_spec_docs/ui_mockup
  ```

### .NET SDK location

- Installed at `$HOME/.dotnet` (user-local install via `dotnet-install.sh`).
- `DOTNET_ROOT` and `PATH` are configured in `~/.bashrc`.
- Android SDK is at `$HOME/android-sdk`; JDK 17 at `/usr/lib/jvm/java-17-openjdk-amd64`.

### Key build/restore commands

| Command | Purpose |
|---------|---------|
| `dotnet restore -p:TargetFramework=net8.0-android` | Restore NuGet packages (Android only) |
| `dotnet build -f net8.0-android -p:TargetFrameworks=net8.0-android` | Build for Android |
| `dotnet clean` | Clean build artifacts |

### No automated tests

This project currently has no unit or integration test projects. Validation is manual — refer to "What to Test" in `WITNESS/AGENT_HANDOFF_INSTRUCTIONS.md`.

### Mock services

The app auto-detects missing API keys and falls back to `MockConversationProvider`. Set `USE_MOCK_SERVICES=true` to force mock mode even with keys present. No external services (databases, Docker, etc.) are required.
