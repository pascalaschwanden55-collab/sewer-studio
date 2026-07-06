internal static class ProjectModernizerExitCodeResolver
{
    public static int Resolve(ModernizeReport report)
    {
        if (report.CopyErrors > 0)
            return ProjectModernizerExitCodes.CopyErrors;

        return report.UnresolvedPaths == 0
            ? ProjectModernizerExitCodes.Success
            : ProjectModernizerExitCodes.UnresolvedPaths;
    }
}
