using System.Text.Json;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Tests.GamePacks;

public class PackDrivenRouterTests
{
    private readonly GameSkillPack _chessPack;

    public PackDrivenRouterTests()
    {
        _chessPack = new GameSkillPack
        {
            Id = "chess-online",
            TopStripField = "position_assessment",
            ObservationSchema = new ObservationSchema
            {
                SchemaName = "chess_analysis",
                Fields = new()
                {
                    new() { Key = "visual_description", Route = "diagnostic" },
                    new() { Key = "position_assessment", Route = "visual" },
                    new() { Key = "threats", Route = "visual" },
                    new() { Key = "suggested_action", Route = "visual" },
                    new() { Key = "fen", Route = "temporal" },
                    new() { Key = "last_move", Route = "temporal" },
                    new() { Key = "confidence", Route = "metadata" },
                }
            },
            EventMapping = new()
            {
                new() { Field = "threats", EventType = "Danger" },
                new() { Field = "position_assessment", EventType = "Assessment" },
                new() { Field = "suggested_action", EventType = "SageAdvice" },
            }
        };
    }

    [Fact]
    public void RouteObservations_ChessPack_VisualFieldsEmitCorrectEventTypes()
    {
        var observations = JsonDocument.Parse("""
        {
            "visual_description": "White king on e1",
            "position_assessment": "White is slightly better",
            "threats": "Knight fork on f7",
            "suggested_action": "Play Nf7+",
            "fen": "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            "last_move": "e2e4",
            "confidence": "CERTAIN"
        }
        """).RootElement;

        var events = RouteObservationsToEvents(_chessPack, observations);

        Assert.Contains(events, e => e.Type == EventOutputType.Danger && e.Content.Contains("Knight fork"));
        Assert.Contains(events, e => e.Type == EventOutputType.Assessment && e.Content.Contains("slightly better"));
        Assert.Contains(events, e => e.Type == EventOutputType.SageAdvice && e.Content.Contains("Nf7+"));
        Assert.DoesNotContain(events, e => e.Content.Contains("White king on e1"));
        Assert.DoesNotContain(events, e => e.Content.Contains("rnbqkbnr"));
    }

    [Fact]
    public void RouteObservations_ChessPack_TopStripGetsPositionAssessment()
    {
        var observations = JsonDocument.Parse("""
        {"position_assessment":"Equal position","confidence":"LIKELY"}
        """).RootElement;

        var topStrip = GetTopStripText(_chessPack, observations);
        Assert.Equal("Equal position", topStrip);
    }

    [Fact]
    public void RouteObservations_ChessPack_TemporalFieldsCreateBrainEvents()
    {
        var observations = JsonDocument.Parse("""
        {"fen":"rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1","last_move":"e2e4","confidence":"CERTAIN"}
        """).RootElement;

        var brainEvents = RouteObservationsToBrainEvents(_chessPack, observations);
        Assert.Contains(brainEvents, e => e.Category == "fen");
        Assert.Contains(brainEvents, e => e.Category == "last_move" && e.Text == "e2e4");
    }

    [Fact]
    public void RouteObservations_ChessPack_EmissionPriorityFollowsEventMappingOrder()
    {
        var observations = JsonDocument.Parse("""
        {"position_assessment":"Equal","threats":"Fork","suggested_action":"Retreat","confidence":"CERTAIN"}
        """).RootElement;

        var events = RouteObservationsToEvents(_chessPack, observations);

        var dangerIdx = events.FindIndex(e => e.Type == EventOutputType.Danger);
        var assessIdx = events.FindIndex(e => e.Type == EventOutputType.Assessment);
        var sageIdx = events.FindIndex(e => e.Type == EventOutputType.SageAdvice);

        Assert.True(dangerIdx < assessIdx, "Danger should come before Assessment");
        Assert.True(assessIdx < sageIdx, "Assessment should come before SageAdvice");
    }

    // Helper: extract visual events from pack routing
    private static List<(EventOutputType Type, string Content)> RouteObservationsToEvents(
        GameSkillPack pack, JsonElement observations)
    {
        var events = new List<(EventOutputType, string)>();
        foreach (var mapping in pack.EventMapping)
        {
            if (!observations.TryGetProperty(mapping.Field, out var value)) continue;
            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (!Enum.TryParse<EventOutputType>(mapping.EventType, out var eventType)) continue;
            events.Add((eventType, text!));
        }
        return events;
    }

    private static string? GetTopStripText(GameSkillPack pack, JsonElement observations)
    {
        if (observations.TryGetProperty(pack.TopStripField, out var value))
            return value.GetString();
        return null;
    }

    private static List<(string Category, string Text)> RouteObservationsToBrainEvents(
        GameSkillPack pack, JsonElement observations)
    {
        var events = new List<(string, string)>();
        var fieldLookup = pack.ObservationSchema.Fields.ToDictionary(f => f.Key);
        foreach (var prop in observations.EnumerateObject())
        {
            if (!fieldLookup.TryGetValue(prop.Name, out var field)) continue;
            if (field.Route != "temporal") continue;
            var text = prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString()
                : prop.Value.GetRawText();
            if (!string.IsNullOrWhiteSpace(text))
                events.Add((prop.Name, text!));
        }
        return events;
    }
}
