using System.IO.Compression;
using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Packt den Programmordner nach den Regeln des
/// <see cref="ProgramSnapshotFileCatalog"/> in eine einzelne ZIP-Datei.
///
/// Drei Grenzen sind bewusst hart:
/// Die Quelle wird nur gelesen, Verknuepfungen werden nie verfolgt, und das Ziel
/// entsteht erst nach vollstaendigem Schreiben. Ein Abbruch mittendrin laesst
/// deshalb weder eine halbe noch eine ueberschriebene Sicherung zurueck.
/// </summary>
public sealed class ProgramSnapshotService : IProgramSnapshotService
{
    private readonly IGitCommitResolver? _gitCommits;
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;
    private readonly Func<string, IEnumerable<string>> _enumerateFiles;

    public ProgramSnapshotService(IGitCommitResolver? gitCommits = null)
        : this(gitCommits, Directory.EnumerateDirectories, Directory.EnumerateFiles)
    {
    }

    /// <summary>
    /// Test-Seam: erlaubt einen Ordnerdurchlauf, der bei bestimmten Ordnern wirft.
    /// Ein echter unlesbarer Ordner laesst sich sonst nur ueber Zugriffsrechte
    /// erzeugen, was je nach Benutzerrechten unterschiedlich wirkt.
    /// </summary>
    internal ProgramSnapshotService(
        IGitCommitResolver? gitCommits,
        Func<string, IEnumerable<string>> enumerateDirectories,
        Func<string, IEnumerable<string>> enumerateFiles)
    {
        _gitCommits = gitCommits;
        _enumerateDirectories = enumerateDirectories;
        _enumerateFiles = enumerateFiles;
    }

