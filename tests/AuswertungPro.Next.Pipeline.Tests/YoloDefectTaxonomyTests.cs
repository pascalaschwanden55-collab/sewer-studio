using AuswertungPro.Next.UI.Ai.Training.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer YoloDefectTaxonomy — Mapping zwischen VSA-Codes und YOLO-Klassen.
/// Deckt ab: FromVsaCode, AllClasses, GenerateDataYaml.
/// </summary>
public sealed class YoloDefectTaxonomyTests
{
    // ═══════════════════════════════════════════════════════════════
    // FromVsaCode — Jeder VSA-Praefix auf korrekte Klasse
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("BAB", 0, "crack")]
    [InlineData("BAC", 1, "fracture")]
    [InlineData("BAA", 2, "deformation")]
    [InlineData("BAF", 2, "deformation")]
    [InlineData("BAH", 3, "displacement")]
    [InlineData("BAI", 4, "intrusion")]
    [InlineData("BBD", 4, "intrusion")]
    [InlineData("BBB", 5, "root")]
    [InlineData("BBC", 6, "deposit")]
    [InlineData("BBA", 6, "deposit")]
    [InlineData("BBF", 7, "infiltration")]
    [InlineData("BBG", 7, "infiltration")]
    [InlineData("BCA", 8, "connection")]
    [InlineData("BAD", 9, "structural_other")]
    [InlineData("BAE", 9, "structural_other")]
    [InlineData("BAG", 9, "structural_other")]
    [InlineData("BAJ", 9, "structural_other")]
    [InlineData("BAK", 9, "structural_other")]
    [InlineData("BBE", 9, "structural_other")]
    [InlineData("BBH", 9, "structural_other")]
    [InlineData("BCB", 9, "structural_other")]
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
    [InlineData("BAB.B.A", 0, "crack")]       // Querriss laengs → crack
    [InlineData("BAB.A", 0, "crack")]          // Laengsriss → crack
    [InlineData("BAC.A.B", 1, "fracture")]     // Bruch partiell → fracture
    [InlineData("BAH.B.C", 3, "displacement")] // Versatz → displacement
    [InlineData("BCA.E.B", 8, "connection")]   // Seitl. Anschluss → connection
    [InlineData("BBB.A", 5, "root")]           // Wurzeln → root
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
