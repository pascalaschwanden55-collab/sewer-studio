using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

internal static class TrainingInventoryPaths
{
    public static string NormalizeRequired(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("KnowledgeRoot darf nicht leer sein.", nameof(path));
        return Path.GetFullPath(path.Trim());
    }

    public static string? NormalizeOptional(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim());

    public static IReadOnlyList<string> NormalizeDistinct(IEnumerable<string> paths)
        => paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string ResolveAgainstRoot(string path, string root)
    {
        var trimmed = path.Trim();
        return Path.IsPathFullyQualified(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(trimmed, root);
    }

    public static bool IsWithinAny(string path, IReadOnlyList<string> roots)
        => roots.Any(root => IsWithin(path, root));

    public static bool IsWithin(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
               || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    // Gemeinsame Verknuepfungspruefung (Deepscan A5): ganzer Pfad ab Laufwerk, ein noch fehlender
    // Rest ist erlaubt, Lesefehler werfen ihre urspruengliche Ausnahme. Gemeldet wird das unterste
    // verknuepfte Glied (frueher das oberste; nur bei mehreren Verknuepfungen verschieden).
    public static string? FindReparsePoint(string path)
    {
        var befund = VerknuepfungsSchutz.PruefePfadAbLaufwerk(path, VerknuepfungsRegel.GanzerPfad);
        if (befund.Fehler is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(befund.Fehler);
        return befund.Befund == VerknuepfungsBefund.Verknuepfung ? befund.Pfad : null;
    }
}
