using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// W01 fuer Schaechte (Deepscan 02.10.2026, Welle 2): Tabelle und Formular zeigen denselben
/// Schacht gleichzeitig. Eine neuere Korrektur im Datensatz (Inline-Edit, Rueckgaengig,
/// Uebernahme) darf durch eine Formulareingabe, die auf einem aelteren Stand beruht, nie still
/// verloren gehen. Dieselbe Regel wie <see cref="DataPageFormularTabelleAbgleichTests"/>.
/// </summary>
public sealed class SchaechteFormularTabelleAbgleichTests
{
    private const string Feld = "Bemerkung";

    private static (SchaechteRecordDetailsBuilder Builder, List<(string Feld, string Aktuell, string Eingabe)> Konflikte, List<string?> Commits) Builder()
    {
        var konflikte = new List<(string, string, string)>();
        var commits = new List<string?>();
        // Wie CommitSchachtDetailKonsolidiert: alle Schreibweisen, Handwert.
        var builder = new SchaechteRecordDetailsBuilder(
            _ => [],
            _ => null,
            (record, feld, wert) =>
            {
                commits.Add(wert);
                foreach (var key in feld.AlleKeys)
                    record.SetFieldValue(key, wert ?? string.Empty, FieldSource.Manual, userEdited: true);
            },
            konfliktGemeldet: (feld, aktuell, eingabe) => konflikte.Add((feld, aktuell, eingabe)));
        return (builder, konflikte, commits);
    }

