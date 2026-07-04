using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingYoloExportCandidateSelectorTests
{
    [Fact]
    public void Select_filtert_approved_samples_mit_existierendem_frame_und_trainingsfreigabe()
    {
        var eligible = new TrainingSample
        {
            SampleId = "eligible",
            Status = TrainingSampleStatus.Approved,
            FramePath = "exists-a.jpg",
            Code = "OK"
        };
        var ineligible = new TrainingSample
        {
            SampleId = "ineligible",
            Status = TrainingSampleStatus.Approved,
            FramePath = "exists-b.jpg",
            Code = "BAD"
        };
        var missingFile = new TrainingSample
        {
            SampleId = "missing-file",
            Status = TrainingSampleStatus.Approved,
            FramePath = "missing.jpg",
            Code = "OK"
        };
        var rejected = new TrainingSample
        {
            SampleId = "rejected",
            Status = TrainingSampleStatus.Rejected,
            FramePath = "exists-c.jpg",
            Code = "OK"
        };
        var checkedFiles = new List<string>();

        var result = TrainingYoloExportCandidateSelector.Select(
            [eligible, ineligible, missingFile, rejected],
            fileExists: path =>
            {
                checkedFiles.Add(path);
                return path.StartsWith("exists-", StringComparison.Ordinal);
            },
            isTrainingExportEligible: sample => sample.Code == "OK");

        Assert.Equal([eligible, ineligible], result.Candidates);
        Assert.Equal([eligible], result.Approved);
        Assert.True(result.RequiresPersistence);
        Assert.Equal(["exists-a.jpg", "exists-b.jpg", "missing.jpg"], checkedFiles);
    }
}
