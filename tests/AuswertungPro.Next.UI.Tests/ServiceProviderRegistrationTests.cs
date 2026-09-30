using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.UI.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderRegistrationTests
{
    [Fact]
    public void GetService_liefert_die_bereits_erzeugte_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);

        Assert.Same(services.Projects, services.GetService(typeof(IProjectRepository)));
        Assert.Same(services.ProjektPruefung, services.GetService(typeof(AuswertungPro.Next.Application.UseCases.ProjektPruefung.IProjektPruefung)));
        Assert.Same(
            services.ProtocolPdfLayoutSettings,
            services.GetService(typeof(IProtocolPdfLayoutSettings)));
    }

    [Fact]
    public void GetService_wirft_bei_einem_unbekannten_Typ_sichtbar()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);

        var error = Assert.Throws<InvalidOperationException>(
            () => services.GetService(typeof(ServiceProviderRegistrationTests)));

        Assert.Contains(typeof(ServiceProviderRegistrationTests).FullName!, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetService_wirft_bei_fehlendem_Typ_sichtbar()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);

        Assert.Throws<ArgumentNullException>(() => services.GetService(null!));
    }

    [Fact]
    public void Zentrale_Registrierung_enthaelt_alle_bisherigen_Dienste()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);
        var field = typeof(ServiceProvider).GetField(
            "_services",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var registrations = Assert.IsAssignableFrom<IReadOnlyDictionary<Type, object>>(field!.GetValue(services));
        // Keine feste Zahl: ob jeder Dienst eingetragen ist, prueft
        // Jede_Schnittstellen_Eigenschaft_ist_registriert (Abgleich per Reflection).
        Assert.Same(services.XtfLieferungen,
            registrations[typeof(AuswertungPro.Next.Application.Xtf.Lieferung.IXtfLieferungsAblage)]);
        Assert.Same(services.BackupAdditionalFolders,
            registrations[typeof(AuswertungPro.Next.Application.Backup.IBackupAdditionalFolders)]);
        Assert.Same(
            services.DossierPlanPublications,
            registrations[typeof(AuswertungPro.Next.Application.Dossiers.IDossierPlanPublicationService)]);
        Assert.Same(
            services.DossierComponentLists,
            registrations[typeof(AuswertungPro.Next.Application.Dossiers.IDossierComponentListExportService)]);
        Assert.Same(
            services.ProtocolPdfLayoutSettings,
            registrations[typeof(IProtocolPdfLayoutSettings)]);
        var exporterSettingsField = typeof(ProtocolPdfExporter).GetField(
            "_layoutSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(exporterSettingsField);
        Assert.Same(
            services.ProtocolPdfLayoutSettings,
            exporterSettingsField!.GetValue(services.ProtocolPdfExporter));
        Assert.Same(
            services.ProjectOverviewCatalog,
            registrations[typeof(IProjectOverviewCatalog)]);
        Assert.Same(
            services.KnowledgeRealtimeMirror,
            registrations[typeof(IKnowledgeRealtimeMirrorService)]);
        Assert.Same(
            services.StoredImportFiles,
            registrations[typeof(IStoredImportFileService)]);
        Assert.Same(
            services.StoredImportFilePaths,
            registrations[typeof(IStoredImportFilePathResolver)]);
        Assert.Same(
            services.ImportFileStaging,
            registrations[typeof(IImportFileStagingService)]);
        Assert.Same(
            services.ShaftDistribution,
            registrations[typeof(IShaftDistributionService)]);
        Assert.Same(
            services.TrainingCenterDocuments,
            registrations[typeof(ITrainingCenterDocumentStore)]);
        Assert.Same(
            services.ImportMediaDistribution,
            registrations[typeof(IImportMediaDistributionService)]);
        Assert.Same(
            services.TrainingDataInventory,
            registrations[typeof(ITrainingDataInventoryService)]);
        Assert.Same(
            services.PersonalGoldAlbum,
            registrations[typeof(IPersonalGoldAlbumService)]);
        Assert.Same(
            services.PersonalGoldInbox,
            registrations[typeof(IPersonalGoldInboxService)]);
        Assert.Same(
            services.TrainingPdfReviews,
            registrations[typeof(ITrainingPdfReviewImportService)]);
        Assert.Same(
            services.TrainingExportRegistry,
            registrations[typeof(ITrainingExportRegistryStore)]);
        Assert.Same(
            services.TrainingExportPlanInput,
            registrations[typeof(ITrainingExportPlanInputBuilder)]);
        Assert.Same(
            services.TrainingExportPlans,
            registrations[typeof(ITrainingExportPlanService)]);
        Assert.Same(
            services.TrainingExportSidecarRequests,
            registrations[typeof(ITrainingExportSidecarRequestBuilder)]);
        Assert.Same(
            services.TrainingExportLocalExecutor,
            registrations[typeof(ITrainingExportPlanLocalExecutor)]);
        Assert.Same(
            services.TrainingExportCompletion,
            registrations[typeof(ITrainingExportCompletionService)]);
        Assert.Same(
            services.TrainingExportExecution,
            registrations[typeof(ITrainingExportExecutionService)]);
        Assert.Same(
            services.TrainingYoloExportCoordinator,
            registrations[typeof(ITrainingYoloExportCoordinator)]);
        Assert.Same(
            services.TrainingYoloExport,
            registrations[typeof(TrainingYoloExportDependencies)]);
        Assert.Same(
            services.TrainingYoloExportCoordinator,
            services.TrainingYoloExport.Coordinator);
        Assert.Same(
            services.BendSuggestionScan,
            registrations[typeof(IBendSuggestionScanService)]);
        Assert.Same(
            services.CodingSuggestionExposure,
            registrations[typeof(ICodingSuggestionExposure)]);
        Assert.Same(
            services.CodingSuggestionRegistry,
            registrations[typeof(ICodingSuggestionRegistry)]);
        Assert.Same(
            services.VideoClipExtraction,
            registrations[typeof(IVideoClipExtractor)]);
        // Echte Invariante statt tautologischem GetService-Selbstvergleich: jeder registrierte
        // Wert muss tatsaechlich eine Instanz seines Vertragstyps sein. Das faengt eine vertippte
        // Zuordnung [typeof(IFoo)] = services.Bar ab, die der Compiler nicht bemerkt (der
        // Dictionary-Wert ist object).
        Assert.All(registrations, registration =>
        {
            Assert.NotNull(registration.Value);
            Assert.True(
                registration.Key.IsInstanceOfType(registration.Value),
                $"Registrierung {registration.Key.Name} -> {registration.Value.GetType().Name} " +
                "passt nicht zum Vertragstyp.");
        });
    }

    /// <summary>
    /// Oeffentliche ServiceProvider-Eigenschaften mit Schnittstellentyp, die NICHT in der
    /// Registrierungskarte stehen (Stand 30.09.2026). Grund fuer alle: Die Dienste werden nur ueber
    /// ihre Eigenschaft gelesen, kein Aufrufer fragt sie per GetService(Typ) ab. Neue Dienste gehoeren
    /// in die Karte, nicht in diese Liste. Wird ein Eintrag registriert oder entfernt, verlangt
    /// Ausnahmeliste_enthaelt_keine_verwaisten_Eintraege, ihn hier zu streichen - die Liste sinkt nur.
    /// </summary>
    private static readonly HashSet<string> NichtRegistriert = new(StringComparer.Ordinal)
    {
        "Dialogs",
        "DropdownOptions",
        "PlaywrightInstaller",
        "LogTailReader",
        "KnowledgeBackup",
        "CodexArtifactCleanup",
        "ProjectContentSignature",
        "ProtocolAi",
        "Retrieval",
        "MeasureRecommendation",
        "VideoAnalysisPipelines",
        "SanierungOptimizations",
        "SchachtMassnahmenKatalog",
        "SchattenStore",
        "CostFieldSync",
        "ImportTransactionJournal",
        "ImportTransactionRecovery",
        "PhotoImport",
        "ProjectPortability",
        "ProjectPhotoAssignment",
        "HoldingRename",
        "ShaftRename",
        "PlanPdfImport",
        "ProtocolRegeneration",
        "ProtocolSingleRegeneration",
        "OneClickImportReports",
        "ImportSummaryExporter",
        "ProjectRestorePoints",
        "ProjectRecovery",
        "ImportSourceArchiver",
        "DichtheitImportDistributor",
        "KanalImportDistributor",
        "ProtocolPdfExports",
        "DossierParcels",
        "DossierLandRegistry",
        "DossierSewerNetwork",
        "DossierSchachtNetz",
        "DossierDirectory",
        "DossierPlanImages",
        "DossierPlanAdjuster",
        "DossierPreviewPages"
    };

    [Fact]
    public void Jede_Schnittstellen_Eigenschaft_ist_registriert()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);
        var registrations = LeseRegistrierungen(services);
        var eigenschaften = OeffentlicheEigenschaften();

        var fehlend = eigenschaften
            .Where(p => p.PropertyType.IsInterface
                && !NichtRegistriert.Contains(p.Name)
                && !IstRegistriert(p, services, registrations))
            .Select(p => $"{p.Name} ({p.PropertyType.Name})")
            .ToList();

        Assert.True(
            fehlend.Count == 0,
            "Diese Dienst-Eigenschaften fehlen in ServiceProviderRegistrationMap: " +
            string.Join(", ", fehlend) + ". In ServiceProviderRegistrationMap eintragen.");
    }

    [Fact]
    public void Jeder_Karteneintrag_gehoert_zu_einer_Eigenschaft()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);
        var registrations = LeseRegistrierungen(services);
        var werte = OeffentlicheEigenschaften()
            .Select(p => p.GetValue(services))
            .Where(v => v is not null)
            .ToList();

        var verwaist = registrations
            .Where(r => !werte.Any(w => ReferenceEquals(w, r.Value)))
            .Select(r => r.Key.Name)
            .ToList();

        Assert.True(
            verwaist.Count == 0,
            "Diese Karteneintraege gehoeren zu keiner oeffentlichen Eigenschaft: " +
            string.Join(", ", verwaist));
    }

    [Fact]
    public void Ausnahmeliste_enthaelt_keine_verwaisten_Eintraege()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);
        var registrations = LeseRegistrierungen(services);
        var eigenschaften = OeffentlicheEigenschaften().ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var name in NichtRegistriert)
        {
            Assert.True(
                eigenschaften.TryGetValue(name, out var eigenschaft) && eigenschaft.PropertyType.IsInterface,
                $"Ausnahme {name} ist keine oeffentliche Schnittstellen-Eigenschaft mehr.");
            Assert.False(
                IstRegistriert(eigenschaft!, services, registrations),
                $"Ausnahme {name} ist inzwischen registriert und gehoert aus der Liste.");
        }
    }

    private static IReadOnlyDictionary<Type, object> LeseRegistrierungen(ServiceProvider services)
    {
        var field = typeof(ServiceProvider).GetField(
            "_services",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<IReadOnlyDictionary<Type, object>>(field!.GetValue(services));
    }

    // Alle Teildateien der partiellen Klasse liegen im selben Typ; Indexer und statische
    // Eigenschaften sind keine Dienste.
    private static List<PropertyInfo> OeffentlicheEigenschaften()
        => typeof(ServiceProvider)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is not null)
            .ToList();

    private static bool IstRegistriert(
        PropertyInfo eigenschaft,
        ServiceProvider services,
        IReadOnlyDictionary<Type, object> registrations)
    {
        var wert = eigenschaft.GetValue(services);
        return wert is not null && registrations.Values.Any(v => ReferenceEquals(v, wert));
    }

    private static ServiceProvider CreateServices(ILoggerFactory loggerFactory)
        => new(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
}
