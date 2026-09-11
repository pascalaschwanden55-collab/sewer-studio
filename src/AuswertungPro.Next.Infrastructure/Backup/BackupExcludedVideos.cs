using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

internal static class BackupExcludedVideos
{
    // Abwählen bedeutet nicht aktualisieren. Bereits gesicherte Videos bleiben erhalten.
    public static void Preserve(string root, ISet<string> expected)
    {
        foreach (var component in new[] { "Projekte", "Externe_Dateien" })
        {
            var folder = BackupTargetPathGuard.ResolveRelativePath(root, component);
            if (!Directory.Exists(folder)) continue;
            BackupTargetPathGuard.EnsureTreeIsSafe(folder);
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                if (BackupExclusionRules.IsProjectVideoFileExcluded(file))
                    expected.Add(Path.GetRelativePath(root, file));
        }
    }
}
