using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Helpers;

/// <summary>
/// Extension-Method fuer sicheres Fire-and-Forget.
/// Verhindert ungeloggte Exceptions bei _ = SomeAsync(...) Aufrufen.
/// </summary>
public static class TaskExtensions
{
    private static ILogger? _logger;

    /// <summary>
    /// Verbindet unbeobachtete Hintergrundaufgaben mit dem normalen Tageslog.
    /// </summary>
    public static void ConfigureLogging(ILogger? logger)
        => Volatile.Write(ref _logger, logger);

    /// <summary>
    /// Fuehrt eine Task aus ohne auf das Ergebnis zu warten.
    /// Exceptions werden im Tageslog und zusaetzlich per Debug.WriteLine gemeldet (kein Crash).
    /// </summary>
    /// <param name="task">Die auszufuehrende Task.</param>
    /// <param name="context">Kontext-Info fuer Log (z.B. "LiveDetection").</param>
    /// <param name="onError">Optionaler Error-Callback.</param>
    public static async void SafeFireAndForget(
        this Task task,
        string? context = null,
        Action<Exception>? onError = null,
        ILogger? logger = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation ist kein Fehler — still ignorieren
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"[FireAndForget] {context ?? "?"}: {ex.GetType().Name}: {ex.Message}");

            // Der normale Tageslog ist die verlaessliche Fehlerquelle. Debug.WriteLine ist nur
            // eine zusaetzliche Hilfe fuer Entwickler und in der installierten App unsichtbar.
            TryLog(logger ?? Volatile.Read(ref _logger), ex, context);

            if (onError is not null)
            {
                try
                {
                    onError(ex);
                }
                catch (Exception callbackException)
                {
                    Debug.WriteLine(
                        $"[FireAndForget] Fehlerbehandlung {context ?? "?"}: " +
                        $"{callbackException.GetType().Name}: {callbackException.Message}");
                    TryLog(logger ?? Volatile.Read(ref _logger), callbackException, $"{context ?? "?"}.Fehlerbehandlung");
                }
            }
        }
    }

    private static void TryLog(ILogger? logger, Exception exception, string? context)
    {
        if (logger is null)
            return;

        try
        {
            logger.LogError(
                exception,
                "Hintergrundaufgabe {Context} ist fehlgeschlagen.",
                context ?? "Unbekannt");
        }
        catch (Exception loggingException)
        {
            // Auch ein voruebergehend nicht beschreibbarer Log darf die App nicht beenden.
            Debug.WriteLine(
                $"[FireAndForget] Tageslog nicht beschreibbar: " +
                $"{loggingException.GetType().Name}: {loggingException.Message}");
        }
    }
}
