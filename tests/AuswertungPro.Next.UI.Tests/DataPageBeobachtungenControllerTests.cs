using System.Collections.ObjectModel;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageBeobachtungenControllerTests
{
    [Fact]
    public void BuildOpenRequest_meldet_fehlende_zeile_ohne_request()
    {
        var infos = new List<(string Message, string Title)>();
        var controller = new DataPageBeobachtungenController(
            (message, title) => infos.Add((message, title)),
            (_, _) => throw new InvalidOperationException("Keine Warnung erwartet."),
            _ => throw new InvalidOperationException("Keine VSA-Auswertung erwartet."));

        var request = controller.BuildOpenRequest(
            record: null,
            new ObservableCollection<ProtocolEntry>(),
            new TestCommand(),
            _ => throw new InvalidOperationException("Keine Auswahl erwartet."),
            () => throw new InvalidOperationException("Kein Refresh erwartet."),
            (_, _) => throw new InvalidOperationException("Kein Sync erwartet."));

        Assert.Null(request);
        Assert.Equal((DataPageRecordCommandRouter.MissingSelectionMessage, "Beobachtungen"), Assert.Single(infos));
    }

    [Fact]
    public void BuildOpenRequest_selektiert_record_und_baut_fenster_request()
    {
        var entries = new ObservableCollection<ProtocolEntry> { new() { Code = "BAJA" } };
        var command = new TestCommand();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "12.034-12.035", FieldSource.Manual, userEdited: true);
        HaltungRecord? selected = null;
        var controller = CreateController();

        var request = controller.BuildOpenRequest(
            record,
            entries,
            command,
            value => selected = value,
            () => { },
            (_, _) => { });

        Assert.NotNull(request);
        Assert.Same(record, selected);
        Assert.Same(entries, request.Entries);
        Assert.Same(command, request.OpenProtocolCommand);
        Assert.Same(record, request.Record);
        Assert.Equal("12.034-12.035", request.HoldingName);
        Assert.NotNull(request.VsaUpdateAction);
        Assert.NotNull(request.SyncHoldingFieldsAction);
    }

    [Fact]
    public void VsaUpdateAction_refreshes_record_und_meldet_erfolg()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-1", FieldSource.Manual, userEdited: true);
        var refreshed = 0;
        var infos = new List<(string Message, string Title)>();
        var controller = new DataPageBeobachtungenController(
            (message, title) => infos.Add((message, title)),
            (_, _) => throw new InvalidOperationException("Keine Warnung erwartet."),
            candidate =>
            {
                Assert.Same(record, candidate);
                return new DataPageBeobachtungenVsaResult(Ok: true, ErrorMessage: null);
            });

        var request = controller.BuildOpenRequest(
            record,
            new ObservableCollection<ProtocolEntry>(),
            new TestCommand(),
            _ => { },
            () => refreshed++,
            (_, _) => { });

        request!.VsaUpdateAction();

        Assert.Equal(1, refreshed);
        Assert.Equal(("VSA Zustand aktualisiert f\u00fcr H-1.", "VSA"), Assert.Single(infos));
    }

    [Fact]
    public void VsaUpdateAction_meldet_fehler_ohne_refresh()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-1", FieldSource.Manual, userEdited: true);
        var refreshed = 0;
        var warnings = new List<(string Message, string Title)>();
        var controller = new DataPageBeobachtungenController(
            (_, _) => throw new InvalidOperationException("Keine Info erwartet."),
            (message, title) => warnings.Add((message, title)),
            _ => new DataPageBeobachtungenVsaResult(Ok: false, ErrorMessage: "ungueltig"));

        var request = controller.BuildOpenRequest(
            record,
            new ObservableCollection<ProtocolEntry>(),
            new TestCommand(),
            _ => { },
            () => refreshed++,
            (_, _) => { });

        request!.VsaUpdateAction();

        Assert.Equal(0, refreshed);
        Assert.Equal(("VSA Fehler: ungueltig", "VSA"), Assert.Single(warnings));
    }

    [Fact]
    public void SyncHoldingFieldsAction_synchronisiert_mit_status()
    {
        var record = new HaltungRecord();
        var syncCalls = new List<(HaltungRecord Record, bool ShowStatus)>();
        var controller = CreateController();

        var request = controller.BuildOpenRequest(
            record,
            new ObservableCollection<ProtocolEntry>(),
            new TestCommand(),
            _ => { },
            () => { },
            (candidate, showStatus) => syncCalls.Add((candidate, showStatus)));

        request!.SyncHoldingFieldsAction();

        var call = Assert.Single(syncCalls);
        Assert.Same(record, call.Record);
        Assert.True(call.ShowStatus);
    }

    private static DataPageBeobachtungenController CreateController()
        => new(
            (_, _) => { },
            (_, _) => { },
            _ => null);

    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
