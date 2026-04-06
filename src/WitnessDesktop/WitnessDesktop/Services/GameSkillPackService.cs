using System.Text.Json;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Services;

public sealed class GameSkillPackService : IGameSkillPackService
{
    private readonly string _packsDirectory;
    private readonly Dictionary<string, GameSkillPack> _cache = new();

    public GameSkillPack? ActivePack { get; private set; }

    public GameSkillPackService(string packsDirectory)
    {
        _packsDirectory = packsDirectory;
    }

    public GameSkillPack? LoadPack(string packId)
    {
        if (_cache.TryGetValue(packId, out var cached))
            return cached;

        var packDir = Path.Combine(_packsDirectory, packId);
        if (!Directory.Exists(packDir))
            return null;

        var manifestPath = Path.Combine(packDir, "pack.json");
        if (!File.Exists(manifestPath))
            return null;

        GameSkillPack? pack;
        try
        {
            var json = File.ReadAllText(manifestPath);
            pack = JsonSerializer.Deserialize<GameSkillPack>(json);
        }
        catch
        {
            return null;
        }

        if (pack == null)
            return null;

        pack.PackDirectory = packDir;

        // Load brain instructions markdown
        var instructionsPath = Path.Combine(packDir, pack.BrainInstructions);
        if (!File.Exists(instructionsPath))
            return null;

        pack.BrainInstructionsContent = File.ReadAllText(instructionsPath);

        // Validate: all eventMapping fields must exist in observationSchema
        var fieldKeys = new HashSet<string>(pack.ObservationSchema.Fields.Select(f => f.Key));
        foreach (var mapping in pack.EventMapping)
        {
            if (!fieldKeys.Contains(mapping.Field))
                return null;

            if (!Enum.TryParse<EventOutputType>(mapping.EventType, out _))
                return null;
        }

        // Validate: topStripField must be a valid field key (or empty)
        if (!string.IsNullOrEmpty(pack.TopStripField) && !fieldKeys.Contains(pack.TopStripField))
            return null;

        // Validate: schemaName must be non-empty
        if (string.IsNullOrWhiteSpace(pack.ObservationSchema.SchemaName))
            return null;

        _cache[packId] = pack;
        return pack;
    }

    public void SetActivePack(string? packId)
    {
        if (packId == null)
        {
            ActivePack = null;
            return;
        }

        ActivePack = LoadPack(packId);
    }

    public IReadOnlyList<string> GetAvailablePackIds()
    {
        if (!Directory.Exists(_packsDirectory))
            return Array.Empty<string>();

        return Directory.GetDirectories(_packsDirectory)
            .Select(Path.GetFileName)
            .Where(name => name != null && File.Exists(Path.Combine(_packsDirectory, name, "pack.json")))
            .ToList()!;
    }
}
