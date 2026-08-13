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

    public ProgramSnapshotService(IGitCommitResolver? gitCommits = null)
    {
        _gitCommits = gitCommits;
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

            var fileCount = 0;
            var skippedReparsePoints = 0;
            using (var zip = ZipFile.Open(temporaryArchivePath, ZipArchiveMode.Create))
            {
                foreach (var relativePath in EnumerateSnapshotFiles(programRoot, ref skippedReparsePoints, ct))
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

                await WriteManifestAsync(zip, programRoot, fileCount, skippedReparsePoints, ct)
                    .ConfigureAwait(false);
            }

            CommitArchive(temporaryArchivePath, destinationPath);
            temporaryArchivePath = null;

            var size = new FileInfo(destinationPath).Length;
            progress?.Report(
                $"Momentaufnahme fertig: {fileCount} Dateien, {size / (1024.0 * 1024.0):F1} MB");
            return new ProgramSnapshotResult(true, null, fileCount, size, skippedReparsePoints);
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
    /// Laeuft den Programmordner ab und liefert die aufzunehmenden Dateien als
    /// Pfade relativ zur Wurzel. Ausgeschlossene und verknuepfte Ordner werden
    /// gar nicht erst betreten; ein unlesbarer Unterordner beendet den Lauf nicht.
    /// </summary>
    internal static IEnumerable<string> EnumerateSnapshotFiles(
        string programRoot,
        ref int skippedReparsePoints,
        CancellationToken ct)
    {
        var results = new List<string>();
        var skipped = 0;
        var pending = new Stack<string>();
        pending.Push(programRoot);

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = pending.Pop();

            foreach (var directory in SafeEnumerate(() => Directory.EnumerateDirectories(current)))
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

            foreach (var file in SafeEnumerate(() => Directory.EnumerateFiles(current)))
            {
                if (ReparsePointGuard.IsReparsePoint(file))
                {
                    skipped++;
                    continue;
                }

                results.Add(Path.GetRelativePath(programRoot, file));
            }
        }

        skippedReparsePoints += skipped;
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }

    private static IEnumerable<string> SafeEnumerate(Func<IEnumerable<string>> enumerate)
    {
        try
        {
            return enumerate().ToList();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[ProgramSnapshot] Ordner nicht lesbar: {ex.GetType().Name}: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private async Task WriteManifestAsync(
        ZipArchive zip,
        string programRoot,
        int fileCount,
        int skippedReparsePoints,
        CancellationToken ct)
    {
        var manifest = new
        {
            Product = "SewerStudio",
            Kind = "ProgramSnapshot",
            Version = 1,
            CreatedUtc = DateTime.UtcNow.ToString("o"),
            ProgramRoot = programRoot,
            FileCount = fileCount,
            SkippedReparsePoints = skippedReparsePoints,
            GitCommit = TryResolveGitCommit(programRoot),
            Hinweis =
                "Ableitbares fehlt bewusst: Build-Ausgabe, Python-Umgebung, Kartenkacheln, Arbeitsreste. "
                + "Nach dem Auspacken werden sie durch Bauen bzw. Nachladen neu erzeugt."
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
