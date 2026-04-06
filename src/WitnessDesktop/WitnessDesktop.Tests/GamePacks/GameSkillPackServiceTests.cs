using System.Text.Json;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.GamePacks;

public class GameSkillPackModelTests
{
    [Fact]
    public void ObservationField_DeserializesFromJson()
    {
        var json = """{"key":"threats","type":"string?","required":false,"route":"visual","description":"Key threat"}""";
        var field = JsonSerializer.Deserialize<ObservationField>(json);

        Assert.NotNull(field);
        Assert.Equal("threats", field.Key);
        Assert.Equal("string?", field.Type);
        Assert.False(field.Required);
        Assert.Equal("visual", field.Route);
        Assert.Equal("Key threat", field.Description);
    }

    [Fact]
    public void EventMappingEntry_DeserializesFromJson()
    {
        var json = """{"field":"threats","eventType":"Danger"}""";
        var entry = JsonSerializer.Deserialize<EventMappingEntry>(json);

        Assert.NotNull(entry);
        Assert.Equal("threats", entry.Field);
        Assert.Equal("Danger", entry.EventType);
    }

    [Fact]
    public void GameSkillPack_DeserializesFullManifest()
    {
        var json = """
        {
          "id": "chess-online",
          "name": "Chess (Online)",
          "version": "1.0.0",
          "genre": "chess",
          "observationSchema": {
            "schemaName": "chess_analysis",
            "fields": [
              {"key":"position_assessment","type":"string","required":true,"route":"visual","description":"Who is better"}
            ]
          },
          "eventMapping": [{"field":"position_assessment","eventType":"Assessment"}],
          "topStripField": "position_assessment",
          "userPromptTemplate": "Current game: chess. Position #{moveNumber}.",
          "brainInstructions": "brain-instructions.md"
        }
        """;
        var pack = JsonSerializer.Deserialize<GameSkillPack>(json);

        Assert.NotNull(pack);
        Assert.Equal("chess-online", pack.Id);
        Assert.Equal("chess", pack.Genre);
        Assert.Single(pack.ObservationSchema.Fields);
        Assert.Single(pack.EventMapping);
        Assert.Equal("position_assessment", pack.TopStripField);
    }

    [Fact]
    public void BuildResponseFormat_ChessFields_MatchesExpectedSchema()
    {
        var schema = new ObservationSchema
        {
            SchemaName = "chess_analysis",
            Fields = new List<ObservationField>
            {
                new() { Key = "visual_description", Type = "string", Required = true, Description = "Literal description of what you see" },
                new() { Key = "position_assessment", Type = "string", Required = true, Description = "Who is better and why" },
                new() { Key = "threats", Type = "string?", Required = false, Description = "Key threat or opportunity" },
                new() { Key = "suggested_action", Type = "string?", Required = false, Description = "Recommended action" },
                new() { Key = "fen", Type = "string?", Required = false, Description = "FEN string or null" },
                new() { Key = "last_move", Type = "string?", Required = false, Description = "The move just played" },
                new() { Key = "confidence", Type = "enum", Required = true, Description = "Confidence level",
                         Values = new List<string> { "CERTAIN", "LIKELY", "UNCERTAIN", "GUESSING" } },
            }
        };

        var result = schema.BuildResponseFormat();
        var jsonSchema = result.GetProperty("json_schema");

        Assert.Equal("chess_analysis", jsonSchema.GetProperty("name").GetString());
        Assert.True(jsonSchema.GetProperty("strict").GetBoolean());

        var props = jsonSchema.GetProperty("schema").GetProperty("properties");
        Assert.Equal("string", props.GetProperty("visual_description").GetProperty("type").GetString());

        // Nullable field: type is ["string", "null"]
        var threatsType = props.GetProperty("threats").GetProperty("type");
        Assert.Equal(JsonValueKind.Array, threatsType.ValueKind);
        Assert.Equal("string", threatsType[0].GetString());
        Assert.Equal("null", threatsType[1].GetString());

        // Enum field
        var confEnum = props.GetProperty("confidence").GetProperty("enum");
        Assert.Equal(4, confEnum.GetArrayLength());
        Assert.Equal("CERTAIN", confEnum[0].GetString());

        // Required fields
        var required = jsonSchema.GetProperty("schema").GetProperty("required");
        var reqList = new List<string>();
        foreach (var r in required.EnumerateArray()) reqList.Add(r.GetString()!);
        Assert.Contains("visual_description", reqList);
        Assert.Contains("position_assessment", reqList);
        Assert.Contains("confidence", reqList);
        Assert.DoesNotContain("threats", reqList);
        Assert.DoesNotContain("fen", reqList);
    }

