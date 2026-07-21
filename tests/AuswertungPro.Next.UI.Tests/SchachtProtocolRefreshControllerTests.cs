using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtProtocolRefreshControllerTests
{
    [Fact]
    public void CanExecute_requires_selected_record_with_linked_pdf_path()
    {
        Assert.False(SchachtProtocolRefreshController.CanExecute(null));

        var record = new SchachtRecord();
        Assert.False(SchachtProtocolRefreshController.CanExecute(record));

        record.SetFieldValue("PDF_Path", "   ");
        Assert.False(SchachtProtocolRefreshController.CanExecute(record));

        record.SetFieldValue("PDF_Path", "Schaechte_Verteilt/S-1/protokoll.pdf");
        Assert.True(SchachtProtocolRefreshController.CanExecute(record));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_stops_silently_without_record_or_pdf_path(bool hasRecord)
    {
        var harness = new Harness();
        var record = hasRecord ? new SchachtRecord() : null;

        var outcome = await harness.Controller.ExecuteAsync(record);

        Assert.Equal(
            hasRecord
                ? SchachtProtocolRefreshOutcome.MissingLinkedPdfPath
                : SchachtProtocolRefreshOutcome.MissingSelection,
            outcome);
        Assert.Empty(harness.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_reports_missing_project_before_confirmation()
    {
        var harness = new Harness { ProjectFolder = " " };

        var outcome = await harness.Controller.ExecuteAsync(CreateRecord());

        Assert.Equal(SchachtProtocolRefreshOutcome.MissingProject, outcome);
        Assert.Equal(
            new[]
            {
                "project-folder",
                "info|Aktualisieren|Kein Projekt geoeffnet."
            },
            harness.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_cancel_confirmation_happens_before_path_resolution()
    {
        var harness = new Harness { ConfirmRefresh = false };

        var outcome = await harness.Controller.ExecuteAsync(CreateRecord());

        Assert.Equal(SchachtProtocolRefreshOutcome.Cancelled, outcome);
        Assert.Equal(
            new[]
            {
                "project-folder",
                "project-context",
                "confirm|Aktualisieren|defaultNo=True|Der Schacht wird komplett aus dem Protokoll neu aufgebaut. Von Hand erfasste Werte gehen dabei verloren. Fortfahren?"
            },
            harness.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_warns_when_linked_file_cannot_be_resolved()
    {
        var harness = new Harness { ResolvedPath = null };

        var outcome = await harness.Controller.ExecuteAsync(CreateRecord());

        Assert.Equal(SchachtProtocolRefreshOutcome.LinkedFileMissing, outcome);
        Assert.Equal(
            new[]
            {
                "project-folder",
                "project-context",
                "confirm|Aktualisieren|defaultNo=True|Der Schacht wird komplett aus dem Protokoll neu aufgebaut. Von Hand erfasste Werte gehen dabei verloren. Fortfahren?",
                "resolve|Schaechte_Verteilt/S-1/protokoll.pdf|C:\\Projekt",
                "warn|Aktualisieren|Die verknuepfte Protokoll-Datei wurde nicht gefunden."
            },
            harness.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_read_failure_does_not_check_project_or_mutate_data()
    {
        var harness = new Harness { ReadResult = null };

        var outcome = await harness.Controller.ExecuteAsync(CreateRecord());

        Assert.Equal(SchachtProtocolRefreshOutcome.ReadFailed, outcome);
        Assert.Equal("read|C:\\Projekt\\protokoll.pdf|Aktualisieren", harness.Calls[^1]);
        Assert.DoesNotContain(harness.Calls, call => call.StartsWith("project-still-open|", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Calls, call => call.StartsWith("apply|", StringComparison.Ordinal));
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("save|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_stops_after_read_when_project_changed()
    {
        var harness = new Harness { ProjectChecks = new[] { false } };

        var outcome = await harness.Controller.ExecuteAsync(CreateRecord());

        Assert.Equal(SchachtProtocolRefreshOutcome.ProjectChanged, outcome);
        Assert.Equal(
            "project-still-open|C:\\Projekt\\projekt.json|Aktualisieren|impact=None",
            harness.Calls[^1]);
        Assert.DoesNotContain(harness.Calls, call => call.StartsWith("apply|", StringComparison.Ordinal));
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("save|", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, "S-1", null, "Das verknuepfte PDF ist kein lesbares Schachtprotokoll.")]
    [InlineData(true, "   ", "", "Das verknuepfte PDF ist kein lesbares Schachtprotokoll.")]
    [InlineData(true, null, "Parser-Hinweis", "Parser-Hinweis")]
    public async Task ExecuteAsync_warns_for_invalid_protocol_result(
        bool isProtocol,
        string? shaftNumber,
        string? readHint,
        string expectedWarning)
    {
        var harness = new Harness
        {
            ReadResult = CreateParseResult(isProtocol, shaftNumber, readHint)
        };

        var outcome = await harness.Controller.ExecuteAsync(CreateRecord());

        Assert.Equal(SchachtProtocolRefreshOutcome.InvalidProtocol, outcome);
        Assert.Equal($"warn|Aktualisieren|{expectedWarning}", harness.Calls[^1]);
        Assert.DoesNotContain(harness.Calls, call => call.StartsWith("apply|", StringComparison.Ordinal));
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("save|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_applies_original_record_and_relative_path_in_existing_order()
    {
        var harness = new Harness();
        var record = CreateRecord();
        var before = DateTime.UtcNow;

        var outcome = await harness.Controller.ExecuteAsync(record);
        var after = DateTime.UtcNow;

        Assert.Equal(SchachtProtocolRefreshOutcome.Updated, outcome);
        Assert.Same(record, harness.AppliedRecord);
        Assert.Same(harness.ReadResult, harness.AppliedResult);
        Assert.Equal("Schaechte_Verteilt/S-1/protokoll.pdf", harness.AppliedPath);
        Assert.Equal(
            new[]
            {
                "project-folder",
                "project-context",
                "confirm|Aktualisieren|defaultNo=True|Der Schacht wird komplett aus dem Protokoll neu aufgebaut. Von Hand erfasste Werte gehen dabei verloren. Fortfahren?",
                "resolve|Schaechte_Verteilt/S-1/protokoll.pdf|C:\\Projekt",
                "read|C:\\Projekt\\protokoll.pdf|Aktualisieren",
                "project-still-open|C:\\Projekt\\projekt.json|Aktualisieren|impact=None",
                "apply|Schaechte_Verteilt/S-1/protokoll.pdf",
                "project-still-open|C:\\Projekt\\projekt.json|Aktualisieren|impact=ProjectDataChanged",
                "save|dirty=True|modified=True",
                "last-result|Schacht S-1 aktualisiert (1 Beobachtungen)."
            },
            harness.Calls);
        Assert.True(harness.Project.Dirty);
        Assert.Equal(DateTimeKind.Utc, harness.Project.ModifiedAtUtc.Kind);
        Assert.InRange(harness.Project.ModifiedAtUtc, before, after);
    }

    [Fact]
    public async Task ExecuteAsync_keeps_existing_success_result_when_save_returns_false()
    {
        var harness = new Harness { SaveResult = false };

        var outcome = await harness.Controller.ExecuteAsync(CreateRecord());

        Assert.Equal(SchachtProtocolRefreshOutcome.Updated, outcome);
        Assert.Contains("save|dirty=True|modified=True", harness.Calls);
        Assert.Equal(
            "last-result|Schacht S-1 aktualisiert (1 Beobachtungen).",
            harness.Calls[^1]);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_turn_apply_failure_into_success()
    {
        var harness = new Harness { ApplyException = new InvalidOperationException("kaputt") };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Controller.ExecuteAsync(CreateRecord()));

        Assert.Equal("kaputt", exception.Message);
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("save|", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Calls, call => call.StartsWith("last-result|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_project_change_after_apply_keeps_dirty_commit_but_stops_save()
    {
        var harness = new Harness { ProjectChecks = new[] { true, false } };

        var outcome = await harness.Controller.ExecuteAsync(CreateRecord());

        Assert.Equal(SchachtProtocolRefreshOutcome.UpdatedButNotSaved, outcome);
        Assert.Contains(
            "apply|Schaechte_Verteilt/S-1/protokoll.pdf",
            harness.Calls);
        Assert.True(harness.Project.Dirty);
        Assert.NotEqual(DateTime.UnixEpoch, harness.Project.ModifiedAtUtc);
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("save|", StringComparison.Ordinal));
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("last-result|", StringComparison.Ordinal));
    }

    private static SchachtRecord CreateRecord()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("PDF_Path", "Schaechte_Verteilt/S-1/protokoll.pdf");
        return record;
    }

    private static SchachtProtocolParseResult CreateParseResult(
        bool isProtocol = true,
        string? shaftNumber = "S-1",
        string? readHint = null)
        => new(
            isProtocol,
            shaftNumber,
            Datum: null,
            Funktion: null,
            Schachtform: null,
            Dimension: null,
            Schachttiefe: null,
            PrimaereSchaeden: null,
            Bemerkungen: null,
            Status: null,
            Link: null,
            Schaeden: new[] { (Bauteil: "Deckel", Schaden: "Riss") },
            readHint);

    private sealed class Harness
    {
        private readonly DialogFake _dialogs;
        private int _projectCheckIndex;

        internal Harness()
        {
            _dialogs = new DialogFake(Calls, () => ConfirmRefresh);
            Actions = new SchachtProtocolRefreshActions(
                GetProjectFolder: () =>
                {
                    Calls.Add("project-folder");
                    return ProjectFolder;
                },
                CaptureProject: () =>
                {
                    Calls.Add("project-context");
                    return new ProjectOperationContext(Project, ProjectPath);
                },
                ResolveLinkedFile: (relativePath, projectFolder) =>
                {
                    Calls.Add($"resolve|{relativePath}|{projectFolder}");
                    return ResolvedPath;
                },
                ReadProtocolAsync: (absolutePath, title) =>
                {
                    Calls.Add($"read|{absolutePath}|{title}");
                    return Task.FromResult<SchachtProtocolParseResult?>(ReadResult);
                },
                ProjectIsStillOpen: (projectContext, title, impact) =>
                {
                    Assert.Same(Project, projectContext.Project);
                    Assert.Equal(ProjectPath, projectContext.ProjectPath);
                    Calls.Add(
                        $"project-still-open|{projectContext.ProjectPath}|{title}|impact={impact}");
                    var index = Math.Min(_projectCheckIndex, ProjectChecks.Count - 1);
                    var result = ProjectChecks[index];
                    _projectCheckIndex++;
                    return result;
                },
                Apply: (record, result, relativePath) =>
                {
                    Calls.Add($"apply|{relativePath}");
                    if (ApplyException is not null)
                        throw ApplyException;

                    AppliedRecord = record;
                    AppliedResult = result;
                    AppliedPath = relativePath;
                },
                SaveProject: () =>
                {
                    Calls.Add(
                        $"save|dirty={Project.Dirty}|modified={Project.ModifiedAtUtc != DateTime.UnixEpoch}");
                    return SaveResult;
                },
                SetLastResult: value => Calls.Add($"last-result|{value}"));
            Controller = new SchachtProtocolRefreshController(_dialogs, Actions);
        }

        internal List<string> Calls { get; } = new();
        internal SchachtProtocolRefreshController Controller { get; }
        internal SchachtProtocolRefreshActions Actions { get; }
        internal bool ConfirmRefresh { get; init; } = true;
        internal string? ProjectFolder { get; init; } = "C:\\Projekt";
        internal string? ProjectPath { get; init; } = "C:\\Projekt\\projekt.json";
        internal string? ResolvedPath { get; init; } = "C:\\Projekt\\protokoll.pdf";
        internal SchachtProtocolParseResult? ReadResult { get; init; } = CreateParseResult();
        internal IReadOnlyList<bool> ProjectChecks { get; init; } = new[] { true, true };
        internal Project Project { get; } = new() { ModifiedAtUtc = DateTime.UnixEpoch };
        internal bool SaveResult { get; init; } = true;
        internal Exception? ApplyException { get; init; }
        internal SchachtRecord? AppliedRecord { get; private set; }
        internal SchachtProtocolParseResult? AppliedResult { get; private set; }
        internal string? AppliedPath { get; private set; }
    }

    private sealed class DialogFake : IDialogService
    {
        private readonly ICollection<string> _calls;
        private readonly Func<bool> _confirmWarn;

        internal DialogFake(ICollection<string> calls, Func<bool> confirmWarn)
        {
            _calls = calls;
            _confirmWarn = confirmWarn;
        }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") => _calls.Add($"info|{title}|{message}");
        public void Warn(string message, string title = "Warnung") => _calls.Add($"warn|{title}|{message}");
        public void Error(string message, string title = "Fehler") => _calls.Add($"error|{title}|{message}");
        public bool Confirm(string message, string title = "Bestaetigung") => false;

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
        {
            _calls.Add($"confirm|{title}|defaultNo={defaultNo}|{message}");
            return _confirmWarn();
        }

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => DialogConfirm.Cancel;
    }
}
