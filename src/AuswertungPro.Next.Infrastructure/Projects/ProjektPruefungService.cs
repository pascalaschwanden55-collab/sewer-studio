using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>Lesende Dateipruefung; jede Datei einmal pro Lauf, kein rekursiver Suchlauf.</summary>
public sealed class ProjektPruefungService : IProjektPruefung
{
    public ProjektPruefergebnis Pruefe(Project projekt, string? projektdatei, CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        return ProjektPruefregeln.Pruefe(projekt, raw =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = raw.Trim();
            if (cache.TryGetValue(path, out var vorhanden)) return vorhanden;
            return cache[path] = Dateifehler(path, projektdatei);
        }, cancellationToken);
    }

    internal static string? Dateifehler(string raw, string? projektdatei)
    {
        try
        {
            if (Path.IsPathRooted(raw) && !Path.IsPathFullyQualified(raw))
                return "Der Dateipfad ist nicht vollständig.";
            if (!Path.IsPathRooted(raw) && string.IsNullOrWhiteSpace(projektdatei))
                return "Ohne gespeicherten Projektordner nicht prüfbar.";
            if (!Path.IsPathRooted(raw) && !ProjectPathResolver.IsSafeRelativeProjectPath(raw))
                return "Ungültiger relativer Projektpfad.";
            var root = ProjectFileLocator.ProjectRootFromFile(projektdatei);
            if (!Path.IsPathRooted(raw) && (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)))
                return "Ohne gültigen Projektordner nicht prüfbar.";
            var ziel = Path.IsPathRooted(raw) ? raw : Path.Combine(root!, raw);
            if (!ImportSourcePathGuard.TryInspectFile(ziel, out var path, out var existiert, out var fehler))
                return "Datei nicht prüfbar: " + fehler;
            if (!existiert) return "Datei fehlt.";
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.ReadByte() < 0 ? "Datei ist leer." : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return $"Datei nicht prüfbar: {ex.Message}"; }
    }
}
