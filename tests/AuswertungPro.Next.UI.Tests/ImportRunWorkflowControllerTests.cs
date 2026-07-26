using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ImportRunWorkflowControllerTests
{
    [Theory]
    [InlineData("PDF")]
    [InlineData("XTF")]
    [InlineData("WinCan")]
    [InlineData("IBAK")]
    [InlineData("KINS")]
    public async Task RunAsync_AllManualImportTypesCreateRestorePointBeforeImport(string label)
    {
        var project = new Project();
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            label,
            "source",
            (_, _, _) =>
            {
                calls.Add($"import:{label}");
                return Result<ImportStats>.Success(
                    new ImportStats(0, 0, 0, 0, 0, Array.Empty<string>()));
            });

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls),
            CancellationToken.None);

        Assert.Equal($"restore:{label}", calls[0]);
        Assert.Equal($"import:{label}", calls[1]);
    }

    [Fact]
    public async Task RunAsync_commit_success_runs_post_processing_save_and_report()
    {
        var project = new Project();
        var calls = new List<string>();
        var state = new UiState();

        var request = new ImportRunWorkflowRequest<string>(
            Label: "PDF",
            Source: "source.pdf",
            Import: (source, target, ctx) =>
            {
                calls.Add($"import:{source}:{ReferenceEquals(project, target)}:{ctx.DryRun}");
                ctx.Log.AddEntry("PDF", "Import", ImportLogStatus.Created, recordKey: "H1", field: "Haltungsname");
                return Result<ImportStats>.Success(new ImportStats(
                    Found: 2,
                    Created: 1,
                    Updated: 1,
                    Errors: 0,
                    Uncertain: 0,
                    Messages: new[] { "m1", "m2" }));
            },
            PostImportAsync: (_, target, ctx) =>
            {
                calls.Add($"post:{ctx.DryRun}:{ReferenceEquals(project, target)}");
                target.Metadata["PostImport"] = "behalten";
                return Task.CompletedTask;
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                project,
                state,
                calls,
                validatePlausibility: _ => new[] { "Warnung 1" }),
            CancellationToken.None);

        Assert.Contains("import:source.pdf:False:False", calls);
        Assert.Contains("restore:PDF", calls);
        Assert.Contains("post:False:False", calls);
        Assert.Contains("dedup", calls);
        Assert.Contains("after:PDF", calls);
        Assert.Contains("replace", calls);
        Assert.Contains("save", calls);
        Assert.Contains("report:PDF:False", calls);
        Assert.Equal("import-report.txt", state.LastReportPath);
        Assert.Equal(100, state.ProgressPercent);
        Assert.False(state.IsImportInProgress);
        Assert.False(state.CanCancel);
        Assert.Equal("", state.Phase);
        Assert.Contains("PDF Import:", state.Summary);
        Assert.Contains("Plausibilitaet: 1 Warnung", state.Summary);
        Assert.Contains("m1", state.Details);
        Assert.Contains("Warnung 1", state.Details);
        Assert.Equal("PDF importiert", state.Statuses[^1]);
        Assert.NotNull(state.ReplacedProject);
        Assert.True(state.ReplacedProject!.Dirty);
        Assert.Equal("behalten", state.ReplacedProject.Metadata["PostImport"]);
    }

    [Fact]
    public async Task RunAsync_failed_import_stops_before_post_processing_and_save()
    {
        var project = new Project();
        var calls = new List<string>();
        var state = new UiState();

        var request = new ImportRunWorkflowRequest<string>(
            Label: "XTF",
            Source: "broken.xtf",
            Import: (_, _, _) => Result<ImportStats>.Fail("X", "kaputt"),
            PostImportAsync: (_, _, _) =>
            {
                calls.Add("post");
                return Task.CompletedTask;
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls),
            CancellationToken.None);

        Assert.Equal(["restore:XTF", "report:XTF:False"], calls);
        Assert.Equal("XTF Import fehlgeschlagen - Projektdaten wurden nicht uebernommen: kaputt", state.Summary);
        Assert.Equal("XTF Import fehlgeschlagen - Projektdaten wurden nicht uebernommen", state.Statuses[^1]);
        Assert.Null(state.ReplacedProject);
        Assert.False(state.IsImportInProgress);
        Assert.False(state.CanCancel);
        Assert.Equal("import-report.txt", state.LastReportPath);
        Assert.Equal("", state.Phase);
    }

    [Fact]
    public async Task RunAsync_dry_run_uses_project_copy_and_confirmed_preview_runs_commit()
    {
        var project = new Project { Name = "Live" };
        var previewProject = new Project { Name = "Preview" };
        var calls = new List<string>();
        var state = new UiState();

        var request = new ImportRunWorkflowRequest<string>(
            Label: "WinCan",
            Source: "folder",
            Import: (_, target, ctx) =>
            {
                calls.Add($"import:{target.Name}:{ctx.DryRun}");
                ctx.Log.AddEntry(
                    "WinCan",
                    "Import",
                    ctx.DryRun ? ImportLogStatus.Created : ImportLogStatus.Updated,
                    recordKey: "H1");
                return Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, Array.Empty<string>()));
            },
            DryRun: true,
            PostImportAsync: (_, _, ctx) =>
            {
                calls.Add($"post:{ctx.DryRun}");
                return Task.CompletedTask;
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                project,
                state,
                calls,
                deepCopyProject: _ => previewProject,
                showPreview: (_, label) =>
                {
                    calls.Add($"preview:{label}");
                    return true;
                }),
            CancellationToken.None);

        Assert.Equal(
            [
                "import:Preview:True",
                "preview:WinCan",
                "restore:WinCan",
                "import:Preview:False",
                "post:False",
                "dedup",
                "after:WinCan",
                "replace",
                "save",
                "report:WinCan:False",
                "report:WinCan:True"
            ],
            calls);
        Assert.False(state.IsImportInProgress);
        Assert.False(state.CanCancel);
        Assert.Equal("import-report.txt", state.LastReportPath);
    }

    [Fact]
    public async Task RunAsync_cancel_after_mutation_keeps_live_project_unchanged()
    {
        var project = new Project { Name = "Live" };
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            "PDF",
            "source.pdf",
            (_, target, _) =>
            {
                target.Data.Add(new HaltungRecord());
                throw new OperationCanceledException();
            });

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls),
            CancellationToken.None);

        Assert.Empty(project.Data);
        Assert.Null(state.ReplacedProject);
        Assert.DoesNotContain("replace", calls);
        Assert.Contains("Projektdaten wurden nicht uebernommen", state.Summary);
    }

    [Fact]
    public async Task RunAsync_exception_after_mutation_keeps_live_project_unchanged()
    {
        var project = new Project { Name = "Live" };
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            "XTF",
            "source.xtf",
            (_, target, _) =>
            {
                target.Data.Add(new HaltungRecord());
                throw new InvalidOperationException("Testfehler");
            });

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls),
            CancellationToken.None);

        Assert.Empty(project.Data);
        Assert.Null(state.ReplacedProject);
        Assert.DoesNotContain("replace", calls);
        Assert.Contains("Projektdaten wurden nicht uebernommen", state.Summary);
    }

    [Fact]
    public async Task RunAsync_project_switch_during_import_discards_result_before_post_processing()
    {
        var originalProject = new Project { Name = "Original" };
        var openedProject = new Project { Name = "Spaeter geoeffnet" };
        var activeProject = originalProject;
        var activePath = @"C:\Projekte\Original\projekt.json";
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "PDF",
            Source: "source.pdf",
            Import: (_, _, _) =>
            {
                calls.Add("import");
                activeProject = openedProject;
                activePath = @"C:\Projekte\Neu\projekt.json";
                return Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, []));
            },
            PostImportAsync: (_, _, _) =>
            {
                calls.Add("post");
                return Task.CompletedTask;
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                originalProject,
                state,
                calls,
                getProject: () => activeProject,
                getProjectPath: () => activePath),
            CancellationToken.None);

        Assert.DoesNotContain("post", calls);
        Assert.DoesNotContain("dedup", calls);
        Assert.DoesNotContain("replace", calls);
        Assert.DoesNotContain("save", calls);
        Assert.Null(state.ReplacedProject);
        Assert.Same(openedProject, activeProject);
        Assert.Contains("Projekt wurde gewechselt", state.Summary);
        Assert.Contains("nicht uebernommen", state.Summary);
    }

    [Fact]
    public async Task RunAsync_project_path_change_discards_result_without_saving()
    {
        var project = new Project { Name = "Gleiches Objekt" };
        var activePath = @"C:\Projekte\Original\projekt.json";
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            "XTF",
            "source.xtf",
            (_, _, _) =>
            {
                activePath = @"C:\Projekte\SpeichernUnter\projekt.json";
                return Result<ImportStats>.Success(new ImportStats(1, 0, 1, 0, 0, []));
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                project,
                state,
                calls,
                getProjectPath: () => activePath),
            CancellationToken.None);

        Assert.DoesNotContain("replace", calls);
        Assert.DoesNotContain("save", calls);
        Assert.Null(state.ReplacedProject);
        Assert.Contains("Projekt wurde gewechselt", state.Summary);
    }

    [Fact]
    public async Task RunAsync_project_switch_in_post_processing_stops_before_commit()
    {
        var originalProject = new Project { Name = "Original" };
        var openedProject = new Project { Name = "Spaeter geoeffnet" };
        var activeProject = originalProject;
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "WinCan",
            Source: "folder",
            Import: (_, _, _) => Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, [])),
            PostImportAsync: (_, _, _) =>
            {
                calls.Add("post");
                activeProject = openedProject;
                return Task.CompletedTask;
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                originalProject,
                state,
                calls,
                getProject: () => activeProject),
            CancellationToken.None);

        Assert.Contains("post", calls);
        Assert.DoesNotContain("dedup", calls);
        Assert.DoesNotContain("after:WinCan", calls);
        Assert.DoesNotContain("replace", calls);
        Assert.DoesNotContain("save", calls);
        Assert.Same(openedProject, activeProject);
        Assert.Contains("Projekt wurde gewechselt", state.Summary);
    }

    [Fact]
    public async Task RunAsync_confirmed_preview_keeps_original_project_identity()
    {
        var originalProject = new Project { Name = "Original" };
        var openedProject = new Project { Name = "Spaeter geoeffnet" };
        var activeProject = originalProject;
        var activePath = @"C:\Projekte\Original\projekt.json";
        var importCalls = 0;
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "PDF",
            Source: "source.pdf",
            Import: (_, _, _) =>
            {
                importCalls++;
                return Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, []));
            },
            DryRun: true,
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                originalProject,
                state,
                calls,
                showPreview: (_, _) =>
                {
                    activeProject = openedProject;
                    activePath = @"C:\Projekte\Neu\projekt.json";
                    return true;
                },
                getProject: () => activeProject,
                getProjectPath: () => activePath),
            CancellationToken.None);

        Assert.Equal(1, importCalls);
        Assert.DoesNotContain("restore:PDF", calls);
        Assert.DoesNotContain("replace", calls);
        Assert.DoesNotContain("save", calls);
        Assert.Same(openedProject, activeProject);
        Assert.Contains("Projekt wurde gewechselt", state.Summary);
    }

    [Fact]
    public async Task RunAsync_cancellation_after_import_result_stops_before_post_and_commit()
    {
        using var cancellation = new CancellationTokenSource();
        var project = new Project { Name = "Original" };
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "PDF",
            Source: "source.pdf",
            Import: (_, _, _) =>
            {
                calls.Add("import");
                cancellation.Cancel();
                return Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, []));
            },
            PostImportAsync: (_, _, _) =>
            {
                calls.Add("post");
                return Task.CompletedTask;
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls),
            cancellation.Token);

        Assert.DoesNotContain("post", calls);
        Assert.DoesNotContain("dedup", calls);
        Assert.DoesNotContain("replace", calls);
        Assert.DoesNotContain("save", calls);
        Assert.Null(state.ReplacedProject);
        Assert.Contains("abgebrochen", state.Summary);
    }

    [Fact]
    public async Task RunAsync_cancellation_in_post_processing_stops_before_commit()
    {
        using var cancellation = new CancellationTokenSource();
        var project = new Project { Name = "Original" };
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "XTF",
            Source: "source.xtf",
            Import: (_, _, _) => Result<ImportStats>.Success(new ImportStats(1, 0, 1, 0, 0, [])),
            PostImportAsync: (_, _, _) =>
            {
                calls.Add("post");
                cancellation.Cancel();
                return Task.CompletedTask;
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls),
            cancellation.Token);

        Assert.Contains("post", calls);
        Assert.DoesNotContain("dedup", calls);
        Assert.DoesNotContain("replace", calls);
        Assert.DoesNotContain("save", calls);
        Assert.Null(state.ReplacedProject);
        Assert.Contains("abgebrochen", state.Summary);
    }

    [Fact]
    public async Task RunAsync_failed_save_reports_imported_but_not_saved()
    {
        var project = new Project { Name = "Original" };
        var reportDirectory = "original-reports";
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            "IBAK",
            "folder",
            (_, _, _) => Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, [])),
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                project,
                state,
                calls,
                saveProject: () =>
                {
                    reportDirectory = "wrong-new-reports";
                    return false;
                },
                getReportDir: () => reportDirectory),
            CancellationToken.None);

        Assert.Contains("replace", calls);
        Assert.Contains("save", calls);
        Assert.NotNull(state.ReplacedProject);
        Assert.True(state.ReplacedProject!.Dirty);
        Assert.Contains("nicht gespeichert", state.Summary);
        Assert.Equal("IBAK importiert, aber nicht gespeichert", state.Statuses[^1]);
        Assert.NotEqual(100, state.ProgressPercent);
        Assert.Equal("original-reports", state.LastExportReportDirectory);
        Assert.Equal(1, state.LastExportLog?.TotalErrors);
    }

    [Fact]
    public async Task RunAsync_save_exception_reports_committed_project_as_not_saved()
    {
        var project = new Project { Name = "Original" };
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            "IBAK",
            "folder",
            (_, _, _) => Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, [])),
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                project,
                state,
                calls,
                saveProject: () => throw new IOException("Datentraeger nicht erreichbar")),
            CancellationToken.None);

        Assert.Contains("replace", calls);
        Assert.NotNull(state.ReplacedProject);
        Assert.True(state.ReplacedProject!.Dirty);
        Assert.Contains("nicht gespeichert", state.Summary);
        Assert.DoesNotContain("Projekt unveraendert", state.Summary);
        Assert.Equal("IBAK importiert, aber nicht gespeichert", state.Statuses[^1]);
        Assert.Equal(1, state.LastExportLog?.TotalErrors);
    }

    [Fact]
    public async Task RunAsync_post_processing_error_is_visible_as_import_with_notice()
    {
        var project = new Project { Name = "Original" };
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "WinCan",
            Source: "folder",
            Import: (_, _, _) => Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, [])),
            PostImportAsync: (_, _, _) => throw new IOException("Foto konnte nicht kopiert werden"),
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls),
            CancellationToken.None);

        Assert.Contains("replace", calls);
        Assert.Contains("save", calls);
        Assert.Contains("Nacharbeiten unvollstaendig", state.Summary);
        Assert.Contains("Foto konnte nicht kopiert werden", state.Details);
        Assert.Equal("WinCan importiert mit Hinweisen", state.Statuses[^1]);
        Assert.Equal(1, state.LastExportLog?.TotalErrors);
    }

    [Fact]
    public async Task RunAsync_project_switch_writes_report_to_original_report_directory()
    {
        var originalProject = new Project { Name = "Original" };
        var openedProject = new Project { Name = "Neu" };
        var activeProject = originalProject;
        var reportDirectory = @"C:\Projekte\Original\Importberichte";
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            "KINS",
            "folder",
            (_, _, _) =>
            {
                activeProject = openedProject;
                reportDirectory = @"C:\Projekte\Neu\Importberichte";
                return Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, []));
            });

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                originalProject,
                state,
                calls,
                getProject: () => activeProject,
                getReportDir: () => reportDirectory),
            CancellationToken.None);

        Assert.Equal(@"C:\Projekte\Original\Importberichte", state.LastExportReportDirectory);
    }

    [Fact]
    public async Task RunAsync_veroeffentlicht_Dateien_vor_Nachlauf_und_bestaetigt_sie_nach_Projektuebernahme()
    {
        var project = new Project { Name = "Original" };
        var calls = new List<string>();
        var state = new UiState();
        var staging = new FileStagingSessionFake(calls);
        var request = new ImportRunWorkflowRequest<string>(
            Label: "PDF",
            Source: "source.pdf",
            Import: (_, _, context) =>
            {
                Assert.Same(staging, context.FileStaging);
                calls.Add("import");
                return Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, []));
            },
            PostImportAsync: (_, _, context) =>
            {
                Assert.Same(staging, context.FileStaging);
                calls.Add("post");
                return Task.CompletedTask;
            },
            SaveProjectAfterCommit: true,
            BeginFileStaging: projectPath =>
            {
                Assert.Equal(@"C:\Projekte\Test\projekt.json", projectPath);
                calls.Add("begin-files");
                return staging;
            });

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls),
            CancellationToken.None);

        Assert.True(calls.IndexOf("publish-files") < calls.IndexOf("after:PDF"));
        Assert.True(calls.IndexOf("replace") < calls.IndexOf("accept-files"));
        Assert.True(calls.IndexOf("accept-files") < calls.IndexOf("save"));
        Assert.Contains("dispose-files", calls);
        Assert.True(staging.Accepted);
    }

    [Fact]
    public async Task RunAsync_Projektwechsel_nach_Dateivorbereitung_veroeffentlicht_nichts_und_raeumt_auf()
    {
        var originalProject = new Project { Name = "Original" };
        var openedProject = new Project { Name = "Neu" };
        var activeProject = originalProject;
        var calls = new List<string>();
        var state = new UiState();
        var staging = new FileStagingSessionFake(calls);
        var request = new ImportRunWorkflowRequest<string>(
            Label: "XTF",
            Source: "source.xtf",
            Import: (_, _, _) => Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, [])),
            PostImportAsync: (_, _, _) =>
            {
                activeProject = openedProject;
                return Task.CompletedTask;
            },
            BeginFileStaging: _ => staging);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                originalProject,
                state,
                calls,
                getProject: () => activeProject),
            CancellationToken.None);

        Assert.DoesNotContain("publish-files", calls);
        Assert.DoesNotContain("replace", calls);
        Assert.Contains("dispose-files", calls);
        Assert.False(staging.Accepted);
    }

    [Fact]
    public async Task RunAsync_erfolg_schreibt_marker_setzt_txid_und_raeumt_marker_auf()
    {
        var project = new Project();
        var calls = new List<string>();
        var state = new UiState();
        var staging = new FileStagingSessionFake(calls);
        var journal = new FakeTransactionJournal();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "PDF",
            Source: "source.pdf",
            Import: (_, _, _) => Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, [])),
            SaveProjectAfterCommit: true,
            BeginFileStaging: _ => staging);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls, journal: journal),
            CancellationToken.None);

        // Marker wurde vor dem ersten Datei-Move bereits mit den geplanten
        // Rollback-Zielen geschrieben und danach mit dem Ist-Stand erneuert.
        Assert.True(journal.BeginCalls.Count >= 2);
        Assert.Single(journal.BeginCalls[0].PublishedTargets);
        Assert.Equal(1, journal.ClearCalls);
        Assert.Null(journal.TryRead(staging.ProjectRoot));
        // Der uebernommene Record traegt die Commit-TxId des Markers.
        var txId = journal.BeginCalls[0].TxId;
        Assert.Equal(txId, state.ReplacedProject!.LastCommittedImportTxId);
        // Nach der Veroeffentlichung enthaelt der Marker die veroeffentlichte Zieldatei.
        Assert.Contains(journal.BeginCalls, m => m.PublishedTargets.Count == 1);
        Assert.All(journal.BeginCalls, marker => Assert.Equal(staging.StagingRoot, marker.StagingRoot));
    }

    [Fact]
    public async Task RunAsync_Speicherfehler_mit_Dateistaging_behaelt_Marker_fuer_Recovery()
    {
        var project = new Project();
        var calls = new List<string>();
        var state = new UiState();
        var staging = new FileStagingSessionFake(calls);
        var journal = new FakeTransactionJournal();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "PDF",
            Source: "source.pdf",
            Import: (_, _, _) => Result<ImportStats>.Success(
                new ImportStats(1, 1, 0, 0, 0, [])),
            SaveProjectAfterCommit: true,
            BeginFileStaging: _ => staging);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                project,
                state,
                calls,
                saveProject: () => false,
                journal: journal),
            CancellationToken.None);

        Assert.True(staging.Accepted);
        Assert.NotNull(journal.TryRead(staging.ProjectRoot));
        Assert.Equal(0, journal.ClearCalls);
        Assert.Contains("nicht gespeichert", state.Summary);
    }

    [Fact]
    public async Task RunAsync_Aufraeumfehler_behaelt_Marker_fuer_Recovery()
    {
        var project = new Project();
        var calls = new List<string>();
        var state = new UiState();
        var staging = new FileStagingSessionFake(calls, throwOnDispose: true);
        var journal = new FakeTransactionJournal();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "PDF",
            Source: "source.pdf",
            Import: (_, _, _) => Result<ImportStats>.Success(
                new ImportStats(1, 1, 0, 0, 0, [])),
            SaveProjectAfterCommit: true,
            BeginFileStaging: _ => staging);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls, journal: journal),
            CancellationToken.None);

        Assert.NotNull(journal.TryRead(staging.ProjectRoot));
        Assert.Equal(0, journal.ClearCalls);
        Assert.Contains(
            "nicht vollstaendig aufgeraeumt",
            state.Details,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_live_edit_during_import_discards_result()
    {
        // U4: Der Nutzer bearbeitet dasselbe Projekt (gleiche Instanz, gleicher Pfad) waehrend
        // des Imports. Die Content-Signatur weicht beim finalen Check ab -> Ergebnis wird NICHT
        // uebernommen, die manuellen Aenderungen bleiben erhalten.
        var project = new Project { Name = "Original" };
        var edited = false;
        var calls = new List<string>();
        var state = new UiState();
        var request = new ImportRunWorkflowRequest<string>(
            Label: "PDF",
            Source: "source.pdf",
            Import: (_, _, _) =>
            {
                // Simuliert eine Nutzerbearbeitung waehrend des laufenden Imports.
                edited = true;
                return Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, []));
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(
                project,
                state,
                calls,
                computeSignature: _ => edited ? "nach-edit" : "vor-import"),
            CancellationToken.None);

        Assert.DoesNotContain("replace", calls);
        Assert.DoesNotContain("save", calls);
        Assert.Null(state.ReplacedProject);
        Assert.Contains("waehrend des Imports bearbeitet", state.Summary);
    }

    private static ImportRunWorkflowActions Actions(
        Project project,
        UiState state,
        List<string> calls,
        Func<Project, Project>? deepCopyProject = null,
        Func<ImportPreviewResult, string, bool>? showPreview = null,
        Func<Project, IReadOnlyList<string>>? validatePlausibility = null,
        Func<Project>? getProject = null,
        Func<string?>? getProjectPath = null,
        Func<bool>? saveProject = null,
        Func<string?>? getReportDir = null,
        Func<Project, string>? computeSignature = null,
        IImportTransactionJournal? journal = null)
        => new(
            GetProject: getProject ?? (() => project),
            GetProjectPath: getProjectPath ?? (() => @"C:\Projekte\Test\projekt.json"),
            DeepCopyProject: deepCopyProject ?? (p => new Project { Name = p.Name }),
            ReplaceProject: replacement =>
            {
                state.ReplacedProject = replacement;
                calls.Add("replace");
            },
            CreateRestorePoint: label => calls.Add($"restore:{label}"),
            GetReportDir: getReportDir ?? (() => "reports"),
            ExportReport: (log, reportDir) =>
            {
                state.LastExportReportDirectory = reportDir;
                state.LastExportLog = log;
                calls.Add($"report:{log.ImportType}:{log.WasDryRun}");
                return log.WasDryRun ? "preview-report.txt" : "import-report.txt";
            },
            ShowPreview: showPreview ?? ((_, _) => false),
            ValidatePlausibility: validatePlausibility ?? (_ => Array.Empty<string>()),
            DeduplicateAllPrimaryDamages: _ => calls.Add("dedup"),
            RunAfterImportAsync: (_, label) =>
            {
                calls.Add($"after:{label}");
                return Task.CompletedTask;
            },
            SaveProject: () =>
            {
                calls.Add("save");
                return saveProject?.Invoke() ?? true;
            },
            SetStatus: state.Statuses.Add,
            SetCanCancel: value => state.CanCancel = value,
            SetIsImportInProgress: value => state.IsImportInProgress = value,
            SetProgressPercent: value => state.ProgressPercent = value,
            SetPhase: value => state.Phase = value,
            SetProgressText: value => state.Progress = value,
            GetSummaryText: () => state.Summary,
            SetSummaryText: value => state.Summary = value,
            GetDetailsText: () => state.Details,
            SetDetailsText: value => state.Details = value,
            SetLastReportPath: value => state.LastReportPath = value,
            CollectionLock: new object(),
            // Standard: stabile Signatur -> kein U4-Konflikt. Der U4-Test uebergibt eine
            // veraenderliche Signatur.
            ComputeSignature: computeSignature ?? (_ => "sig"),
            Journal: journal);

    private sealed class UiState
    {
        public string Summary { get; set; } = "";
        public string Details { get; set; } = "";
        public string Phase { get; set; } = "";
        public string Progress { get; set; } = "";
        public string LastReportPath { get; set; } = "";
        public string LastExportReportDirectory { get; set; } = "";
        public ImportRunLog? LastExportLog { get; set; }
        public double ProgressPercent { get; set; }
        public bool CanCancel { get; set; }
        public bool IsImportInProgress { get; set; }
        public Project? ReplacedProject { get; set; }
        public List<string> Statuses { get; } = new();
    }

    private sealed class FakeTransactionJournal : IImportTransactionJournal
    {
        public List<ImportTransactionMarker> BeginCalls { get; } = new();
        public int ClearCalls { get; private set; }
        private ImportTransactionMarker? _current;

        public void Begin(string projectRoot, ImportTransactionMarker marker)
        {
            BeginCalls.Add(marker);
            _current = marker;
        }

        public ImportTransactionMarker? TryRead(string projectRoot) => _current;

        public void Clear(string projectRoot)
        {
            ClearCalls++;
            _current = null;
        }
    }

    private sealed class FileStagingSessionFake(
        List<string> calls,
        bool throwOnDispose = false) : IImportFileStagingSession
    {
        public string ProjectRoot => @"C:\Projekte\Test";
        public string StagingRoot => Path.Combine(ProjectRoot, "Projektdateien", ".import-staging");
        public bool Accepted { get; private set; }
        public IReadOnlyList<PublishedFileInfo> PreparedFiles { get; } =
            [new PublishedFileInfo("Bilder/1.jpg", "AABB")];
        public IReadOnlyList<PublishedFileInfo> PublishedFiles { get; private set; } = [];

        public string StageCopy(
            string sourcePath,
            string targetDirectory,
            Func<DateTime>? now = null,
            CancellationToken cancellationToken = default)
            => Path.Combine(targetDirectory, Path.GetFileName(sourcePath));

        public void Publish()
        {
            calls.Add("publish-files");
            PublishedFiles = [new PublishedFileInfo("Bilder/1.jpg", "AABB")];
        }

        public void Accept()
        {
            Accepted = true;
            calls.Add("accept-files");
        }

        public void Dispose()
        {
            calls.Add("dispose-files");
            if (throwOnDispose)
                throw new IOException("Arbeitsordner gesperrt");
        }
    }
}
