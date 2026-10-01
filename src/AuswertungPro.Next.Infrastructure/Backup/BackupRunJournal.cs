using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Durable Vorherkopien für einen Sicherungslauf; nur innerhalb des markierten Ziels.</summary>
public sealed class BackupRunJournal : IDisposable
{
    private const string PendingName = "_Versionen/.unterbrochener-lauf";
    private readonly string _root;
    private readonly string _pending;
    private readonly FileStream _lock;
    private readonly HashSet<string> _recorded = new(StringComparer.OrdinalIgnoreCase);
    private bool _finished;

    private sealed record Header(string Root, string Stand);
    private sealed record Entry(string Path, bool Existed, string? Hash);

    public static bool IsPending(string root) => Directory.Exists(Path.Combine(root, PendingName));

    internal BackupRunJournal(string root)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        BackupTargetPathGuard.EnsureTreeIsSafe(_root);
        var versions = Safe(BackupVersionRetention.VersionsFolderName);
        Directory.CreateDirectory(versions);
        _lock = new FileStream(Safe("_Versionen/.sicherung.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        _pending = Safe(PendingName);
        try
        {
            RecoverCore();
            var prepared = Safe("_Versionen/.vorbereitet-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(prepared, ".journal"));
            var stand = BackupVersionRetention.BuildStandName(DateTime.Now);
            var latest = Directory.EnumerateDirectories(versions).Select(Path.GetFileName)
                .Where(name => name is not null && BackupVersionRetention.IsStandName(name))
                .OrderByDescending(name => name, StringComparer.Ordinal).FirstOrDefault();
            // Auch bei zurückgestellter Uhr und mehreren Läufen pro Sekunde bleibt
            // die neue Vorversion die neueste und wird nicht sofort ausgedünnt.
            if (latest is not null && string.CompareOrdinal(latest, stand) >= 0)
                stand = BackupVersionRetention.BuildStandName(DateTime.ParseExact(latest,
                    "yyyy-MM-dd_HHmmss", System.Globalization.CultureInfo.InvariantCulture).AddSeconds(1));
            Write(Path.Combine(prepared, ".journal/header.json"), new Header(_root, stand));
            Directory.Move(prepared, _pending);
        }
        catch { _lock.Dispose(); throw; }
    }

    public void Preserve(string file)
    {
        try
        {
            BackupTargetPathGuard.EnsurePathIsSafe(_root, file);
            var relative = Path.GetRelativePath(_root, file);
            if (relative == "." || BackupVersionRetention.IsVersionsDir(relative)
                || relative.Split(Path.DirectorySeparatorChar)[0].Equals(".journal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Ungültiges Rücksetzziel.");
            if (_recorded.Contains(relative)) return;
            var existed = File.Exists(file);
            string? hash = null;
            if (existed)
            {
                var copy = BackupTargetPathGuard.ResolveRelativePath(_pending, relative);
                CopyDurable(file, copy);
                hash = Hash(copy);
                if (hash != Hash(file)) throw new IOException("Vorherkopie stimmt nicht mit dem Ziel überein.");
            }
            var key = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(relative)));
            Write(Safe(PendingName + "/.journal/" + key + ".json"), new Entry(relative, existed, hash));
            _recorded.Add(relative);
        }
        catch (Exception ex)
        {
            throw BackupTargetBoundary.Fail("Rücksetzprotokoll konnte nicht sicher geschrieben werden.", ex);
        }
    }

    public void Commit()
    {
        Write(Safe(PendingName + "/.journal/committed.json"), true);
        _finished = true;
        FinishCommitted(ReadHeader());
    }

    internal bool HasHistory => ReadEntries().Any(entry => entry.Existed
        && !entry.Path.StartsWith("Extras" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        && !entry.Path.StartsWith("manifest.json", StringComparison.OrdinalIgnoreCase)
        && !entry.Path.EndsWith(DirectoryMirror.TempSuffix, StringComparison.OrdinalIgnoreCase));

    internal void WriteText(string path, string content, bool saveBackup = false)
    {
        Preserve(path);
        var temp = path + DirectoryMirror.TempSuffix;
        Preserve(temp);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None,
                   4096, FileOptions.WriteThrough))
        {
            stream.Write(System.Text.Encoding.UTF8.GetBytes(content));
            stream.Flush(flushToDisk: true);
        }
        if (saveBackup && File.Exists(path))
        {
            Preserve(path + ".bak");
            CopyDurable(path, path + ".bak");
        }
        BackupTargetPathGuard.EnsurePathIsSafe(_root, path);
        File.Move(temp, path, overwrite: true);
    }

    public void Rollback()
    {
        if (_finished) return;
        RecoverCore();
        _finished = true;
    }

