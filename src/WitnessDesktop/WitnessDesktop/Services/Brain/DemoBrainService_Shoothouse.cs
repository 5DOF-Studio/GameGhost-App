using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Brain;

/// <summary>
/// Demo IBrainService generated from Shoothouse HC Cyber Attack gameplay video.
/// 5:55 match, 5-0 OPFOR sweep. Player: Wakandan. Agent voice: RASA.
///
/// Generated: 2026-04-01 by demo-mockservice-creator pipeline.
/// Source: demo-assets/shoothouse-gemini3pro-analysis.json (Gemini 3 Pro extraction)
/// Narration: RASA personality (Claude narration pass)
///
/// Visual data (AnalysisText): structured JSON matching cod-hc-cyber-attack pack schema.
/// Temporal data (BrainHint): signal, urgency, map callout.
/// VoiceNarration: sync-resilient RASA-voiced commentary.
/// </summary>
public sealed class DemoBrainService_Shoothouse : IBrainService
{
    private readonly Channel<BrainResult> _channel;
    private readonly Channel<FrameSubmission> _frameSlot;
    private readonly Task _consumerTask;
    private readonly ILogger<DemoBrainService_Shoothouse> _logger;
    private CancellationTokenSource _cts = new();
    private int _activeTasks;
    private bool _disposed;
    private int _resultIndex;

