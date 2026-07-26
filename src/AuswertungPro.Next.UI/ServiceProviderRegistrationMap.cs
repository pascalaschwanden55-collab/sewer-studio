using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Application.Vsa;

using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Common;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Diagnostics;
using AuswertungPro.Next.Infrastructure.Export;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Protocol;
using AuswertungPro.Next.Infrastructure.Settings;
using AuswertungPro.Next.Infrastructure.Telemetry;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.Infrastructure.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Reports;

using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Ordnet die bereits erzeugten, zentralen Dienstinstanzen ihren Vertragstypen zu.
/// Diese Klasse erzeugt selbst keine Dienste.
/// </summary>
internal static class ServiceProviderRegistrationMap
{
    public static IReadOnlyDictionary<Type, object> Create(ServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new Dictionary<Type, object>
        {
            [typeof(IFullBackupService)] = services.FullBackup,
            [typeof(IKnowledgeRealtimeMirrorService)] = services.KnowledgeRealtimeMirror,
            [typeof(IFullBackupSourcesProvider)] = services.BackupSources,
            [typeof(IBackupTargetMarkerGuard)] = services.BackupTargetMarkers,
            [typeof(ISqliteSnapshotCopier)] = services.SqliteSnapshots,
            [typeof(IBackupManifestIntegrityService)] = services.BackupManifestIntegrity,
            [typeof(ISettingsRestorePointStore)] = services.SettingsRestorePoints,
            [typeof(ISettingsFileStore)] = services.SettingsFiles,
            [typeof(ISettingsQuarantineStore)] = services.SettingsQuarantine,
            [typeof(ISettingsMigrationService)] = services.SettingsMigration,
            [typeof(IExplorerRevealService)] = services.ExplorerReveal,
            [typeof(ISafeShellOpenService)] = services.ShellOpen,
            [typeof(IFolderOpenService)] = services.FolderOpen,
            [typeof(IProgramRootLocator)] = services.ProgramRootLocator,
            [typeof(IRepositoryRootLocator)] = services.RepositoryRootLocator,
            [typeof(IFfmpegExecutableLocator)] = services.FfmpegExecutables,
            [typeof(IProcessOutputReader)] = services.ProcessOutputs,
            [typeof(IVideoFrameExtractor)] = services.VideoFrameExtraction,
            [typeof(ITrainingFfmpegPathResolver)] = services.TrainingFfmpegPaths,
            [typeof(ISidecarScriptLocator)] = services.SidecarScripts,
            [typeof(ISidecarTokenResolver)] = services.SidecarTokens,
            [typeof(IAiStartedProcessLifetime)] = services.AiStartedProcesses,
            [typeof(IVsaYoloClassMapStore)] = services.VsaYoloClasses,
            [typeof(ITrainingYoloClassMapStore)] = services.TrainingYoloClasses,
            [typeof(IKatasterXtfPathResolver)] = services.KatasterXtfPaths,
            [typeof(IHaltungCadastreTableStore)] = services.HaltungCadastreTables,
            [typeof(IHaltungCadastreIndexProvider)] = services.HaltungCadastreIndexes,
            [typeof(IOfflineBasemapPathResolver)] = services.OfflineBasemapPaths,
            [typeof(Mapping.IKarteBasemapLayerFactory)] = services.BasemapLayers,
            [typeof(IVsaCatalogPathResolver)] = services.VsaCatalogPaths,
            [typeof(IGitCommitResolver)] = services.GitCommit,
            [typeof(IProjectRepository)] = services.Projects,
            [typeof(ICostStoreFactory)] = services.CostStores,
            [typeof(IProjectPhotoReferenceNormalizer)] = services.ProjectPhotoReferences,
            [typeof(IProjectFileDiscovery)] = services.ProjectFileDiscovery,
            [typeof(IProjectOverviewCatalog)] = services.ProjectOverviewCatalog,
            [typeof(IProjectDropPathResolver)] = services.ProjectDropPaths,
            [typeof(IProjectStructureInitializer)] = services.ProjectStructure,
            [typeof(IKiasExportPatternDetector)] = services.KiasExportPatterns,
            [typeof(IKanalExportDetectionService)] = services.KanalExportDetection,
            [typeof(ISchaechteTemplateColumnReader)] = services.SchaechteTemplateColumns,
            [typeof(IVideoStartErrorLogWriter)] = services.VideoStartErrorLogs,
            [typeof(IKnowledgeWalCheckpoint)] = services.KnowledgeWalCheckpoint,
            [typeof(IKnowledgeBaseHealthInspector)] = services.KnowledgeBaseHealth,
            [typeof(IKnowledgeBasePathService)] = services.KnowledgePaths,
            [typeof(IPdfFileSafetyChecker)] = services.PdfFileSafety,
            [typeof(IAtomicPdfFileReplacer)] = services.PdfFileReplacement,
            [typeof(IPdfTextExtractor)] = services.PdfTextExtraction,
            [typeof(IPdfOcrExtractor)] = services.PdfOcrExtraction,
            [typeof(ISchachtProtocolOcrReader)] = services.SchachtProtocolOcr,
            [typeof(IPdfFormFieldReader)] = services.PdfFormFields,
            [typeof(IPdfTextPrefixReader)] = services.PdfTextPrefixes,
            [typeof(IPdfImportService)] = services.PdfImport,
            [typeof(IPdfTextLayerRewriter)] = services.PdfTextLayerRewrite,
            [typeof(IDistributionPdfPageReader)] = services.DistributionPdfPages,
            [typeof(IDistributionFileTransfer)] = services.DistributionFileTransfers,
            [typeof(IVideoConflictCandidateCopier)] = services.VideoConflictCandidates,
            [typeof(IShaftPdfSelectionExpander)] = services.ShaftPdfSelectionExpansion,
            [typeof(INameBasedProtocolDistributor)] = services.NameBasedProtocolDistributor,
            [typeof(IVsaMediaPathResolver)] = services.VsaMediaPaths,
            [typeof(IXtfHoldingFileReader)] = services.XtfHoldingFiles,
            [typeof(IM150SourceFileReader)] = services.M150SourceFiles,
            [typeof(IM150MdbRowReader)] = services.M150MdbRows,
            [typeof(IXtfImportService)] = services.XtfImport,
            [typeof(IWinCanDbImportService)] = services.WinCanImport,
            [typeof(IXtfStammdatenSourceReader)] = services.XtfStammdatenSources,
            [typeof(IIbakPdfStammdatenSourceReader)] = services.IbakPdfStammdatenSources,
            [typeof(IIbakFdbConnectionOptions)] = services.IbakConnections,
            [typeof(IIbakImportService)] = services.IbakImport,
            [typeof(IKinsImportService)] = services.KinsImport,
            [typeof(IKinsDvdTextEnricher)] = services.KinsDvdTextEnrichment,
            [typeof(IKinsDbfWhitelistEnricher)] = services.KinsDbfWhitelistEnrichment,
            [typeof(IKinsGesamtprotokollLocator)] = services.KinsGesamtprotokolle,
            [typeof(IImportRunReportExporter)] = services.ImportRunReports,
            [typeof(IStoredImportFileService)] = services.StoredImportFiles,
            [typeof(IStoredImportFilePathResolver)] = services.StoredImportFilePaths,
            [typeof(IImportFileStagingService)] = services.ImportFileStaging,
            [typeof(IImportMediaDistributionService)] = services.ImportMediaDistribution,
            [typeof(ISchachtProtocolImportService)] = services.SchachtProtocolImport,
            [typeof(ISchachtStammdatenErgaenzungsService)] = services.SchachtStammdatenErgaenzung,
            [typeof(IExcelExportService)] = services.ExcelExport,
            [typeof(INpkLeistungsverzeichnisExcelExporter)] = services.NpkExcelExport,
            [typeof(IDistributionPatternResolver)] = services.DistributionPatterns,
            [typeof(IDistributionDirectoryTreeResolver)] = services.DistributionDirectoryTree,
            [typeof(IVsaEvaluationService)] = services.Vsa,
            [typeof(IVsaShadowTelemetryWriter)] = services.VsaShadowTelemetry,
            [typeof(ISidecarTelemetryWriter)] = services.SidecarTelemetry,
            [typeof(IPipelineTraceWriter)] = services.PipelineTrace,
            [typeof(ITelemetryPathResolver)] = services.TelemetryPaths,
            [typeof(IGpuModelSelector)] = services.GpuModels,
            [typeof(IAiPlatformSettingsResolver)] = services.AiSettings,
            [typeof(IPipelineEnvironmentOptions)] = services.PipelineEnvironment,
            [typeof(IProtocolService)] = services.Protocols,
            [typeof(IPdfMergeService)] = services.PdfMerge,
            [typeof(AuswertungPro.Next.Application.Output.IOfferPdfExportService)] = services.OfferPdfExport,
            [typeof(IDossierPhotoAvailabilityService)] = services.DossierPhotoAvailability,
            [typeof(IInspectionProtocolFileLocator)] = services.InspectionProtocolFiles,
            [typeof(IDichtheitProtocolFileLocator)] = services.DichtheitProtocolFiles,
            [typeof(ISchachtFileTargetResolver)] = services.SchachtFileTargets,
            [typeof(IProtocolTrainingStore)] = services.ProtocolTraining,
            [typeof(ITrainingCenterSettingsStore)] = services.TrainingSettings,
            [typeof(ITrainingCaseIdSource)] = services.TrainingCases,
            [typeof(ISelfTrainingHistoryStore)] = services.SelfTrainingHistory,
            [typeof(ITeacherAnnotationStore)] = services.TeacherAnnotations,
            [typeof(IAiOptimizationSessionStore)] = services.AiOptimizationSessions,
            [typeof(ITrainingSampleStore)] = services.TrainingSamples,
            [typeof(IPersonalGoldAlbumService)] = services.PersonalGoldAlbum,
            [typeof(IPersonalGoldInboxService)] = services.PersonalGoldInbox,
            [typeof(ITrainingDataInventoryService)] = services.TrainingDataInventory,
            [typeof(ITrainingExportRegistryStore)] = services.TrainingExportRegistry,
            [typeof(ITrainingExportPlanInputBuilder)] = services.TrainingExportPlanInput,
            [typeof(ITrainingExportPlanService)] = services.TrainingExportPlans,
            [typeof(ITrainingExportSidecarRequestBuilder)] = services.TrainingExportSidecarRequests,
            [typeof(ITrainingExportPlanLocalExecutor)] = services.TrainingExportLocalExecutor,
            [typeof(ITrainingExportCompletionService)] = services.TrainingExportCompletion,
            [typeof(ITrainingExportExecutionService)] = services.TrainingExportExecution,
            [typeof(ITrainingYoloExportCoordinator)] = services.TrainingYoloExportCoordinator,
            [typeof(TrainingYoloExportDependencies)] = services.TrainingYoloExport,
            [typeof(ITrainingFrameStore)] = services.TrainingFrames,
            [typeof(ITrainingPreviewFrameExtractor)] = services.TrainingPreviewFrames,
            [typeof(ICodingFramePhotoStore)] = services.CodingFramePhotos,
            [typeof(ICodingDefectPreviewRenderer)] = services.CodingDefectPreviews,
            [typeof(IKnowledgeBaseDiagnosticsRunner)] = services.KnowledgeBaseDiagnostics,
            [typeof(IDiagnosticsPackageService)] = services.DiagnosticsPackages,
            [typeof(AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider)] = services.CodeCatalog,
            [typeof(AuswertungPro.Next.Application.Protocol.IVsaCodeSelectionCatalog)] = services.CodeSelectionCatalog,
            [typeof(ILogger)] = services.Logger,
            [typeof(ILoggerFactory)] = services.LoggerFactory,
            [typeof(IStatusColorService)] = services.StatusColors,
            [typeof(ICodeUsageTracker)] = services.CodeUsage,
        };
    }
}
