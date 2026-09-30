using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.Import.Quellen;

/// <summary>
/// Welche von mehreren Untersuchungen derselben Haltung uebernommen wird. Eine Regel fuer
/// den WinCan-Import und den VSA-KEK-XTF-Import; das Lesen der Daten bleibt in der
/// Infrastruktur.
///
/// Gewaehlt wird die Untersuchung mit dem neuesten glaubwuerdigen Datum. Der WinCan-
/// Vorgabetag 2007-12-31 und alles vor 1990 sind Platzhalter, keine Aufnahmetage. Real
/// aufgefallen in Seilergasse (07.638905-78998): Die Untersuchung mit 12 Befunden trug das
/// Platzhalterdatum und verlor gegen eine mit 4 Befunden. Ohne glaubwuerdiges Datum ordnet
/// ein technischer Zeitstempel, falls die Quelle einen hat (WinCan: INS_TimeStamp; die
/// XTF hat keinen), sonst die Quellreihenfolge.
/// </summary>
public static class UntersuchungsAuswahl
{
    /// <summary>
    /// Der Tag, den WinCan VX als Vorgabe eintraegt, solange niemand ein Datum erfasst hat
    /// ("2007-12-31 23:27:20", ohne Bruchteilsekunden). Gemessen in der Seilergasse-Datenbank.
    /// </summary>
    public static readonly DateOnly WinCanVorgabetag = new(2007, 12, 31);

    /// <summary>Ist das Datum der WinCan-Vorgabetag?</summary>
    public static bool IstVorgabetag(DateTime? datum)
        => datum is { } d && DateOnly.FromDateTime(d) == WinCanVorgabetag;

    /// <summary>
    /// Das Datum, wenn es ein Aufnahmetag sein kann; ein Datum vor 1990 oder der
    /// WinCan-Vorgabetag ist ein Platzhalter und ergibt <c>null</c>.
    /// </summary>
    public static DateTime? Glaubwuerdig(DateTime? datum)
        => datum is { Year: >= 1990 } d && !IstVorgabetag(d) ? datum : null;

    /// <summary>
    /// Der Schluessel, nach dem die Untersuchungen geordnet werden: das erste glaubwuerdige
    /// Aufnahmedatum, sonst der technische Zeitstempel, sonst <see cref="DateTime.MinValue"/>.
    /// Der Zeitstempel darf nur ordnen und wird nie zum Inspektionsdatum.
    /// </summary>
    public static DateTime Sortierschluessel(IEnumerable<DateTime?> aufnahmedaten, DateTime? technischerZeitstempel = null)
    {
        ArgumentNullException.ThrowIfNull(aufnahmedaten);

        foreach (var datum in aufnahmedaten)
        {
            if (Glaubwuerdig(datum) is { } glaubwuerdig)
                return glaubwuerdig;
        }

        return technischerZeitstempel ?? DateTime.MinValue;
    }

    /// <summary>
    /// Ordnet die Untersuchungen einer Haltung: die zu uebernehmende zuerst. Neuerer
    /// Schluessel vor aelterem; bei Gleichstand bleibt die Quellreihenfolge (stabil).
    /// </summary>
    public static List<T> Ordne<T>(IEnumerable<T> kandidaten, Func<T, DateTime> sortierschluessel)
    {
        ArgumentNullException.ThrowIfNull(kandidaten);
        ArgumentNullException.ThrowIfNull(sortierschluessel);
        return kandidaten.OrderByDescending(sortierschluessel).ToList();
    }
}
