using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Suche;

public enum GlobaleSucheArt { Haltung, Schacht, Strasse }

public sealed record GlobaleSucheTreffer(GlobaleSucheArt Art, string Text, object? Ziel);

/// <summary>Inventar 8.5: Treffer Haltung, Schacht, Strasse; hoechstens 12.</summary>
public static class GlobaleSucheRegel
{
    public static IReadOnlyList<GlobaleSucheTreffer> Suche(
        string? text,
        IEnumerable<HaltungRecord> haltungen,
        IEnumerable<SchachtRecord> schaechte,
        Func<SchachtRecord, string> schachtNummer,
        int max = 12)
    {
        var q = (text ?? string.Empty).Trim();
        if (q.Length == 0)
            return Array.Empty<GlobaleSucheTreffer>();
        bool Passt(string? s) => !string.IsNullOrEmpty(s) && s.Contains(q, StringComparison.OrdinalIgnoreCase);

        var ergebnis = new List<GlobaleSucheTreffer>();
        var strassen = new List<string>();
        foreach (var h in haltungen)
        {
            var name = h.GetFieldValue(FieldKeys.HoldingName);
            var strasse = h.GetFieldValue(FieldKeys.Street);
            if (Passt(name) || Passt(strasse))
                ergebnis.Add(new(GlobaleSucheArt.Haltung, string.IsNullOrWhiteSpace(strasse) ? $"Haltung {name}" : $"Haltung {name} · {strasse}", h));
            if (Passt(strasse) && !strassen.Contains(strasse!, StringComparer.OrdinalIgnoreCase))
                strassen.Add(strasse!);
        }
        foreach (var s in schaechte)
        {
            var nummer = schachtNummer(s);
            var strasse = s.GetFieldValue(FieldKeys.Street);
            if (Passt(nummer) || Passt(strasse))
                ergebnis.Add(new(GlobaleSucheArt.Schacht, string.IsNullOrWhiteSpace(strasse) ? $"Schacht {nummer}" : $"Schacht {nummer} · {strasse}", s));
            if (Passt(strasse) && !strassen.Contains(strasse!, StringComparer.OrdinalIgnoreCase))
                strassen.Add(strasse!);
        }
        ergebnis.AddRange(strassen.Select(st => new GlobaleSucheTreffer(GlobaleSucheArt.Strasse, $"Strasse {st}", st)));
        return ergebnis.Take(max).ToList();
    }
}
