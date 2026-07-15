using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShaftPdfSelectionExpanderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Kompatibilitaetsfassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<ShaftPdfSelectionExpansionService>(services.ShaftPdfSelectionExpansion);
        Assert.Same(
            services.ShaftPdfSelectionExpansion,
            ShaftPdfSelectionExpander.Current);
        Assert.Same(
            services.ShaftPdfSelectionExpansion,
            services.GetService(typeof(IShaftPdfSelectionExpander)));
    }
}
