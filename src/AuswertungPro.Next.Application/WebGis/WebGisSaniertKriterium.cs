using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Entscheidet, ob ein Objekt als saniert gilt — ausschliesslich anhand der
/// Sanierungs-Objektakten (Art "sanierung", Status "Ausgefuehrt"), NICHT anhand
/// des Bemerkungstexts. Grund: Der Bemerkungstext ist nicht verlaesslich; die Akte
/// ist die fachliche Wahrheit und traegt Verfahren, Jahr und Umfang.
///
/// Reine Werte-Logik ueber die Objektakten des Projekts.
/// </summary>
public static class WebGisSaniertKriterium
{
    public const string ArtSanierung = "sanierung";
    public const string StatusFeld = "sanierung.s_status";
    public const string StatusAusgefuehrt = "Ausgeführt";

    /// <summary>
    /// Alle Sanierungs-Akten mit Status "Ausgefuehrt", die den Datensatz
    /// <paramref name="recordId"/> referenzieren.
    /// </summary>
    public static IReadOnlyList<ObjektAkte> AusgefuehrteAkten(
        IEnumerable<ObjektAkte> objektakten, Guid recordId)
    {
        ArgumentNullException.ThrowIfNull(objektakten);

        return objektakten
            .Where(o => string.Equals(o.Art, ArtSanierung, StringComparison.Ordinal))
            .Where(o => o.Bezuege.Contains(recordId))
            .Where(IstAusgefuehrt)
            .ToList();
    }

    /// <summary>true, wenn mindestens eine ausgefuehrte Sanierungs-Akte existiert.</summary>
    public static bool IstSaniert(IEnumerable<ObjektAkte> objektakten, Guid recordId)
        => AusgefuehrteAkten(objektakten, recordId).Count > 0;

    private static bool IstAusgefuehrt(ObjektAkte akte)
    {
        if (!akte.Werte.TryGetValue(StatusFeld, out var wert) || wert is null)
            return false;
        return string.Equals(
            (wert.Text ?? string.Empty).Trim(), StatusAusgefuehrt, StringComparison.OrdinalIgnoreCase);
    }
}
