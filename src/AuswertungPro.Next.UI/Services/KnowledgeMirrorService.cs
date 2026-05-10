using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Maintenance;

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
        catch (ObjectDisposedException)
        {
            // Service-Shutdown: _debounceTimer wurde disposed.
            // Kein Logging — normaler Lifecycle.
        }
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

            // Sprint 1 (2026-05-07): SHA256-Verifikation gegen manifest.json.
            // Bei Mismatch (korrupte Mirror-DB) → KEIN Restore.
            // Bei NoManifest (alte Mirror-Version) → Restore mit Warning erlaubt.
            var verification = KnowledgeMirrorVerifier.Verify(brainDb);
            if (verification.Status == VerificationStatus.HashMismatch
                || verification.Status == VerificationStatus.SizeMismatch)
            {
                Debug.WriteLine($"[BrainMirror] Restore abgebrochen — Verifikation fehlgeschlagen: {verification.Message}");
                return false;
            }

            if (verification.Status == VerificationStatus.NoManifest)
            {
                Debug.WriteLine($"[BrainMirror] WARNUNG: Restore ohne Manifest-Verifikation (alter Mirror): {brainPath}");
            }
            else
            {
                Debug.WriteLine($"[BrainMirror] Verifikation OK ({verification.Message}) — Restore startet");
            }

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
        SyncNowAsync().SafeFireAndForget("KnowledgeMirrorDebounceSync");
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

        // Sprint 1 (2026-05-07): Manifest mit SHA256 fuer KnowledgeBase.db schreiben
        // damit ein spaeteres Restore die DB-Integritaet pruefen kann.
        TryWriteDbManifest(target);
    }

    /// <summary>
    /// Schreibt manifest.json mit SHA256 + Bytes der KnowledgeBase.db in den
    /// Mirror-Root. Best-effort: Fehler nicht eskalieren — der Sync war erfolgreich,
    /// nur die spaetere Verifikation ist dann nicht moeglich.
    /// </summary>
    private void TryWriteDbManifest(string mirrorRoot)
    {
        try
        {
            var dbPath = Path.Combine(mirrorRoot, "KnowledgeBase.db");
            if (!File.Exists(dbPath)) return;

            var hash = KnowledgeMirrorVerifier.ComputeSha256(dbPath);
            var bytes = new FileInfo(dbPath).Length;
            KnowledgeMirrorVerifier.WriteManifest(mirrorRoot, "KnowledgeBase.db", hash, bytes);
            Debug.WriteLine($"[BrainMirror] Manifest geschrieben: {hash[..16]}…");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Brain-Mirror Manifest konnte nicht geschrieben werden");
        }
    }

    // Audit-Fix: Whitelist + Verzeichnis-Skipliste statt rekursiver Vollkopie.
    // Bei 80 GB / 118k Dateien skaliert ungefilterte Mirror-Strategie nicht mehr.
    private static readonly string[] SkipDirNames =
    {
        "frames",            // Video-Frame-Cache, regenerierbar
        "frames_extracted",
        "frames_temp",
        "tmp",
        "temp",
        "_archive",          // Alte Snapshots
        ".git",
        "obj", "bin",        // Build-Artefakte (defensiv)
        "yolo_runs",         // Trainings-Output, lokal regenerierbar
        "florence2_shadow_log",
    };

    private static readonly string[] SkipFileExtensions =
    {
        ".tmp", ".temp", ".log", ".trace",
        ".pyc",                          // Python-Cache
        ".lock", ".lscache",
        // Ungewuenschte Build-Artefakte falls direkt im Knowledge-Tree liegen
    };

    private static bool ShouldSkipDirectory(string dirName)
    {
        foreach (var skip in SkipDirNames)
            if (string.Equals(dirName, skip, StringComparison.OrdinalIgnoreCase))
                return true;
        // Backup-Dirs mit Datums-Suffix (.bak_YYYYMMDD)
        if (dirName.Contains(".bak_", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool ShouldSkipFile(string fileName)
    {
        // WAL/SHM (SQLite-Internals)
        if (fileName.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith("-shm", StringComparison.OrdinalIgnoreCase))
            return true;
        // Backup-Dateien mit Zeitstempel
        if (fileName.Contains(".bak_", StringComparison.OrdinalIgnoreCase))
            return true;
        // Extension-basiertes Skip
        var ext = Path.GetExtension(fileName);
        foreach (var skipExt in SkipFileExtensions)
            if (string.Equals(ext, skipExt, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static void SyncDirectoryRecursive(string source, string target, ref int syncCount)
    {
        Directory.CreateDirectory(target);

        foreach (var srcFile in Directory.GetFiles(source))
        {
            var fileName = Path.GetFileName(srcFile);
            if (ShouldSkipFile(fileName))
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
            if (ShouldSkipDirectory(dirName))
                continue;
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
            if (ShouldSkipFile(fileName))
                continue;

            var dstFile = Path.Combine(target, fileName);
            try
            {
                File.Copy(srcFile, dstFile, overwrite: false);
                fileCount++;
            }
            catch (Exception ex)
            {
                // Phase 1.2: Empty-catch-Sweep — Debug-Log statt stilles Schlucken.
                Debug.WriteLine($"[KnowledgeMirror] File.Copy {srcFile} -> {dstFile}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        foreach (var srcDir in Directory.GetDirectories(source))
        {
            var dirName = Path.GetFileName(srcDir);
            if (ShouldSkipDirectory(dirName))
                continue;
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

    /// <summary>
    /// Audit 2026-05-06 (Punkt 3.8): Health-Check fuer den Brain-Mirror.
    /// Validiert dass die Spiegel-Kopie auf E:\Brain aktuell und nicht-leer ist.
    /// Wird im Audit-Tab angezeigt — wenn Yellow/Red, dann Manual-SyncNow oder
    /// Pfad-Korrektur noetig.
    /// </summary>
    public KnowledgeMirrorHealth GetHealth()
    {
        try
        {
            if (!Directory.Exists(_brainRoot))
                return new KnowledgeMirrorHealth(
                    Status: KnowledgeMirrorStatus.Red,
                    Message: $"Brain-Root '{_brainRoot}' existiert nicht",
                    BrainDbBytes: 0,
                    BrainDbAgeHours: null);

            var knowledgeDb = Path.Combine(_knowledgeRoot, "KnowledgeBase.db");
            var brainDb = Path.Combine(_brainRoot, "KnowledgeBase.db");

            if (!File.Exists(knowledgeDb))
                return new KnowledgeMirrorHealth(
                    Status: KnowledgeMirrorStatus.Yellow,
                    Message: "Keine Knowledge-DB lokal — Mirror nicht relevant",
                    BrainDbBytes: 0,
                    BrainDbAgeHours: null);

            if (!File.Exists(brainDb))
                return new KnowledgeMirrorHealth(
                    Status: KnowledgeMirrorStatus.Red,
                    Message: "Knowledge-DB lokal vorhanden, aber kein Mirror auf Brain",
                    BrainDbBytes: 0,
                    BrainDbAgeHours: null);

            var brainInfo = new FileInfo(brainDb);
            var localInfo = new FileInfo(knowledgeDb);
            var ageHours = (DateTime.UtcNow - brainInfo.LastWriteTimeUtc).TotalHours;

            if (brainInfo.Length == 0)
                return new KnowledgeMirrorHealth(
                    Status: KnowledgeMirrorStatus.Red,
                    Message: "Brain-DB ist 0 Byte",
                    BrainDbBytes: 0,
                    BrainDbAgeHours: ageHours);

            // Wenn lokale DB deutlich groesser ist als Mirror-DB, hinkt der Mirror hinterher.
            // Threshold: mehr als 10 % Diff ODER lokal > 1 MB groesser.
            var sizeDiffBytes = localInfo.Length - brainInfo.Length;
            if (sizeDiffBytes > 1_000_000 || sizeDiffBytes > localInfo.Length * 0.10)
                return new KnowledgeMirrorHealth(
                    Status: KnowledgeMirrorStatus.Yellow,
                    Message: $"Mirror hinkt zurueck: lokal {localInfo.Length:N0} B, Brain {brainInfo.Length:N0} B",
                    BrainDbBytes: brainInfo.Length,
                    BrainDbAgeHours: ageHours);

            // Zu alt: > 7 Tage ohne Sync ist verdaechtig wenn lokal modifiziert wurde
            if (ageHours > 24 * 7 && localInfo.LastWriteTimeUtc > brainInfo.LastWriteTimeUtc)
                return new KnowledgeMirrorHealth(
                    Status: KnowledgeMirrorStatus.Yellow,
                    Message: $"Mirror seit {ageHours:F0}h nicht aktualisiert obwohl lokal Aenderungen",
                    BrainDbBytes: brainInfo.Length,
                    BrainDbAgeHours: ageHours);

            return new KnowledgeMirrorHealth(
                Status: KnowledgeMirrorStatus.Green,
                Message: $"Mirror OK ({brainInfo.Length:N0} B, {ageHours:F1}h alt)",
                BrainDbBytes: brainInfo.Length,
                BrainDbAgeHours: ageHours);
        }
        catch (Exception ex)
        {
            return new KnowledgeMirrorHealth(
                Status: KnowledgeMirrorStatus.Red,
                Message: $"Health-Check Exception: {ex.GetType().Name}: {ex.Message}",
                BrainDbBytes: 0,
                BrainDbAgeHours: null);
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

/// <summary>Status-Ampel fuer den Brain-Mirror.</summary>
public enum KnowledgeMirrorStatus
{
    Green,
    Yellow,
    Red,
}

/// <summary>Ergebnis eines Brain-Mirror-Health-Checks.</summary>
public sealed record KnowledgeMirrorHealth(
    KnowledgeMirrorStatus Status,
    string Message,
    long BrainDbBytes,
    double? BrainDbAgeHours);
