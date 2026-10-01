using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class CodingLocalizedDetectionPlanTests : IDisposable
{
    private readonly AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? _previous = VsaCodeResolver.CurrentCatalog;
    public CodingLocalizedDetectionPlanTests() => VsaResolverTestCatalog.ConfigureDefault();
    public void Dispose() => VsaCodeResolver.ConfigureCatalog(_previous);
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static YoloDetectionDto Box(string label = "BAB_riss") => new(10, 10, 30, 30, label, 0.9);
    private static YoloResponse Response(params YoloDetectionDto[] boxes) => new(true, boxes, "defect", 1,
        DetectorQualified: true, DetectorArtifactSha256: Hash);

    [Fact]
    public void Gleiche_Stelle_wird_einmal_segmentiert_mit_zwei_getrennten_Modellwerten()
    {
        var plan = CodingLocalizedDetectionPlan.Build([new(10, 10, 30, 30, "crack", 0.6, "crack")], Response(Box()), Hash, 100, 100);
        var source = Assert.Single(plan.Detections);
        Assert.Equal(CodingDetectionSource.YoloAndDino, source.Source);
        Assert.Equal(0.9, source.YoloConfidence);
        Assert.Equal(0.6, source.DinoConfidence);
        Assert.Equal("BAB", CodingLocalizedDetectionPlan.ResolveEventCode(source));
        Assert.Equal(0, plan.RejectedBoxes);
    }

    [Fact]
    public void Andere_Gruppe_an_gleicher_Stelle_bleibt_eigener_Befund_ohne_erfundenen_Konsens()
    {
        var plan = CodingLocalizedDetectionPlan.Build([new(10, 10, 30, 30, "root intrusion", 0.6, "root")], Response(Box()), Hash, 100, 100);
        Assert.Equal(2, plan.Detections.Count);
        Assert.DoesNotContain(plan.Detections, d => d.Source == CodingDetectionSource.YoloAndDino);
    }

    [Theory]
    [InlineData("SONST_schaden")]
    [InlineData("BBD_boden")]
    [InlineData("BABBA_fake")]
    [InlineData("BAB_scherbe")]
    public void Nicht_belegte_Klasse_wird_nicht_als_Code_erfunden(string label)
    {
        var plan = CodingLocalizedDetectionPlan.Build([], Response(Box(label)), Hash, 100, 100);
        Assert.Empty(plan.Detections);
        Assert.Equal(1, plan.RejectedYoloBoxes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    [InlineData("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public void Fehlender_oder_abweichender_Modellnachweis_laesst_Dino_weiterlaufen(string? hash)
    {
        var plan = CodingLocalizedDetectionPlan.Build([new(10, 10, 30, 30, "crack", 0.6, "crack")], Response(Box()), hash, 100, 100);
        Assert.Equal(CodingDetectionSource.Dino, Assert.Single(plan.Detections).Source);
        Assert.Equal(1, plan.RejectedYoloBoxes);
    }

    [Fact]
    public void Ungueltige_Koordinaten_und_Modellwerte_werden_vor_Sam_ausgeschlossen()
    {
        var invalid = new[] { Box() with { X1 = double.NaN }, Box() with { X1 = -1 },
            Box() with { X2 = 101 }, Box() with { Y2 = 10 }, Box() with { Confidence = double.PositiveInfinity } };
        var plan = CodingLocalizedDetectionPlan.Build([], Response(invalid), Hash, 100, 100);
        Assert.Empty(plan.Detections);
        Assert.Equal(invalid.Length, plan.RejectedYoloBoxes);
    }

    [Fact]
    public void Unbekannte_Quelle_oder_fremder_Code_kann_keine_Maske_uebernehmen()
    {
        var source = Assert.Single(CodingLocalizedDetectionPlan.Build([], Response(Box()), Hash, 100, 100).Detections);
        var mask = new SamMaskResult("BAB_riss", 0.9, [10, 10, 30, 30], "", 400, 10000, 20, 20, 20, 20);
        Assert.Null(CodingLocalizedDetectionPlan.Match(mask, [source with { Source = (CodingDetectionSource)99 }], 100, 100));
        Assert.Null(CodingLocalizedDetectionPlan.Match(mask, [source with { VsaMainCode = "BAF" }], 100, 100));
    }

    [Fact]
    public void Auch_ungueltige_Dino_Box_wird_vor_Sam_gemeldet()
    {
        var plan = CodingLocalizedDetectionPlan.Build([new(double.NaN, 10, 30, 30, "crack", 0.6, "crack")], null, null, 100, 100);
        Assert.Empty(plan.Detections);
        Assert.Equal(1, plan.RejectedDinoBoxes);
    }

    [Fact]
    public void Verschachtelte_Boxen_uebernehmen_nur_die_Confidence_ihrer_eigenen_Sam_Eingabe()
    {
        var sources = CodingLocalizedDetectionPlan.Build([
            new(-20, -20, 90, 90, "crack", 0.9, "crack"),
            new(10, 10, 30, 30, "crack", 0.4, "crack")], null, null, 100, 100).Detections;
        var mask = new SamMaskResult("crack", 0.8, [10, 10, 30, 30], "", 400, 10000, 20, 20, 20, 20);
        Assert.Equal(0.4, CodingLocalizedDetectionPlan.Match(mask, sources, 100, 100)!.DinoConfidence);
        var clipped = mask with { Bbox = new double[] { 0, 0, 90, 90 } };
        Assert.Equal(0.9, CodingLocalizedDetectionPlan.Match(clipped, sources, 100, 100)!.DinoConfidence);
        Assert.Null(CodingLocalizedDetectionPlan.Match(mask, [sources[1], sources[1]], 100, 100));
    }
}
