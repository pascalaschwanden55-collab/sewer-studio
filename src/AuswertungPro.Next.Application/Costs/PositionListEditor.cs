using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Costs;

/// <summary>
/// Stellt reine Listenoperationen fuer <see cref="PositionTemplate"/>-Listen bereit:
/// Verschieben nach oben/unten, Entfernen mit Folge-Selektion und Anlegen
/// einer neuen Standard-Position. Kein UI-Bezug.
/// </summary>
public static class PositionListEditor
{
    /// <summary>
    /// Gibt <c>true</c> zurueck, wenn das Element an <paramref name="index"/> nach oben
    /// verschoben werden kann (d.h. es ist nicht das erste Element).
    /// </summary>
    public static bool CanMoveUp(IList<PositionTemplate> list, int index) =>
        list.Count > 0 && index > 0;

    /// <summary>
    /// Gibt <c>true</c> zurueck, wenn das Element an <paramref name="index"/> nach unten
    /// verschoben werden kann (d.h. es ist nicht das letzte Element).
    /// </summary>
    public static bool CanMoveDown(IList<PositionTemplate> list, int index) =>
        index >= 0 && index < list.Count - 1;

    /// <summary>
    /// Tauscht das Element an <paramref name="index"/> mit dem Element davor.
    /// Gibt <c>true</c> zurueck, wenn der Tausch durchgefuehrt wurde.
    /// </summary>
    public static bool MoveUp(IList<PositionTemplate> list, int index)
    {
        if (!CanMoveUp(list, index))
            return false;

        (list[index - 1], list[index]) = (list[index], list[index - 1]);
        return true;
    }

    /// <summary>
    /// Tauscht das Element an <paramref name="index"/> mit dem Element danach.
    /// Gibt <c>true</c> zurueck, wenn der Tausch durchgefuehrt wurde.
    /// </summary>
    public static bool MoveDown(IList<PositionTemplate> list, int index)
    {
        if (!CanMoveDown(list, index))
            return false;

        (list[index], list[index + 1]) = (list[index + 1], list[index]);
        return true;
    }

    /// <summary>
    /// Entfernt das Element an <paramref name="index"/> aus der Liste und berechnet
    /// den Index des naechsten zu selektierenden Elements.
    /// Gibt -1 zurueck, wenn die Liste danach leer ist.
    /// </summary>
    public static int RemoveAndGetNextIndex(IList<PositionTemplate> list, int index)
    {
        if (index < 0 || index >= list.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        list.RemoveAt(index);

        if (list.Count == 0)
            return -1;

        // Naechstes Element waehlen: unveraendert wenn moeglich, sonst vorangehendes
        return Math.Min(index, list.Count - 1);
    }

    /// <summary>
    /// Erstellt eine neue <see cref="PositionTemplate"/> mit den Standardwerten
    /// fuer eine freie (benutzerdefinierte) Position.
    /// </summary>
    public static PositionTemplate CreateDefault() =>
        new()
        {
            Enabled = true,
            DefaultQty = 1,
            Name = "Neue Position",
            Unit = "Stk",
            Price = 0,
            IsCustom = true
        };
}
