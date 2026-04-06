# CoD Video Analysis Guide — Extraction Parameters

**Derived from:** Shoothouse HC Cyber Attack annotation (5:55, ~119 segments, 142 screenshots)
**Purpose:** Instructions for video analyzer (Gemini) to reliably extract structured data from Call of Duty MW3 gameplay footage
**Target consumer:** Demo Mockservice Creator agent → IBrainService C# mock

---

## Always-On HUD Elements (Every Gameplay Frame)

These elements are present on every in-game frame (not menus, replays, or scoreboards). Extract on every segment.

### 1. Compass (Top Center)

**Reliability: HIGHEST — present on every gameplay frame**

- **Bearing:** Numeric 0-360, displayed at compass center
- **Cardinal direction:** N, NE, E, SE, S, SW, W, NW — shown as tick labels
- **Location callout:** Named area directly below bearing (e.g., JUNKYARD, SHANTYTOWN, CENTER)

**Extraction format:**
```
facing:DIR(bearing) at:CALLOUT
```
**Examples from annotation:**
- `facing:E(104) at:FORKLIFT`
- `facing:NW(348) at:CENTER`
- `facing:SW(263) at:JUNKYARD`

**Movement notation (when direction changes within segment):**
```
facing:E→NE at:FORKLIFT→CONTAINERS
```

**Known callout names on Shoothouse:**
ALPHA_GATE, BRAVO_GATE, FORKLIFT, CONTAINERS, CENTER, SHANTYTOWN, JUNKYARD, COURTYARD, UNDERPASS

### 2. Player Weapon State

**Reliability: HIGH — always visible in first-person view**

Extractable states:
- **ADS (Aim Down Sights):** Scope/optic fills center screen — player is aiming precisely
- **Hip-fire:** Weapon low and forward — moving or scanning
- **Sprinting:** Weapon tilted/lowered — player running, cannot fire
- **Reloading:** Magazine animation visible — temporarily vulnerable
- **Scoped:** Circular scope view with crosshairs — zoomed in, limited peripheral vision

**RULE: Do NOT identify weapon type from visual appearance.** Only identify weapons from:
- Loadout selection menus (shown pre-round)
- FINAL KILL replay weapon info (bottom-right panel)
- Kill feed weapon icons (small but sometimes readable)

### 3. Objective Markers

**Reliability: HIGH — present when objectives are active**

- **BOMB marker:** Icon + distance in meters (e.g., "BOMB 29m") — shows bomb location
- **DEFEND marker:** Icon + distance — shows team's data center location
- **Teammate distance markers:** Blue tags with name + distance (e.g., "Lulu350z 12m")

---

## Frequent HUD Elements (Most Gameplay Frames)

### 4. Kill Feed (Bottom Left)

**Reliability: HIGHEST — present on nearly every frame during action**

The kill feed is the single most information-dense element. It shows ALL kills across the entire map, not just the player's.

**Color coding rules (HC Cyber Attack):**
| Pattern | Meaning |
|---------|---------|
| **Yellow text (left)** killed name (right) | Player's own kill |
| **Blue name (left)** killed **Red name (right)** | Teammate killed enemy |
| **Red name (left)** killed **Blue name (right)** | Enemy killed teammate |
| **Blue name (left)** killed **Blue name (right)** | Teamkill (friendly fire — HC mode) |

**Kill feed provides:**
- Who killed whom (gamertags)
- Weapon icon (small, between names)
- Sequence of engagements across the map
- Team momentum (consecutive kills by one side = dominating)

**Extraction format:**
```
Kill feed: [PlayerName] killed [VictimName]
Kill feed: [PlayerName] killed [VictimName], [PlayerName2] killed [VictimName2]
```

**Key patterns observed in annotation:**
- Streaks: same player appearing multiple times in feed = hot hand
- Trades: Player A kills Player B, Player B kills Player C (simultaneous engagements)
- Doubles: Same player kills two enemies in rapid succession
- Feed going quiet: lull in action, teams resetting

### 5. Teammate Name Tags

**Reliability: HIGH — visible when teammates are in line of sight**

- Blue diamond icon + gamertag + distance
- Shows who is nearby and their approximate position
- Useful for team positioning and coordination context
- Tags appear through walls at close range

### 6. Accolade / Medal Popups (Top Right)

**Reliability: MEDIUM — event-driven, trails after kills**

