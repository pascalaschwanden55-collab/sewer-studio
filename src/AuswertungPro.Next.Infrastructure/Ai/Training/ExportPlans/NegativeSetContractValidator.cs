using System.Globalization;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Vertragsspezifische Striktheitspruefungen fuer die Negativ-Set- und Queue-Belege
/// (bcc- und proto-Vertrag). Ausgelagert aus <see cref="TrainingExportRegistryFileStore"/>,
/// damit der Store die Architektur-Groessenlimits einhaelt; die Pruefregeln bleiben identisch.
/// </summary>
internal static class NegativeSetContractValidator
{
    /// <summary>Vertragsspezifische Zusatzfelder des semantischen Negativ-Set-Belegs.</summary>
    public static void ValidateContractSpecificShape(
        NegativeSetSemanticFileDocument semantic,
        bool isProto)
    {
        if (isProto)
        {
            // proto: Auschlusslisten (string-Listen, duerfen leer sein) und art/pfad-Schutzform.
            if (semantic.ExcludedNotNormalizable is null
                || semantic.ExcludedEvalProtected is null
                || semantic.ExcludedNotNormalizable.Any(string.IsNullOrWhiteSpace)
                || semantic.ExcludedEvalProtected.Any(string.IsNullOrWhiteSpace))
            {
                throw new TrainingExportPlanException(
                    "Die Auschlusslisten des proto-Negativ-Sets sind ungueltig.");
            }
            foreach (var set in semantic.ProtectedSets)
            {
                if (set is null
                    || string.IsNullOrWhiteSpace(set.Art)
                    || string.IsNullOrWhiteSpace(set.Pfad)
                    || set.SetId is not null
                    || set.ManifestStatus is not null
                    || set.ManifestSha256 is not null
                    || set.CandidatesSha256 is not null)
                {
                    throw new TrainingExportPlanException(
                        "Der Schutzbestand des proto-Negativ-Sets ist ungueltig.");
                }
            }
            var snapshot = semantic.ProtectionSnapshot;
            if (snapshot.SchluesselGesamt is null or < 0
                || snapshot.QuellenAnteile is null
                || snapshot.QuellenAnteile.Any(item =>
                    string.IsNullOrWhiteSpace(item.Key) || item.Value < 0)
                || snapshot.OhneDiagnoseWarteschlangen is null
                || snapshot.ByteSchutz is null
                || snapshot.TrainingSamplesSha256 is not null
                || snapshot.ExportRegistrySha256 is not null
                || snapshot.KnownImageHashes is not null
                || snapshot.KnownImageHashesSha256 is not null
                || snapshot.KnownHoldingAliases is not null
                || snapshot.KnownHoldingAliasesSha256 is not null
                || snapshot.CandidateScopeSha256 is not null
                || snapshot.BaseModelSha256 is not null)
            {
                throw new TrainingExportPlanException(
                    "Die Schutz-Momentaufnahme des proto-Negativ-Sets ist ungueltig.");
            }
            return;
        }

        // bcc: keine Auschlusslisten, Schutz-Set-Eintraege mit set_id, 8-Felder-Momentaufnahme.
        if (semantic.ExcludedNotNormalizable is not null
            || semantic.ExcludedEvalProtected is not null)
        {
            throw new TrainingExportPlanException(
                "Der bcc-Negativ-Set-Vertrag kennt keine Auschlusslisten.");
        }
        foreach (var set in semantic.ProtectedSets)
        {
            if (set is null
                || string.IsNullOrWhiteSpace(set.SetId)
                || string.IsNullOrWhiteSpace(set.ManifestStatus)
                || string.IsNullOrWhiteSpace(set.CandidatesSha256)
                || set.Art is not null
                || set.Pfad is not null)
            {
                throw new TrainingExportPlanException(
                    "Der Schutzbestand des bcc-Negativ-Sets ist ungueltig.");
            }
        }
        var bccSnapshot = semantic.ProtectionSnapshot;
        if (bccSnapshot.TrainingSamplesSha256 is null
            || bccSnapshot.ExportRegistrySha256 is null
            || bccSnapshot.KnownImageHashes is null
            || bccSnapshot.KnownImageHashesSha256 is null
            || bccSnapshot.KnownHoldingAliases is null
            || bccSnapshot.KnownHoldingAliasesSha256 is null
            || bccSnapshot.CandidateScopeSha256 is null
            || bccSnapshot.BaseModelSha256 is null
            || bccSnapshot.SchluesselGesamt is not null
            || bccSnapshot.QuellenAnteile is not null
            || bccSnapshot.OhneDiagnoseWarteschlangen is not null
            || bccSnapshot.ByteSchutz is not null)
        {
            throw new TrainingExportPlanException(
                "Die Schutz-Momentaufnahme des bcc-Negativ-Sets ist ungueltig.");
        }
    }

