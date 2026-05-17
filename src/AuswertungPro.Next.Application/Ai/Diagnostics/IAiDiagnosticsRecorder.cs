using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.Diagnostics;

/// <summary>
/// Aufzeichnungs-Schnittstelle fuer AI-Diagnose-Events. Implementierungen muessen
/// thread-safe sein — Aufrufer kommen aus parallelen Pipelines (Qwen,
/// Multi-Model, Filter-Pfad).
///
/// Bewusst KEIN INotifyCollectionChanged / IObservable: UI wird via Snapshot
/// abgefragt (Polling oder eigene Subscribe-Logik). Damit bleibt der Recorder
/// frei von WPF/UI-Abhaengigkeiten und kann in Tests + Headless verwendet
/// werden.
/// </summary>
public interface IAiDiagnosticsRecorder
{
    /// <summary>Nimmt ein Event auf. Darf nicht werfen.</summary>
    void Record(AiDiagnosticEvent evt);

    /// <summary>
    /// Liefert eine unveraenderliche Momentaufnahme der zuletzt aufgezeichneten
    /// Events, juengster Eintrag zuerst.
    /// </summary>
    /// <param name="limit">Maximalanzahl Eintraege im Snapshot.</param>
    IReadOnlyList<AiDiagnosticEvent> Snapshot(int limit = 100);

    /// <summary>Leert den Ringbuffer.</summary>
    void Clear();
}
