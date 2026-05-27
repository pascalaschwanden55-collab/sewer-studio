using System.Text.Json;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VsaKekManifestTruthTests
{
    [Fact]
    public void Manifest_locks_ba_and_bb_code_truth()
    {
        var titles = LoadManifestTitles();

        AssertTitleContains(titles, "BAAA", "deformiert");
        AssertTitleContains(titles, "BAAB", "deformiert");

        AssertTitleContains(titles, "BAFAA", "Rauhe Rohrwandung");
        AssertTitleContains(titles, "BAFAB", "chemischen Angriff");

        AssertTitleContains(titles, "BAHC", "Anschluss");
        AssertTitleContains(titles, "BAIAB", "Dichtring einragend");
        AssertTitleContains(titles, "BAIZ", "Dichtungsmaterial");
        AssertTitleContains(titles, "BAJA", "Rohrverbindung");
        AssertTitleContains(titles, "BAJB", "versetzt");

        AssertTitleContains(titles, "BBAA", "Pfahlwurzel");
        AssertTitleContains(titles, "BBAB", "Wurzeleinwuchs");
        AssertTitleContains(titles, "BBAC", "Wurzelwerk");

        AssertTitleContains(titles, "BBBA", "Inkrustation");
        AssertTitleContains(titles, "BBBB", "Fett");
        AssertTitleContains(titles, "BBBC", "Faeulnis", "Fäulnis");

        AssertTitleContains(titles, "BBCA", "Sand");
        AssertTitleContains(titles, "BBCB", "Kies");
        AssertTitleContains(titles, "BBCC", "Harte Ablagerungen");

        Assert.False(titles.ContainsKey("BBD"));
        AssertTitleContains(titles, "BBDA", "Sand dringt ein");
        AssertTitleContains(titles, "BBDZ", "Bodenmaterial dringt ein");
    }

    private static Dictionary<string, string> LoadManifestTitles()
    {
        var path = FindManifestPath();
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);

        return doc.RootElement
            .GetProperty("codes")
            .EnumerateArray()
            .Where(e => e.TryGetProperty("code", out _))
            .ToDictionary(
                e => e.GetProperty("code").GetString()!,
                e => e.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                StringComparer.OrdinalIgnoreCase);
    }

    private static string FindManifestPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AuswertungPro.Next.UI",
                "Data",
                "vsa_kek_2020_catalog_manifest.json");

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException("VSA-KEK-Katalogmanifest wurde nicht gefunden.");
    }

    private static void AssertTitleContains(
        IReadOnlyDictionary<string, string> titles,
        string code,
        params string[] expectedTextVariants)
    {
        Assert.True(titles.TryGetValue(code, out var title), $"Code {code} fehlt im Manifest.");
        Assert.Contains(expectedTextVariants, expected =>
            title.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }
}
