using System;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Cost;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Bildet Kostenkalkulation (HoldingCost) und Massnahmen-Empfehlung
/// (MeasureRecommendationResult) auf die Felder eines HaltungRecord ab.
/// Reine Feld-Logik — Fenster, Lernen, Grid-Refresh und Dirty-Flag bleiben im ViewModel.
/// Aus <c>UI.DataPage.DataPageSanierungCostMapper</c> extrahiert (verhaltensneutral).
/// </summary>
public static class SanierungCostFieldMapper
{
    /// <summary>
    /// Alle Felder, die <see cref="ApplyCosts"/> schreibt (fuer <see cref="ClearCosts"/>).
    /// </summary>
    public static readonly string[] CostFieldNames =
    {
        FieldKeys.Cost,
        FieldKeys.RecommendedRehabilitationMeasures,
        FieldKeys.LinerRenovationMeters,
        FieldKeys.LinerRenovationCount,
        FieldKeys.ConnectionsToGrout,
        FieldKeys.RepairSleeve,
        FieldKeys.LinerEndSleeve,
        FieldKeys.ShortLinerRepair,
    };

    /// <summary>
    /// Die 6 Mengen-Zaehlfelder (abgeleitete Felder OHNE Kosten/Empfohlene). Bei „Sanieren=Ja ohne
    /// Massnahme" werden nur diese geleert, damit ein handgetippter Kosten-Pauschalbetrag bleibt.
    /// </summary>
    public static readonly string[] QuantityFieldNames =
    {
        FieldKeys.LinerRenovationMeters,
        FieldKeys.LinerRenovationCount,
        FieldKeys.ConnectionsToGrout,
        FieldKeys.RepairSleeve,
        FieldKeys.LinerEndSleeve,
        FieldKeys.ShortLinerRepair,
    };

