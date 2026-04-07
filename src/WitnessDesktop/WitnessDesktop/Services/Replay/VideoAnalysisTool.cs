using System.Text.Json;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Services.Replay;

public sealed class VideoAnalysisTool : IVideoAnalysisTool
{
    private readonly GeminiVideoClient _client;
    private readonly string _flashModel;
    private readonly string _proModel;
    private readonly ISessionTraceService? _sessionTrace;
    private int _consecutiveRateLimits;

    public bool IsCircuitBroken => _consecutiveRateLimits >= 3;

    public VideoAnalysisTool(GeminiVideoClient client, string flashModel, string proModel,
        ISessionTraceService? sessionTrace = null)
    {
        _client = client;
        _flashModel = flashModel;
        _proModel = proModel;
        _sessionTrace = sessionTrace;
    }

    public async Task<VideoAnalysisResult> AnalyzeAsync(ReplaySegment segment, GameSkillPack pack, CancellationToken ct = default)
    {
        if (IsCircuitBroken)
        {
            _sessionTrace?.TrackEvent("replay.tool.circuit_breaker", new Dictionary<string, string>
            {
                ["failure_count"] = _consecutiveRateLimits.ToString()
            });
            throw new GeminiRateLimitException("Gemini analysis circuit breaker is open — automated analysis disabled for this session");
        }

        var segmentId = $"{segment.SessionId}-{segment.SegmentIndex}";
        _sessionTrace?.TrackEvent("replay.tool.analyze", new Dictionary<string, string>
        {
            ["segment_id"] = segmentId
        });

        var prompt = BuildAnalyzePrompt(pack);
        var (responseJson, model) = await ExecuteWithFileAsync(segment.FilePath, prompt, _flashModel, ct);

        var (beats, summary) = ParseAnalysisResponse(responseJson);

        Interlocked.Exchange(ref _consecutiveRateLimits, 0); // Success resets breaker

        return new VideoAnalysisResult
        {
            SegmentId = $"{segment.SessionId}-{segment.SegmentIndex}",
            SessionId = segment.SessionId,
            StartUtc = segment.StartUtc,
            EndUtc = segment.EndUtc,
            RawJson = responseJson,
            Beats = beats,
            NarrativeSummary = summary,
            PackId = pack.Id,
            Model = model
        };
    }

    public async Task<VideoSearchResult> SearchAsync(IReadOnlyList<ReplaySegment> segments, GameSkillPack pack, string query, CancellationToken ct = default)
    {
        // [W6] Guard for empty segments list
        if (segments.Count == 0)
            return new VideoSearchResult { Query = query, Hits = [], Summary = "No segments available for search" };

        _sessionTrace?.TrackEvent("replay.tool.search", new Dictionary<string, string>
        {
            ["query"] = query.Length > 80 ? query[..80] : query,
            ["time_hint"] = segments[0].StartUtc.ToString("O")
        });

        // Search always uses pro model — no circuit breaker check (user-initiated)
        var segment = segments[0]; // For now, search first segment. Multi-segment deferred.
        var prompt = BuildSearchPrompt(pack, query, segments);
        var (responseJson, _) = await ExecuteWithFileAsync(segment.FilePath, prompt, _proModel, ct);

        return ParseSearchResponse(responseJson, query, segment.FilePath);
    }

    private async Task<(string json, string model)> ExecuteWithFileAsync(string filePath, string prompt, string model, CancellationToken ct)
    {
        GeminiFileMetadata? fileMeta = null;
        try
        {
            fileMeta = await _client.UploadVideoAsync(filePath, ct);
            await _client.WaitForActiveAsync(fileMeta.Name, ct);
            var responseJson = await _client.GenerateContentAsync(fileMeta.Uri, prompt, model, ct);
            return (responseJson, model);
        }
        catch (GeminiRateLimitException)
        {
            Interlocked.Increment(ref _consecutiveRateLimits);
            throw;
        }
        finally
        {
            if (fileMeta != null)
                await _client.DeleteFileAsync(fileMeta.Name, ct);
        }
    }

