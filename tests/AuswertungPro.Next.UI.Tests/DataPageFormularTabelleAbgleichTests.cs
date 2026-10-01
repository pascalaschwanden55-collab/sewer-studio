using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 1, Nachpruefung W01: Tabelle und Formular zeigen dieselbe Haltung gleichzeitig.
/// Eine neuere Korrektur im Datensatz darf durch eine Formulareingabe nie verloren gehen.
/// Der Ablauf entspricht der Gegenprobe der Nachpruefung (Bemerkungen: Alt -> Neue
/// Tabellenkorrektur -> Zusatz im Formular).
/// </summary>
public sealed class DataPageFormularTabelleAbgleichTests
{
    private const string Feld = "Bemerkungen";

    private static (DataPageDetailItemFactory Fabrik, List<(string Feld, string Aktuell, string Eingabe)> Konflikte, List<string> Commits) Fabrik()
    {
        var konflikte = new List<(string, string, string)>();
        var commits = new List<string>();
        var fabrik = new DataPageDetailItemFactory(
            _ => null,
            (record, feld, wert) =>
            {
                commits.Add(wert);
                record.SetFieldValue(feld, wert, FieldSource.Manual, userEdited: true);
            },
            konfliktGemeldet: (feld, aktuell, eingabe) => konflikte.Add((feld, aktuell, eingabe)));
        return (fabrik, konflikte, commits);
    }

    private static HaltungRecord RecordMit(string wert)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(Feld, wert, FieldSource.Manual, userEdited: true);
        return record;
    }

    [Fact]
    public void Tabellenkorrektur_erscheint_im_Formular_und_bleibt_beim_Zusatz_erhalten()
    {
        var (fabrik, konflikte, _) = Fabrik();
        var record = RecordMit("Alt");
        var item = fabrik.Create(Feld, record);
        using var sync = new DataPageDetailLiveSync(record, [new RecordDetailGroup("Test", string.Empty, [item])]);

        // Tabelle (oder ein Dienst) aendert den Datensatz, waehrend das Formular offen ist.
        record.SetFieldValue(Feld, "Neue Tabellenkorrektur", FieldSource.Manual, userEdited: true);
        Assert.Equal("Neue Tabellenkorrektur", item.Value);
        Assert.Equal("Neue Tabellenkorrektur", item.Ausgangswert);

        // Der Zusatz im Formular baut auf dem aktuellen Wert auf.
        item.Value = item.Value + " + Zusatz im Formular";

        Assert.Equal("Neue Tabellenkorrektur + Zusatz im Formular", record.GetFieldValue(Feld));
        Assert.Empty(konflikte);
    }

    [Fact]
    public void Aenderung_waehrend_der_Bearbeitung_ueberschreibt_die_neuere_Korrektur_nicht()
    {
        var (fabrik, konflikte, _) = Fabrik();
        var record = RecordMit("Alt");
        var item = fabrik.Create(Feld, record);
        using var sync = new DataPageDetailLiveSync(record, [new RecordDetailGroup("Test", string.Empty, [item])]);

        // Der Editor hat den Fokus: Der Text bleibt unter dem Cursor stehen ...
        item.IsEditing = true;
        record.SetFieldValue(Feld, "Neue Tabellenkorrektur", FieldSource.Manual, userEdited: true);
        Assert.Equal("Alt", item.Value);

        // ... aber die veraltete Eingabe darf die neuere Korrektur nicht ersetzen.
        item.Value = "Alt + Zusatz im Formular";

        Assert.Equal("Neue Tabellenkorrektur", record.GetFieldValue(Feld));
        var konflikt = Assert.Single(konflikte);
        Assert.Equal((Feld, "Neue Tabellenkorrektur", "Alt + Zusatz im Formular"), konflikt);

        // Nach dem Fokusverlust zeigt das Formular den gueltigen Datensatzwert.
        item.BeendeBearbeitung();
        Assert.False(item.IsEditing);
        Assert.Equal("Neue Tabellenkorrektur", item.Value);
        Assert.Equal("Neue Tabellenkorrektur", item.Ausgangswert);
    }

    [Fact]
    public void Zwei_Formulareingaben_nacheinander_sind_kein_Konflikt_auch_ohne_Live_Abgleich()
    {
        // Das modale Detailfenster verwendet dieselbe Fabrik ohne DataPageDetailLiveSync.
        var (fabrik, konflikte, commits) = Fabrik();
        var record = RecordMit("Alt");
        var item = fabrik.Create(Feld, record);

        item.Value = "eins";
        item.Value = "zwei";

        Assert.Equal("zwei", record.GetFieldValue(Feld));
        Assert.Equal(["eins", "zwei"], commits);
        Assert.Empty(konflikte);
        Assert.Equal("zwei", item.Ausgangswert);
    }

    [Fact]
    public void Uebernahme_aus_dem_Datensatz_schreibt_nicht_zurueck()
    {
        var (fabrik, _, commits) = Fabrik();
        var item = fabrik.Create(Feld, RecordMit("Alt"));

        item.UebernehmeAusDatensatz("Von aussen");

        Assert.Equal("Von aussen", item.Value);
        Assert.Empty(commits);
    }

    [Fact]
    public void Live_Abgleich_endet_mit_Dispose()
    {
        var (fabrik, _, _) = Fabrik();
        var record = RecordMit("Alt");
        var item = fabrik.Create(Feld, record);
        var sync = new DataPageDetailLiveSync(record, [new RecordDetailGroup("Test", string.Empty, [item])]);
        Assert.Equal(1, sync.FeldAnzahl);

        sync.Dispose();
        record.SetFieldValue(Feld, "Nach Dispose", FieldSource.Manual, userEdited: true);

        Assert.Equal("Alt", item.Value);
    }

    [Theory]
    [InlineData("Alt", "Alt", "Alt + Zusatz", false)]           // Datensatz unveraendert: schreiben
    [InlineData("Alt", "Neu", "Alt + Zusatz", true)]            // Datensatz inzwischen anders: Konflikt
    [InlineData("Alt", "Neu", "Neu", false)]                    // Eingabe ist genau der neue Wert: kein Konflikt
    [InlineData("", "", "x", false)]                            // leeres Feld, erste Eingabe
    public void Konfliktregel(string ausgangswert, string aktuell, string eingabe, bool konflikt)
        => Assert.Equal(konflikt, DataPageDetailItemFactory.IstKonflikt(ausgangswert, aktuell, eingabe));
}
