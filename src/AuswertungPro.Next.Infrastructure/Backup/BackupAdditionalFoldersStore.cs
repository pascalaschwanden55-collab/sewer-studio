using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Eigene Konfiguration, die auch eine noch laufende alte Programmversion nicht überschreibt.</summary>
public sealed class BackupAdditionalFoldersStore(string settingsFolder) : IBackupAdditionalFolders
{
    private readonly string _file = Path.Combine(settingsFolder, "backup-additional-folders.json");
    public IReadOnlyList<string> Load()
        => File.Exists(_file)
            ? Normalize(JsonSerializer.Deserialize<string[]>(File.ReadAllText(_file))
                        ?? throw new InvalidDataException("Zusätzliche Sicherungsordner sind ungültig."))
            : [];

    public void Save(IEnumerable<string> folders)
        => AtomicTextFileWriter.WriteAllText(_file, JsonSerializer.Serialize(Normalize(folders)), durable: true);

    private static string[] Normalize(IEnumerable<string> folders)
        => folders.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p =>
        {
            var path = p.Trim();
            if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Bitte einen vollständigen Ordnerpfad eingeben.");
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
