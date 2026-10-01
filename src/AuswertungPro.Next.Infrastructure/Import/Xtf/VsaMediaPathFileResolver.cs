using System.Diagnostics;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Loest Foto- und Videopfade aus KEK.Datei relativ zur XTF und ihren Export-Eltern auf.
/// Security (S2-1..S2-3): Es werden nur bekannte Medientypen aufgeloest, UNC-Pfade werden
/// verworfen (NTLM-Hash-Leak via SMB) und Relativpfade duerfen nicht aus dem XTF-Verzeichnis
/// ausbrechen. Absolute Pfade auf anderen Laufwerken bleiben erlaubt (WinCan-Workflows).
/// Verworfene Pfade werden wie "nicht gefunden" behandelt (leerer String) und per Trace gemeldet.
/// </summary>
public sealed class VsaMediaPathFileResolver : IVsaMediaPathResolver
{
    private static readonly string[] PhotoFolders = ["Foto", "Fotos", "Picture", "Pictures"];
    private static readonly string[] VideoFolders = ["Film", "Video", "Videos"];

    public string ResolvePhoto(string xtfPath, string? relativeFolder, string? fileName)
        => Resolve(xtfPath, relativeFolder, fileName, PhotoFolders);

    public string ResolveVideo(string xtfPath, string? relativeFolder, string? fileName)
        => Resolve(xtfPath, relativeFolder, fileName, VideoFolders);

    private static string Resolve(
        string xtfPath,
        string? relativeFolder,
        string? fileName,
        IReadOnlyList<string> subfolders)
    {
        var normalizedFileName = (fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedFileName))
            return string.Empty;

        // S2-1: Nur bekannte Medien-Endungen aufloesen — sonst koennte eine praeparierte XTF
        // beliebige lokale Dateien (Dokumente, EXE) referenzieren, die spaeter ins Projekt kopiert werden.
        if (!MediaFileAllowlist.IsMediaFile(normalizedFileName))
        {
            Trace.WriteLine($"[VsaMediaPathFileResolver] Medienpfad ohne bekannte Medien-Endung verworfen: {normalizedFileName}");
            return string.Empty;
        }

        // S2-3: UNC-Pfade ablehnen (Zugriff wuerde SMB-Authentifizierung an fremde Hosts ausloesen).
        if (MediaFileAllowlist.IsUnc(normalizedFileName))
        {
            Trace.WriteLine($"[VsaMediaPathFileResolver] UNC-Medienpfad verworfen: {normalizedFileName}");
            return string.Empty;
        }

        // Nur vollqualifizierte absolute Pfade bleiben erlaubt (z. B. Videos auf E:\).
        // Laufwerk-relative Formen wie "C:foto.jpg" haengen vom Prozess-Arbeitsordner ab
        // und sind deshalb kein stabiler, sicher pruefbarer Importpfad.
        if (Path.IsPathFullyQualified(normalizedFileName))
        {
            return ImportSourcePathGuard.TryInspectFile(
                normalizedFileName,
                out var safeAbsolutePath,
                out _,
                out _)
                ? safeAbsolutePath
                : string.Empty;
        }

        if (Path.IsPathRooted(normalizedFileName)
            || ContainsParentTraversal(normalizedFileName))
        {
            Trace.WriteLine(
                $"[VsaMediaPathFileResolver] Relativer Dateiname mit Verzeichnisbruch verworfen: {normalizedFileName}");
            return string.Empty;
        }

        var relative = (relativeFolder ?? string.Empty).Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var candidates = new List<string>();
        var xtfDirectory = Path.GetDirectoryName(xtfPath) ?? string.Empty;
        var directory = xtfDirectory;
        for (var level = 0; level < 3 && !string.IsNullOrWhiteSpace(directory); level++)
        {
            if (!string.IsNullOrWhiteSpace(relative))
            {
                // S2-2: Der explizite Relativpfad darf nicht aus dem XTF-Verzeichnis ausbrechen
                // (Traversal ueber "..\.."). Der Aufstieg ueber die Ebenen selbst bleibt erlaubt.
                var relativeCandidate = Path.GetFullPath(Path.Combine(directory, relative, normalizedFileName));
                if (IsContainedIn(relativeCandidate, xtfDirectory))
                    candidates.Add(relativeCandidate);
                else
                    Trace.WriteLine($"[VsaMediaPathFileResolver] Relativpfad mit Verzeichnisbruch verworfen: {relative}");
            }
            candidates.Add(Path.GetFullPath(Path.Combine(directory, normalizedFileName)));
            foreach (var subfolder in subfolders)
                candidates.Add(Path.GetFullPath(Path.Combine(directory, subfolder, normalizedFileName)));

            directory = Path.GetDirectoryName(directory) ?? string.Empty;
        }

        string? safeFallback = null;
        foreach (var candidate in candidates)
        {
            if (!ImportSourcePathGuard.TryInspectFile(
                    candidate,
                    out var safeCandidate,
                    out var exists,
                    out var error))
            {
                Trace.WriteLine(
                    $"[VsaMediaPathFileResolver] Unsicherer Medienkandidat verworfen: {error}");
                continue;
            }

            safeFallback ??= safeCandidate;
            if (exists)
                return safeCandidate;
        }

        return safeFallback ?? string.Empty;
    }

    private static bool IsContainedIn(string fullPath, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            return false;

        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsParentTraversal(string path)
        => path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
}
