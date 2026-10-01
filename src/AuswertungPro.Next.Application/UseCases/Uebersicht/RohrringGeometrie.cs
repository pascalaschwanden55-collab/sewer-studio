using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.UseCases.Uebersicht;

public sealed record RohrringBogen(double StartGrad, double SweepGrad, int Stufe, string Tooltip);

/// <summary>
/// Rohrquerschnitt mit Uhrlagen (Inventar 5.1). Anders als der Prototyp folgt die Lage der
/// erfassten Uhrlage (Uhr_von/Uhr_bis), die Indexregel ist nur der Rueckfall ohne Uhrlage.
/// 0 Grad = 12 Uhr, im Uhrzeigersinn.
///
/// Nova-Fixwelle F1: Gezeichnet werden nur Schaeden (BA*/BB* nach
/// <see cref="SchadensgruppenRegel"/>) — Bestandsaufnahme wie Rohranfang (BCD), Rohrende (BCE),
/// Anschluss (BCA) oder Bogen (BCC) ist kein Schaden. Von den verbleibenden Befunden zeigt der
/// Ring die schwersten zuerst; bei gleicher Stufe entscheidet der kleinere Meterwert.
/// </summary>
public static class RohrringGeometrie
{
    private static readonly string[] VonAliase = ["Uhr_von", "vsa.uhr.von", "ClockPos1", "SchadenlageAnfang"];
    private static readonly string[] BisAliase = ["Uhr_bis", "vsa.uhr.bis", "ClockPos2", "SchadenlageEnde"];

    /// <summary>
    /// Die Schaeden der Haltung in der Reihenfolge, in der Ring und Liste sie zeigen:
    /// Stufe absteigend, bei Gleichstand der kleinere Meterwert zuerst. Keine Begrenzung —
    /// die Liste zeigt alle, der Ring nimmt sich davon die ersten drei.
    /// </summary>
    public static IReadOnlyList<ProtocolEntry> Schaeden(IEnumerable<ProtocolEntry>? entries)
        => (entries ?? Array.Empty<ProtocolEntry>())
            .Where(e => e is not null && !e.IsDeleted && SchadensgruppenRegel.IstSchaden(e.Code))
            .OrderByDescending(StufeVon)
            .ThenBy(e => e.MeterStart ?? double.MaxValue)
            .ToList();

    public static IReadOnlyList<RohrringBogen> Boegen(IReadOnlyList<ProtocolEntry> entries, int max = 3)
    {
        var liste = new List<RohrringBogen>();
        var i = 0;
        foreach (var e in Schaeden(entries).Take(max))
        {
            var von = Stunde(e, VonAliase);
            var bis = Stunde(e, BisAliase);
            double start, sweep;
            if (von is { } v)
            {
                start = (v % 12) * 30.0;
                sweep = bis is { } b && b != v ? (((b - v) + 12) % 12) * 30.0 : 30.0;
            }
            else
            {
                start = i * 70.0;
                sweep = 30.0;
            }
            liste.Add(new RohrringBogen(start, Math.Max(30.0, sweep), StufeVon(e), $"{e.Code} {e.Beschreibung}".Trim()));
            i++;
        }
        return liste;
    }

    public static int StufeVon(ProtocolEntry e)
        => int.TryParse(e.CodeMeta?.Severity, out var s) && s is >= 1 and <= 5 ? s : 1;

    private static int? Stunde(ProtocolEntry e, string[] aliase)
    {
        if (e.CodeMeta?.Parameters is not { } p) return null;
        foreach (var key in aliase)
            if (p.TryGetValue(key, out var raw) && raw is { Length: > 0 })
            {
                var ziffern = new string(raw.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(ziffern, out var h) && h is >= 0 and <= 12) return h == 0 ? 12 : h;
            }
        return null;
    }
}
