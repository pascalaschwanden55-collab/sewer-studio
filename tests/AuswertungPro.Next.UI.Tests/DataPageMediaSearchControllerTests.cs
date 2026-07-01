using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageMediaSearchControllerTests
{
    [Fact]
    public void Open_meldet_leere_liste_ohne_dialog()
    {
        var statuses = new List<string>();
        var shown = 0;
        var controller = CreateController(
            records: Array.Empty<HaltungRecord>(),
            showMediaSearch: (_, _) =>
            {
                shown++;
                return null;
            },
            setStatus: statuses.Add);

        controller.Open();

        Assert.Equal("Keine Haltungen vorhanden.", statuses.Single());
        Assert.Equal(0, shown);
    }

    [Fact]
    public void Open_nutzt_quellordner_vor_legacy_ordner()
    {
        var records = new[] { new HaltungRecord() };
        var shown = new List<(IReadOnlyList<HaltungRecord> Records, string? InitialFolder)>();
        var controller = CreateController(
            records,
            lastVideoSourceFolder: "C:\\Quelle",
            lastVideoFolder: "C:\\Legacy",
            showMediaSearch: (passedRecords, initial) =>
            {
                shown.Add((passedRecords, initial));
                return null;
            });

        controller.Open();

        var show = Assert.Single(shown);
        Assert.Same(records, show.Records);
        Assert.Equal("C:\\Quelle", show.InitialFolder);
    }

    [Fact]
    public void Open_nutzt_legacy_ordner_wenn_quellordner_leer_ist()
    {
        var shown = new List<string?>();
        var controller = CreateController(
            new[] { new HaltungRecord() },
            lastVideoSourceFolder: "",
            lastVideoFolder: "C:\\Legacy",
            showMediaSearch: (_, initial) =>
            {
                shown.Add(initial);
                return null;
            });

        controller.Open();

        Assert.Equal("C:\\Legacy", shown.Single());
    }

    [Fact]
    public void Open_ohne_angewendetes_ergebnis_setzt_keine_aenderungen()
    {
        var dirty = 0;
        var refreshed = 0;
        var statuses = new List<string>();
        var controller = CreateController(
            new[] { new HaltungRecord() },
            showMediaSearch: (_, _) => new DataPageMediaSearchResult(false, 1, 2, 3),
            markProjectDirty: () => dirty++,
            notifyRecordsChanged: () => refreshed++,
            setStatus: statuses.Add);

        controller.Open();

        Assert.Equal(0, dirty);
        Assert.Equal(0, refreshed);
        Assert.Empty(statuses);
    }

    [Fact]
    public void Open_mit_angewendetem_ergebnis_markiert_dirty_refresh_und_status()
    {
        var dirty = 0;
        var refreshed = 0;
        var statuses = new List<string>();
        var controller = CreateController(
            new[] { new HaltungRecord() },
            showMediaSearch: (_, _) => new DataPageMediaSearchResult(true, 4, 2, 7),
            markProjectDirty: () => dirty++,
            notifyRecordsChanged: () => refreshed++,
            setStatus: statuses.Add);

        controller.Open();

        Assert.Equal(1, dirty);
        Assert.Equal(1, refreshed);
        Assert.Equal("Medien verlinkt: 4 Videos, 2 PDFs, 7 Fotos", statuses.Single());
    }

    private static DataPageMediaSearchController CreateController(
        IReadOnlyList<HaltungRecord> records,
        string? lastVideoSourceFolder = null,
        string? lastVideoFolder = null,
        Func<IReadOnlyList<HaltungRecord>, string?, DataPageMediaSearchResult?>? showMediaSearch = null,
        Action? markProjectDirty = null,
        Action? notifyRecordsChanged = null,
        Action<string>? setStatus = null)
        => new(
            getRecords: () => records,
            getLastVideoSourceFolder: () => lastVideoSourceFolder,
            getLastVideoFolder: () => lastVideoFolder,
            showMediaSearch ?? ((_, _) => null),
            markProjectDirty ?? (() => { }),
            notifyRecordsChanged ?? (() => { }),
            setStatus ?? (_ => { }));
}
