using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Foto-Sammlung fuer die Galerie im Haltungs-Detail: alle FotoPaths der
/// Protokolleintraege, gegen den Projekt-Root aufgeloest, dedupliziert,
/// beschriftet mit Meter + Code.
/// </summary>
public sealed class HaltungFotoGalerieBuilderTests
{
    private static HaltungRecord Haltung(params ProtocolEntry[] entries)
    {
        var record = new HaltungRecord();
        record.Protocol = new ProtocolDocument();
        foreach (var e in entries)
            record.Protocol.Current.Entries.Add(e);
        return record;
    }

    private static ProtocolEntry Eintrag(string code, double meter, params string[] fotos)
    {
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Imported,
            Code = code,
            MeterStart = meter,
            MeterEnd = meter
        };
        foreach (var f in fotos)
            entry.FotoPaths.Add(f);
        return entry;
    }

    [Fact]
    public void Build_NimmtKeineFotosAusOriginalUndVeraendertCurrentNicht()
    {
        var current = Eintrag("BCCYB", 3.57);
        var original = Eintrag("BCCYB", 3.57, @"Fotos\Haltungen\H1\f1.jpg");
        var record = Haltung(current);
        record.Protocol!.Original.Entries.Add(original);

        var fotos = HaltungFotoGalerieBuilder.Build(record, @"C:\Projekt", _ => true);

        Assert.Empty(fotos);
        Assert.Empty(current.FotoPaths);
    }

    [Fact]
    public void Build_LoestRelativePfadeAuf_UndBeschriftetMitMeterUndCode()
    {
        var record = Haltung(Eintrag("BBBA", 20.5, @"Fotos\Haltungen\H1\f1.jpg"));

        var fotos = HaltungFotoGalerieBuilder.Build(record, @"C:\Projekt", _ => true);

        var foto = Assert.Single(fotos);
        Assert.Equal(@"C:\Projekt\Fotos\Haltungen\H1\f1.jpg", foto.Pfad);
        Assert.Equal("20.5 m · BBBA", foto.Beschriftung);
    }

    [Fact]
    public void Build_UeberspringtFehlendeDateien_UndDedupliziert()
    {
        var record = Haltung(
            Eintrag("BAB", 1.0, @"Fotos\a.jpg", @"Fotos\fehlt.jpg"),
            Eintrag("BAC", 2.0, @"Fotos\a.jpg")); // Duplikat

        var fotos = HaltungFotoGalerieBuilder.Build(
            record, @"C:\P",
            pfad => !pfad.Contains("fehlt"));

        var foto = Assert.Single(fotos);
        Assert.Contains("a.jpg", foto.Pfad);
        Assert.Equal("1.0 m · BAB", foto.Beschriftung); // erster Eintrag gewinnt
    }

    [Fact]
    public void Build_SortiertNachMeter()
    {
        var record = Haltung(
            Eintrag("BCE", 30.4, @"F\ende.jpg"),
            Eintrag("BCD", 0.0, @"F\anfang.jpg"));

        var fotos = HaltungFotoGalerieBuilder.Build(record, @"C:\P", _ => true);

        Assert.Equal(2, fotos.Count);
        Assert.Contains("anfang", fotos[0].Pfad);
        Assert.Contains("ende", fotos[1].Pfad);
    }

    [Fact]
    public void Build_LeereEingaben_LiefernLeereListe()
    {
        Assert.Empty(HaltungFotoGalerieBuilder.Build(null, @"C:\P", _ => true));
        Assert.Empty(HaltungFotoGalerieBuilder.Build(Haltung(), @"C:\P", _ => true));
        Assert.Empty(HaltungFotoGalerieBuilder.Build(
            Haltung(Eintrag("BAB", 1.0)), @"C:\P", _ => true)); // Eintrag ohne Fotos
    }
}
