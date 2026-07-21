namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

/// <summary>Prueft den Bericht als verbindlichen Vertrag fuer nachfolgende Werkzeuge.</summary>
public static class TrainingInventoryReportValidator
{
    public static void Validate(TrainingDataInventoryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        Require(
            report.SchemaVersion == TrainingDataInventoryReportSchema.CurrentSchemaVersion,
            $"Nicht unterstuetzte Inventar-Schemaversion: {report.SchemaVersion}");
        Require(
            report.ScannerVersion == TrainingDataInventoryReportSchema.CurrentScannerVersion,
            $"Nicht unterstuetzte Inventar-Scanner-Version: {report.ScannerVersion}");
        Require(Guid.TryParseExact(report.RunId, "N", out _), "RunId fehlt oder ist ungueltig.");
        Require(report.GeneratedUtc != default, "GeneratedUtc fehlt oder ist ungueltig.");
        Require(report.ReadOnly, "Inventarbericht muss readOnly=true sein.");
        Require(!string.IsNullOrWhiteSpace(report.KnowledgeRoot), "KnowledgeRoot fehlt.");
        Require(Path.IsPathFullyQualified(report.KnowledgeRoot), "KnowledgeRoot muss absolut sein.");
        var evalProtection = report.EvalProtection
                             ?? throw new InvalidDataException("EvalProtection fehlt.");
        var records = report.TeacherRecords
                      ?? throw new InvalidDataException("TeacherRecords fehlen.");
        var sources = report.Sources
                      ?? throw new InvalidDataException("Sources fehlen.");
        var summary = report.Summary
                      ?? throw new InvalidDataException("Summary fehlt.");
        var issues = report.Issues
                     ?? throw new InvalidDataException("Issues fehlen.");
        var searchRoots = report.SearchRoots
                          ?? throw new InvalidDataException("SearchRoots fehlen.");
        var protectedRoots = report.ProtectedRoots
                             ?? throw new InvalidDataException("ProtectedRoots fehlen.");
        var skippedDirectories = report.SkippedDirectories
                                 ?? throw new InvalidDataException("SkippedDirectories fehlen.");
        ValidateRoots(searchRoots, "SearchRoots");
        ValidateRoots(protectedRoots, "ProtectedRoots");
        ValidateRoots(skippedDirectories, "SkippedDirectories");

        ValidateRecordKeys(records);
        ValidateEvalProtection(evalProtection, records);
        ValidateEvalRoot(report.EvalSetRoot, protectedRoots, evalProtection);
        foreach (var record in records)
        {
            if (record is null)
                throw new InvalidDataException("TeacherRecords enthalten einen leeren Eintrag.");
            ValidatePath(record.RecordKey, nameof(record.FullFrame), record.FullFrame, searchRoots, protectedRoots);
            ValidatePath(record.RecordKey, nameof(record.CroppedRegion), record.CroppedRegion, searchRoots, protectedRoots);
            ValidatePath(record.RecordKey, nameof(record.YoloAnnotation), record.YoloAnnotation, searchRoots, protectedRoots);
            ValidatePath(record.RecordKey, nameof(record.Video), record.Video, searchRoots, protectedRoots);
            ValidateRecord(record);
        }

        var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source is null)
                throw new InvalidDataException("Sources enthalten einen leeren Eintrag.");
            Require(!string.IsNullOrWhiteSpace(source.Path), "Quellenpfad fehlt.");
            Require(Path.IsPathFullyQualified(source.Path), $"Quellenpfad ist nicht absolut: {source.Path}");
            Require(sourcePaths.Add(GetFullPath(source.Path)), $"Quellenpfad ist doppelt: {source.Path}");
            if (source.Role == TrainingInventorySourceRole.Current)
            {
                var expectedPath = Path.Combine(
                    report.KnowledgeRoot,
                    source.DataKind == TrainingInventoryDataKind.TeacherAnnotations
                        ? "teacher_annotations.json"
                        : "training_samples.json");
                Require(PathsEqual(source.Path, expectedPath),
                    $"Aktuelle Quelle liegt nicht am verbindlichen Pfad: {source.Path}");
            }
            if (source.Sha256 is not null)
                Require(IsSha256(source.Sha256), $"{source.Path}: Quellen-SHA-256 ist ungueltig.");
            if (source.ParseState == TrainingInventoryParseState.Parsed)
            {
                Require(IsSha256(source.Sha256), $"{source.Path}: Quellen-SHA-256 fehlt.");
                Require(source.Bytes is >= 0, $"{source.Path}: Quellengroesse fehlt.");
                Require(source.LastWriteUtc.HasValue, $"{source.Path}: Aenderungszeit fehlt.");
                Require(source.RecordCount is >= 0, $"{source.Path}: Datensatzanzahl fehlt.");
                Require(string.IsNullOrWhiteSpace(source.Error), $"{source.Path}: geparste Quelle enthaelt einen Fehler.");
            }
            else if (source.ParseState == TrainingInventoryParseState.Invalid)
            {
                Require(!string.IsNullOrWhiteSpace(source.Error), $"{source.Path}: ungueltige Quelle ohne Fehlertext.");
            }
        }

        foreach (var issue in issues)
        {
            if (issue is null)
                throw new InvalidDataException("Issues enthalten einen leeren Eintrag.");
            Require(!string.IsNullOrWhiteSpace(issue.Code), "Issue-Code fehlt.");
            Require(!string.IsNullOrWhiteSpace(issue.Message), $"{issue.Code}: Issue-Meldung fehlt.");
            if (!string.IsNullOrWhiteSpace(issue.RecordKey))
                Require(records.Any(record => record.RecordKey.Equals(issue.RecordKey, StringComparison.OrdinalIgnoreCase)),
                    $"{issue.Code}: unbekannter RecordKey {issue.RecordKey}.");
        }

        var expected = TrainingInventorySummaryBuilder.Build(records, sources);
        ValidateSummary(summary, expected);
    }

    private static void ValidateRoots(IReadOnlyList<string> roots, string field)
    {
        Require(roots.All(root => !string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root)),
            $"{field} muessen absolute, nichtleere Pfade enthalten.");
        Require(roots.Distinct(StringComparer.OrdinalIgnoreCase).Count() == roots.Count,
            $"{field} enthalten doppelte Pfade.");
    }

    private static void ValidateEvalRoot(
        string? evalSetRoot,
        IReadOnlyList<string> protectedRoots,
        TrainingInventoryEvalProtectionStatus status)
    {
        if (string.IsNullOrWhiteSpace(evalSetRoot))
        {
            Require(status.Sets.Count == 0, "Eval-Sets ohne konfigurierten EvalSetRoot.");
            return;
        }

        Require(Path.IsPathFullyQualified(evalSetRoot), "EvalSetRoot muss absolut sein.");
        Require(IsWithinAny(evalSetRoot, protectedRoots), "EvalSetRoot fehlt in den Schutzwurzeln.");
        Require(status.Sets.All(set => IsWithinAny(set.RootPath, protectedRoots)),
            "Eval-/Abnahme-Set liegt ausserhalb der konfigurierten Schutzwurzeln.");
    }

    private static void ValidateRecordKeys(IReadOnlyList<TeacherInventoryRecord> records)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (record is null)
                throw new InvalidDataException("TeacherRecords enthalten einen leeren Eintrag.");
            Require(!string.IsNullOrWhiteSpace(record.RecordKey), "Teacher-RecordKey fehlt.");
            Require(keys.Add(record.RecordKey), $"Teacher-RecordKey ist doppelt: {record.RecordKey}");
        }
    }

    private static void ValidateEvalProtection(
        TrainingInventoryEvalProtectionStatus status,
        IReadOnlyList<TeacherInventoryRecord> records)
    {
        var sets = status.Sets
                   ?? throw new InvalidDataException("EvalProtection.Sets fehlen.");
        var discoveryErrors = status.DiscoveryErrors
                              ?? throw new InvalidDataException("EvalProtection.DiscoveryErrors fehlen.");
        Require(discoveryErrors.All(error => !string.IsNullOrWhiteSpace(error)),
            "EvalProtection enthaelt eine leere Discovery-Fehlermeldung.");

        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var set in sets)
        {
            if (set is null)
                throw new InvalidDataException("EvalProtection.Sets enthalten einen leeren Eintrag.");
            Require(!string.IsNullOrWhiteSpace(set.RootPath), "Eval-Set-Root fehlt.");
            Require(Path.IsPathFullyQualified(set.RootPath), $"Eval-Set-Root ist nicht absolut: {set.RootPath}");
            Require(roots.Add(GetFullPath(set.RootPath)), $"Eval-Set-Root ist doppelt: {set.RootPath}");
            Require(set.ImageFiles >= 0, $"{set.RootPath}: negative Bildanzahl.");
            Require(set.ManifestImageHashes >= 0, $"{set.RootPath}: negative Manifestanzahl.");
            Require(set.VerifiedImageHashes >= 0, $"{set.RootPath}: negative Hashanzahl.");
            Require(set.HoldingKeys >= 0, $"{set.RootPath}: negative Haltungsanzahl.");
            Require(set.VerifiedImageHashes <= set.ManifestImageHashes,
                $"{set.RootPath}: mehr verifizierte als vorhandene Manifest-Hashes.");
            Require(set.ManifestImageHashes <= set.ImageFiles || !set.ImageHashesComplete,
                $"{set.RootPath}: vollstaendiger Hashstatus mit zusaetzlichen Manifest-Eintraegen.");
            var errors = set.Errors
                         ?? throw new InvalidDataException($"{set.RootPath}: Fehlerliste fehlt.");
            Require(errors.All(error => !string.IsNullOrWhiteSpace(error)),
                $"{set.RootPath}: leere Fehlermeldung.");
            if (errors.Count > 0)
            {
                Require(!set.ImageHashesComplete && !set.HoldingKeysComplete,
                    $"{set.RootPath}: Eval-Set mit Fehlern darf nicht vollstaendig sein.");
            }

            if (set.ImageHashesComplete)
            {
                Require(status.ImageHashCheckEnabled, $"{set.RootPath}: Hashstatus trotz deaktivierter Pruefung.");
                Require(set.ImageFiles > 0, $"{set.RootPath}: vollstaendiger Hashstatus ohne Bilder.");
                Require(set.ManifestImageHashes == set.ImageFiles,
                    $"{set.RootPath}: Manifest- und Bildanzahl stimmen nicht ueberein.");
                Require(set.VerifiedImageHashes == set.ImageFiles,
                    $"{set.RootPath}: nicht alle Bild-Hashes wurden verifiziert.");
            }

            if (set.HoldingKeysComplete)
                Require(set.HoldingKeys > 0, $"{set.RootPath}: vollstaendiger Haltungsschutz ohne Schluessel.");
        }

        if (!status.Complete)
        {
            Require(records.All(record => record.Disposition != TrainingInventoryDisposition.TrainValCandidate),
                "Train/Val-Kandidat trotz unvollstaendigem Eval-Schutz.");
            Require(records.All(record => record.EvalState != TrainingInventoryEvalState.Clean),
                "Sauberer Eval-Status trotz unvollstaendigem Eval-Schutz.");
        }
    }

    private static void ValidatePath(
        string recordKey,
        string field,
        TrainingInventoryPathReference? path,
        IReadOnlyList<string> searchRoots,
        IReadOnlyList<string> protectedRoots)
    {
        if (path is null)
            throw new InvalidDataException($"{recordKey}/{field}: Pfadangabe fehlt.");
        var candidates = path.Candidates
                         ?? throw new InvalidDataException($"{recordKey}/{field}: Kandidatenliste fehlt.");
        Require(candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count() == candidates.Count,
            $"{recordKey}/{field}: Kandidatenliste enthaelt doppelte Pfade.");

        if (path.State == TrainingInventoryPathState.Existing)
        {
            var existingPath = path.ExistingPath;
            Require(!string.IsNullOrWhiteSpace(existingPath), $"{recordKey}/{field}: ExistingPath fehlt.");
            Require(Path.IsPathFullyQualified(existingPath!), $"{recordKey}/{field}: ExistingPath ist nicht absolut.");
            Require(path.IsProtected == IsWithinAny(existingPath!, protectedRoots),
                $"{recordKey}/{field}: Schutzstatus des ExistingPath ist widerspruechlich.");
            Require(string.IsNullOrWhiteSpace(path.SuggestedPath) && candidates.Count == 0,
                $"{recordKey}/{field}: bestehender Pfad darf keine Vorschlaege tragen.");
        }
        if (path.State == TrainingInventoryPathState.SuggestedForManualReview)
        {
            var suggestedPath = path.SuggestedPath;
            Require(!string.IsNullOrWhiteSpace(suggestedPath), $"{recordKey}/{field}: SuggestedPath fehlt.");
            Require(Path.IsPathFullyQualified(suggestedPath!), $"{recordKey}/{field}: SuggestedPath ist nicht absolut.");
            Require(candidates.Count == 1, $"{recordKey}/{field}: Vorschlag muss genau einen Kandidaten haben.");
            Require(PathsEqual(candidates[0], suggestedPath!),
                $"{recordKey}/{field}: SuggestedPath stimmt nicht mit dem Kandidaten ueberein.");
            Require(IsWithinAny(suggestedPath!, searchRoots),
                $"{recordKey}/{field}: SuggestedPath liegt ausserhalb der Suchwurzeln.");
            Require(!IsWithinAny(suggestedPath!, protectedRoots) && !path.IsProtected,
                $"{recordKey}/{field}: geschuetzter Pfad darf kein Reparaturvorschlag sein.");
            Require(string.IsNullOrWhiteSpace(path.ExistingPath),
                $"{recordKey}/{field}: Vorschlag darf keinen ExistingPath tragen.");
        }
        if (path.State is TrainingInventoryPathState.Ambiguous
            or TrainingInventoryPathState.ProtectedCandidate)
        {
            Require(candidates.Count > 0, $"{recordKey}/{field}: Kandidaten fehlen.");
        }

        Require(candidates.All(candidate =>
                !string.IsNullOrWhiteSpace(candidate) && Path.IsPathFullyQualified(candidate)),
            $"{recordKey}/{field}: Kandidaten muessen absolute Pfade sein.");
        Require(candidates.All(candidate =>
                IsWithinAny(candidate, searchRoots) || IsWithinAny(candidate, protectedRoots)),
            $"{recordKey}/{field}: Kandidat liegt ausserhalb der Such- und Schutzwurzeln.");
        if (path.State == TrainingInventoryPathState.Ambiguous)
        {
            Require(candidates.Count > 1, $"{recordKey}/{field}: mehrdeutiger Pfad braucht mehrere Kandidaten.");
            Require(path.IsProtected == candidates.Any(candidate => IsWithinAny(candidate, protectedRoots)),
                $"{recordKey}/{field}: Schutzstatus der mehrdeutigen Kandidaten ist widerspruechlich.");
            Require(string.IsNullOrWhiteSpace(path.ExistingPath) && string.IsNullOrWhiteSpace(path.SuggestedPath),
                $"{recordKey}/{field}: mehrdeutiger Pfad darf keinen Einzelpfad tragen.");
            Require(path.HashState == TrainingInventoryHashState.NotApplicable,
                $"{recordKey}/{field}: mehrdeutiger Pfad darf keinen Hashstatus tragen.");
        }
        if (path.State == TrainingInventoryPathState.ProtectedCandidate)
        {
            Require(path.IsProtected, $"{recordKey}/{field}: geschuetzter Kandidat ohne Schutzstatus.");
            Require(candidates.Count == 1, $"{recordKey}/{field}: geschuetzter Kandidat muss eindeutig sein.");
            Require(candidates.All(candidate => IsWithinAny(candidate, protectedRoots)),
                $"{recordKey}/{field}: ungeschuetzter Pfad im geschuetzten Kandidatensatz.");
            Require(string.IsNullOrWhiteSpace(path.ExistingPath) && string.IsNullOrWhiteSpace(path.SuggestedPath),
                $"{recordKey}/{field}: geschuetzter Kandidat darf keinen Einzelpfad tragen.");
            Require(path.HashState == TrainingInventoryHashState.NotApplicable,
                $"{recordKey}/{field}: geschuetzter Kandidat darf keinen Hashstatus tragen.");
        }

        if (path.State is TrainingInventoryPathState.Empty
            or TrainingInventoryPathState.Missing
            or TrainingInventoryPathState.Invalid)
        {
            Require(string.IsNullOrWhiteSpace(path.ExistingPath)
                    && string.IsNullOrWhiteSpace(path.SuggestedPath)
                    && candidates.Count == 0
                    && !path.IsProtected,
                $"{recordKey}/{field}: leerer, fehlender oder ungueltiger Pfad traegt Aufloesungsdaten.");
            Require(path.HashState == TrainingInventoryHashState.NotApplicable,
                $"{recordKey}/{field}: leerer, fehlender oder ungueltiger Pfad darf keinen Hashstatus tragen.");
        }
        if (path.State == TrainingInventoryPathState.Empty)
            Require(string.IsNullOrWhiteSpace(path.StoredPath), $"{recordKey}/{field}: leerer Zustand mit gespeichertem Pfad.");
        if (path.State is not TrainingInventoryPathState.Empty)
            Require(!string.IsNullOrWhiteSpace(path.StoredPath), $"{recordKey}/{field}: gespeicherter Pfad fehlt.");

        if (path.HashState == TrainingInventoryHashState.Computed)
        {
            Require(IsSha256(path.Sha256), $"{recordKey}/{field}: gueltiger SHA-256 fehlt.");
        }
        else
        {
            Require(path.Sha256 is null, $"{recordKey}/{field}: SHA-256 ohne berechneten Hashstatus.");
        }

        if (path.HashState == TrainingInventoryHashState.ReadError)
            Require(!string.IsNullOrWhiteSpace(path.Error), $"{recordKey}/{field}: Hash-Lesefehler ohne Meldung.");
        if (path.State == TrainingInventoryPathState.Invalid)
            Require(!string.IsNullOrWhiteSpace(path.Error), $"{recordKey}/{field}: ungueltiger Pfad ohne Meldung.");
        if (path.HashState != TrainingInventoryHashState.ReadError
            && path.State != TrainingInventoryPathState.Invalid)
        {
            Require(string.IsNullOrWhiteSpace(path.Error), $"{recordKey}/{field}: unerwartete Fehlermeldung.");
        }
    }

    private static void ValidateRecord(TeacherInventoryRecord record)
    {
        var holdingCandidates = record.HoldingCandidates
                                ?? throw new InvalidDataException($"{record.RecordKey}: HoldingCandidates fehlen.");
        var reasons = record.ReasonCodes
                      ?? throw new InvalidDataException($"{record.RecordKey}: ReasonCodes fehlen.");
        var holding = new TeacherInventoryHoldingAssessment(
            record.HoldingState,
            record.SuggestedHolding,
            holdingCandidates);
        Require(holdingCandidates.All(candidate => !string.IsNullOrWhiteSpace(candidate)),
            $"{record.RecordKey}: leerer Haltungskandidat.");
        Require(holdingCandidates.Distinct(StringComparer.OrdinalIgnoreCase).Count() == holdingCandidates.Count,
            $"{record.RecordKey}: doppelte Haltungskandidaten.");

        if (record.HoldingState == TrainingInventoryHoldingState.Explicit)
        {
            Require(record.SuggestedHolding is null && holdingCandidates.Count == 0,
                $"{record.RecordKey}: explizite Haltung darf keinen Vorschlag tragen.");
        }
        else if (record.HoldingState == TrainingInventoryHoldingState.SuggestionNeedsManualReview)
        {
            Require(!string.IsNullOrWhiteSpace(record.SuggestedHolding),
                $"{record.RecordKey}: Haltungsvorschlag fehlt.");
            Require(holdingCandidates.Count == 1
                    && holdingCandidates[0].Equals(record.SuggestedHolding, StringComparison.OrdinalIgnoreCase),
                $"{record.RecordKey}: Haltungsvorschlag ist widerspruechlich.");
        }
        else if (record.HoldingState == TrainingInventoryHoldingState.Ambiguous)
        {
            Require(holdingCandidates.Count > 1, $"{record.RecordKey}: mehrdeutige Haltung ohne Kandidaten.");
            Require(string.IsNullOrWhiteSpace(record.SuggestedHolding),
                $"{record.RecordKey}: mehrdeutige Haltung darf keinen Einzelvorschlag tragen.");
        }
        else
        {
            Require(string.IsNullOrWhiteSpace(record.SuggestedHolding) && holdingCandidates.Count == 0,
                $"{record.RecordKey}: unbekannte Haltung darf keine Vorschlaege tragen.");
        }

        var expectedDisposition = TeacherInventoryPolicy.ClassifyDisposition(
            record.FullFrame,
            record.HoldingState,
            record.BoxState,
            record.EvalState);
        Require(record.Disposition == expectedDisposition,
            $"{record.RecordKey}: Disposition ist widerspruechlich.");

        if (record.FullFrame.IsProtected && record.FullFrame.Exists)
        {
            Require(record.EvalState is not TrainingInventoryEvalState.Clean
                    and not TrainingInventoryEvalState.NotChecked,
                $"{record.RecordKey}: bestehender geschuetzter Pfad hat keinen Eval-Sperrstatus.");
            Require(record.Disposition == TrainingInventoryDisposition.EvaluationLocked,
                $"{record.RecordKey}: bestehender geschuetzter Pfad ist nicht gesperrt.");
        }
        if (record.EvalState is TrainingInventoryEvalState.ProtectedPath
            or TrainingInventoryEvalState.ProtectedPathAndHolding)
        {
            Require(record.FullFrame.IsProtected,
                $"{record.RecordKey}: ProtectedPath ohne geschuetzten Pfad.");
        }
        if (record.EvalState is TrainingInventoryEvalState.ImageHash
            or TrainingInventoryEvalState.ImageHashAndHolding)
        {
            Require(record.FullFrame.HashState == TrainingInventoryHashState.Computed
                    && IsSha256(record.FullFrame.Sha256),
                $"{record.RecordKey}: ImageHash-Status ohne berechneten Bild-Hash.");
        }
        if (record.EvalState == TrainingInventoryEvalState.Clean && record.FullFrame.Exists)
        {
            Require(record.FullFrame.HashState == TrainingInventoryHashState.Computed
                    && IsSha256(record.FullFrame.Sha256),
                $"{record.RecordKey}: sauberer Eval-Status ohne berechneten Bild-Hash.");
        }

        var expectedReasons = TeacherInventoryPolicy.BuildReasonCodes(
            record.FullFrame,
            record.BoxState,
            holding,
            record.EvalState,
            record.Disposition);
        Require(reasons.SequenceEqual(expectedReasons, StringComparer.Ordinal),
            $"{record.RecordKey}: ReasonCodes sind widerspruechlich.");
    }

    private static void ValidateSummary(
        TrainingDataInventorySummary actual,
        TrainingDataInventorySummary expected)
    {
        _ = actual.Data ?? throw new InvalidDataException("summary.data fehlt.");
        _ = actual.Holdings ?? throw new InvalidDataException("summary.holdings fehlt.");
        _ = actual.Triage ?? throw new InvalidDataException("summary.triage fehlt.");
        _ = actual.Paths ?? throw new InvalidDataException("summary.paths fehlt.");
        _ = actual.Evaluation ?? throw new InvalidDataException("summary.evaluation fehlt.");
        _ = actual.Sources ?? throw new InvalidDataException("summary.sources fehlt.");
        Equal(actual.Data.TeacherRecords, expected.Data.TeacherRecords, "summary.data.teacherRecords");
        Equal(actual.Data.ExistingFullFrames, expected.Data.ExistingFullFrames, "summary.data.existingFullFrames");
        Equal(actual.Data.PositiveAreaBoxes, expected.Data.PositiveAreaBoxes, "summary.data.positiveAreaBoxes");
        Equal(actual.Data.StrictlyValidBoxes, expected.Data.StrictlyValidBoxes, "summary.data.strictlyValidBoxes");
        Equal(actual.Data.ExistingFrameAndPositiveArea, expected.Data.ExistingFrameAndPositiveArea, "summary.data.existingFrameAndPositiveArea");
        Equal(actual.Data.ExistingFrameAndStrictlyValidBox, expected.Data.ExistingFrameAndStrictlyValidBox, "summary.data.existingFrameAndStrictlyValidBox");
        Equal(actual.Holdings.Explicit, expected.Holdings.Explicit, "summary.holdings.explicit");
        Equal(actual.Holdings.NonExplicit, expected.Holdings.NonExplicit, "summary.holdings.nonExplicit");
        Equal(actual.Holdings.ExistingFramePositiveAreaExplicit, expected.Holdings.ExistingFramePositiveAreaExplicit, "summary.holdings.existingFramePositiveAreaExplicit");
        Equal(actual.Triage.TrainValCandidates, expected.Triage.TrainValCandidates, "summary.triage.trainValCandidates");
        Equal(actual.Triage.QuarantineOrigin, expected.Triage.QuarantineOrigin, "summary.triage.quarantineOrigin");
        Equal(actual.Triage.QuarantineGeometry, expected.Triage.QuarantineGeometry, "summary.triage.quarantineGeometry");
        Equal(actual.Triage.Archive, expected.Triage.Archive, "summary.triage.archive");
        Equal(actual.Triage.EvaluationLocked, expected.Triage.EvaluationLocked, "summary.triage.evaluationLocked");
        Equal(actual.Triage.EvaluationNotChecked, expected.Triage.EvaluationNotChecked, "summary.triage.evaluationNotChecked");
        Equal(actual.Paths.FullFrameSuggestions, expected.Paths.FullFrameSuggestions, "summary.paths.fullFrameSuggestions");
        Equal(actual.Paths.AmbiguousFullFrameReferences, expected.Paths.AmbiguousFullFrameReferences, "summary.paths.ambiguousFullFrameReferences");
        Equal(actual.Paths.ReadErrors, expected.Paths.ReadErrors, "summary.paths.readErrors");
        Equal(actual.Evaluation.ReservedRecords, expected.Evaluation.ReservedRecords, "summary.evaluation.reservedRecords");
        Equal(actual.Evaluation.UncheckedRecords, expected.Evaluation.UncheckedRecords, "summary.evaluation.uncheckedRecords");
        Equal(actual.Sources.Documents, expected.Sources.Documents, "summary.sources.documents");
        Equal(actual.Sources.InvalidDocuments, expected.Sources.InvalidDocuments, "summary.sources.invalidDocuments");
        Equal(actual.Triage.Total, actual.Data.TeacherRecords, "summary.triage.total");
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsWithinAny(string path, IReadOnlyList<string> roots)
        => roots.Any(root => IsWithin(path, root));

    private static bool IsWithin(string path, string root)
    {
        var fullPath = GetFullPath(path);
        var fullRoot = GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
               || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
        => GetFullPath(left).Equals(GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static string GetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException($"Ungueltiger Pfad im Inventarbericht: {path}", ex);
        }
    }

    private static void Equal(int actual, int expected, string field)
        => Require(actual == expected, $"{field} ist widerspruechlich: {actual} statt {expected}.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