    /// <summary>bcc-Vertrag: gebundene Auswahlmodelle des Queue-Belegs (Pflicht, eindeutig).</summary>
    public static IReadOnlySet<string> ValidateModelScope(
        IReadOnlyList<QueueModelReceiptFileDocument>? modelScope)
    {
        if (modelScope is null || modelScope.Count == 0)
        {
            throw new TrainingExportPlanException(
                "Der Queue-Beleg besitzt keine gebundenen Auswahlmodelle.");
        }

        var modelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in modelScope)
        {
            if (model is null
                || string.IsNullOrWhiteSpace(model.CandidateId)
                || !modelIds.Add(model.CandidateId))
            {
                throw new TrainingExportPlanException(
                    "Der Queue-Beleg besitzt doppelte oder leere Modell-IDs.");
            }
            TrainingExportRegistryFileStore.RequireLowercaseSha256(
                model.CandidateManifestSha256,
                $"Kandidatenmanifest-Hash von '{model.CandidateId}'");
            TrainingExportRegistryFileStore.RequireLowercaseSha256(
                model.WeightsSha256,
                $"Gewichte-Hash von '{model.CandidateId}'");
            TrainingExportRegistryFileStore.RequireLowercaseSha256(
                model.DatasetPlanId,
                $"Datensatzplan-ID von '{model.CandidateId}'");
            TrainingExportRegistryFileStore.RequireLowercaseSha256(
                model.DatasetManifestSha256,
                $"Datensatzmanifest-Hash von '{model.CandidateId}'");
        }

