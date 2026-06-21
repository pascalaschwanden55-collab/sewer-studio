using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTrainingSampleEvalProtectorTests
{
    [Fact]
    public void IsProtected_returns_true_for_reserved_eval_haltung()
    {
        var protector = new CodingTrainingSampleEvalProtector(
            () => new EvalContaminationSets(
                new HashSet<string>(),
                new HashSet<string> { "287425-81162" }));

        Assert.True(protector.IsProtected(new TrainingSample
        {
            CaseId = "287425-81162/2025_Saniert",
            FramePath = ""
        }));
    }

    [Fact]
    public void Classify_returns_clean_when_no_eval_sets_are_loaded()
    {
        var protector = new CodingTrainingSampleEvalProtector(
            () => new EvalContaminationSets(new HashSet<string>(), new HashSet<string>()));

        Assert.Equal(
            EvalContaminationGuard.ExportContaminationResult.Clean,
            protector.Classify(new TrainingSample { CaseId = "111-222", FramePath = "" }));
    }

    [Fact]
    public void LoadSets_is_called_once_and_cached()
    {
        var loads = 0;
        var protector = new CodingTrainingSampleEvalProtector(
            () =>
            {
                loads++;
                return new EvalContaminationSets(
                    new HashSet<string>(),
                    new HashSet<string> { "111-222" });
            });

        Assert.True(protector.IsProtected(new TrainingSample { CaseId = "111-222" }));
        Assert.True(protector.IsProtected(new TrainingSample { CaseId = "111-222" }));
        Assert.Equal(1, loads);
    }

    [Fact]
    public void Load_failure_logs_and_keeps_training_unblocked()
    {
        var logs = new List<string>();
        var protector = new CodingTrainingSampleEvalProtector(
            () => throw new InvalidOperationException("manifest kaputt"),
            logs.Add);

        Assert.False(protector.IsProtected(new TrainingSample { CaseId = "111-222" }));
        Assert.Single(logs);
        Assert.Contains("manifest kaputt", logs[0]);
    }
}
