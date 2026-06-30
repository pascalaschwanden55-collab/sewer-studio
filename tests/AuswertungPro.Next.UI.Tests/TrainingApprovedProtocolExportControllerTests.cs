using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingApprovedProtocolExportControllerTests
{
    [Fact]
    public async Task RunAsync_exports_eligible_unexported_approved_samples_and_builds_existing_messages()
    {
        var now = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var first = Sample("s1", "BBA", TrainingSampleStatus.Approved);
        first.Beschreibung = "Riss";
        first.MeterStart = 1.2;
        first.MeterEnd = 2.3;
        first.IsStreckenschaden = true;
        var second = Sample("s2", "BAA", TrainingSampleStatus.Approved);
        var exported = new List<(ProtocolEntry Entry, string? CaseId)>();
        var persistCalls = 0;

        var result = await TrainingApprovedProtocolExportController.RunAsync(
            [first, second],
            _ => true,
            (entry, caseId) => exported.Add((entry, caseId)),
            () =>
            {
                persistCalls++;
                return Task.CompletedTask;
            },
            () => now,
            "target.json");

        Assert.Equal(2, result.ExportedCount);
        Assert.Equal(now, first.ExportedUtc);
        Assert.Equal(now, second.ExportedUtc);
        Assert.Equal(1, persistCalls);
        Assert.Equal("Protokoll-Training: 2 Samples als Few-Shot-Beispiele gespeichert (2 Codes).", result.StatusText);
        Assert.Equal(
            [
                "Protokoll-Training: 2 Samples als Few-Shot-Beispiele gespeichert.",
                "  Codes: BAA, BBA",
                "  Ziel: target.json",
                "  Wirkung: Qwen nutzt diese Beispiele bei zuk\u00fcnftigen Protokoll-Generierungen."
            ],
            result.LogLines);

        Assert.Equal(2, exported.Count);
        Assert.Equal(("BBA", "Riss", 1.2, 2.3, true, "case-s1"), Snapshot(exported[0]));
        Assert.Equal(("BAA", "", 0, 0, false, "case-s2"), Snapshot(exported[1]));
    }

    [Fact]
    public async Task RunAsync_persists_eligibility_changes_before_returning_when_all_candidates_are_ineligible()
    {
        var sample = Sample("s1", "BBA", TrainingSampleStatus.Approved);
        var persistCalls = 0;
        var addCalls = 0;

        var result = await TrainingApprovedProtocolExportController.RunAsync(
            [sample],
            _ => false,
            (_, _) => addCalls++,
            () =>
            {
                persistCalls++;
                return Task.CompletedTask;
            },
            () => DateTime.UtcNow,
            "target.json");

        Assert.Equal(0, result.ExportedCount);
        Assert.Equal("Keine nicht-exportierten Approved-Samples vorhanden.", result.StatusText);
        Assert.Empty(result.LogLines);
        Assert.Equal(1, persistCalls);
        Assert.Equal(0, addCalls);
        Assert.Null(sample.ExportedUtc);
    }

    [Fact]
    public async Task RunAsync_ignores_rejected_new_and_already_exported_samples_without_persisting()
    {
        var alreadyExported = Sample("done", "BBA", TrainingSampleStatus.Approved);
        alreadyExported.ExportedUtc = DateTime.UtcNow.AddDays(-1);
        var persistCalls = 0;
        var addCalls = 0;

        var result = await TrainingApprovedProtocolExportController.RunAsync(
            [
                alreadyExported,
                Sample("rejected", "BAB", TrainingSampleStatus.Rejected),
                Sample("new", "BAA", TrainingSampleStatus.New)
            ],
            _ => true,
            (_, _) => addCalls++,
            () =>
            {
                persistCalls++;
                return Task.CompletedTask;
            },
            () => DateTime.UtcNow,
            "target.json");

        Assert.Equal(0, result.ExportedCount);
        Assert.Equal("Keine nicht-exportierten Approved-Samples vorhanden.", result.StatusText);
        Assert.Equal(0, persistCalls);
        Assert.Equal(0, addCalls);
    }

    private static TrainingSample Sample(string id, string code, TrainingSampleStatus status)
        => new()
        {
            SampleId = id,
            CaseId = $"case-{id}",
            Code = code,
            Status = status,
            FramePath = $"{id}.jpg"
        };

    private static (string Code, string Beschreibung, double? MeterStart, double? MeterEnd, bool IsStreckenschaden, string? CaseId) Snapshot(
        (ProtocolEntry Entry, string? CaseId) exported)
        => (
            exported.Entry.Code,
            exported.Entry.Beschreibung,
            exported.Entry.MeterStart,
            exported.Entry.MeterEnd,
            exported.Entry.IsStreckenschaden,
            exported.CaseId);
}
