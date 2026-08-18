namespace AuswertungPro.Next.Infrastructure.Output.Offers;

/// <summary>
/// Welche Abschnitte die Kostenzusammenstellung enthaelt. Jeder Abschnitt ist einzeln
/// abschaltbar — auch die Vollaufstellung, die bei einem mittleren Projekt allein
/// rund 19 Seiten fuellt.
///
/// Die Totale sind bewusst NICHT schaltbar: Sie sind der Kern des Ausdrucks und
/// duerfen sich durch ein Haekchen nie veraendern.
/// </summary>
public sealed record CostSummaryPdfSections
{
    /// <summary>Kostenzusammenstellung nach Eigentuemer.</summary>
    public bool OwnerSummary { get; init; }

    /// <summary>Kosten je Massnahme (Netto).</summary>
    public bool MeasureSummary { get; init; }

    /// <summary>Zeilenweise Datenuebersicht der gefilterten Bauteile.</summary>
    public bool DataOverview { get; init; }

    /// <summary>Spezialstatistik (Inliner, Manschetten, LEM).</summary>
    public bool SpecialStats { get; init; }

    /// <summary>Statistik "Kosten nach Ausgefuehrt durch".</summary>
    public bool ExecutorStats { get; init; }

    /// <summary>Gesamtzusammenstellung nach Einzelposition (zusammengezaehlt).</summary>
    public bool PositionSummary { get; init; }

    /// <summary>
    /// Detailliste je Bauteil: Kopfzeile mit Total, darunter die Massnahmen mit Menge
    /// und Betrag. Der Mittelweg zwischen einzeiliger Uebersicht und Vollaufstellung.
    /// </summary>
    public bool DetailList { get; init; }

    /// <summary>
    /// Komplette Aufstellung aller Einzelpositionen je Bauteil — der mit Abstand
    /// groesste Abschnitt. Standardmaessig aus.
    /// </summary>
    public bool FullPositionList { get; init; }

    /// <summary>
    /// Standard: die Zusammenstellung, die ihren Namen verdient — Kennzahlen,
    /// Eigentuemer und Massnahmen auf zwei bis drei Seiten.
    /// </summary>
    public static CostSummaryPdfSections Schlank { get; } = new()
    {
        OwnerSummary = true,
        MeasureSummary = true,
        DetailList = true
    };

    /// <summary>Vollstaendige Dokumentation mit allen Abschnitten.</summary>
    public static CostSummaryPdfSections Alles { get; } = new()
    {
        OwnerSummary = true,
        MeasureSummary = true,
        DetailList = true,
        DataOverview = true,
        SpecialStats = true,
        ExecutorStats = true,
        PositionSummary = true,
        FullPositionList = true
    };
}