    private static readonly BrainResult[] DemoResults =
    [
        // Beat 0: 0:42-0:45 [TRANSITION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""The lobby screen fades to black as the game transitions to the map loading screen."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""transition"",""compass"":{""bearing"":0,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""menu"",""round_state"":{""round"":0,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""none"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "There it is. Clean long-range pick through center. ONE SHOT ONE KILL. Logged.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "90007c4c",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "There it is. Clean long-range pick through center. ONE SHOT ONE KILL. Logged.",
                SuggestedMove = null
            }
        },

        // Beat 1: 0:57-1:07 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Objective splash 'HARDCORE CYBER ATTACK' appears. Player maintains their hold on the middle lane, a common opening strategy on Shoot House."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":88,""direction"":""E"",""callout"":""ALPHA_GATE""},""player_state"":""ads"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":49,""status"":""none""}}",
            VoiceNarration = "Showtime. Scope's up, center lane locked. Classic Shoothouse opening.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "d15c1c30",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Showtime. Scope's up, center lane locked. Classic Shoothouse opening.",
                SuggestedMove = "ALPHA_GATE"
            }
        },

        // Beat 2: 1:07-1:10 [ACTION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""First blood occurs elsewhere on the map via an enemy teamkill. Player remains focused on the middle lane."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":88,""direction"":""E"",""callout"":""ALPHA_GATE""},""player_state"":""ads"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":49,""status"":""none""}}",
            VoiceNarration = "First blood elsewhere. Feed's active. Wakandan holding patient — discipline.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "bf973ad2",
            Hint = new BrainHint
            {
                Signal = "engagement",
                Urgency = "medium",
                Summary = "First blood elsewhere. Feed's active. Wakandan holding patient — discipline.",
                SuggestedMove = "ALPHA_GATE"
            }
        },

        // Beat 3: 1:10-1:13 [URGENCY]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player spots an enemy crossing the middle lane and secures a long-range kill, then immediately breaks ADS to sprint forward towards Forklift to gain ground."",""system_alerts"":[],""player_kills"":[{""killer"":""Wakandan"",""victim"":""[CH]WindowLick3r"",""attribution"":""player""}],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":45,""direction"":""NE"",""callout"":""FORKLIFT""},""player_state"":""sprinting"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":35,""status"":""none""}}",
            VoiceNarration = "There it is. Clean long-range pick through center. ONE SHOT ONE KILL. Logged.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "3a5ab80a",
            Hint = new BrainHint
            {
                Signal = "engagement",
                Urgency = "high",
                Summary = "There it is. Clean long-range pick through center. ONE SHOT ONE KILL. Logged.",
                SuggestedMove = "FORKLIFT"
            }
        },

        // Beat 4: 1:13-1:16 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player advances to the Forklift area and quickly aims down sights again to re-check the middle lane for trailing enemies."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":45,""direction"":""NE"",""callout"":""FORKLIFT""},""player_state"":""ads"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":35,""status"":""none""}}",
            VoiceNarration = "Pushed up to Forklift. Scope back on center lane. Smart — don't give up the angle.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "09ba6c37",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Pushed up to Forklift. Scope back on center lane. Smart — don't give up the angl",
                SuggestedMove = "FORKLIFT"
            }
        },

        // Beat 5: 1:16-1:19 [URGENCY]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player spots a second enemy in the middle lane and secures another kill, effectively locking down the central sightline."",""system_alerts"":[],""player_kills"":[{""killer"":""Wakandan"",""victim"":""[LKS]Plate_God02"",""attribution"":""player""}],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":88,""direction"":""E"",""callout"":""FORKLIFT""},""player_state"":""ads"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":35,""status"":""none""}}",
            VoiceNarration = "Second body. Same sightline, same result. Wakandan's owning center right now.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "12ede724",
            Hint = new BrainHint
            {
                Signal = "engagement",
                Urgency = "high",
                Summary = "Second body. Same sightline, same result. Wakandan's owning center right now.",
                SuggestedMove = "FORKLIFT"
            }
        },

        // Beat 6: 1:19-1:25 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player sprints through the Underpass, aggressively closing the distance to the center of the map to contest the bomb carrier."",""system_alerts"":[""ENEMY HAS THE BOMB!""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":88,""direction"":""E"",""callout"":""UNDERPASS""},""player_state"":""sprinting"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":25,""status"":""none""}}",
            VoiceNarration = "Enemy grabbed the bomb. Wakandan pushing through Underpass to contest. Bold move.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "5ee9fc6d",
            Hint = new BrainHint
            {
                Signal = "tactical",
                Urgency = "medium",
                Summary = "Enemy grabbed the bomb. Wakandan pushing through Underpass to contest. Bold move",
                SuggestedMove = "UNDERPASS"
            }
        },

        // Beat 7: 1:25-1:28 [URGENCY]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player spots an enemy on the elevated platform in the center and lands a precise headshot, clearing the path forward."",""system_alerts"":[],""player_kills"":[{""killer"":""Wakandan"",""victim"":""[LKS]Plate_God02"",""attribution"":""player""}],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":88,""direction"":""E"",""callout"":""CENTER""},""player_state"":""ads"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":25,""status"":""none""}}",
            VoiceNarration = "Third kill. Headshot on the platform. Path to bomb is clear. Receipts stacking.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "550beb2d",
            Hint = new BrainHint
            {
                Signal = "engagement",
                Urgency = "high",
                Summary = "Third kill. Headshot on the platform. Path to bomb is clear. Receipts stacking.",
                SuggestedMove = "CENTER"
            }
        },

        // Beat 8: 1:28-1:31 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player pushes up the right side of the map towards the Containers area, moving closer to the bomb's location."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":45,""direction"":""NE"",""callout"":""CONTAINERS""},""player_state"":""sprinting"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":15,""status"":""none""}}",
            VoiceNarration = "Pushing Containers. Getting close to the objective. Pressure building.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "59c1fa9f",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Pushing Containers. Getting close to the objective. Pressure building.",
                SuggestedMove = "CONTAINERS"
            }
        },

        // Beat 9: 1:31-1:34 [URGENCY]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player rounds the corner and accidentally shoots a teammate (Anastasia) due to the lack of HUD indicators in Hardcore mode. The enemy still has the bomb."",""system_alerts"":[""ENEMY HAS THE BOMB!""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":45,""direction"":""NE"",""callout"":""CONTAINERS""},""player_state"":""ads"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":15,""status"":""none""}}",
            VoiceNarration = "Oh no. Teamkill on Anastasia. Hardcore mode — no friendly tags. That's going in the journal.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "a561c15d",
            Hint = new BrainHint
            {
                Signal = "danger",
                Urgency = "high",
                Summary = "Oh no. Teamkill on Anastasia. Hardcore mode — no friendly tags. That's going in ",
                SuggestedMove = "CONTAINERS"
            }
        },

        // Beat 10: 1:34-1:37 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player holds the angle near the containers, watching for enemies pushing from the right side, likely anticipating a flank."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":45,""direction"":""NE"",""callout"":""CONTAINERS""},""player_state"":""ads"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":15,""status"":""none""}}",
            VoiceNarration = "Holding Containers angle. Waiting for the peek. Smart play after the mishap.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "f3bca07e",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Holding Containers angle. Waiting for the peek. Smart play after the mishap.",
                SuggestedMove = "CONTAINERS"
            }
        },

        // Beat 11: 1:37-1:40 [ACTION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player is shot and killed by an enemy from an unseen angle while holding the container sightline."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":45,""direction"":""NE"",""callout"":""CONTAINERS""},""player_state"":""dead"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":15,""status"":""none""}}",
            VoiceNarration = "Down. Enemy had the angle. First death of the match. Containers is a risky hold.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "89626295",
            Hint = new BrainHint
            {
                Signal = "danger",
                Urgency = "high",
                Summary = "Down. Enemy had the angle. First death of the match. Containers is a risky hold.",
                SuggestedMove = "CONTAINERS"
            }
        },

        // Beat 12: 1:40-1:55 [TRANSITION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Still spectating Anastasia. The round is nearing its conclusion."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""spectating"",""compass"":{""bearing"":0,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""spectating"",""round_state"":{""round"":1,""score_friendly"":0,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""none"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Round 2. Same opening — center lane from Alpha Gate. If it works, run it back.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "755cb601",
            Hint = new BrainHint
            {
                Signal = "tactical",
                Urgency = "medium",
                Summary = "Round 2. Same opening — center lane from Alpha Gate. If it works, run it back.",
                SuggestedMove = null
            }
        },

        // Beat 13: 1:55-1:58 [TRANSITION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Round ends. 'ROUND WIN' splash appears. The friendly team successfully won the round."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""transition"",""compass"":{""bearing"":0,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""spectating"",""round_state"":{""round"":1,""score_friendly"":1,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""none"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Round 1 secured. OPFOR takes it 1-0.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "a82aaf78",
            Hint = new BrainHint
            {
                Signal = "objective",
                Urgency = "low",
                Summary = "Round 1 secured. OPFOR takes it 1-0.",
                SuggestedMove = null
            }
        },

        // Beat 14: 2:13-2:22 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player stops and aims down sights again as the objective splash appears, ensuring the lane is clear before advancing further."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":88,""direction"":""E"",""callout"":""ALPHA_GATE""},""player_state"":""ads"",""round_state"":{""round"":2,""score_friendly"":1,""score_enemy"":0,""side"":""defend""},""objective"":{""type"":""defend"",""distance_m"":71,""status"":""none""}}",
            VoiceNarration = "Round 2. Same opening — center lane from Alpha Gate. If it works, run it back.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "d3861f5b",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Round 2. Same opening — center lane from Alpha Gate. If it works, run it back.",
                SuggestedMove = "ALPHA_GATE"
            }
        },

        // Beat 15: 2:22-2:25 [ACTION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player continues to hold the middle lane. An enemy teamkill occurs in the kill feed, similar to the start of Round 1."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":88,""direction"":""E"",""callout"":""ALPHA_GATE""},""player_state"":""ads"",""round_state"":{""round"":2,""score_friendly"":1,""score_enemy"":0,""side"":""defend""},""objective"":{""type"":""defend"",""distance_m"":71,""status"":""none""}}",
            VoiceNarration = "Enemy teamkill in feed. Wakandan reading the same playbook. Noted.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "43ba0f26",
            Hint = new BrainHint
            {
                Signal = "engagement",
                Urgency = "medium",
                Summary = "Enemy teamkill in feed. Wakandan reading the same playbook. Noted.",
                SuggestedMove = "ALPHA_GATE"
            }
        },

        // Beat 16: 2:25-2:28 [URGENCY]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Player spots an enemy crossing the middle lane and lands a long-range headshot, then immediately begins pushing forward towards Forklift."",""system_alerts"":[],""player_kills"":[{""killer"":""Wakandan"",""victim"":""[CH]WindowLick3r"",""attribution"":""player""}],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":45,""direction"":""NE"",""callout"":""FORKLIFT""},""player_state"":""sprinting"",""round_state"":{""round"":2,""score_friendly"":1,""score_enemy"":0,""side"":""defend""},""objective"":{""type"":""defend"",""distance_m"":71,""status"":""none""}}",
            VoiceNarration = "LONGSHOT. 51 meters. Same lane, same victim type. Pattern locked. Clean.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "41dd131d",
            Hint = new BrainHint
            {
                Signal = "engagement",
                Urgency = "high",
                Summary = "LONGSHOT. 51 meters. Same lane, same victim type. Pattern locked. Clean.",
                SuggestedMove = "FORKLIFT"
            }
        },

        // Beat 17: 2:28-2:39 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan holds an angle at CONTAINERS, aiming down sights towards the red crate, anticipating enemy movement."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":118,""direction"":""ESE"",""callout"":""CONTAINERS""},""player_state"":""ads"",""round_state"":{""round"":3,""score_friendly"":2,""score_enemy"":0,""side"":""defend""},""objective"":{""type"":""defend"",""distance_m"":76,""status"":""none""}}",
            VoiceNarration = "Pushed to Containers. Defensive setup holding strong.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "7ea5a1d7",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Pushed to Containers. Defensive setup holding strong.",
                SuggestedMove = "CONTAINERS"
            }
        },

        // Beat 18: 2:39-2:42 [URGENCY]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan spots an enemy and secures a kill with a single sniper shot. The team's defensive setup is paying off."",""system_alerts"":[],""player_kills"":[{""killer"":""Wakandan"",""victim"":""[KBS]Plato_God02"",""attribution"":""player""}],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":118,""direction"":""ESE"",""callout"":""CONTAINERS""},""player_state"":""ads"",""round_state"":{""round"":3,""score_friendly"":2,""score_enemy"":0,""side"":""defend""},""objective"":{""type"":""defend"",""distance_m"":76,""status"":""none""}}",
            VoiceNarration = "Clean single shot on Plato. The sniper angles are paying dividends.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "f0949cb8",
            Hint = new BrainHint
            {
                Signal = "engagement",
                Urgency = "high",
                Summary = "Clean single shot on Plato. The sniper angles are paying dividends.",
                SuggestedMove = "CONTAINERS"
            }
        },

        // Beat 19: 2:42-2:48 [TRANSITION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Round Win screen continues to display."",""system_alerts"":[""ROUND WIN""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""transition"",""compass"":{""bearing"":118,""direction"":""ESE"",""callout"":""CONTAINERS""},""player_state"":""hip_fire"",""round_state"":{""round"":3,""score_friendly"":2,""score_enemy"":0,""side"":""defend""},""objective"":{""type"":""none"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Round secured. That's 3-0. Dominant defensive showing.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "cad49813",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "Round secured. That's 3-0. Dominant defensive showing.",
                SuggestedMove = "CONTAINERS"
            }
        },

        // Beat 20: 2:57-3:00 [TRANSITION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Teams switch sides. OPFOR is now attacking."",""system_alerts"":[""SWITCHING SIDES""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""transition"",""compass"":{""bearing"":0,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""menu"",""round_state"":{""round"":4,""score_friendly"":2,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""none"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Sides switching. Now defending. Let's see if the playbook adapts.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "cbfa8929",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "Sides switching. Now defending. Let's see if the playbook adapts.",
                SuggestedMove = null
            }
        },

        // Beat 21: 3:00-3:03 [QUIET]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Countdown for Round 4 begins. Wakandan is equipped with a sniper rifle and a suppressed pistol."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":4,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""hip_fire"",""round_state"":{""round"":4,""score_friendly"":2,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Round 4. Sniper and suppressed pistol ready. Defense mode.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "e98ded0e",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "Round 4. Sniper and suppressed pistol ready. Defense mode.",
                SuggestedMove = null
            }
        },

        // Beat 22: 3:03-3:12 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan aims down sights at BRAVO GATE, checking for enemy presence."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":256,""direction"":""WSW"",""callout"":""BRAVO GATE""},""player_state"":""ads"",""round_state"":{""round"":4,""score_friendly"":2,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Bravo Gate this time. Different gate, same concept — hold a sightline early.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "a1c8f049",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Bravo Gate this time. Different gate, same concept — hold a sightline early.",
                SuggestedMove = "BRAVO GATE"
            }
        },

        // Beat 23: 3:12-3:15 [ACTION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan spots an enemy and fires, but misses or lands a non-lethal hit."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":256,""direction"":""WSW"",""callout"":""BRAVO GATE""},""player_state"":""ads"",""round_state"":{""round"":4,""score_friendly"":2,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Shot fired, didn't connect. Rare miss. Adjusting.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "5bea4b25",
            Hint = new BrainHint
            {
                Signal = "engagement",
                Urgency = "high",
                Summary = "Shot fired, didn't connect. Rare miss. Adjusting.",
                SuggestedMove = "BRAVO GATE"
            }
        },

        // Beat 24: 3:15-3:27 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan maintains his position, keeping the sightline covered."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":255,""direction"":""WSW"",""callout"":""SHANTYTOWN""},""player_state"":""ads"",""round_state"":{""round"":4,""score_friendly"":2,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Shantytown angles. Patiently waiting for enemy push.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "fb43ca3c",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Shantytown angles. Patiently waiting for enemy push.",
                SuggestedMove = "SHANTYTOWN"
            }
        },

        // Beat 25: 3:27-3:30 [URGENCY]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""System alerts that the enemy has the bomb. Wakandan immediately sprints back towards BRAVO GATE to intercept."",""system_alerts"":[""ENEMY HAS THE BOMB!""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":288,""direction"":""WNW"",""callout"":""BRAVO GATE""},""player_state"":""sprinting"",""round_state"":{""round"":4,""score_friendly"":2,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Enemy has the bomb! Sprinting to intercept. Defensive urgency.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "0738476e",
            Hint = new BrainHint
            {
                Signal = "danger",
                Urgency = "high",
                Summary = "Enemy has the bomb! Sprinting to intercept. Defensive urgency.",
                SuggestedMove = "BRAVO GATE"
            }
        },

        // Beat 26: 3:30-3:33 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan aims down sights at BRAVO GATE, anticipating the enemy bomb carrier."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":348,""direction"":""NNW"",""callout"":""BRAVO GATE""},""player_state"":""ads"",""round_state"":{""round"":4,""score_friendly"":2,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Bravo Gate, scope up. Waiting for the carrier to show.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "7b9c096f",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "high",
                Summary = "Bravo Gate, scope up. Waiting for the carrier to show.",
                SuggestedMove = "BRAVO GATE"
            }
        },

        // Beat 27: 3:33-3:39 [ACTION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan recovers from the stun, moves to SHANTYTOWN, but is killed by Plato_God02."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":249,""direction"":""WSW"",""callout"":""SHANTYTOWN""},""player_state"":""dead"",""round_state"":{""round"":4,""score_friendly"":2,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Flashed and caught. Plato gets the revenge kill. Second death of the match.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "5ad684b9",
            Hint = new BrainHint
            {
                Signal = "danger",
                Urgency = "high",
                Summary = "Flashed and caught. Plato gets the revenge kill. Second death of the match.",
                SuggestedMove = "SHANTYTOWN"
            }
        },

        // Beat 28: 3:39-3:45 [QUIET]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan continues to spectate as the round progresses."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":0,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""spectating"",""round_state"":{""round"":4,""score_friendly"":2,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Spectating G0atphukr. Team closing it out without Wakandan.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "33bd591b",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "Spectating G0atphukr. Team closing it out without Wakandan.",
                SuggestedMove = null
            }
        },

        // Beat 29: 3:45-3:48 [TRANSITION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Round 4 ends with a win for OPFOR, despite Wakandan's death."",""system_alerts"":[""ROUND WIN""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""transition"",""compass"":{""bearing"":0,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""spectating"",""round_state"":{""round"":4,""score_friendly"":3,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""none"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Round secured anyway. 4-0. Team carried this one.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "ad4461a0",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "Round secured anyway. 4-0. Team carried this one.",
                SuggestedMove = null
            }
        },

        // Beat 30: 4:00-4:03 [QUIET]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Countdown for Round 5 begins. Wakandan is ready with his sniper and pistol."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":4,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""hip_fire"",""round_state"":{""round"":5,""score_friendly"":3,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Round 5. Match point. Let's finish this clean.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "ba0ac035",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "Round 5. Match point. Let's finish this clean.",
                SuggestedMove = null
            }
        },

        // Beat 31: 4:03-4:18 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan continues sprinting through SHANTYTOWN."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":257,""direction"":""WSW"",""callout"":""SHANTYTOWN""},""player_state"":""sprinting"",""round_state"":{""round"":5,""score_friendly"":3,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Changed route — Shantytown direct. Adapted from the death at Bravo Gate. Logged.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "a1868174",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Changed route — Shantytown direct. Adapted from the death at Bravo Gate. Logged.",
                SuggestedMove = "SHANTYTOWN"
            }
        },

        // Beat 32: 4:18-4:21 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan approaches JUNKYARD, preparing for a potential encounter."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":282,""direction"":""WNW"",""callout"":""JUNKYARD""},""player_state"":""sprinting"",""round_state"":{""round"":5,""score_friendly"":3,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Pushing Junkyard. Full mobile rotation. No more anchoring at gates.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "78151965",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Pushing Junkyard. Full mobile rotation. No more anchoring at gates.",
                SuggestedMove = "JUNKYARD"
            }
        },

        // Beat 33: 4:21-4:24 [URGENCY]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan encounters Plato_God02 in JUNKYARD and quickly eliminates him with a pistol shot."",""system_alerts"":[],""player_kills"":[{""killer"":""Wakandan"",""victim"":""[KBS]Plato_God02"",""attribution"":""player""}],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":252,""direction"":""WSW"",""callout"":""JUNKYARD""},""player_state"":""hip_fire"",""round_state"":{""round"":5,""score_friendly"":3,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Plato walks into Wakandan at Junkyard. ONE SHOT. Revenge served cold. Clean.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "30d78c2a",
            Hint = new BrainHint
            {
                Signal = "engagement",
                Urgency = "high",
                Summary = "Plato walks into Wakandan at Junkyard. ONE SHOT. Revenge served cold. Clean.",
                SuggestedMove = "JUNKYARD"
            }
        },

        // Beat 34: 4:24-4:36 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan maintains his position, ensuring no enemies push through CENTER."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":349,""direction"":""NNW"",""callout"":""CENTER""},""player_state"":""ads"",""round_state"":{""round"":5,""score_friendly"":3,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Holding Center. Scope patience. Teammates sweeping the map.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "43b88367",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Holding Center. Scope patience. Teammates sweeping the map.",
                SuggestedMove = "CENTER"
            }
        },

        // Beat 35: 4:36-4:39 [TRANSITION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Round 5 ends with another win for OPFOR. They are dominating the match."",""system_alerts"":[""ROUND WIN""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""transition"",""compass"":{""bearing"":349,""direction"":""NNW"",""callout"":""CENTER""},""player_state"":""hip_fire"",""round_state"":{""round"":5,""score_friendly"":4,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""none"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Round 5 done. That's 5-0. Total domination.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "69bf3683",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "Round 5 done. That's 5-0. Total domination.",
                SuggestedMove = "CENTER"
            }
        },

        // Beat 36: 4:51-4:54 [TRANSITION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Match Point screen appears. OPFOR needs one more round to win the match."",""system_alerts"":[""MATCH POINT""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""transition"",""compass"":{""bearing"":0,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""menu"",""round_state"":{""round"":6,""score_friendly"":4,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""none"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "MATCH POINT. One more round. Don't choke now.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "1ced787d",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "MATCH POINT. One more round. Don't choke now.",
                SuggestedMove = null
            }
        },

        // Beat 37: 4:57-5:00 [QUIET]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Countdown for Round 6 begins. Wakandan is ready."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":4,""direction"":""N"",""callout"":""UNKNOWN""},""player_state"":""hip_fire"",""round_state"":{""round"":6,""score_friendly"":4,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Final round. Wakandan ready. Let's close this out.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "cea58ceb",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "Final round. Wakandan ready. Let's close this out.",
                SuggestedMove = null
            }
        },

        // Beat 38: 5:00-5:15 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan aims down sights at UNDERPASS, covering another potential enemy route."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":15,""direction"":""ENE"",""callout"":""UNDERPASS""},""player_state"":""ads"",""round_state"":{""round"":6,""score_friendly"":4,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Courtyard to Underpass — completely new approach. Full adaptation across six rounds. Logged.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "1d0e7728",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Courtyard to Underpass — completely new approach. Full adaptation across six rou",
                SuggestedMove = "UNDERPASS"
            }
        },

        // Beat 39: 5:15-5:30 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan throws a tactical grenade in JUNKYARD, likely to clear an area or check for enemies."",""system_alerts"":[""FRIENDLY UAV ONLINE""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":149,""direction"":""SSE"",""callout"":""JUNKYARD""},""player_state"":""hip_fire"",""round_state"":{""round"":6,""score_friendly"":4,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Friendly UAV online. Intel on the board. Junkyard rotation with team support.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "717d9878",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Friendly UAV online. Intel on the board. Junkyard rotation with team support.",
                SuggestedMove = "JUNKYARD"
            }
        },

        // Beat 40: 5:30-5:39 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan moves towards CENTER, pushing towards the objective."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":258,""direction"":""WSW"",""callout"":""CENTER""},""player_state"":""sprinting"",""round_state"":{""round"":6,""score_friendly"":4,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Rotating Center. Full mobile play. Every round a different route.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "1c10386e",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "medium",
                Summary = "Rotating Center. Full mobile play. Every round a different route.",
                SuggestedMove = "CENTER"
            }
        },

        // Beat 41: 5:39-5:42 [URGENCY]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""System alerts that the bomb has been armed by a teammate. Wakandan sprints towards FORKLIFT to defend the plant."",""system_alerts"":[""ARMED BOMB!""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":354,""direction"":""N"",""callout"":""FORKLIFT""},""player_state"":""sprinting"",""round_state"":{""round"":6,""score_friendly"":4,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "BOMB PLANTED! Teammate got it down. Defend the site!",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "cf2bcdde",
            Hint = new BrainHint
            {
                Signal = "objective",
                Urgency = "high",
                Summary = "BOMB PLANTED! Teammate got it down. Defend the site!",
                SuggestedMove = "FORKLIFT"
            }
        },

        // Beat 42: 5:42-5:45 [HOLD]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""Wakandan continues sprinting through FORKLIFT to secure the area around the armed bomb."",""system_alerts"":[],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""gameplay"",""compass"":{""bearing"":354,""direction"":""N"",""callout"":""FORKLIFT""},""player_state"":""sprinting"",""round_state"":{""round"":6,""score_friendly"":4,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""bomb"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "Sprinting to Forklift. Securing the plant perimeter.",
            Priority = BrainResultPriority.Interrupt,
            CorrelationId = "4bdaebb7",
            Hint = new BrainHint
            {
                Signal = "position_analysis",
                Urgency = "high",
                Summary = "Sprinting to Forklift. Securing the plant perimeter.",
                SuggestedMove = "FORKLIFT"
            }
        },

        // Beat 43: 5:45-5:48 [TRANSITION]
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = @"{""assessment"":""The match ends with a VICTORY for OPFOR. They successfully defended the armed bomb or eliminated the remaining enemies."",""system_alerts"":[""VICTORY""],""player_kills"":[],""confidence"":""CERTAIN"",""phase"":""transition"",""compass"":{""bearing"":354,""direction"":""N"",""callout"":""FORKLIFT""},""player_state"":""hip_fire"",""round_state"":{""round"":6,""score_friendly"":5,""score_enemy"":0,""side"":""attack""},""objective"":{""type"":""none"",""distance_m"":0,""status"":""none""}}",
            VoiceNarration = "VICTORY. 5-0 sweep. Not a single round dropped. Highlight reel. No notes.",
            Priority = BrainResultPriority.WhenIdle,
            CorrelationId = "61761377",
            Hint = new BrainHint
            {
                Signal = "none",
                Urgency = "low",
                Summary = "VICTORY. 5-0 sweep. Not a single round dropped. Highlight reel. No notes.",
                SuggestedMove = "FORKLIFT"
            }
        }
    ];

    public DemoBrainService_Shoothouse(ILogger<DemoBrainService_Shoothouse> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<BrainResult>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
        _frameSlot = Channel.CreateBounded<FrameSubmission>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        _consumerTask = Task.Run(ConsumeFramesAsync);
        _logger.LogInformation("[DemoBrain-Shoothouse] Initialized with {Count} pre-computed results", DemoResults.Length);
    }

    public ChannelReader<BrainResult> Results => _channel.Reader;
    public bool IsBusy => Volatile.Read(ref _activeTasks) > 0;
    public string ProviderName => "Demo Brain (Shoothouse HC Cyber Attack)";

    public bool TrySubmitFrame(byte[] imageData, string context)
    {
        var submission = new FrameSubmission(imageData, context, DateTime.UtcNow);
        return _frameSlot.Writer.TryWrite(submission);
    }

    private async Task ConsumeFramesAsync()
    {
        try
        {
            await foreach (var frame in _frameSlot.Reader.ReadAllAsync())
            {
                Interlocked.Increment(ref _activeTasks);
                try
                {
                    await Task.Delay(3000, _cts.Token);
                    var index = Interlocked.Increment(ref _resultIndex) % DemoResults.Length;
                    var template = DemoResults[index];
                    var result = template with
                    {
                        CreatedAt = DateTimeOffset.UtcNow,
                        CorrelationId = Guid.NewGuid().ToString("N")[..8]
                    };
                    await _channel.Writer.WriteAsync(result, _cts.Token);
                    Console.WriteLine($"[DemoBrain] >>> EMITTED result {index + 1}/{DemoResults.Length} type={result.Type} signal={result.Hint?.Signal ?? "none"} narration={(result.VoiceNarration?[..Math.Min(60, result.VoiceNarration.Length)] ?? "null")}");
                    _logger.LogDebug("[DemoBrain-Shoothouse] Emitted {Index}/{Total} ({Signal})",
                        index + 1, DemoResults.Length, result.Hint?.Signal ?? "none");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[DemoBrain] Frame processing cancelled");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DemoBrain] !!! ERROR in frame consumer: {ex.Message}");
                    _logger.LogError(ex, "[DemoBrain-Shoothouse] Error in frame consumer");
                }
                finally
                {
                    Interlocked.Decrement(ref _activeTasks);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[DemoBrain-Shoothouse] Consumer loop ended");
        }
    }

    public Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TrySubmitFrame(imageData, context);
        return Task.CompletedTask;
    }

    public Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        _ = Task.Run(async () =>
        {
            Interlocked.Increment(ref _activeTasks);
            try
            {
                await Task.Delay(300, _cts.Token);
                var result = new BrainResult
                {
                    Type = BrainResultType.ToolResult,
                    AnalysisText = "[Demo] Shoothouse HC Cyber Attack. Wakandan 4K/2D, 5-0 sweep.",
                    VoiceNarration = "Demo replay question. Noted.",
                    Priority = BrainResultPriority.WhenIdle,
                    CorrelationId = correlationId
                };
                await _channel.Writer.WriteAsync(result, _cts.Token);
            }
            catch (OperationCanceledException) { }
            finally { Interlocked.Decrement(ref _activeTasks); }
        }, CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task<string> ChatAsync(string userQuery, IReadOnlyList<ChatMessage> chatHistory, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Task.Delay(200, ct);
        return "[Demo] Shoothouse HC Cyber Attack. Wakandan 4-2 KD, 5-0 sweep. Anastasia MVP (8K).";
    }

    public void CancelAll()
    {
        _logger.LogInformation("[DemoBrain-Shoothouse] CancelAll");
        var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        try { oldCts.Cancel(); } catch { }
        oldCts.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _logger.LogInformation("[DemoBrain-Shoothouse] Disposing");
        CancelAll();
        _frameSlot.Writer.TryComplete();
        _channel.Writer.TryComplete();
        _consumerTask.Wait(TimeSpan.FromMilliseconds(500));
        _cts.Dispose();
    }
}
