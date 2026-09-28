namespace AuswertungPro.Next.Application.UseCases.Verteilung;

/// <summary>
/// Legt nach jeder Verteilung (Haltungen, Schächte, Dichtheit, Protokolle) einen Textbericht
/// neben die Importberichte. Vorher stand das Ergebnis nur im Ergebnisfeld der Seite und war
/// nach dem nächsten Lauf weg.
/// </summary>
public interface IVerteilberichtAblage
{
    /// <param name="projektOrdner">Ordner der Projektdatei.</param>
    /// <param name="art">Kurzname der Verteilung, erscheint im Dateinamen (z. B. «Haltungen»).</param>
    /// <param name="text">Vollständiger Bericht.</param>
    /// <returns>Pfad des Berichts oder <c>null</c>, wenn er nicht geschrieben werden konnte.</returns>
    string? Schreibe(string projektOrdner, string art, string text);
}
