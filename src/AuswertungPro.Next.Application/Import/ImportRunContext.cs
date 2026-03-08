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

    public ImportRunContext(
        CancellationToken cancellationToken,
        IProgress<ImportProgress>? progress,
        ImportRunLog log,
        bool dryRun = false)
    {
        CancellationToken = cancellationToken;
        Progress = progress;
        Log = log ?? throw new ArgumentNullException(nameof(log));
        DryRun = dryRun;
    }

    private ImportRunContext()
    {
        CancellationToken = CancellationToken.None;
        Progress = null;
        Log = new ImportRunLog();
        DryRun = false;
    }

    public static ImportRunContext Default { get; } = new();
}
