using FluentAssertions;
using Zadana.Domain.Modules.Social.Support;

namespace Zadana.UnitTests.Modules.Social;

public class NotificationCategorySoundMapTests
{
    [Fact]
    public void BuildEffectiveMap_ShouldUseDefaultForMissingCategories()
    {
        var map = NotificationCategorySoundMap.BuildEffectiveMap(
            new Dictionary<string, string> { ["dispatch"] = "urgent" },
            "chime");

        map["default"].Should().Be("chime");
        map["dispatch"].Should().Be("urgent");
        map["assignment"].Should().Be("chime");
        map["wallet"].Should().Be("chime");
    }

    [Fact]
    public void ResolveForCategory_ShouldReturnCategoryOverride()
    {
        var json = NotificationCategorySoundMap.Serialize(
            new Dictionary<string, string> { ["dispatch"] = "urgent" },
            "chime");

        NotificationCategorySoundMap.ResolveForCategory(json, "chime", "dispatch")
            .Should()
            .Be("urgent");

        NotificationCategorySoundMap.ResolveForCategory(json, "chime", "wallet")
            .Should()
            .Be("chime");
    }

    [Fact]
    public void Normalize_ShouldRejectUnknownSoundKeys()
    {
        var map = NotificationCategorySoundMap.BuildEffectiveMap(
            new Dictionary<string, string> { ["dispatch"] = "invalid-sound" },
            "classic");

        map["dispatch"].Should().Be("classic");
    }
}
