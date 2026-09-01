using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageCellEditControllerTests
{
    [Fact]
    public void Sanieren_ausschalten_nach_bestaetigung_leert_kosten()
    {
        var record = RecordWith("Sanieren_JaNein", "Ja");
        foreach (var fieldName in SanierungCostFieldMapper.CostFieldNames)
            record.SetFieldValue(fieldName, "123.45", FieldSource.Manual, userEdited: true);
        var ensured = new List<(string Field, string? Value)>();

        var shouldSave = Apply(
            "Sanieren_JaNein",
            record,
            "Nein",
            confirm: true,
            ensure: (field, value) => ensured.Add((field, value)));

        Assert.True(shouldSave);
        Assert.Equal("Nein", record.GetFieldValue("Sanieren_JaNein"));
        Assert.All(
            SanierungCostFieldMapper.CostFieldNames,
            fieldName => Assert.Equal(string.Empty, record.GetFieldValue(fieldName)));
        Assert.Equal(("Sanieren_JaNein", "Nein"), Assert.Single(ensured));
    }

    [Fact]
    public void Sanieren_ausschalten_bei_abbruch_belaesst_ja_und_kosten()
    {
        var record = RecordWith("Sanieren_JaNein", "Ja");
        record.SetFieldValue("Kosten", "123.45", FieldSource.Manual, userEdited: true);
        string? ensuredValue = null;

        var shouldSave = Apply(
            "Sanieren_JaNein",
            record,
            string.Empty,
            confirm: false,
            ensure: (_, value) => ensuredValue = value);

        Assert.True(shouldSave);
        Assert.Equal("Ja", record.GetFieldValue("Sanieren_JaNein"));
        Assert.Equal("123.45", record.GetFieldValue("Kosten"));
        Assert.Equal("Ja", ensuredValue);
    }

    [Theory]
    [InlineData("Eigentuemer")]
    [InlineData("Pruefungsresultat")]
    [InlineData("Referenzpruefung")]
    public void Verwaltetes_feld_schreibt_wert_und_ergaenzt_option(string fieldName)
    {
        var record = new HaltungRecord();
        (string Field, string? Value)? ensured = null;

        var shouldSave = Apply(
            fieldName,
            record,
            "Neu",
            ensure: (field, value) => ensured = (field, value));

        Assert.True(shouldSave);
        Assert.Equal("Neu", record.GetFieldValue(fieldName));
        Assert.Equal((fieldName, "Neu"), ensured);
        Assert.True(record.FieldMeta[fieldName].UserEdited);
    }

    [Fact]
    public void Verwaltetes_feld_ignoriert_leeren_wert_aber_aktualisiert_optionen()
    {
        var record = RecordWith("Eigentuemer", "Bisher");
        var optionCalls = 0;

        Apply(
            "Eigentuemer",
            record,
            "  ",
            ensure: (_, _) => optionCalls++);

        Assert.Equal("Bisher", record.GetFieldValue("Eigentuemer"));
        Assert.Equal(1, optionCalls);
    }

    [Fact]
    public void Zustandsklasse_ohne_neuen_wert_markiert_bisherigen_wert_manuell()
    {
        var record = RecordWith("Zustandsklasse", "3");

        var shouldSave = Apply("Zustandsklasse", record, editedValue: null);

        Assert.True(shouldSave);
        Assert.Equal("3", record.GetFieldValue("Zustandsklasse"));
        Assert.True(record.FieldMeta["Zustandsklasse"].UserEdited);
    }

    [Fact]
    public void Haltungsname_delegiert_umbenennung_und_gibt_abbruch_zurueck()
    {
        var record = RecordWith("Haltungsname", "Alt");
        (string OldValue, string NewValue)? rename = null;

        var shouldSave = Apply(
            "Haltungsname",
            record,
            "Neu",
            rename: (_, oldValue, newValue) =>
            {
                rename = (oldValue, newValue);
                return false;
            });

        Assert.False(shouldSave);
        Assert.Equal(("Alt", "Neu"), rename);
        Assert.Equal("Alt", record.GetFieldValue("Haltungsname"));
    }

    [Fact]
    public void Normales_textfeld_wird_als_manuell_bearbeitet_markiert()
    {
        var record = new HaltungRecord();

        var shouldSave = Apply("Bemerkungen", record, "Kontrolliert");

        Assert.True(shouldSave);
        Assert.Equal("Kontrolliert", record.GetFieldValue("Bemerkungen"));
        Assert.True(record.FieldMeta["Bemerkungen"].UserEdited);
    }

    [Fact]
    public void Fehlender_datensatz_bleibt_ohne_nebenwirkung_speicherbar()
    {
        var optionCalls = 0;
        var renameCalls = 0;

        var shouldSave = Apply(
            "Bemerkungen",
            record: null,
            "Text",
            ensure: (_, _) => optionCalls++,
            rename: (_, _, _) =>
            {
                renameCalls++;
                return true;
            });

        Assert.True(shouldSave);
        Assert.Equal(0, optionCalls);
        Assert.Equal(0, renameCalls);
    }

    [Fact]
    public void Geaenderter_oberer_Schacht_zieht_den_Haltungsnamen_nach()
    {
        var record = Haltung("77565-77564", oben: "77564", unten: "77565");
        var umbenannt = new List<(string Alt, string Neu)>();

        var shouldSave = Apply(
            "Schacht_oben",
            record,
            "77500",
            rename: (item, alt, neu) =>
            {
                umbenannt.Add((alt, neu));
                item.SetFieldValue("Haltungsname", neu, FieldSource.Manual, userEdited: true);
                return true;
            });

        Assert.True(shouldSave);
        Assert.Equal("77500", record.GetFieldValue("Schacht_oben"));
        Assert.Equal(("77565-77564", "77565-77500"), Assert.Single(umbenannt));
        Assert.Equal("77565-77500", record.GetFieldValue("Haltungsname"));
    }

    [Fact]
    public void Geaenderter_unterer_Schacht_zieht_den_Haltungsnamen_nach()
    {
        var record = Haltung("77564-77565", oben: "77564", unten: "77565");
        var umbenannt = new List<(string Alt, string Neu)>();

        Apply(
            "Schacht_unten",
            record,
            "77900",
            rename: (item, alt, neu) =>
            {
                umbenannt.Add((alt, neu));
                item.SetFieldValue("Haltungsname", neu, FieldSource.Manual, userEdited: true);
                return true;
            });

        Assert.Equal(("77564-77565", "77564-77900"), Assert.Single(umbenannt));
    }

    [Fact]
    public void Ein_selbst_vergebener_Haltungsname_wird_beim_Schachtwechsel_nicht_umbenannt()
    {
        var record = Haltung("Jagdmatt West", oben: "77564", unten: "77565");
        var umbenannt = new List<string>();

        Apply(
            "Schacht_oben",
            record,
            "77500",
            rename: (_, _, neu) =>
            {
                umbenannt.Add(neu);
                return true;
            });

        Assert.Empty(umbenannt);
        Assert.Equal("77500", record.GetFieldValue("Schacht_oben"));
        Assert.Equal("Jagdmatt West", record.GetFieldValue("Haltungsname"));
    }

    [Fact]
    public void Ein_gescheitertes_Umbenennen_laesst_den_bisherigen_Haltungsnamen_stehen()
    {
        var record = Haltung("77565-77564", oben: "77564", unten: "77565");

        var shouldSave = Apply(
            "Schacht_oben",
            record,
            "77500",
            rename: (_, _, _) => false);

        Assert.True(shouldSave);
        Assert.Equal("77500", record.GetFieldValue("Schacht_oben"));
        Assert.Equal("77565-77564", record.GetFieldValue("Haltungsname"));
    }

    private static HaltungRecord Haltung(string name, string oben, string unten)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Schacht_oben", oben, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Schacht_unten", unten, FieldSource.Manual, userEdited: false);
        return record;
    }

    private static bool Apply(
        string fieldName,
        HaltungRecord? record,
        string? editedValue,
        bool confirm = true,
        Action<string, string?>? ensure = null,
        Func<HaltungRecord, string, string, bool>? rename = null)
        => DataPageCellEditController.Apply(
            fieldName,
            record,
            editedValue,
            (_, _) => confirm,
            ensure ?? ((_, _) => { }),
            rename ?? ((item, _, newValue) =>
            {
                item.SetFieldValue("Haltungsname", newValue, FieldSource.Manual, userEdited: true);
                return true;
            }));

    private static HaltungRecord RecordWith(string fieldName, string value)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(fieldName, value, FieldSource.Manual, userEdited: false);
        return record;
    }
}
