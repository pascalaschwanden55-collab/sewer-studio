using System;
using AuswertungPro.Next.Application.Export;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

public sealed class DistributionDirectoryTreeResolverTests
{
    private static readonly DateTime Datum = new(2026, 6, 26);
    private readonly IDistributionDirectoryTreeResolver _resolver = new DistributionDirectoryTreeResolver();

    [Fact]
    public void AlteLeereKonfiguration_ErgibtRootUndFestenHaltungsordner()
    {
        var context = new DistributionPatternContext(Haltung: "06.24341-35625");

        var path = _resolver.ResolveObjectDirectory(
            "Root", null, null, "{Haltung}", context);

        Assert.Equal("Root/06.24341-35625", Norm(path));
    }

    [Fact]
    public void GemeindeUndJahr_StehenVorDemFestenHaltungsordner()
    {
        var context = new DistributionPatternContext(
            Datum: Datum,
            Gemeinde: "Altdorf",
            Haltung: "06.24341-35625");

        var path = _resolver.ResolveObjectDirectory(
            "Root", "{Gemeinde}", "{Jahr}", "{Haltung}", context);

        Assert.Equal("Root/Altdorf/2026/06.24341-35625", Norm(path));
    }

    [Fact]
    public void Schacht_VerwendetDieSchachtnummerAlsFesteLetzteEbene()
    {
        var context = new DistributionPatternContext(
            Gemeinde: "Altdorf",
            Schachtnummer: "KS 60191");

        var path = _resolver.ResolveObjectDirectory(
            "Root", "{Gemeinde}", null, "{Schachtnummer}", context);

        Assert.Equal("Root/Altdorf/KS 60191", Norm(path));
    }

    [Fact]
    public void UngueltigeZeichen_WerdenInAllenErzeugtenSegmentenBereinigt()
    {
        var context = new DistributionPatternContext(
            Gemeinde: "Alt/dorf",
            Haltung: "06:24341/35625");

        var path = _resolver.ResolveObjectDirectory(
            "Root", "{Gemeinde}", null, "{Haltung}", context);

        Assert.Equal("Root/Alt_dorf/06_24341_35625", Norm(path));
    }

    [Fact]
    public void LeereOptionaleEbenen_EntfallenVollstaendig()
    {
        var context = new DistributionPatternContext(Haltung: "06.24341-35625");

        var path = _resolver.ResolveObjectDirectory(
            "Root", "{Gemeinde}", " {Unbekannt} ", "{Haltung}", context);

        Assert.Equal("Root/06.24341-35625", Norm(path));
    }

    [Fact]
    public void HaltungImBenutzerordner_ErsetztNichtDieFesteLetzteEbene()
    {
        var context = new DistributionPatternContext(
            Datum: Datum,
            Haltung: "06.24341-35625");

        var path = _resolver.ResolveObjectDirectory(
            "Root", "{Haltung}", "{Jahr}", "{Haltung}", context);

        Assert.Equal("Root/06.24341-35625/2026/06.24341-35625", Norm(path));
    }

    [Fact]
    public void PunktSegmente_KoennenDieZielWurzelNichtVerlassen()
    {
        var context = new DistributionPatternContext(
            Gemeinde: "..",
            Haltung: "100-200");

        var path = _resolver.ResolveObjectDirectory(
            "Root", "{Gemeinde}", null, "{Haltung}", context);

        Assert.Equal("Root/UNKNOWN/100-200", Norm(path));
        Assert.DoesNotContain("../", Norm(path));
    }

    private static string Norm(string path) => path.Replace('\\', '/');
}
