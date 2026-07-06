internal sealed record ModernizeOptions(
    string ProjectFolder,
    string? ProjectFile,
    string? SourceFolder,
    bool DryRun,
    bool FlattenOnly)
{
    public static ModernizeOptions? Parse(string[] args)
    {
        string? projectFolder = null;
        string? projectFile = null;
        string? sourceFolder = null;
        var dryRun = false;
        var flattenOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--project-folder" when i + 1 < args.Length:
                    projectFolder = args[++i];
                    break;
                case "--project-file" when i + 1 < args.Length:
                    projectFile = args[++i];
                    break;
                case "--source-folder" when i + 1 < args.Length:
                    sourceFolder = args[++i];
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--flatten-only":
                    flattenOnly = true;
                    break;
                default:
                    return null;
            }
        }

        return string.IsNullOrWhiteSpace(projectFolder)
            ? null
            : new ModernizeOptions(projectFolder, projectFile, sourceFolder, dryRun, flattenOnly);
    }
}
