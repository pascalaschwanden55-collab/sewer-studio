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
    public void Apply_SchreibtWerteHerkunftUndVorschlag()
    {
        var entry = new ProtocolEntry { Code = "BCA" };
        QuantificationCodeMetaWriter.Apply(entry, "BCA", Quant(CalibrationSource.Manual));

        var p = entry.CodeMeta!.Parameters;
        Assert.Equal("3:00", p["vsa.uhr.von"]);
        Assert.Equal("45", p["vsa.hoehe.mm"]);
        Assert.Equal("12", p["vsa.breite.mm"]);
        Assert.Equal("30", p["vsa.ausdehnung.prozent"]);
        Assert.Equal("20", p["vsa.querschnitt.prozent"]);
        Assert.Equal("manuell", p["vsa.kalibrierung.quelle"]);
        Assert.Equal("Vorschlag", p["vsa.quant.quelle"]);
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
