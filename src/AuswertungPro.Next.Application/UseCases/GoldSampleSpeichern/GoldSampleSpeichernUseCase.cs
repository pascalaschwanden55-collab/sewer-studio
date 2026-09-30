using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.UseCases.GoldSampleSpeichern;

/// <summary>
/// Geschuetztes Speichern einer persoenlich bestaetigten Box als Goldsample.
/// Phasen in fester Reihenfolge:
/// 1. Eingaben pruefen (Bearbeiter, Beschreibung, Katalogcode),
/// 2. Herkunft pruefen (Bestandsversion, persoenliche Herkunft, Korrektur),
/// 3. Bild binden und schuetzen (gebundener Bildstand, Eval-Schutz, inhaltsadressierte Goldkopie),
/// 4. Sample dauerhaft speichern (Maske, Gold-Gate, Neuanlage/Ersatz),
/// 5. KB-Nachtrag, 6. Teacher-Nachlauf, 7. gemeinsames Ergebnis.
/// Bis einschliesslich Phase 4 fuehrt jeder Fehler zu "nicht gespeichert"; ein Abbruch
/// wird weitergeworfen. Ab der dauerhaften Speicherung gibt es nur noch gespeicherte
/// Ergebnisse, Fehler im Nachlauf erscheinen als Warnung.
/// </summary>
public sealed class GoldSampleSpeichernUseCase
{
    private readonly ITrainingSampleStore _sampleStore;
    private readonly ITrainingFrameStore _frameStore;
    private readonly Func<string?> _resolveGoldFramesDir;
    private readonly Func<string, byte[]> _readFileBytes;
    private readonly Func<string?> _resolveEvalSetRoot;
    private readonly Func<string?, GoldSampleEvalSchutz> _loadEvalSchutz;
    private readonly Func<string, bool> _isCodeKnown;
    private readonly Func<string, string?> _codeLabelLookup;
    private readonly GoldSampleMaskenPruefung _pruefeMaske;
    private readonly GoldSampleAblage _ablage;
    private readonly GoldSampleNachlauf _nachlauf;

    public GoldSampleSpeichernUseCase(
        ITrainingSampleStore sampleStore,
        ITrainingFrameStore frameStore,
        Func<string?> resolveGoldFramesDir,
        IKnowledgeBaseIndexer kbIndexer,
        ITeacherAnnotationStore teacherStore,
        IVsaYoloClassMapStore teacherClassMap,
        Func<ITrainingAnnotationExportService> createExportService,
        Func<string, byte[]> readFileBytes,
        Func<string?> resolveEvalSetRoot,
        Func<string?, GoldSampleEvalSchutz> loadEvalSchutz,
        Func<string, bool> isCodeKnown,
        Func<string, string?> codeLabelLookup,
        GoldSampleMaskenPruefung pruefeMaske)
    {
        _sampleStore = sampleStore;
        _frameStore = frameStore;
        _resolveGoldFramesDir = resolveGoldFramesDir;
        _readFileBytes = readFileBytes;
        _resolveEvalSetRoot = resolveEvalSetRoot;
        _loadEvalSchutz = loadEvalSchutz;
        _isCodeKnown = isCodeKnown;
        _codeLabelLookup = codeLabelLookup;
        _pruefeMaske = pruefeMaske;
        _ablage = new GoldSampleAblage(sampleStore, kbIndexer, teacherStore);
        _nachlauf = new GoldSampleNachlauf(
            sampleStore, kbIndexer, teacherStore, teacherClassMap, createExportService);
    }

