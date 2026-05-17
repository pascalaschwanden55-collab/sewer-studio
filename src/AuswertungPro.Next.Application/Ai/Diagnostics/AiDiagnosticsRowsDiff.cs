using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.Diagnostics;

/// <summary>
/// Differential-Update einer Zeilensammlung gegen einen Recorder-Snapshot.
///
/// Audit 2026-05-13 M7: vorher rief das AiDiagnosticsWindow jede Sekunde
/// <c>_rows.Clear() + foreach Add</c> auf — bei 500 Eintraegen ein komplettes
/// Re-Rendering und Selection-Verlust. Diese Klasse bringt die Liste mit
/// minimalen Insert/Remove-Operationen auf den Snapshot-Zustand, sodass
/// WPF nur die tatsaechlich geaenderten Zeilen neu zeichnen muss.
///
/// Reine Logik ohne UI-Abhaengigkeit — generisch ueber den Row-Typ.
/// </summary>
public static class AiDiagnosticsRowsDiff
{
    /// <summary>
    /// Bringt <paramref name="existingRows"/> in den Zustand von
    /// <paramref name="snapshot"/>: in der Liste fehlende Snapshot-Eintraege
    /// werden positionstreu eingefuegt, nicht mehr im Snapshot vertretene
    /// Eintraege werden entfernt. Reihenfolge der Liste entspricht nach dem
    /// Aufruf der Reihenfolge des Snapshots.
    /// </summary>
    /// <typeparam name="TRow">View-Adapter-Typ (z.B. <c>EventRow</c>).</typeparam>
    /// <param name="existingRows">Aktuelle Zeilen (z.B. ObservableCollection).</param>
    /// <param name="snapshot">Soll-Zustand aus dem Recorder, juengster Eintrag zuerst.</param>
    /// <param name="keyOf">Liest den stabilen Schluessel aus einer Zeile (typischerweise <c>TimestampUtc</c>).</param>
    /// <param name="toRow">Wandelt ein Event in eine Zeile (View-Adapter).</param>
    public static void Apply<TRow>(
        IList<TRow> existingRows,
        IReadOnlyList<AiDiagnosticEvent> snapshot,
        Func<TRow, DateTimeOffset> keyOf,
        Func<AiDiagnosticEvent, TRow> toRow)
    {
        if (existingRows is null) throw new ArgumentNullException(nameof(existingRows));
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (keyOf is null) throw new ArgumentNullException(nameof(keyOf));
        if (toRow is null) throw new ArgumentNullException(nameof(toRow));

        // 1) Eintraege entfernen, die nicht (mehr) im Snapshot stehen.
        //    Ringbuffer-Eviction oder Filter-Wechsel.
        var desired = new HashSet<DateTimeOffset>(snapshot.Count);
        foreach (var e in snapshot)
            desired.Add(e.TimestampUtc);

        for (int i = existingRows.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(keyOf(existingRows[i])))
                existingRows.RemoveAt(i);
        }

        // 2) Snapshot-Reihenfolge erzwingen — Insert wo Schluessel abweicht.
        //    Da Schritt 1 bereits alles Ueberzaehlige entfernt hat, muss
        //    existingRows nach Schritt 2 exakt die Snapshot-Sequenz haben.
        for (int i = 0; i < snapshot.Count; i++)
        {
            var snapKey = snapshot[i].TimestampUtc;
            if (i >= existingRows.Count || !KeyEquals(keyOf(existingRows[i]), snapKey))
            {
                existingRows.Insert(i, toRow(snapshot[i]));
            }
        }
    }

    private static bool KeyEquals(DateTimeOffset a, DateTimeOffset b)
        => a.UtcTicks == b.UtcTicks;
}