        return modelIds;
    }

    /// <summary>bcc-Vertrag der Queue-Items (id = bcc-hn-&lt;sha256[:16]&gt;, Modelltrigger-Pflicht).</summary>
    public static void ValidateBccItems(
        IReadOnlyList<QueueItemReceiptFileDocument> items,
        IReadOnlySet<string> modelIds)
    {
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        var imageHashes = new HashSet<string>(StringComparer.Ordinal);
        var physicalHoldings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id))
                throw new TrainingExportPlanException("Der Queue-Beleg enthaelt eine leere Bild-ID.");

            var imageSha256 = TrainingExportRegistryFileStore.RequireLowercaseSha256(
                item.ImageSha256,
                $"Queue-Bildhash von '{item.Id}'");
            var sourceRef = TrainingExportRegistryFileStore.RequireLowercaseSha256(
                item.SourceRef,
                $"Queue-Quellbeleg von '{item.Id}'");
            if (string.IsNullOrWhiteSpace(item.HoldingKey)
                || string.IsNullOrWhiteSpace(item.PhysicalHoldingKey))
            {
                throw new TrainingExportPlanException(
                    $"Queue-Bildbeleg '{item.Id}' besitzt keine gueltige Haltung.");
            }
            var holdingKey = TrainingExportRegistryFileStore.NormalizeStrictHoldingKey(item.HoldingKey);
            var imageFormat = item.ImageFormat?.ToLowerInvariant();
            if (!itemIds.Add(item.Id)
                || !string.Equals(item.Id, $"bcc-hn-{imageSha256[..16]}", StringComparison.Ordinal)
                || !string.Equals(item.HoldingKey, holdingKey, StringComparison.Ordinal)
                || !string.Equals(
                    item.PhysicalHoldingKey,
                    TrainingExportRegistryFileStore.PhysicalHoldingKey(holdingKey),
                    StringComparison.Ordinal)
                || !imageHashes.Add(imageSha256)
                || !physicalHoldings.Add(item.PhysicalHoldingKey)
                || !string.Equals(item.SourceRef, sourceRef, StringComparison.Ordinal)
                || !DateOnly.TryParseExact(
                    item.InspectionDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _)
                || item.SizeBytes < TrainingExportRegistryFileStore.MinimumTrainingNegativeBytes
                || imageFormat is not ("jpg" or "jpeg" or "png")
                || !string.Equals(item.ImageFormat, imageFormat, StringComparison.Ordinal)
                // proto-only-Felder duerfen im bcc-Vertrag nicht vorkommen.
                || item.ItemId is not null
                || item.Code is not null
                || item.Gruppe is not null
                || item.Quelle is not null
                || item.QuellDatei is not null
                || item.Leitungsinspektion is not null
                || item.TargetFileName is not null)
            {
                throw new TrainingExportPlanException(
                    $"Queue-Bildbeleg '{item.Id}' ist ungueltig.");
            }

            if (item.Predictions is null || item.Predictions.Count != modelIds.Count)
            {
                throw new TrainingExportPlanException(
                    $"Queue-Bild '{item.Id}' besitzt keine vollstaendige Modellvorhersage.");
            }
            var predictedModelIds = new HashSet<string>(StringComparer.Ordinal);
            var triggered = false;
            foreach (var prediction in item.Predictions)
            {
                if (prediction is null
                    || string.IsNullOrWhiteSpace(prediction.ModelId)
                    || !modelIds.Contains(prediction.ModelId)
                    || !predictedModelIds.Add(prediction.ModelId)
                    || prediction.BccDetectionCount < 0
                    || (prediction.MaxBccConfidence is { } confidence
                        && (!double.IsFinite(confidence) || confidence is < 0 or > 1))
                    || (prediction.PredictedBcc && prediction.BccDetectionCount < 1))
                {
                    throw new TrainingExportPlanException(
                        $"Queue-Vorhersage fuer '{item.Id}' ist ungueltig.");
                }
                triggered |= prediction.PredictedBcc;
            }
            if (!predictedModelIds.SetEquals(modelIds) || !triggered)
            {
                throw new TrainingExportPlanException(
                    $"Queue-Bild '{item.Id}' ist nicht an einen BCC-Modelltrigger gebunden.");
            }
        }
    }

    /// <summary>
    /// proto-Vertrag der Queue-Items: item_id = proto-hn-&lt;sha256[:20]&gt;, Zieldateiname
    /// img_&lt;sha&gt;.&lt;format&gt;, keine Duplikate, Haltungs-Eindeutigkeit auf Casefold-Ebene.
    /// bcc-only-Felder (id, physical_holding_key, source_ref, inspection_date, predictions)
    /// duerfen nicht vorkommen; eine Modellbindung gibt es im proto-Vertrag nicht.
    /// </summary>
    public static void ValidateProtoItems(IReadOnlyList<QueueItemReceiptFileDocument> items)
    {
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        var imageHashes = new HashSet<string>(StringComparer.Ordinal);
        var holdingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.ItemId))
                throw new TrainingExportPlanException("Der Queue-Beleg enthaelt eine leere Bild-ID.");

            var imageSha256 = TrainingExportRegistryFileStore.RequireLowercaseSha256(
                item.ImageSha256,
                $"Queue-Bildhash von '{item.ItemId}'");
            var imageFormat = item.ImageFormat?.ToLowerInvariant();
            if (!itemIds.Add(item.ItemId)
                || !string.Equals(item.ItemId, $"proto-hn-{imageSha256[..20]}", StringComparison.Ordinal)
                || !imageHashes.Add(imageSha256)
                || string.IsNullOrWhiteSpace(item.HoldingKey)
                || !holdingKeys.Add(item.HoldingKey)
                || item.SizeBytes < TrainingExportRegistryFileStore.MinimumTrainingNegativeBytes
                || imageFormat is not ("jpg" or "jpeg" or "png")
                || !string.Equals(item.ImageFormat, imageFormat, StringComparison.Ordinal)
                || !string.Equals(
                    item.TargetFileName,
                    $"img_{imageSha256}.{imageFormat}",
                    StringComparison.Ordinal)
                // bcc-only-Felder duerfen im proto-Vertrag nicht vorkommen.
                || item.Id is not null
                || item.PhysicalHoldingKey is not null
                || item.SourceRef is not null
                || item.InspectionDate is not null
                || item.Predictions is not null)
            {
                throw new TrainingExportPlanException(
                    $"Queue-Bildbeleg '{item.ItemId}' ist ungueltig.");
            }
        }
    }
}
