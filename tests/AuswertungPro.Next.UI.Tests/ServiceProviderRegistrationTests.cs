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
        // 138 -> 139: IImportedFileLedger (Gesamtaudit 2026-08-14, P1-5: nimmt die Dateien
        // eines verworfenen Ein-Knopf-Imports zurueck, statt sie unbemerkt liegen zu lassen).
        // 139 -> 140: IShaftDistributionService kapselt die transaktionale manuelle
        // Schachtverteilung (grosser Audit-Umbau 2026-08-14).
        // 140 -> 141: ITrainingCenterDocumentStore verschiebt die JSON-Dateiarbeit
        // aus der eingefrorenen UI-Fassade in die Infrastructure.
        // 141 -> 143: INpkOfferPdfExportService und IPdfPrintService (Wiederholungsaudit
        // 2026-08-14, P2-3: der NPK-Weg baute Vorlagenpfade selbst, erzeugte direkt einen
        // Renderer und startete Process.Start im ViewModel).
        // 143 -> 144: IImportPdfReferenceResolver ordnet Herstellernamen wie
        // "Section_8_892037-74091.pdf" einer bereits vorhandenen Haltung/einem Schacht zu.
        // Ohne ihn fielen im Projekt Hellgasse alle 38 Haltungsprotokolle still heraus.
        // 144 -> 145: IProtocolPdfDateReader liest das Protokolldatum mit derselben
        // Textquelle wie die Verteilung. Vorher hiess dieselbe Datei je nach Weg anders
        // ("20231010_80783.pdf" nach dem Verteilen, "00000000_80783.pdf" nach dem Import).
        // 145 -> 146: IDistributionReconciliationService ("Abgleichen") verschiebt aus
        // Haltungen_Verteilt und Schaechte_Verteilt alles ohne Gegenstueck im Projekt in
        // den Papierkorb.
        // 146 -> 150: Eigentuemerdossier (IDossierStore, IDossierWordExportService,
        // IDossierAttachmentService, IDossierPdfAssemblyService). Buendelt mehrere
        // Haltungen einer Liegenschaft zu einem Dossier fuer den Eigentuemer.
        // 150 -> 151: IDossierOutputPreviewService erzeugt die Vorschau aus dem
        // echten Word-/PDF-Weg statt aus einer nachgezeichneten WPF-Seite.
        // 151 -> 152: IDossierPlanPublicationService veroeffentlicht einen bearbeiteten
        // Plan nur innerhalb des Projekts und liefert den sicheren Rueckbau-Beleg.
        // 152 -> 153: IProtocolPdfLayoutSettings liefert Exporter und Dossierdialog
        // dieselbe Live-Einstellung, ohne settings.json beim Klick erneut zu laden.
        // 153 -> 154: IDossierComponentListExportService erzeugt Haltungs- und
        // Schachtlisten bewusst aus dem aktuellen Stand des Eigentuemerdossiers.
        // 154 -> 152: Kartenansicht entfernt (IOfflineBasemapPathResolver,
        // IKarteBasemapLayerFactory). Die raeumliche Arbeit laeuft ueber QGIS.
        // 152 -> 153: IQgisBestandLeser liest die lokalen GeoPackage-Kopien des
        // Abwassernetzes fuer "Leere Felder aus QGIS ergaenzen" — offline, ohne den
        // gedrosselten Netzdienst des Kantons.
        // 153 -> 154: IXtfNeuExportService schreibt eine NEUE SIA405-Datei fuer Objekte
        // ohne Katastervorlage. Der Revisionsweg braucht eine Originaldatei; private
        // Anschlussleitungen haben keine.
        // 154 -> 155: IXtfExportVorschauDialog zeigt vor dem XTF-Schreiben die Alt/Neu-Tabelle
        // im eigenen Fenster; der Ablauf liegt im UseCase, das ViewModel verdrahtet nur.
        // 155 -> 156: IPipeEndSuggestionScanService fragt die freigegebenen Bild-Einordner
        // fuer Rohranfang und Rohrende (Sidecar /classify/lernstufe) im Vorabdurchlauf des
        // Training Studios — derselbe Weg wie der Bogen-Copilot, ohne eigenen Client.
        // 156 -> 157: IKatasterKennungLeser liest die SIA405-Kennungen aus der GEONIS-Kopie
        // fuer "Katasterkennungen ergaenzen" — die QGIS-Kopien (Lisag-WFS) tragen nur eine Lisag-Nummer,
        // die beim Veroeffentlichen wechselt; ohne die GEONIS-Kennung legt ein Import Duplikate an.
        Assert.True(
            registrations.Count == 157,
            $"Erwartet 157 Registrierungen, tatsaechlich {registrations.Count}. Bei einem neuen " +
            "Dienst die Registrierung in ServiceProviderRegistrationMap ergaenzen und diese Zahl " +
            "bewusst anpassen.");
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
