using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.UI.Services;

public sealed record EvalContaminationSets(
    IReadOnlySet<string> ImageHashes,
    IReadOnlySet<string> HaltungKeys);

public static class EvalContaminationSetProvider
{
    public static EvalContaminationSets Load(AppSettings? settings)
        => Load(settings?.EvalSetRoot ?? AppSettings.Load().EvalSetRoot);

    public static EvalContaminationSets Load(string? evalSetRoot)
    {
        // Leer ist die einzige ausdrueckliche Abschaltung. Ein konfigurierter, aber
        // fehlender/defekter Schutzordner darf Training niemals still freigeben.
        if (string.IsNullOrWhiteSpace(evalSetRoot))
        {
            return new EvalContaminationSets(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var fullRoot = Path.GetFullPath(evalSetRoot);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException(
                $"Der konfigurierte Eval-Schutzordner wurde nicht gefunden: {fullRoot}");
        }

        ValidateJsonFiles(fullRoot, "_manifest.json", requireCandidatesShape: false);
        ValidateJsonFiles(fullRoot, "_candidates.json", requireCandidatesShape: true);

        var loadedSets = new EvalContaminationSets(
            EvalContaminationGuard.LoadEvalImageHashes(fullRoot),
            EvalContaminationGuard.LoadEvalHaltungKeys(fullRoot));
        if (loadedSets.ImageHashes.Count == 0 && loadedSets.HaltungKeys.Count == 0)
        {
            throw new InvalidDataException(
                $"Der konfigurierte Eval-Schutzordner enthaelt keine lesbaren Schutzdaten: {fullRoot}");
        }

        // Der Snapshot ist die gemeinsame semantische Grenze: Nur echte
        // SHA-256-Hashes und kanonische numerische Haltungskeys duerfen als
        // Schutzdaten weitergereicht werden.
        var validated = new TrainingPdfReviewProtectionSnapshot(
            loadedSets.ImageHashes,
            loadedSets.HaltungKeys);
        return new EvalContaminationSets(
            validated.ImageHashes,
            validated.HoldingKeys);
    }

    public static TrainingPdfReviewProtectionSnapshot LoadPdfProtectionSnapshot(
        string? evalSetRoot)
    {
        var sets = Load(evalSetRoot);
        if (!string.IsNullOrWhiteSpace(evalSetRoot)
            && sets.HaltungKeys.Count == 0)
        {
            throw new InvalidDataException(
                "Der konfigurierte Eval-Schutz enthaelt keine gueltigen Haltungskennungen. " +
                "PDF-Fotos duerfen ohne Haltungs-Schutz nicht importiert werden, weil ihre Bildfarben normalisiert werden koennen.");
        }

        return new TrainingPdfReviewProtectionSnapshot(
            sets.ImageHashes,
            sets.HaltungKeys);
    }

    private static void ValidateJsonFiles(
        string root,
        string fileName,
        bool requireCandidatesShape)
    {
        foreach (var path in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(path))
                    ?? throw new InvalidDataException("JSON ist leer.");
                if (requireCandidatesShape
                    && node is not JsonArray
                    && node?["candidates"] is not JsonArray)
                {
                    throw new InvalidDataException(
                        "Erwartet wird ein Array oder ein Objekt mit 'candidates'-Array.");
                }
                if (!requireCandidatesShape && node is not JsonObject)
                    throw new InvalidDataException("Erwartet wird ein JSON-Objekt.");

                if (requireCandidatesShape)
                    ValidateCandidateHoldingKeys(node);
                else
                    ValidateManifestImageHashes(node.AsObject());
            }
            catch (Exception ex) when (ex is not InvalidDataException
                                       || !ex.Message.Contains(path, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Eval-Schutzdatei '{path}' ist nicht lesbar: {ex.Message}",
                    ex);
            }
        }
    }

    private static void ValidateCandidateHoldingKeys(JsonNode node)
    {
        var candidates = node as JsonArray ?? node["candidates"]!.AsArray();
        foreach (var candidate in candidates)
        {
            if (candidate is not JsonObject candidateObject
                || candidateObject["haltung_key"] is not JsonValue holdingValue
                || !holdingValue.TryGetValue<string>(out var holdingKey)
                || string.IsNullOrWhiteSpace(holdingKey))
            {
                throw new InvalidDataException(
                    "Jeder Eval-Kandidat braucht eine Haltungskennung als Text.");
            }

            _ = new TrainingPdfReviewProtectionSnapshot(
                [],
                [holdingKey]);
        }
    }

    private static void ValidateManifestImageHashes(JsonObject manifest)
    {
        if (manifest["hashes"] is null)
            return;
        if (manifest["hashes"] is not JsonObject hashes)
            throw new InvalidDataException("'hashes' muss ein JSON-Objekt sein.");

        foreach (var property in hashes.Where(entry =>
                     entry.Key.StartsWith(
                         "images/",
                         StringComparison.OrdinalIgnoreCase)))
        {
            if (property.Value is not JsonObject imageEntry
                || imageEntry["sha256"] is not JsonValue shaValue
                || !shaValue.TryGetValue<string>(out var sha256)
                || string.IsNullOrWhiteSpace(sha256))
            {
                throw new InvalidDataException(
                    $"Der Bildhash '{property.Key}' braucht einen SHA-256-Wert als Text.");
            }

            _ = new TrainingPdfReviewProtectionSnapshot(
                [sha256],
                []);
        }
    }
}
