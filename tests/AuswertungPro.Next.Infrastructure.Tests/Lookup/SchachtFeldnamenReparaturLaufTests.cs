using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Der Aufraeumlauf ueber mehrere Schaechte: erst der Plan, dann der Bericht.
/// </summary>
public sealed class SchachtFeldnamenReparaturLaufTests
{
    [Fact]
    public void Der_Plan_zaehlt_ueber_alle_Schaechte()
    {
        var plan = SchachtFeldnamenReparaturLauf.Plane(
            new[] { MitDubletten("1"), MitDubletten("2"), Sauber("3") },
            new[] { "Primäre Schäden" });

        Assert.Equal(3, plan.GeprueteSchaechte);
        Assert.Equal(2, plan.BetroffeneSchaechte);
        Assert.Equal(4, plan.ZusammenzufuehrendeSchreibweisen);
        Assert.Equal(0, plan.UneindeutigeGruppen);
    }

    [Fact]
    public void Der_Bericht_nennt_Zahl_und_Zielnamen()
    {
        var plan = SchachtFeldnamenReparaturLauf.Plane(
            new[] { MitDubletten("1") }, new[] { "Primäre Schäden" });

        var bericht = SchachtFeldnamenReparaturLauf.Bericht(plan);

        Assert.Contains("2 doppelte Schreibweisen auf 1 Schaechten", bericht, StringComparison.Ordinal);
        Assert.Contains("Primäre Schäden", bericht, StringComparison.Ordinal);
        Assert.Contains("gehen dabei nicht verloren", bericht, StringComparison.Ordinal);
    }

    // Der Zeilenumbruch aus der Excel-Kopfzeile darf die Aufzaehlung im Dialog nicht
    // zerreissen.
    [Fact]
    public void Ein_Zeilenumbruch_im_Feldnamen_bleibt_im_Bericht_einzeilig()
    {
        var record = new SchachtRecord();
        record.Fields["Schachtnummer"] = "1";
        record.Fields["Status\noffen/abgeschlossen"] = "offen";
        record.Fields["Status offen/abgeschlossen"] = "";

        var bericht = SchachtFeldnamenReparaturLauf.Bericht(
            SchachtFeldnamenReparaturLauf.Plane(new[] { record }, new[] { "Status\noffen/abgeschlossen" }));

        Assert.Contains("Status offen/abgeschlossen", bericht, StringComparison.Ordinal);
        foreach (var zeile in bericht.Split('\n'))
            Assert.DoesNotContain("  ->  Status\r", zeile, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Lauf_fuehrt_zusammen_und_zaehlt()
    {
        var eins = MitDubletten("1");
        var plan = SchachtFeldnamenReparaturLauf.Plane(new[] { eins }, new[] { "Primäre Schäden" });

        Assert.Equal(2, SchachtFeldnamenReparaturLauf.Wende(plan));
        Assert.Equal("BAB Riss", eins.GetFieldValue("Primäre Schäden"));
        Assert.False(eins.Fields.ContainsKey("PrimÃ¤re SchÃ¤den"));
    }

    [Fact]
    public void Ein_sauberes_Projekt_meldet_nichts_zu_tun()
    {
        var plan = SchachtFeldnamenReparaturLauf.Plane(new[] { Sauber("1") });

        Assert.True(plan.OhneAenderung);
        Assert.Contains("nichts zusammenzufuehren", SchachtFeldnamenReparaturLauf.Bericht(plan),
            StringComparison.Ordinal);
    }

    private static SchachtRecord MitDubletten(string nummer)
    {
        var record = Sauber(nummer);
        record.Fields["Primäre Schäden"] = "BAB Riss";
        record.Fields["PrimÃ¤re SchÃ¤den"] = "BAB Riss";
        record.Fields["Primaere Schaeden"] = "";
        return record;
    }

    private static SchachtRecord Sauber(string nummer)
    {
        var record = new SchachtRecord();
        record.Fields["Schachtnummer"] = nummer;
        record.Fields["Funktion"] = "Kontrollschacht";
        return record;
    }
}
