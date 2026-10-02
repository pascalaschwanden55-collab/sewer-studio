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

    // Review PR #78: Innerhalb einer Schreibweisen-Gruppe gehen Handwerte (auch bewusst leer)
    // automatischen Quellen vor; erst innerhalb derselben Klasse entscheidet die Zeit.
    private const string A = "Primäre Schäden";
    private const string B = "Primaere Schaeden";

    private static void Schreibe(SchachtRecord record, string feld, string wert, bool hand, int minute)
    {
        record.SetFieldValue(feld, wert, hand ? FieldSource.Manual : FieldSource.Pdf, userEdited: hand);
        record.FieldMeta[feld].LastUpdatedUtc = new DateTime(2026, 10, 2, 12, minute, 0, DateTimeKind.Utc);
    }

    private static string Aktuell(SchachtRecord record)
        => SchachtFeldnamen.AktuellerWert(record.Fields, record.FieldMeta, [A, B]);

    [Fact]
    public void Handwert_gewinnt_gegen_spaeteren_Import_in_anderer_Schreibweise()
    {
        // Bestand aus der Zeit vor der Gruppensperre (SchachtFeldnamen.HatHandwert): Der Import
        // schrieb alle Schreibweisen; die handbearbeitete blieb geschuetzt, die andere bekam den
        // Importwert mit neuerem Zeitstempel. Heute sperrt der Schreibweg das, deshalb wird der
        // Bestand hier in umgekehrter Reihenfolge angelegt und danach gestempelt.
        var record = new SchachtRecord();
        Schreibe(record, B, "Importwert", hand: false, minute: 2);
        Schreibe(record, A, "Handkorrektur", hand: true, minute: 1);

        Assert.Equal("Handkorrektur", Aktuell(record));
    }

    [Fact]
    public void Bewusst_leerer_Handwert_gewinnt_gegen_spaeteren_Import()
    {
        var record = new SchachtRecord();
        Schreibe(record, B, "Importwert", hand: false, minute: 2);
        Schreibe(record, A, "", hand: true, minute: 1);

        Assert.Equal("", Aktuell(record));
    }

    [Fact]
    public void Von_zwei_Handwerten_gewinnt_der_juengste()
    {
        var record = new SchachtRecord();
        Schreibe(record, A, "eins", hand: true, minute: 2);
        Schreibe(record, B, "zwei", hand: true, minute: 3);
        Assert.Equal("zwei", Aktuell(record));

        Schreibe(record, A, "drei", hand: true, minute: 4);
        Assert.Equal("drei", Aktuell(record));
    }

    [Fact]
    public void Ohne_Handwert_gewinnt_der_juengste_Importwert_und_ohne_Zeit_der_erste()
    {
        var record = new SchachtRecord();
        Schreibe(record, A, "alt", hand: false, minute: 1);
        Schreibe(record, B, "neu", hand: false, minute: 2);
        Assert.Equal("neu", Aktuell(record));

        var ohneZeit = new Dictionary<string, string> { [A] = "", [B] = "zweiter" };
        Assert.Equal("zweiter", SchachtFeldnamen.AktuellerWert(ohneZeit, null, [A, B]));
    }
}
