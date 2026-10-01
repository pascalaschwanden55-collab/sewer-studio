using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class YoloClassVsaMapperTests
{
    // Klassen, die bewusst KEINE Zuordnung haben (laufen auf der Default-Schwelle)
    private static readonly HashSet<string> BewusstOhneMapping =
        new(StringComparer.OrdinalIgnoreCase) { "structural_other" };

    [Theory]
    [InlineData("crack", "BAB")]
    [InlineData("fracture", "BAC")]
    [InlineData("deformation", "BAA")]
    [InlineData("displacement", "BAJ")]
    [InlineData("intrusion", "BAI")]
    [InlineData("root", "BBA")]
    [InlineData("roots", "BBA")]
    [InlineData("deposit", "BBC")]
    [InlineData("infiltration", "BBF")]
    [InlineData("connection", "BCA")]
    public void ToVsaMainCode_MapptEnglischeKlassennamen(string className, string expected)
    {
        Assert.Equal(expected, YoloClassVsaMapper.ToVsaMainCode(className));
    }

    [Theory]
    [InlineData("BAB_riss", "BAB")]
    [InlineData("BAH_schadanschluss", "BAH")]
    [InlineData("BBF_infiltration", "BBF")]
    [InlineData("BBD_boden", "BBD")]
    [InlineData("BCC_bogen", "BCC")]
    public void ToVsaMainCode_Mappt_class_map_v2_fuer_Schwellenwerte(
        string className,
        string expected)
    {
        Assert.Equal(expected, YoloClassVsaMapper.ToVsaMainCode(className));
    }

    [Fact]
    public void ToPersistableVsaCode_speichert_niemals_nacktes_BBD()
    {
        Assert.Equal("BBDZ", YoloClassVsaMapper.ToPersistableVsaCode("BBD_boden"));
        Assert.Equal("BBDZ", YoloClassVsaMapper.ToPersistableVsaCode("BBD"));
        Assert.NotEqual("BBD", YoloClassVsaMapper.ToPersistableVsaCode("BBD_boden"));
    }

    [Theory]
    [InlineData("BAB_riss", "BAB")]
    [InlineData("BBF_infiltration", "BBF")]
    [InlineData("BCC_bogen", "BCC")]
    [InlineData("SONST_schaden", null)]
    public void ToPersistableVsaCode_loest_v2_Klassen_sicher_auf(
        string className,
        string? expected)
    {
        Assert.Equal(expected, YoloClassVsaMapper.ToPersistableVsaCode(className));
    }

    [Theory]
    [InlineData("BAB_crack", "BAB")]
    [InlineData("bab_crack", "BAB")]
    [InlineData("BCA_connection", "BCA")]
    public void ToVsaMainCode_UnterstuetztLegacyVsaPraefix(string className, string expected)
    {
        Assert.Equal(expected, YoloClassVsaMapper.ToVsaMainCode(className));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("structural_other")]
    [InlineData("unbekannte_klasse")]
    public void ToVsaMainCode_LiefertNullOhneZuordnung(string? className)
    {
        Assert.Null(YoloClassVsaMapper.ToVsaMainCode(className));
    }

    [Fact]
    public void AlleProduktivenYoloKlassenSindGemapptOderBewusstAusgenommen()
    {
        // Schutz gegen stilles Zurueckfallen auf die Default-Schwelle:
        // Jede Klasse der produktiven Gewichte muss entweder einen VSA-Hauptcode
        // liefern oder explizit in der Ausnahmen-Liste stehen.
        var namesPath = FindFixtureNamesJsonPath();
        var classNames = LeseKlassennamen(namesPath);

        Assert.NotEmpty(classNames);

        foreach (var className in classNames)
        {
            var code = YoloClassVsaMapper.ToVsaMainCode(className);
            if (BewusstOhneMapping.Contains(className))
            {
                Assert.Null(code);
            }
            else
            {
                Assert.False(string.IsNullOrEmpty(code),
                    $"YOLO-Klasse '{className}' aus {Path.GetFileName(namesPath)} hat keine VSA-Zuordnung — " +
                    "Mapping in YoloClassVsaMapper ergaenzen oder Klasse bewusst ausnehmen.");
                Assert.Matches("^B[A-Z]{2}$", code);
            }
        }
    }

    /// <summary>
    /// Die produktive Klassenkarte liegt neben den Modellgewichten und ist bewusst nicht
    /// eingecheckt (<c>sidecar/models/.gitignore</c>). Auf einem frischen Rechner fehlt
    /// sie deshalb, und der Test brach dort ab. Steht sie zur Verfuegung, muss sie mit der
    /// eingecheckten Liste uebereinstimmen — sonst waere die Fixture veraltet und der
    /// Test nur noch scheinbar gruen.
    /// </summary>
    [Fact]
    public void DieEingechecktenKlassenStimmenMitDenProduktivenUeberein()
    {
        var produktiv = SucheProduktiveNamesJson();
        if (produktiv is null)
            return; // Ohne Modellgewichte gibt es nichts zu vergleichen.

        Assert.Equal(
            LeseKlassennamen(FindFixtureNamesJsonPath()),
            LeseKlassennamen(produktiv));
    }

    private static List<string> LeseKlassennamen(string pfad)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(pfad));
        return doc.RootElement
            .GetProperty("names")
            .EnumerateObject()
            .Select(p => p.Value.GetString()!)
            .ToList();
    }

    private static string FindFixtureNamesJsonPath()
        => Suche(Path.Combine("tests", "Fixtures", "Yolo", "yolo26m.names.json"))
           ?? throw new FileNotFoundException(
               "tests/Fixtures/Yolo/yolo26m.names.json wurde nicht gefunden.");

    private static string? SucheProduktiveNamesJson()
        => Suche(Path.Combine("sidecar", "models", "yolo26m", "yolo26m.names.json"));

    private static string? Suche(string relativerPfad)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativerPfad);
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        return null;
    }
}
