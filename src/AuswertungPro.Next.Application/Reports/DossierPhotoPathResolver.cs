namespace AuswertungPro.Next.Application.Reports;

/// <summary>Ermittelt den absoluten Pfad eines Dossierfotos ohne Dateizugriff.</summary>
public static class DossierPhotoPathResolver
{
    public static string? Resolve(string? raw, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            return normalized;

        if (string.IsNullOrWhiteSpace(projectFolder))
            return null;

        return Path.GetFullPath(Path.Combine(projectFolder, normalized));
    }
}
