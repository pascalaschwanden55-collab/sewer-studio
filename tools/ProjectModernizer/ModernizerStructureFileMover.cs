internal static class ModernizerStructureFileMover
{
    public static string? MoveOrCopyStructureFile(
        string source,
        string target,
        string moveRoot,
        bool dryRun,
        ModernizeReport report,
        StructureMoveKind kind)
    {
        try
        {
            source = Path.GetFullPath(source);
            target = Path.GetFullPath(target);
            _ = moveRoot;
            if (ModernizerFileSystem.SameFullPath(source, target))
                return target;

            if (File.Exists(target))
            {
                if (ModernizerFileSystem.SameFileContent(source, target))
                {
                    report.ReusedFiles++;
                    CountStructureMove(report, kind);
                    return target;
                }

                target = ModernizerFileSystem.BuildCollisionSafePath(target);
            }

            if (!dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: false);
            }

            CountStructureMove(report, kind);
            return target;
        }
        catch (Exception ex)
        {
            report.CopyErrors++;
            report.Messages.Add($"Strukturfehler {source}: {ex.Message}");
            return null;
        }
    }

    private static void CountStructureMove(ModernizeReport report, StructureMoveKind kind)
    {
        if (kind == StructureMoveKind.CentralPhoto)
            report.CentralPhotos++;
        else
            report.FlattenedFiles++;
    }
}
