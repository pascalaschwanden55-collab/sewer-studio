using Xunit;
using AuswertungPro.Next.Application.Ai.Training.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer YoloDefectTaxonomy — Mapping zwischen VSA-Codes und YOLO-Klassen.
/// Deckt ab: FromVsaCode, AllClasses, GenerateDataYaml.
/// </summary>
[Trait("Category", "Unit")]
public sealed class YoloDefectTaxonomyTests
{
    // ═══════════════════════════════════════════════════════════════
    // FromVsaCode — Jeder VSA-Praefix auf korrekte Klasse
    // ═══════════════════════════════════════════════════════════════

    // VSA-Merkblatt 2018 (Vernehmlassung 1.0.8) — verbindliche Zuordnung
    [Theory]
    [InlineData("BAB", 0, "crack")]           // Risse
    [InlineData("BAC", 1, "fracture")]        // Bruch/Einsturz
    [InlineData("BAA", 2, "deformation")]     // Verformung
    [InlineData("BAF", 2, "deformation")]     // Oberflaechenschaden
    [InlineData("BAJ", 3, "displacement")]    // Verschobene Rohrverbindung (= Versatz!)
    [InlineData("BAG", 4, "intrusion")]       // Einragender Anschluss
    [InlineData("BBD", 4, "intrusion")]       // Eindringen von Bodenmaterial
    [InlineData("BBA", 5, "root")]            // Wurzeln (VSA 2018!)
    [InlineData("BBC", 6, "deposit")]         // Ablagerungen
    [InlineData("BBB", 6, "deposit")]         // Anhaftende Stoffe/Inkrustation (VSA 2018!)
    [InlineData("BBF", 7, "infiltration")]    // Infiltration
    [InlineData("BBG", 7, "infiltration")]    // Exfiltration
    [InlineData("BCA", 8, "connection")]      // Seitlicher Anschluss
    [InlineData("BAD", 9, "structural_other")]  // Defektes Mauerwerk
    [InlineData("BAE", 9, "structural_other")]  // Fehlender Moertel
    [InlineData("BAH", 9, "structural_other")]  // Schadhafter Anschluss
    [InlineData("BAI", 9, "structural_other")]  // Einragendes Dichtungsmaterial
    [InlineData("BAK", 9, "structural_other")]  // Feststellung Innenauskleidung
    [InlineData("BAL", 9, "structural_other")]  // Schadhafte Reparatur
    [InlineData("BAM", 9, "structural_other")]  // Schadhafte Schweissnaht
    [InlineData("BAN", 9, "structural_other")]  // Poroese Leitung
    [InlineData("BAO", 9, "structural_other")]  // Boden sichtbar
    [InlineData("BAP", 9, "structural_other")]  // Hohlraum sichtbar
    [InlineData("BBE", 9, "structural_other")]  // Andere Hindernisse
    [InlineData("BBH", 9, "structural_other")]  // Ungeziefer
    [InlineData("BCB", 9, "structural_other")]  // Punktuelle Reparatur
    public void FromVsaCode_KnownCodes_ReturnsCorrectClass(string vsaCode, int expectedId, string expectedName)
    {
        var result = YoloDefectTaxonomy.FromVsaCode(vsaCode);

        Assert.NotNull(result);
        Assert.Equal(expectedId, result!.Value.ClassId);
        Assert.Equal(expectedName, result.Value.ClassName);
    }

