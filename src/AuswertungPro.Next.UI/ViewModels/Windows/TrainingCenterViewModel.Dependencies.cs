using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public partial class TrainingCenterViewModel
{
    private readonly IKnowledgeBackupService _knowledgeBackup;
    private readonly ITrainingSampleStore _trainingSamples;
    private readonly ITrainingFrameStore _trainingFrames;
    private readonly ITrainingPreviewFrameExtractor _trainingPreviewFrames;
    private readonly ITrainingFfmpegPathResolver _trainingFfmpegPaths;
    private readonly ITrainingCenterSettingsStore _trainingSettings;
    private readonly ISelfTrainingHistoryStore _selfTrainingHistory;
    private readonly ITeacherAnnotationStore _teacherAnnotations;
    private readonly IProtocolTrainingStore _protocolTraining;
    private readonly IProcessOutputReader _processOutputs;
    private readonly TrainingYoloExportDependencies? _trainingYoloExport;
    private readonly IDialogService _dialogs;

    public TrainingCenterViewModel(
        TrainingCenterStore store,
        TrainingCenterImportService import,
        ICodeCatalogProvider? codeCatalog,
        IKnowledgeBaseDiagnosticsRunner kbDiagnostics,
        AppSettings? settings = null,
        IUiThread? uiThread = null)
        : this(
            store,
            import,
            codeCatalog,
            kbDiagnostics,
            settings,
            uiThread,
            new KnowledgeBackupTransferService())
    {
    }

    public TrainingCenterViewModel(
        TrainingCenterStore store,
        TrainingCenterImportService import,
        ICodeCatalogProvider? codeCatalog,
        IKnowledgeBaseDiagnosticsRunner kbDiagnostics,
        AppSettings? settings,
        IUiThread? uiThread,
        IKnowledgeBackupService knowledgeBackup,
        ITrainingSampleStore? trainingSamples = null,
        ITrainingPreviewFrameExtractor? trainingPreviewFrames = null,
        IDialogService? dialogs = null)
        : this(
            store,
            import,
            codeCatalog,
            kbDiagnostics,
            settings,
            uiThread,
            knowledgeBackup,
            trainingSamples,
            FrameStore.Current,
            trainingPreviewFrames,
            TrainingFfmpegPathResolver.CompatibilityService,
            TrainingCenterSettingsStore.Current,
            SelfTrainingHistoryStore.Current,
            TeacherAnnotationStore.Current,
            ProtocolTrainingStore.Current,
            ProcessOutputReader.Current,
            dialogs: dialogs)
    {
    }

    internal TrainingCenterViewModel(
        TrainingCenterStore store,
        TrainingCenterImportService import,
        ICodeCatalogProvider? codeCatalog,
        IKnowledgeBaseDiagnosticsRunner kbDiagnostics,
        AppSettings? settings,
        IUiThread? uiThread,
        IKnowledgeBackupService knowledgeBackup,
        ITrainingSampleStore? trainingSamples,
        ITrainingFrameStore trainingFrames,
        ITrainingPreviewFrameExtractor? trainingPreviewFrames,
        ITrainingFfmpegPathResolver trainingFfmpegPaths,
        ITrainingCenterSettingsStore trainingSettings,
        ISelfTrainingHistoryStore selfTrainingHistory,
        ITeacherAnnotationStore teacherAnnotations,
        IProtocolTrainingStore protocolTraining,
        IProcessOutputReader processOutputs,
        IDialogService? dialogs = null,
        TrainingYoloExportDependencies? trainingYoloExport = null)
    {
        _store = store;
        _import = import;
        _codeCatalog = codeCatalog;
        _kbDiagnostics = kbDiagnostics;
        _settings = settings;
        _uiThread = uiThread ?? UiThreadDispatcher.Instance;
        _knowledgeBackup = knowledgeBackup ?? throw new ArgumentNullException(nameof(knowledgeBackup));
        _trainingSamples = trainingSamples ?? TrainingSamplesStore.Current;
        _trainingFrames = trainingFrames ?? throw new ArgumentNullException(nameof(trainingFrames));
        _trainingPreviewFrames = trainingPreviewFrames ?? TrainingPreviewFrameExtractor.Current;
        _trainingFfmpegPaths = trainingFfmpegPaths
            ?? throw new ArgumentNullException(nameof(trainingFfmpegPaths));
        _trainingSettings = trainingSettings
            ?? throw new ArgumentNullException(nameof(trainingSettings));
        _selfTrainingHistory = selfTrainingHistory
            ?? throw new ArgumentNullException(nameof(selfTrainingHistory));
        _teacherAnnotations = teacherAnnotations
            ?? throw new ArgumentNullException(nameof(teacherAnnotations));
        _protocolTraining = protocolTraining
            ?? throw new ArgumentNullException(nameof(protocolTraining));
        _processOutputs = processOutputs
            ?? throw new ArgumentNullException(nameof(processOutputs));
        _trainingYoloExport = trainingYoloExport;
        _dialogs = dialogs ?? DialogHost.Current;
        _kbDashboard = CreateKnowledgeBaseDashboard(kbDiagnostics);
    }
}
