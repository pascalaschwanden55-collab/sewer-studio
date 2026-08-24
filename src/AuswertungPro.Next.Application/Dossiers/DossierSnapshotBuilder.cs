using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>Eine Haltung des Dossiers in Anzeigeform.</summary>
public sealed record DossierHoldingLine(
    Guid HoldingId,
    string HoldingName,
    string Street,
    double? LengthMeters,
    string ConditionClass,
    decimal NetCost,
    string Measures);

/// <summary>Ein Schacht der Liegenschaft.</summary>
public sealed record DossierShaftLine(
    Guid ShaftId,
    string Number,
    string Street,
    string ConditionClass,
    decimal NetCost);

/// <summary>
/// Der berechnete Stand eines Dossiers: seine Haltungen, die Kennzahlen und
/// die Verweise, zu denen keine Haltung mehr existiert.
/// </summary>
public sealed record DossierSnapshot(
    Guid DossierId,
    string Name,
    IReadOnlyList<DossierHoldingLine> Holdings,
    IReadOnlyList<Guid> MissingHoldingIds,
    DashboardStatistics Statistics,
    IReadOnlyList<DossierShaftLine>? ShaftLines = null)
{
    /// <summary>Die Schaechte der Liegenschaft.</summary>
    public IReadOnlyList<DossierShaftLine> Shafts
        => ShaftLines ?? Array.Empty<DossierShaftLine>();

    public int ShaftCount => Shafts.Count;
    public int HoldingCount => Holdings.Count;

    public bool HasMissingHoldings => MissingHoldingIds.Count > 0;

    /// <summary>Summe der Nettokosten der enthaltenen Haltungen.</summary>
    public decimal NetCostTotal => Holdings.Sum(h => h.NetCost);

    /// <summary>Gesamtlaenge der enthaltenen Haltungen in Metern.</summary>
    public double LengthTotal => Math.Round(Holdings.Sum(h => h.LengthMeters ?? 0d), 2);
}

/// <summary>
/// Rechnet den Stand eines Dossiers aus dem Projekt aus. Pure Logik ohne
/// Dateisystem. Die Kennzahlen kommen bewusst aus
/// <see cref="DashboardStatisticsBuilder"/>, damit das Dossier-Cockpit
/// dieselben Zahlen zeigt wie die Projektuebersicht und keine zweite
/// Rechenlogik entsteht.
/// </summary>
public static class DossierSnapshotBuilder
{
    public static DossierSnapshot Build(
        DossierDefinition? dossier,
        Project? project,
        ProjectCostStore? haltungCosts,
        ProjectCostStore? schachtCosts = null)
    {
        var definition = dossier ?? new DossierDefinition();
        var allHoldings = project?.Data?.ToList() ?? new List<HaltungRecord>();
        var byId = new Dictionary<Guid, HaltungRecord>();
        foreach (var record in allHoldings)
            byId[record.Id] = record;

        var costMap = haltungCosts?.ByHolding
            ?? new Dictionary<string, HoldingCost>(StringComparer.OrdinalIgnoreCase);

        var selected = new List<HaltungRecord>();
        var missing = new List<Guid>();

        // Reihenfolge der gespeicherten Auswahl beibehalten: sie ist die
        // Reihenfolge, in der Pascal die Haltungen zugeordnet hat.
        foreach (var id in definition.HoldingIds)
        {
            if (byId.TryGetValue(id, out var record))
                selected.Add(record);
            else
                missing.Add(id);
        }

        var lines = selected
            .Select(record => BuildLine(record, costMap))
            .ToList();

        // Nur die Kosten der ausgewaehlten Haltungen weiterreichen, sonst
        // wuerde das Dossier-Cockpit die Kosten des ganzen Gebiets zeigen.
        var scopedCosts = BuildScopedCostStore(selected, costMap);

        var scopedProject = new Project();
        foreach (var record in selected)
            scopedProject.Data.Add(record);

        var statistics = DashboardStatisticsBuilder.Build(
            scopedProject,
            scopedCosts,
            schachtCosts is null ? new ProjectCostStore() : FilterEmpty(schachtCosts));

        return new DossierSnapshot(
            definition.Id,
            definition.Name,
            lines,
            missing,
            statistics,
            BuildShaftLines(definition, project, schachtCosts));
    }

