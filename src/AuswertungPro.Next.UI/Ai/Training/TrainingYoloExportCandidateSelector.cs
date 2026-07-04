using AuswertungPro.Next.Application.Ai.Training;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingYoloExportCandidateSelection(
    IReadOnlyList<TrainingSample> Candidates,
    IReadOnlyList<TrainingSample> Approved)
{
    public bool RequiresPersistence => Candidates.Count != Approved.Count;
}

public static class TrainingYoloExportCandidateSelector
{
    public static TrainingYoloExportCandidateSelection SelectWithFileSystem(
        IEnumerable<TrainingSample> samples,
        Func<TrainingSample, bool> isTrainingExportEligible)
        => Select(samples, File.Exists, isTrainingExportEligible);

    public static TrainingYoloExportCandidateSelection Select(
        IEnumerable<TrainingSample> samples,
        Func<string, bool> fileExists,
        Func<TrainingSample, bool> isTrainingExportEligible)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(isTrainingExportEligible);

        var candidates = samples
            .Where(sample => sample.Status == TrainingSampleStatus.Approved
                             && !string.IsNullOrWhiteSpace(sample.FramePath)
                             && fileExists(sample.FramePath))
            .ToList();
        var approved = candidates
            .Where(isTrainingExportEligible)
            .ToList();

        return new TrainingYoloExportCandidateSelection(candidates, approved);
    }
}