    public async Task<WorkbenchSaveResult> SaveAsync(
        GoldSampleSpeichernAnfrage anfrage,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        var item = anfrage.Item;
        var box = anfrage.Box;
        var decision = anfrage.Decision;

        // 1) Eingaben pruefen (VOR jedem Schreiben und vor dem Eval-Schutz).
        var beschreibung = decision.Beschreibung?.Trim() ?? string.Empty;
        var confirmedByUser = decision.ConfirmedByUser?.Trim() ?? string.Empty;
        var finalCode = GoldSampleAufbau.NormalizeCode(decision.VsaCode);
        if (PruefeEingaben(decision, beschreibung, confirmedByUser, finalCode) is { } eingabeAblehnung)
            return eingabeAblehnung;

        // 2) Herkunft pruefen: Bestandsversion, zugelassene Herkunft, Korrektur/MatchLevel.
        var (herkunft, herkunftsAblehnung) = await GoldSampleHerkunft
            .BestimmeAsync(_sampleStore, item, decision, finalCode)
            .ConfigureAwait(false);
        if (herkunftsAblehnung is not null)
            return herkunftsAblehnung;

        // 3) Bild binden und schuetzen. Der Eval-Schutz folgt auf die Bindung, weil er
        //    beim gebundenen Bildstand genau dieselben Bytes pruefen muss.
        var (imageSnapshot, bindungsAblehnung) = BindeBildstand(item, anfrage.ImageSnapshot);
        if (bindungsAblehnung is not null)
            return bindungsAblehnung;
        var (snapshotBytes, evalAblehnung) = PruefeEvalSchutz(item, imageSnapshot);
        if (evalAblehnung is not null)
            return evalAblehnung;

        // Stabile Objekt-ID: ein geladener Bestandssatz (z. B. aus 'Unvollstaendige Goldframes')
        // behaelt seine SampleId — bei gleichem Code als Ergaenzung, bei geaendertem Code als
        // Ersatz inkl. KB-/Teacher-Bereinigung. So entsteht bei einer Codekorrektur kein
        // zweiter Datensatz.
        var sampleId = herkunft!.RepariertBestand
            ? item.ExistingSampleId!
            : $"wb_{Guid.NewGuid():N}"[..15];
        var (storedImage, imageRejection) = await StoreGoldImageAsync(
                item, finalCode, snapshotBytes, imageSnapshot, ct)
            .ConfigureAwait(false);
        if (imageRejection is not null)
            return imageRejection;

        // 4) Sample dauerhaft speichern (Entwurf oder Gold).
        var (gespeichert, speicherAblehnung) = await SpeichereDauerhaftAsync(
                anfrage, herkunft, sampleId, finalCode, beschreibung, confirmedByUser, storedImage!, ct)
            .ConfigureAwait(false);
        if (speicherAblehnung is not null)
            return speicherAblehnung;

        // ── Grenze: Das Sample ist ab hier dauerhaft gespeichert. ─────────────────
        // Ab hier entsteht nur noch ein Ergebnis mit Saved=true; Fehler werden Warnungen.
        if (!gespeichert!.GoldApproved)
            return EntwurfErgebnis(gespeichert);

        // 5) KB-Nachtrag. 6) Teacher-Nachlauf (auch nach einem KB-Fehler, unabhaengiger Schritt).
        var (kbState, kbWarning) = await _nachlauf.RecordKbIndexAsync(gespeichert, ct).ConfigureAwait(false);
        var (teacherId, teacherWarning) = await _nachlauf.RecordTeacherCandidateAsync(
                gespeichert, item, box, decision, finalCode, beschreibung, ct)
            .ConfigureAwait(false);

        // 7) Gemeinsames Ergebnis: Ersetz-, KB- und Teacher-Warnung gemeinsam sichtbar.
        return GoldErgebnis(gespeichert, kbState, kbWarning, teacherId, teacherWarning);
    }

    private WorkbenchSaveResult? PruefeEingaben(
        WorkbenchDecision decision,
        string beschreibung,
        string confirmedByUser,
        string finalCode)
    {
        if (confirmedByUser.Length == 0)
        {
            return GoldSampleAufbau.Rejected(
                "Persönliche Bestätigung fehlt. Ohne Bearbeiter wird kein Goldsample gespeichert.");
        }
        if (beschreibung.Length < 10)
            return GoldSampleAufbau.Rejected("Beschreibung zu kurz (mindestens 10 Zeichen).");
        if (GoldDescriptionPolicy.IsPlaceholder(beschreibung))
            return GoldSampleAufbau.Rejected(
                "Bitte die Platzhalter-Beschreibung ersetzen (Lage und Ausmass konkret angeben).");
        if (!_isCodeKnown(finalCode))
            return GoldSampleAufbau.Rejected($"Unbekannter VSA-Code '{decision.VsaCode}'.");
        return null;
    }

    /// <summary>
    /// Fuer gebundene Qualitaetspruefungen werden genau die beim Laden geprueften Bildbytes
    /// als Snapshot verwendet. Damit koennen weder ein Dateiaustausch noch ein
    /// Schreib-/Lese-Rennen die alte Maske mit einem neuen Bild verbinden.
    /// </summary>
    private (WorkbenchImageSnapshot? Snapshot, WorkbenchSaveResult? Ablehnung) BindeBildstand(
        WorkbenchItem item,
        WorkbenchImageSnapshot? imageSnapshot)
    {
        if (string.IsNullOrWhiteSpace(item.ExpectedImageSha256))
            return (imageSnapshot, null);

        try
        {
            imageSnapshot ??= WorkbenchImageSnapshot.Create(
                _readFileBytes(item.FramePath),
                Path.GetExtension(item.FramePath));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, GoldSampleAufbau.Rejected(
                $"Gebundener Bildstand konnte nicht sicher gelesen werden: {UserError.DescribeAndReport(ex, "Gebundenen Bildstand lesen")}"));
        }