    /// <summary>
    /// Die Schaechte in der Reihenfolge, in der sie im Dossier stehen. Eine
    /// Nummer ohne Datensatz im Projekt wird uebersprungen — ein erfundener
    /// Schacht im Brief waere schlimmer als eine kurze Liste.
    /// </summary>
    private static IReadOnlyList<DossierShaftLine> BuildShaftLines(
        DossierDefinition definition,
        Project? project,
        ProjectCostStore? schachtCosts)
    {
        if (definition.ShaftNumbers is null || definition.ShaftNumbers.Count == 0)
            return Array.Empty<DossierShaftLine>();

        var nachNummer = new Dictionary<string, SchachtRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in project?.SchaechteData ?? Enumerable.Empty<SchachtRecord>())
        {
            var nummer = (record.GetFieldValue("Schachtnummer") ?? string.Empty).Trim();
            if (nummer.Length > 0 && !nachNummer.ContainsKey(nummer))
                nachNummer[nummer] = record;
        }

        var kosten = schachtCosts?.ByHolding
            ?? new Dictionary<string, HoldingCost>(StringComparer.OrdinalIgnoreCase);

        var zeilen = new List<DossierShaftLine>();

        foreach (var nummer in definition.ShaftNumbers)
        {
            var sauber = (nummer ?? string.Empty).Trim();
            if (sauber.Length == 0 || !nachNummer.TryGetValue(sauber, out var record))
                continue;

            zeilen.Add(new DossierShaftLine(
                record.Id,
                (record.GetFieldValue("Schachtnummer") ?? sauber).Trim(),
                (record.GetFieldValue(FieldKeys.Street) ?? string.Empty).Trim(),
                DashboardStatisticsBuilder.NormalizeZustandsklasse(
                    record.GetFieldValue(FieldKeys.ConditionClass)),
                ResolveNetTotal(TryGetCost(kosten, sauber))));
        }

        return zeilen;
    }

    private static DossierHoldingLine BuildLine(
        HaltungRecord record,
        IReadOnlyDictionary<string, HoldingCost> costMap)
    {
        var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? string.Empty).Trim();
        var cost = TryGetCost(costMap, name);

        return new DossierHoldingLine(
            record.Id,
            name,
            (record.GetFieldValue(FieldKeys.Street) ?? string.Empty).Trim(),
            ParseDouble(record.GetFieldValue(FieldKeys.HoldingLengthMeters)),
            DashboardStatisticsBuilder.NormalizeZustandsklasse(
                record.GetFieldValue(FieldKeys.ConditionClass)),
            ResolveNetTotal(cost),
            (record.GetFieldValue(FieldKeys.RecommendedRehabilitationMeasures) ?? string.Empty).Trim());
    }

    private static ProjectCostStore BuildScopedCostStore(
        IReadOnlyList<HaltungRecord> selected,
        IReadOnlyDictionary<string, HoldingCost> costMap)
    {
        var scoped = new ProjectCostStore();
        foreach (var record in selected)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? string.Empty).Trim();
            if (name.Length == 0)
                continue;

            var cost = TryGetCost(costMap, name);
            if (cost is not null)
                scoped.ByHolding[name] = cost;
        }

        return scoped;
    }

    /// <summary>
    /// Schachtkosten gehoeren derzeit nicht zum Eigentuemerdossier. Sie werden
    /// bewusst leer weitergereicht statt die Kosten des ganzen Gebiets zu zeigen.
    /// </summary>
    private static ProjectCostStore FilterEmpty(ProjectCostStore _) => new();

    private static HoldingCost? TryGetCost(
        IReadOnlyDictionary<string, HoldingCost> costMap,
        string holdingName)
    {
        if (holdingName.Length == 0)
            return null;

        return costMap.TryGetValue(holdingName, out var cost) ? cost : null;
    }

    private static decimal ResolveNetTotal(HoldingCost? cost)
    {
        if (cost is null)
            return 0m;

        return TablePauschaleCostHelper.ResolveNetTotal(cost);
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Replace("'", "").Replace(" ", "").Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number)
            ? number
            : null;
    }
}
