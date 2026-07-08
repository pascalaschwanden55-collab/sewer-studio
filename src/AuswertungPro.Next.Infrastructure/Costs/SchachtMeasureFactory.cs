using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Baut die Kosten fuer EINEN Schacht ueber dieselbe generische Mechanik wie Haltungen
/// (<see cref="HoldingMeasureFactory"/>). Schaechte haben keine DN/Laenge/Anschluss-Defaults,
/// darum wird mit <c>record: null</c> gebaut — es greifen keine Import-Automatiken; die Menge
/// der Hauptarbeit kommt manuell aus der Schacht-Matrix (Default 1 Stueck/Stunde).
/// Bewusst duenn: keine Kopie der Build-Logik, nur der record-freie Aufruf.
/// </summary>
public static class SchachtMeasureFactory
{
    public static HoldingCost? Build(
        string schachtnummer,
        string measureId,
        IReadOnlyDictionary<string, MeasureTemplate> templates,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        decimal vatRate,
        IReadOnlyCollection<string>? extraOptionKeys = null,
        decimal hauptarbeitMenge = 1m,
        string? hauptarbeitItemKey = null)
        => HoldingMeasureFactory.Build(
            schachtnummer,
            record: null,
            measureId,
            templates,
            catalog,
            vatRate,
            extraOptionKeys,
            hauptarbeitMenge,
            hauptarbeitItemKey);
}