    private static string BuildAnalyzePrompt(GameSkillPack pack)
    {
        return $"""
            You are a game analysis engine. Extract structured observations from every 3-second segment of this gameplay video.

            ## Game Context
            {pack.BrainInstructionsContent}

            ## Output Format
            Return a JSON object with two fields:
            - "beats": array of observations, each with "start_time", "end_time", "signal" (none/danger/opportunity), "urgency" (low/medium/high), "assessment" (what happened), "temporal_context" (how this relates to previous beats)
            - "narrative_summary": 2-3 sentence summary of the entire segment

            ## Rules
            - Extract from every 3-second window. Do NOT skip any time range.
            - Do NOT write narration or commentary. Pure structured extraction.
            - If nothing notable happens in a window, still include it with signal="none".
            - Use game-specific terminology from the context above.
            """;
    }

    private static string BuildSearchPrompt(GameSkillPack pack, string query, IReadOnlyList<ReplaySegment> segments)
    {
        var timeRange = segments.Count > 1
            ? $"This video covers {segments[0].StartUtc:HH:mm:ss} to {segments[^1].EndUtc:HH:mm:ss} of gameplay."
            : $"This is a {segments[0].Duration.TotalMinutes:F0}-minute gameplay segment.";

        return $"""
            You are a game analysis engine performing a targeted search.

            ## Game Context
            {pack.BrainInstructionsContent}

            ## Search Query
            {query}

            ## Video Context
            {timeRange}

            ## Output Format
            Return a JSON object with two fields:
            - "hits": array of matches, each with "start_time", "end_time", "description" (what happened), "confidence" (HIGH/LIKELY/POSSIBLE)
            - "summary": 1-2 sentence answer to the query

            ## Rules
            - Only return segments matching the query. Include 1-2 segments of surrounding context for each hit.
            - If no matches found, return empty hits array with summary explaining why.
            - Use game-specific terminology.
            """;
    }

    private static (IReadOnlyList<AnalyzedBeat> beats, string summary) ParseAnalysisResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("narrative_summary", out var sumEl)
                ? sumEl.GetString() ?? "No summary available"
                : "No summary available";

            var beats = new List<AnalyzedBeat>();
            if (root.TryGetProperty("beats", out var beatsEl))
            {
                foreach (var beat in beatsEl.EnumerateArray())
                {
                    beats.Add(new AnalyzedBeat
                    {
                        StartTime = beat.TryGetProperty("start_time", out var st) ? st.GetString()! : "0:00",
                        EndTime = beat.TryGetProperty("end_time", out var et) ? et.GetString()! : "0:03",
                        Signal = beat.TryGetProperty("signal", out var sig) ? sig.GetString()! : "none",
                        Urgency = beat.TryGetProperty("urgency", out var urg) ? urg.GetString()! : "low",
                        Assessment = beat.TryGetProperty("assessment", out var assess) ? assess.GetString()! : "",
                        TemporalContext = beat.TryGetProperty("temporal_context", out var ctx) ? ctx.GetString() : null
                    });
                }
            }

            return (beats, summary);
        }
        catch
        {
            return ([], "Analysis response could not be parsed");
        }
    }

    private static VideoSearchResult ParseSearchResponse(string json, string query, string segmentPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var sumEl)
                ? sumEl.GetString() ?? "No results"
                : "No results";

            var hits = new List<SearchHit>();
            if (root.TryGetProperty("hits", out var hitsEl))
            {
                foreach (var hit in hitsEl.EnumerateArray())
                {
                    hits.Add(new SearchHit
                    {
                        StartTime = hit.TryGetProperty("start_time", out var st) ? st.GetString()! : "0:00",
                        EndTime = hit.TryGetProperty("end_time", out var et) ? et.GetString()! : "0:03",
                        SegmentFilePath = segmentPath,
                        Description = hit.TryGetProperty("description", out var desc) ? desc.GetString()! : "",
                        Confidence = hit.TryGetProperty("confidence", out var conf) ? conf.GetString()! : "LIKELY"
                    });
                }
            }

            return new VideoSearchResult { Query = query, Hits = hits, Summary = summary };
        }
        catch
        {
            return new VideoSearchResult { Query = query, Hits = [], Summary = "Search response could not be parsed" };
        }
    }
}
