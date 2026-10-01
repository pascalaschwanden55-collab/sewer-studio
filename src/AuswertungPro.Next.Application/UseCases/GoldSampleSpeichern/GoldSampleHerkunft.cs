using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.UseCases.GoldSampleSpeichern;

/// <summary>
/// Phase "Herkunft pruefen": bindet ein zu reparierendes Bestandssample (genau ein Treffer,
/// unveraenderter Bestaetigungsstand), laesst nur persoenliche Herkunft (PdfPhoto mit
/// gueltiger Pruefspur oder ManualCoding) zu und leitet Korrektur und MatchLevel ab.
/// Diese Phase liest hoechstens den Bestand und schreibt nichts.
/// </summary>
internal sealed record GoldSampleHerkunft(
    TrainingSample? Bestand,
    bool RepariertBestand,
    string? SourceType,
    string SourceNote,
    string? SourceReferenceCode,
    string? SourceReferenceDescription,
    bool CodeChanged,
    bool WasCorrected,
    string MatchLevel)
{
    public static async Task<(GoldSampleHerkunft? Herkunft, WorkbenchSaveResult? Ablehnung)> BestimmeAsync(
        ITrainingSampleStore sampleStore,
        WorkbenchItem item,
        WorkbenchDecision decision,
        string finalCode)
    {
        var repairsExistingSample = !string.IsNullOrWhiteSpace(item.ExistingSampleId);
        TrainingSample? existingSample = null;
        if (repairsExistingSample)
        {
            try
            {
                var matches = (await sampleStore.LoadAsync().ConfigureAwait(false))
                    .Where(sample => string.Equals(
                        sample.SampleId,
                        item.ExistingSampleId,
                        StringComparison.Ordinal))
                    .ToList();
                if (matches.Count != 1)
                {
                    return (null, GoldSampleAufbau.Rejected(
                        matches.Count == 0
                            ? "Goldsample wurde nicht gespeichert: Der zu reparierende Bestandseintrag wurde nicht gefunden."
                            : "Die Sample-ID ist im Bestand nicht eindeutig. Es wurde nichts gespeichert."));
                }

                existingSample = matches[0];
                if (item.ExpectedConfirmedAtUtc.HasValue
                    && (!existingSample.ConfirmedAtUtc.HasValue
                        || GoldSampleAufbau.ToUtc(existingSample.ConfirmedAtUtc.Value)
                           != item.ExpectedConfirmedAtUtc.Value.ToUniversalTime()))
                {
                    return (null, GoldSampleAufbau.Rejected(
                        "Goldsample wurde inzwischen in einem anderen Arbeitsablauf geändert. Bitte die Goldprüfung neu laden."));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (null, GoldSampleAufbau.Rejected(
                    $"Das zu reparierende Goldsample konnte nicht sicher gelesen werden: {UserError.DescribeAndReport(ex, "Goldsample zur Reparatur lesen")}"));
            }
        }

        var sourceType = existingSample is null
            ? (item.SourceSuggestion is null
                ? SourceTypeNames.ManualCoding
                : SourceTypeNames.PdfPhoto)
            : existingSample.SourceType;
        var sourceNote = existingSample is null
            ? GoldSampleAufbau.BuildSourceNote(item.SourceSuggestion)
            : existingSample.Notes ?? string.Empty;
        var sourceReferenceCode = existingSample is null
            ? item.SourceSuggestion?.VsaCode?.Trim()
            : existingSample.SourceReferenceCode;
        var sourceReferenceDescription = existingSample is null
            ? item.SourceSuggestion?.Beschreibung?.Trim()
            : existingSample.SourceReferenceDescription;
        var isPdfPhoto = string.Equals(
            sourceType,
            SourceTypeNames.PdfPhoto,
            StringComparison.OrdinalIgnoreCase);
        var isManualCoding = string.Equals(
            sourceType,
            SourceTypeNames.ManualCoding,
            StringComparison.OrdinalIgnoreCase);
        if (!isPdfPhoto && !isManualCoding)
        {
            return (null, GoldSampleAufbau.Rejected(
                "Die gespeicherte Herkunft ist nicht als persönliches Gold zugelassen. Es wurde nichts gespeichert."));
        }
        if (isPdfPhoto
            && (!PdfGoldProvenancePolicy.IsValid(sourceNote)
                || string.IsNullOrWhiteSpace(sourceReferenceCode)
                || string.IsNullOrWhiteSpace(sourceReferenceDescription)))
        {
            return (null, GoldSampleAufbau.Rejected(
                "PDF-Goldsample kann nicht gespeichert werden: Die Operateurreferenz oder PDF-Prüfspur ist unvollständig oder ungültig."));
        }
        // Bewusste Doppelsicherung, greift heute nie: Ohne Bestand und mit PDF-Vorschlag
        // setzt die Ableitung oben sourceType immer auf PdfPhoto. Sie schuetzt die
        // Trainingsdaten, falls diese Ableitung spaeter geaendert wird (z. B. Herkunft aus
        // dem Vorschlag selbst): Ein Fund mit PDF-Vorschlag darf dann nicht ohne die
        // PDF-Pruefspur oben als anderes Gold gespeichert werden. Nicht entfernen.
        // Festgehalten in GoldSampleSpeichernUseCaseTests.PDF_Vorschlag_ohne_Bestand_ergibt_immer_PDF_Herkunft.
        if (existingSample is null
            && item.SourceSuggestion is not null
            && !isPdfPhoto)
        {
            return (null, GoldSampleAufbau.Rejected(
                "Die PDF-Herkunft konnte nicht eindeutig gebunden werden. Es wurde nichts gespeichert."));
        }

        var codeChanged = repairsExistingSample
            && !string.Equals(
                GoldSampleAufbau.NormalizeCode(existingSample?.Code ?? item.ExistingCode),
                finalCode,
                StringComparison.OrdinalIgnoreCase);
        var keepsExistingReviewDecision = repairsExistingSample
            && !codeChanged
            && existingSample?.Corrected.HasValue == true
            && (string.Equals(
                    existingSample.MatchLevel,
                    MatchLevelNames.ReviewApproved,
                    StringComparison.Ordinal)
                || string.Equals(
                    existingSample.MatchLevel,
                    MatchLevelNames.ReviewCorrected,
                    StringComparison.Ordinal));
        var wasCorrected = keepsExistingReviewDecision
            ? existingSample!.Corrected!.Value
            : isPdfPhoto
                ? !string.Equals(
                    GoldSampleAufbau.NormalizeCode(sourceReferenceCode),
                    finalCode,
                    StringComparison.OrdinalIgnoreCase)
                : decision.WasCorrected;
        var matchLevel = keepsExistingReviewDecision
            ? existingSample!.MatchLevel!
            : wasCorrected
                ? MatchLevelNames.ReviewCorrected
                : MatchLevelNames.ReviewApproved;

        return (new GoldSampleHerkunft(
            existingSample,
            repairsExistingSample,
            sourceType,
            sourceNote,
            sourceReferenceCode,
            sourceReferenceDescription,
            codeChanged,
            wasCorrected,
            matchLevel), null);
    }
}
