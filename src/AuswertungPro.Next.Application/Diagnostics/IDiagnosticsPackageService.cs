namespace AuswertungPro.Next.Application.Diagnostics;

public sealed record DiagnosticsPackageResult(
    bool Success,
    string? PackagePath,
    int IncludedLogFileCount,
    string UserMessage);

/// <summary>Erstellt ein begrenztes, bereinigtes Supportpaket ohne Projektdateien.</summary>
public interface IDiagnosticsPackageService
{
    string LogDirectory { get; }

    Task<DiagnosticsPackageResult> CreateAsync(
        string destinationZipPath,
        CancellationToken cancellationToken = default);
}
