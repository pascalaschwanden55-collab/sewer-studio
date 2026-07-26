using System.IO;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtProtocolSingleImportControllerTests
{
    [Fact]
    public async Task ExecuteAsync_read_null_stops_before_project_check()
    {
        var harness = new Harness { ReadResult = null };

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal(new[] { "read|quelle.pdf|Protokoll importieren" }, harness.Calls);
        Assert.Null(harness.Service.AppliedTarget);
        Assert.Empty(harness.Project.SchaechteData);
    }

    [Fact]
    public async Task ExecuteAsync_project_change_after_read_precedes_protocol_validation()
    {
        var harness = new Harness
        {
            ReadResult = CreateParseResult(isProtocol: false),
            ProjectChecks = new[] { false }
        };

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal(
            new[]
            {
                "read|quelle.pdf|Protokoll importieren",
                "project-check|C:\\Projekt\\projekt.json|Protokoll importieren|False|impact=None"
            },
            harness.Calls);
        Assert.Empty(harness.Warnings);
        Assert.Null(harness.Service.LastFindNumber);
    }

    [Theory]
    [InlineData(null, "Das gewaehlte PDF ist kein Schachtprotokoll.")]
    [InlineData("", "Das gewaehlte PDF ist kein Schachtprotokoll.")]
    [InlineData("   ", "Das gewaehlte PDF ist kein Schachtprotokoll.")]
    [InlineData("Parser-Hinweis", "Parser-Hinweis")]
    public async Task ExecuteAsync_invalid_protocol_uses_read_hint_or_fallback(
        string? readHint,
        string expectedWarning)
    {
        var harness = new Harness
        {
            ReadResult = CreateParseResult(isProtocol: false, readHint: readHint)
        };

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal((expectedWarning, "Protokoll importieren"), Assert.Single(harness.Warnings));
        Assert.Null(harness.Service.LastFindNumber);
        Assert.DoesNotContain(harness.Calls, call => call.StartsWith("distribute|", StringComparison.Ordinal));
        AssertNoCommitSideEffects(harness);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_protocol_without_shaft_number_has_distinct_warning(
        string? shaftNumber)
    {
        var harness = new Harness
        {
            ReadResult = CreateParseResult(shaftNumber: shaftNumber)
        };

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal(
            ("Im Protokoll wurde keine Schachtnummer gefunden.", "Protokoll importieren"),
            Assert.Single(harness.Warnings));
        Assert.Null(harness.Service.LastFindNumber);
        AssertNoCommitSideEffects(harness);
    }

    [Fact]
    public async Task ExecuteAsync_new_target_preserves_order_arguments_lock_and_utc_time()
    {
        var harness = new Harness();
        var before = DateTime.UtcNow;

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");
        var after = DateTime.UtcNow;

        Assert.Equal(
            new[]
            {
                "read|quelle.pdf|Protokoll importieren",
                "project-check|C:\\Projekt\\projekt.json|Protokoll importieren|True|impact=None",
                "find|S-1",
                "last-result|Schacht S-1: PDF wird ins Projekt kopiert ...",
                "distribute|C:\\Projekt|S-1|quelle.pdf",
                "project-check|C:\\Projekt\\projekt.json|Protokoll importieren|True|impact=ProjectFilesWritten",
                "apply|Schaechte_Verteilt/S-1/protokoll.pdf",
                "collection-add|lock=True",
                "project-check|C:\\Projekt\\projekt.json|Protokoll importieren|True|impact=ProjectFilesWritten, ProjectDataChanged",
                "selected|lock=False",
                "project-check|C:\\Projekt\\projekt.json|Protokoll importieren|True|impact=ProjectFilesWritten, ProjectDataChanged",
                "save|dirty=True|modified=True",
                "last-result|Protokoll importiert: Schacht S-1 (1 Beobachtungen)."
            },
            harness.Calls);
        Assert.Same(harness.ReadResult, harness.Service.AppliedResult);
        Assert.Equal("Schaechte_Verteilt/S-1/protokoll.pdf", harness.Service.AppliedPath);
        Assert.Same(harness.Service.AppliedTarget, harness.Selected);
        Assert.Same(harness.Selected, Assert.Single(harness.Project.SchaechteData));
        Assert.True(harness.Project.Dirty);
        Assert.Equal(DateTimeKind.Utc, harness.Project.ModifiedAtUtc.Kind);
        Assert.InRange(harness.Project.ModifiedAtUtc, before, after);
    }

    [Fact]
    public async Task ExecuteAsync_existing_target_yes_overwrites_same_record_without_add()
    {
        var harness = new Harness();
        var existing = new SchachtRecord();
        harness.Project.SchaechteData.Add(existing);
        harness.Calls.Clear();
        harness.Service.FindResult = existing;
        harness.CollisionChoice = DialogConfirm.Yes;
        var before = DateTime.UtcNow;

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");
        var after = DateTime.UtcNow;

        Assert.Same(existing, harness.Service.AppliedTarget);
        Assert.Same(existing, harness.Selected);
        Assert.Single(harness.Project.SchaechteData);
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("collection-add|", StringComparison.Ordinal));
        AssertCollisionDialog(harness);
        AssertCompletedImport(harness, before, after, expectedDamageCount: 1);
    }

    [Fact]
    public async Task ExecuteAsync_existing_target_no_creates_and_adds_new_record()
    {
        var harness = new Harness();
        var existing = new SchachtRecord();
        harness.Project.SchaechteData.Add(existing);
        harness.Calls.Clear();
        harness.Service.FindResult = existing;
        harness.CollisionChoice = DialogConfirm.No;
        var before = DateTime.UtcNow;

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");
        var after = DateTime.UtcNow;

        Assert.NotSame(existing, harness.Service.AppliedTarget);
        Assert.Same(harness.Service.AppliedTarget, harness.Selected);
        Assert.Equal(2, harness.Project.SchaechteData.Count);
        Assert.True(
            harness.Calls.IndexOf("apply|Schaechte_Verteilt/S-1/protokoll.pdf")
            < harness.Calls.IndexOf("collection-add|lock=True"));
        AssertCollisionDialog(harness);
        AssertCompletedImport(harness, before, after, expectedDamageCount: 1);
    }

    [Fact]
    public async Task ExecuteAsync_existing_target_cancel_stops_before_copy()
    {
        var harness = new Harness();
        harness.Service.FindResult = new SchachtRecord();
        harness.CollisionChoice = DialogConfirm.Cancel;

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal("confirm-cancel", harness.Calls[^1]);
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("distribute|", StringComparison.Ordinal));
        Assert.Null(harness.Service.AppliedTarget);
        Assert.Null(harness.Selected);
        AssertCollisionDialog(harness);
    }

    [Fact]
    public async Task ExecuteAsync_found_target_outside_current_records_is_added_after_apply()
    {
        var harness = new Harness();
        var found = new SchachtRecord();
        harness.Service.FindResult = found;
        harness.CollisionChoice = DialogConfirm.Yes;

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Same(found, harness.Service.AppliedTarget);
        Assert.Same(found, Assert.Single(harness.Project.SchaechteData));
        Assert.True(
            harness.Calls.IndexOf("apply|Schaechte_Verteilt/S-1/protokoll.pdf")
            < harness.Calls.IndexOf("collection-add|lock=True"));
    }

    [Fact]
    public async Task ExecuteAsync_copy_failure_preserves_failure_text_and_stops_mutation()
    {
        var harness = new Harness();
        harness.Service.DistributeException = new UserFacingException("Kopieren ging nicht.");

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal(
            "last-result|Protokoll konnte nicht kopiert werden.",
            harness.Calls[^2]);
        Assert.Equal("warn", harness.Calls[^1]);
        Assert.Equal(
            ("Das PDF konnte nicht ins Projekt kopiert werden:\nKopieren ging nicht.", "Protokoll importieren"),
            Assert.Single(harness.Warnings));
        Assert.Null(harness.Service.AppliedTarget);
        Assert.Empty(harness.Project.SchaechteData);
        Assert.Null(harness.Selected);
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("save|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_copy_failure_catches_ordinary_technical_exception()
    {
        var harness = new Harness();
        harness.Service.DistributeException = new InvalidOperationException("technisch kaputt");

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal("last-result|Protokoll konnte nicht kopiert werden.", harness.Calls[^2]);
        Assert.Equal(
            (
                "Das PDF konnte nicht ins Projekt kopiert werden:\n" +
                "Der Vorgang konnte nicht abgeschlossen werden. Technische Details stehen im Programmlog.",
                "Protokoll importieren"),
            Assert.Single(harness.Warnings));
        AssertNoCommitSideEffects(harness);
    }

    [Fact]
    public async Task ExecuteAsync_project_change_after_copy_stops_before_apply()
    {
        var harness = new Harness { ProjectChecks = new[] { true, false } };

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal(
            "project-check|C:\\Projekt\\projekt.json|Protokoll importieren|False|impact=ProjectFilesWritten",
            harness.Calls[^1]);
        Assert.NotNull(harness.Service.LastDistributedSource);
        Assert.Null(harness.Service.AppliedTarget);
        Assert.Empty(harness.Project.SchaechteData);
        Assert.Null(harness.Selected);
    }

    [Fact]
    public async Task ExecuteAsync_existing_project_file_does_not_report_new_file_write()
    {
        var harness = new Harness { ProjectChecks = new[] { true, false } };
        harness.Service.DistributedFileCreated = false;

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal(
            "project-check|C:\\Projekt\\projekt.json|Protokoll importieren|False|impact=None",
            harness.Calls[^1]);
        Assert.Null(harness.Service.AppliedTarget);
        Assert.Empty(harness.Project.SchaechteData);
    }

    [Fact]
    public async Task ExecuteAsync_legacy_import_service_reports_conservative_file_impact()
    {
        var dialogHarness = new Harness();
        var project = new Project();
        var impacts = new List<ProjectOperationImpact>();
        var service = new LegacyOnlyProtocolImportFake();
        var controller = new SchachtProtocolSingleImportController(
            new DialogFake(dialogHarness),
            service,
            new SchachtProtocolSingleImportActions(
                ReadProtocolAsync: (_, _) => Task.FromResult<SchachtProtocolParseResult?>(CreateParseResult()),
                ProjectIsStillOpen: (_, _, impact) =>
                {
                    impacts.Add(impact);
                    return impacts.Count == 1;
                },
                CollectionLock: new object(),
                SaveProject: () => throw new InvalidOperationException("Save darf nicht laufen."),
                SetSelected: _ => throw new InvalidOperationException("Auswahl darf nicht laufen."),
                ClearSelectedIfSame: _ => { },
                SetLastResult: _ => { }));

        await controller.ExecuteAsync(
            new ProjectOperationContext(project, "C:\\Projekt\\projekt.json"),
            "C:\\Projekt",
            "quelle.pdf");

        Assert.Equal(
            new[]
            {
                ProjectOperationImpact.None,
                ProjectOperationImpact.ProjectFilesWritten
            },
            impacts);
        Assert.Equal(1, service.DistributeCalls);
        Assert.Equal(0, service.ApplyCalls);
    }

    [Fact]
    public async Task ExecuteAsync_apply_failure_propagates_without_success_side_effects()
    {
        var harness = new Harness();
        harness.Service.ApplyException = new InvalidOperationException("apply kaputt");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Controller.ExecuteAsync(
                harness.ProjectContext,
                "C:\\Projekt",
                "quelle.pdf"));

        Assert.Equal("apply kaputt", exception.Message);
        Assert.Empty(harness.Project.SchaechteData);
        Assert.Null(harness.Selected);
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("save|", StringComparison.Ordinal));
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("last-result|Protokoll importiert:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_project_change_after_selection_keeps_dirty_commit_but_stops_save()
    {
        var harness = new Harness
        {
            ProjectChecks = new[] { true, true, true, false }
        };

        await harness.Controller.ExecuteAsync(
            harness.ProjectContext,
            "C:\\Projekt",
            "quelle.pdf");

        Assert.Null(harness.Selected);
        Assert.Contains("clear-selected|same=True", harness.Calls);
        Assert.Single(harness.Project.SchaechteData);
        Assert.True(harness.Project.Dirty);
        Assert.NotEqual(DateTime.UnixEpoch, harness.Project.ModifiedAtUtc);
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("save|", StringComparison.Ordinal));
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("last-result|Protokoll importiert:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_save_false_reports_imported_but_not_saved()
    {
        var harness = new Harness { SaveResult = false };

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Contains("save|dirty=True|modified=True", harness.Calls);
        Assert.Contains(
            "last-result|Protokoll uebernommen, aber nicht gespeichert: Schacht S-1 (1 Beobachtungen).",
            harness.Calls);
        Assert.Contains(
            harness.Warnings,
            warning => warning.Message.Contains(
                "uebernommen, aber nicht gespeichert",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_save_exception_reports_imported_but_not_saved()
    {
        var harness = new Harness
        {
            SaveException = new IOException("Datentraeger voll")
        };

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Contains(
            harness.Warnings,
            warning => warning.Message.Contains(
                "uebernommen, aber nicht gespeichert",
                StringComparison.Ordinal));
        Assert.Contains("nicht gespeichert", harness.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_forwards_raw_nonempty_values_without_normalizing()
    {
        var harness = new Harness
        {
            ReadResult = CreateParseResult(shaftNumber: " S-1 ")
        };
        harness.Service.DistributedPath = " rel / path ";

        await harness.Controller.ExecuteAsync(
            harness.ProjectContext,
            " C:\\Projekt ",
            "  quelle.pdf  ");

        Assert.Equal(" S-1 ", harness.Service.LastFindNumber);
        Assert.Equal(" C:\\Projekt ", harness.Service.LastDistributedProjectFolder);
        Assert.Equal(" S-1 ", harness.Service.LastDistributedShaftNumber);
        Assert.Equal("  quelle.pdf  ", harness.Service.LastDistributedSource);
        Assert.Equal(" rel / path ", harness.Service.AppliedPath);
        Assert.Equal(
            "last-result|Protokoll importiert: Schacht  S-1  (1 Beobachtungen).",
            harness.Calls[^1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task ExecuteAsync_success_text_uses_actual_damage_count(int damageCount)
    {
        var harness = new Harness
        {
            ReadResult = CreateParseResult(damageCount: damageCount)
        };

        await harness.Controller.ExecuteAsync(harness.ProjectContext, "C:\\Projekt", "quelle.pdf");

        Assert.Equal(
            $"last-result|Protokoll importiert: Schacht S-1 ({damageCount} Beobachtungen).",
            harness.Calls[^1]);
    }

    private static void AssertNoCommitSideEffects(Harness harness)
    {
        Assert.Null(harness.Service.AppliedTarget);
        Assert.Empty(harness.Project.SchaechteData);
        Assert.Null(harness.Selected);
        Assert.False(harness.Project.Dirty);
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("save|", StringComparison.Ordinal));
        Assert.DoesNotContain(
            harness.Calls,
            call => call.StartsWith("last-result|Protokoll importiert:", StringComparison.Ordinal));
    }

    private static void AssertCompletedImport(
        Harness harness,
        DateTime before,
        DateTime after,
        int expectedDamageCount)
    {
        Assert.True(harness.Project.Dirty);
        Assert.Equal(DateTimeKind.Utc, harness.Project.ModifiedAtUtc.Kind);
        Assert.InRange(harness.Project.ModifiedAtUtc, before, after);
        Assert.Single(
            harness.Calls.Where(call => call.StartsWith("save|", StringComparison.Ordinal)));
        Assert.Equal(
            $"last-result|Protokoll importiert: Schacht S-1 ({expectedDamageCount} Beobachtungen).",
            harness.Calls[^1]);
    }

    private static void AssertCollisionDialog(Harness harness)
    {
        var expected =
            "Schacht S-1 ist bereits vorhanden.\n\n" +
            "Ja = Ueberschreiben\nNein = Als neuen Schacht anlegen\nAbbrechen = Nichts tun";
        Assert.Equal((expected, "Protokoll importieren"), Assert.Single(harness.ConfirmCancelCalls));
    }

    private static SchachtProtocolParseResult CreateParseResult(
        bool isProtocol = true,
        string? shaftNumber = "S-1",
        string? readHint = null,
        int damageCount = 1)
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
            Schaeden: Enumerable.Range(0, damageCount)
                .Select(index => (Bauteil: $"Bauteil {index}", Schaden: $"Schaden {index}"))
                .ToArray(),
            readHint);

    private sealed class Harness
    {
        private int _projectCheckIndex;

        internal Harness()
        {
            Project.SchaechteData.CollectionChanged += (_, _) =>
                Calls.Add($"collection-add|lock={Monitor.IsEntered(CollectionLock)}");
            Service = new ProtocolImportFake(this);
            var dialogs = new DialogFake(this);
            var actions = new SchachtProtocolSingleImportActions(
                ReadProtocolAsync: (pdfPath, title) =>
                {
                    Calls.Add($"read|{pdfPath}|{title}");
                    return Task.FromResult<SchachtProtocolParseResult?>(ReadResult);
                },
                ProjectIsStillOpen: (projectContext, title, impact) =>
                {
                    Assert.Same(Project, projectContext.Project);
                    Assert.Equal(ProjectPath, projectContext.ProjectPath);
                    var index = Math.Min(_projectCheckIndex, ProjectChecks.Count - 1);
                    var result = ProjectChecks[index];
                    _projectCheckIndex++;
                    Calls.Add(
                        $"project-check|{projectContext.ProjectPath}|{title}|{result}|impact={impact}");
                    return result;
                },
                CollectionLock,
                SaveProject: () =>
                {
                    Calls.Add(
                        $"save|dirty={Project.Dirty}|modified={Project.ModifiedAtUtc != DateTime.UnixEpoch}");
                    if (SaveException is not null)
                        throw SaveException;
                    return SaveResult;
                },
                SetSelected: record =>
                {
                    Calls.Add($"selected|lock={Monitor.IsEntered(CollectionLock)}");
                    Selected = record;
                },
                ClearSelectedIfSame: record =>
                {
                    Calls.Add($"clear-selected|same={ReferenceEquals(Selected, record)}");
                    if (ReferenceEquals(Selected, record))
                        Selected = null;
                },
                SetLastResult: value =>
                {
                    Calls.Add($"last-result|{value}");
                    LastResult = value;
                });
            Controller = new SchachtProtocolSingleImportController(dialogs, Service, actions);
        }

        internal List<string> Calls { get; } = new();
        internal List<(string Message, string Title)> Warnings { get; } = new();
        internal List<(string Message, string Title)> ConfirmCancelCalls { get; } = new();
        internal object CollectionLock { get; } = new();
        internal Project Project { get; } = new() { ModifiedAtUtc = DateTime.UnixEpoch };
        internal string ProjectPath { get; } = "C:\\Projekt\\projekt.json";
        internal ProjectOperationContext ProjectContext => new(Project, ProjectPath);
        internal ProtocolImportFake Service { get; }
        internal SchachtProtocolSingleImportController Controller { get; }
        internal SchachtProtocolParseResult? ReadResult { get; init; } = CreateParseResult();
        internal IReadOnlyList<bool> ProjectChecks { get; init; } =
            new[] { true, true, true, true };
        internal DialogConfirm CollisionChoice { get; set; } = DialogConfirm.Yes;
        internal bool SaveResult { get; init; } = true;
        internal Exception? SaveException { get; init; }
        internal SchachtRecord? Selected { get; private set; }
        internal string? LastResult { get; private set; }
    }

    private sealed class ProtocolImportFake :
        ISchachtProtocolImportService,
        ISchachtProtocolDistributionResultService
    {
        private readonly Harness _harness;

        internal ProtocolImportFake(Harness harness) => _harness = harness;

        internal SchachtRecord? FindResult { get; set; }
        internal Exception? DistributeException { get; set; }
        internal Exception? ApplyException { get; set; }
        internal string DistributedPath { get; set; } = "Schaechte_Verteilt/S-1/protokoll.pdf";
        internal bool DistributedFileCreated { get; set; } = true;
        internal string? LastFindNumber { get; private set; }
        internal string? LastDistributedProjectFolder { get; private set; }
        internal string? LastDistributedShaftNumber { get; private set; }
        internal string? LastDistributedSource { get; private set; }
        internal SchachtRecord? AppliedTarget { get; private set; }
        internal SchachtProtocolParseResult? AppliedResult { get; private set; }
        internal string? AppliedPath { get; private set; }

        public SchachtProtocolParseResult Parse(string pdfPfad)
            => throw new InvalidOperationException("Der Controller muss den gebundenen Leseweg verwenden.");

        public SchachtRecord? FindSchacht(Project project, string? schachtnummer)
        {
            Assert.Same(_harness.Project, project);
            LastFindNumber = schachtnummer;
            _harness.Calls.Add($"find|{schachtnummer}");
            return FindResult;
        }

        public void Apply(
            SchachtRecord ziel,
            SchachtProtocolParseResult ergebnis,
            string pdfPfadFuerFeld)
        {
            _harness.Calls.Add($"apply|{pdfPfadFuerFeld}");
            if (ApplyException is not null)
                throw ApplyException;

            AppliedTarget = ziel;
            AppliedResult = ergebnis;
            AppliedPath = pdfPfadFuerFeld;
        }

        public string DistributePdf(
            string projektOrdner,
            string schachtnummer,
            string pdfQuelle)
            => DistributedPath;

        public SchachtProtocolDistributionResult DistributePdfWithResult(
            string projektOrdner,
            string schachtnummer,
            string pdfQuelle)
        {
            LastDistributedProjectFolder = projektOrdner;
            LastDistributedShaftNumber = schachtnummer;
            LastDistributedSource = pdfQuelle;
            _harness.Calls.Add($"distribute|{projektOrdner}|{schachtnummer}|{pdfQuelle}");
            if (DistributeException is not null)
                throw DistributeException;

            return new SchachtProtocolDistributionResult(
                DistributedPath,
                DistributedFileCreated);
        }
    }

    private sealed class LegacyOnlyProtocolImportFake : ISchachtProtocolImportService
    {
        internal int DistributeCalls { get; private set; }
        internal int ApplyCalls { get; private set; }

        public SchachtProtocolParseResult Parse(string pdfPfad)
            => throw new NotSupportedException();

        public SchachtRecord? FindSchacht(Project project, string? schachtnummer)
            => null;

        public void Apply(
            SchachtRecord ziel,
            SchachtProtocolParseResult ergebnis,
            string pdfPfadFuerFeld)
            => ApplyCalls++;

        public string DistributePdf(
            string projektOrdner,
            string schachtnummer,
            string pdfQuelle)
        {
            DistributeCalls++;
            return "Schaechte_Verteilt/L-1/legacy.pdf";
        }
    }

    private sealed class DialogFake : IDialogService
    {
        private readonly Harness _harness;

        internal DialogFake(Harness harness) => _harness = harness;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }

        public void Warn(string message, string title = "Warnung")
        {
            _harness.Calls.Add("warn");
            _harness.Warnings.Add((message, title));
        }

        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
        {
            _harness.Calls.Add("confirm-cancel");
            _harness.ConfirmCancelCalls.Add((message, title));
            return _harness.CollisionChoice;
        }
    }
}
