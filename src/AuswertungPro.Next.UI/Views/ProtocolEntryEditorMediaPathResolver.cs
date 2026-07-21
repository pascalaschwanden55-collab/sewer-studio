using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Views;

internal sealed class ProtocolEntryEditorMediaPathResolver
{
    private readonly string? _projectFolder;
    private readonly Func<string?> _currentProjectPath;
    private readonly Func<string, bool> _fileExists;

    internal ProtocolEntryEditorMediaPathResolver(
        string? projectFolder,
        Func<string?> currentProjectPath,
        Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(currentProjectPath);

        _projectFolder = projectFolder;
        _currentProjectPath = currentProjectPath;
        _fileExists = fileExists ?? File.Exists;
    }

    internal string ResolveProjectFolder()
    {
        if (!string.IsNullOrWhiteSpace(_projectFolder))
            return _projectFolder;

        var fromSettings = _currentProjectPath();
        if (!string.IsNullOrWhiteSpace(fromSettings))
        {
            var directory = ProjectFileLocator.ProjectRootFromFile(fromSettings)
                            ?? Path.GetDirectoryName(fromSettings);
            if (!string.IsNullOrWhiteSpace(directory))
                return directory;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    internal string? ResolveExistingPath(string? rawPath)
    {
        var path = rawPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (_fileExists(path))
            return path;

        if (Path.IsPathRooted(path))
            return null;

        var baseDirectory = ResolveProjectFolder();
        if (string.IsNullOrWhiteSpace(baseDirectory))
            return null;

        var combined = Path.GetFullPath(Path.Combine(baseDirectory, path));
        return _fileExists(combined) ? combined : null;
    }

    internal IReadOnlyList<string> ResolveImagePaths(IReadOnlyList<string> rawPaths)
    {
        var result = new List<string>();
        foreach (var rawPath in rawPaths)
        {
            var path = ResolveExistingPath(rawPath);
            if (string.IsNullOrWhiteSpace(path))
                continue;
            if (!result.Contains(path, StringComparer.OrdinalIgnoreCase))
                result.Add(path);
        }

        return result;
    }
}
