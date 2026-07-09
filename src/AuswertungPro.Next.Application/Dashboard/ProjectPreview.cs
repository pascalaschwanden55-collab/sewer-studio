using System;
using System.Collections.Generic;
using System.Globalization;

namespace AuswertungPro.Next.Application.Dashboard;

/// <summary>
/// Schreibgeschuetzte Projekt-Vorschau fuer die Projektuebersicht.
/// </summary>
public sealed record ProjectPreview(
    string Name,
    string Description,
    string Path,
    DateTime? ModifiedAtUtc,
    int HoldingCount,
    int SchachtCount,
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
    DashboardStatistics Statistics)
{
    public IReadOnlyList<ZustandBucket> HoldingConditionClasses => Statistics.Haltungen.Buckets;
    public IReadOnlyList<ZustandBucket> SchachtConditionClasses => Statistics.Schaechte.Buckets;

    // Kompatibilitaet fuer bestehende XAML-Bindings bis zum Dashboard-XAML-Umbau.
    public IReadOnlyList<ZustandBucket> ConditionClasses => HoldingConditionClasses;
    public IReadOnlyList<DashboardCostBucket> DnCostGroups => Statistics.HaltungDnCosts;

    public string ModifiedAtDisplay =>
        ModifiedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? "-";
}
