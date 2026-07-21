using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>
/// Dateibasierte Such- und Pfadaufloesung fuer Inspektionsprotokoll-PDFs.
/// Aus DataPageViewModel extrahiert: keine ViewModel-Abhaengigkeit mehr —
/// alle Umgebungswerte kommen als Parameter; gespeicherte Importpfade werden ueber
/// den gemeinsamen IStoredImportFilePathResolver aufgeloest.
/// </summary>
public sealed class InspectionProtocolFileLocator : IInspectionProtocolFileLocator
{
    private const string StoredPdfFilesMetadataKey = "PDF_StoredFiles";
    private readonly IStoredImportFilePathResolver _storedImportFilePaths;

    public InspectionProtocolFileLocator()
        : this(new StoredImportFilePathResolver())
    {
    }

    public InspectionProtocolFileLocator(IStoredImportFilePathResolver storedImportFilePaths)
    {
        _storedImportFilePaths = storedImportFilePaths
            ?? throw new ArgumentNullException(nameof(storedImportFilePaths));
    }

    /// <summary>
    /// Loest einen Roh-Pfad zu einer existierenden Datei auf. Relative Pfade werden
    /// gegen den Ordner des Projekts (<paramref name="projectPath"/>) aufgeloest.
    /// </summary>
    public string? ResolveExistingPath(string? raw, string? projectPath)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (File.Exists(path))
            return path;

