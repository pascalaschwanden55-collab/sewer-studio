using System.Diagnostics;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Kompatibilitaetsfassade; Prozessstart und Abbruch liegen im Instanzdienst.
/// </summary>
public static class ProcessOutputReader
{
    private static IProcessOutputReader _current = new ProcessOutputReaderService();

    public static IProcessOutputReader Current => Volatile.Read(ref _current);

    public static void Use(IProcessOutputReader reader)
        => Volatile.Write(
            ref _current,
            reader ?? throw new ArgumentNullException(nameof(reader)));

    public static Task<ProcessOutputResult?> ReadToExitAsync(
        ProcessStartInfo startInfo,
        CancellationToken ct,
        Action<int>? onStarted = null)
        => Current.ReadToExitAsync(startInfo, ct, onStarted);
}
