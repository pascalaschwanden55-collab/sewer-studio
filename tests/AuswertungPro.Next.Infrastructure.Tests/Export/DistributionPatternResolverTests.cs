using System;
using AuswertungPro.Next.Application.Export;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

/// <summary>
/// Tests fuer die Platzhalter-Engine (Export/Verteil-Konfiguration, Etappe 1a):
/// loest Muster wie {Datum}_{Haltung} auf und baut einen dateisystemsicheren relativen Pfad
/// aus drei Ebenen (Ordner/Unterordner/Datei); leere Ebenen entfallen.
/// </summary>
public sealed class DistributionPatternResolverTests
{
    private static readonly DateTime Datum = new(2026, 6, 26);

    private static DistributionPatternContext HaltungCtx() => new(
        Datum: Datum, Gemeinde: "Altdorf", Haltung: "06.24341-35625");

    private static DistributionPatternContext SchachtCtx() => new(
        Datum: Datum, Gemeinde: "Altdorf", Schachtnummer: "KS 60191");

    private static string Norm(string p) => p.Replace('\\', '/');

    private readonly IDistributionPatternResolver _r = new DistributionPatternResolver();

    [Fact]
    public void ResolveSegment_ersetzt_datum_und_haltung()
        => Assert.Equal("20260626_06.24341-35625",
            _r.ResolveSegment("{Datum}_{Haltung}", HaltungCtx()));

    [Fact]
    public void ResolveSegment_jahr_monat_gemeinde()
    {
        Assert.Equal("2026", _r.ResolveSegment("{Jahr}", HaltungCtx()));
        Assert.Equal("06", _r.ResolveSegment("{Monat}", HaltungCtx()));
        Assert.Equal("Altdorf", _r.ResolveSegment("{Gemeinde}", HaltungCtx()));
    }

    [Fact]
    public void ResolveSegment_schachtnummer()
        => Assert.Equal("KS 60191", _r.ResolveSegment("{Schachtnummer}", SchachtCtx()));

    [Fact]
    public void ResolveSegment_ist_case_insensitiv()
        => Assert.Equal("20260626", _r.ResolveSegment("{datum}", HaltungCtx()));

    [Fact]
    public void ResolveSegment_fehlendes_datum_wird_leer()
        => Assert.Equal("", _r.ResolveSegment("{Datum}", new DistributionPatternContext()));

    [Fact]
    public void ResolveRelativePath_drei_ebenen_voll()
    {
        var path = _r.ResolveRelativePath(
            "{Gemeinde}", "{Haltung}", "{Datum}_{Haltung}", HaltungCtx(), ".pdf");
        Assert.Equal("Altdorf/06.24341-35625/20260626_06.24341-35625.pdf", Norm(path));
    }

    [Fact]
    public void ResolveRelativePath_leerer_unterordner_wird_weggelassen()
    {
        var path = _r.ResolveRelativePath(
            "{Gemeinde}", "", "{Datum}", HaltungCtx(), ".pdf");
        Assert.Equal("Altdorf/20260626.pdf", Norm(path));
    }

    [Fact]
    public void ResolveRelativePath_flach_ohne_ordner()
    {
        var path = _r.ResolveRelativePath(
            "", "", "{Datum}_{Haltung}", HaltungCtx(), ".pdf");
        Assert.Equal("20260626_06.24341-35625.pdf", Norm(path));
    }

    [Fact]
    public void ResolveRelativePath_sanitisiert_ungueltige_zeichen()
    {
        var ctx = new DistributionPatternContext(Datum: Datum, Haltung: "06/24341:35625");
        var path = _r.ResolveRelativePath("", "", "{Haltung}", ctx, ".pdf");
        // '/' und ':' sind ungueltige Dateinamen-Zeichen -> ersetzt, KEINE zusaetzliche Ordnerebene
        Assert.DoesNotContain("/", Norm(path)[..^4]); // vor ".pdf" kein Trenner
        Assert.EndsWith(".pdf", path);
    }

    [Fact]
    public void ResolveRelativePath_leerer_dateiname_faellt_auf_fallback()
    {
        var path = _r.ResolveRelativePath(
            "{Gemeinde}", "", "{Schachtnummer}", HaltungCtx(), ".pdf"); // HaltungCtx hat keine Schachtnummer
        Assert.Equal("Altdorf/unbenannt.pdf", Norm(path));
    }
}
