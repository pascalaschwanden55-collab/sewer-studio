using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageDetailItemFactoryTests
{
    // Ohne Feldschluessel laesst sich keine persoenliche Kartenreihenfolge speichern:
    // die Beschriftung ist dafuer nicht eindeutig genug.
    [Theory]
    [InlineData("Sanieren_JaNein")]  // gepflegte Auswahlliste
    [InlineData("Zustandsklasse")]   // Auswahlliste aus dem Katalog
    [InlineData("Bemerkungen")]      // freies Textfeld
    public void Create_traegt_den_Feldnamen_als_Schluessel(string fieldName)
    {
        var factory = new DataPageDetailItemFactory(
            name => name == "Sanieren_JaNein"
                ? new DataPageManagedComboSpec(new[] { "Ja", "Nein" }, AllowFreeText: true)
                : null,
            (_, _, _) => { });

        var item = factory.Create(fieldName, new HaltungRecord());

        Assert.Equal(fieldName, item.FieldName);
    }

    [Fact]
    public void Create_nutzt_managed_combo_spec_vor_katalog_combo()
    {
        var editCommand = new TestCommand();
        var previewCommand = new TestCommand();
        var resetCommand = new TestCommand();
        var addCommand = new TestCommand();
        var removeCommand = new TestCommand();
        var factory = new DataPageDetailItemFactory(
            fieldName => fieldName == "Sanieren_JaNein"
                ? new DataPageManagedComboSpec(
                    new[] { "Ja", "Nein" },
                    AllowFreeText: true,
                    EditOptionsCommand: editCommand,
                    PreviewOptionsCommand: previewCommand,
                    ResetOptionsCommand: resetCommand,
                    AddOptionCommand: addCommand,
                    RemoveOptionCommand: removeCommand)
                : null,
            (_, _, _) => { });
        var record = new HaltungRecord();
        record.SetFieldValue("Sanieren_JaNein", "Ja", FieldSource.Manual, userEdited: true);

        var item = factory.Create("Sanieren_JaNein", record);

        Assert.Equal("Sanieren Ja/Nein", item.Label);
        Assert.Equal("Ja", item.Value);
        Assert.True(item.IsCombo);
        Assert.True(item.AllowFreeText);
        Assert.Equal(RecordDetailHighlightKind.Sanieren, item.HighlightKind);
        Assert.Equal(new[] { "Ja", "Nein" }, item.Options);
        Assert.Same(editCommand, item.EditOptionsCommand);
        Assert.Same(previewCommand, item.PreviewOptionsCommand);
        Assert.Same(resetCommand, item.ResetOptionsCommand);
        Assert.Same(addCommand, item.AddOptionCommand);
        Assert.Same(removeCommand, item.RemoveOptionCommand);
    }

    [Fact]
    public void Create_nutzt_catalog_combo_wenn_kein_managed_spec_vorhanden_ist()
    {
        var factory = new DataPageDetailItemFactory(_ => null, (_, _, _) => { });
        var record = new HaltungRecord();
        record.SetFieldValue("Nutzungsart", "Mischabwasser", FieldSource.Manual, userEdited: true);

        var item = factory.Create("Nutzungsart", record);

        Assert.Equal("Nutzungsart", item.Label);
        Assert.Equal("Mischabwasser", item.Value);
        Assert.True(item.IsCombo);
        Assert.False(item.AllowFreeText);
        Assert.Equal(FieldCatalog.GetComboItems("Nutzungsart"), item.Options);
    }

    [Fact]
    public void Create_markiert_mehrzeilige_und_int_felder()
    {
        var factory = new DataPageDetailItemFactory(_ => null, (_, _, _) => { });
        var record = new HaltungRecord();

        var multiline = factory.Create("Primaere_Schaeden", record);
        var integer = factory.Create("NR", record);

        Assert.True(multiline.IsMultiline);
        Assert.False(multiline.DigitsOnly);
        Assert.False(integer.IsMultiline);
        Assert.True(integer.DigitsOnly);
    }

    [Fact]
    public void Create_committet_aenderungen_ueber_callback()
    {
        var commits = new List<(HaltungRecord Record, string FieldName, string Value)>();
        var factory = new DataPageDetailItemFactory(_ => null, (record, fieldName, value) =>
            commits.Add((record, fieldName, value)));
        var record = new HaltungRecord();

        var item = factory.Create("Bemerkungen", record);
        item.Value = "neu";

        var commit = Assert.Single(commits);
        Assert.Same(record, commit.Record);
        Assert.Equal("Bemerkungen", commit.FieldName);
        Assert.Equal("neu", commit.Value);
    }

    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
