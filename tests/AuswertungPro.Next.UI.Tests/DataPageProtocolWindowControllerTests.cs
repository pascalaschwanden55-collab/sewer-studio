using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageProtocolWindowControllerTests
{
    [Theory]
    [InlineData("Projektwechsel")]
    [InlineData("Loeschen")]
    [InlineData("Videowechsel")]
    [InlineData("Eintrag_loeschen")]
    public async Task OpenAsync_verwirft_veraltetes_Ziel_nach_der_Pfadauflosung(string fall)
    {
        var p = new Project();
        var h = Record("video.mp4");
        var eintrag = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry();
        h.Protocol = new() { Current = new() { Entries = [eintrag] } };
        p.Data.Add(h);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var shown = 0;
        var controller = new DataPageProtocolWindowController(() => p, () => "C:/eins/projekt.json",
            path => { started.TrySetResult(); release.Wait(TimeSpan.FromSeconds(5)); return path; },
            _ => shown++, () => { }, _ => { }, _ => { });
        var task = controller.OpenAsync(h, eintrag.EntryId);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            switch (fall)
            {
                case "Projektwechsel": p = new Project(); break;
                case "Loeschen": p.Data.Remove(h); break;
                case "Videowechsel": h.SetFieldValue("Link", "neu.mp4", FieldSource.Manual, true); break;
                case "Eintrag_loeschen": eintrag.IsDeleted = true; break;
            }
        }
        finally { release.Set(); }
        Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, shown);
    }

    [Fact]
    public void Open_uebergibt_das_genaue_Sprungziel_an_das_Protokollfenster()
    {
        var id = Guid.NewGuid();
        DataPageProtocolWindowRequest? request = null;
        var controller = CreateController(showProtocolWindow: r => request = r);
        controller.Open(new HaltungRecord(), id);
        Assert.Equal(id, request!.EintragId);
    }

    [Fact]
    public void Open_ignoriert_null_record()
    {
        var shown = new List<DataPageProtocolWindowRequest>();
        var synced = new List<HaltungRecord>();
        var refreshed = new List<HaltungRecord>();
        var controller = CreateController(
            showProtocolWindow: shown.Add,
            syncObservations: synced.Add,
            refreshIfSelected: refreshed.Add);

        controller.Open(null);

        Assert.Empty(shown);
        Assert.Empty(synced);
        Assert.Empty(refreshed);
    }

    [Fact]
    public void Open_baut_request_mit_project_folder_und_resolved_video_path()
    {
        var record = Record("C:\\Videos\\haltung.mp4");
        var project = new Project();
        var shown = new List<DataPageProtocolWindowRequest>();
        var controller = CreateController(
            project: project,
            projectPath: "C:\\Projekt\\projekt.json",
            resolveExistingPath: raw =>
            {
                Assert.Equal("C:\\Videos\\haltung.mp4", raw);
                return "C:\\Resolved\\haltung.mp4";
            },
            showProtocolWindow: shown.Add);

        controller.Open(record);

        var request = Assert.Single(shown);
        Assert.Same(record, request.Record);
        Assert.Same(project, request.Project);
        Assert.Equal("C:\\Resolved\\haltung.mp4", request.ResolvedVideoPath);
        Assert.Equal("C:\\Projekt", request.ProjectFolder);
    }

    [Fact]
    public void Open_request_mark_dirty_ruft_dirty_callback()
    {
        var dirty = 0;
        DataPageProtocolWindowRequest? captured = null;
        var controller = CreateController(
            markDirty: () => dirty++,
            showProtocolWindow: request => captured = request);

        controller.Open(new HaltungRecord());

        Assert.NotNull(captured);
        captured!.MarkDirty();
        Assert.Equal(1, dirty);
    }

    [Fact]
    public void Open_ruft_nach_dialog_sync_und_refresh()
    {
        var record = new HaltungRecord();
        var events = new List<string>();
        var controller = CreateController(
            showProtocolWindow: _ => events.Add("show"),
            syncObservations: r =>
            {
                Assert.Same(record, r);
                events.Add("sync");
            },
            refreshIfSelected: r =>
            {
                Assert.Same(record, r);
                events.Add("refresh");
            });

        controller.Open(record);

        Assert.Equal(new[] { "show", "sync", "refresh" }, events);
    }

    private static DataPageProtocolWindowController CreateController(
        Project? project = null,
        string? projectPath = null,
        Func<string?, string?>? resolveExistingPath = null,
        Action<DataPageProtocolWindowRequest>? showProtocolWindow = null,
        Action? markDirty = null,
        Action<HaltungRecord>? syncObservations = null,
        Action<HaltungRecord>? refreshIfSelected = null)
        => new(
            getProject: () => project ?? new Project(),
            getLastProjectPath: () => projectPath,
            resolveExistingPath ?? (raw => raw),
            showProtocolWindow ?? (_ => { }),
            markDirty ?? (() => { }),
            syncObservations ?? (_ => { }),
            refreshIfSelected ?? (_ => { }));

    private static HaltungRecord Record(string link)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Link", link, FieldSource.Manual, userEdited: false);
        return record;
    }
}
