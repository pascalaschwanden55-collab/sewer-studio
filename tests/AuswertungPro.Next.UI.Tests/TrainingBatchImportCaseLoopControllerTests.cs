using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCaseLoopControllerTests
{
    [Fact]
    public async Task RunAsync_reports_progress_and_processes_cases_in_order()
    {
        var cases = new[] { Case("one"), Case("two") };
        var calls = new List<string>();

        await TrainingBatchImportCaseLoopController.RunAsync(
            cases,
            (index, total, trainingCase) => calls.Add($"progress:{index}/{total}:{trainingCase.CaseId}"),
            (index, trainingCase, _) =>
            {
                calls.Add($"process:{index}:{trainingCase.CaseId}");
                return Task.CompletedTask;
            },
            ex => calls.Add($"failure:{ex.Message}"),
            CancellationToken.None);

        Assert.Equal(
            [
                "progress:0/2:one",
                "process:0:one",
                "progress:1/2:two",
                "process:1:two"
            ],
            calls);
    }

    [Fact]
    public async Task RunAsync_records_non_cancellation_failures_and_continues()
    {
        var cases = new[] { Case("broken"), Case("ok") };
        var calls = new List<string>();

        await TrainingBatchImportCaseLoopController.RunAsync(
            cases,
            (index, _, trainingCase) => calls.Add($"progress:{index}:{trainingCase.CaseId}"),
            (index, trainingCase, _) =>
            {
                calls.Add($"process:{index}:{trainingCase.CaseId}");
                if (trainingCase.CaseId == "broken")
                    throw new InvalidOperationException("kaputt");
                return Task.CompletedTask;
            },
            ex => calls.Add($"failure:{ex.Message}"),
            CancellationToken.None);

        Assert.Equal(
            [
                "progress:0:broken",
                "process:0:broken",
                "failure:kaputt",
                "progress:1:ok",
                "process:1:ok"
            ],
            calls);
    }

    [Fact]
    public async Task RunAsync_propagates_operation_canceled_exception_without_recording_failure()
    {
        var cases = new[] { Case("stop"), Case("never") };
        var calls = new List<string>();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            TrainingBatchImportCaseLoopController.RunAsync(
                cases,
                (index, _, trainingCase) => calls.Add($"progress:{index}:{trainingCase.CaseId}"),
                (index, trainingCase, _) =>
                {
                    calls.Add($"process:{index}:{trainingCase.CaseId}");
                    throw new OperationCanceledException();
                },
                ex => calls.Add($"failure:{ex.Message}"),
                CancellationToken.None));

        Assert.Equal(["progress:0:stop", "process:0:stop"], calls);
    }

    private static TrainingCase Case(string id) => new() { CaseId = id };
}