        if (!string.Equals(
                imageSnapshot.Sha256,
                item.ExpectedImageSha256.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return (null, GoldSampleAufbau.Rejected(
                "Das Bild wurde seit dem Laden der Goldprüfung geändert. Bitte die Goldprüfung neu laden."));
        }

        return (imageSnapshot, null);
    }

    /// <summary>
    /// Eval-Schutz (hart): kein eingefrorenes Mess-Bild darf ins Training/Retrieval.
    /// Beim Foto-Assistenten ist der Snapshot genau eine Arbeitskopie des beim Segmentieren
    /// gebundenen Originals; dieselben Bytes gehen danach an StoreBytesAsync, der
    /// veraenderbare Quellpfad wird nicht erneut gelesen.
    /// </summary>
    private (byte[]? SnapshotBytes, WorkbenchSaveResult? Ablehnung) PruefeEvalSchutz(
        WorkbenchItem item,
        WorkbenchImageSnapshot? imageSnapshot)
    {
        var root = _resolveEvalSetRoot();
        GoldSampleEvalSchutz evalSets;
        try
        {
            evalSets = _loadEvalSchutz(root);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, GoldSampleAufbau.Rejected(
                $"Eval-Schutz nicht verfügbar: {UserError.DescribeAndReport(ex, "Eval-Schutz laden")}"));
        }

        var snapshotBytes = imageSnapshot?.CopyImageBytes();
        var verdict = snapshotBytes is null
            ? EvalContaminationGuard.ClassifyForExport(
                evalSets.ImageHashes,
                evalSets.HaltungKeys,
                item.FramePath,
                item.CaseId)
            : EvalContaminationGuard.ClassifyForExport(
                evalSets.ImageHashes,
                evalSets.HaltungKeys,
                snapshotBytes,
                item.CaseId);
        if (verdict != EvalContaminationGuard.ExportContaminationResult.Clean)
        {
            return (null, GoldSampleAufbau.Rejected(
                $"Eval-Schutz: Bild gehört zum eingefrorenen Mess-Set ({verdict}). Nicht speicherbar."));
        }

