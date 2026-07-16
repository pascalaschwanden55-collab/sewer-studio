using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowDependenciesTests
{
    [Fact]
    public void From_null_provider_exposes_empty_dependencies()
    {
        var dependencies = PlayerWindowDependencies.From(null);

        Assert.Null(dependencies.Settings);
        Assert.Null(dependencies.CodeCatalog);
        Assert.Null(dependencies.CodeSelectionCatalog);
        Assert.Null(dependencies.PipelineConfig);
        Assert.Null(dependencies.ProtocolPdfExporter);
        Assert.NotNull(dependencies.CodingDefectPreviews);
        Assert.NotNull(dependencies.Dialogs);
        Assert.Null(dependencies.LoggerFactory);
        Assert.Null(dependencies.LastProjectPath);
        Assert.False(dependencies.HasCodeCatalog);
        Assert.Null(dependencies.LegacyServiceProvider);
    }
}
