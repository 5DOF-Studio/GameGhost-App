using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public interface IGameSkillPackService
{
    GameSkillPack? ActivePack { get; }
    GameSkillPack? LoadPack(string packId);
    void SetActivePack(string? packId);
    IReadOnlyList<string> GetAvailablePackIds();
}
