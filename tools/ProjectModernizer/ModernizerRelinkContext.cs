internal sealed record ModernizerRelinkContext(
    string Root,
    string ProjectFolder,
    IReadOnlyDictionary<string, List<string>> ExternalFiles,
    bool DryRun,
    ModernizeReport Report,
    FileCopyKind CopyKind);
