using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterSampleGenerationStatusFormatterTests
{
    [Fact]
    public void FormatEmptyCaseStatus_reports_duplicate_only_case()
    {
        var text = TrainingCenterSampleGenerationStatusFormatter.FormatEmptyCaseStatus(
            "06.24341-35625",
            @"C:\Protokolle\haltung.pdf",
            Result(TrainingSampleGenerationOutcome.OnlyDuplicates, parsedEntries: 3));

        Assert.Equal("Keine neuen Samples für 06.24341-35625 (alle 3 Einträge bereits vorhanden).", text);
    }

    [Fact]
    public void FormatEmptyCaseStatus_reports_missing_protocol_path()
    {
        var text = TrainingCenterSampleGenerationStatusFormatter.FormatEmptyCaseStatus(
            "Case-1",
            @"C:\Protokolle\missing.pdf",
            Result(TrainingSampleGenerationOutcome.ProtocolFileMissing));

        Assert.Equal(@"Protokolldatei fehlt: C:\Protokolle\missing.pdf", text);
    }

    [Fact]
    public void FormatBatchSkip_returns_duplicate_status()
    {
        var info = TrainingCenterSampleGenerationStatusFormatter.FormatBatchSkip(
            Result(TrainingSampleGenerationOutcome.OnlyDuplicates, parsedEntries: 4));

        Assert.Equal(TrainingCenterBatchSkipKind.DuplicateOnly, info.Kind);
        Assert.Equal("4 Duplikate", info.ResultSummary);
        Assert.Equal("  -> 0 Samples (alle 4 Eintraege bereits vorhanden)", info.LogMessage);
        Assert.Equal("4 Duplikate", info.LiveCodeInfo);
        Assert.Equal("bereits vorhanden", info.LiveMeterInfo);
    }

    [Fact]
    public void FormatBatchSkip_returns_missing_protocol_status()
    {
        var info = TrainingCenterSampleGenerationStatusFormatter.FormatBatchSkip(
            Result(TrainingSampleGenerationOutcome.ProtocolFileMissing));

        Assert.Equal(TrainingCenterBatchSkipKind.MissingProtocol, info.Kind);
        Assert.Equal("Protokoll fehlt", info.ResultSummary);
        Assert.Equal("  -> 0 Samples (Protokolldatei fehlt)", info.LogMessage);
        Assert.Equal("\u2014", info.LiveCodeInfo);
        Assert.Equal("Protokoll fehlt", info.LiveMeterInfo);
    }

    private static TrainingSampleGenerationResult Result(
        TrainingSampleGenerationOutcome outcome,
        int parsedEntries = 0)
        => new(new List<TrainingSample>(), parsedEntries, 0, outcome);
}