Accolades appear in the top-right corner after kills or special events. They TRAIL a kill — they are NOT separate events.

**Common accolades observed:**
| Accolade | Meaning | Annotation frequency |
|----------|---------|---------------------|
| ONE SHOT ONE KILL | Single bullet kill | High (Wakandan's signature) |
| LONGSHOT (+ distance) | Kill beyond standard range | High (e.g., 59.88M) |
| DEFENSE | Kill while defending objective | Medium |
| HFA | Headshot from afar? | Medium |
| FIRST BLOOD | First kill of the round | Once per round |
| BOMB DRONE | Used bomb drone equipment | Rare |
| 5 KILL STREAK | 5 kills without dying | Event-driven |
| PICKED UP THE BOMB | Player grabbed neutral bomb | Event-driven |
| ARMED BOMB | Player planted the bomb | Event-driven |

**RULE: Accolades trail kills. Do not count them as separate kill events.** If you see "ONE SHOT ONE KILL" it confirms the previous kill was a single-shot elimination, not a new kill.

---

## Periodic / Event-Driven Elements

### 7. Round State Screens

**Reliability: GUARANTEED at specific moments**

These appear between rounds and provide critical structural data:

| Screen | When | Data |
|--------|------|------|
| Pre-round countdown | Before each round | Countdown timer, spawn location |
| Round objective splash | Round start | "HARDCORE CYBER ATTACK — Locate the bomb..." |
| ROUND WIN / ROUND LOSS | Round end | "141 ELIMINATED" or bomb detonation |
| SWITCHING SIDES | Halftime | Score, side swap |
| MATCH POINT | Before potential final round | Score |
| VICTORY / DEFEAT | Match end | Final score |

**Extract from these screens:**
- Round number (derived from score progression)
- Score (OPFOR X — TASK FORCE 141 Y)
- Which side the player is on (attack/defend)
- Win condition (elimination vs bomb)

### 8. Scoreboard

**Reliability: GUARANTEED — shown after every round**

Provides the most structured data in the entire video. Always extract fully.

**Fields per player:**
- Gamertag (with clan tag and ID number)
- Score (numeric)
- Kills (numeric)
- Deaths (numeric)

**Additional scoreboard data:**
- Team roster count (e.g., [6/6] or [4/6] = players left/disconnected)
- Timer remaining
- Map name + mode confirmation (header)

**Extraction format:**
```
OPFOR [6/6]: PlayerA NK/ND(Score), PlayerB NK/ND(Score), ...
TASK FORCE 141 [4/6]: PlayerC NK/ND(Score), ...
```

### 9. FINAL KILL Replay

**Reliability: GUARANTEED — one per round**

The FINAL KILL replay is the richest single data source in the video. It shows the last kill of each round from the killer's POV and reveals data not visible during gameplay:

**Always shown in FINAL KILL:**
- **KILLED BY:** Gamertag + ID number
- **Operator skin:** Name shown top-left (e.g., TANTO)
- **Weapon + full attachments:** Listed on bottom-right panel
  - Optic (e.g., FSS SPECTRE MICROTHERM)
  - Ammunition (e.g., 5.7x28mm OVERPRESSURED +P)
  - Underbarrel (e.g., BRUEN HEAVY SUPPORT GRIP)
  - Muzzle (e.g., QUARTERMASTER SUPPRESSOR)
- **Perk:** Bottom-left (e.g., NINJA VEST, DEMOLITION VEST)
- **Score/rank:** Numeric value
- **"YOU" tag:** On the victim's body from spectator perspective

**This is the ONLY reliable source for weapon identification and loadout details.** In-game weapon appearance should NOT be used to identify weapons.

### 10. System Alerts

**Reliability: HIGH — always shown when triggered**

| Alert | Trigger | Priority |
|-------|---------|----------|
| "ENEMY HAS THE BOMB!" | Enemy picks up bomb | HIGH — objective threat |
| "ARMED BOMB!" | Bomb planted | CRITICAL — time pressure |
| "Bomb is armed, defend it" | Your team planted | CRITICAL — defend |
| "[Player] left the game" | Ragequit/disconnect | MEDIUM — roster change |
| MATCH POINT | Score is 4-0 (or match point) | HIGH — stakes |
| "SWITCHING SIDES" | Halftime | STRUCTURAL — side swap |

---

## HC-Specific Constraints

Hardcore Cyber Attack has a LIMITED HUD compared to Core modes:

| Element | Core | HC |
|---------|------|----|
| Minimap | Always on | OFF (only with UAV/Recon Drone) |
| Kill feed | Full | Present but limited visibility |
| Hit markers | Standard | Present |
| Teammate tags | Always | Present (critical for identifying friendlies) |
| Health bar | Visible | Hidden |
| Friendly fire | Off | ON — teamkills possible |

**When UAV/Recon Drone activates:**
- Minimap appears top-left temporarily
- Red dots show enemy positions
- This is a HIGH-VALUE intel moment — note enemy positions relative to map layout

---

## Derived Tactical Data (Not On HUD — Inferred from Patterns)

These are not directly readable from HUD but were consistently identifiable through annotation:

### Player Strategy Evolution
Track how the player's opening play changes across rounds:
- R1/R2: Gate rush → hold sightline (ALPHA_GATE)
- R3: Same playbook, different gate (BRAVO_GATE) → died
- R4: Skipped gate entirely, went to SHANTYTOWN direct → adapted
- R5: Full mobile rotation, no anchor → maximally adapted

**Pattern:** Players who die at a position will avoid it next round. Track position→death→adaptation.

### Deterrence Salvo
Firing at cover/concealment with no visible target. Identified by:
- ADS or hip-fire with bullet impacts on surfaces
- No hit markers (red or white)
- No kill feed entry following the shots
- Player moves after firing (not holding for a kill)

Only viable with bullet penetration ammunition. Note when observed.

### Team Momentum Indicators
- 3+ consecutive kill feed entries for one team = dominating
- Player ragequits = team collapse (note roster count changes)
- Replacement players (new names appearing mid-match) = backfill
- Kill streak accolades = individual carrying

### Round Win Conditions
In Cyber Attack, rounds end by:
1. **Elimination:** "141 ELIMINATED" / "OPFOR ELIMINATED" — all enemies killed
2. **Bomb detonation:** Bomb planted and timer expires
3. **Bomb defuse:** Planted bomb successfully defused (didn't occur in this match)

---

## Extraction Priority (What to Always Capture)

### Tier 1 — ALWAYS extract (every segment):
1. **Compass:** bearing + location callout
2. **Kill feed:** all entries with color/team attribution
3. **Round score:** current round number and score
4. **Player state:** ADS/hip-fire/sprinting/reloading/dead/spectating

### Tier 2 — Extract when present:
5. **Objective markers:** BOMB/DEFEND distance
6. **Accolades:** medal type + associated kill
7. **Teammate proximity:** who is nearby
8. **System alerts:** bomb status, ragequits, match state

### Tier 3 — Extract when available (event-driven):
9. **Scoreboard:** full team stats after each round
10. **FINAL KILL replay:** operator, weapon, attachments, perks
11. **Minimap:** enemy positions when UAV/Recon active
12. **Strategy observations:** positioning changes, adaptations, rotations

---

## Annotation Conventions

### Timestamp Format
```
### M:SS - M:SS
```
3-second segments aligned to video timeline.

### Segment Content Format
```
> facing:DIR(bearing) at:CALLOUT — action description
> Kill feed: [Player] killed [Victim] — context
> ***HIGH URGENCY MOMENT — narration emphasis***
```

### Urgency Markers
- `***text***` = high urgency, give narration emphasis
- Standard `>` lines = normal narration weight
- Kill events involving the player = always notable
- Round-ending events = always notable

### Player Reference
- First mention in a round: full gamertag with clan tag
- Subsequent: shortened name (e.g., "WindowLick3r" not "[OMEN]WindowLick3r")
- Player being analyzed: "Wakandan" (never "the player")

---

## Match Structure (HC Cyber Attack)

```
MATCH
├── Round 1-2: Team A attacks, Team B defends
├── HALFTIME: SWITCHING SIDES
├── Round 3-4: Team A defends, Team B attacks
├── Round 5+: Alternate (if needed)
└── First to 5 rounds wins (or 6 in some formats)

EACH ROUND:
├── Pre-round countdown (3-5 seconds)
├── Gameplay (up to round time limit)
├── Round end (elimination or bomb)
├── Scoreboard
└── FINAL KILL replay
```

Side switches observed in this match:
- R1-R2: Wakandan on OPFOR (attacking)
- R3-R4: Wakandan on OPFOR (defending) — side switch
- R5: Wakandan on OPFOR (attacking) — side switch again
