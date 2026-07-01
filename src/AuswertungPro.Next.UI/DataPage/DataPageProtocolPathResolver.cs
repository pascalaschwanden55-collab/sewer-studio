using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Reine Such- und Pfadaufloesung fuer Inspektionsprotokoll-PDFs.
/// Aus DataPageViewModel extrahiert: keine ViewModel-Abhaengigkeit mehr —
/// alle Umgebungswerte (Einstellungen, gespeicherte PDF-Liste) kommen als Parameter.
/// </summary>
public static class DataPageProtocolPathResolver
{
    /// <summary>
    /// Loest einen Roh-Pfad zu einer existierenden Datei auf. Relative Pfade werden
    /// gegen den Ordner des Projekts (<paramref name="projectPath"/>) aufgeloest.
    /// </summary>
    public static string? ResolveExistingPath(string? raw, string? projectPath)
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
    public static string? FindProtocolPath(
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

            var fromStored = TryFindProtocolFromStoredPdfFiles(storedFilesRaw, projectDir, holdingTokens);
            if (!string.IsNullOrWhiteSpace(fromStored))
                return fromStored;
        }

        return null;
    }

    /// <summary>
    /// Loest alle Original-PDF-Pfade einer Haltung auf (Felder PDF_Path und PDF_All).
    /// </summary>
    public static List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
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
    public static void AddResolvedPdf(List<string> paths, string? raw, string projectFolder)
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
    public static void ResolveSchachtPdfPaths(SchachtRecord schacht, string projectFolder, List<string> paths)
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
    public static IReadOnlyList<string> BuildHoldingTokens(HaltungRecord record)
        => ProtocolPathResolver.BuildHoldingTokens(record);

    /// <summary>
    /// Waehlt aus mehreren PDF-Kandidaten den besten: bevorzugt einen Treffer mit
    /// Suffix "_&lt;token&gt;.pdf", sonst den lexikografisch letzten Dateinamen.
    /// Delegiert an <see cref="PdfCandidateSelector.PickBest"/>.
    /// </summary>
    public static string? PickBestPdfCandidate(IEnumerable<string> candidates, IReadOnlyList<string> holdingTokens)
        => PdfCandidateSelector.PickBest(candidates, holdingTokens);

    /// <summary>
    /// Parst die in den Projekt-Metadaten gespeicherte PDF-Liste (JSON-Array; faellt
    /// auf Semikolon-Trennung zurueck).
    /// Delegiert an <see cref="PdfCandidateSelector.ParseStoredPathList"/>.
    /// </summary>
    public static IReadOnlyList<string> ParseStoredPathList(string raw)
        => PdfCandidateSelector.ParseStoredPathList(raw);

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

    private static string? TryFindProtocolFromStoredPdfFiles(string? storedFilesRaw, string projectDir, IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(storedFilesRaw))
            return null;

        var candidates = new List<string>();
        foreach (var stored in ParseStoredPathList(storedFilesRaw))
        {
            var resolved = TryResolveStoredPath(projectDir, stored);
            if (string.IsNullOrWhiteSpace(resolved))
                continue;
            if (!string.Equals(Path.GetExtension(resolved), ".pdf", StringComparison.OrdinalIgnoreCase))
                continue;
            candidates.Add(resolved);
        }

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
        return PickBestPdfCandidate(files, holdingTokens);
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

    private static string? TryResolveStoredPath(string projectDir, string rawPath)
    {
        var path = (rawPath ?? string.Empty).Trim();
        if (path.Length == 0)
            return null;

        try
        {
            var full = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(projectDir, path));
            return File.Exists(full) ? full : null;
        }
        catch
        {
            return null;
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