    public async Task<ProgramSnapshotResult> CreateAsync(
        ProgramSnapshotRequest request,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? temporaryArchivePath = null;
        try
        {
            var programRoot = Path.GetFullPath(request.ProgramRoot);
            if (!Directory.Exists(programRoot))
                return Failed($"Programmordner nicht gefunden: {programRoot}");

            // Eine verknuepfte Programmwurzel wuerde unbemerkt woanders lesen.
            if (ReparsePointGuard.IsReparsePoint(programRoot))
                return Failed($"Programmordner ist eine Verknuepfung: {programRoot}");

            var destinationPath = Path.GetFullPath(request.ZipPath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
                return Failed($"Zielordner fehlt: {request.ZipPath}");

            if (IsInside(programRoot, destinationPath))
                return Failed("Die Momentaufnahme darf nicht im Programmordner selbst liegen.");

            Directory.CreateDirectory(destinationDirectory);
            temporaryArchivePath = Path.Combine(
                destinationDirectory,
                $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

            var scan = ScanSnapshotFiles(programRoot, ct, _enumerateDirectories, _enumerateFiles);

            // Ein unlesbarer unersetzlicher Ordner darf keine "erfolgreiche" Sicherung
            // ergeben (Gesamtaudit 2026-08-14, P1-2). Frueher wurde er still uebersprungen.
            var missingRequired = scan.UnreadableDirectories
                .Where(ProgramSnapshotFileCatalog.IsRequiredDirectory)
                .ToList();
            if (missingRequired.Count > 0)
            {
                return new ProgramSnapshotResult(
                    false,
                    "Unersetzliche Ordner konnten nicht gelesen werden: "
                    + string.Join(", ", missingRequired)
                    + ". Die Sicherung waere unvollstaendig und wurde nicht geschrieben.",
                    0, 0, scan.SkippedReparsePoints, scan.UnreadableDirectories);
            }

            var fileCount = 0;
            var skippedReparsePoints = scan.SkippedReparsePoints;
            using (var zip = ZipFile.Open(temporaryArchivePath, ZipArchiveMode.Create))
            {
                foreach (var relativePath in scan.Files)
                {
                    ct.ThrowIfCancellationRequested();
                    var source = Path.Combine(programRoot, relativePath);

                    if (fileCount % 250 == 0)
                        progress?.Report($"Packe: {relativePath}");

                    var entryName = relativePath.Replace(Path.DirectorySeparatorChar, '/');
                    var entry = zip.CreateEntry(entryName, ChooseCompressionLevel(relativePath));
                    await using (var destination = entry.Open())
                    await using (var sourceStream = new FileStream(
                                     source,
                                     FileMode.Open,
                                     FileAccess.Read,
                                     FileShare.ReadWrite))
                    {
                        await sourceStream.CopyToAsync(destination, ct).ConfigureAwait(false);
                    }

                    fileCount++;
                }

                await WriteManifestAsync(
                        zip, programRoot, fileCount, skippedReparsePoints,
                        scan.UnreadableDirectories, ct)
                    .ConfigureAwait(false);
            }

            if (request.VerifyArchive)
            {
                progress?.Report("Sicherung wird geprueft...");
                var verifyError = VerifyArchive(temporaryArchivePath, fileCount, ct);
                if (verifyError is not null)
                    return new ProgramSnapshotResult(
                        false, verifyError, 0, 0, skippedReparsePoints, scan.UnreadableDirectories);
            }

            CommitArchive(temporaryArchivePath, destinationPath);
            temporaryArchivePath = null;

            var size = new FileInfo(destinationPath).Length;

            // Pruefsumme der veroeffentlichten Datei: im Manifest ginge sie nicht
            // (sie wuerde sich selbst enthalten), deshalb als Nebendatei.
            var archiveHash = ComputeFileSha256(destinationPath, ct);
            WriteChecksumSidecar(destinationPath, archiveHash);

            progress?.Report(
                $"Momentaufnahme fertig: {fileCount} Dateien, {size / (1024.0 * 1024.0):F1} MB");
            return new ProgramSnapshotResult(
                true, null, fileCount, size, skippedReparsePoints,
                scan.UnreadableDirectories, archiveHash);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[ProgramSnapshot] Fehlgeschlagen ({request.ZipPath}): {ex.GetType().Name}: {ex.Message}");
            return Failed(UserError.Describe(ex));
        }
        finally
        {
            if (temporaryArchivePath is not null)
            {
                BestEffort.Try(
                    () =>
                    {
                        if (File.Exists(temporaryArchivePath))
                            File.Delete(temporaryArchivePath);
                    },
                    $"Programm-Momentaufnahme: Temp-Datei {temporaryArchivePath} loeschen");
            }
        }
    }

    /// <summary>
    /// Dateiarten, die bereits komprimiert sind. Sie werden unveraendert abgelegt.
    /// Erneutes Packen kostet nur Zeit und macht sie sogar groesser: Ein
    /// 894-MB-Modellgewicht wuchs im Messlauf auf 939 MB.
    /// </summary>
    private static readonly HashSet<string> AlreadyCompressedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pt", ".pth", ".onnx", ".engine", ".safetensors", ".tflite",
            ".pack", ".idx",
            ".zip", ".7z", ".gz", ".tgz", ".bz2", ".xz", ".rar", ".whl", ".nupkg",
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif",
            ".mp3", ".mp4", ".avi", ".mkv", ".mov", ".mpg", ".mpeg", ".webm"
        };

    /// <summary>
    /// Waehlt die Packstufe je Datei. Quellcode wird kraeftig gepackt, bereits
    /// komprimierte Dateien werden unveraendert uebernommen.
    /// </summary>
    internal static CompressionLevel ChooseCompressionLevel(string relativePath)
        => AlreadyCompressedExtensions.Contains(Path.GetExtension(relativePath))
            ? CompressionLevel.NoCompression
            : CompressionLevel.Optimal;

    /// <summary>
    /// Ergebnis des Ordnerdurchlaufs. Die unlesbaren Ordner sind Teil des Ergebnisses
    /// und nicht nur eine Protokollzeile: Nur so kann der Aufrufer entscheiden, ob die
    /// Sicherung ueberhaupt gueltig ist.
    /// </summary>
    internal sealed record SnapshotScan(
        List<string> Files,
        int SkippedReparsePoints,
        List<string> UnreadableDirectories);

    /// <summary>
    /// Laeuft den Programmordner ab und liefert die aufzunehmenden Dateien als
    /// Pfade relativ zur Wurzel. Ausgeschlossene und verknuepfte Ordner werden
    /// gar nicht erst betreten; ein unlesbarer Unterordner beendet den Lauf nicht,
    /// wird aber namentlich zurueckgemeldet.
    /// </summary>
    internal static SnapshotScan ScanSnapshotFiles(
        string programRoot,
        CancellationToken ct,
        Func<string, IEnumerable<string>>? enumerateDirectories = null,
        Func<string, IEnumerable<string>>? enumerateFiles = null)
    {
        enumerateDirectories ??= Directory.EnumerateDirectories;
        enumerateFiles ??= Directory.EnumerateFiles;

        var results = new List<string>();
        var unreadable = new List<string>();
        var skipped = 0;
        var pending = new Stack<string>();
        pending.Push(programRoot);

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = pending.Pop();
            var currentRelative = RelativeOrRoot(programRoot, current);

            foreach (var directory in SafeEnumerate(
                         () => enumerateDirectories(current), currentRelative, unreadable))
            {
                var relative = Path.GetRelativePath(programRoot, directory);
                if (ProgramSnapshotFileCatalog.IsExcludedDirectory(relative))
                    continue;

                if (ReparsePointGuard.IsReparsePoint(directory))
                {
                    skipped++;
                    continue;
                }

                pending.Push(directory);
            }

            foreach (var file in SafeEnumerate(
                         () => enumerateFiles(current), currentRelative, unreadable))
            {
                if (ReparsePointGuard.IsReparsePoint(file))
                {
                    skipped++;
                    continue;
                }

                results.Add(Path.GetRelativePath(programRoot, file));
            }
        }

        results.Sort(StringComparer.OrdinalIgnoreCase);
        unreadable.Sort(StringComparer.OrdinalIgnoreCase);
        return new SnapshotScan(results, skipped, unreadable);
    }

