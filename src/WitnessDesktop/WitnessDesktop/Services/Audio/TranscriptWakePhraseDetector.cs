using WitnessDesktop.Services;

namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Fuzzy wake phrase detector with two-tier matching:
/// 1. Exact case-insensitive match (fast path)
/// 2. Levenshtein distance fallback for STT variants (e.g., "hey Larry" for "hey Leroy")
///
/// D-AI-7: Porcupine on-device wake word engine is the production upgrade path.
/// This fuzzy detector serves as the fallback when Porcupine is unavailable.
/// </summary>
public sealed class TranscriptWakePhraseDetector : IWakePhraseDetector
{
    /// <summary>
    /// Maximum Levenshtein distance to accept as a wake phrase match
    /// for the two-word sliding window tier.
    /// 2 balances STT tolerance ("Larry"→"Leroy" dist 2) against false positives
    /// ("hey wait"→"hey wasp" dist 3, correctly rejected). Porcupine (D-AI-7) is the
    /// production upgrade path for higher accuracy.
    /// </summary>
    public const int DefaultMaxDistance = 2;

    private readonly int _maxDistance;
    private readonly ISessionTraceService? _sessionTrace;

    public TranscriptWakePhraseDetector(int maxDistance = DefaultMaxDistance, ISessionTraceService? sessionTrace = null)
    {
        _maxDistance = maxDistance;
        _sessionTrace = sessionTrace;
    }

    public bool TryDetectWake(string transcript, string agentName, out string? matchedPhrase)
    {
        matchedPhrase = null;

        if (string.IsNullOrWhiteSpace(transcript) || string.IsNullOrWhiteSpace(agentName))
            return false;

        var wakePhrase = $"hey {agentName}";
        int bestDistance = int.MaxValue;

        // Tier 1: Exact case-insensitive match (fast path)
        var idx = transcript.IndexOf(wakePhrase, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            matchedPhrase = transcript.Substring(idx, wakePhrase.Length);
            TraceWakeResult(matched: true, confidence: 1.0f, matchedPhrase);
            return true;
        }

        // Tier 2: Fuzzy match via sliding window over word pairs
        var words = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length - 1; i++)
        {
            var candidate = $"{words[i]} {words[i + 1]}";
            var distance = LevenshteinDistance(candidate.ToLowerInvariant(), wakePhrase.ToLowerInvariant());
            if (distance < bestDistance) bestDistance = distance;
            if (distance <= _maxDistance)
            {
                matchedPhrase = candidate;
                var confidence = 1.0f - ((float)distance / wakePhrase.Length);
                TraceWakeResult(matched: true, confidence, matchedPhrase);
                return true;
            }
        }

        // Tier 3: Single-word check for STT merging ("heyleroy" from "hey leroy")
        // Tighter threshold (maxDistance / 2) to avoid false positives on bare agent names.
        var mergedThreshold = _maxDistance / 2;
        var wakeMerged = wakePhrase.Replace(" ", "").ToLowerInvariant();
        for (int i = 0; i < words.Length; i++)
        {
            var distance = LevenshteinDistance(words[i].ToLowerInvariant(), wakeMerged);
            if (distance < bestDistance) bestDistance = distance;
            if (distance <= mergedThreshold)
            {
                matchedPhrase = words[i];
                var confidence = 1.0f - ((float)distance / wakeMerged.Length);
                TraceWakeResult(matched: true, confidence, matchedPhrase);
                return true;
            }
        }

        // No match — skip tracing (high-volume, low-signal path)
        return false;
    }

    private void TraceWakeResult(bool matched, float confidence, string? phrase)
    {
        _sessionTrace?.TrackEvent("audio.wake.fuzzy_match", new Dictionary<string, string>
        {
            ["matched"] = matched.ToString(),
            ["confidence"] = confidence.ToString("F2"),
            ["phrase"] = phrase ?? ""
        });
    }

    /// <summary>
    /// Single-row stackalloc Levenshtein distance. O(m*n) time, O(n) stack memory.
    /// Avoids heap allocation for short wake phrases (~10 chars) called at transcript frequency.
    /// </summary>
    internal static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var m = a.Length;
        var n = b.Length;
        Span<int> prev = stackalloc int[n + 1];
        Span<int> curr = stackalloc int[n + 1];

        for (int j = 0; j <= n; j++) prev[j] = j;

        for (int i = 1; i <= m; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= n; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(prev[j] + 1, curr[j - 1] + 1), prev[j - 1] + cost);
            }
            var tmp = prev;
            prev = curr;
            curr = tmp;
        }

        return prev[n];
    }
}
