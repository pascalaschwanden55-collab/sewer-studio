using System;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Fuehrt best-effort-Operationen aus, deren Fehlschlag den Hauptablauf nicht abbrechen darf
/// (Cleanup, Dispose, optionale Schritte). Ersetzt stille <c>catch {}</c>-Bloecke: der Fehler
/// wird GEMELDET (Kontext + Typ + Message) statt unsichtbar verschluckt.
///
/// Standard-Sink ist <c>Debug.WriteLine</c> (geeignet fuer ignorierbare Cleanup-Fehler). Fuer
/// fachlich relevante Fehler (Import/KI/Persistenz) soll der Aufrufer einen SICHTBAREN Sink
/// (Logger/Trace) uebergeben, damit kein stiller Datenverlust entsteht.
///
/// Reine Utility, kein State, keine Domaenenlogik — in Application.Common, damit alle Schichten
/// sie nutzen koennen.
/// </summary>
public static class BestEffort
{
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

    private static void Report(string context, Exception ex, Action<string>? onError)
    {
        var message = $"{context}: {ex.GetType().Name}: {ex.Message}";
        if (onError is not null)
            onError(message);
        else
            System.Diagnostics.Debug.WriteLine($"[BestEffort] {message}");
    }
}
