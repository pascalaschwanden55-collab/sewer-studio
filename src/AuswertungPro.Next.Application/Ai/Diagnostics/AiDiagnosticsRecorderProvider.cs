using System;

namespace AuswertungPro.Next.Application.Ai.Diagnostics;

/// <summary>
/// Provider-Bridge fuer den globalen <see cref="IAiDiagnosticsRecorder"/> —
/// analog zu <c>KnowledgeRootProvider</c>, <c>OllamaConfigProvider</c> etc.
///
/// Hintergrund: <c>OllamaClient</c> und andere Infrastructure-Services werden
/// an vielen Stellen direkt instanziiert (statisches <c>CreateOllamaClient</c>),
/// nicht ueber DI. Ein Constructor-Parameter haette breite Aenderungs-Folge.
/// Stattdessen setzt die App beim Start einen Recorder ueber <see cref="Set"/>;
/// Aufrufer holen ihn ueber <see cref="Current"/>. Falls keiner registriert
/// ist, liefert <see cref="Current"/> einen <c>NullRecorder</c>, sodass
/// <c>Record(...)</c>-Aufrufe nie crashen.
/// </summary>
public static class AiDiagnosticsRecorderProvider
{
    private static IAiDiagnosticsRecorder? _instance;

    /// <summary>Liefert den registrierten Recorder oder einen No-Op-Recorder.</summary>
    public static IAiDiagnosticsRecorder Current => _instance ?? NullRecorder.Instance;

    /// <summary>
    /// Setzt den globalen Recorder. Einmal beim App-Start.
    /// Mehrfach-Aufruf ist erlaubt (z.B. Tests im selben Prozess), aber
    /// es wird ein Trace-Hinweis geschrieben — sonst gehen Events am
    /// vorigen Recorder lautlos verloren (Audit 2026-05-13 M10).
    /// </summary>
    public static void Set(IAiDiagnosticsRecorder recorder)
    {
        if (recorder is null) throw new ArgumentNullException(nameof(recorder));

        var previous = _instance;
        if (previous is not null && !ReferenceEquals(previous, recorder))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AiDiagnosticsRecorderProvider] Re-Set: vorheriger Recorder " +
                $"({previous.GetType().Name}) wird durch {recorder.GetType().Name} ersetzt. " +
                $"Bisher aufgezeichnete Events bleiben dort und sind nicht mehr ueber " +
                $"Provider.Current sichtbar.");
        }
        _instance = recorder;
    }

    /// <summary>True wenn ein echter Recorder registriert ist (kein Null-Recorder).</summary>
    public static bool HasRecorder => _instance is not null;

    /// <summary>Nur fuer Tests: zuruecksetzen.</summary>
    public static void Reset() => _instance = null;

    private sealed class NullRecorder : IAiDiagnosticsRecorder
    {
        public static readonly NullRecorder Instance = new();
        private NullRecorder() { }
        public void Record(AiDiagnosticEvent evt) { /* no-op */ }
        public System.Collections.Generic.IReadOnlyList<AiDiagnosticEvent> Snapshot(int limit = 100)
            => System.Array.Empty<AiDiagnosticEvent>();
        public void Clear() { /* no-op */ }
    }
}
