using System.Text.Json;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VsaKekManifestTruthTests
{
    [Fact]
    public void Manifest_locks_ba_and_bb_code_truth()
    {
        var titles = LoadManifestTitles();

        AssertTitleContains(titles, "BAA", "Verformung");
        AssertTitleContains(titles, "BAAA", "deformiert");
        AssertTitleContains(titles, "BAAB", "deformiert");

        AssertTitleContains(titles, "BABAA", "Riss");
        AssertTitleContains(titles, "BABBA", "Riss");

        AssertTitleContains(titles, "BACA", "Scherbe");
        AssertTitleContains(titles, "BACB", "Wandungsteil", "Loch");
        AssertTitleContains(titles, "BACC", "Leitungsbruch", "Einsturz");

        AssertTitleContains(titles, "BAD", "Mauerwerk");
        AssertTitleContains(titles, "BADA", "Mauerwerk");
        AssertTitleContains(titles, "BADB", "Mauerwerk");
        AssertTitleContains(titles, "BAE", "Mörtel", "Moertel");

        AssertTitleContains(titles, "BAFAA", "Rauhe Rohrwandung");
        AssertTitleContains(titles, "BAFAB", "chemischen Angriff");

        AssertTitleContains(titles, "BAHC", "Anschluss");
        AssertTitleContains(titles, "BAIAB", "Dichtring einragend");
        AssertTitleContains(titles, "BAIZ", "Dichtungsmaterial");
        AssertTitleContains(titles, "BAJA", "Rohrverbindung");
        AssertTitleContains(titles, "BAJB", "versetzt");

        AssertTitleContains(titles, "BAK", "Innenauskleidung");
        AssertTitleContains(titles, "BAKA", "Innenauskleidung");
        AssertTitleContains(titles, "BAL", "Reparatur");
        AssertTitleContains(titles, "BALA", "Reparatur");
        AssertTitleContains(titles, "BAM", "Schweissnaht", "Schweißnaht");
        AssertTitleContains(titles, "BAMA", "Schweissnaht", "Schweißnaht");
        AssertTitleContains(titles, "BAN", "porös", "poroes");
        AssertTitleContains(titles, "BAO", "Boden sichtbar");
        AssertTitleContains(titles, "BAP", "Hohlraum");

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

    // Robustheits-Riegel: JEDER Code, den die KI-Befundliste als nackten Code
    // anzeigen kann, MUSS im Katalog einen deutschen Klartext haben (Titel != Code).
    // Quelle des Code-Raums:
    //  - Klassifikator-Klassen (vsa_cls_v5_nocrop): BAB BAF BAI BAJ BBA BBB BCD BCE BDA BDD
    //  - VsaCodeResolver.InferCodeFromLabel: BCA BCC BAC BAA BBC BDDC (+ obige)
    //  - CodingImportFallbackCodePolicy.AllowedPrefixes: BCD BCE BCA BCC BBC BDDC BAA BAB BAC BAF BAH BAI BAJ BBA BBB
    // Faellt einer dieser Codes auf "Titel == Code" zurueck, zeigt die UI den rohen
    // (oft englischen) Modelltext statt deutschem Klartext -> dieser Test verhindert das.
    [Theory]
    [InlineData("BAA")]
    [InlineData("BAB")]
    [InlineData("BAC")]
    [InlineData("BAF")]
    [InlineData("BAH")]
    [InlineData("BAI")]
    [InlineData("BAJ")]
    [InlineData("BBA")]
    [InlineData("BBB")]
    [InlineData("BBC")]
    [InlineData("BCA")]
    [InlineData("BCC")]
    [InlineData("BCD")]
    [InlineData("BCE")]
    [InlineData("BDA")]
    [InlineData("BDD")]
    [InlineData("BDDC")]
    public void Manifest_resolves_ki_and_import_code_space_to_german(string code)
    {
        var titles = LoadManifestTitles();

        Assert.True(titles.TryGetValue(code, out var title),
            $"Code {code} fehlt im Katalog – KI-Liste wuerde rohen Modelltext zeigen.");
        Assert.False(string.IsNullOrWhiteSpace(title), $"Code {code} hat keinen Titel.");
        Assert.False(string.Equals(title, code, StringComparison.OrdinalIgnoreCase),
            $"Code {code} hat den Code als Titel ('{title}') statt deutschen Klartext.");
    }

    [Theory]
    [InlineData("BAB", true, false, true)]
    [InlineData("BAC", true, false, true)]
    [InlineData("BAG", true, false, true)]
    [InlineData("BAGA", true, false, true)]
    [InlineData("BAI", true, false, true)]
    [InlineData("BAJ", true, false, true)]
    [InlineData("BBA", true, false, true)]
    [InlineData("BBB", true, false, true)]
    [InlineData("BBC", true, false, true)]
    [InlineData("BCA", true, true, true)]
    [InlineData("BDD", true, false, false)]
    [InlineData("DCA", true, true, true)]
    [InlineData("DCG", true, true, true)]
    [InlineData("BAA", false, false, false)]
    [InlineData("BAF", false, false, true)]
    [InlineData("BAH", false, false, true)]
    public void Manifest_locks_application_rules_for_quantification_and_clock(
        string code,
        bool requiresQ1,
        bool hasQ2,
        bool hasClock)
    {
        var parameters = LoadManifestParameters();

        Assert.True(parameters.TryGetValue(code, out var codeParameters), $"Code {code} fehlt im Manifest.");
        Assert.Equal(requiresQ1, HasRequired(codeParameters, "Q1"));
        Assert.Equal(hasQ2, HasParameter(codeParameters, "Q2"));
        Assert.Equal(hasClock,
            HasParameter(codeParameters, "SchadenlageAnfang")
            || HasParameter(codeParameters, "SchadenlageEnde"));
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

    private static Dictionary<string, List<JsonElement>> LoadManifestParameters()
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
                e => e.TryGetProperty("parameters", out var parameters)
                    ? parameters.EnumerateArray().Select(p => p.Clone()).ToList()
                    : new List<JsonElement>(),
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

    private static bool HasParameter(IEnumerable<JsonElement> parameters, string dataKey)
        => parameters.Any(p =>
            p.TryGetProperty("dataKey", out var key)
            && string.Equals(key.GetString(), dataKey, StringComparison.OrdinalIgnoreCase));

    private static bool HasRequired(IEnumerable<JsonElement> parameters, string dataKey)
        => parameters.Any(p =>
            p.TryGetProperty("dataKey", out var key)
            && string.Equals(key.GetString(), dataKey, StringComparison.OrdinalIgnoreCase)
            && p.TryGetProperty("required", out var required)
            && required.ValueKind == JsonValueKind.True);
}
