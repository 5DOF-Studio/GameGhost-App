# Gaimer macOS Build and Release Hardening Plan

**Project:** Gaimer Desktop (.NET MAUI, Mac Catalyst)  
**Date:** 2026-03-19  
**Status:** Planned  
**Scope:** macOS local deploy, release build packaging, artifact hygiene, Dock icon reliability, and post-verification documentation promotion

---

## Summary

Gaimer's current macOS build story works, but it is not yet production-grade. The app can be built, signed, notarized, deployed to `/Applications`, and launched. However, the current workflow leaves multiple app bundles and packages on disk, mixes debug and release artifacts in the same operational path, and relies on a release-time icon recovery workaround that is not yet trustworthy enough to be called final.

The two visible symptoms are:

1. **Finder Storage shows multiple Gaimer installs or duplicates.**
2. **The Dock icon is unreliable on some release/local deploy paths.**

These are related only at the packaging/process level. They are not the same underlying defect.

This plan defines the work required to make the macOS pipeline deterministic, minimal, verifiable, and safe to promote into the repository's live instructions after implementation and validation.

---

## Why This Work Exists

The current repository already documents several deployment fixes and one unresolved icon issue:

- `dotnet publish -c Release` can produce multiple Mac Catalyst outputs across runtime identifiers.
- the release script has already needed fixes for architecture detection, zip-signature preservation, and local deploy flow.
- the repository's local docs explicitly note a known bug where the release bundle can still show the default MAUI grid icon in the Dock.

The current problem is not that macOS is randomly duplicating the app. The problem is that the build pipeline currently emits multiple legitimate `.app` and `.pkg` artifacts with the same product identity, and Finder Storage counts them separately.

At the same time, the icon bug indicates the build pipeline does not yet have a single canonical, fully verified release bundle path.

---

## Current State Findings

### 1. Multiple product artifacts are being produced by design

