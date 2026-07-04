using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataSuggestionWorkflowTests
{
    [Fact]
    public async Task RunAsync_tut_nichts_wenn_queue_service_fehlt()
    {
        var calls = new List<string>();

        await TrainingProtocolStartdataSuggestionWorkflow.RunAsync(
            new TrainingProtocolStartdataSuggestionWorkflowRequest(
                QueueService: null,
                InjectedCatalog: new Catalog(),
                FallbackCatalog: () =>
                {
                    calls.Add("fallback");
                    return new Catalog();
                },
                LoadSamplesAsync: () =>
                {
                    calls.Add("load");
                    return Task.FromResult(new List<TrainingSample>());
                },
                ReloadReviewQueue: () => calls.Add("reload"),
                OnUi: action =>
                {
                    calls.Add("ui");
                    action();
                },
                SetReviewStatusText: value => calls.Add("status:" + value),
                Log: value => calls.Add("log:" + value)));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_setzt_missing_catalog_status_und_laed_keine_samples()
    {
        var calls = new List<string>();

        await TrainingProtocolStartdataSuggestionWorkflow.RunAsync(
            new TrainingProtocolStartdataSuggestionWorkflowRequest(
                QueueService: new InfraSelfImproving.ReviewQueueService(),
                InjectedCatalog: null,
                FallbackCatalog: () => null,
                LoadSamplesAsync: () =>
                {
                    calls.Add("load");
                    return Task.FromResult(new List<TrainingSample>());
                },
                ReloadReviewQueue: () => calls.Add("reload"),
                OnUi: action =>
                {
                    calls.Add("ui-before");
                    action();
                    calls.Add("ui-after");
                },
                SetReviewStatusText: value => calls.Add("status:" + value),
                Log: value => calls.Add("log:" + value)));

        Assert.Equal(
            ["ui-before", "status:Kein Code-Katalog verfuegbar.", "ui-after"],
            calls);
    }

    [Fact]
    public async Task RunAsync_laed_samples_reiht_kandidaten_ein_und_aktualisiert_ui()
    {
        var calls = new List<string>();
        var queue = new InfraSelfImproving.ReviewQueueService();

        await TrainingProtocolStartdataSuggestionWorkflow.RunAsync(
            new TrainingProtocolStartdataSuggestionWorkflowRequest(
                QueueService: queue,
                InjectedCatalog: new Catalog(),
                FallbackCatalog: () =>
                {
                    calls.Add("fallback");
                    return null;
                },
                LoadSamplesAsync: () =>
                {
                    calls.Add("load");
                    return Task.FromResult(new List<TrainingSample>
                    {
                        Sample("s1", "BAB"),
                        Sample("s2", "BBA"),
                        Sample("s3", "MWST")
                    });
                },
                ReloadReviewQueue: () => calls.Add("reload"),
                OnUi: action =>
                {
                    calls.Add("ui-before");
                    action();
                    calls.Add("ui-after");
                },
                SetReviewStatusText: value => calls.Add("status:" + value),
                Log: value => calls.Add("log:" + value)));

        Assert.Equal(["s1", "s2"], queue.GetAll().Select(q => q.SelfTrainingSampleId));
        Assert.DoesNotContain("fallback", calls);
        Assert.Contains("load", calls);
        Assert.Contains("reload", calls);
        Assert.Contains("status:2 Protokoll-Startdaten als Kandidaten eingereiht (Freigabe ueber Review).", calls);
        Assert.Contains("log:Protokoll-Startdaten: 2 Kandidaten eingereiht (von 2 gefiltert).", calls);
    }

    private static TrainingSample Sample(string id, string code)
        => new()
        {
            SampleId = id,
            CaseId = $"case-{id}",
            Code = code,
            Beschreibung = "x",
            Status = TrainingSampleStatus.New,
            TrainingEligible = true,
            MeterStart = 1.5,
            InspectionDate = new DateTime(2024, 1, 1),
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

        public IReadOnlyList<string> AllowedCodes()
            => _codes.Values.Where(c => c.IsSelectable).Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }
}
