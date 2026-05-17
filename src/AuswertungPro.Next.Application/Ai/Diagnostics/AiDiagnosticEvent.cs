using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.Diagnostics;

/// <summary>
/// Eintrag in der Diagnose-Spur. Wird vom <see cref="IAiDiagnosticsRecorder"/>
/// aufgenommen und kann von der UI ueber Snapshots gelesen werden.
///
/// Felder sind absichtlich breit, damit sich verschiedene Pfade (Qwen-Raw,
/// YOLO, Multi-Model, Filter-Drop, Pipe-Axis, Event-Created) im selben Schema
/// abbilden lassen. Nicht-anwendbare Felder bleiben <c>null</c>.
/// </summary>
public sealed record AiDiagnosticEvent
{
    /// <summary>Aufzeichnungs-Zeitpunkt (UTC).</summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Stage-Bezeichnung — siehe <see cref="AiDiagnosticStage"/>.</summary>
    public string Stage { get; init; } = "";

    /// <summary>
    /// Frei waehlbare Quellbezeichnung (z.B. Klassen-/Methodenname am Aufrufpunkt).
    /// Hilft beim Zurueckverfolgen ohne Stack-Trace.
    /// </summary>
    public string Source { get; init; } = "";

    /// <summary>Modell-Identifikator falls relevant (z.B. <c>qwen3-vl:8b-q8</c>).</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Stabiler Hash/Identifier des Inputs (z.B. SHA-1 des Frames + Meter).
    /// Erlaubt Korrelation mehrerer Events fuer denselben Frame
    /// (qwen.raw → qwen.mapped → event.created).
    /// </summary>
    public string? InputHash { get; init; }

    /// <summary>Knapper Klartext, was passiert ist (UI-freundlich).</summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// Roh-Ausgabe (z.B. Qwen-JSON, YOLO-Findings als JSON). Wird vom Recorder
    /// auf <see cref="AiDiagnosticsRecorder.MaxRawOutputChars"/> gekuerzt, damit
    /// der Ringbuffer nicht uebermaessig anwaechst. Komplette Rohdaten gehoeren
    /// weiter in die jeweiligen Datei-Logs.
    /// </summary>
    public string? RawOutput { get; init; }

    /// <summary>
    /// Bei einem Drop/Suppress-Event: standardisierter Grund.
    /// Siehe <see cref="AiDiagnosticDropReason"/>.
    /// </summary>
    public string? DroppedReason { get; init; }

    /// <summary>Latenz in Millisekunden (z.B. Qwen-Roundtrip, YOLO-Inference).</summary>
    public double? LatencyMs { get; init; }

    /// <summary>
    /// Optionales freies Wertepaar-Beiwerk (z.B. <c>view_type</c>,
    /// <c>image_quality</c>, <c>frame_index</c>, <c>meter</c>).
    /// Bewusst <c>IReadOnlyDictionary</c>, damit die Aufrufseite das Material
    /// nicht im Nachhinein mutiert.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