The project file states that Mac Catalyst release builds default to multiple runtime identifiers, while debug builds default to x64 unless otherwise configured. See [WitnessDesktop.csproj](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj#L9).

The app identity is uniform across those outputs:

- `ApplicationTitle = Gaimer`
- `AssemblyName = Gaimer`
- `ApplicationId = com.5dof.gaimer`

See [WitnessDesktop.csproj](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj#L24).

The repository currently contains multiple app bundles and package artifacts with that identity:

- `bin/Debug/net8.0-maccatalyst/maccatalyst-x64/Gaimer.app`
- `bin/Release/net8.0-maccatalyst/Gaimer.app`
- `bin/Release/net8.0-maccatalyst/maccatalyst-arm64/Gaimer.app`
- `bin/Release/net8.0-maccatalyst/maccatalyst-x64/Gaimer.app`
- `bin/Release/net8.0-maccatalyst/publish/Gaimer-1.0.pkg`
- `bin/Release/net8.0-maccatalyst/publish/WitnessDesktop-1.0.pkg`

That is enough for Finder Storage to surface several apparent copies of the app, even if `/Applications` only contains one installed app bundle.

### 2. The current local run path creates an extra deployed copy

The current local dev instructions explicitly copy a debug bundle into `/Applications/Gaimer.app`, sign it, and launch it. See [CLAUDE.md](/Users/tonynlemadim/Developer/gAImer_desktop/CLAUDE.md#L16).

This creates a separate deployed app in addition to any debug or release app bundles already present in `bin/`.

### 3. The current release script still depends on a packaging workaround

The current release script:

- runs `dotnet publish`
- locates an RID-specific app bundle
- copies it into `/tmp/gaimer-dist`
- patches icon resources from a debug build
- signs the copied app
- optionally deploys to `/Applications`

See [build-release-mac.sh](/Users/tonynlemadim/Developer/gAImer_desktop/scripts/build-release-mac.sh#L62).

This is functional, but it is not yet clean. It uses:

- RID-specific release bundle selection
- debug-build-derived icon recovery
- output directories that are not treated as the single source of truth for shipping artifacts

### 4. The Dock icon issue is real and not just cache confusion

Local inspection shows an important difference in generated outputs:

- the debug x64 app bundle contains `Assets.car` and `appicon.icns`
- the universal release app bundle contains `Assets.car` and `appicon.icns`
- the RID-specific release x64 app bundle does **not** contain those icon resources by default

This strongly suggests that the current release packaging path is starting from the wrong bundle variant for icon correctness, or at minimum from a variant that requires additional asset repair before it can be treated as canonical.

The repo also already tracks this as an active known bug in [WITNESS/PROGRESS_LOG.md](/Users/tonynlemadim/Developer/gAImer_desktop/WITNESS/PROGRESS_LOG.md#L7).

### 5. There is evidence of stale naming or stale publish output

Both `Gaimer-1.0.pkg` and `WitnessDesktop-1.0.pkg` exist in the publish output. That is a sign that:

- output directories are not being cleaned aggressively enough, and/or
- older packaging identities are still being left behind by the toolchain or prior publish runs

Even if those stale packages are not user-facing, they are part of the same artifact hygiene problem.

---

## Primary Goals

1. **One canonical dev-deploy path**
   The team should have exactly one supported way to build, sign, deploy, and launch the app locally on macOS.

2. **One canonical release path**
   The team should have exactly one supported way to create a shippable macOS release artifact set.

3. **Artifact hygiene**
   The build process must stop leaving multiple product-like app bundles and packages in default output folders that Finder later counts as duplicate installs.

4. **Dock icon reliability**
   The chosen shipping bundle must launch with the correct Dock icon, without depending on a fragile debug-copy fallback as the permanent design.

5. **Verification-first instructions**
   We should not update the live instructions until the new flow is implemented, exercised, and proven end-to-end.

---

## Non-Goals

1. Reworking Windows packaging in this effort.
2. Rebranding unrelated `WitnessDesktop` namespace or source code references outside the packaging surface.
3. Solving every possible MAUI or Xcode upstream issue in the framework itself.
4. Building a GUI installer experience in this phase.

This effort is specifically about making the existing macOS flow production-grade and operationally trustworthy.

---

## Target End State

After implementation, the macOS pipeline should behave like this:

### Dev path

One script builds exactly one host-appropriate app bundle, signs it for local testing, replaces `/Applications/Gaimer.app`, verifies it, and launches it.

Properties:

- deterministic
- no debug/release ambiguity
- no manual one-liner copy-paste commands required
- no extra product artifacts left behind in the repo root as quasi-installed apps

### Release path

One script creates exactly one canonical release app bundle and a small, intentional release artifact set in a dedicated artifacts directory.

Properties:

- deterministic input bundle
- deterministic output location
- signing and notarization preserved
- icon correctness checked before shipping
- stale artifacts removed automatically

### Documentation path

The repository instructions only describe those verified flows, not historical workarounds or competing alternatives.

---

## Decision Principles

The implementation should follow these rules:

1. **A product artifact must have a single owner path.**
   If an app bundle is considered distributable, it should live in one deliberate output directory, not be inferred from toolchain byproducts under `bin/`.

2. **Debug assets must not be a permanent dependency of release packaging.**
   A debug build may be useful for temporary investigation, but production release should not fundamentally depend on debug output being present.

3. **The build should fail on packaging integrity problems.**
   Missing icon resources, incorrect bundle metadata, or inconsistent output selection should be treated as build failures, not silent repairs whenever possible.

4. **A script should encode the process, not a README paragraph.**
   Human-readable docs should describe the supported workflow, but the workflow itself must live in scripts with verification steps.

5. **We will promote only verified truth into live instructions.**
   The AGENT/HANDOFF/README guidance must reflect the implemented and tested path, not speculative intent.

---

## Workstreams

## Workstream A: Canonicalize the macOS Artifact Model

### Problem

The repo currently allows several app bundles to exist simultaneously and appear equally valid:

- debug app bundle
- release universal app bundle
- release arm64 app bundle
- release x64 app bundle
- deployed `/Applications` app bundle
- stale package files

That is appropriate for toolchain intermediates, but not for a team-facing product workflow.

### Plan

1. Define a dedicated artifact root such as `artifacts/macos/`.
2. Treat `bin/` and `obj/` as intermediate build state only.
3. Make every supported script clean and recreate its own output root before use.
4. Ensure local deploy scripts remove and replace only the canonical deployed app in `/Applications`.
5. Ensure release scripts emit only intentional deliverables into the artifact root.

### Expected outcome

Finder Storage should no longer surface a confusing pile of Gaimer app bundles created by prior build runs in the repo output tree, because the production-facing workflow will stop leaving them around as indistinguishable product artifacts.

---

## Workstream B: Split Dev Deploy from Release Packaging

### Problem

The current process mixes several goals:

- build for development
- sign for TCC permissions
- local deploy to `/Applications`
- release packaging
- notarization
- icon repair

That creates drift, duplicate commands, and unclear assumptions.

### Plan

Create two explicitly separate entry points:

1. `scripts/dev-deploy-mac.sh`
2. `scripts/build-release-mac.sh`

#### `dev-deploy-mac.sh` responsibilities

- detect host architecture
- build exactly one Mac Catalyst RID suitable for the current machine
- stage to a dedicated temporary artifact location
- sign with the local development/developer identity and entitlements
- replace `/Applications/Gaimer.app`
- verify signature
- launch app

#### `build-release-mac.sh` responsibilities

- clean the release artifact root
- build the canonical release bundle variant
- validate icon resources and bundle metadata
- sign with hardened runtime
- optionally notarize and staple
- emit final deliverables into `artifacts/macos/release/`

### Expected outcome

Developers will no longer have to choose between multiple ad-hoc build and deploy approaches. The docs can then point to one dev command and one release command.

---

## Workstream C: Make the Canonical Release Bundle Explicit

### Problem

The current release flow starts from an RID-specific bundle selected after publish. Local inspection suggests that this is the wrong bundle variant to trust for icon correctness, because the RID-specific release x64 bundle can be missing icon resources that exist in the universal release bundle.

### Plan

During implementation, explicitly test and decide which of the following becomes the canonical release source:

1. **Universal release app bundle**
2. **Single-RID release app bundle**
3. **Per-RID split release bundles**

### Recommended default direction

Use the **universal release app bundle** as the first candidate canonical release source, because current evidence shows it already contains icon resources that the RID-specific release bundle lacks.

### Validation questions

1. Does the universal release app sign and notarize cleanly?
2. Does it launch on both Intel and Apple Silicon machines as expected?
3. Does it preserve the correct Dock icon after signing, zipping, notarizing, and local deploy?
4. Does it avoid the icon regression seen on the RID-specific release path?

### Fallback direction

If universal release proves unreliable for another reason, move to explicit per-RID releases, but then:

- treat each RID bundle as an intentional deliverable
- never let both coexist in a shared product-like output location without clear naming or isolation
- make the icon/resource validation mandatory for each RID bundle

---

## Workstream D: Replace Icon Recovery with Icon Verification

### Problem

The current release script repairs icon state by copying `Assets.car` and `appicon.icns` from a debug build into the staged release app and patching plist keys. That may be useful as a stopgap, but it is not a production-level strategy.

### Plan

Move from "repair after the fact" to "validate the bundle we intend to ship."

#### Required checks

For the canonical release bundle, verify all of the following before signing is considered complete:

1. `Contents/Resources/Assets.car` exists
2. `Contents/Resources/appicon.icns` exists if the platform path expects it
3. `Info.plist` contains:
   - `CFBundleDisplayName`
   - `CFBundleName`
   - `CFBundleIconFile`
   - `CFBundleIconName`
   - `XSAppIconAssets` when applicable
4. the built app launches with the expected Dock icon on the supported deploy path

#### Preferred implementation direction

1. Validate the canonical release bundle as generated.
2. If the bundle is missing required icon resources, fail fast.
3. Only keep a post-build asset injection step if it is proven necessary and deterministic.
4. If a post-build asset injection step remains necessary, source icon assets from the release-generation pipeline itself, not from a separately built debug app.

### Why this matters

The Dock icon is part of product identity. A release path that can silently fall back to the default MAUI grid is not fully shippable.

---

## Workstream E: Add Packaging Integrity Checks

### Problem

The current scripts verify code signatures, but packaging correctness still relies on manual observation and memory of prior bugs.

### Plan

Add automated validation steps that run inside the scripts.

### Candidate checks

1. Verify the selected app bundle path exists and is the expected one.
2. Verify the executable architecture matches the intended release mode.
3. Verify icon resources exist.
4. Verify required plist keys exist.
5. Verify code signing.
6. Verify notarization status when applicable.
7. Verify the final release artifact names are exactly the expected names.
8. Fail if stale `WitnessDesktop`-named release artifacts are present in the shipping output root.

### Expected outcome

Packaging regressions become obvious immediately, rather than being discovered only after a Dock icon mismatch or Finder Storage confusion.

---

## Workstream F: Eliminate Historical Naming Drift from Release Outputs

### Problem

Both `Gaimer-1.0.pkg` and `WitnessDesktop-1.0.pkg` exist in release publish output today. That is not acceptable for a polished shipping pipeline.

### Plan

1. Identify whether the duplicate package names are produced by the toolchain, old publish state, or stale output retention.
2. Ensure the release artifact root is fully cleaned before each run.
3. Ensure only the intended package/product name survives in the final artifact directory.
4. Add a validation check that fails if a stale `WitnessDesktop` package appears after a clean release run.

### Expected outcome

Product identity becomes singular again: one app name, one release naming scheme, one artifact family.

---

## Workstream G: Documentation Promotion After Verification

### Problem

The current live instructions reflect a working but transitional state. They include direct copy commands, historical assumptions, and a mix of local testing and release guidance.

### Plan

After implementation and verification, update the live instruction surfaces in this order:

1. [WITNESS/AGENT_HANDOFF_INSTRUCTIONS.md](/Users/tonynlemadim/Developer/gAImer_desktop/WITNESS/AGENT_HANDOFF_INSTRUCTIONS.md)
2. [CLAUDE.md](/Users/tonynlemadim/Developer/gAImer_desktop/CLAUDE.md)
3. [README.md](/Users/tonynlemadim/Developer/gAImer_desktop/README.md)
4. [WITNESS/PROGRESS_LOG.md](/Users/tonynlemadim/Developer/gAImer_desktop/WITNESS/PROGRESS_LOG.md)
5. [chronicles/DECISION_LOG.md](/Users/tonynlemadim/Developer/gAImer_desktop/chronicles/DECISION_LOG.md), if this results in a stable process decision

### Promotion rule

Do not update those files to describe the new process until the process has been:

1. implemented
2. exercised locally
3. verified for icon correctness
4. verified for signing/notarization correctness where applicable

---

## Implementation Plan

## Phase 1: Build Hygiene and Flow Separation

### Tasks

1. Create `scripts/dev-deploy-mac.sh`.
2. Refactor `scripts/build-release-mac.sh` so it no longer shares dev-only assumptions.
3. Introduce a dedicated artifact root for macOS deliverables.
4. Remove reliance on README/CLAUDE one-liner build chains as operational truth.
5. Add cleanup of stale release artifacts before each run.

### Success criteria

- one supported dev deploy command
- one supported release command
- no manual deploy one-liner needed in live instructions
- no stale `Gaimer` or `WitnessDesktop` product outputs left in the shipping artifact root

---

## Phase 2: Canonical Release Bundle Selection

### Tasks

1. Compare universal release app vs RID-specific release app as the release source.
2. Build and inspect each candidate's icon resources and plist metadata.
3. Deploy each candidate via the supported local path.
4. Observe Dock icon behavior, signing behavior, and launch behavior.
5. Select the canonical release source.

### Success criteria

- one release source bundle selected intentionally
- reason for selection documented
- no "pick whatever bundle exists" behavior left in the script

---

## Phase 3: Icon Integrity Hardening

### Tasks

1. Add scripted verification for icon resources and plist keys.
2. Determine whether any post-build icon repair is still needed.
3. If repair is needed, source it from release-stage assets, not debug-stage assets.
4. Fail the build if icon integrity cannot be established.

### Success criteria

- canonical release bundle launches with correct Dock icon
- build fails if icon assets are absent or inconsistent
- release no longer depends on debug output presence

---

## Phase 4: Release Validation

### Tasks

1. Run local dev deploy flow.
2. Run release build flow without notarization.
3. Run full notarized release flow if credentials are available.
4. Verify:
   - launch
   - Dock icon
   - signature
   - artifact naming
   - absence of stale shipping outputs

### Success criteria

- local dev deploy works on the current machine
- release build produces a clean, expected artifact set
- notarized flow remains intact

---

## Phase 5: Documentation Promotion

### Tasks

1. Replace historical commands in live docs with the canonical scripts.
2. Document the chosen release bundle strategy.
3. Document the artifact root and expected outputs.
4. Document the verification checklist.
5. Record the decision and rationale in chronicles if the process becomes stable.

### Success criteria

- AGENT/HANDOFF/README/CLAUDE are aligned
- no contradictory macOS build instructions remain
- future agents inherit a clean, single path

---

## Verification Matrix

The work is not complete until every row below is green.

| Area | Check | Required |
|------|-------|----------|
| Dev deploy | Supported script deploys app to `/Applications/Gaimer.app` | Yes |
| Dev deploy | App launches successfully | Yes |
| Dev deploy | Correct Dock icon displays | Yes |
| Dev deploy | TCC-sensitive features still work after signing | Yes |
| Release build | Canonical release bundle chosen explicitly | Yes |
| Release build | Final artifact root contains only intended outputs | Yes |
| Release build | No stale `WitnessDesktop` package remains in release outputs | Yes |
| Release build | Icon resources and plist keys verified by script | Yes |
| Release build | Code signing verifies cleanly | Yes |
| Release build | Notarization and staple remain valid, if run | Yes |
| Storage hygiene | Finder no longer reflects a confusing pile of repo-side Gaimer app outputs after normal use | Yes |
| Documentation | Live instructions updated only after verification | Yes |

---

## Acceptance Criteria

This project is considered complete only when all of the following are true:

1. A new developer can use one documented command to locally deploy and test the macOS app.
2. A release manager can use one documented command to create the macOS release artifact set.
3. The canonical release bundle displays the correct Dock icon.
4. The release process does not depend on an existing debug build.
5. The shipping artifact directory does not contain duplicate or stale product identities.
6. Repository instructions describe only the verified process.

---

## Risks and Notes

### Risk 1: The MAUI publish icon problem may be upstream

It is possible the RID-specific release icon issue is an upstream .NET MAUI or Apple toolchain behavior. If so, the permanent solution in this repository will be a **permanent mitigation**, not necessarily an upstream fix.

That is acceptable, provided the mitigation is deterministic and verified.

### Risk 2: Universal release may introduce other tradeoffs

If the universal release app is the only variant with reliable icon assets, we must still confirm it signs, notarizes, launches, and behaves correctly on the current hardware.

### Risk 3: Finder Storage classification is not entirely deterministic

Finder's Storage categories and duplicate heuristics are computed by macOS, not by this repository. We can control artifact hygiene and reduce duplicate-looking app bundles, but we cannot guarantee every Finder label such as `Duplicates` or `Other`.

What we can and must guarantee is that the supported build flow no longer sprays multiple product-like Gaimer outputs across the machine by default.

---

## Recommended Immediate Execution Order

1. Implement dev/release flow separation.
2. Move to a dedicated macOS artifact root.
3. Test universal release bundle as the canonical source candidate.
4. Add icon and plist verification.
5. Re-run local deploy and release packaging.
6. Verify Dock icon.
7. Only then update live instructions.

---

## Final Deliverables Expected from This Plan

1. Hardened macOS dev deploy script
2. Hardened macOS release build script
3. Deterministic macOS artifact directory structure
4. Verified Dock icon behavior on the canonical path
5. Updated live instructions after successful verification

---

## References

### Repository references

- [WitnessDesktop.csproj](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj#L9)
- [build-release-mac.sh](/Users/tonynlemadim/Developer/gAImer_desktop/scripts/build-release-mac.sh#L62)
- [CLAUDE.md](/Users/tonynlemadim/Developer/gAImer_desktop/CLAUDE.md#L16)
- [WITNESS/PROGRESS_LOG.md](/Users/tonynlemadim/Developer/gAImer_desktop/WITNESS/PROGRESS_LOG.md#L7)
- [Platforms/MacCatalyst/Info.plist](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/Info.plist#L33)

### External references

- Microsoft Learn: [.NET MAUI publish outside the Mac App Store](https://learn.microsoft.com/en-us/dotnet/maui/mac-catalyst/deployment/publish-outside-app-store?view=net-maui-9.0)
- Microsoft Learn: [.NET MAUI app icons](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/app-icons?view=net-maui-9.0)
- Apple Developer: [CFBundleIconFile](https://developer.apple.com/documentation/bundleresources/information-property-list/cfbundleiconfile)

