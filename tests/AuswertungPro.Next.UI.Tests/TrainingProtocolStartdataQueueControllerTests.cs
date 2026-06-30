using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataQueueControllerTests
{
    [Fact]
    public void Run_enqueues_catalog_valid_new_samples_and_reports_existing_status_and_log_text()
    {
        var queue = new InfraSelfImproving.ReviewQueueService();
        var samples = new[]
        {
            Sample("s1", "BAB", TrainingSampleStatus.New),
            Sample("s2", "BBA", TrainingSampleStatus.New),
            Sample("s3", "MWST", TrainingSampleStatus.New),
            Sample("s4", "BAB", TrainingSampleStatus.Approved)
        };

        var result = TrainingProtocolStartdataQueueController.Run(samples, new Catalog(), queue);

        Assert.Equal(2, result.AddedCount);
        Assert.Equal(2, result.CandidateCount);
        Assert.Equal("2 Protokoll-Startdaten als Kandidaten eingereiht (Freigabe ueber Review).", result.StatusText);
        Assert.Equal("Protokoll-Startdaten: 2 Kandidaten eingereiht (von 2 gefiltert).", result.LogText);

        var queued = queue.GetAll();
        Assert.Equal(["s1", "s2"], queued.Select(q => q.SelfTrainingSampleId));
        Assert.All(queued, q =>
        {
            Assert.Equal("ProtocolStartdata", q.SelfTrainingMatchLevel);
            Assert.Equal("Protokoll-Startdaten", q.SelfTrainingReason);
            Assert.Equal(q.SelfTrainingVsaCode, q.SelfTrainingSuggestedCode);
        });
    }

    [Fact]
    public void Run_deduplicates_already_queued_sample_ids_but_keeps_candidate_count()
    {
        var queue = new InfraSelfImproving.ReviewQueueService();
        queue.EnqueueFromSelfTraining(
            caseId: "case-s1",
            vsaCode: "BAB",
            suggestedCode: "BAB",
            meter: 1.5,
            framePath: "old.jpg",
            matchLevel: "ProtocolStartdata",
            reason: "Protokoll-Startdaten",
            sampleId: "s1");

        var samples = new[]
        {
            Sample("s1", "BAB", TrainingSampleStatus.New),
            Sample("s2", "BBA", TrainingSampleStatus.New)
        };

        var result = TrainingProtocolStartdataQueueController.Run(samples, new Catalog(), queue);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(2, queue.Count);
        Assert.Equal("1 Protokoll-Startdaten als Kandidaten eingereiht (Freigabe ueber Review).", result.StatusText);
        Assert.Equal("Protokoll-Startdaten: 1 Kandidaten eingereiht (von 2 gefiltert).", result.LogText);
    }

    private static TrainingSample Sample(string id, string code, TrainingSampleStatus status)
        => new()
        {
            SampleId = id,
            CaseId = $"case-{id}",
            Code = code,
            Beschreibung = "x",
            Status = status,
            InspectionDate = new DateTime(2024, 1, 1),
            TrainingEligible = true,
            MeterStart = 1.5,
            FramePath = $"{id}.jpg"
        };

    private sealed class Catalog : ICodeCatalogProvider
    {
        private readonly Dictionary<string, CodeDefinition> _codes = new()
        {
            ["BAB"] = new CodeDefinition { Code = "BAB", IsSelectable = true },
            ["BBA"] = new CodeDefinition { Code = "BBA", IsSelectable = true },
            ["MWST"] = new CodeDefinition { Code = "MWST", IsSelectable = false }
        };

        public IReadOnlyList<CodeDefinition> GetAll() => _codes.Values.ToList();
        public bool TryGet(string code, out CodeDefinition def)
        {
            var found = _codes.TryGetValue(code, out var definition);
            def = definition ?? new CodeDefinition();
            return found;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new NotSupportedException();
        public IReadOnlyList<string> AllowedCodes() => _codes.Values.Where(c => c.IsSelectable).Select(c => c.Code).ToList();
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }
}