        return (snapshotBytes, null);
    }

    private sealed record StoredGoldImage(string FramePath, string Sha256);

    /// <summary>
    /// Legt das angenommene Bild unveraendert inhaltsadressiert unter
    /// gold_frames\&lt;Hauptcode - Klartext&gt; ab und bindet seinen Hash an den gespeicherten
    /// Goldpfad. Scheitert die sichere Kopie, wird nichts gespeichert.
    /// </summary>
    private async Task<(StoredGoldImage? Image, WorkbenchSaveResult? Rejection)> StoreGoldImageAsync(
        WorkbenchItem item,
        string finalCode,
        byte[]? snapshotBytes,
        WorkbenchImageSnapshot? imageSnapshot,
        CancellationToken ct)
    {
        string? storedFramePath;
        try
        {
            var goldFramesRoot = _resolveGoldFramesDir();
            var codeFolder = PersonalGoldMainCodeCatalog.FormatFolderName(
                finalCode,
                _codeLabelLookup);
            var codeFramesDir = string.IsNullOrWhiteSpace(goldFramesRoot)
                ? goldFramesRoot
                : Path.Combine(goldFramesRoot, codeFolder);
            storedFramePath = snapshotBytes is null
                ? await _frameStore
                    .StoreExistingAsync(item.FramePath, codeFramesDir, ct)
                    .ConfigureAwait(false)
                : await _frameStore
                    .StoreBytesAsync(snapshotBytes, imageSnapshot!.Extension, codeFramesDir, ct)
                    .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, GoldSampleAufbau.Rejected($"Goldbild konnte nicht sicher gespeichert werden: {UserError.DescribeAndReport(ex, "Goldbild speichern")}"));
        }
        if (string.IsNullOrWhiteSpace(storedFramePath))
        {
            return (null, GoldSampleAufbau.Rejected("Goldbild konnte nicht sicher gespeichert werden."));
        }

        string storedImageSha256;
        try
        {
            storedImageSha256 = imageSnapshot?.Sha256
                ?? Convert.ToHexStringLower(SHA256.HashData(_readFileBytes(storedFramePath)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, GoldSampleAufbau.Rejected(
                $"Goldbild konnte nach dem Speichern nicht bytegenau geprüft werden: {UserError.DescribeAndReport(ex, "Goldbild nachprüfen")}"));
        }

        return (new StoredGoldImage(storedFramePath, storedImageSha256), null);
    }

    /// <summary>
    /// Entwurf oder Gold entscheiden und das Sample dauerhaft speichern. Vollstaendig ist ein
    /// Fund nur mit gepruefter SAM-Maske UND bestandener persoenlicher Gold-Policy; sonst
    /// bleibt er ein Entwurf (Status=Draft, Gelb). Liefert den Zustand
    /// <see cref="DauerhaftGespeichertesGoldSample"/> oder eine Ablehnung, nie beides.
    /// </summary>
    private async Task<(DauerhaftGespeichertesGoldSample? Gespeichert, WorkbenchSaveResult? Ablehnung)> SpeichereDauerhaftAsync(
        GoldSampleSpeichernAnfrage anfrage,
        GoldSampleHerkunft herkunft,
        string sampleId,
        string finalCode,
        string beschreibung,
        string confirmedByUser,
        StoredGoldImage storedImage,
        CancellationToken ct)
    {
        var goldMask = _pruefeMaske(anfrage.Segmentation, anfrage.Box, storedImage.FramePath);
        var maskValid = goldMask.IsValid;

        var sample = GoldSampleAufbau.Erzeuge(
            anfrage.Item, anfrage.Box, anfrage.Decision, herkunft, sampleId,
            finalCode, beschreibung, confirmedByUser, storedImage.FramePath, maskValid);
        goldMask.ApplyTo(sample);

        // Letzte zentrale Gold-Schranke: Caller-Flags allein duerfen keinen
        // unvollstaendigen Fund zu KB oder Teacher durchreichen.
        var goldEligibility = maskValid
            ? ManualGoldTrainingPolicy.EvaluateForExport(sample, confirmedByUser)
            : new TrainingEligibilityResult(
                false,
                ManualGoldTrainingPolicy.GoldGeometryRequiredReason);
        var goldApproved = maskValid && goldEligibility.IsEligible;
        if (!goldApproved)
        {
            sample.Status = TrainingSampleStatus.Draft;
            sample.QualityGateLevel = "Yellow";
        }

        var (replaceWarning, sampleRejection) = await _ablage.PersistAsync(
                anfrage.Item, sample, herkunft.RepariertBestand, herkunft.CodeChanged, ct)
            .ConfigureAwait(false);
        if (sampleRejection is not null)
            return (null, sampleRejection);

        return (new DauerhaftGespeichertesGoldSample(
            sample, sampleId, storedImage.FramePath, storedImage.Sha256, goldApproved, replaceWarning), null);
    }

    /// <summary>
    /// Entwurf ohne gepruefte Maske: gespeichert, aber NICHT Gold. KB-Index (KbIndexState
    /// bleibt Pending) und Teacher-Kandidat werden nicht geschrieben. Das Sample landet in
    /// 'Unvollstaendige Goldframes'; beim Nachruesten mit Maske laeuft der volle Gold-Pfad.
    /// </summary>
    private static WorkbenchSaveResult EntwurfErgebnis(DauerhaftGespeichertesGoldSample gespeichert)
        => new(
            true,
            GoldSampleAufbau.CombineWarnings(
                "Entwurf gespeichert: ohne geprüfte SAM-Maske kein Goldsample. Das Sample landet in 'Unvollständige Goldframes' und kann dort mit Maske nachgerüstet werden.",
                gespeichert.ErsatzWarnung),
            gespeichert.SampleId,
            "Entwurf",
            null,
            StoredImageSha256: gespeichert.StoredImageSha256,
            StoredConfirmedAtUtc: gespeichert.Sample.ConfirmedAtUtc is { } draftConfirmedAtUtc
                ? GoldSampleAufbau.ToUtc(draftConfirmedAtUtc) : null);

    private static WorkbenchSaveResult GoldErgebnis(
        DauerhaftGespeichertesGoldSample gespeichert,
        string kbState,
        string? kbWarning,
        string? teacherId,
        string? teacherWarning)
        => new(
            true,
            GoldSampleAufbau.CombineWarnings(gespeichert.ErsatzWarnung, kbWarning, teacherWarning),
            gespeichert.SampleId,
            kbState,
            teacherId,
            GoldApproved: true,
            StoredImageSha256: gespeichert.StoredImageSha256,
            StoredConfirmedAtUtc: gespeichert.Sample.ConfirmedAtUtc is { } goldConfirmedAtUtc
                ? GoldSampleAufbau.ToUtc(goldConfirmedAtUtc) : null);
}
