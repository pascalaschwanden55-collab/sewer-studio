using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingTrainingSampleFactory
{
    public static string? PrimaryFramePath(CodingEvent codingEvent)
        => codingEvent.Entry.FotoPaths.Count > 0 ? codingEvent.Entry.FotoPaths[0] : null;

    public static TrainingSample Create(
        CodingEvent codingEvent,
        string caseId,
        string? framePath,
        DateTime? inspectionDate,
        string? confirmedByUser,
        DateTime? confirmedAtUtc,
        string? evidenceFramePath = null,
        string? snapshotError = null)
    {
        var sample = CodingEventToSampleMapper.FromCodingEvent(
            codingEvent,
            caseId,
            framePath,
            inspectionDate,
            confirmedByUser,
            confirmedAtUtc,
            evidenceFramePath);

        sample.SnapshotError = snapshotError;
        AddAdditionalFramePaths(sample, codingEvent);
        return sample;
    }

    private static void AddAdditionalFramePaths(TrainingSample sample, CodingEvent codingEvent)
    {
        if (codingEvent.Entry.FotoPaths.Count <= 1)
            return;

        sample.AdditionalFramePaths ??= new List<string>();
        for (var i = 1; i < codingEvent.Entry.FotoPaths.Count; i++)
            sample.AdditionalFramePaths.Add(codingEvent.Entry.FotoPaths[i]);
    }
}
