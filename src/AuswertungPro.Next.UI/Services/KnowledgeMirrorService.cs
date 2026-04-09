using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Write-Through Mirror: Kopiert den Knowledge-Ordner nach E:\Brain (oder konfiguriertem Pfad).
/// Jedes Mal wenn die KI etwas lernt, wird automatisch eine Kopie erstellt.
/// Beim naechsten Start wird aus Brain wiederhergestellt falls Knowledge leer ist.
///
/// Design:
///  - NotifyChanged() → Debounced Sync (3s Verzoegerung, verhindert Spam bei Batch-Ops)
///  - SyncNowAsync()  → Sofortiger Sync (nach Import/Rebuild)
///  - TryRestoreFromBrain() → Statisch, wird beim Startup aufgerufen
///  - Inkrementell: Nur geaenderte Dateien kopieren (LastWriteTime)
///  - Additiv: Im Mirror wird nie geloescht
///  - Best-effort: Fehler loggen, aber nie den Hauptbetrieb blockieren
/// </summary>
public sealed class KnowledgeMirrorService : IDisposable
{
    /// <summary>Globale Singleton-Instanz fuer Zugriff aus statischen Stores.</summary>
    public static KnowledgeMirrorService? Current { get; private set; }

    private readonly string _knowledgeRoot;
    private readonly string _brainRoot;
    private readonly ILogger? _logger;
    private readonly Timer _debounceTimer;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private const int DebounceDelayMs = 3000;

    public KnowledgeMirrorService(string knowledgeRoot, string brainRoot, ILogger? logger = null)
    {
        _knowledgeRoot = knowledgeRoot;
        _brainRoot = brainRoot;
        _logger = logger;
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        // Singleton setzen (erster gewinnt)
        Current ??= this;

        try
        {
            Directory.CreateDirectory(_brainRoot);
            _logger?.LogInformation("Brain-Mirror aktiv: {Knowledge} → {Brain}", _knowledgeRoot, _brainRoot);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Brain-Mirror Zielordner konnte nicht erstellt werden: {Path}", _brainRoot);
        }
    }

    // ── Oeffentliche API ─────────────────────────────────────────────