    // Auch separat für die Wiederherstellung auf einem Ersatz-PC nutzbar.
    public static void Recover(string root)
    {
        if (!File.Exists(Path.Combine(root, BackupPlanBuilder.MarkerFileName)))
            throw new InvalidDataException("Sicherungsmarker fehlt. Keine Wiederherstellung ausgeführt.");
        var error = BackupTargetGuard.MarkerGuard.ValidateAndCreateMarker(root);
        if (error is not null) throw new InvalidDataException(error);
        using var journal = new BackupRunJournal(root);
        journal.Rollback();
    }

    private void RecoverCore()
    {
        if (!Directory.Exists(_pending)) return;
        BackupTargetPathGuard.EnsureTreeIsSafe(_pending);
        var header = ReadHeader();
        if (File.Exists(Safe(PendingName + "/.journal/committed.json")))
        {
            if (!JsonSerializer.Deserialize<bool>(File.ReadAllText(Safe(PendingName + "/.journal/committed.json"))))
                throw new InvalidDataException("Ungültiger Sicherungsabschluss.");
            FinishCommitted(header);
            return;
        }
        var entries = ReadEntries();
        // Erst ALLE Vorherkopien und Pfade prüfen, bevor etwas zurückgeschrieben wird.
        foreach (var entry in entries)
        {
            if (entry.Path == "." || BackupVersionRetention.IsVersionsDir(entry.Path)
                || entry.Path.Split(Path.DirectorySeparatorChar)[0].Equals(".journal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Ungültiges Rücksetzziel.");
            _ = Safe(entry.Path);
            if (!string.Equals(Path.GetRelativePath(_root, Safe(entry.Path)), entry.Path, StringComparison.Ordinal))
                throw new InvalidDataException("Nicht kanonischer Rücksetzpfad.");
            var copy = BackupTargetPathGuard.ResolveRelativePath(_pending, entry.Path);
            if (entry.Existed && (!File.Exists(copy) || Hash(copy) != entry.Hash))
                throw new InvalidDataException("Vorherkopie beschädigt oder fehlt: " + entry.Path);
        }
        foreach (var entry in entries)
        {
            var target = Safe(entry.Path);
            if (entry.Existed)
            {
                if (File.Exists(target) && Hash(target) == entry.Hash) continue;
                var temp = Safe(PendingName + "/.journal/recovery.tmp");
                CopyDurable(BackupTargetPathGuard.ResolveRelativePath(_pending, entry.Path), temp);
                BackupTargetPathGuard.EnsurePathIsSafe(_root, target);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(temp, target, overwrite: true);
            }
            else if (File.Exists(target)) File.Delete(target);
        }
        // Atomar aus dem Recovery-Namen nehmen: ein Abbruch beim Aufräumen darf
        // keine bereits entfernten Vorherkopien erneut als erforderlich ansehen.
        var retired = Safe("_Versionen/.zurueckgesetzt-" + Guid.NewGuid().ToString("N"));
        Directory.Move(_pending, retired);
        BackupTargetPathGuard.EnsureTreeIsSafe(retired);
        Directory.Delete(retired, recursive: true);
    }

    private Header ReadHeader()
    {
        var header = JsonSerializer.Deserialize<Header>(File.ReadAllText(Safe(PendingName + "/.journal/header.json")))
                     ?? throw new InvalidDataException("Rücksetzprotokoll ohne Kopf.");
        if (!string.Equals(header.Root, _root, StringComparison.OrdinalIgnoreCase)
            || !BackupVersionRetention.IsStandName(header.Stand))
            throw new InvalidDataException("Rücksetzprotokoll gehört zu einem anderen Sicherungsordner.");
        return header;
    }

    private void FinishCommitted(Header header)
    {
        BackupTargetPathGuard.EnsureTreeIsSafe(_pending);
        if (HasHistory) Directory.Move(_pending, Safe("_Versionen/" + header.Stand));
        else
        {
            var retired = Safe("_Versionen/.abgeschlossen-" + Guid.NewGuid().ToString("N"));
            Directory.Move(_pending, retired);
            BackupTargetPathGuard.EnsureTreeIsSafe(retired);
            Directory.Delete(retired, recursive: true);
        }
    }

    private Entry[] ReadEntries() => Directory.EnumerateFiles(Safe(PendingName + "/.journal"), "*.json")
        .Where(p => Path.GetFileName(p) is not ("header.json" or "committed.json"))
        .Select(p => JsonSerializer.Deserialize<Entry>(File.ReadAllText(p))
            ?? throw new InvalidDataException("Ungültiges Rücksetzprotokoll.")).ToArray();

    private string Safe(string relative) => BackupTargetPathGuard.ResolveRelativePath(_root, relative);
    private static string Hash(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    private static void Write<T>(string path, T value)
        => AtomicTextFileWriter.WriteAllText(path, JsonSerializer.Serialize(value), durable: true);

    private static void CopyDurable(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None,
                   128 * 1024, FileOptions.WriteThrough))
        {
            input.CopyTo(output);
            output.Flush(flushToDisk: true);
        }
        File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
    }

    public void Dispose()
    {
        try { if (!_finished) Rollback(); }
        finally { _lock.Dispose(); }
    }
}
