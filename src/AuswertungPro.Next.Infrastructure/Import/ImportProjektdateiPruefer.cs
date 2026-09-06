using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>Prüft die gespeicherten Medien-/Protokollverweise gegen die echte Projekt-Lesesicht.</summary>
internal static class ImportProjektdateiPruefer
{
    internal static (ImportBestandsbilanz Bestand, IReadOnlyList<string> Fehler) Pruefe(
        Project project, string root, IImportFileStagingSession? staging)
    {
        var ergebnisse = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var fehler = new List<string>();
        bool PruefePfad(string path)
        {
            if (ergebnisse.TryGetValue(path, out var vorhanden)) return vorhanden;
            var lesbar = IstLesbar(path, root, staging, out var grund);
            ergebnisse[path] = lesbar;
            if (!lesbar) fehler.Add($"Projektdatei fehlt oder ist nicht lesbar: {path} — {grund}");
            return lesbar;
        }
        var bestand = ImportBestandszaehler.Zaehle(project, PruefePfad);
        foreach (var protokoll in project.Data.Select(h => h.Protocol)
                     .Concat(project.SchaechteData.Select(s => s.Protocol)).Where(p => p is not null))
        {
            foreach (var revision in new[] { protokoll!.Original, protokoll.Current }.Concat(protokoll.History))
            {
                foreach (var path in revision.ImportVideoPaths ?? [])
                    if (!string.IsNullOrWhiteSpace(path)) PruefePfad(path);
            }
        }
        return (bestand, fehler);
    }

    internal static bool IstLesbar(string path, string root, IImportFileStagingSession? staging, out string? grund)
    {
        try
        {
            if (!Path.IsPathRooted(path) && !ProjectPathResolver.IsSafeRelativeProjectPath(path))
                throw new IOException("Ungültiger relativer Projektpfad.");
            var guard = new ProjectWritePathGuard(root);
            var target = guard.EnsureSafeFileTarget(Path.GetFullPath(Path.Combine(root, path)));
            var read = guard.EnsureSafeFileTarget(staging?.ResolveReadPath(target) ?? target);
            using var stream = File.OpenRead(read);
            if (stream.ReadByte() < 0) throw new IOException("Datei ist leer.");
            grund = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            grund = ex.Message;
            return false;
        }
    }
}