    private static SchachtRecord RecordMit(string wert)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "S-1");
        record.SetFieldValue(Feld, wert, FieldSource.Manual, userEdited: true);
        return record;
    }

    private static RecordDetailItem Item(IReadOnlyList<RecordDetailGroup> gruppen, string feld)
        => gruppen.SelectMany(g => g.Items).Single(i => i.FieldName == feld);

    [Fact]
    public void Aenderung_waehrend_der_Bearbeitung_ueberschreibt_die_neuere_Tabellenkorrektur_nicht()
    {
        var (builder, konflikte, _) = Builder();
        var record = RecordMit("Alt");
        var gruppen = builder.Build([Feld], record);
        var item = Item(gruppen, Feld);
        using var sync = new DataPageDetailLiveSync(record, record.GetFieldValue, gruppen);

        // Der Editor hat den Fokus, die Tabelle korrigiert denselben Wert.
        item.IsEditing = true;
        record.SetFieldValue(Feld, "Neue Tabellenkorrektur", FieldSource.Manual, userEdited: true);
        Assert.Equal("Alt", item.Value);

        // Die veraltete Formulareingabe darf die neuere Korrektur nicht ersetzen.
        item.Value = "Alt + Zusatz im Formular";

        Assert.Equal("Neue Tabellenkorrektur", record.GetFieldValue(Feld));
        Assert.Equal((Feld, "Neue Tabellenkorrektur", "Alt + Zusatz im Formular"), Assert.Single(konflikte));

        item.BeendeBearbeitung();
        Assert.Equal("Neue Tabellenkorrektur", item.Value);
        Assert.Equal("Neue Tabellenkorrektur", item.Ausgangswert);
    }

    [Fact]
    public void Detailfenster_ohne_Live_Abgleich_ueberschreibt_die_neuere_Korrektur_nicht()
    {
        // Das Detailfenster (RecordDetailsWindow) nutzt denselben Builder ohne Live-Abgleich.
        var (builder, konflikte, commits) = Builder();
        var record = RecordMit("Alt");
        var item = Item(builder.Build([Feld], record), Feld);

        record.SetFieldValue(Feld, "Rueckgaengig gemacht", FieldSource.Manual, userEdited: true);
        item.Value = "Alt + Zusatz";

        Assert.Equal("Rueckgaengig gemacht", record.GetFieldValue(Feld));
        Assert.Empty(commits);
        Assert.Equal((Feld, "Rueckgaengig gemacht", "Alt + Zusatz"), Assert.Single(konflikte));
        Assert.Equal("Rueckgaengig gemacht", item.Value);
    }

    [Fact]
    public void Tabellenkorrektur_erscheint_im_Formular_und_bleibt_beim_Zusatz_erhalten()
    {
        var (builder, konflikte, _) = Builder();
        var record = RecordMit("Alt");
        var gruppen = builder.Build([Feld], record);
        var item = Item(gruppen, Feld);
        using var sync = new DataPageDetailLiveSync(record, record.GetFieldValue, gruppen);

        record.SetFieldValue(Feld, "Neue Tabellenkorrektur", FieldSource.Manual, userEdited: true);
        Assert.Equal("Neue Tabellenkorrektur", item.Value);

        item.Value = item.Value + " + Zusatz im Formular";

        Assert.Equal("Neue Tabellenkorrektur + Zusatz im Formular", record.GetFieldValue(Feld));
        Assert.Empty(konflikte);
    }

    [Fact]
    public void Zwei_Formulareingaben_nacheinander_sind_kein_Konflikt_auch_ohne_Live_Abgleich()
    {
        var (builder, konflikte, commits) = Builder();
        var record = RecordMit("Alt");
        var item = Item(builder.Build([Feld], record), Feld);

        item.Value = "eins";
        item.Value = "zwei";

        Assert.Equal("zwei", record.GetFieldValue(Feld));
        Assert.Equal(["eins", "zwei"], commits);
        Assert.Empty(konflikte);
        Assert.Equal("zwei", item.Ausgangswert);
    }

    [Fact]
    public void Schreibweisen_eines_Feldes_gelten_als_ein_Wert_und_sind_kein_Konflikt()
    {
        // Die Vorlage fuehrt "Ausführung" leer, der Import schrieb "Ausfuehrung". Das Formular
        // zeigt EIN Feld; der Abgleich darf es nach einer fremden Feldaenderung nicht leeren,
        // und die naechste Eingabe ist deshalb kein (falscher) Konflikt.
        var (builder, konflikte, _) = Builder();
        var record = RecordMit("Alt");
        record.SetFieldValue("Ausfuehrung", "Beton");
        record.SetFieldValue("Ausführung", "");
        var gruppen = builder.Build(["Ausführung", Feld], record);
        var item = Item(gruppen, "Ausführung");
        using var sync = new DataPageDetailLiveSync(record, record.GetFieldValue, gruppen);
        Assert.Equal("Beton", item.Value);

        record.SetFieldValue(Feld, "anderes Feld geaendert", FieldSource.Manual, userEdited: true);
        Assert.Equal("Beton", item.Value);

        item.Value = "Kunststoff";

        Assert.Empty(konflikte);
        Assert.Equal("Kunststoff", record.GetFieldValue("Ausfuehrung"));
        Assert.Equal("Kunststoff", record.GetFieldValue("Ausführung"));
    }

    [Fact]
    public void Bewusst_leer_aus_der_Tabelle_bleibt_leer_und_Handwert()
    {
        // E3: Ein in der Tabelle bewusst geleertes Feld ist eine neuere Korrektur wie jede andere.
        var (builder, konflikte, _) = Builder();
        var record = RecordMit("Alt");
        var item = Item(builder.Build([Feld], record), Feld);

        record.SetFieldValue(Feld, "", FieldSource.Manual, userEdited: true);
        item.Value = "Alt + Zusatz";

        Assert.True(record.IstBewusstLeer(Feld));
        Assert.Equal((Feld, "", "Alt + Zusatz"), Assert.Single(konflikte));
    }

    [Fact]
    public void Formular_leert_ein_Feld_ohne_neuere_Korrektur_als_Handwert()
    {
        var (builder, konflikte, _) = Builder();
        var record = RecordMit("Alt");
        var item = Item(builder.Build([Feld], record), Feld);

        item.Value = "";

        Assert.Empty(konflikte);
        Assert.True(record.IstBewusstLeer(Feld));
    }

    // Zeitstempel ausdruecklich, damit die Reihenfolge nicht an der Uhr haengt.
    private static void Stempel(SchachtRecord record, string feld, int minute)
        => record.FieldMeta[feld].LastUpdatedUtc = new DateTime(2026, 10, 2, 12, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Bewusst_geleerte_juengste_Schreibweise_ist_der_aktuelle_Stand()
    {
        // Review PR #75 (P1): Das Formular hat beide Schreibweisen gleich gesetzt, danach leert die
        // Tabelle eine davon bewusst. Die andere traegt noch den Altwert. Massgebend ist die zuletzt
        // geaenderte Schreibweise - sonst gilt die Formulareingabe nicht als Konflikt und
        // ueberschreibt die Leer-Korrektur in beiden Schreibweisen.
        var (builder, konflikte, commits) = Builder();
        var record = RecordMit("Alt");
        record.SetFieldValue("Ausführung", "Beton", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Ausfuehrung", "Beton", FieldSource.Manual, userEdited: true);
        Stempel(record, "Ausführung", 1);
        Stempel(record, "Ausfuehrung", 1);
        var item = Item(builder.Build(["Ausfuehrung", "Ausführung", Feld], record), "Ausfuehrung");
        Assert.Equal("Beton", item.Value);

        record.SetFieldValue("Ausführung", "", FieldSource.Manual, userEdited: true);
        Stempel(record, "Ausführung", 2);
        item.Value = "Beton + Zusatz";

        Assert.Empty(commits);
        Assert.Equal(("Ausfuehrung", "", "Beton + Zusatz"), Assert.Single(konflikte));
        Assert.True(record.IstBewusstLeer("Ausführung"));
        Assert.Equal("Beton", record.GetFieldValue("Ausfuehrung"));

        // Neu aufgebaut zeigt das Formular den juengsten Stand: bewusst leer.
        Assert.Equal("", Item(builder.Build(["Ausfuehrung", "Ausführung", Feld], record), "Ausfuehrung").Value);
    }

    [Fact]
    public void Gleiche_Schreibweisen_mit_verschiedenen_Zeitstempeln_sind_kein_Konflikt()
    {
        var (builder, konflikte, _) = Builder();
        var record = RecordMit("Alt");
        record.SetFieldValue("Ausführung", "Beton", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Ausfuehrung", "Beton", FieldSource.Manual, userEdited: true);
        Stempel(record, "Ausführung", 1);
        Stempel(record, "Ausfuehrung", 3);
        var item = Item(builder.Build(["Ausführung", Feld], record), "Ausführung");

        item.Value = "Kunststoff";

        Assert.Empty(konflikte);
        Assert.Equal("Kunststoff", record.GetFieldValue("Ausführung"));
        Assert.Equal("Kunststoff", record.GetFieldValue("Ausfuehrung"));
    }
}
