using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageCostRestoreControllerTests
{
    [Fact]
    public void Restore_nutzt_selected_fallback_und_warnt_bei_fehlendem_haltungsnamen()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            selected: new HaltungRecord(),
            loadStore: _ => throw new InvalidOperationException("store should not load"));

        controller.Restore(null);

        Assert.Equal(("Haltungsname fehlt in der Zeile.", "Kosten/Massnahmen"), dialogs.LastWarn);
        Assert.Null(dialogs.LastInfo);
    }

    [Fact]
    public void Restore_meldet_fehlenden_projektpfad()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            projectPath: "",
            loadStore: _ => throw new InvalidOperationException("store should not load"));

        controller.Restore(Record("H1"));

        Assert.Equal(("Projekt bitte zuerst speichern/oeffnen, um Kosten wiederherzustellen.", "Kosten/Massnahmen"), dialogs.LastInfo);
    }

    [Fact]
    public void Restore_meldet_fehlende_gespeicherte_kosten_mit_store_pfad()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            projectPath: "C:\\Projekt\\projekt.json",
            loadStore: _ => new ProjectCostStore(),
            getStorePath: dir =>
            {
                Assert.Equal("C:\\Projekt", dir);
                return "C:\\Projekt\\costs\\costs.json";
            });

        controller.Restore(Record("H1"));

        Assert.Equal(
            ("Keine gespeicherten Kosten/Massnahmen gefunden fuer:\nH1\n\nDatei:\nC:\\Projekt\\costs\\costs.json", "Kosten/Massnahmen"),
            dialogs.LastInfo);
    }

    [Fact]
    public void Restore_wendet_kosten_an_und_setzt_status()
    {
        var record = Record(" H1 ");
        var cost = new HoldingCost { Holding = "H1", Total = 123m };
        var applied = new List<(HaltungRecord Record, HoldingCost Cost)>();
        var statuses = new List<string>();
        var store = new ProjectCostStore
        {
            ByHolding = new Dictionary<string, HoldingCost>(StringComparer.OrdinalIgnoreCase)
            {
                ["H1"] = cost
            }
        };
        var controller = CreateController(
            new CapturingDialogService(),
            projectPath: "C:\\Projekt\\projekt.json",
            loadStore: path =>
            {
                Assert.Equal("C:\\Projekt\\projekt.json", path);
                return store;
            },
            applyCosts: (r, c) => applied.Add((r, c)),
            setStatus: statuses.Add);

        controller.Restore(record);

        var appliedItem = Assert.Single(applied);
        Assert.Same(record, appliedItem.Record);
        Assert.Same(cost, appliedItem.Cost);
        Assert.Equal("Kosten/Maßnahmen wiederhergestellt: H1", statuses.Single());
    }

    private static DataPageCostRestoreController CreateController(
        CapturingDialogService dialogs,
        HaltungRecord? selected = null,
        string? projectPath = "C:\\Projekt\\projekt.json",
        Func<string, ProjectCostStore>? loadStore = null,
        Func<string, string>? getStorePath = null,
        Action<HaltungRecord, HoldingCost>? applyCosts = null,
        Action<string>? setStatus = null)
        => new(
            dialogs,
            getSelected: () => selected,
            getProjectPath: () => projectPath,
            loadStore ?? (_ => new ProjectCostStore()),
            getStorePath ?? DefaultGetStorePath,
            applyCosts ?? ((_, _) => { }),
            setStatus ?? (_ => { }));

    private static string DefaultGetStorePath(string dir)
        => Path.Combine(dir, "costs", "costs.json");

    private static HaltungRecord Record(string holding)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: false);
        return record;
    }

    private sealed class CapturingDialogService : IDialogService
    {
        public (string Message, string Title)? LastInfo { get; private set; }
        public (string Message, string Title)? LastWarn { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => throw new NotSupportedException();

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => throw new NotSupportedException();

        public string[] OpenFiles(string title, string filter)
            => throw new NotSupportedException();

        public string? SelectFolder(string title, string? initialPath = null)
            => throw new NotSupportedException();

        public void Info(string message, string title = "Hinweis")
            => LastInfo = (message, title);

        public void Warn(string message, string title = "Warnung")
            => LastWarn = (message, title);

        public void Error(string message, string title = "Fehler")
            => throw new NotSupportedException();

        public bool Confirm(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
            => throw new NotSupportedException();

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();
    }
}
