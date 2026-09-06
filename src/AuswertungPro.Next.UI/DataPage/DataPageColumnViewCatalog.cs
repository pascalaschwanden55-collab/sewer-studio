using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Eine gespeicherte Spaltenansicht der Haltungsliste. Felder == null bedeutet alle Spalten.</summary>
public sealed record DataPageColumnView(string Key, string Titel, IReadOnlyList<string>? Felder);

/// <summary>
/// Feste Spaltensaetze aus dem Nova-Prototyp. Reine Daten, keine WPF-Abhaengigkeit: Der
/// Controller blendet Spalten nur ein oder aus; Werte und Export bleiben unberuehrt.
/// </summary>
public static class DataPageColumnViewCatalog
{
    public static IReadOnlyList<DataPageColumnView> Views { get; } =
    [
        new("kompakt", "Kompakt", [FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm, FieldKeys.HoldingLengthMeters, FieldKeys.ConditionClass, FieldKeys.Link]),
        new("stammdaten", "Stammdaten", [FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm, FieldKeys.ClearWidthMm, FieldKeys.ProfileType, FieldKeys.UsageType, FieldKeys.HoldingLengthMeters, FieldKeys.InspectionYear, FieldKeys.ConstructionYear, FieldKeys.Owner, FieldKeys.GeonisId, FieldKeys.CadastreObjectId]),
        new("bewertung", "Bewertung", [FieldKeys.HoldingName, FieldKeys.ConditionClass, "VSA_Zustandsnote_D", "VSA_Zustandsnote_S", "VSA_Zustandsnote_B", "VSA_Geschaetzt", "Pruefungsresultat", "Referenzpruefung", "Gewaesserschutz", "Grundwasserspiegel"]),
        new("sanierung", "Sanierung", [FieldKeys.HoldingName, FieldKeys.RenovationDecision, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.LinerRenovationMeters, FieldKeys.ConnectionsToGrout, FieldKeys.RepairSleeve, FieldKeys.LinerEndSleeve, FieldKeys.ShortLinerRepair, "Erneuerung_Neubau_m", FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus]),
        new("kosten", "Kosten", [FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.ConditionClass, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.Cost, FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus, FieldKeys.Owner]),
        new("alle", "Alle Spalten", null)
    ];

    public static DataPageColumnView Resolve(string? key)
        => Views.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Views[^1];
}
