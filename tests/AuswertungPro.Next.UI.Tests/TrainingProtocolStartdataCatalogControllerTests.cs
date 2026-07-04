using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataCatalogControllerTests
{
    [Fact]
    public void Resolve_bevorzugt_injizierten_katalog_ohne_fallback_aufruf()
    {
        var injected = new Catalog();
        var fallbackCalled = false;

        var resolved = TrainingProtocolStartdataCatalogController.Resolve(
            injected,
            () =>
            {
                fallbackCalled = true;
                return new Catalog();
            });

        Assert.Same(injected, resolved);
        Assert.False(fallbackCalled);
    }

    [Fact]
    public void Resolve_verwendet_fallback_wenn_injizierter_katalog_fehlt()
    {
        var fallback = new Catalog();

        var resolved = TrainingProtocolStartdataCatalogController.Resolve(
            injectedCatalog: null,
            () => fallback);

        Assert.Same(fallback, resolved);
    }

    [Fact]
    public void EnsureAvailable_setzt_fehlstatus_auf_ui_thread_wenn_katalog_fehlt()
    {
        var calls = new List<string>();

        var available = TrainingProtocolStartdataCatalogController.EnsureAvailable(
            catalog: null,
            onUi: action =>
            {
                calls.Add("ui-before");
                action();
                calls.Add("ui-after");
            },
            setReviewStatusText: value => calls.Add("status:" + value));

        Assert.False(available);
        Assert.Equal(["ui-before", "status:Kein Code-Katalog verfuegbar.", "ui-after"], calls);
    }

    [Fact]
    public void EnsureAvailable_gibt_true_zurueck_wenn_katalog_vorhanden_ist()
    {
        var calls = new List<string>();

        var available = TrainingProtocolStartdataCatalogController.EnsureAvailable(
            new Catalog(),
            action =>
            {
                calls.Add("ui");
                action();
            },
            value => calls.Add("status:" + value));

        Assert.True(available);
        Assert.Empty(calls);
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
