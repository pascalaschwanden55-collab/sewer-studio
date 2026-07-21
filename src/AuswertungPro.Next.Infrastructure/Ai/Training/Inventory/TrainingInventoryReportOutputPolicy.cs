namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

/// <summary>
/// Schuetzt Trainingsquellen vor einem versehentlichen Ueberschreiben durch Inventarberichte.
/// Die reine Zielpruefung kann vor dem aufwendigen Inventarlauf ausgefuehrt werden.
/// </summary>
public static class TrainingInventoryReportOutputPolicy
{
    public static TrainingInventoryReportOutputPaths ValidateTarget(
        string outputPath,
        string knowledgeRoot,
        IEnumerable<string> searchRoots,
        IEnumerable<string> protectedRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRoot);
        ArgumentNullException.ThrowIfNull(searchRoots);
        ArgumentNullException.ThrowIfNull(protectedRoots);

        var output = Path.GetFullPath(outputPath);
        var root = Path.GetFullPath(knowledgeRoot);
        var normalizedSearchRoots = searchRoots.Select(Path.GetFullPath).ToArray();
        var normalizedProtectedRoots = protectedRoots.Select(Path.GetFullPath).ToArray();
        foreach (var inputRoot in normalizedSearchRoots.Concat(normalizedProtectedRoots))
        {
            var reparsePoint = TrainingInventoryPaths.FindReparsePoint(inputRoot);
            if (reparsePoint is not null)
            {
                throw new InvalidOperationException(
                    $"Such- und Schutzwurzeln duerfen keine Verknuepfung oder Junction enthalten: {reparsePoint}");
            }
        }
        var paths = new TrainingInventoryReportOutputPaths(
            output,
            output + ".sha256",
            root,
            normalizedSearchRoots,
            normalizedProtectedRoots);
        if (!Path.GetExtension(output).Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Der Bericht muss die Endung .json haben.");

        var reportRoot = Path.Combine(root, "training", "reports");
        if (WriteTargets(paths).Any(path => !TrainingInventoryPaths.IsWithin(path, reportRoot)))
        {
            throw new InvalidOperationException(
                "Der Bericht darf nur unter <root>\\training\\reports gespeichert werden.");
        }

        var protectedInputs = normalizedSearchRoots.Concat(normalizedProtectedRoots).ToArray();
        if (WriteTargets(paths).Any(target =>
                protectedInputs.Any(input => TrainingInventoryPaths.IsWithin(target, input))))
        {
            throw new InvalidOperationException(
                "Der Bericht darf nicht in einem Bild-, Label- oder Schutzordner liegen.");
        }

        RejectReparsePoints(paths);

        return paths;
    }

    public static void EnsureNoSourceCollision(
        TrainingInventoryReportOutputPaths outputPaths,
        IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(outputPaths);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        // Alle Zielregeln unmittelbar vor dem Schreiben erneut pruefen.
        var revalidated = ValidateTarget(
            outputPaths.ReportPath,
            outputPaths.KnowledgeRoot,
            outputPaths.SearchRoots,
            outputPaths.ProtectedRoots);
        if (!PathsEqual(revalidated.ReportPath, outputPaths.ReportPath)
            || !PathsEqual(revalidated.Sha256Path, outputPaths.Sha256Path))
        {
            throw new InvalidOperationException("Berichtsziel hat sich seit der Vorpruefung geaendert.");
        }

        var writeTargets = WriteTargets(outputPaths).ToArray();
        if (sourcePaths.Any(source => writeTargets.Any(target => PathsEqual(source, target))))
            throw new InvalidOperationException("Der Bericht darf keine Trainingsquelle ueberschreiben.");
    }

    private static IEnumerable<string> WriteTargets(TrainingInventoryReportOutputPaths paths)
    {
        yield return paths.ReportPath;
        yield return paths.Sha256Path;
        yield return paths.ReportPath + ".bak";
        yield return paths.Sha256Path + ".bak";
    }

    private static bool PathsEqual(string left, string right)
        => Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static void RejectReparsePoints(TrainingInventoryReportOutputPaths paths)
    {
        foreach (var target in WriteTargets(paths))
        {
            var reparsePoint = TrainingInventoryPaths.FindReparsePoint(target);
            if (reparsePoint is not null)
            {
                throw new InvalidOperationException(
                    $"Der Berichtspfad darf keine Verknuepfung oder Junction enthalten: {reparsePoint}");
            }
        }
    }

}

public sealed class TrainingInventoryReportOutputPaths
{
    internal TrainingInventoryReportOutputPaths(
        string reportPath,
        string sha256Path,
        string knowledgeRoot,
        IReadOnlyList<string> searchRoots,
        IReadOnlyList<string> protectedRoots)
    {
        ReportPath = reportPath;
        Sha256Path = sha256Path;
        KnowledgeRoot = knowledgeRoot;
        SearchRoots = searchRoots;
        ProtectedRoots = protectedRoots;
    }

    public string ReportPath { get; }
    public string Sha256Path { get; }
    internal string KnowledgeRoot { get; }
    internal IReadOnlyList<string> SearchRoots { get; }
    internal IReadOnlyList<string> ProtectedRoots { get; }
}
