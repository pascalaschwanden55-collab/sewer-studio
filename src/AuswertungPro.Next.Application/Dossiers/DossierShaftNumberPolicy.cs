using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Die eine Regel, welche Nummer ein Schacht traegt.
///
/// Ein Dossier verweist auf Schaechte ueber ihre Nummer. Wenn das
/// Auswahlfenster, die Cockpit-Tabelle und das Nachfuehren diese Nummer je
/// selbst zusammensuchen, driften die drei Regeln auseinander: dann waehlt
/// man einen Schacht, den die Tabelle danach nicht mehr wiederfindet. Genau
/// dieser Fehler — dieselbe Suche zweimal im Code — hat schon einmal einen
/// ganzen Import leer ausgehen lassen.
///
/// Die Reihenfolge entspricht <c>SchaechteColumnPolicy.GetSchachtNumber</c>
/// der Schaechte-Seite, damit das Dossier denselben Schacht meint wie das
/// uebrige Programm.
/// </summary>
public static class DossierShaftNumberPolicy
{
    /// <summary>Feldnamen in der Reihenfolge, in der sie gelten.</summary>
    private static readonly string[] NumberFields = ["Schachtnummer", "Nr.", "NR."];

    /// <summary>
    /// Die Nummer dieses Schachts, oder eine leere Zeichenkette, wenn er keine
    /// traegt. Ein Schacht ohne Nummer ist im Dossier nicht speicherbar.
    /// </summary>
    public static string NumberOf(SchachtRecord? record)
    {
        if (record is null)
            return string.Empty;

        foreach (var field in NumberFields)
        {
            var value = (record.GetFieldValue(field) ?? string.Empty).Trim();
            if (value.Length > 0)
                return value;
        }

        return string.Empty;
    }

    /// <summary>
    /// Die Nummern aller Schaechte des Projekts in Projektreihenfolge, ohne
    /// leere und ohne doppelte. Doppelte gaebe es sonst zweimal zur Auswahl,
    /// ohne dass man sie auseinanderhalten koennte.
    /// </summary>
    public static IReadOnlyList<string> NumbersOf(Project? project)
    {
        var nummern = new List<string>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in project?.SchaechteData ?? Enumerable.Empty<SchachtRecord>())
        {
            var nummer = NumberOf(record);
            if (nummer.Length > 0 && gesehen.Add(nummer))
                nummern.Add(nummer);
        }

        return nummern;
    }
}
