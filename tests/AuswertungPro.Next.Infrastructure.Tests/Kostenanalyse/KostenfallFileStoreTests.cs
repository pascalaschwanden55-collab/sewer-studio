using System;
using System.IO;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Infrastructure.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallFileStoreTests : IDisposable
{
    private readonly string _wurzel = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }

    private static Kostenfall Fall(string haltung) => new()
    {
        Haltung = haltung,
        Projekt = "Zone 1.15",
        ErfasstUtc = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
        Herkunft = KostenfallHerkunft.Unbeeinflusst,
        Merkmale = new KostenfallMerkmale
        {
            DnMm = 300,
            LaengeM = 42.5,
            BogenAnzahl = 1,
            AnschlussAnzahl = 2,
            Schaeden = [new SchadensMerkmal("BAF", 2, true)]
        },
        Positionen = [new MassnahmePosition("SCHLAUCHLINER_GFK", 42.5m, "m")]
    };

    [Fact]
    public void Ein_leerer_Ordner_liefert_keine_Faelle()
    {
        Assert.Empty(new KostenfallFileStore(_wurzel).Lade());
    }

    [Fact]
    public void Gespeicherte_Faelle_kommen_unveraendert_zurueck()
    {
        var store = new KostenfallFileStore(_wurzel);
        store.Speichere([Fall("H-1"), Fall("H-2")]);

        var geladen = new KostenfallFileStore(_wurzel).Lade();

        Assert.Equal(2, geladen.Count);
        var erster = Assert.Single(geladen, f => f.Haltung == "H-1");
        Assert.Equal(300, erster.Merkmale.DnMm);
        Assert.Equal(42.5, erster.Merkmale.LaengeM);
        Assert.Equal(1, erster.Merkmale.BogenAnzahl);
        Assert.Equal(2, erster.Merkmale.AnschlussAnzahl);
        Assert.Equal("BAF", Assert.Single(erster.Merkmale.Schaeden).Hauptcode);
        Assert.Equal(42.5m, Assert.Single(erster.Positionen).Menge);
    }

    [Fact]
    public void Eine_beschaedigte_Datei_wird_gemeldet_und_nicht_ueberschrieben()
    {
        var pfad = Path.Combine(_wurzel, "kostenanalyse", "kostenfaelle_v1.json");
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        File.WriteAllText(pfad, "{ kaputt");

        var store = new KostenfallFileStore(_wurzel);

        Assert.Throws<InvalidDataException>(() => store.Lade());
        Assert.Equal("{ kaputt", File.ReadAllText(pfad));
    }
}
