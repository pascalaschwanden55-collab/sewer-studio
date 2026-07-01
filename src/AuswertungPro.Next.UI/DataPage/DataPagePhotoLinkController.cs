namespace AuswertungPro.Next.UI.DataPage;

public enum DataPagePhotoLinkStatus
{
    Noop,
    Missing,
    Open
}

public sealed record DataPagePhotoLinkOpenPlan(
    DataPagePhotoLinkStatus Status,
    string? RawPath,
    string? ResolvedPath);

/// <summary>
/// Reine Entscheidungslogik fuer Foto-Links der Haltungsansicht.
/// Datei-Dialoge und ShellOpen bleiben im Code-behind.
/// </summary>
public static class DataPagePhotoLinkController
{
    public static DataPagePhotoLinkOpenPlan BuildOpenPlan(
        string? rawPath,
        string? projectPath,
        Func<string, string?, string?> resolvePath,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(resolvePath);
        ArgumentNullException.ThrowIfNull(fileExists);

        if (string.IsNullOrWhiteSpace(rawPath))
            return new DataPagePhotoLinkOpenPlan(DataPagePhotoLinkStatus.Noop, rawPath, null);

        var trimmed = rawPath.Trim();
        var resolved = resolvePath(trimmed, projectPath) ?? trimmed;
        if (string.IsNullOrWhiteSpace(resolved) || !fileExists(resolved))
            return new DataPagePhotoLinkOpenPlan(DataPagePhotoLinkStatus.Missing, trimmed, resolved);

        return new DataPagePhotoLinkOpenPlan(DataPagePhotoLinkStatus.Open, trimmed, resolved);
    }
}
