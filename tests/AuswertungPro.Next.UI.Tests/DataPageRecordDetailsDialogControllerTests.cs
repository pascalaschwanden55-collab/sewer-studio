using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRecordDetailsDialogControllerTests
{
    [Fact]
    public void Build_erstellt_titel_header_gruppen_und_suggest_command_fuer_haltung()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "12.034-12.035", FieldSource.Manual, userEdited: true);
        var groups = new[]
        {
            new RecordDetailGroup("Stammdaten", "Beschreibung", Array.Empty<RecordDetailItem>())
        };
        var command = new TestCommand();
        var controller = new DataPageRecordDetailsDialogController(
            candidate =>
            {
                Assert.Same(record, candidate);
                return groups;
            },
            candidate =>
            {
                Assert.Same(record, candidate);
                return command;
            });

        var request = controller.Build(record);

        Assert.Equal("Haltungsdetails - 12.034-12.035", request.Title);
        Assert.Equal("Haltung 12.034-12.035", request.Header);
        Assert.Equal("Komplette Zeile in Spaltenreihenfolge der Haltungs-Ansicht.", request.SubHeader);
        Assert.Same(groups, request.Groups);
        Assert.Same(command, request.SuggestMeasuresCommand);
    }

    [Fact]
    public void Build_nutzt_neutralen_titel_wenn_haltungsname_fehlt()
    {
        var controller = new DataPageRecordDetailsDialogController(
            _ => Array.Empty<RecordDetailGroup>(),
            _ => null);

        var request = controller.Build(new HaltungRecord());

        Assert.Equal("Haltungsdetails", request.Title);
        Assert.Equal("Haltungsdetails", request.Header);
        Assert.Equal("Komplette Zeile in Spaltenreihenfolge der Haltungs-Ansicht.", request.SubHeader);
        Assert.Empty(request.Groups);
        Assert.Null(request.SuggestMeasuresCommand);
    }

    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
