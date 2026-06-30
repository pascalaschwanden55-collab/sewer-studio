using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ImportRunWorkflowControllerTests
{
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
            PostImportAsync: (_, ctx) =>
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
                validatePlausibility: _ => new[] { "Warnung 1" }),
            CancellationToken.None);

        Assert.Contains("import:source.pdf:True:False", calls);
        Assert.Contains("post:False", calls);
        Assert.Contains("dedup", calls);
        Assert.Contains("after:PDF", calls);
        Assert.Contains("save", calls);
        Assert.Contains("report:PDF:False", calls);
        Assert.Equal("report.txt", state.LastReportPath);
        Assert.Equal(100, state.ProgressPercent);
        Assert.False(state.IsImportInProgress);
        Assert.False(state.CanCancel);
        Assert.Equal("", state.Phase);
        Assert.Contains("PDF Import:", state.Summary);
        Assert.Contains("Plausibilitaet: 1 Warnung", state.Summary);
        Assert.Contains("m1", state.Details);
        Assert.Contains("Warnung 1", state.Details);
        Assert.Equal("PDF importiert", state.Statuses[^1]);
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
            PostImportAsync: (_, _) =>
            {
                calls.Add("post");
                return Task.CompletedTask;
            },
            SaveProjectAfterCommit: true);

        await ImportRunWorkflowController.RunAsync(
            request,
            Actions(project, state, calls),
            CancellationToken.None);

        Assert.DoesNotContain("post", calls);
        Assert.DoesNotContain("dedup", calls);
        Assert.DoesNotContain("after:XTF", calls);
        Assert.DoesNotContain("save", calls);
        Assert.Contains("report:XTF:False", calls);
        Assert.Equal("XTF Import fehlgeschlagen: kaputt", state.Summary);
        Assert.Equal("XTF Import fehlgeschlagen", state.Statuses[^1]);
        Assert.False(state.IsImportInProgress);
        Assert.False(state.CanCancel);
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
            PostImportAsync: (_, ctx) =>
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

        Assert.Contains("import:Preview:True", calls);
        Assert.Contains("preview:WinCan", calls);
        Assert.Contains("import:Live:False", calls);
        Assert.Single(calls.Where(call => call == "post:False"));
        Assert.DoesNotContain("post:True", calls);
        Assert.Single(calls.Where(call => call == "save"));
        Assert.Equal(2, calls.Count(call => call.StartsWith("report:WinCan:", StringComparison.Ordinal)));
        Assert.False(state.IsImportInProgress);
        Assert.False(state.CanCancel);
    }

    private static ImportRunWorkflowActions Actions(
        Project project,
        UiState state,
        List<string> calls,
        Func<Project, Project>? deepCopyProject = null,
        Func<ImportPreviewResult, string, bool>? showPreview = null,
        Func<Project, IReadOnlyList<string>>? validatePlausibility = null)
        => new(
            GetProject: () => project,
            DeepCopyProject: deepCopyProject ?? (p => p),
            GetReportDir: () => "reports",
            ExportReport: (log, _) =>
            {
                calls.Add($"report:{log.ImportType}:{log.WasDryRun}");
                return "report.txt";
            },
            ShowPreview: showPreview ?? ((_, _) => false),
            ValidatePlausibility: validatePlausibility ?? (_ => Array.Empty<string>()),
            DeduplicateAllPrimaryDamages: () => calls.Add("dedup"),
            RunAfterImportAsync: label =>
            {
                calls.Add($"after:{label}");
                return Task.CompletedTask;
            },
            SaveProject: () => calls.Add("save"),
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
            CollectionLock: new object());

    private sealed class UiState
    {
        public string Summary { get; set; } = "";
        public string Details { get; set; } = "";
        public string Phase { get; set; } = "";
        public string Progress { get; set; } = "";
        public string LastReportPath { get; set; } = "";
        public double ProgressPercent { get; set; }
        public bool CanCancel { get; set; }
        public bool IsImportInProgress { get; set; }
        public List<string> Statuses { get; } = new();
    }
}