    /// <summary>
    /// Meldet eine Aenderung im Knowledge-Ordner.
    /// Startet einen debounced Sync (3s Verzoegerung).
    /// Sicher aus jedem Thread aufrufbar.
    /// </summary>
    public void NotifyChanged()
    {
        try
        {
            _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
        }
        catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Sofortiger Sync ohne Debounce (z.B. nach Import oder Rebuild).
    /// Thread-safe via SemaphoreSlim.
    /// </summary>
    public async Task SyncNowAsync(CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(0, ct))
        {
            // Bereits ein Sync aktiv → ueberspringen
            Debug.WriteLine("[BrainMirror] Sync uebersprungen (bereits aktiv)");
            return;
        }

        try
        {
            await Task.Run(() => SyncDirectory(_knowledgeRoot, _brainRoot), ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Brain-Mirror Sync fehlgeschlagen");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Stellt Knowledge aus Brain wieder her, falls Knowledge leer ist.
    /// Wird beim Startup in KnowledgeRoot.GetRoot() aufgerufen.
    /// </summary>
    public static bool TryRestoreFromBrain(string knowledgeRoot, string brainPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(brainPath) || !Directory.Exists(brainPath))
                return false;

            // Pruefen ob Brain ueberhaupt eine DB hat
            var brainDb = Path.Combine(brainPath, "KnowledgeBase.db");
            if (!File.Exists(brainDb))
                return false;

            // Pruefen ob Knowledge schon eine DB hat (dann kein Restore)
            var knowledgeDb = Path.Combine(knowledgeRoot, "KnowledgeBase.db");
            if (File.Exists(knowledgeDb))
                return false;

            Debug.WriteLine($"[BrainMirror] Knowledge leer, stelle aus Brain wieder her: {brainPath}");
            Directory.CreateDirectory(knowledgeRoot);

            var fileCount = 0;
            CopyDirectoryRecursive(brainPath, knowledgeRoot, ref fileCount);

            Debug.WriteLine($"[BrainMirror] Restore abgeschlossen: {fileCount} Dateien aus Brain kopiert");
            return fileCount > 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BrainMirror] Restore fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    // ── Interne Sync-Logik ───────────────────────────────────────────

    private void OnDebounceElapsed(object? state)
    {
        _ = SyncNowAsync();
    }

    /// <summary>
    /// Inkrementeller Directory-Sync: Nur neuere Dateien kopieren.
    /// SQLite-DB wird vorher per WAL-Checkpoint konsistent gemacht.
    /// </summary>
    private void SyncDirectory(string source, string target)
    {
        if (!Directory.Exists(source)) return;

        // SQLite WAL-Checkpoint vor dem Kopieren (konsistente DB)
        TryWalCheckpoint(source);

        var synced = 0;
        SyncDirectoryRecursive(source, target, ref synced);

        if (synced > 0)
            Debug.WriteLine($"[BrainMirror] Sync: {synced} Dateien aktualisiert → {target}");
    }

    private static void SyncDirectoryRecursive(string source, string target, ref int syncCount)
    {
        Directory.CreateDirectory(target);

        foreach (var srcFile in Directory.GetFiles(source))
        {
            var fileName = Path.GetFileName(srcFile);

            // WAL/SHM nicht separat kopieren (DB-Checkpoint erledigt das)
            if (fileName.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("-shm", StringComparison.OrdinalIgnoreCase))
                continue;

            var dstFile = Path.Combine(target, fileName);

            if (NeedsCopy(srcFile, dstFile))
            {
                try
                {
                    File.Copy(srcFile, dstFile, overwrite: true);
                    syncCount++;
                }
                catch
                {
                    // Best-effort: einzelne Dateifehler blockieren nicht den Rest
                }
            }
        }

        foreach (var srcDir in Directory.GetDirectories(source))
        {
            var dirName = Path.GetFileName(srcDir);
            var dstDir = Path.Combine(target, dirName);
            SyncDirectoryRecursive(srcDir, dstDir, ref syncCount);
        }
    }

    /// <summary>True wenn Quelldatei neuer oder groesser als Zieldatei.</summary>
    private static bool NeedsCopy(string src, string dst)
    {
        if (!File.Exists(dst)) return true;

        var srcInfo = new FileInfo(src);
        var dstInfo = new FileInfo(dst);
        return srcInfo.LastWriteTimeUtc > dstInfo.LastWriteTimeUtc
            || srcInfo.Length != dstInfo.Length;
    }

    private static void CopyDirectoryRecursive(string source, string target, ref int fileCount)
    {
        Directory.CreateDirectory(target);

        foreach (var srcFile in Directory.GetFiles(source))
        {
            var fileName = Path.GetFileName(srcFile);
            // WAL/SHM nicht kopieren beim Restore
            if (fileName.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("-shm", StringComparison.OrdinalIgnoreCase))
                continue;

            var dstFile = Path.Combine(target, fileName);
            try
            {
                File.Copy(srcFile, dstFile, overwrite: false);
                fileCount++;
            }
            catch { }
        }

        foreach (var srcDir in Directory.GetDirectories(source))
        {
            var dirName = Path.GetFileName(srcDir);
            CopyDirectoryRecursive(srcDir, Path.Combine(target, dirName), ref fileCount);
        }
    }

    /// <summary>
    /// Flusht SQLite WAL in die Hauptdatei damit die Kopie konsistent ist.
    /// </summary>
    private static void TryWalCheckpoint(string knowledgeRoot)
    {
        var dbPath = Path.Combine(knowledgeRoot, "KnowledgeBase.db");
        if (!File.Exists(dbPath)) return;

        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Best-effort: WAL/SHM werden ggf. nicht geflusht,
            // die Hauptdatei ist aber trotzdem konsistent
        }
    }

    public void Dispose()
    {
        _debounceTimer.Dispose();
        _syncLock.Dispose();
        if (ReferenceEquals(Current, this))
            Current = null;
    }
}