    [Fact]
    public void BuildResponseFormat_FpsFields_HandlesObjectAndArrayAsString()
    {
        var schema = new ObservationSchema
        {
            SchemaName = "fps_analysis",
            Fields = new List<ObservationField>
            {
                new() { Key = "compass", Type = "object", Required = false, Description = "Bearing data" },
                new() { Key = "kill_feed", Type = "array", Required = false, Description = "Kill events" },
                new() { Key = "assessment", Type = "string", Required = true, Description = "Summary" },
            }
        };

        var result = schema.BuildResponseFormat();
        var props = result.GetProperty("json_schema").GetProperty("schema").GetProperty("properties");

        // Object and array types become nullable strings (strict schema safe)
        var compassType = props.GetProperty("compass").GetProperty("type");
        Assert.Equal(JsonValueKind.Array, compassType.ValueKind);
        Assert.Equal("string", compassType[0].GetString());
        Assert.Equal("null", compassType[1].GetString());
    }
}

public class GameSkillPackServiceTests
{
    private readonly string _testPacksDir;

    public GameSkillPackServiceTests()
    {
        _testPacksDir = Path.Combine(Path.GetTempPath(), $"gaimer-test-packs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_testPacksDir, "test-pack"));

        File.WriteAllText(Path.Combine(_testPacksDir, "test-pack", "pack.json"), """
        {
          "id": "test-pack",
          "name": "Test Pack",
          "version": "1.0.0",
          "genre": "test",
          "observationSchema": {
            "schemaName": "test_schema",
            "fields": [
              {"key":"assessment","type":"string","required":true,"route":"visual","description":"Test field"},
              {"key":"confidence","type":"enum","values":["CERTAIN","LIKELY"],"required":true,"route":"metadata","description":"Confidence"}
            ]
          },
          "eventMapping": [{"field":"assessment","eventType":"Assessment"}],
          "topStripField": "assessment",
          "brainInstructions": "brain-instructions.md"
        }
        """);
        File.WriteAllText(Path.Combine(_testPacksDir, "test-pack", "brain-instructions.md"),
            "# Test Instructions\nAnalyze this.");
    }

    [Fact]
    public void LoadPack_ValidPack_ReturnsPackWithContent()
    {
        var service = new GameSkillPackService(_testPacksDir);
        var pack = service.LoadPack("test-pack");

        Assert.NotNull(pack);
        Assert.Equal("test-pack", pack.Id);
        Assert.Equal("test", pack.Genre);
        Assert.Contains("Test Instructions", pack.BrainInstructionsContent);
        Assert.Equal(2, pack.ObservationSchema.Fields.Count);
    }

    [Fact]
    public void LoadPack_MissingPack_ReturnsNull()
    {
        var service = new GameSkillPackService(_testPacksDir);
        var pack = service.LoadPack("nonexistent");
        Assert.Null(pack);
    }

    [Fact]
    public void SetActivePack_SetsAndGetsActivePack()
    {
        var service = new GameSkillPackService(_testPacksDir);
        Assert.Null(service.ActivePack);

        service.SetActivePack("test-pack");
        Assert.NotNull(service.ActivePack);
        Assert.Equal("test-pack", service.ActivePack.Id);
    }

    [Fact]
    public void SetActivePack_Null_ClearsActivePack()
    {
        var service = new GameSkillPackService(_testPacksDir);
        service.SetActivePack("test-pack");
        Assert.NotNull(service.ActivePack);

        service.SetActivePack(null);
        Assert.Null(service.ActivePack);
    }

    [Fact]
    public void GetAvailablePackIds_ReturnsDirectoryNames()
    {
        var service = new GameSkillPackService(_testPacksDir);
        var ids = service.GetAvailablePackIds();
        Assert.Contains("test-pack", ids);
    }

    [Fact]
    public void LoadPack_MissingBrainInstructions_ReturnsNull()
    {
        var badDir = Path.Combine(_testPacksDir, "bad-pack");
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "pack.json"), """
        {
          "id":"bad","name":"Bad","version":"1.0.0","genre":"test",
          "observationSchema":{"schemaName":"x","fields":[]},
          "eventMapping":[],"topStripField":"","brainInstructions":"missing.md"
        }
        """);

