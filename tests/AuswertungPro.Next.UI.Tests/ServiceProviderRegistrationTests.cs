using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
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
        // Bewusster Tripwire: die Zahl zwingt bei jedem neuen/entfernten Dienst zu einer
        // Entscheidung. Die Meldung nennt den Grund, statt nur eine nackte Zahl-Abweichung
        // zu zeigen (frueherer Kritikpunkt: nichtssagend).
        // 132 -> 135: Bogen-Vorschlaege (Auftrag Paket 4): IBendSuggestionScanService,
        // ICodingSuggestionExposure, IVideoClipExtractor.
        // 135 -> 136: ISchachtProtocolFileLocator (Schachtprotokoll "Aktualisieren" findet die
        // PDF auch bei absoluter Verknuepfung oder umbenannter Datei im eigenen Schachtordner).
        // 136 -> 137: IXtfRevisionExportService (revidierte XTF aus dem aktuellen Projektstand).
        // 137 -> 138: IProgramSnapshotService (Programmstand als eine ZIP-Datei fuer Ziele,
        // an denen hunderttausende Einzeldateien nicht taugen, z. B. ein Cloud-Ordner).
        Assert.True(
            registrations.Count == 138,
            $"Erwartet 138 Registrierungen, tatsaechlich {registrations.Count}. Bei einem neuen " +
            "Dienst die Registrierung in ServiceProviderRegistrationMap ergaenzen und diese Zahl " +
            "bewusst anpassen.");
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

    private static ServiceProvider CreateServices(ILoggerFactory loggerFactory)
        => new(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
}
