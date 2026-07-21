using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageDropdownOptionSynchronizerTests
{
    [Fact]
    public void ParseRecommendedTemplates_trennt_normalisiert_und_entdoppelt()
    {
        var result = DataPageDropdownOptionSynchronizer.ParseRecommendedTemplates(
            "- Inliner\r\n*Manschette; inliner, Kurzliner| --* Roboter");

        Assert.Equal(
            ["Inliner", "Manschette", "Kurzliner", "Roboter"],
            result);
    }

    [Fact]
    public void SyncFromRecords_uebernimmt_Feldwerte_mit_bisheriger_Reihenfolge()
    {
        var first = new HaltungRecord();
        Set(first, "Sanieren_JaNein", "Ja");
        Set(first, "Pruefungsresultat", "Bestanden");
        Set(first, "Referenzpruefung", "Ja");
        Set(first, "Empfohlene_Sanierungsmassnahmen", "- Inliner; Manschette");

        var second = new HaltungRecord();
        Set(second, "Sanieren_JaNein", "ja");
        Set(second, "Pruefungsresultat", "Keine");
        Set(second, "Referenzpruefung", "   ");
        Set(second, "Empfohlene_Sanierungsmassnahmen", "inliner|Kurzliner");

        var options = CreateOptions();

        DataPageDropdownOptionSynchronizer.SyncFromRecords([first, second], options);

        Assert.Equal(["Ja", "Nein"], options.SanierenOptions);
        Assert.Equal(["Bestanden", "Keine"], options.PruefungsresultatOptions);
        Assert.Equal(["Ja", "Nein"], options.ReferenzpruefungOptions);
        Assert.Equal(
            ["Kurzliner", "Manschette", "Inliner", "Vorhanden"],
            options.EmpfohleneSanierungsmassnahmenOptions);
    }

    [Fact]
    public void SyncFromRecords_ignoriert_leere_Werte_und_Eigentuemer()
    {
        var record = new HaltungRecord();
        Set(record, "Eigentuemer", "Neuer Eigentuemer");
        var options = CreateOptions();

        DataPageDropdownOptionSynchronizer.SyncFromRecords([record], options);

        Assert.Equal(["Nein"], options.SanierenOptions);
        Assert.Equal(["Keine"], options.PruefungsresultatOptions);
        Assert.Equal(["Nein"], options.ReferenzpruefungOptions);
        Assert.Equal(["Vorhanden"], options.EmpfohleneSanierungsmassnahmenOptions);
    }

    private static DataPageDropdownOptionSets CreateOptions()
        => new(
            SanierenOptions: new ObservableCollection<string> { "Nein" },
            PruefungsresultatOptions: new ObservableCollection<string> { "Keine" },
            ReferenzpruefungOptions: new ObservableCollection<string> { "Nein" },
            EmpfohleneSanierungsmassnahmenOptions:
                new ObservableCollection<string> { "Vorhanden" });

    private static void Set(HaltungRecord record, string fieldName, string value)
        => record.SetFieldValue(fieldName, value, FieldSource.Manual, userEdited: false);
}
