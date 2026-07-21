using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

/// <summary>Sammelt technische Probleme in einer einheitlichen Berichtsform.</summary>
internal static class TrainingInventoryIssueCollector
{
    public static void AddMissingRoots(
        IReadOnlyList<string> searchRoots,
        IReadOnlyList<string> protectedRoots,
        ICollection<TrainingInventoryIssue> issues)
    {
        foreach (var path in searchRoots.Where(path => !Directory.Exists(path)))
        {
            issues.Add(CreateWarning(
                TrainingInventoryIssueCodes.SearchRootMissing,
                "Suchordner fehlt und konnte nicht in die Dateisuche einbezogen werden.",
                path));
        }

        foreach (var path in protectedRoots.Where(path => !Directory.Exists(path)))
        {
            issues.Add(CreateWarning(
                TrainingInventoryIssueCodes.ProtectedRootMissing,
                "Geschuetzter Ordner fehlt und konnte nicht in die Dateisuche einbezogen werden.",
                path));
        }
    }

    public static void AddSkippedDirectories(
        IEnumerable<string> skippedDirectories,
        ICollection<TrainingInventoryIssue> issues)
    {
        foreach (var path in skippedDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(CreateWarning(
                TrainingInventoryIssueCodes.DirectorySkipped,
                "Ordner konnte nicht vollstaendig gelesen werden.",
                path));
        }
    }

    public static void AddSource(
        TrainingInventorySourceDocument source,
        ICollection<TrainingInventoryIssue> issues)
    {
        if (source.ParseState == TrainingInventoryParseState.Invalid)
        {
            issues.Add(new TrainingInventoryIssue
            {
                Severity = source.Role == TrainingInventorySourceRole.Current
                    ? TrainingInventoryIssueSeverity.Error
                    : TrainingInventoryIssueSeverity.Warning,
                Code = TrainingInventoryIssueCodes.SourceInvalid,
                Message = source.Error ?? "JSON-Quelle ist ungueltig.",
                Path = source.Path
            });
        }
        else if (source.ParseState == TrainingInventoryParseState.Missing)
        {
            issues.Add(CreateWarning(
                TrainingInventoryIssueCodes.SourceMissing,
                "Aktuelle Quelldatei fehlt.",
                source.Path));
        }
    }

    public static void AddEvalProtection(
        string? evalSetRoot,
        TrainingInventoryEvalProtectionStatus status,
        ICollection<TrainingInventoryIssue> issues)
    {
        if (status.Complete)
            return;

        var missingParts = new List<string>();
        if (!status.SetsFound)
            missingParts.Add("kein freigegebenes Eval-Set gefunden");
        if (!status.DiscoveryComplete)
            missingParts.Add("Eval-Set-Suche unvollstaendig");
        if (!status.ImageHashesAvailable)
        {
            missingParts.Add(status.ImageHashCheckEnabled
                ? "Bild-Hashes unvollstaendig"
                : "Bild-Hashvergleich bewusst deaktiviert");
        }
        if (!status.HoldingKeysAvailable)
            missingParts.Add("Haltungs-Schluessel");

        var incompleteSets = status.Sets.Count(set => !set.Complete);
        if (incompleteSets > 0)
            missingParts.Add($"{incompleteSets} von {status.Sets.Count} Eval-Sets unvollstaendig");

        issues.Add(new TrainingInventoryIssue
        {
            Severity = status.ImageHashCheckEnabled
                ? TrainingInventoryIssueSeverity.Error
                : TrainingInventoryIssueSeverity.Warning,
            Code = status.ImageHashCheckEnabled
                ? TrainingInventoryIssueCodes.EvalProtectionUnavailable
                : TrainingInventoryIssueCodes.EvalHashCheckDisabled,
            Message = $"Eval-Schutz unvollstaendig ({string.Join(", ", missingParts)}). "
                      + "Trainingsfreigabe bleibt gesperrt.",
            Path = evalSetRoot
        });
    }

    public static void AddRecord(
        TeacherInventoryRecord record,
        ICollection<TrainingInventoryIssue> issues)
    {
        AddPath(record.RecordKey, "FullFramePath", record.FullFrame, issues);
        AddPath(record.RecordKey, "CroppedRegionPath", record.CroppedRegion, issues);
        AddPath(record.RecordKey, "YoloAnnotationPath", record.YoloAnnotation, issues);
        AddPath(record.RecordKey, "VideoPath", record.Video, issues);
    }

    private static void AddPath(
        string recordKey,
        string field,
        TrainingInventoryPathReference path,
        ICollection<TrainingInventoryIssue> issues)
    {
        if (path.State == TrainingInventoryPathState.Invalid)
        {
            issues.Add(new TrainingInventoryIssue
            {
                Severity = TrainingInventoryIssueSeverity.Error,
                Code = TrainingInventoryIssueCodes.PathInvalid,
                Message = $"{field} ist ungueltig oder nicht lesbar: {path.Error}",
                Path = path.StoredPath,
                RecordKey = recordKey
            });
        }
        else if (path.HashState == TrainingInventoryHashState.ReadError)
        {
            issues.Add(new TrainingInventoryIssue
            {
                Severity = TrainingInventoryIssueSeverity.Error,
                Code = TrainingInventoryIssueCodes.AssetHashReadError,
                Message = $"{field} konnte nicht gehasht werden: {path.Error}",
                Path = path.ExistingPath ?? path.SuggestedPath,
                RecordKey = recordKey
            });
        }
    }

    private static TrainingInventoryIssue CreateWarning(
        string code,
        string message,
        string? path)
        => new()
        {
            Severity = TrainingInventoryIssueSeverity.Warning,
            Code = code,
            Message = message,
            Path = path
        };
}
