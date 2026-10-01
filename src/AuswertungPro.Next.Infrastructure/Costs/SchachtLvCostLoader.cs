using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Cost;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Laedt die erfassten Schachtkosten fuer das projektweite Leistungsverzeichnis aus BEIDEN
/// gepflegten Quellen und fuehrt sie zusammen:
/// <list type="bullet">
/// <item>schacht_costs.json — Schacht-Matrix, mit Katalogpositionen (NPK-Kapitel 700).</item>
/// <item>schacht_empfehlungen.json — Massnahmen-Dialog der Schaechte-Seite, freie Texte
/// ohne Katalog-ItemKey.</item>
/// </list>
/// Steht ein Schacht in beiden Dateien, gewinnt die Matrix: zusammengezaehlt wird NIE,
/// sonst stuende derselbe Schacht doppelt und zu teuer im Leistungsverzeichnis.
/// Die Schacht-<see cref="HoldingCost"/>s fliessen unveraendert in denselben
/// <c>ProjectPositionAggregator</c> wie die Haltungen.
/// </summary>
public static class SchachtLvCostLoader
{
    internal const string MatrixFileName = "schacht_costs.json";
    internal const string EmpfehlungFileName = "schacht_empfehlungen.json";

    /// <summary>
    /// Einheit fuer Massnahmen aus dem Dialog. Dort wird nur "was" und "wie teuer" erfasst,
    /// keine Einheit — im LV erscheinen sie darum als Stueckzahl mit Gesamtpreis.
    /// </summary>
    private const string StueckEinheit = "Stk";

    /// <summary>
    /// Liefert die Schacht-Kosten als HoldingCost-Liste. <paramref name="loadError"/> != null
    /// bedeutet: mindestens eine der beiden Dateien existiert, war aber nicht lesbar
    /// (beschaedigt/gesperrt) — der Aufrufer soll dann warnen, aber das Haltungs-LV NICHT
    /// blockieren (die betroffenen Schaechte fehlen dann).
    /// </summary>
    public static IReadOnlyList<HoldingCost> LoadForLv(string? projectPath, out string? loadError)
    {
        var matrix = new ProjectCostStoreRepository(MatrixFileName)
            .Load(projectPath, out var matrixError);
        var empfehlungen = new ProjectCostStoreRepository(EmpfehlungFileName)
            .Load(projectPath, out var empfehlungError);

        loadError = CombineErrors(matrixError, empfehlungError);
        return Merge(matrix, empfehlungen);
    }

    /// <summary>
    /// Reine Zusammenfuehrung beider Quellen — ohne Dateizugriff testbar.
    /// Leere Eintraege (ohne Massnahmen) fallen in beiden Quellen weg.
    /// </summary>
    internal static IReadOnlyList<HoldingCost> Merge(
        ProjectCostStore matrix,
        ProjectCostStore empfehlungen)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(empfehlungen);

        var result = matrix.ByHolding.Values
            .Where(HatMassnahmen)
            .ToList();

        var belegt = new HashSet<string>(
            result.Select(cost => (cost.Holding ?? "").Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var cost in empfehlungen.ByHolding.Values.Where(HatMassnahmen))
        {
            // Die Matrix ist die genauere Quelle und darf nicht verdoppelt werden.
            if (!belegt.Add((cost.Holding ?? "").Trim()))
                continue;

            result.Add(AlsStueckposition(cost));
        }

        return result;
    }

    /// <summary>
    /// "Hat Massnahmen" heisst: mindestens eine ausgewaehlte Zeile mit positiver
    /// Menge — dieselbe Regel wie im uebrigen Kostencode.
    ///
    /// Vorher stand hier <c>Measures.Count > 0</c>. Ein MeasureCost darf aber
    /// leere, abgewaehlte oder mengenlose Zeilen enthalten; ein solcher
    /// Matrixeintrag belegte den Schacht und verdraengte die gueltige Empfehlung
    /// aus der anderen Quelle, obwohl aus der Matrix nichts exportiert wurde. Im
    /// Leistungsverzeichnis fehlte die Position dann ganz
    /// (Gesamtaudit 2026-08-18, F-01).
    /// </summary>
    private static bool HatMassnahmen(HoldingCost? cost)
        => ExportableCostRule.HasExportableLine(cost);

    /// <summary>
    /// Setzt bei Massnahmen ohne Einheit "Stk", damit sie im LV als Stueckzahl mit
    /// Gesamtpreis erscheinen. Mengen, Preise und Texte bleiben unveraendert; eine
    /// bereits erfasste Einheit wird nie ueberschrieben. Arbeitet auf einer Kopie,
    /// damit die geladene Datei unveraendert bleibt.
    /// </summary>
    private static HoldingCost AlsStueckposition(HoldingCost cost)
        => cost with
        {
            Measures = cost.Measures
                .Select(measure => measure with
                {
                    Lines = measure.Lines
                        .Select(line => string.IsNullOrWhiteSpace(line.Unit)
                            ? line with { Unit = StueckEinheit }
                            : line)
                        .ToList()
                })
                .ToList()
        };

    private static string? CombineErrors(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
            return string.IsNullOrWhiteSpace(second) ? null : second;

        return string.IsNullOrWhiteSpace(second) ? first : $"{first}\n{second}";
    }
}
