namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Reine Pfad-Segment-Ersetzung fuer Haltungsnamen.
/// Kein IO, kein State — ausschliesslich String-Transformation.
/// </summary>
public static class HoldingPathRewriter
{
    /// <summary>
    /// Ersetzt das Haltungsname-Segment in einem Pfad.
    /// Funktioniert mit Backslash, Forward-Slash, am Anfang und am Ende.
    /// </summary>
    public static string ReplaceHoldingInPath(string path, string oldSan, string newSan)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var result = path;

        // Mitte: \OLD\ und /OLD/
        result = result.Replace("\\" + oldSan + "\\", "\\" + newSan + "\\", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("/" + oldSan + "/", "/" + newSan + "/", StringComparison.OrdinalIgnoreCase);

        // Ende: \OLD oder /OLD (ohne Trailing-Separator)
        if (result.EndsWith("\\" + oldSan, StringComparison.OrdinalIgnoreCase))
            result = result[..^oldSan.Length] + newSan;
        else if (result.EndsWith("/" + oldSan, StringComparison.OrdinalIgnoreCase))
            result = result[..^oldSan.Length] + newSan;

        // Anfang: OLD\ oder OLD/ (relative Pfade)
        if (result.StartsWith(oldSan + "\\", StringComparison.OrdinalIgnoreCase))
            result = newSan + result[oldSan.Length..];
        else if (result.StartsWith(oldSan + "/", StringComparison.OrdinalIgnoreCase))
            result = newSan + result[oldSan.Length..];

        return result;
    }
}
