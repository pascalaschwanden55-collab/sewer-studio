using System.IO;
using AuswertungPro.Next.Application.Devis;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Devis;
using AuswertungPro.Next.Infrastructure.Sanierung;
using AuswertungPro.Next.UI.Composition;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Phase 5.1.A: Smoke-Test fuer den parallelen DI-Container.
/// Stellt sicher, dass alle in 5.2.A-C migrierten Services aufgeloest werden koennen
/// — ohne ServiceProvider, ohne App.Services.
/// </summary>
public sealed class ServiceCollectionConfiguratorTests
{
    [Fact]
    public void AddSewerStudioCoreServices_ResolvesAllRegisteredTypes()
    {
        EnsureConfigDirectory();

        var services = new ServiceCollection();
        services.AddSewerStudioCoreServices();
        using var provider = services.BuildServiceProvider();

        // ImportExport
        Assert.NotNull(provider.GetRequiredService<IProjectRepository>());
        Assert.NotNull(provider.GetRequiredService<IPdfImportService>());
        Assert.NotNull(provider.GetRequiredService<IXtfImportService>());
        Assert.NotNull(provider.GetRequiredService<IWinCanDbImportService>());
        Assert.NotNull(provider.GetRequiredService<IIbakImportService>());
        Assert.NotNull(provider.GetRequiredService<IKinsImportService>());
        Assert.NotNull(provider.GetRequiredService<IExcelExportService>());

        // Protocol/Reports
        Assert.NotNull(provider.GetRequiredService<IProtocolService>());
        Assert.NotNull(provider.GetRequiredService<IPhotoImportService>());
        Assert.NotNull(provider.GetRequiredService<ProtocolPdfExporter>());

        // Devis/Sanierung
        Assert.NotNull(provider.GetRequiredService<IDevisGenerator>());
        Assert.NotNull(provider.GetRequiredService<DevisExcelExporter>());
        Assert.NotNull(provider.GetRequiredService<SubmissionsPositionService>());
        Assert.NotNull(provider.GetRequiredService<HistorischeSanierungenService>());
        Assert.NotNull(provider.GetRequiredService<MarktdatenImportService>());
        Assert.NotNull(provider.GetRequiredService<RehabilitationRulesEngine>());
        Assert.NotNull(provider.GetRequiredService<SanierungUserRulesService>());
    }

    [Fact]
    public void AddSewerStudioCoreServices_RegistersServicesAsSingletons()
    {
        EnsureConfigDirectory();

        var services = new ServiceCollection();
        services.AddSewerStudioCoreServices();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IProjectRepository>();
        var second = provider.GetRequiredService<IProjectRepository>();
        Assert.Same(first, second);

        var firstDevis = provider.GetRequiredService<IDevisGenerator>();
        var secondDevis = provider.GetRequiredService<IDevisGenerator>();
        Assert.Same(firstDevis, secondDevis);
    }

    /// <summary>
    /// DevisSanierungModule liest JSON-Files aus AppContext.BaseDirectory/Config.
    /// Im Test-Run sind die Files nicht vorhanden — lege leere JSON-Dateien an,
    /// damit die Service-Konstruktoren nicht werfen.
    /// </summary>
    private static void EnsureConfigDirectory()
    {
        var configDir = Path.Combine(System.AppContext.BaseDirectory, "Config");
        Directory.CreateDirectory(configDir);

        EnsureFile(Path.Combine(configDir, "devis_mappings.json"), "{}");
        EnsureFile(Path.Combine(configDir, "submission_positionen.json"), "{}");
        EnsureFile(Path.Combine(configDir, "historische_sanierungen.json"), "{}");
        EnsureFile(Path.Combine(configDir, "sanierung_user_rules.json"), "{}");
        EnsureFile(Path.Combine(configDir, "rehabilitation_methods.json"), "{}");
    }

    private static void EnsureFile(string path, string content)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, content);
    }
}