    // ═══════════════════════════════════════════════════════════════
    // FromVsaCode — Steuercodes geben null zurueck
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("BCD")]   // Rohranfang
    [InlineData("BCE")]   // Rohrende
    [InlineData("BCC")]   // Bogen
    [InlineData("BDB")]   // Steuercode
    [InlineData("BDC")]   // Steuercode
    [InlineData("BDD")]   // Steuercode
    [InlineData("BDE")]   // Steuercode
    [InlineData("BDF")]   // Steuercode
    public void FromVsaCode_Steuercodes_ReturnsNull(string steuercode)
    {
        var result = YoloDefectTaxonomy.FromVsaCode(steuercode);
        Assert.Null(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // FromVsaCode — Sub-Codes mit Punkten (z.B. "BAB.B.A")
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    // VSA 2018: BBA=Wurzeln, BBB=Anhaftende Stoffe, BAJ=Versatz
    [InlineData("BAB.B.A", 0, "crack")]       // Querriss laengs → crack
    [InlineData("BAB.A", 0, "crack")]          // Laengsriss → crack
    [InlineData("BAC.A.B", 1, "fracture")]     // Bruch partiell → fracture
    [InlineData("BAJ.B.C", 3, "displacement")] // Verschobene Rohrverbindung → displacement (VSA 2018)
    [InlineData("BCA.E.B", 8, "connection")]   // Seitl. Anschluss → connection
    [InlineData("BBA.A", 5, "root")]           // Wurzeln → root (VSA 2018: BBA = Wurzeln!)
    [InlineData("BBC.A.D", 6, "deposit")]      // Ablagerung → deposit
    public void FromVsaCode_SubCodesWithDots_ExtractsPrefixCorrectly(
        string vsaCode, int expectedId, string expectedName)
    {
        var result = YoloDefectTaxonomy.FromVsaCode(vsaCode);

        Assert.NotNull(result);
        Assert.Equal(expectedId, result!.Value.ClassId);
        Assert.Equal(expectedName, result.Value.ClassName);
    }

    // ═══════════════════════════════════════════════════════════════
    // FromVsaCode — Ungueltige Eingaben
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XY")]
    [InlineData("ZZZ")]
    public void FromVsaCode_InvalidInput_ReturnsNull(string? input)
    {
        var result = YoloDefectTaxonomy.FromVsaCode(input);
        Assert.Null(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // AllClasses — Genau 10 Klassen
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void AllClasses_Returns10Entries()
    {
        var classes = YoloDefectTaxonomy.AllClasses;
        Assert.Equal(10, classes.Length);
    }

    [Fact]
    public void AllClasses_IdsAreContiguous0To9()
    {
        var classes = YoloDefectTaxonomy.AllClasses;
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i, classes[i].ClassId);
        }
    }

    [Fact]
    public void AllClasses_ContainsAllExpectedNames()
    {
        var names = YoloDefectTaxonomy.AllClasses.Select(c => c.ClassName).ToArray();
        Assert.Contains("crack", names);
        Assert.Contains("fracture", names);
        Assert.Contains("deformation", names);
        Assert.Contains("displacement", names);
        Assert.Contains("intrusion", names);
        Assert.Contains("root", names);
        Assert.Contains("deposit", names);
        Assert.Contains("infiltration", names);
        Assert.Contains("connection", names);
        Assert.Contains("structural_other", names);
    }

    // ═══════════════════════════════════════════════════════════════
    // GenerateDataYaml — YOLO data.yaml Inhalt
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDataYaml_ContainsNc10()
    {
        var yaml = YoloDefectTaxonomy.GenerateDataYaml(@"C:\datasets\sewer");
        Assert.Contains("nc: 10", yaml);
    }

    [Fact]
    public void GenerateDataYaml_ContainsAllClassNames()
    {
        var yaml = YoloDefectTaxonomy.GenerateDataYaml(@"C:\datasets\sewer");

        foreach (var cls in YoloDefectTaxonomy.AllClasses)
        {
            Assert.Contains(cls.ClassName, yaml);
        }
    }

    [Fact]
    public void GenerateDataYaml_ContainsDatasetPath()
    {
        const string path = @"C:\datasets\sewer";
        var yaml = YoloDefectTaxonomy.GenerateDataYaml(path);
        Assert.Contains(path, yaml);
    }

    [Fact]
    public void GenerateDataYaml_ContainsTrainAndValPaths()
    {
        var yaml = YoloDefectTaxonomy.GenerateDataYaml(@"C:\datasets\sewer");
        Assert.Contains("train:", yaml);
        Assert.Contains("val:", yaml);
    }
}
