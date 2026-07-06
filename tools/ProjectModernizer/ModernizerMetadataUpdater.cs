using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

internal static class ModernizerMetadataUpdater
{
    public static void UpdateStoredImportMetadata(Project project, bool dryRun, ModernizeReport report)
    {
        foreach (var key in ModernizerProjectKeys.StoredImportMetadataKeys)
        {
            if (!project.Metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            var updated = value
                .Replace(ModernizerLegacyFolders.Imports + "\\\\", ProjectStructure.Importdateien + "\\\\", StringComparison.OrdinalIgnoreCase)
                .Replace(ModernizerLegacyFolders.Imports + "\\", ProjectStructure.Importdateien + "\\", StringComparison.OrdinalIgnoreCase)
                .Replace(ModernizerLegacyFolders.Imports + "/", ProjectStructure.Importdateien + "/", StringComparison.OrdinalIgnoreCase);

            if (!string.Equals(value, updated, StringComparison.Ordinal))
            {
                if (!dryRun)
                    project.Metadata[key] = updated;
                report.MetadataUpdated++;
            }
        }
    }
}