        var service = new GameSkillPackService(_testPacksDir);
        var pack = service.LoadPack("bad-pack");
        Assert.Null(pack);
    }

    [Fact]
    public void LoadPack_InvalidEventMapping_ReturnsNull()
    {
        var badDir = Path.Combine(_testPacksDir, "bad-mapping");
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "brain-instructions.md"), "ok");
        File.WriteAllText(Path.Combine(badDir, "pack.json"), """
        {
          "id":"bad","name":"Bad","version":"1.0.0","genre":"test",
          "observationSchema":{"schemaName":"x","fields":[
            {"key":"real_field","type":"string","required":true,"route":"visual","description":"exists"}
          ]},
          "eventMapping":[{"field":"nonexistent_field","eventType":"Assessment"}],
          "topStripField":"real_field","brainInstructions":"brain-instructions.md"
        }
        """);

        var service = new GameSkillPackService(_testPacksDir);
        var pack = service.LoadPack("bad-mapping");
        Assert.Null(pack);
    }

    [Fact]
    public void LoadPack_ChessOnline_LoadsFromProjectGamePacks()
    {
        var projectDir = FindProjectGamePacksDir();
        if (projectDir == null) return; // Skip if not in project context

        var service = new GameSkillPackService(projectDir);
        var pack = service.LoadPack("chess-online");

        Assert.NotNull(pack);
        Assert.Equal("chess-online", pack.Id);
        Assert.Equal("chess_analysis", pack.ObservationSchema.SchemaName);
        Assert.Equal(7, pack.ObservationSchema.Fields.Count);
        Assert.Equal(3, pack.EventMapping.Count);
        Assert.Contains("[CAPABILITIES]", pack.BrainInstructionsContent);
        Assert.Contains("[OUTPUT FORMAT]", pack.BrainInstructionsContent);
        Assert.Contains("[READING ACCURACY]", pack.BrainInstructionsContent);
    }

    [Fact]
    public void LoadPack_CodHcCyberAttack_LoadsFromProjectGamePacks()
    {
        var projectDir = FindProjectGamePacksDir();
        if (projectDir == null) return;

        var service = new GameSkillPackService(projectDir);
        var pack = service.LoadPack("cod-hc-cyber-attack");

        Assert.NotNull(pack);
        Assert.Equal("cod-hc-cyber-attack", pack.Id);
        Assert.Equal("fps", pack.Genre);
        Assert.Equal("fps_analysis", pack.ObservationSchema.SchemaName);
        Assert.Equal("assessment", pack.TopStripField);
        Assert.True(pack.ObservationSchema.Fields.Count >= 8, "FPS pack should have at least 8 fields");
        Assert.NotEmpty(pack.BrainInstructionsContent);
    }

    private static string? FindProjectGamePacksDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "GamePacks");
            if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "chess-online")))
                return candidate;
            dir = Path.GetDirectoryName(dir)!;
        }
        return null;
    }
}
