using GaimerDesktop.Models.Timeline;

namespace GaimerDesktop.Tests.Timeline;

public class EventIconMapTests
{
    private static IEnumerable<EventOutputType> GenericTypes =>
        Enum.GetValues<EventOutputType>().Where(t => t < EventOutputType.AgentSpecific);

    [Fact]
    public void GetIcon_AllEventOutputTypes_ReturnNonNull()
    {
        foreach (var type in GenericTypes)
        {
            var icon = EventIconMap.GetIcon(type);

            icon.Should().NotBeNullOrEmpty($"icon for {type} should not be null/empty");
        }
    }

    [Fact]
    public void GetCapsuleColorHex_AllTypes_ReturnValidHex()
    {
        foreach (var type in GenericTypes)
        {
            var hex = EventIconMap.GetCapsuleColorHex(type);

            hex.Should().StartWith("#", $"capsule color for {type} should start with #");
            hex.Should().HaveLength(9, $"capsule color for {type} should be ARGB (#AARRGGBB)");
        }
    }

    [Fact]
    public void GetCapsuleStrokeHex_AllTypes_ReturnValidHex()
    {
        foreach (var type in GenericTypes)
        {
            var hex = EventIconMap.GetCapsuleStrokeHex(type);

            hex.Should().StartWith("#", $"capsule stroke for {type} should start with #");
            hex.Should().HaveLength(9, $"capsule stroke for {type} should be ARGB (#AARRGGBB)");
        }
    }
}