        if (!Path.IsPathRooted(path))
        {
            var baseDir = ResolveProjectRoot(projectPath);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var combined = Path.GetFullPath(Path.Combine(baseDir, path));
                if (File.Exists(combined))
                    return combined;
            }
        }

        return null;
    }

    /// <summary>
    /// Ermittelt den Projekt-ROOT aus dem projekt.json-Pfad. Seit die projekt.json unter
    /// <c>Projektdateien\</c> liegen kann, ist <see cref="Path.GetDirectoryName(string)"/> NICHT
    /// mehr der Root — relative Medienpfade (z.B. <c>Haltungen_Verteilt\…</c>) sind aber relativ
    /// zum Root gespeichert. <see cref="ProjectFileLocator.ProjectRootFromFile"/> liefert den echten
    /// Root (rueckwaertskompatibel: liegt die Datei direkt im Root, ist es dessen Verzeichnis).
    /// </summary>
    private static string? ResolveProjectRoot(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return null;
        return ProjectFileLocator.ProjectRootFromFile(projectPath)
               ?? Path.GetDirectoryName(projectPath);
    }

    /// <summary>
    /// Sucht das zur Haltung passende Protokoll-PDF ueber mehrere Strategien:
    /// 1) ueber den (bereits aufgeloesten) Link, 2) im Startordner, 3) im Projekt
    /// (Haltungen-Unterordner und in den Metadaten gespeicherte PDF-Liste).
    /// </summary>
    public string? FindProtocolPath(
        HaltungRecord record,
        string? resolvedLink,
        string? initialFolder,
        string? projectPath,
        string? storedFilesRaw)
    {
        var holdingTokens = BuildHoldingTokens(record);

        var fromLink = TryResolveProtocolFromLink(resolvedLink, holdingTokens);
        if (!string.IsNullOrWhiteSpace(fromLink))
            return fromLink;

        var fromInitial = TryFindProtocolFromRoot(initialFolder, holdingTokens);
        if (!string.IsNullOrWhiteSpace(fromInitial))
            return fromInitial;

        var projectDir = ResolveProjectRoot(projectPath);
        if (!string.IsNullOrWhiteSpace(projectDir))
        {
            // Neue Struktur: verteilte Protokolle liegen in Haltungen_Verteilt\; alte in Haltungen\.
            var fromVerteilt = TryFindProtocolFromRoot(Path.Combine(projectDir, "Haltungen_Verteilt"), holdingTokens);
            if (!string.IsNullOrWhiteSpace(fromVerteilt))
                return fromVerteilt;

            var fromHoldings = TryFindProtocolFromRoot(Path.Combine(projectDir, "Haltungen"), holdingTokens);
            if (!string.IsNullOrWhiteSpace(fromHoldings))
                return fromHoldings;

            var fromImportPdf = TryFindProtocolFromRoot(Path.Combine(projectDir, "Importdateien", "PDF"), holdingTokens);
            if (!string.IsNullOrWhiteSpace(fromImportPdf))
                return fromImportPdf;

            var fromImportFiles = TryFindProtocolFromRoot(Path.Combine(projectDir, "Importdateien"), holdingTokens);
            if (!string.IsNullOrWhiteSpace(fromImportFiles))
                return fromImportFiles;

        }

        var fromStored = TryFindProtocolFromStoredPdfFiles(
            storedFilesRaw,
            projectPath,
            holdingTokens);
        if (!string.IsNullOrWhiteSpace(fromStored))
            return fromStored;

        return null;
    }

    /// <summary>
    /// Loest alle Original-PDF-Pfade einer Haltung auf (Felder PDF_Path und PDF_All).
    /// </summary>
    public List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
    {
        var paths = new List<string>();

        // PDF_Path
        var pdfPath = record.GetFieldValue(FieldKeys.PdfPath)?.Trim();
        AddResolvedPdf(paths, pdfPath, projectFolder);

        // PDF_All (semikolon-getrennt)
        var pdfAll = record.GetFieldValue(FieldKeys.PdfAll)?.Trim();
        if (!string.IsNullOrWhiteSpace(pdfAll))
        {
            foreach (var part in pdfAll.Split(';', StringSplitOptions.RemoveEmptyEntries))
                AddResolvedPdf(paths, part.Trim(), projectFolder);
        }

        return paths;
    }

    /// <summary>
    /// Loest einen Roh-PDF-Pfad auf und fuegt ihn (ohne Duplikate) der Liste hinzu.
    /// Faellt auf eine Dateinamen-Suche im Projektordner zurueck, wenn der Pfad nicht existiert.
    /// </summary>
    public void AddResolvedPdf(List<string> paths, string? raw, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);

        // Absoluter Pfad
        if (Path.IsPathRooted(normalized))
        {
            if (File.Exists(normalized))
            {
                if (!paths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    paths.Add(normalized);
                return;
            }

            // Fallback: absoluter Pfad existiert nicht (Laufwerk nicht gemountet) → Dateinamen im Projektordner suchen
            if (!string.IsNullOrWhiteSpace(projectFolder))
            {
                var fallback = TryFindPdfInProject(Path.GetFileName(normalized), projectFolder);
                if (fallback != null && !paths.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                    paths.Add(fallback);
            }
            return;
        }

        // Relativer Pfad
        if (!string.IsNullOrWhiteSpace(projectFolder))
        {
            var combined = Path.GetFullPath(Path.Combine(projectFolder, normalized));
            if (File.Exists(combined))
            {
                if (!paths.Contains(combined, StringComparer.OrdinalIgnoreCase))
                    paths.Add(combined);
                return;
            }

            // Fallback: relativer Pfad nicht aufloesbar → Dateinamen im Projektordner suchen
            var fallback = TryFindPdfInProject(Path.GetFileName(normalized), projectFolder);
            if (fallback != null && !paths.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                paths.Add(fallback);
        }
    }

    /// <summary>
    /// Sammelt die zu einem Schacht gehoerenden PDF-Pfade (PDF_Path und ein
    /// PDF-Link) und haengt die aufgeloesten Pfade an die uebergebene Liste an.
    /// </summary>
    public void ResolveSchachtPdfPaths(SchachtRecord schacht, string projectFolder, List<string> paths)
    {
        var pdfPath = schacht.GetFieldValue(FieldKeys.PdfPath)?.Trim();
        AddResolvedPdf(paths, pdfPath, projectFolder);

        var link = schacht.GetFieldValue(FieldKeys.Link)?.Trim();
        if (!string.IsNullOrWhiteSpace(link) && link.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            AddResolvedPdf(paths, link, projectFolder);
    }

    /// <summary>
    /// Baut die Suchtoken einer Haltung: sanitisierter Name plus Rohname (dedupliziert).
    /// Delegiert an <see cref="ProtocolPathResolver.BuildHoldingTokens"/>.
    /// </summary>
    private static IReadOnlyList<string> BuildHoldingTokens(HaltungRecord record)
        => ProtocolPathResolver.BuildHoldingTokens(record);

    /// <summary>
    /// Waehlt aus mehreren PDF-Kandidaten den besten: bevorzugt einen Treffer mit
    /// Suffix "_&lt;token&gt;.pdf", sonst den lexikografisch letzten Dateinamen.
    /// Delegiert an <see cref="PdfCandidateSelector.PickBest"/>.
    /// </summary>
    private static string? PickBestPdfCandidate(IEnumerable<string> candidates, IReadOnlyList<string> holdingTokens)
        => PdfCandidateSelector.PickBest(candidates, holdingTokens);

    private static string? TryResolveProtocolFromLink(string? resolvedLink, IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(resolvedLink))
            return null;

        if (string.Equals(Path.GetExtension(resolvedLink), ".pdf", StringComparison.OrdinalIgnoreCase))
            return resolvedLink;

        var folder = Path.GetDirectoryName(resolvedLink);
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        var inSameFolder = TryFindPdfInDirectory(folder, holdingTokens, SearchOption.TopDirectoryOnly);
        if (!string.IsNullOrWhiteSpace(inSameFolder))
            return inSameFolder;

        try
        {
            var parent = Directory.GetParent(folder);
            if (parent is not null && string.Equals(parent.Name, "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
            {
                var gemeindeRoot = parent.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(gemeindeRoot))
                {
                    var inGemeinde = TryFindProtocolFromRoot(gemeindeRoot, holdingTokens);
                    if (!string.IsNullOrWhiteSpace(inGemeinde))
                        return inGemeinde;
                }
            }
        }
        catch
        {
            // Weiter mit anderen Suchstrategien.
        }

        return null;
    }

    private static string? TryFindProtocolFromRoot(string? rootDir, IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            return null;

        var holdingDir = TryFindHoldingDirectory(rootDir, holdingTokens);
        if (!string.IsNullOrWhiteSpace(holdingDir))
        {
            var inHolding = TryFindPdfInDirectory(holdingDir, holdingTokens, SearchOption.TopDirectoryOnly);
            if (!string.IsNullOrWhiteSpace(inHolding))
                return inHolding;

            var inHoldingRecursive = TryFindPdfInDirectory(holdingDir, holdingTokens, SearchOption.AllDirectories);
            if (!string.IsNullOrWhiteSpace(inHoldingRecursive))
                return inHoldingRecursive;
        }

        return TryFindPdfInDirectory(rootDir, holdingTokens, SearchOption.AllDirectories);
    }

    private string? TryFindProtocolFromStoredPdfFiles(
        string? storedFilesRaw,
        string? projectPath,
        IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(storedFilesRaw))
            return null;

        var metadata = new Dictionary<string, string>
        {
            [StoredPdfFilesMetadataKey] = storedFilesRaw
        };
        var candidates = _storedImportFilePaths.ResolveExistingFiles(
                metadata,
                StoredPdfFilesMetadataKey,
                projectPath)
            .Where(path => string.Equals(
                Path.GetExtension(path),
                ".pdf",
                StringComparison.OrdinalIgnoreCase));

        return PickBestPdfCandidate(candidates, holdingTokens);
    }

    private static string? TryFindHoldingDirectory(string rootDir, IReadOnlyList<string> holdingTokens)
    {
        if (holdingTokens.Count == 0)
            return null;

        foreach (var token in holdingTokens)
        {
            var direct = Path.Combine(rootDir, token);
            if (Directory.Exists(direct))
                return direct;
        }

        foreach (var sub in SafeEnumerateDirectories(rootDir))
        {
            if (string.Equals(Path.GetFileName(sub), "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var token in holdingTokens)
            {
                var candidate = Path.Combine(sub, token);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static string? TryFindPdfInDirectory(string directory, IReadOnlyList<string> holdingTokens, SearchOption searchOption)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        var files = SafeEnumerateFiles(directory, "*.pdf", searchOption);
        return PickBestProtocolPdfCandidate(files, holdingTokens);
    }

    private enum ProtocolPdfCandidateKind
    {
        Unknown,
        Protocol,
        Plan
    }

    private static string? PickBestProtocolPdfCandidate(IEnumerable<string> candidates, IReadOnlyList<string> holdingTokens)
    {
        var list = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count == 0)
            return null;

        var classified = list
            .Select(path => new { Path = path, Kind = ClassifyProtocolPdfCandidate(path, holdingTokens) })
            .ToList();

        var protocols = classified
            .Where(x => x.Kind == ProtocolPdfCandidateKind.Protocol)
            .Select(x => x.Path)
            .ToList();
        if (protocols.Count > 0)
            return PickBestPdfCandidate(protocols, holdingTokens);

        var unknown = classified
            .Where(x => x.Kind == ProtocolPdfCandidateKind.Unknown)
            .Select(x => x.Path)
            .ToList();

        return PickBestPdfCandidate(unknown, holdingTokens);
    }

    private static ProtocolPdfCandidateKind ClassifyProtocolPdfCandidate(string path, IReadOnlyList<string> holdingTokens)
    {
        var text = ReadPdfTextPrefix(path, maxPages: 6);
        if (string.IsNullOrWhiteSpace(text))
            return ProtocolPdfCandidateKind.Unknown;

        var hasToken = holdingTokens.Any(token =>
            !string.IsNullOrWhiteSpace(token) &&
            text.Contains(token, StringComparison.OrdinalIgnoreCase));
        var hasProtocolMarker =
            text.Contains("Haltungsinspektion", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Haltungsbilder", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Leitungs-Stammdaten", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Leitungsbericht", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Leitungsgrafik", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Leitungsbildbericht", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Insp.-Datum", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Kanalinspektion", StringComparison.OrdinalIgnoreCase);

        if (hasToken && hasProtocolMarker)
            return ProtocolPdfCandidateKind.Protocol;

        var fileName = Path.GetFileName(path);
        var looksLikePlan =
            fileName.Contains("plan", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Leitungsende", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Dachwasser angeschlossen", StringComparison.OrdinalIgnoreCase);

        if (looksLikePlan && !hasProtocolMarker)
            return ProtocolPdfCandidateKind.Plan;

        return ProtocolPdfCandidateKind.Unknown;
    }

    private static string? ReadPdfTextPrefix(string path, int maxPages)
    {
        try
        {
            using var document = PdfDocument.Open(path);
            return string.Join(
                "\n",
                document.GetPages()
                    .Take(Math.Max(1, maxPages))
                    .Select(page => page.Text));
        }
        catch
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }
    }

    private static IReadOnlyList<string> SafeEnumerateFiles(string directory, string pattern, SearchOption searchOption)
    {
        try
        {
            return AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration
                .EnumerateFilesSafe(directory, pattern, recursive: searchOption == SearchOption.AllDirectories)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? TryFindPdfInProject(string fileName, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // 1. Direkt im Projektordner
        var direct = Path.Combine(projectFolder, fileName);
        if (File.Exists(direct))
            return direct;

        // 2. In Haltungen/<ID>/ Unterordnern
        var haltungenDir = Path.Combine(projectFolder, "Haltungen");
        if (Directory.Exists(haltungenDir))
        {
            try
            {
                var found = AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration
                    .EnumerateFilesSafe(haltungenDir, fileName, recursive: true)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                if (found.Length > 0)
                    return found[0];
            }
            catch { /* Zugriffsfehler ignorieren */ }
        }

        // 3. In typischen Unterordnern (Misc, Docu, PDF, Protokolle)
        foreach (var sub in new[] { "Misc", "Docu", "PDF", "Protokolle", "Dokumente" })
        {
            var subDir = Path.Combine(projectFolder, sub);
            if (!Directory.Exists(subDir))
                continue;
            try
            {
                var found = AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration
                    .EnumerateFilesSafe(subDir, fileName, recursive: true)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                if (found.Length > 0)
                    return found[0];
            }
            catch { /* Zugriffsfehler ignorieren */ }
        }

        return null;
    }
}
