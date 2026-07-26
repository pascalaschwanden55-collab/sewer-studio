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

        // Rooted/absolute Pfade bleiben erlaubt (z. B. Videos auf externem Laufwerk E:\).
        if (Path.IsPathRooted(normalizedFileName))
            return normalizedFileName;

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

        return candidates.FirstOrDefault(File.Exists)
               ?? (candidates.Count > 0 ? candidates[0] : normalizedFileName);
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
}
