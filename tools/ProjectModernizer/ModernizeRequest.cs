internal sealed record ModernizeRequest(
    string ProjectFolder,
    string ProjectFile,
    string? SourceFolder,
    bool DryRun,
    bool FlattenOnly);
