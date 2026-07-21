namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Buendelt alle Cross-Cutting-Concerns fuer einen Import-Lauf:
/// CancellationToken, Progress, strukturiertes Log und DryRun-Flag.
/// </summary>
public sealed class ImportRunContext
{
    public CancellationToken CancellationToken { get; }
    public IProgress<ImportProgress>? Progress { get; }
    public ImportRunLog Log { get; }
    public bool DryRun { get; }
    public object? CollectionLock { get; }
    public IImportFileStagingSession? FileStaging { get; }

    public ImportRunContext(
        CancellationToken cancellationToken,
        IProgress<ImportProgress>? progress,
        ImportRunLog log,
        bool dryRun = false,
        object? collectionLock = null,
        IImportFileStagingSession? fileStaging = null)
    {
        CancellationToken = cancellationToken;
        Progress = progress;
        Log = log ?? throw new ArgumentNullException(nameof(log));
        DryRun = dryRun;
        CollectionLock = collectionLock;
        FileStaging = fileStaging;
    }

    private ImportRunContext()
    {
        CancellationToken = CancellationToken.None;
        Progress = null;
        Log = new ImportRunLog();
        DryRun = false;
        FileStaging = null;
    }

    public static ImportRunContext Default { get; } = new();

    public void WithCollectionLock(Action action)
    {
        if (action is null)
            throw new ArgumentNullException(nameof(action));

        if (CollectionLock is null)
        {
            action();
            return;
        }

        lock (CollectionLock)
            action();
    }

    public T WithCollectionLock<T>(Func<T> action)
    {
        if (action is null)
            throw new ArgumentNullException(nameof(action));

        if (CollectionLock is null)
            return action();

        lock (CollectionLock)
            return action();
    }
}
