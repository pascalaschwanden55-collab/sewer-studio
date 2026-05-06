using System;
using AuswertungPro.Next.Application.Ai.Vision;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Helpers;

/// <summary>
/// Extension-Method fuer sicheres Fire-and-Forget.
/// Verhindert ungeloggte Exceptions bei _ = SomeAsync(...) Aufrufen.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Fuehrt eine Task aus ohne auf das Ergebnis zu warten.
    /// Exceptions werden per Debug.WriteLine geloggt (kein Crash).
    /// </summary>
    /// <param name="task">Die auszufuehrende Task.</param>
    /// <param name="context">Kontext-Info fuer Log (z.B. "LiveDetection").</param>
    /// <param name="onError">Optionaler Error-Callback.</param>
    public static async void SafeFireAndForget(
        this Task task,
        string? context = null,
        Action<Exception>? onError = null)
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
            onError?.Invoke(ex);
        }
    }
}
