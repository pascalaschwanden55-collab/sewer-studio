namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Fortschritts-Information fuer einen laufenden Import-Lauf.
/// </summary>
public sealed record ImportProgress(
    string Phase,
    int Current,
    int Total,
    string StatusText,
    string? CurrentFile = null);
