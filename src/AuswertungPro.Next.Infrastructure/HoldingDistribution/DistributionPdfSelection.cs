using Distributor = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Prueft eine Auswahl von PDFs fuer die Verteilung. Jede ausgewaehlte Datei, die fehlt oder
/// keine PDF ist, bekommt ein eigenes Fehlerergebnis; die uebrigen werden verteilt. Vorher
/// verschwanden solche Dateien still, und die Ergebniszahl stimmte nicht mit der Auswahl ueberein.
/// </summary>
internal static class DistributionPdfSelection
{
    internal sealed record Auswahl(List<string> Gueltig, List<Distributor.DistributionResult> Abgelehnt);

    /// <param name="ohneSplitTeile">Interne Teildateien («split_…») werden still uebergangen.</param>
    internal static Auswahl Pruefe(IEnumerable<string> pdfFiles, bool ohneSplitTeile)
    {
        var gueltig = new List<string>();
        var abgelehnt = new List<Distributor.DistributionResult>();
        foreach (var pfad in pdfFiles
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .Select(p => p.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(pfad))
            {
                abgelehnt.Add(Fehler(pfad, $"Ausgewählte Datei nicht gefunden: {Path.GetFileName(pfad)}"));
                continue;
            }

            if (!string.Equals(Path.GetExtension(pfad), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                abgelehnt.Add(Fehler(pfad, $"Ausgewählte Datei ist keine PDF: {Path.GetFileName(pfad)}"));
                continue;
            }

            if (ohneSplitTeile && Path.GetFileName(pfad).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
                continue;

            gueltig.Add(pfad);
        }

        return new Auswahl(gueltig, abgelehnt);
    }

    /// <summary>Abgelehnte zuerst, dann die Verteilung der gueltigen; ohne jede Datei die bisherige Meldung.</summary>
    internal static IReadOnlyList<Distributor.DistributionResult> Ergebnis(
        Auswahl auswahl,
        List<string> gueltig,
        Func<List<string>, IReadOnlyList<Distributor.DistributionResult>> verteile)
    {
        if (gueltig.Count == 0)
        {
            return auswahl.Abgelehnt.Count > 0
                ? auswahl.Abgelehnt
                : [Fehler("", "No valid PDF files selected.")];
        }

        return auswahl.Abgelehnt.Count == 0
            ? verteile(gueltig)
            : [.. auswahl.Abgelehnt, .. verteile(gueltig)];
    }

    private static Distributor.DistributionResult Fehler(string pfad, string meldung)
        => new(false, meldung, pfad, null, null, null, null, null, Distributor.VideoMatchStatus.NotChecked);
}
