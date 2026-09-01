using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ResolveFromClassifierTests
{
    private static IReadOnlyList<YoloClassifyPrediction> Preds(params (string Code, double Conf)[] preds)
    {
        var list = new List<YoloClassifyPrediction>();
        foreach (var (code, conf) in preds)
            list.Add(new YoloClassifyPrediction(code, conf));
        return list;
    }

    [Fact]
    public void Fehlende_Predictions_liefern_keine_Entscheidung()
    {
        Assert.Null(VsaCodeResolver.ResolveFromClassifier(null, 0, 10));
        Assert.Null(VsaCodeResolver.ResolveFromClassifier([], 0, 10));
    }

    [Theory]
    [InlineData("BCE", 0.90)]
    [InlineData("LEER", 0.60)]
    [InlineData("OTHER", 0.60)]
    [InlineData("NORMAL", 0.60)]
    public void Bogen_Veto_korrigiert_unzuverlaessige_Klassen(string code, double confidence)
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds((code, confidence)), currentMeter: 5, totalLength: 20, isBend: true);

        Assert.NotNull(r);
        Assert.Equal("BCC", r!.Code);
        Assert.True(r.Confidence >= 0.75);
        Assert.Contains("Bogen-Geometrie", r.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bogen_Veto_nutzt_auch_schwache_Bce_Zweitklasse()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("BAB", 0.40), ("BCE", 0.21)), 5, 20, isBend: true);

        Assert.Equal(new VsaCodeResolver.ResolvedCode("BCC", 0.75, "Bogen-Geometrie"), r);
    }

    [Fact]
    public void Bogen_Veto_ueberschreibt_keinen_klaren_Schadencode()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("BAB", 0.41), ("BCE", 0.90)), 5, 20, isBend: true);

        Assert.Equal("BAB", r?.Code);
        Assert.Equal(0.41, r?.Confidence);
        Assert.StartsWith("YOLO BAB", r?.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void BcdAmRohranfangWirdBestaetigt()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BCD", 0.9)), currentMeter: 0.2, totalLength: 50);
        Assert.NotNull(r);
        Assert.Equal("BCD", r!.Code);
        Assert.Equal(0.9, r.Confidence);
        Assert.StartsWith("Meter", r.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bcd_Zweitklasse_wird_nur_unmittelbar_am_Anfang_hochgestuft()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("OTHER", 0.10), ("BCD", 0.21)), currentMeter: 0.49, totalLength: 50);

        Assert.Equal("BCD", r?.Code);
        Assert.Equal(0.80, r?.Confidence);
        Assert.Contains("YOLO BCD 21", r?.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bcd_bei_genau_halbem_Meter_bekommt_keinen_Meter_Bonus()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("BCD", 0.90)), currentMeter: 0.50, totalLength: 50);

        Assert.Equal("BCD", r?.Code);
        Assert.Equal(0.90, r?.Confidence);
        Assert.StartsWith("YOLO BCD", r?.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void BcdMittenImRohrWirdVerworfen()
    {
        // Negativ-Gate: Rohranfang gibt es nicht bei 8m
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BCD", 0.9)), currentMeter: 8.0, totalLength: 50);
        Assert.Null(r);
    }

    [Fact]
    public void BceMittenImRohrWirdVerworfen()
    {
        // Pilot 2026-06-10: offener Anschluss bei 1.7m von 12.5m wurde als BCE codiert
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BCE", 0.95)), currentMeter: 1.7, totalLength: 12.5);
        Assert.Null(r);
    }

    [Fact]
    public void BceAmRohrendeWirdBestaetigt()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BCE", 0.95)), currentMeter: 12.2, totalLength: 12.5);
        Assert.NotNull(r);
        Assert.Equal("BCE", r!.Code);
        Assert.Equal(0.95, r.Confidence);
        Assert.StartsWith("Meter", r.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bce_Zweitklasse_wird_hinter_neunzig_Prozent_hochgestuft()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("OTHER", 0.10), ("BCE", 0.21)), currentMeter: 9.01, totalLength: 10);

        Assert.Equal("BCE", r?.Code);
        Assert.Equal(0.80, r?.Confidence);
        Assert.StartsWith("Meter", r?.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bce_bei_genau_neunzig_Prozent_bekommt_keinen_Meter_Bonus()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("BCE", 0.95)), currentMeter: 9, totalLength: 10);

        Assert.Equal("BCE", r?.Code);
        Assert.Equal(0.95, r?.Confidence);
        Assert.StartsWith("YOLO BCE", r?.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Sehr_kurze_Gesamtlaenge_loest_keine_Endregel_aus()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("BCE", 0.95)), currentMeter: 0.95, totalLength: 1);

        Assert.Equal("BCE", r?.Code);
        Assert.StartsWith("YOLO BCE", r?.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Importkontext_nimmt_naechsten_Treffer_der_gleichen_Familie()
    {
        var context = new[]
        {
            ("BAB9", "weiter weg", 11.0),
            ("BBA", "falsche Familie", 10.01),
            ("BAB1", "nahe", 10.2)
        };

        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("BAB", 0.60)), currentMeter: 10, totalLength: 50, importContext: context);

        Assert.Equal("BAB1", r?.Code);
        Assert.Equal(0.60, r?.Confidence);
        Assert.Contains("Import BAB1 @ 10.2m", r?.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Importkontext_ist_bei_genau_einhalb_Metern_nicht_mehr_nah()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("BAB", 0.60)),
            currentMeter: 10,
            totalLength: 50,
            importContext: [("BAB1", "Grenze", 11.5)]);

        Assert.Equal("BAB", r?.Code);
        Assert.StartsWith("YOLO BAB", r?.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Importkontext_braucht_mehr_als_dreissig_Prozent_Konfidenz()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("BAB", 0.30)),
            currentMeter: 10,
            totalLength: 50,
            importContext: [("BAB1", "nahe", 10.1)]);

        Assert.Null(r);
    }

    [Fact]
    public void BefundcodeMitHoherKonfidenzWirdUebernommen()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BAB", 0.72)), currentMeter: 5.0, totalLength: 50);
        Assert.NotNull(r);
        Assert.Equal("BAB", r!.Code);
    }

    [Theory]
    [InlineData("BCA")]
    [InlineData("BCC")]
    [InlineData("BBC")]
    [InlineData("BAA")]
    public void Paket5KlassenMitHoherKonfidenzWerdenUebernommen(string code)
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds((code, 0.41)), currentMeter: 5.0, totalLength: 50);
        Assert.NotNull(r);
        Assert.Equal(code, r!.Code);
    }

    [Fact]
    public void NiedrigeKonfidenzLiefertKeineEntscheidung()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BAB", 0.30)), currentMeter: 5.0, totalLength: 50);
        Assert.Null(r);
    }

    [Fact]
    public void Genau_vierzig_Prozent_reichen_nicht_fuer_reine_Klassifikation()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BAB", 0.40)), 5, 50);
        Assert.Null(r);
    }

    [Fact]
    public void Other_nutzt_gueltige_Zweitklasse_als_Fallback()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(
            Preds(("OTHER", 0.90), ("bab", 0.16)), 5, 50);

        Assert.Equal("BAB", r?.Code);
        Assert.Equal(0.16, r?.Confidence);
        Assert.Contains("Fallback bab 16", r?.Source, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(UngueltigeOtherFallbacks))]
    public void Other_ohne_gueltige_Zweitklasse_liefert_nichts(
        IReadOnlyList<YoloClassifyPrediction> predictions)
        => Assert.Null(VsaCodeResolver.ResolveFromClassifier(predictions, 5, 50));

    public static TheoryData<IReadOnlyList<YoloClassifyPrediction>> UngueltigeOtherFallbacks => new()
    {
        Preds(("OTHER", 0.90)),
        Preds(("OTHER", 0.90), ("BAB", 0.15)),
        Preds(("OTHER", 0.90), ("OTHER", 0.90))
    };
}
