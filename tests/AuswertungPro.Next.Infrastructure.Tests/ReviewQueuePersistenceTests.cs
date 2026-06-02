using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ReviewQueuePersistenceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "rq-" + Guid.NewGuid().ToString("N") + ".json");
    public void Dispose() { try { if (File.Exists(_path)) File.Delete(_path); } catch { } }

    [Fact]
    public void Roundtrip_haelt_SelfTraining_Items_inkl_SampleId()
    {
        var a = new ReviewQueueService(_path);
        a.EnqueueFromSelfTraining(
            caseId: "06.1-2", vsaCode: "BAB", suggestedCode: "",
            meter: 12.3, framePath: "f.png", matchLevel: "NoFindings",
            reason: "HumanReviewRequired", sampleId: "06.1-2_st_001_120000");

        var b = new ReviewQueueService(_path); // neu aus Datei laden
        var items = b.GetAll();

        Assert.Single(items);
        Assert.Equal("06.1-2_st_001_120000", items[0].SelfTrainingSampleId);
        Assert.Equal("BAB", items[0].SelfTrainingVsaCode);
    }

    [Fact]
    public void Alte_Datei_ohne_SampleId_laedt_mit_null_SampleId()
    {
        File.WriteAllText(_path,
            "[{\"Id\":\"x\",\"Priority\":0.9,\"EnqueuedUtc\":\"2026-01-01T00:00:00Z\"," +
            "\"SelfTrainingCaseId\":\"06.1-2\",\"SelfTrainingVsaCode\":\"BAB\"," +
            "\"SelfTrainingMeter\":12.3,\"SelfTrainingFramePath\":\"f.png\",\"SelfTrainingMatchLevel\":\"Mismatch\"}]");

        var svc = new ReviewQueueService(_path);
        var items = svc.GetAll();

        Assert.Single(items);
        Assert.Null(items[0].SelfTrainingSampleId);
        Assert.Equal("06.1-2", items[0].SelfTrainingCaseId);
    }
}
