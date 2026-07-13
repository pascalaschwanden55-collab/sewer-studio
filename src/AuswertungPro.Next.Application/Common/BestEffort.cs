using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Fuehrt best-effort-Operationen aus, deren Fehlschlag den Hauptablauf nicht abbrechen darf
/// (Cleanup, Dispose, optionale Schritte). Ersetzt stille <c>catch {}</c>-Bloecke: der Fehler
/// wird GEMELDET (Kontext + Typ + Message) statt unsichtbar verschluckt.
///
/// Die WPF-App verbindet den Standard-Sink beim Start mit dem Tageslog. Ohne konfigurierte App
/// bleibt <c>Trace.WriteLine</c> als Release-tauglicher Rueckfall erhalten.
///
/// Reine Utility, kein State, keine Domaenenlogik — in Application.Common, damit alle Schichten
/// sie nutzen koennen.
/// </summary>
public static class BestEffort
{
    private static Action<string>? _defaultErrorSink;

    /// <summary>
    /// Verbindet Best-Effort-Fehler mit dem zentralen Programmlog. <c>null</c> setzt die
    /// Konfiguration zurueck (z. B. beim Test- oder Programmende).
    /// </summary>
    public static void ConfigureDefaultErrorSink(Action<string>? sink)
        => Volatile.Write(ref _defaultErrorSink, sink);

    /// <summary>Fuehrt <paramref name="action"/> aus; meldet einen Fehler statt ihn zu verschlucken.</summary>
    public static void Try(Action action, string context, Action<string>? onError = null)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Report(context, ex, onError);
        }
    }

    /// <summary>Async-Variante von <see cref="Try"/>.</summary>
    public static async Task TryAsync(Func<Task> action, string context, Action<string>? onError = null)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Report(context, ex, onError);
        }
    }

    /// <summary>
    /// Meldet eine wichtige Warnung ueber denselben Release-tauglichen Kanal. Geeignet fuer
    /// Fehlerpfade, die absichtlich mit einem Ersatzwert oder Backup weiterarbeiten.
    /// </summary>
    public static void ReportWarning(string message, Action<string>? onError = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Write(message, onError);
    }

    private static void Report(string context, Exception ex, Action<string>? onError)
    {
        var message = $"{context}: {ex.GetType().Name}: {ex.Message}";
        Write(message, onError);
    }

    private static void Write(string message, Action<string>? onError)
    {
        var sink = onError ?? Volatile.Read(ref _defaultErrorSink);
        if (sink is not null)
        {
            try
            {
                sink(message);
                return;
            }
            catch (Exception sinkError)
            {
                Trace.WriteLine(
                    $"[BestEffort] Log-Sink fehlgeschlagen: {sinkError.GetType().Name}: {sinkError.Message}");
            }
        }

        Trace.WriteLine($"[BestEffort] {message}");
    }
}
