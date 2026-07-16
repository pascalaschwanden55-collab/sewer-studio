using System.Diagnostics;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Kompatibilitaetsfassade; Prozessstart und Abbruch liegen im Instanzdienst.
/// </summary>
public static class ProcessOutputReader
{
    private static readonly IProcessOutputReader Default = new ProcessOutputReaderService();

    public static IProcessOutputReader Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IProcessOutputReader reader) =>
        throw new NotSupportedException(
            "Der globale Prozessausgabe-Leser kann nicht mehr ausgetauscht werden. " +
            "IProcessOutputReader bitte per Konstruktor uebergeben.");

    public static Task<ProcessOutputResult?> ReadToExitAsync(
        ProcessStartInfo startInfo,
        CancellationToken ct,
        Action<int>? onStarted = null)
        => Current.ReadToExitAsync(startInfo, ct, onStarted);
}
