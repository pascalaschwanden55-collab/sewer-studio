using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Ai.Training;

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

        var sets = new EvalContaminationSets(
            EvalContaminationGuard.LoadEvalImageHashes(fullRoot),
            EvalContaminationGuard.LoadEvalHaltungKeys(fullRoot));
        if (sets.ImageHashes.Count == 0 && sets.HaltungKeys.Count == 0)
        {
            throw new InvalidDataException(
                $"Der konfigurierte Eval-Schutzordner enthaelt keine lesbaren Schutzdaten: {fullRoot}");
        }

        return sets;
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
}
