using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Parst den Haltungs-/Video-Header aus KINS-TXT-Exportdateien.
/// Extrahiert aus HoldingFolderDistributor.SidecarXtf – verhaltensneutral.
/// </summary>
internal static class KinsTxtHeaderParser
{
    /// <summary>
    /// Regex fuer eine Haltungs-Kopfzeile im KINS-TXT-Format:
    ///   &lt;Nutzungsart&gt; &lt;Von&gt; -&gt; &lt;Nach&gt; ... @Datei=&lt;Videodatei&gt;
    /// </summary>
    internal static readonly Regex KinsTxtHeaderRegex = new(
        @"^\s*(?<usage>\S+)\s+(?<from>[0-9.]+)\s*->\s*(?<to>[0-9.]+).*?@Datei=(?<video>[^\s]+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Versucht, eine Haltungs-ID und den Videodateinamen aus einer KINS-TXT-Kopfzeile zu lesen.
    /// Gibt true zurueck wenn das Muster erkannt wurde.
    /// </summary>
    internal static bool TryParseTxtHeader(string line, out string haltung, out string videoFile)
    {
        haltung = string.Empty;
        videoFile = string.Empty;
        var match = KinsTxtHeaderRegex.Match(line ?? string.Empty);
        if (!match.Success)
            return false;

        var from = match.Groups["from"].Value.Trim();
        var to = match.Groups["to"].Value.Trim();
        var video = match.Groups["video"].Value.Trim();
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return false;

        haltung = $"{from}-{to}";
        videoFile = video;
        return true;
    }
}