    /// <summary>Relativer Pfad; die Wurzel selbst wird als "." benannt.</summary>
    private static string RelativeOrRoot(string programRoot, string path)
    {
        var relative = Path.GetRelativePath(programRoot, path);
        return string.IsNullOrEmpty(relative) ? "." : relative;
    }

    private static IEnumerable<string> SafeEnumerate(
        Func<IEnumerable<string>> enumerate,
        string relativeDirectory,
        List<string> unreadable)
    {
        try
        {
            return enumerate().ToList();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[ProgramSnapshot] Ordner nicht lesbar ({relativeDirectory}): "
                + $"{ex.GetType().Name}: {ex.Message}");
            if (!unreadable.Contains(relativeDirectory, StringComparer.OrdinalIgnoreCase))
                unreadable.Add(relativeDirectory);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Oeffnet die fertige ZIP erneut und prueft jeden Eintrag: Laenge und selbst
    /// nachgerechnete CRC-Pruefsumme gegen den im Archiv gespeicherten Wert.
    ///
    /// Das eigene Nachrechnen ist der Kern: System.IO.Compression prueft die CRC nur
    /// beim SCHREIBEN, nicht beim Lesen. Reines Durchlesen wuerde eine gekippte
    /// Bitfolge in einer unkomprimiert abgelegten Datei — also gerade in den
    /// Modellgewichten — nicht bemerken.
    ///
    /// Gibt null zurueck, wenn alles passt.
    /// </summary>
    internal static string? VerifyArchive(string archivePath, int expectedFileCount, CancellationToken ct)
    {
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);

            var manifestFound = false;
            var dataEntries = 0;
            var buffer = new byte[81920];

            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (string.Equals(entry.FullName, "_manifest.json", StringComparison.Ordinal))
                    manifestFound = true;
                else
                    dataEntries++;

                long gelesen = 0;
                var crc = 0xFFFFFFFFu;
                using (var stream = entry.Open())
                {
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        gelesen += read;
                        crc = Crc32Update(crc, buffer, read);
                    }
                }

                crc ^= 0xFFFFFFFFu;

                if (gelesen != entry.Length)
                    return $"Nachpruefung fehlgeschlagen: {entry.FullName} ist {gelesen} Bytes gross, "
                           + $"erwartet {entry.Length}.";

