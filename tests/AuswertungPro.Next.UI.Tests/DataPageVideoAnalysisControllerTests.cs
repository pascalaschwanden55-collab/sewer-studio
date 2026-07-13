using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using System.Net.Http;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoAnalysisControllerTests
{
    [Fact]
    public void Open_ignoriert_null_record_ohne_pfad_oder_dialog()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            ensureVideoPath: _ => throw new InvalidOperationException("path should not be requested"));

        controller.Open(null);

        Assert.Null(dialogs.LastInfo);
        Assert.Null(dialogs.LastWarn);
    }

    [Fact]
    public void Open_bricht_bei_leerem_video_pfad_ab()
    {
        var dialogs = new CapturingDialogService();
        var shown = false;
        var controller = CreateController(
            dialogs,
            ensureVideoPath: _ => "",
            showPipelineWindow: (_, _) =>
            {
                shown = true;
                return null;
            });

        controller.Open(new HaltungRecord());

        Assert.False(shown);
        Assert.Null(dialogs.LastInfo);
        Assert.Null(dialogs.LastWarn);
    }

    [Fact]
    public void Open_warnt_wenn_code_katalog_leer_ist()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            getAllowedCodes: () => Array.Empty<string>());

        controller.Open(new HaltungRecord());

        Assert.Equal(("VSA-Code-Katalog ist leer oder nicht geladen.", "Videoanalyse KI"), dialogs.LastWarn);
    }

    [Fact]
    public void Open_informiert_wenn_ki_deaktiviert_ist()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            loadRuntimeSettings: () => Settings(enabled: false));

        controller.Open(new HaltungRecord());

        Assert.Equal(("KI ist deaktiviert (SEWERSTUDIO_AI_ENABLED=0).", "Videoanalyse KI"), dialogs.LastInfo);
    }

    [Fact]
    public void Open_verwendet_http_client_fuer_weitere_analysen_wieder()
    {
        var clients = new List<HttpClient>();
        using var controller = CreateController(
            new CapturingDialogService(),
            createPipeline: (_, _, http) =>
            {
                clients.Add(http);
                return new NoopPipeline();
            });

        controller.Open(new HaltungRecord());
        controller.Open(new HaltungRecord());

        Assert.Equal(2, clients.Count);
        Assert.Same(clients[0], clients[1]);
    }

    [Fact]
    public async Task Dispose_gibt_http_client_frei_und_ist_wiederholbar()
    {
        HttpClient? client = null;
        var controller = CreateController(
            new CapturingDialogService(),
            createPipeline: (_, _, http) =>
            {
                client = http;
                return new NoopPipeline();
            });
        controller.Open(new HaltungRecord());

        controller.Dispose();
        controller.Dispose();

        Assert.NotNull(client);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await client!.GetAsync("http://127.0.0.1:1"));
    }

    [Fact]
    public void Open_baut_pipeline_request_und_uebernimmt_erfolgreiches_protokoll()
    {
        var dialogs = new CapturingDialogService();
        var record = Record("H-01", length: "12,5");
        var incoming = DocumentWithEntry("BAA", ProtocolEntrySource.Ai);
        PipelineRequest? capturedRequest = null;
        var markedDirty = new List<HaltungRecord>();
        var refreshed = new List<HaltungRecord>();
        var refreshSelected = 0;
        var autosaves = 0;
        var controller = CreateController(
            dialogs,
            showPipelineWindow: (request, _) =>
            {
                capturedRequest = request;
                return SuccessfulResult(incoming);
            },
            isSelected: r => ReferenceEquals(r, record),
            markProjectDirty: markedDirty.Add,
            refreshRecordInGrid: refreshed.Add,
            refreshSelectedProtocolEntries: () => refreshSelected++,
            scheduleAutoSave: () => autosaves++);

        controller.Open(record);

        Assert.NotNull(capturedRequest);
        Assert.Equal("H-01", capturedRequest!.HaltungId);
        Assert.Equal("C:\\Videos\\haltung.mp4", capturedRequest.VideoPath);
        Assert.Equal(new[] { "BAA", "BAB" }, capturedRequest.AllowedCodes);
        Assert.Equal(12.5, capturedRequest.ReachLengthM);
        Assert.Equal(incoming.Current.Entries[0].Code, record.Protocol?.Current.Entries[0].Code);
        Assert.Same(record, Assert.Single(markedDirty));
        Assert.Same(record, Assert.Single(refreshed));
        Assert.Equal(1, refreshSelected);
        Assert.Equal(1, autosaves);
    }

    [Fact]
    public void Open_belaesst_manuelles_protokoll_wenn_user_reanalyse_ablehnt()
    {
        var dialogs = new CapturingDialogService { ConfirmResult = false };
        var existing = DocumentWithEntry("MAN", ProtocolEntrySource.Manual);
        var incoming = DocumentWithEntry("AI", ProtocolEntrySource.Ai);
        var record = Record("H-01", length: "5");
        record.Protocol = existing;
        var dirty = 0;
        var controller = CreateController(
            dialogs,
            showPipelineWindow: (_, _) => SuccessfulResult(incoming),
            markProjectDirty: _ => dirty++);

        controller.Open(record);

        Assert.Same(existing, record.Protocol);
        Assert.Equal(0, dirty);
        Assert.Equal("KI-Reanalyse", dialogs.LastConfirm?.Title);
    }

    [Fact]
    public void TryStartByName_validiert_sucht_und_plant_analyse()
    {
        var dialogs = new CapturingDialogService();
        var record = Record("H-01", length: "7");
        var scheduled = new List<Action>();
        var opened = 0;
        var controller = CreateController(
            dialogs,
            records: new[] { record },
            beginInvoke: scheduled.Add,
            showPipelineWindow: (_, _) =>
            {
                opened++;
                return null;
            });

        var blank = controller.TryStartByName(" ");
        var missing = controller.TryStartByName("X");
        var found = controller.TryStartByName(" h-01 ");

        Assert.False(blank.Ok);
        Assert.Equal("Haltungsname fehlt.", blank.Message);
        Assert.False(missing.Ok);
        Assert.Contains("nicht im geladenen Projekt gefunden", missing.Message);
        Assert.True(found.Ok);
        Assert.Equal("KI-Videoanalyse fuer 'h-01' gestartet.", found.Message);

        var action = Assert.Single(scheduled);
        action();
        Assert.Equal(1, opened);
    }

    private static DataPageVideoAnalysisController CreateController(
        CapturingDialogService dialogs,
        IReadOnlyList<HaltungRecord>? records = null,
        Func<HaltungRecord, string?>? ensureVideoPath = null,
        Func<IReadOnlyList<string>?>? getAllowedCodes = null,
        Func<AiRuntimeSettings>? loadRuntimeSettings = null,
        Func<AiRuntimeSettings, IAiSuggestionPlausibilityService, HttpClient, IVideoAnalysisPipelineService>? createPipeline = null,
        Func<PipelineRequest, IVideoAnalysisPipelineService, PipelineResult?>? showPipelineWindow = null,
        Func<HaltungRecord, bool>? isSelected = null,
        Action<HaltungRecord>? markProjectDirty = null,
        Action<HaltungRecord>? refreshRecordInGrid = null,
        Action? refreshSelectedProtocolEntries = null,
        Action? scheduleAutoSave = null,
        Action<Action>? beginInvoke = null)
        => new(
            dialogs,
            getRecords: () => records ?? Array.Empty<HaltungRecord>(),
            ensureVideoPath ?? (_ => "C:\\Videos\\haltung.mp4"),
            getAllowedCodes ?? (() => new[] { "BAA", "BAB" }),
            loadRuntimeSettings ?? (() => Settings(enabled: true)),
            createPipeline ?? ((_, _, _) => new NoopPipeline()),
            showPipelineWindow ?? ((_, _) => null),
            isSelected ?? (_ => false),
            markProjectDirty ?? (_ => { }),
            refreshRecordInGrid ?? (_ => { }),
            refreshSelectedProtocolEntries ?? (() => { }),
            scheduleAutoSave ?? (() => { }),
            beginInvoke ?? (action => action()));

    private static HaltungRecord Record(string name, string? length)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        if (length is not null)
            record.SetFieldValue("Haltungslaenge_m", length, FieldSource.Manual, userEdited: false);
        return record;
    }

    private static ProtocolDocument DocumentWithEntry(string code, ProtocolEntrySource source)
        => new()
        {
            Current = new ProtocolRevision
            {
                Entries =
                {
                    new ProtocolEntry
                    {
                        Code = code,
                        Source = source
                    }
                }
            }
        };

    private static PipelineResult SuccessfulResult(ProtocolDocument document)
        => new(
            document,
            Array.Empty<RawVideoDetection>(),
            Array.Empty<MappedProtocolEntry>(),
            null,
            Array.Empty<string>(),
            Error: null);

    private static AiRuntimeSettings Settings(bool enabled)
        => new(
            enabled,
            new Uri("http://localhost:11434"),
            "vision",
            "text",
            null,
            null,
            TimeSpan.FromSeconds(30),
            "5m",
            4096);

    private sealed class NoopPipeline : IVideoAnalysisPipelineService
    {
        public Task<PipelineResult> RunAsync(
            PipelineRequest request,
            IProgress<PipelineProgress>? progress = null,
            CancellationToken ct = default)
            => Task.FromResult(PipelineResult.Failed("not used"));
    }

    private sealed class CapturingDialogService : IDialogService
    {
        public (string Message, string Title)? LastInfo { get; private set; }
        public (string Message, string Title)? LastWarn { get; private set; }
        public (string Message, string Title)? LastConfirm { get; private set; }
        public bool ConfirmResult { get; set; } = true;

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
        {
            LastConfirm = (message, title);
            return ConfirmResult;
        }

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
            => throw new NotSupportedException();

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();
    }
}
