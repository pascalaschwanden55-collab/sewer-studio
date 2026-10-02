using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// R1 (Deepscan 02.10.2026): Quellordner, die die sichere Dateisuche der Verteilung
/// ausliess (nicht lesbar oder Verknuepfung), werden als Fehlerergebnis gemeldet.
/// Vorher fehlten ihre Protokolle oder Videos still: die Verteilung zeigte «0 Fehler».
/// Verknuepfungen werden weiterhin nicht betreten.
/// </summary>
internal static class UebersprungeneQuellordner
{
    /// <summary>
    /// Rekursive PDF-Suche wie bisher (ohne eigene <c>split_</c>-Zwischendateien);
    /// sammelt dabei die uebersprungenen Ordner.
    /// </summary>
    internal static List<string> FindePdfs(string quellordner, ICollection<string> uebersprungen)
        => Common.SafeFileEnumeration.EnumerateFilesSafe(quellordner, "*.pdf", recursive: true, uebersprungen)
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Stellt je uebersprungenem Ordner ein Fehlerergebnis voran: die gesammelten Ordner der
    /// Protokollsuche und, falls angegeben, die unter dem rekursiv durchsuchten Videoordner.
    /// </summary>
    internal static IReadOnlyList<HoldingFolderDistributor.DistributionResult> Ergaenze(
        IReadOnlyList<HoldingFolderDistributor.DistributionResult> ergebnisse,
        IEnumerable<string>? uebersprungen,
        string? videoOrdner = null)
    {
        var ordner = (uebersprungen ?? [])
            .Concat(UebersprungeneOrdner.SammleBaum(videoOrdner))
            .Where(pfad => !string.IsNullOrWhiteSpace(pfad))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pfad => pfad, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ordner.Count == 0)
            return ergebnisse;

        return ordner
            .Select(pfad => new HoldingFolderDistributor.DistributionResult(
                false, UebersprungeneOrdner.Meldung(pfad), pfad, null, null, null, null, null,
                HoldingFolderDistributor.VideoMatchStatus.NotChecked))
            .Concat(ergebnisse)
            .ToList();
    }
}