    /// <summary>
    /// Uebertraegt eine Massnahmen-Empfehlung (Lernlogik/KI) auf den Record.
    /// Nur die Feldwerte; das Pflegen der Auswahl-Optionen bleibt im ViewModel.
    /// </summary>
    public static void ApplyRecommendation(HaltungRecord record, MeasureRecommendationResult recommendation)
    {
        var value = string.Join(Environment.NewLine, recommendation.Measures);
        record.SetFieldValue(FieldKeys.RecommendedRehabilitationMeasures, value, FieldSource.Unknown, userEdited: false);

        if (recommendation.EstimatedTotalCost is not null)
            record.SetFieldValue(FieldKeys.Cost, recommendation.EstimatedTotalCost.Value.ToString("0.00", CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
        if (recommendation.RenovierungInlinerM is not null)
            record.SetFieldValue(FieldKeys.LinerRenovationMeters, MeasuresTextBuilder.FormatDecimal(recommendation.RenovierungInlinerM.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.RenovierungInlinerStk is not null)
            record.SetFieldValue(FieldKeys.LinerRenovationCount, MeasuresTextBuilder.FormatInt(recommendation.RenovierungInlinerStk.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.AnschluesseVerpressen is not null)
            record.SetFieldValue(FieldKeys.ConnectionsToGrout, MeasuresTextBuilder.FormatInt(recommendation.AnschluesseVerpressen.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.ReparaturManschette is not null)
            record.SetFieldValue(FieldKeys.RepairSleeve, MeasuresTextBuilder.FormatInt(recommendation.ReparaturManschette.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.ReparaturKurzliner is not null)
            record.SetFieldValue(FieldKeys.ShortLinerRepair, MeasuresTextBuilder.FormatInt(recommendation.ReparaturKurzliner.Value), FieldSource.Unknown, userEdited: false);
    }

    /// <summary>
    /// Uebertraegt eine Kostenkalkulation auf den Record (Kosten, Massnahmen-Text,
    /// abgeleitete Mengen). Das Lernen und der Grid-Refresh bleiben im ViewModel.
    /// </summary>
    public static void ApplyCosts(HaltungRecord record, HoldingCost cost, bool includeCosts = true)
    {
        if (includeCosts)
        {
            // Nettobetrag in Tabellenfeld "Kosten" uebertragen.
            var netTotal = ResolveNetTotal(cost);
            var totalText = netTotal.ToString("0.00", CultureInfo.InvariantCulture);
            record.SetFieldValue(FieldKeys.Cost, totalText, FieldSource.Manual, userEdited: true);
        }

        var massnahmenText = MeasuresTextBuilder.BuildMeasuresText(cost);
        record.SetFieldValue(FieldKeys.RecommendedRehabilitationMeasures, massnahmenText, FieldSource.Manual, userEdited: true);

        var inlinerMeters = SumMeasureLengths(
            cost,
            "NADELFILZ",
            "GFK",
            "SCHLAUCHLINER_NADELFILZ",
            "SCHLAUCHLINER_NADELFILZ_OPENEND",
            "SCHLAUCHLINER_GFK");
        // Domain-Regel: wenn ein Liner gewaehlt ist, zaehlt genau 1 Stueck.
        var inlinerStk = HasSelectedLiner(cost) ? 1 : 0;
        // Anzahl Anschluesse = max ueber alle Anschluss-Arten (jeder Anschluss zaehlt
        // einmal; Auffraesen + Einbinden am selben Anschluss nicht doppelt).
        // Audit W7: pro Massnahme zaehlen und Maximum nehmen — die Anschluss-Zahl wird
        // in JEDES Massnahmen-Buendel injiziert, Summieren wuerde sie mehrfach zaehlen
        // (2 Buendel x 3 Anschluesse ergaben 6 statt 3).
        var anschluesse = Math.Max(
            MaxMeasureQty(cost, "ANSCHLUSS_EINBINDEN", "ANSCHLUSS_DICHTEN", "ANSCHLUSS_VERSCHLIESSEN"),
            MaxMeasureQty(cost, "ANSCHLUSS_AUFFRAESEN"));
        // LEM ist keine Reparatur-Manschette und darf Reparatur_Manschette NICHT fuellen.
        var manschette = SumSelectedQty(cost, "MANSCHETTE_PER_ST", "MANSCHETTE_EDELSTAHL");
        var lem = SumSelectedQty(cost, "LINERENDMANSCHETTE_LEM");
        var kurzliner = SumSelectedQty(cost, "KURZLINER_PER_ST", "QUICKLOCK_PER_ST", "KURZLINER_PARTLINER");

        record.SetFieldValue(FieldKeys.LinerRenovationMeters, MeasuresTextBuilder.FormatDecimal(inlinerMeters), FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.LinerRenovationCount, MeasuresTextBuilder.FormatInt(inlinerStk), FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.ConnectionsToGrout, MeasuresTextBuilder.FormatInt(anschluesse), FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.RepairSleeve, MeasuresTextBuilder.FormatInt(manschette), FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.LinerEndSleeve, MeasuresTextBuilder.FormatInt(lem), FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.ShortLinerRepair, MeasuresTextBuilder.FormatInt(kurzliner), FieldSource.Manual, userEdited: true);
    }

    /// <summary>
    /// Leert alle von <see cref="ApplyCosts"/> gesetzten Kosten-/Massnahmen-/Mengenfelder
    /// (auf leer, NICHT auf 0.00). Fuer Haltungen, die in der Matrix auf "keine Massnahme"
    /// zurueckgesetzt wurden — damit keine alten Werte stehen bleiben.
    /// </summary>
    public static void ClearCosts(HaltungRecord record)
    {
        if (record is null)
            return;

        foreach (var field in CostFieldNames)
            record.SetFieldValue(field, "", FieldSource.Manual, userEdited: true);
    }

    /// <summary>
    /// Zieht die abgeleiteten Kostenfelder eines Records nach der Sanieren-Regel nach:
    /// <list type="bullet">
    /// <item>Sanieren=Ja + Massnahmen → alle 8 Felder aus <paramref name="cost"/> berechnen.</item>
    /// <item>Sanieren=Ja ohne Massnahme → die 6 Mengenfelder leeren, Kosten/Empfohlene behalten
    /// (Schutz fuer handgetippten Pauschalbetrag).</item>
    /// <item>Sanieren=Nein/leer → alle 8 Felder leeren (Haltung wird nicht saniert).</item>
    /// </list>
    /// Schreibt nur geaenderte Felder (userEdited:true, wie <see cref="ApplyCosts"/>) — kein UI/Lernen.
    /// Rueckgabe: true, wenn sich mindestens ein Feld geaendert hat.
    /// </summary>
    public static bool SyncRecord(HaltungRecord record, HoldingCost? cost)
    {
        if (record is null)
            return false;

        var toRenovate = string.Equals(
            (record.GetFieldValue(FieldKeys.RenovationDecision) ?? "").Trim(), "Ja",
            StringComparison.OrdinalIgnoreCase);

        var target = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

        if (!toRenovate)
        {
            foreach (var f in CostFieldNames)
                target[f] = "";
        }
        else
        {
            // Dieselbe Regel wie ueberall sonst - jetzt an einer Stelle. Sie stand
            // hier und in MeasurePricingEngine handgeschrieben, waehrend
            // SchachtLvCostLoader eine schwaechere benutzte (Gesamtaudit
            // 2026-08-18, F-01). Verhalten unveraendert.
            var hasMeasures = ExportableCostRule.HasExportableLine(cost);
            if (!hasMeasures)
            {
                foreach (var f in QuantityFieldNames)
                    target[f] = "";
                // Kosten + Empfohlene_Sanierungsmassnahmen bleiben unangetastet (Pauschal-Schutz).
            }
            else
            {
                var inlinerMeters = SumMeasureLengths(cost!,
                    "NADELFILZ", "GFK", "SCHLAUCHLINER_NADELFILZ",
                    "SCHLAUCHLINER_NADELFILZ_OPENEND", "SCHLAUCHLINER_GFK");
                var inlinerStk = HasSelectedLiner(cost!) ? 1 : 0;
                var anschluesse = Math.Max(
                    MaxMeasureQty(cost!, "ANSCHLUSS_EINBINDEN", "ANSCHLUSS_DICHTEN", "ANSCHLUSS_VERSCHLIESSEN"),
                    MaxMeasureQty(cost!, "ANSCHLUSS_AUFFRAESEN"));
                var manschette = SumSelectedQty(cost!, "MANSCHETTE_PER_ST", "MANSCHETTE_EDELSTAHL");
                var lem = SumSelectedQty(cost!, "LINERENDMANSCHETTE_LEM");
                var kurzliner = SumSelectedQty(cost!, "KURZLINER_PER_ST", "QUICKLOCK_PER_ST", "KURZLINER_PARTLINER");

                target[FieldKeys.LinerRenovationMeters] = MeasuresTextBuilder.FormatDecimal(inlinerMeters);
                target[FieldKeys.LinerRenovationCount] = MeasuresTextBuilder.FormatInt(inlinerStk);
                target[FieldKeys.ConnectionsToGrout] = MeasuresTextBuilder.FormatInt(anschluesse);
                target[FieldKeys.RepairSleeve] = MeasuresTextBuilder.FormatInt(manschette);
                target[FieldKeys.LinerEndSleeve] = MeasuresTextBuilder.FormatInt(lem);
                target[FieldKeys.ShortLinerRepair] = MeasuresTextBuilder.FormatInt(kurzliner);
                target[FieldKeys.Cost] = ResolveNetTotal(cost!).ToString("0.00", CultureInfo.InvariantCulture);
                target[FieldKeys.RecommendedRehabilitationMeasures] = MeasuresTextBuilder.BuildMeasuresText(cost!);
            }
        }

        var changed = false;
        foreach (var kv in target)
        {
            if (!string.Equals(record.GetFieldValue(kv.Key), kv.Value, StringComparison.Ordinal))
            {
                record.SetFieldValue(kv.Key, kv.Value, FieldSource.Manual, userEdited: true);
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// Ermittelt den Nettobetrag aus Total oder — falls nur Brutto bekannt — durch
    /// Rueckrechnung ueber den MWST-Satz.
    /// </summary>
    public static decimal ResolveNetTotal(HoldingCost cost)
    {
        if (cost.Total > 0m)
            return cost.Total;

        if (cost.TotalInclMwst > 0m && cost.MwstRate > 0m)
            return Math.Round(cost.TotalInclMwst / (1m + cost.MwstRate), 2, MidpointRounding.AwayFromZero);

        return cost.TotalInclMwst;
    }

    /// <summary>
    /// Summiert Laengenangaben der Liner-Massnahmen aus den Zeilen (oder LengthMeters-Property).
    /// </summary>
    public static decimal SumMeasureLengths(HoldingCost cost, params string[] measureIds)
    {
        var sum = 0m;
        foreach (var measure in cost.Measures)
        {
            if (!measureIds.Any(id => MeasureClassification.MatchesIdentifier(measure.MeasureId, id)))
                continue;
            if (measure.LengthMeters is not null)
            {
                sum += measure.LengthMeters.Value;
                continue;
            }

            var fallback = measure.Lines
                .Where(l => l.Selected && string.Equals(l.Unit, "m", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Qty)
                .DefaultIfEmpty(0m)
                .Max();
            sum += fallback;
        }
        return sum;
    }

    /// <summary>
    /// Prueft, ob in den Massnahmen mindestens eine Selected Liner-Zeile vorkommt.
    /// </summary>
    public static bool HasSelectedLiner(HoldingCost cost)
    {
        foreach (var measure in cost.Measures)
        {
            var selectedLines = measure.Lines.Where(l => l.Selected).ToList();
            if (selectedLines.Count == 0)
                continue;

            if (selectedLines.Any(MeasureClassification.IsLinerLine))
                return true;

            // Fallback fuer Legacy-Payloads, bei denen nur die Massnahmen-ID verlaesslich ist.
            if (MeasureClassification.IsLinerIdentifier(measure.MeasureId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Summiert die Menge aller Selected Zeilen mit einem der angegebenen ItemKeys.
    /// </summary>
    public static int SumSelectedQty(HoldingCost cost, params string[] itemKeys)
    {
        var total = 0m;
        foreach (var measure in cost.Measures)
        {
            foreach (var line in measure.Lines)
            {
                if (!line.Selected)
                    continue;
                if (!itemKeys.Any(key => string.Equals(line.ItemKey, key, StringComparison.OrdinalIgnoreCase)))
                    continue;
                total += line.Qty;
            }
        }
        return (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Teilsumme pro Massnahme, dann Maximum ueber die Massnahmen — fuer Mengen, die
    /// (wie die Anschluss-Zahl) in jedes Buendel injiziert werden und darum nicht
    /// ueber Massnahmen summiert werden duerfen (Audit W7).
    /// </summary>
    public static int MaxMeasureQty(HoldingCost cost, params string[] itemKeys)
    {
        var max = 0m;
        foreach (var measure in cost.Measures)
        {
            var sub = 0m;
            foreach (var line in measure.Lines)
            {
                if (!line.Selected)
                    continue;
                if (!itemKeys.Any(key => string.Equals(line.ItemKey, key, StringComparison.OrdinalIgnoreCase)))
                    continue;
                sub += line.Qty;
            }
            if (sub > max)
                max = sub;
        }
        return (int)Math.Round(max, 0, MidpointRounding.AwayFromZero);
    }
}
