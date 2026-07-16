using System;
using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Findet die Repo-Wurzel anhand der AuswertungPro.sln.</summary>
public static class RepoRootLocator
{
    private static readonly IRepositoryRootLocator Default = new RepositoryRootFileLocator();

    public static IRepositoryRootLocator Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IRepositoryRootLocator locator) =>
        throw new NotSupportedException(
            "Die globale Projektordnersuche kann nicht mehr ausgetauscht werden. " +
            "IRepositoryRootLocator bitte per Konstruktor uebergeben.");

    public static string? Locate()
        => Current.Locate(AppContext.BaseDirectory);

    public static string? Locate(string? startPath)
        => Current.Locate(startPath);
}
