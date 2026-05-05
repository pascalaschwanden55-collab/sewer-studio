using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Devis;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Devis;
using AuswertungPro.Next.Infrastructure.Sanierung;
using AuswertungPro.Next.UI.Modules;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Composition;

/// <summary>
/// Phase 5.1.A: Erstes Skelett des DI-Containers (Microsoft.Extensions.DependencyInjection).
///
/// Parallel zu ServiceProvider — beide existieren gleichzeitig waehrend der Migration:
///   - ServiceProvider liefert weiter alle Aufrufer ueber App.Services (legacy)
///   - Dieser DI-Container ist initial verfuegbar, aber von keinem Aufrufer benutzt
///
/// In Etappe 2 werden Aufrufer schrittweise auf Constructor-Injection umgestellt;
/// in Etappe 4 wird ServiceProvider als Compatibility-Shim reduziert oder geloescht.
///
/// Aktuell registriert nur die bereits migrierten Modul-Services (5.2.A-C):
///   - ImportExportModule (Projects, PdfImport, XtfImport, WinCanImport, IbakImport,
///     KinsImport, ExcelExport)
///   - ProtocolReportsModule (Protocols, PhotoImport, ProtocolPdfExporter)
///   - DevisSanierungModule (DevisGenerator, DevisExcelExporter, SubmissionsPositions,
///     HistorischeSanierungen, MarktdatenImport, RehabRulesEngine, SanierungUserRules)
///
/// AiPipeline + KnowledgeBase + VsaCatalogResolver bleiben fuer Etappe 2 offen,
/// weil sie Lifecycle-Tasks (Warmup, BrainMirror) und Disposable-State haben.
/// </summary>
public static class ServiceCollectionConfigurator
{
    /// <summary>
    /// Registriert die heute migrierten Domain-IO-, Reports- und Devis/Sanierungs-
    /// Services im IServiceCollection. Erfordert AppSettings + Logger als Singleton
    /// vorab registriert.
    /// </summary>
    public static IServiceCollection AddSewerStudioCoreServices(this IServiceCollection services)
    {
        // Phase 5.2.A — ImportExport
        var importExport = ImportExportModule.Configure();
        services.AddSingleton(importExport.Projects);
        services.AddSingleton(importExport.PdfImport);
        services.AddSingleton(importExport.XtfImport);
        services.AddSingleton(importExport.WinCanImport);
        services.AddSingleton(importExport.IbakImport);
        services.AddSingleton(importExport.KinsImport);
        services.AddSingleton(importExport.ExcelExport);

        // Phase 5.2.B — Protocol/Reports
        var protocolReports = ProtocolReportsModule.Configure();
        services.AddSingleton(protocolReports.Protocols);
        services.AddSingleton(protocolReports.PhotoImport);
        services.AddSingleton(protocolReports.ProtocolPdfExporter);

        // Phase 5.2.C — Devis/Sanierung
        var devisSanierung = DevisSanierungModule.Configure();
        services.AddSingleton(devisSanierung.DevisGenerator);
        services.AddSingleton(devisSanierung.DevisExcelExporter);
        services.AddSingleton(devisSanierung.SubmissionsPositions);
        services.AddSingleton(devisSanierung.HistorischeSanierungen);
        services.AddSingleton(devisSanierung.MarktdatenImport);
        services.AddSingleton(devisSanierung.RehabRulesEngine);
        services.AddSingleton(devisSanierung.SanierungUserRules);

        // Cross-cutting UI services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ErrorCodeGenerator>();

        return services;
    }

    /// <summary>
    /// Convenience: Settings + Logger registrieren.
    /// Wird von App.OnStartup vor AddSewerStudioCoreServices aufgerufen.
    /// </summary>
    public static IServiceCollection AddSewerStudioInfrastructure(
        this IServiceCollection services,
        AppSettings settings,
        DiagnosticsOptions diagnostics,
        ILogger logger,
        ILoggerFactory loggerFactory)
    {
        services.AddSingleton(settings);
        services.AddSingleton(diagnostics);
        services.AddSingleton(logger);
        services.AddSingleton(loggerFactory);
        return services;
    }
}
