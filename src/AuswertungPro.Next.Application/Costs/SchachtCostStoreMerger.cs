using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Costs;

/// <summary>
/// Fuehrt die zwei gepflegten Schacht-Kostenquellen zusammen:
/// die Schacht-Matrix (<c>schacht_costs.json</c>) und den Massnahmen-Dialog
/// (<c>schacht_empfehlungen.json</c>).
///
/// Die Matrix ist die genauere Quelle und hat Vorrang; die Empfehlung greift nur
/// dort, wo die Matrix den Schacht nicht kennt. Dieselbe Regel wie im Druckcenter
/// (<c>BuilderPageSchachtRowBuilder</c>) — ein Projekt, das nur den Dialog nutzt,
/// darf nirgends mit 0 CHF dastehen.
/// </summary>
public static class SchachtCostStoreMerger
{
    public static ProjectCostStore Merge(ProjectCostStore? matrix, ProjectCostStore? empfehlungen)
    {
        var zusammen = new ProjectCostStore
        {
            ByHolding = new Dictionary<string, HoldingCost>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var (nummer, cost) in matrix?.ByHolding ?? [])
            zusammen.ByHolding[nummer] = cost;

        foreach (var (nummer, cost) in empfehlungen?.ByHolding ?? [])
        {
            // Die Matrix darf nicht verdoppelt werden.
            if (!zusammen.ByHolding.ContainsKey(nummer))
                zusammen.ByHolding[nummer] = cost;
        }

        return zusammen;
    }
}
