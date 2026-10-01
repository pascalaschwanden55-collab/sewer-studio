using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Cost;

/// <summary>
/// Eine Kostenzeile ist erst dann exportierbar, wenn sie ausgewaehlt ist UND eine
/// positive Menge traegt. Diese Regel stand handgeschrieben an mehreren Stellen
/// (SanierungCostFieldMapper, MeasurePricingEngine) — und an einer Stelle stand
/// eine schwaechere: <c>Measures.Count > 0</c>.
///
/// Der Unterschied ist nicht akademisch. Ein <see cref="MeasureCost"/> darf leere,
/// abgewaehlte oder mengenlose Zeilen enthalten. In der Schacht-Zusammenfuehrung
/// galt so ein Eintrag als "hat Massnahmen", belegte den Schacht und verdraengte
/// die gueltige Empfehlung aus der anderen Quelle — im Leistungsverzeichnis fehlte
/// die Position dann ganz (Gesamtaudit 2026-08-18, F-01).
///
/// Ein Preis von 0 ist ausdruecklich erlaubt: Im NPK-Leistungsverzeichnis bleibt die
/// EP-Spalte leer, wo der Preis variabel ist, und wird vom Anwender ergaenzt.
/// Negative Mengen scheiden ueber <c>&gt; 0</c> ohnehin aus.
/// </summary>
public static class ExportableCostRule
{
    /// <summary>Traegt die Zeile eine exportierbare Position?</summary>
    public static bool IsExportable(CostLine? line)
        => line is { Selected: true, Qty: > 0m };

    /// <summary>Traegt die Massnahme mindestens eine exportierbare Zeile?</summary>
    public static bool HasExportableLine(MeasureCost? measure)
        => measure is not null && measure.Lines.Any(IsExportable);

    /// <summary>Traegt der Kostentraeger mindestens eine exportierbare Zeile?</summary>
    public static bool HasExportableLine(HoldingCost? cost)
        => cost is not null && cost.Measures.Any(HasExportableLine);
}
