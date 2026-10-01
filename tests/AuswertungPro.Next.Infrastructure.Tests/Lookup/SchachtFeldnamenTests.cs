using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Schachtfelder heissen nach der Excel-Kopfzeile. Dieselbe Angabe steht in echten
/// Projekten deshalb unter verschiedenen Schreibweisen.
/// </summary>
public sealed class SchachtFeldnamenTests
{
    [Theory]
    [InlineData("Eigentümer", "Eigentuemer")]
    [InlineData("Eigentuemer", "Eigentümer")]
    [InlineData("Primäre Schäden", "Primaere Schaeden")]
    [InlineData("Status\noffen/abgeschlossen", "Status offen/abgeschlossen")]
    [InlineData("Ausführung\nDatum/Jahr", "Ausfuehrung Datum/Jahr")]
    public void Zwei_Schreibweisen_derselben_Angabe_gelten_als_gleich(string a, string b)
        => Assert.Equal(SchachtFeldnamen.Falte(a), SchachtFeldnamen.Falte(b));

    [Theory]
    [InlineData("Dimension", "Dimension1_mm")]
    [InlineData("Material", "Funktion")]
    [InlineData("Status", "Strasse")]
    public void Verschiedene_Angaben_bleiben_verschieden(string a, string b)
        => Assert.NotEqual(SchachtFeldnamen.Falte(a), SchachtFeldnamen.Falte(b));

    [Fact]
    public void Der_vorhandene_Name_gewinnt()
    {
        var record = new SchachtRecord();
        record.Fields["Eigentümer"] = "";

        Assert.Equal("Eigentümer", SchachtFeldnamen.Feld(record, "Eigentuemer"));
    }

    // Bei mehreren Schreibweisen gewinnt die mit Inhalt — sonst verdeckte eine leere
    // Zweitschreibweise den echten Wert.
    [Fact]
    public void Bei_mehreren_Schreibweisen_gewinnt_die_gefuellte()
    {
        var record = new SchachtRecord();
        record.Fields["Primaere Schaeden"] = "";
        record.Fields["Primäre Schäden"] = "BAB Riss";

        Assert.Equal("Primäre Schäden", SchachtFeldnamen.Feld(record, "Primaere Schaeden"));
        Assert.Equal(2, SchachtFeldnamen.Schreibweisen(record, "Primäre Schäden").Count);
    }

    [Fact]
    public void Kennt_der_Datensatz_das_Feld_nicht_gilt_der_gemeinte_Name()
    {
        var record = new SchachtRecord();

        Assert.Equal("Dimension1_mm", SchachtFeldnamen.Feld(record, "Dimension1_mm"));
    }
}
