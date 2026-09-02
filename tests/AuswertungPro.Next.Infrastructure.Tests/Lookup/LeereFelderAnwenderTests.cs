using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Der Ausfuehrer schreibt nur den Plan — und prueft dabei noch einmal, dass das
/// Zielfeld wirklich leer ist.
/// </summary>
public sealed class LeereFelderAnwenderTests
{
    [Fact]
    public void Ein_geplantes_Feld_wird_geschrieben()
    {
        var record = Haltung("80638-80631");

        var geschrieben = LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { record },
            Plan(new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug")));

        Assert.Equal(1, geschrieben);
        Assert.Equal("Steinzeug", record.GetFieldValue(FieldKeys.PipeMaterial));
    }

    // Der aus dem Kataster geholte Wert ist KEINE Handeingabe. Waere er als solche
    // markiert, ginge er beim naechsten Mal als "vom Operateur gesetzt" in die
    // revidierte XTF zurueck — in dieselbe Quelle, aus der er stammt.
    [Fact]
    public void Der_nachgefuellte_Wert_gilt_nicht_als_Handeingabe()
    {
        var record = Haltung("80638-80631");

        LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { record },
            Plan(new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug")));

        var meta = record.FieldMeta[FieldKeys.PipeMaterial];

        Assert.False(meta.UserEdited);
        Assert.Equal(FieldSource.Kataster, meta.Source);
    }

    // Zwischen Planung und Bestaetigung kann der Bearbeiter etwas eingetippt haben.
    // Seine Arbeit gewinnt auch dann.
    [Fact]
    public void Ein_inzwischen_gefuelltes_Feld_bleibt_unberuehrt()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Manual, userEdited: true);

        var geschrieben = LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { record },
            Plan(new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug")));

        Assert.Equal(0, geschrieben);
        Assert.Equal("Beton", record.GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Ein_Datensatz_ausserhalb_des_Plans_bleibt_unberuehrt()
    {
        var record = Haltung("99-999");

        var geschrieben = LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { record },
            Plan(new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug")));

        Assert.Equal(0, geschrieben);
        Assert.Equal("", record.GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Der_Bericht_nennt_Zahl_und_Feld()
    {
        var bericht = LeereFelderBericht.Schreibe(
            Plan(
                new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug"),
                new LeereFeldPosition("80631-80551", FieldKeys.PipeMaterial, "Zement")),
            @"D:\QGIS\Leitungen.gpkg");

        Assert.Contains("2 leere Felder auf 2 Haltungen", bericht, StringComparison.Ordinal);
        Assert.Contains("2x  Rohrmaterial", bericht, StringComparison.Ordinal);
        Assert.Contains(@"D:\QGIS\Leitungen.gpkg", bericht, StringComparison.Ordinal);
        Assert.Contains("nie ueberschrieben", bericht, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Bericht_nennt_die_mehrdeutigen_Namen()
    {
        var plan = new LeereFelderPlan(
            BauteilArt.Haltung,
            Array.Empty<LeereFeldPosition>(),
            new[] { new LeerfeldHinweis("u-u", LeerfeldGrund.Mehrdeutig) },
            GepruefteBauteile: 1);

        var bericht = LeereFelderBericht.Schreibe(plan, "x.gpkg");

        Assert.Contains("1 mit mehrfach vorkommendem Namen", bericht, StringComparison.Ordinal);
        Assert.Contains("nichts zu ergaenzen", bericht, StringComparison.OrdinalIgnoreCase);
    }

    private static LeereFelderPlan Plan(params LeereFeldPosition[] positionen)
        => new(BauteilArt.Haltung, positionen, Array.Empty<LeerfeldHinweis>(), positionen.Length);

    private static HaltungRecord Haltung(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Xtf, userEdited: false);
        return record;
    }
}
