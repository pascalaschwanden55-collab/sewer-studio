using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteDropdownOptionSynchronizerTests
{
    [Fact]
    public void SyncFromRecords_adds_matching_schacht_field_values_to_option_lists()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Sanieren ja/nein", "Ja");
        record.SetFieldValue("Dichtheit Ergebnis", "Pruefung bestanden");
        record.SetFieldValue("Referenzpruefung", "Nein");
        record.SetFieldValue("Ausgefuehrt durch", "Muster AG");
        var options = CreateOptions();

        SchaechteDropdownOptionSynchronizer.SyncFromRecords(new[] { record }, options);

        Assert.Equal(new[] { "Ja", "Nein" }, options.SanierenOptions);
        Assert.Equal(new[] { "Pruefung bestanden" }, options.PruefungsresultatOptions);
        Assert.Equal(new[] { "Nein" }, options.ReferenzpruefungOptions);
        Assert.Equal(new[] { "Muster AG" }, options.AusgefuehrtDurchOptions);
    }

    [Fact]
    public void SyncFromRecords_handles_mojibake_keys_and_ignores_empty_or_duplicate_values()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Dichtheit", "Bestanden");
        record.SetFieldValue("Referenzpr" + "\u00c3\u00bc" + "fung", "Ja");
        record.SetFieldValue("Ausgefuhrt durch", "  ");
        var options = CreateOptions();
        options.ReferenzpruefungOptions.Add("ja");

        SchaechteDropdownOptionSynchronizer.SyncFromRecords(new[] { record }, options);

        Assert.Equal(new[] { "ja" }, options.ReferenzpruefungOptions);
        Assert.Equal(new[] { "Bestanden" }, options.PruefungsresultatOptions);
        Assert.Empty(options.AusgefuehrtDurchOptions);
    }

    private static SchaechteDropdownOptionSets CreateOptions()
        => new(
            SanierenOptions: new ObservableCollection<string> { "Nein" },
            PruefungsresultatOptions: new ObservableCollection<string>(),
            ReferenzpruefungOptions: new ObservableCollection<string>(),
            AusgefuehrtDurchOptions: new ObservableCollection<string>());
}
