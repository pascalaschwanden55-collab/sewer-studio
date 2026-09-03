using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Zeichensalat in Feldnamen: derselbe Wert unter drei Schreibweisen. Gemessen im
/// Projekt Jagdmatt, wo "Primäre Schäden" viermal und die Ausfuehrung siebenmal
/// dasteht.
/// </summary>
public sealed class SchachtFeldnamenReparaturTests
{
    // Die echten kaputten Namen aus dem Projekt.
    [Theory]
    [InlineData("PrimÃ¤re SchÃ¤den", "Primäre Schäden")]
    [InlineData("PrimÃƒÂ¤re SchÃƒÂ¤den", "Primäre Schäden")]
    [InlineData("AusfÃ¼hrung Datum/Jahr", "Ausführung Datum/Jahr")]
    [InlineData("AusfÃƒÂ¼hrung Datum/Jahr", "Ausführung Datum/Jahr")]
    public void Zeichensalat_wird_zurueckgerechnet(string kaputt, string erwartet)
        => Assert.Equal(erwartet, SchachtFeldnamenReparatur.Entwirre(kaputt));

    // Der wichtigste Test: Ein korrekter Name darf den Lauf unveraendert ueberstehen.
    // Sonst macht die Reparatur aus heilen Namen neue kaputte.
    [Theory]
    [InlineData("Primäre Schäden")]
    [InlineData("Eigentümer")]
    [InlineData("Strasse")]
    [InlineData("Straße")]
    [InlineData("NR.")]
    [InlineData("Status\noffen/abgeschlossen")]
    [InlineData("Dimension 1 mm")]
    [InlineData("")]
    public void Ein_richtiger_Name_bleibt_unveraendert(string name)
        => Assert.Equal(name, SchachtFeldnamenReparatur.Entwirre(name));

    [Fact]
    public void Vier_Schreibweisen_werden_zu_einer()
    {
        var record = Schacht();
        foreach (var name in new[]
                 { "Primäre Schäden", "Primaere Schaeden", "PrimÃ¤re SchÃ¤den", "PrimÃƒÂ¤re SchÃƒÂ¤den" })
        {
            record.Fields[name] = "BAB Riss";
        }

        var gruppen = SchachtFeldnamenReparatur.Plane(record, new[] { "Primäre Schäden" });
        var gruppe = Assert.Single(gruppen);

        Assert.Equal("Primäre Schäden", gruppe.Ziel);
        Assert.Equal(3, gruppe.Aufzuloesen.Count);
        Assert.False(gruppe.Uneindeutig);

        Assert.Equal(3, SchachtFeldnamenReparatur.Wende(record, gruppen));
        Assert.Equal("BAB Riss", record.GetFieldValue("Primäre Schäden"));
        Assert.False(record.Fields.ContainsKey("PrimÃ¤re SchÃ¤den"));
    }

    // Fail-closed: Verschiedene Werte unter verschiedenen Schreibweisen bedeuten,
    // dass irgendwann getrennt weitergearbeitet wurde. Welcher gilt, weiss nur der
    // Mensch — dann wird nichts angefasst.
    [Fact]
    public void Verschiedene_Werte_werden_nicht_zusammengefuehrt()
    {
        var record = Schacht();
        record.Fields["Primäre Schäden"] = "BAB Riss";
        record.Fields["PrimÃ¤re SchÃ¤den"] = "BAC Bruch";

        var gruppen = SchachtFeldnamenReparatur.Plane(record);
        Assert.True(Assert.Single(gruppen).Uneindeutig);

        Assert.Equal(0, SchachtFeldnamenReparatur.Wende(record, gruppen));
        Assert.Equal("BAB Riss", record.GetFieldValue("Primäre Schäden"));
        Assert.Equal("BAC Bruch", record.GetFieldValue("PrimÃ¤re SchÃ¤den"));
    }

    // Der Wert der gefuellten Schreibweise ueberlebt, auch wenn das Ziel leer war.
    [Fact]
    public void Der_vorhandene_Wert_ueberlebt_die_Zusammenfuehrung()
    {
        var record = Schacht();
        record.Fields["Ausführung Datum/Jahr"] = "";
        record.Fields["AusfÃ¼hrung Datum/Jahr"] = "2018";

        var gruppen = SchachtFeldnamenReparatur.Plane(record, new[] { "Ausführung Datum/Jahr" });
        SchachtFeldnamenReparatur.Wende(record, gruppen);

        Assert.Equal("2018", record.GetFieldValue("Ausführung Datum/Jahr"));
    }

    [Fact]
    public void Ein_sauberer_Datensatz_ergibt_keine_Gruppe()
    {
        var record = Schacht();
        record.Fields["Funktion"] = "Kontrollschacht";
        record.Fields["Material"] = "Beton";

        Assert.Empty(SchachtFeldnamenReparatur.Plane(record));
    }

    // Die Spalte der Oberflaeche gewinnt: Sonst laege der zusammengefuehrte Wert
    // danach in einem Feld, das die Tabelle nicht anzeigt.
    [Fact]
    public void Der_Name_der_Oberflaeche_gewinnt()
    {
        var record = Schacht();
        record.Fields["Eigentuemer"] = "Privat";
        record.Fields["Eigentümer"] = "";

        var gruppen = SchachtFeldnamenReparatur.Plane(record, new[] { "Eigentümer" });
        SchachtFeldnamenReparatur.Wende(record, gruppen);

        Assert.Equal("Privat", record.GetFieldValue("Eigentümer"));
        Assert.False(record.Fields.ContainsKey("Eigentuemer"));
    }

    private static SchachtRecord Schacht()
    {
        var record = new SchachtRecord();
        record.Fields["Schachtnummer"] = "80089";
        return record;
    }
}
