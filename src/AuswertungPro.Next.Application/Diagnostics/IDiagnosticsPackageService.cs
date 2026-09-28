namespace AuswertungPro.Next.Application.Diagnostics;

public sealed record DiagnosticsPackageResult(
    bool Success,
    string? PackagePath,
    int IncludedLogFileCount,
    string UserMessage,
    /// <summary>
    /// Anzahl der Logdateien, die beim Erstellen nicht gelesen werden konnten (0 bei sauberem
    /// Erfolg). Traegt genau die Information, die vorher nur im <see cref="UserMessage"/>-Text
    /// stand ("… nicht lesbar") - damit ein Aufrufer einen Teilerfolg erkennen kann, ohne den
    /// Text zu parsen.
    /// </summary>
    int SkippedLogFileCount = 0);

/// <summary>Erstellt ein begrenztes, bereinigtes Supportpaket ohne Projektdateien.</summary>
public interface IDiagnosticsPackageService
{
    string LogDirectory { get; }

    Task<DiagnosticsPackageResult> CreateAsync(
        string destinationZipPath,
        CancellationToken cancellationToken = default);
}
