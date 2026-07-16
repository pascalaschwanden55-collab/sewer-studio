using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShaftPdfSelectionExpanderDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_die_SchachtPdfAuswahlErweiterung()
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
            services.GetService(typeof(IShaftPdfSelectionExpander)));
    }

    [Fact]
    public void Statische_SchachtPdfAuswahlFassade_ist_unveraenderbar()
    {
        var before = ShaftPdfSelectionExpander.Current;
        var use = typeof(ShaftPdfSelectionExpander).GetMethod(nameof(ShaftPdfSelectionExpander.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [new ShaftPdfSelectionExpansionService()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, ShaftPdfSelectionExpander.Current);
    }
}
