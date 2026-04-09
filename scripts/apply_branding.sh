#!/usr/bin/env bash
# apply_branding.sh — Re-apply Game Ghost branding after gaimer-v2 overlay sync.
#
# Usage: ./scripts/apply_branding.sh [--dry-run]
#
# This script is idempotent — safe to run multiple times.
# It replaces user-facing "Gaimer" references with "Game Ghost" / "Ghost Team"
# while preserving internal identifiers (service names, namespaces, debug prefixes).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$REPO_ROOT/src/WitnessDesktop/WitnessDesktop"
TESTS="$REPO_ROOT/src/WitnessDesktop/WitnessDesktop.Tests"
DRY_RUN=false

if [[ "${1:-}" == "--dry-run" ]]; then
    DRY_RUN=true
    echo "[dry-run] Showing what would change (no files modified)"
fi

changed=0

# Helper: sed in-place (macOS-compatible)
do_sed() {
    local file="$1"
    local pattern="$2"
    local replacement="$3"

    if grep -q "$pattern" "$file" 2>/dev/null; then
        if $DRY_RUN; then
            echo "  WOULD: $file"
            grep -n "$pattern" "$file" | head -3
        else
            sed -i '' "s|$pattern|$replacement|g" "$file"
            echo "  FIXED: $file"
        fi
        changed=$((changed + 1))
    fi
}

echo ""
echo "=== Game Ghost Branding Pass ==="
echo ""

# ---------------------------------------------------------------
# 1. XAML UI Titles
# ---------------------------------------------------------------
echo "[1/6] XAML page titles..."
do_sed "$SRC/MainPage.xaml" \
    'Title="Gaimer Dashboard"' \
    'Title="Game Ghost Dashboard"'

do_sed "$SRC/Views/OnboardingPage.xaml" \
    'Title="Gaimer Onboarding"' \
    'Title="Game Ghost Onboarding"'

do_sed "$SRC/Views/AgentSelectionPage.xaml" \
    'Title="Gaimer Agents"' \
    'Title="Game Ghost Agents"'

do_sed "$SRC/Views/DevLauncherPage.xaml" \
    'Title="Gaimer Dev Launcher"' \
    'Title="Game Ghost Dev Launcher"'

# ---------------------------------------------------------------
# 2. Settings sidebar label
# ---------------------------------------------------------------
echo "[2/6] Settings sidebar label..."
do_sed "$SRC/Views/SettingsPage.xaml" \
    'Text="Gaimer"' \
    'Text="Game Ghost"'

# ---------------------------------------------------------------
# 3. User-facing "Gaimer Team" → "Ghost Team"
# ---------------------------------------------------------------
echo "[3/6] User-facing Gaimer Team → Ghost Team..."

# SettingsPage team description
do_sed "$SRC/Views/SettingsPage.xaml" \
    'Gaimer Team lets your AI agents' \
    'Ghost Team lets your AI agents'

# ConnectorCard display name
do_sed "$SRC/Connectors/ConnectorCardProviderExtensions.cs" \
    '"Gaimer Team"' \
    '"Ghost Team"'

# ToolExecutor error message
do_sed "$SRC/Services/Brain/ToolExecutor.cs" \
    '"Gaimer Team is not connected"' \
    '"Ghost Team is not connected"'

# ---------------------------------------------------------------
# 4. Prompt identity strings
# ---------------------------------------------------------------
echo "[4/6] Prompt identity strings..."
do_sed "$SRC/Services/ChatPromptBuilder.cs" \
    'built into Gaimer' \
    'built into Game Ghost'

# ---------------------------------------------------------------
# 5. .csproj branding (safety check)
# ---------------------------------------------------------------
echo "[5/6] .csproj branding check..."
CSPROJ="$SRC/WitnessDesktop.csproj"
if grep -q '<ApplicationTitle>Gaimer</ApplicationTitle>' "$CSPROJ" 2>/dev/null; then
    do_sed "$CSPROJ" \
        '<ApplicationTitle>Gaimer</ApplicationTitle>' \
        '<ApplicationTitle>Game Ghost</ApplicationTitle>'
fi
if grep -q '<ApplicationId>com.5dof.gaimer</ApplicationId>' "$CSPROJ" 2>/dev/null; then
    do_sed "$CSPROJ" \
        '<ApplicationId>com.5dof.gaimer</ApplicationId>' \
        '<ApplicationId>com.5dof.gameghost</ApplicationId>'
fi

# ---------------------------------------------------------------
# 6. Test assertions that match branding
# ---------------------------------------------------------------
echo "[6/6] Test assertion branding..."
do_sed "$TESTS/Services/ChatPromptBuilderTests.cs" \
    'built into Gaimer' \
    'built into Game Ghost'

# ---------------------------------------------------------------
# Summary
# ---------------------------------------------------------------
echo ""
if $DRY_RUN; then
    echo "=== Dry run complete: $changed file(s) would be changed ==="
else
    echo "=== Branding pass complete: $changed replacement(s) applied ==="
fi
echo ""

# ---------------------------------------------------------------
# Verification: scan for remaining user-facing "Gaimer" leaks
# ---------------------------------------------------------------
echo "=== Verification: scanning for user-facing Gaimer leaks ==="
LEAKS=$(grep -rn '"Gaimer\b' "$SRC/" --include="*.xaml" --include="*.cs" 2>/dev/null \
    | grep -iv 'IGaimerTeam\|GaimerTeamService\|GaimerPipe\|MockGaimer\|GaimerScreenCapture\|GaimerSpeech\|GaimerGhostMode\|namespace\|using \|Debug.Write\|// \|#region\|#endregion\|\.Gaimer\|Sir Leroy' \
    || true)

if [ -n "$LEAKS" ]; then
    echo "POTENTIAL LEAKS found (review manually):"
    echo "$LEAKS"
    echo ""
else
    echo "No user-facing Gaimer leaks detected."
    echo ""
fi
