using System;
using System.Collections.Generic;
using System.Globalization;

namespace AuswertungPro.Next.Application.Dashboard;

/// <summary>
/// Schreibgeschützte Projekt-Vorschau für die Projektübersicht (rechtes Panel). Trägt genau die
/// Anzeige-Daten eines Projekts, ohne es zu öffnen. Schadensgruppen sind bewusst NICHT enthalten.
/// </summary>
public sealed record ProjectPreview(
    string Name,
    string Description,
    string Path,
    DateTime? ModifiedAtUtc,
    string? AppVersion,
    int HoldingCount,
    double TotalLengthMeters,
    decimal TotalCost,
    string Auftraggeber,
    string Gemeinde,
    string Zone,
    string Strasse,
    string Bearbeiter,
    string Inspektionsdatum,
    string AuftragNr,
    string Firma,
    IReadOnlyList<DashboardBucket> ConditionClasses,
    IReadOnlyList<DashboardCostBucket> DnCostGroups)
{
    public bool HasHoldings => HoldingCount > 0;

    /// <summary>Lokales Datum (nur Tag) oder „—".</summary>
    public string ModifiedAtDisplay =>
        ModifiedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? "—";
}
