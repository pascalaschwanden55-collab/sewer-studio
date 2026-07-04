using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingGoldKbReconcileRuntimeServices
{
    public TrainingGoldKbReconcileRuntimeServices(
        Func<string, IProgress<string>?, CancellationToken, Task<KnowledgeBackupService.BackupResult>> ExportBackupAsync,
        Func<string> GetKnowledgeBaseRoot,
        Func<DateTime> GetNow,
        Action<string> CreateDirectory)
    {
        this.ExportBackupAsync = ExportBackupAsync ?? throw new ArgumentNullException(nameof(ExportBackupAsync));
        this.GetKnowledgeBaseRoot = GetKnowledgeBaseRoot ?? throw new ArgumentNullException(nameof(GetKnowledgeBaseRoot));
        this.GetNow = GetNow ?? throw new ArgumentNullException(nameof(GetNow));
        this.CreateDirectory = CreateDirectory ?? throw new ArgumentNullException(nameof(CreateDirectory));
    }

    public Func<string, IProgress<string>?, CancellationToken, Task<KnowledgeBackupService.BackupResult>> ExportBackupAsync { get; }
    public Func<string> GetKnowledgeBaseRoot { get; }
    public Func<DateTime> GetNow { get; }
    public Action<string> CreateDirectory { get; }
}

public static class TrainingGoldKbReconcileRequestFactory
{
    public static TrainingGoldKbReconcileRunWorkflowRequest CreateWithDefaults(
        Action<bool> SetBusy,
        Func<Task<List<TrainingSample>>> LoadSamplesAsync,
        Func<List<TrainingSample>, Task> MergeOrUpdateAsync,
        Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> IndexAsync,
        Action<string> Log,
        Action<string> SetStatus,
        Action<Action> OnUi,
        CancellationToken CancellationToken)
        => Create(
            SetBusy,
            LoadSamplesAsync,
            MergeOrUpdateAsync,
            IndexAsync,
            Log,
            SetStatus,
            OnUi,
            CancellationToken,
            new TrainingGoldKbReconcileRuntimeServices(
                KnowledgeBackupService.ExportAsync,
                () => KnowledgeBasePaths.GetRoot(),
                () => DateTime.Now,
                directory => System.IO.Directory.CreateDirectory(directory)));

    public static TrainingGoldKbReconcileRunWorkflowRequest Create(
        Action<bool> SetBusy,
        Func<Task<List<TrainingSample>>> LoadSamplesAsync,
        Func<List<TrainingSample>, Task> MergeOrUpdateAsync,
        Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> IndexAsync,
        Action<string> Log,
        Action<string> SetStatus,
        Action<Action> OnUi,
        CancellationToken CancellationToken,
        TrainingGoldKbReconcileRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(SetBusy);
        ArgumentNullException.ThrowIfNull(LoadSamplesAsync);
        ArgumentNullException.ThrowIfNull(MergeOrUpdateAsync);
        ArgumentNullException.ThrowIfNull(IndexAsync);
        ArgumentNullException.ThrowIfNull(Log);
        ArgumentNullException.ThrowIfNull(SetStatus);
        ArgumentNullException.ThrowIfNull(OnUi);
        ArgumentNullException.ThrowIfNull(services);

        return new TrainingGoldKbReconcileRunWorkflowRequest(
            SetBusy,
            LoadSamplesAsync,
            MergeOrUpdateAsync,
            IndexAsync,
            async (path, progress, token) =>
            {
                var backup = await services.ExportBackupAsync(path, progress, token).ConfigureAwait(false);
                return new TrainingGoldKbReconcileBackupResult(
                    backup.Success,
                    backup.Error,
                    backup.FileCount);
            },
            services.GetKnowledgeBaseRoot,
            services.GetNow,
            services.CreateDirectory,
            Log,
            SetStatus,
            OnUi,
            CancellationToken);
    }
}
