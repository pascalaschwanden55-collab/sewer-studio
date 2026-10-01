using System;
using System.Threading;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Haelt die Reihenfolge der Einstellungs-Schreibvorgaenge.
///
/// Anlass (Auditbefund 15, 18.09.2026): Die verzoegerte und die sofortige Speicherung
/// entnahmen ihren Auftrag unter einer Sperre, schrieben ihn aber AUSSERHALB davon.
/// Begann der verzoegerte Lauf zuerst und wurde langsamer fertig, lag hinterher der
/// aeltere Stand auf der Platte.
///
/// Deshalb entscheidet und schreibt diese Regel im selben kritischen Abschnitt. Eine
/// blosse Vorabfrage wuerde nicht genuegen: Zwischen Freigabe und Schreiben koennten
/// sich zwei Auftraege erneut ueberholen — genau der Fehler, um den es geht.
/// </summary>
public sealed class SettingsWriteOrder
{
    private long _next;
    private long _lastWritten;
    private readonly object _sync = new();

    /// <summary>Nummer fuer einen neuen Schreibauftrag; streng aufsteigend.</summary>
    public long Next() => Interlocked.Increment(ref _next);

    /// <summary>
    /// Fuehrt <paramref name="write"/> nur aus, wenn <paramref name="sequence"/> neuer ist
    /// als der zuletzt geschriebene Stand. true = geschrieben, false = ueberholt und
    /// bewusst verworfen (der Inhalt ist bereits durch einen neueren Stand ersetzt).
    /// </summary>
    public bool Write(long sequence, Action write)
    {
        ArgumentNullException.ThrowIfNull(write);

        lock (_sync)
        {
            if (sequence <= _lastWritten)
                return false;

            write();
            _lastWritten = sequence;
            return true;
        }
    }
}
