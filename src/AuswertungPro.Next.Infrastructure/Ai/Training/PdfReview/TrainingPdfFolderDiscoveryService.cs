using System.Security;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;

/// <summary>
/// Sucht PDFs unter mehreren Wurzeln. Verzeichnis- und Dateiverknuepfungen
/// werden nicht betreten, damit der Scan den gewaehlten Bereich nicht verlaesst.
/// </summary>
public sealed class TrainingPdfFolderDiscoveryService
    : ITrainingPdfFolderDiscoveryService
{
    private readonly Func<string, FileAttributes> _readAttributes;

    public TrainingPdfFolderDiscoveryService()
        : this(File.GetAttributes)
    {
    }

    internal TrainingPdfFolderDiscoveryService(
        Func<string, FileAttributes> readAttributes)
    {
        _readAttributes = readAttributes
                          ?? throw new ArgumentNullException(nameof(readAttributes));
    }

    public TrainingPdfFolderDiscoveryResult Discover(
        IReadOnlyList<string> roots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roots);

        var issues = new List<TrainingPdfFolderDiscoveryIssue>();
        var pdfPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();

        foreach (var root in roots.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryNormalizeRoot(root, out var fullRoot, out var error))
            {
                issues.Add(new TrainingPdfFolderDiscoveryIssue(
                    root ?? string.Empty,
                    "root_invalid",
                    error));
                continue;
            }

            if (!Directory.Exists(fullRoot))
            {
                issues.Add(new TrainingPdfFolderDiscoveryIssue(
                    fullRoot,
                    "root_missing",
                    $"Ordner wurde nicht gefunden: {fullRoot}"));
                continue;
            }

            if (!TryFindReparsePointInPath(
                    fullRoot,
                    issues,
                    "root_unreadable",
                    out var rootReparsePoint))
            {
                continue;
            }
            if (rootReparsePoint is not null)
            {
                issues.Add(new TrainingPdfFolderDiscoveryIssue(
                    rootReparsePoint,
                    "reparse_point",
                    $"Verknuepfte Root-Pfadkette wurde nicht durchsucht: {rootReparsePoint}"));
                continue;
            }

            pending.Push(fullRoot);
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            if (!TryFindReparsePointInPath(
                    directory,
                    issues,
                    "directory_unreadable",
                    out var currentReparsePoint))
            {
                continue;
            }
            if (currentReparsePoint is not null)
            {
                issues.Add(new TrainingPdfFolderDiscoveryIssue(
                    currentReparsePoint,
                    "reparse_point",
                    $"Verknuepfter Ordner wurde vor dem Lesen uebersprungen: {currentReparsePoint}"));
                continue;
            }

            if (!visitedDirectories.Add(directory))
                continue;

            string[] files;
            string[] directories;
            try
            {
                files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
                directories = Directory.GetDirectories(
                    directory,
                    "*",
                    SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (IsFileSystemReadError(ex))
            {
                issues.Add(new TrainingPdfFolderDiscoveryIssue(
                    directory,
                    "directory_unreadable",
                    $"Ordner konnte nicht gelesen werden: {directory}. {ex.Message}"));
                continue;
            }

            foreach (var file in files
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(
                        Path.GetExtension(file),
                        ".pdf",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryIsReparsePoint(
                        file,
                        issues,
                        "file_unreadable",
                        out var fileIsReparse))
                {
                    continue;
                }
                if (fileIsReparse)
                {
                    issues.Add(new TrainingPdfFolderDiscoveryIssue(
                        file,
                        "reparse_point",
                        $"Verknuepfte PDF-Datei wurde ausgelassen: {file}"));
                    continue;
                }

                pdfPaths.Add(Path.GetFullPath(file));
            }

            foreach (var child in directories
                         .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullChild = Path.GetFullPath(child);
                if (!TryIsReparsePoint(
                        fullChild,
                        issues,
                        "directory_unreadable",
                        out var childIsReparse))
                {
                    continue;
                }
                if (childIsReparse)
                {
                    issues.Add(new TrainingPdfFolderDiscoveryIssue(
                        fullChild,
                        "reparse_point",
                        $"Verknuepfter Ordner wurde nicht durchsucht: {fullChild}"));
                    continue;
                }

                pending.Push(fullChild);
            }
        }

        return new TrainingPdfFolderDiscoveryResult(
            pdfPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            issues);
    }

    private static bool TryNormalizeRoot(
        string? root,
        out string fullRoot,
        out string error)
    {
        fullRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
        {
            error = "Ein ausgewaehlter Ordnerpfad ist leer.";
            return false;
        }

        try
        {
            fullRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(root.Trim()));
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException
                                   or NotSupportedException
                                   or PathTooLongException)
        {
            error = $"Ordnerpfad ist ungueltig: {ex.Message}";
            return false;
        }
    }

    private bool TryFindReparsePointInPath(
        string path,
        ICollection<TrainingPdfFolderDiscoveryIssue> issues,
        string unreadableReason,
        out string? reparsePoint)
    {
        reparsePoint = null;
        var chain = new Stack<string>();
        var current = Path.GetFullPath(path);
        while (true)
        {
            chain.Push(current);
            var parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent)
                || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        while (chain.Count > 0)
        {
            var candidate = chain.Pop();
            if (!TryIsReparsePoint(
                    candidate,
                    issues,
                    unreadableReason,
                    out var isReparsePoint))
            {
                return false;
            }

            if (!isReparsePoint)
                continue;

            reparsePoint = candidate;
            return true;
        }

        return true;
    }

    private bool TryIsReparsePoint(
        string path,
        ICollection<TrainingPdfFolderDiscoveryIssue> issues,
        string unreadableReason,
        out bool isReparsePoint)
    {
        try
        {
            isReparsePoint =
                (_readAttributes(path) & FileAttributes.ReparsePoint) != 0;
            return true;
        }
        catch (Exception ex) when (IsFileSystemReadError(ex))
        {
            isReparsePoint = false;
            issues.Add(new TrainingPdfFolderDiscoveryIssue(
                path,
                unreadableReason,
                $"Pfad konnte nicht geprueft werden: {path}. {ex.Message}"));
            return false;
        }
    }

    private static bool IsFileSystemReadError(Exception ex)
        => ex is IOException
            or UnauthorizedAccessException
            or SecurityException;
}
