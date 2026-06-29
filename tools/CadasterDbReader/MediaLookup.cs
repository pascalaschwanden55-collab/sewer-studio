namespace CadasterDbReader;

/// <summary>
/// Kein-IO-Klasse: mappt Dateinamen auf vollstaendige Pfade fuer Foto- und Video-Lookup.
/// Der Aufbau des Index via EnumerateFilesSafe (IO) verbleibt in Program.cs (BuildMediaLookup).
/// </summary>
internal sealed class MediaLookup
{
    private readonly Dictionary<string, string> _byFileName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initialisiert den Lookup-Index aus einer Menge von Dateipfaden.
    /// </summary>
    public MediaLookup(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            if (!_byFileName.ContainsKey(name))
                _byFileName[name] = path;
        }
    }

    /// <summary>
    /// Sucht einen Pfad anhand von Dateiname und optionaler Erweiterung.
    /// </summary>
    public string? Resolve(string? fileName, string? extension)
    {
        foreach (var candidate in CandidateNames(fileName, extension))
        {
            if (_byFileName.TryGetValue(candidate, out var path))
                return path;
        }
        return null;
    }

    /// <summary>
    /// Erzeugt Kandidaten-Dateinamen mit/ohne Erweiterung und mit/ohne Pfadpraefix.
    /// </summary>
    private static IEnumerable<string> CandidateNames(string? fileName, string? extension)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            yield break;

        var cleanName = fileName.Trim();
        yield return cleanName;
        var baseName = Path.GetFileName(cleanName);
        if (!string.Equals(baseName, cleanName, StringComparison.OrdinalIgnoreCase))
            yield return baseName;

        var ext = NormalizeExtension(extension);
        if (!string.IsNullOrWhiteSpace(ext) && string.IsNullOrWhiteSpace(Path.GetExtension(cleanName)))
        {
            yield return cleanName + ext;
            if (!string.Equals(baseName, cleanName, StringComparison.OrdinalIgnoreCase))
                yield return baseName + ext;
        }
    }

    /// <summary>
    /// Normalisiert eine Dateiendung: fuegt fuehrenden Punkt hinzu falls fehlend.
    /// </summary>
    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;
        var ext = extension.Trim();
        return ext.StartsWith('.') ? ext : "." + ext;
    }
}
