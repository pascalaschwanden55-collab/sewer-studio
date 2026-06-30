using System.IO;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingGoldKbReconcileBackupResult(
    bool Success,
    string? Error,
    int FileCount);

public sealed record TrainingGoldKbReconcileWorkflowResult(
    int Total,
    int Eligible,
    int Indexed,
    int Skipped,
    string? BackupZip,
    bool BackupFailed);

public static class TrainingGoldKbReconcileWorkflowController
{
    private const int BatchSize = 50;

    public static async Task<TrainingGoldKbReconcileWorkflowResult> RunAsync(
        Func<Task<List<TrainingSample>>> loadSamplesAsync,
        Func<List<TrainingSample>, Task> mergeOrUpdateAsync,
        Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> indexAsync,
        Func<string, IProgress<string>, CancellationToken, Task<TrainingGoldKbReconcileBackupResult>> exportBackupAsync,
        Func<string> getKnowledgeBaseRoot,
        Func<DateTime> getNow,
        Action<string> createDirectory,
        Action<string> log,
        Action<string> setStatus,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(loadSamplesAsync);
        ArgumentNullException.ThrowIfNull(mergeOrUpdateAsync);
        ArgumentNullException.ThrowIfNull(indexAsync);
        ArgumentNullException.ThrowIfNull(exportBackupAsync);
        ArgumentNullException.ThrowIfNull(getKnowledgeBaseRoot);
        ArgumentNullException.ThrowIfNull(getNow);
        ArgumentNullException.ThrowIfNull(createDirectory);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        var all = await loadSamplesAsync().ConfigureAwait(false);
        var pending = KbReconcilePlanner.SelectPending(all);
        var (total, eligible) = KbReconcilePlanner.CountPending(all);
        if (total == 0)
        {
            log("KB-Nachholen: keine offenen Gold-Samples (alles bereits indexiert).");
            setStatus("KB-Nachholen: nichts zu tun");
            return new TrainingGoldKbReconcileWorkflowResult(0, 0, 0, 0, null, false);
        }

        log($"KB-Nachholen: {total} bestaetigte Gold-Samples warten (davon {eligible} trainingsfaehig markiert).");

        var backupDirectory = Path.Combine(getKnowledgeBaseRoot(), "kb_backups");
        var backupZip = Path.Combine(
            backupDirectory,
            $"vor_kb_nachholen_{getNow():yyyy-MM-dd_HHmmss}.zip");
        createDirectory(backupDirectory);

        setStatus("KB-Nachholen: Backup wird erstellt\u2026");
        var backup = await exportBackupAsync(
            backupZip,
            new Progress<string>(m => setStatus($"Backup: {m}")),
            ct).ConfigureAwait(false);
        if (!backup.Success)
        {
            log($"KB-Nachholen ABGEBROCHEN: Backup fehlgeschlagen ({backup.Error}). Keine Aenderung vorgenommen.");
            setStatus("KB-Nachholen: Backup fehlgeschlagen");
            return new TrainingGoldKbReconcileWorkflowResult(total, eligible, 0, 0, backupZip, true);
        }

        log($"KB-Nachholen: Backup angelegt ({backup.FileCount} Dateien) unter {backupZip}");
        setStatus($"KB-Nachholen: 0/{total}");

        var indexed = 0;
        var skipped = 0;
        var processed = 0;

        for (var i = 0; i < pending.Count; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = pending.Skip(i).Take(BatchSize).ToList();

            foreach (var sample in batch)
                sample.KbIndexState = KbIndexState.Pending;
            await mergeOrUpdateAsync(batch).ConfigureAwait(false);

            var indexResult = await indexAsync(batch, ct).ConfigureAwait(false);

            foreach (var sample in batch)
            {
                if (indexResult.IndexedIds.Contains(sample.SampleId))
                {
                    sample.KbIndexState = KbIndexState.Indexed;
                    indexed++;
                }
                else if (indexResult.SkippedIds.Contains(sample.SampleId))
                {
                    sample.KbIndexState = KbIndexState.Skipped;
                    skipped++;
                }
                else
                {
                    sample.KbIndexState = KbIndexState.Error;
                    skipped++;
                }

                processed++;
            }

            await mergeOrUpdateAsync(batch).ConfigureAwait(false);

            setStatus($"KB-Nachholen: {processed}/{total}");
        }

        log($"KB-Nachholen fertig: {indexed} indexiert, {skipped} uebersprungen/fehlgeschlagen (von {total}).");
        setStatus($"KB-Nachholen: {indexed} indexiert, {skipped} uebersprungen");

        return new TrainingGoldKbReconcileWorkflowResult(total, eligible, indexed, skipped, backupZip, false);
    }
}
