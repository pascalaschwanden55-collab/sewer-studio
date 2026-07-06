using AuswertungPro.Next.Infrastructure.Import;

internal sealed record ModernizerFlattenContext(
    string HoldingRoot,
    string ProjectFolder,
    string HoldingName,
    string DateStamp,
    Dictionary<string, string> PathMap,
    bool DryRun,
    ModernizeReport Report)
{
    public string MoveRoot => Path.Combine(ProjectFolder, ProjectStructure.HaltungenVerteilt);
}
