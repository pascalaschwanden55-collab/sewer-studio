namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Struktur fuer den geparsten Haltungs-Header einer KINS-TXT-Datei.
/// Wird von <see cref="KinsTextLineParser.TryParseHeaderLine"/> befuellt.
/// </summary>
internal readonly record struct KinsHoldingHeader(
    string Usage,
    string From,
    string To,
    string Material,
    string? Diameter,
    string VideoFile);
