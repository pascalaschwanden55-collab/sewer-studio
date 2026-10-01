using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Baut aus einem geoeffneten Projekt und seinem Kostenspeicher die Lernfaelle auf.
///
/// Uebersprungene Haltungen werden mit Grund gemeldet, nicht still verschluckt: Wer
/// spaeter wissen will, warum nur 58 von 96 Haltungen zaehlen, findet die Antwort hier.
///
/// Alle so gewonnenen Faelle gelten als unbeeinflusst — sie entstanden, bevor es
/// ueberhaupt einen Vorschlag gab.
/// </summary>
public static class KostenfallAufbauLauf
{
    public static (IReadOnlyList<Kostenfall> Faelle, IReadOnlyList<string> Uebersprungen) Baue(
        Project projekt,
        ProjectCostStore kosten,
        string projektName,
        DateTime jetztUtc)
    {
        ArgumentNullException.ThrowIfNull(projekt);
        ArgumentNullException.ThrowIfNull(kosten);

        var faelle = new List<Kostenfall>();
        var uebersprungen = new List<string>();

        foreach (var record in projekt.Data)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            var anzeige = name.Length == 0 ? "(ohne Namen)" : name;

            if (name.Length == 0 || !kosten.ByHolding.TryGetValue(name, out var cost))
            {
                uebersprungen.Add($"{anzeige}: keine Kostenzusammenstellung.");
                continue;
            }

            if (KostenfallExtraktor.TryErstellen(
                    record, cost, projektName, KostenfallHerkunft.Unbeeinflusst, jetztUtc,
                    out var fall, out var grund))
            {
                faelle.Add(fall);
            }
            else
            {
                uebersprungen.Add($"{anzeige}: {grund}");
            }
        }

        return (faelle, uebersprungen);
    }
}
