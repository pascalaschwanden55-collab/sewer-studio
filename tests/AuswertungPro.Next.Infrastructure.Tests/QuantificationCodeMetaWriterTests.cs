using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class QuantificationCodeMetaWriterTests
{
    private static MaskQuantificationService.QuantifiedMask Quant(
        CalibrationSource source,
        int? height = 45, int? width = 12, int? extent = 30, int? crossSection = 20, string? clock = "3:00")
        => new("BCA", 0.9, height, width, extent, crossSection, null, clock, source);

    [Fact]
    public void Apply_BCA_SchreibtNurHoeheBreiteUhrlage_KeineProzente()
    {
        // Teil 10 (codeabhaengig): BCA (Anschluss) traegt laut VSA nur Hoehe + Breite (mm).
        // Ausdehnung-%/Querschnitt-% sind fuer BCA NICHT vorgesehen -> duerfen nicht geschrieben werden,
        // auch wenn SAM solche Werte liefert. Frueher schrieb der Writer generisch alles.
        var entry = new ProtocolEntry { Code = "BCA" };
        QuantificationCodeMetaWriter.Apply(entry, "BCA", Quant(CalibrationSource.Manual));

        var p = entry.CodeMeta!.Parameters;
        Assert.Equal("3:00", p["vsa.uhr.von"]);
        Assert.Equal("45", p["vsa.hoehe.mm"]);
        Assert.Equal("12", p["vsa.breite.mm"]);
        Assert.False(p.ContainsKey("vsa.ausdehnung.prozent"));
        Assert.False(p.ContainsKey("vsa.querschnitt.prozent"));
        Assert.Equal("manuell", p["vsa.kalibrierung.quelle"]);
        Assert.Equal("Vorschlag", p["vsa.quant.quelle"]);
    }

    [Fact]
    public void Apply_Wurzeln_BBA_SchreibtNurQuerschnittProzent()
    {
        // BBA (Wurzeln): nur Querschnittsverminderung %. Hoehe/Breite mm + Ausdehnung% NICHT.
        var entry = new ProtocolEntry { Code = "BBAC" };
        QuantificationCodeMetaWriter.Apply(entry, "BBAC", Quant(CalibrationSource.Manual));

        var p = entry.CodeMeta!.Parameters;
        Assert.Equal("20", p["vsa.querschnitt.prozent"]);
        Assert.False(p.ContainsKey("vsa.hoehe.mm"));
        Assert.False(p.ContainsKey("vsa.breite.mm"));
        Assert.False(p.ContainsKey("vsa.ausdehnung.prozent"));
    }

    [Fact]
    public void Apply_ManifestRuleOhneQ_UnterdruecktAlleMasse()
    {
        // Explizite Manifest-Regel ohne Q (z.B. BBF Infiltration): keine mm/%-Werte, nur Uhrlage.
        var entry = new ProtocolEntry { Code = "BBF" };
        var noQ = new AuswertungPro.Next.Application.Ai.QuantificationGate.ManifestQuantRule(
            HasQ1: false, HasQ2: false, AllowClock: true);
        QuantificationCodeMetaWriter.Apply(entry, "BBF", Quant(CalibrationSource.Manual), noQ);

        var p = entry.CodeMeta!.Parameters;
        Assert.Equal("3:00", p["vsa.uhr.von"]);
        Assert.False(p.ContainsKey("vsa.hoehe.mm"));
        Assert.False(p.ContainsKey("vsa.breite.mm"));
        Assert.False(p.ContainsKey("vsa.querschnitt.prozent"));
    }

    [Fact]
    public void Apply_HerkunftNone_AlsGeschaetzt_Auto_AlsAutomatisch()
    {
        var e1 = new ProtocolEntry { Code = "BCA" };
        QuantificationCodeMetaWriter.Apply(e1, "BCA", Quant(CalibrationSource.None));
        Assert.Equal("geschaetzt", e1.CodeMeta!.Parameters["vsa.kalibrierung.quelle"]);

        var e2 = new ProtocolEntry { Code = "BCA" };
        QuantificationCodeMetaWriter.Apply(e2, "BCA", Quant(CalibrationSource.Auto));
        Assert.Equal("automatisch", e2.CodeMeta!.Parameters["vsa.kalibrierung.quelle"]);
    }

    [Fact]
    public void Apply_OhneMesswerte_SchreibtNichts()
    {
        var entry = new ProtocolEntry { Code = "BCD" };
        QuantificationCodeMetaWriter.Apply(entry, "BCD",
            new MaskQuantificationService.QuantifiedMask("BCD", 0.9, null, null, null, null, null, null, CalibrationSource.None));

        Assert.False(entry.CodeMeta?.Parameters.ContainsKey("vsa.hoehe.mm") ?? false);
        Assert.False(entry.CodeMeta?.Parameters.ContainsKey("vsa.quant.quelle") ?? false);
    }
}
