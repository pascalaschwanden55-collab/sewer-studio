using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataSuggestionRequestFactoryTests
{
    [Fact]
    public async Task Create_uebernimmt_queue_catalog_ui_und_loader()
    {
        var queue = new InfraSelfImproving.ReviewQueueService();
        var injectedCatalog = new Catalog();
        var fallbackCatalog = new Catalog();
        var loadedSample = new TrainingSample { SampleId = "sample-1" };
        var calls = new List<string>();

        var request = TrainingProtocolStartdataSuggestionRequestFactory.Create(
            new TrainingProtocolStartdataSuggestionRequestFactoryRequest(
                QueueService: queue,
                InjectedCatalog: injectedCatalog,
                ReloadReviewQueue: () => calls.Add("reload"),
                OnUi: action =>
                {
                    calls.Add("ui-before");
                    action();
                    calls.Add("ui-after");
                },
                SetReviewStatusText: value => calls.Add($"status:{value}"),
                Log: value => calls.Add($"log:{value}")),
            FallbackCatalog: () =>
            {
                calls.Add("fallback");
                return fallbackCatalog;
            },
            LoadSamplesAsync: () =>
            {
                calls.Add("load");
                return Task.FromResult(new List<TrainingSample> { loadedSample });
            });

        Assert.Same(queue, request.QueueService);
        Assert.Same(injectedCatalog, request.InjectedCatalog);
        Assert.Same(fallbackCatalog, request.FallbackCatalog());
        var loaded = await request.LoadSamplesAsync();
        request.ReloadReviewQueue();
        request.OnUi(() => calls.Add("ui-action"));
        request.SetReviewStatusText("bereit");
        request.Log("fertig");

        Assert.Equal([loadedSample], loaded);
        Assert.Equal(
            ["fallback", "load", "reload", "ui-before", "ui-action", "ui-after", "status:bereit", "log:fertig"],
            calls);
    }

    private sealed class Catalog : ICodeCatalogProvider
    {
        public IReadOnlyList<CodeDefinition> GetAll() => [];

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = new CodeDefinition();
            return false;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
        {
        }

        public IReadOnlyList<string> AllowedCodes() => [];

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }
}
