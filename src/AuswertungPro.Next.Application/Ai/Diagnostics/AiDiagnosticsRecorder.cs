using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Ai.Diagnostics;

/// <summary>
/// Default-Implementierung des <see cref="IAiDiagnosticsRecorder"/>:
/// Thread-safer Ringbuffer auf Basis von <see cref="ConcurrentQueue{T}"/>.
///
/// Speichert maximal <see cref="Capacity"/> Events; ueberschuessige Eintraege
/// werden FIFO verworfen. <see cref="AiDiagnosticEvent.RawOutput"/> wird auf
/// <see cref="MaxRawOutputChars"/> gekuerzt, damit der Buffer nicht durch
/// einzelne 200KB-JSON-Antworten gesprengt wird.
/// </summary>
public sealed class AiDiagnosticsRecorder : IAiDiagnosticsRecorder
{
    /// <summary>Maximale Anzahl Events im Ringbuffer (Default 200).</summary>
    public int Capacity { get; }

    /// <summary>Maximale Zeichenlaenge pro RawOutput (Default 16 KB).</summary>
    public int MaxRawOutputChars { get; }

    private readonly ConcurrentQueue<AiDiagnosticEvent> _events = new();

    public AiDiagnosticsRecorder(int capacity = 200, int maxRawOutputChars = 16 * 1024)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (maxRawOutputChars < 0) throw new ArgumentOutOfRangeException(nameof(maxRawOutputChars));
        Capacity = capacity;
        MaxRawOutputChars = maxRawOutputChars;
    }

    public void Record(AiDiagnosticEvent evt)
    {
        if (evt is null) return;

        // RawOutput kuerzen falls noetig — Kopie nur dann erzeugen, wenn
        // Truncation greift, sonst Original wiederverwenden.
        var clipped = evt;
        if (evt.RawOutput is { Length: int len } && len > MaxRawOutputChars)
        {
            clipped = evt with
            {
                RawOutput = evt.RawOutput[..MaxRawOutputChars] +
                            $"\n…[truncated {len - MaxRawOutputChars} chars]"
            };
        }

        _events.Enqueue(clipped);

        // Ringbuffer-Verhalten: zu viele Eintraege → aelteste droppen.
        while (_events.Count > Capacity && _events.TryDequeue(out _)) { }
    }

    public IReadOnlyList<AiDiagnosticEvent> Snapshot(int limit = 100)
    {
        if (limit <= 0) return Array.Empty<AiDiagnosticEvent>();

        // ToArray ist thread-safe auf ConcurrentQueue (interner Lock-Snapshot).
        var all = _events.ToArray();

        // Juengster Eintrag zuerst.
        if (all.Length <= limit)
            return all.Reverse().ToArray();

        return all.Skip(all.Length - limit).Reverse().ToArray();
    }

    public void Clear()
    {
        while (_events.TryDequeue(out _)) { }
    }
}
