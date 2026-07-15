using System.Diagnostics;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Gesammelte Ausgabe eines beendeten externen Prozesses.</summary>
public sealed record ProcessOutputResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

/// <summary>
/// Startet einen externen Prozess, leert beide Ausgabekanaele und beendet
/// bei Abbruch den gesamten gestarteten Prozessbaum.
/// </summary>
public interface IProcessOutputReader
{
    Task<ProcessOutputResult?> ReadToExitAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken,
        Action<int>? onStarted = null);
}