                if (crc != entry.Crc32)
                    return $"Nachpruefung fehlgeschlagen: Pruefsumme von {entry.FullName} stimmt nicht "
                           + "(Daten in der Sicherung sind beschaedigt).";
            }

            if (!manifestFound)
                return "Nachpruefung fehlgeschlagen: Das Manifest fehlt in der Sicherung.";

            if (dataEntries != expectedFileCount)
                return $"Nachpruefung fehlgeschlagen: {dataEntries} Eintraege in der Sicherung, "
                       + $"erwartet {expectedFileCount}.";

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"Nachpruefung fehlgeschlagen: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>
    /// CRC-32 (Polynom 0xEDB88320) — dieselbe Rechnung, die das ZIP-Format verwendet.
    /// Bewusst selbst gerechnet statt ein weiteres NuGet-Paket aufzunehmen.
    /// </summary>
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var wert = i;
            for (var bit = 0; bit < 8; bit++)
                wert = (wert & 1) != 0 ? 0xEDB88320u ^ (wert >> 1) : wert >> 1;
            table[i] = wert;
        }

        return table;
    }

    private static uint Crc32Update(uint crc, byte[] buffer, int count)
    {
        for (var i = 0; i < count; i++)
            crc = Crc32Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static string ComputeFileSha256(string path, CancellationToken ct)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            sha.TransformBlock(buffer, 0, read, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
    }

    /// <summary>
    /// Legt die Pruefsumme neben die Sicherung. Schlaegt das fehl, bleibt die
    /// Sicherung selbst gueltig — die Nebendatei ist eine Zugabe, keine Bedingung.
    /// </summary>
    private static void WriteChecksumSidecar(string archivePath, string sha256)
        => BestEffort.Try(
            () => File.WriteAllText(
                archivePath + ".sha256",
                $"{sha256}  {Path.GetFileName(archivePath)}{Environment.NewLine}"),
            $"Programm-Momentaufnahme: Pruefsumme neben {archivePath} schreiben");

    private async Task WriteManifestAsync(
        ZipArchive zip,
        string programRoot,
        int fileCount,
        int skippedReparsePoints,
        IReadOnlyList<string> unreadableDirectories,
        CancellationToken ct)
    {
        var manifest = new
        {
            Product = "SewerStudio",
            Kind = "ProgramSnapshot",
            Version = 2,
            CreatedUtc = DateTime.UtcNow.ToString("o"),
            ProgramRoot = programRoot,
            FileCount = fileCount,
            SkippedReparsePoints = skippedReparsePoints,
            // Unlesbare Ordner stehen ausdruecklich im Manifest: sonst sieht eine
            // lueckenhafte Sicherung spaeter wie eine vollstaendige aus.
            UnreadableDirectories = unreadableDirectories,
            GitCommit = TryResolveGitCommit(programRoot),
            Hinweis =
                "Ableitbares fehlt bewusst: Build-Ausgabe, Python-Umgebung, Kartenkacheln, Arbeitsreste. "
                + "Nach dem Auspacken werden sie durch Bauen bzw. Nachladen neu erzeugt.",
            HinweisCommit =
                "GitCommit beschreibt nur den zuletzt eingecheckten Stand. Nicht eingecheckte "
                + "Arbeitsdateien sind ebenfalls enthalten; der Commit identifiziert den Inhalt "
                + "dieser Sicherung deshalb nicht eindeutig.",
            HinweisPruefsumme =
                "Die SHA-256-Pruefsumme der Sicherung selbst liegt als Nebendatei <name>.zip.sha256 "
                + "daneben - im Manifest wuerde sie sich selbst enthalten."
        };

        var entry = zip.CreateEntry("_manifest.json", CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, manifest, cancellationToken: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Der Commit ist nur eine Notiz im Manifest. Ein fehlendes oder unlesbares
    /// Git darf die fertige Sicherung nicht verhindern.
    /// </summary>
    private string? TryResolveGitCommit(string programRoot)
    {
        if (_gitCommits is null)
            return null;

        try
        {
            return _gitCommits.Resolve(programRoot);
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[ProgramSnapshot] Git-Commit nicht lesbar: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static void CommitArchive(string temporaryPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
            File.Replace(temporaryPath, destinationPath, null, ignoreMetadataErrors: true);
        else
            File.Move(temporaryPath, destinationPath);
    }

    /// <summary>Meldet, ob das Ziel innerhalb der Quelle liegt (Selbstaufnahme).</summary>
    private static bool IsInside(string root, string candidate)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var relative = Path.GetRelativePath(normalizedRoot, Path.GetFullPath(candidate));
        return !relative.StartsWith("..", StringComparison.Ordinal)
               && !Path.IsPathRooted(relative);
    }

    private static ProgramSnapshotResult Failed(string error)
        => new(false, error, 0, 0, 0);
}
