using System.Text.Json;
using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Liest die bereits gespeicherte Projektzuordnung, bevor ein Spiegel veraendert wird.</summary>
internal static class BackupProjectTargetMapping
{
    public static IReadOnlyDictionary<string, string> Read(string backupRoot)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifestPath = BackupTargetPathGuard.ResolveRelativePath(backupRoot, "manifest.json");
        try
        {
            using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = JsonDocument.Parse(stream);
            var plan = RequiredProperty(document.RootElement, "Plan", JsonValueKind.Array);
            var projectComponents = plan.EnumerateArray()
                .Where(component => RequiredProperty(component, "Name", JsonValueKind.String).GetString() == "Projekte")
                .ToArray();
            if (projectComponents.Length != 1)
                throw InvalidMapping("Die Projektkomponente fehlt oder ist mehrfach vorhanden.");
            var sources = RequiredProperty(projectComponents[0], "Sources", JsonValueKind.Array);
            foreach (var source in sources.EnumerateArray())
            {
                var sourcePath = RequiredProperty(source, "SourceRoot", JsonValueKind.String).GetString()!;
                var targetPath = RequiredProperty(source, "TargetRelativeRoot", JsonValueKind.String).GetString()!;
                if (string.IsNullOrWhiteSpace(sourcePath) || !Path.IsPathFullyQualified(sourcePath))
                    throw InvalidMapping("Eine Projektquelle hat keinen eindeutigen absoluten Pfad.");
                sourcePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourcePath));
                targetPath = ValidateTarget(backupRoot, targetPath);
                if (!mappings.TryAdd(sourcePath, targetPath) || !targets.Add(targetPath))
                    throw InvalidMapping("Eine Projektquelle oder ihr Ziel ist mehrfach zugeordnet.");
            }
        }
        catch (FileNotFoundException)
        {
            // Nur ohne vorhandenen Projektspiegel ist dies ein sicherer Erstlauf.
        }
        catch (JsonException ex)
        {
            throw InvalidMapping("manifest.json ist nicht lesbar.", ex);
        }

        EnsureExistingProjectsAreMapped(backupRoot, targets);
        return mappings;
    }

    /// <summary>
    /// Nur fuer den gespeicherten Herkunftsnachweis: Videoschutz oder ein nicht
    /// loeschbarer Altbestand koennen Kopien entfernter Quellen zuruecklassen.
    /// Diese Quellen werden dadurch nicht wieder in den Kopierplan aufgenommen.
    /// </summary>
    public static IReadOnlyList<BackupComponent> ForManifest(
        IReadOnlyList<BackupComponent> plan,
        IReadOnlyDictionary<string, string> previousTargets,
        string backupRoot)
    {
        var projects = plan.Single(component => component.Name == "Projekte");
        var sources = projects.Sources.ToList();
        var currentSources = sources.Select(source => source.SourceRoot).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var previous in previousTargets)
        {
            if (currentSources.Contains(previous.Key)) continue;
            var target = BackupTargetPathGuard.ResolveRelativePath(backupRoot, previous.Value);
            if (Directory.Exists(target))
                sources.Add(new BackupSource(previous.Key, previous.Value, Required: false));
        }
        return plan.Select(component => component == projects ? projects with { Sources = sources } : component).ToArray();
    }

    private static JsonElement RequiredProperty(JsonElement element, string name, JsonValueKind kind)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw InvalidMapping("Ein Eintrag im bisherigen Plan ist ungültig.");
        var properties = element.EnumerateObject().Where(property => property.Name == name).ToArray();
        if (properties.Length != 1 || properties[0].Value.ValueKind != kind)
            throw InvalidMapping($"Die Angabe {name} fehlt, ist mehrfach vorhanden oder ungültig.");
        return properties[0].Value;
    }

    private static string ValidateTarget(string backupRoot, string target)
    {
        // Projektziele sind direkte Unterordner von Projekte. Keine Elternsegmente,
        // fremden Komponenten oder Windows-Namensaliase aus einem Manifest uebernehmen.
        var segments = target.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar);
        if (segments.Length != 2 || !segments[0].Equals("Projekte", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[1]) || segments[1] is "." or ".."
            || segments[1].IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || segments[1].EndsWith('.') || segments[1].EndsWith(' '))
            throw InvalidMapping("Ein Projektziel liegt nicht eindeutig im Projektspiegel.");
        var relative = Path.Combine("Projekte", segments[1]);
        _ = BackupTargetPathGuard.ResolveRelativePath(backupRoot, relative);
        return relative;
    }

    private static void EnsureExistingProjectsAreMapped(string backupRoot, ISet<string> targets)
    {
        var projectsRoot = BackupTargetPathGuard.ResolveRelativePath(backupRoot, "Projekte");
        if (File.Exists(projectsRoot))
            throw InvalidMapping("Der Projektspiegel ist kein Ordner.");
        if (!Directory.Exists(projectsRoot)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(projectsRoot))
        {
            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, entry);
            var relative = Path.GetRelativePath(backupRoot, entry);
            if (!Directory.Exists(entry))
                throw InvalidMapping($"Die vorhandene Projektkopie {relative} ist keiner Quelle eindeutig zugeordnet.");
            if (targets.Contains(relative)) continue;
            // Die Ruecknahme eines Erstlaufs entfernt seine Dateien, darf aber leere
            // Ordner zuruecklassen. Nur ein sicherer, vollstaendig dateileerer Baum
            // benoetigt noch keinen Herkunftsnachweis.
            BackupTargetPathGuard.EnsureTreeIsSafe(entry);
            if (Directory.EnumerateFiles(entry, "*", SearchOption.AllDirectories).Any())
                throw InvalidMapping($"Die vorhandene Projektkopie {relative} ist keiner Quelle eindeutig zugeordnet.");
        }
    }

    private static InvalidDataException InvalidMapping(string detail, Exception? inner = null)
        => new("Die bisherige Projektzuordnung der Sicherung ist unklar. " + detail +
               " Die Sicherung wurde vor dem Kopieren und Bereinigen gestoppt.", inner);
}
