using AuswertungPro.Next.Infrastructure.Projects;

internal static class ProjectModernizerRunner
{
    public static int Run(string[] args)
        => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var options = ModernizeOptions.Parse(args);
        if (options is null)
        {
            PrintUsage(output);
            return ProjectModernizerExitCodes.Usage;
        }

        var repo = new JsonProjectRepository();
        var loaded = ModernizerProjectLoader.Load(options, repo);
        if (!loaded.Ok || loaded.Value is null)
        {
            error.WriteLine(loaded.ErrorMessage);
            return int.TryParse(loaded.ErrorCode, out var code)
                ? code
                : ProjectModernizerExitCodes.LoadFailed;
        }

        var project = loaded.Value.Project;
        var request = loaded.Value.Request;
        var report = ModernizerWorkflow.Run(project, request);

        if (!request.DryRun)
        {
            var save = ModernizedProjectSaver.Save(project, request, repo, report);
            if (!save.Ok)
            {
                error.WriteLine(save.ErrorMessage);
                return ProjectModernizerExitCodes.SaveFailed;
            }
        }

        ModernizeReportWriter.Print(output, request.ProjectFolder, request.ProjectFile, request.SourceFolder, report);
        return ProjectModernizerExitCodeResolver.Resolve(report);
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("ProjectModernizer");
        writer.WriteLine("  --project-folder <ordner> [--project-file <json>] [--source-folder <export>] [--dry-run] [--flatten-only]");
    }

}
