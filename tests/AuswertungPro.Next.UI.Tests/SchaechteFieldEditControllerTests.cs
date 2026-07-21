using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteFieldEditControllerTests
{
    [Fact]
    public void Normales_feld_wird_gesetzt()
    {
        var record = new SchachtRecord();

        var applied = Apply("Bemerkungen", record, "Kontrolliert");

        Assert.True(applied);
        Assert.Equal("Kontrolliert", record.GetFieldValue("Bemerkungen"));
    }

    [Fact]
    public void Schachtnummer_delegiert_alten_und_neuen_wert()
    {
        var record = RecordWith("Schachtnummer", "S-Alt");
        (SchachtRecord Record, string OldValue, string NewValue)? call = null;

        var applied = Apply(
            "Schachtnummer",
            record,
            "S-Neu",
            rename: (item, oldValue, newValue) =>
            {
                call = (item, oldValue, newValue);
                item.SetFieldValue("Schachtnummer", newValue);
                return true;
            });

        Assert.True(applied);
        Assert.Equal((record, "S-Alt", "S-Neu"), call);
        Assert.Equal("S-Neu", record.GetFieldValue("Schachtnummer"));
    }

    [Fact]
    public void Fehlgeschlagene_schachtnummer_aenderung_bricht_ohne_option_oder_mutation_ab()
    {
        var record = RecordWith("Schachtnummer", "S-Alt");
        var optionCalls = 0;

        var applied = Apply(
            "Schachtnummer",
            record,
            "S-Neu",
            rename: (_, _, _) => false,
            ensure: (_, _) => optionCalls++);

        Assert.False(applied);
        Assert.Equal("S-Alt", record.GetFieldValue("Schachtnummer"));
        Assert.Equal(0, optionCalls);
    }

    [Fact]
    public void Verwaltetes_feld_ergaenzt_die_aufgeloeste_option()
    {
        var record = new SchachtRecord();
        (string Field, string? Value)? ensured = null;

        var applied = Apply(
            "Schachtform",
            record,
            "Rund",
            ensure: (field, value) => ensured = (field, value));

        Assert.True(applied);
        Assert.Equal("Rund", record.GetFieldValue("Schachtform"));
        Assert.Equal(("Schachtform", "Rund"), ensured);
    }

    [Fact]
    public void Freies_feld_ergaenzt_keine_option()
    {
        var record = new SchachtRecord();
        var optionCalls = 0;

        Apply("Bemerkungen", record, "Text", ensure: (_, _) => optionCalls++);

        Assert.Equal(0, optionCalls);
    }

    [Fact]
    public void Leerer_wert_loescht_den_bisherigen_feldwert()
    {
        var record = RecordWith("Bemerkungen", "Alt");

        var applied = Apply("Bemerkungen", record, string.Empty);

        Assert.True(applied);
        Assert.Equal(string.Empty, record.GetFieldValue("Bemerkungen"));
    }

    private static bool Apply(
        string fieldName,
        SchachtRecord record,
        string editedValue,
        Func<SchachtRecord, string, string, bool>? rename = null,
        Action<string, string?>? ensure = null)
        => SchaechteFieldEditController.Apply(
            fieldName,
            record,
            editedValue,
            rename ?? ((item, _, newValue) =>
            {
                item.SetFieldValue("Schachtnummer", newValue);
                return true;
            }),
            ensure ?? ((_, _) => { }));

    private static SchachtRecord RecordWith(string fieldName, string value)
    {
        var record = new SchachtRecord();
        record.SetFieldValue(fieldName, value);
        return record;
    }
}
