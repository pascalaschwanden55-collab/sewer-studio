using System.Reflection;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VsaYoloClassMapTests
{
    [Fact]
    public void Default_map_contains_canonical_baa_baf_baj_families()
    {
        var defaults = GetDefaultMap();

        Assert.Equal(15, defaults["BAA"]);
        Assert.Equal(6, defaults["BAF"]);
        Assert.Equal(9, defaults["BAJ"]);
        Assert.Contains("BAH", defaults.Keys);
        Assert.Contains("BAI", defaults.Keys);
    }

    private static IReadOnlyDictionary<string, int> GetDefaultMap()
    {
        var field = typeof(VsaYoloClassMap).GetField(
            "_defaults",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(field!.GetValue(null));
    }
}
