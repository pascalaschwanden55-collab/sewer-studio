using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Legt Hauptcode-Unterordner an und liest darin abgelegte Bilder.
/// Es werden keine Eingangsdateien verschoben, geloescht oder veraendert.
/// </summary>
public sealed class PersonalGoldInboxFileService : IPersonalGoldInboxService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

    private readonly string _rootPath;
    private readonly Func<string, string?> _codeLabelLookup;

    public PersonalGoldInboxFileService(
        string knowledgeRoot,
        Func<string, string?>? codeLabelLookup = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRoot);
        _rootPath = Path.Combine(
            Path.GetFullPath(knowledgeRoot),
            "training",
            "gold_inbox");
        _codeLabelLookup = codeLabelLookup ?? VsaCodeResolver.LookupLabel;
    }

    public string EnsureFolders()
    {
        Directory.CreateDirectory(_rootPath);
        Directory.CreateDirectory(Path.Combine(_rootPath, "_OHNE_ZUORDNUNG"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "_ERLEDIGT"));
        foreach (var mainCode in PersonalGoldMainCodeCatalog.RequiredCodes)
        {
            Directory.CreateDirectory(Path.Combine(
                _rootPath,
                PersonalGoldMainCodeCatalog.FormatFolderName(
                    mainCode,
                    _codeLabelLookup)));
        }
        return _rootPath;
    }

    public Task<PersonalGoldInboxSnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
        => Task.Run(() => Load(cancellationToken), cancellationToken);

    private PersonalGoldInboxSnapshot Load(CancellationToken cancellationToken)
    {
        EnsureFolders();
        var images = new List<PersonalGoldInboxImage>();
        var issues = new List<string>();
        ReadFolder(_rootPath, suggestedMainCode: null, images, issues, cancellationToken);

        foreach (var directory in Directory
                     .EnumerateDirectories(_rootPath, "*", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (string.Equals(
                        Path.GetFileName(directory),
                        "_ERLEDIGT",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    issues.Add($"Verknuepfter Ordner wurde uebersprungen: {directory}");
                    continue;
                }

                ReadFolder(
                    directory,
                    ResolveMainCode(Path.GetFileName(directory)),
                    images,
                    issues,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                issues.Add($"Ordner konnte nicht gelesen werden: {directory} ({ex.Message})");
            }
        }

        return new PersonalGoldInboxSnapshot(
            _rootPath,
            images
                .OrderBy(image => image.SuggestedMainCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(image => image.FramePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            issues);
    }

    private static void ReadFolder(
        string folder,
        string? suggestedMainCode,
        ICollection<PersonalGoldInboxImage> images,
        ICollection<string> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var path in Directory
                         .EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                    continue;
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    issues.Add($"Verknuepfte Datei wurde uebersprungen: {path}");
                    continue;
                }

                var fullPath = Path.GetFullPath(path);
                images.Add(new PersonalGoldInboxImage(
                    fullPath,
                    BuildQueueId(fullPath),
                    suggestedMainCode));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            issues.Add($"Bilder konnten nicht gelesen werden: {folder} ({ex.Message})");
        }
    }

    private static string BuildQueueId(string path)
    {
        var normalized = Path.GetFullPath(path).ToUpperInvariant();
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        return $"gold_inbox_{hash[..16]}";
    }

    private static string? ResolveMainCode(string? folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return null;

        var normalized = folderName.Trim().ToUpperInvariant();
        if (normalized.Length < 3 || !normalized[..3].All(char.IsLetter))
            return null;

        if (normalized.Length == 3)
            return normalized;

        return normalized[3] is ' ' or '-' or '–' or '—' or '_'
            ? normalized[..3]
            : null;
    }

}
