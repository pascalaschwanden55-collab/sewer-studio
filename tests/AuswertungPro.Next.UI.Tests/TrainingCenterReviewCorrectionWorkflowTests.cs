using System.IO;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterReviewCorrectionWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_accepted_explorer_entry_applies_selected_code_and_description()
    {
        using var temp = new TempDir();
        var framePath = Path.Combine(temp.Path, "frame.jpg");
        File.WriteAllText(framePath, "jpg");
        var item = new ReviewQueueItem("review-1", null, 0.8, DateTime.UnixEpoch)
        {
            SelfTrainingCaseId = "case-1",
            SelfTrainingVsaCode = "BAA",
            SelfTrainingMeter = 12.5,
            SelfTrainingFramePath = framePath,
            SelfTrainingMatchLevel = "Mismatch"
        };
        VsaCodeExplorerViewModel? shownViewModel = null;
        ReviewQueueItem? appliedItem = null;
        string? appliedCode = null;
        string? appliedDescription = null;

        var result = await TrainingCenterReviewCorrectionWorkflow.ExecuteAsync(
            new TrainingCenterReviewCorrectionRequest(item, EmptyVsaCodeSelectionCatalog.Instance),
            new TrainingCenterReviewCorrectionActions(
                ShowCodeExplorer: viewModel =>
                {
                    shownViewModel = viewModel;
                    return new VsaCodeExplorerDialogResult(
                        true,
                        new ProtocolEntry
                        {
                            Code = "BAB",
                            Beschreibung = "Riss laengs"
                        });
                },
                ApplyCorrectionAsync: (reviewItem, correctedCode, correctedDescription) =>
                {
                    appliedItem = reviewItem;
                    appliedCode = correctedCode;
                    appliedDescription = correctedDescription;
                    return Task.CompletedTask;
                },
                Warn: (_, _) => throw new InvalidOperationException("Warn darf nicht aufgerufen werden.")));

        Assert.Equal(TrainingCenterReviewCorrectionOutcome.Applied, result.Outcome);
        Assert.Same(item, appliedItem);
        Assert.Equal("BAB", appliedCode);
        Assert.Equal("Riss laengs", appliedDescription);
        Assert.NotNull(shownViewModel);
        Assert.Equal("12.50", shownViewModel!.MeterStart);
        Assert.Equal([framePath], shownViewModel.FotoPaths);
    }

    [Fact]
    public async Task ExecuteAsync_without_catalog_warns_and_does_not_open_dialog()
    {
        var item = new ReviewQueueItem("review-1", null, 0.8, DateTime.UnixEpoch);
        var warnings = new List<(string Message, string Title)>();

        var result = await TrainingCenterReviewCorrectionWorkflow.ExecuteAsync(
            new TrainingCenterReviewCorrectionRequest(item, Catalog: null),
            new TrainingCenterReviewCorrectionActions(
                ShowCodeExplorer: _ => throw new InvalidOperationException("Dialog darf nicht geoeffnet werden."),
                ApplyCorrectionAsync: (_, _, _) => throw new InvalidOperationException("Korrektur darf nicht angewendet werden."),
                Warn: (message, title) => warnings.Add((message, title))));

        Assert.Equal(TrainingCenterReviewCorrectionOutcome.CatalogUnavailable, result.Outcome);
        var warning = Assert.Single(warnings);
        Assert.Contains("Code-Katalog", warning.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Korrektur", warning.Title);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "training-review-correction-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }
}
