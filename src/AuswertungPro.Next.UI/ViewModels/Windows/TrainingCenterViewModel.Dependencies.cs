using System;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public partial class TrainingCenterViewModel
{
    private readonly IKnowledgeBackupService _knowledgeBackup;
    private readonly ITrainingSampleStore _trainingSamples;

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
        ITrainingSampleStore? trainingSamples = null)
    {
        _store = store;
        _import = import;
        _codeCatalog = codeCatalog;
        _kbDiagnostics = kbDiagnostics;
        _settings = settings;
        _uiThread = uiThread ?? UiThreadDispatcher.Instance;
        _knowledgeBackup = knowledgeBackup ?? throw new ArgumentNullException(nameof(knowledgeBackup));
        _trainingSamples = trainingSamples ?? TrainingSamplesStore.Current;
        _kbDashboard = CreateKnowledgeBaseDashboard(kbDiagnostics);
    }
}
