using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Geladene Eval-Schutzmengen. Beide Mengen leer heisst: Der Schutz ist bewusst
/// abgeschaltet (leerer Eintrag). Ein geladener Schutz traegt immer mindestens eine
/// Haltungskennung.
/// </summary>
public sealed record EvalProtectionSets(
    IReadOnlySet<string> ImageHashes,
    IReadOnlySet<string> HaltungKeys)
{
    public static EvalProtectionSets Disabled { get; } =
        new(FrozenSet<string>.Empty, FrozenSet<string>.Empty);

    public bool IsDisabled => ImageHashes.Count == 0 && HaltungKeys.Count == 0;
}

/// <summary>
/// Die EINE strenge Lesart des Eval-Schutzordners (Deepscan 02.10.2026, A1/R2, Entscheid
/// Pascal E1 «sperren»). Gold-Speicher, Wissenssuche und die UI-Lader nutzen sie; Vorbild
/// ist der Inventar-Leser des YOLO-Exports. Bedeutung an allen Lesewegen:
/// <list type="bullet">
/// <item>Eintrag bewusst leer: Schutz aus (die einzige Abschaltung).</item>
/// <item>Ordner fehlt oder ist selbst eine Verknuepfung: Fehler.</item>
/// <item>Ein Unterordner ist unlesbar oder eine Verknuepfung/Junction: Fehler (nie betreten,
/// nie still weglassen — sonst fehlen dahinter liegende Pruefsaetze).</item>
/// <item>Eine <c>_manifest.json</c> oder <c>_candidates.json</c> ist unlesbar oder ungueltig,
/// eine Kandidatenliste ist leer oder ein Kandidat hat keine gueltige Haltungskennung: Fehler.</item>
/// <item>Der Ordner liefert keine einzige Haltungskennung: Fehler.</item>
/// </list>
/// Fehler kommen nur als <see cref="IOException"/> (auch <see cref="DirectoryNotFoundException"/>)
/// oder <see cref="InvalidDataException"/>; Aufrufer sperren dann, statt ungefiltert weiterzuarbeiten.
/// </summary>
public static class EvalProtectionSetReader
{
    private const string ManifestFileName = "_manifest.json";
    private const string CandidatesFileName = "_candidates.json";

    public static EvalProtectionSets LoadStrict(string? evalSetRoot)
    {
        if (string.IsNullOrWhiteSpace(evalSetRoot))
            return EvalProtectionSets.Disabled;

        var fullRoot = Path.GetFullPath(evalSetRoot);
        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException($"Der konfigurierte Prüfdaten-Ordner fehlt: {fullRoot}");
        RejectReparsePoint(fullRoot);

        var skipped = new List<string>();
        var directories = SafeFileEnumeration.EnumerateDirectoriesSafe(fullRoot, skipped).ToArray();
        if (skipped.Count > 0)
        {
            throw new IOException(
                "Im Prüfdaten-Ordner sind Ordner nicht lesbar oder Verknüpfungen: "
                + string.Join(", ", skipped.Distinct(StringComparer.OrdinalIgnoreCase)));
        }

        foreach (var directory in directories)
        {
            ValidateJsonFile(Path.Combine(directory, ManifestFileName), ValidateManifest);
            ValidateJsonFile(Path.Combine(directory, CandidatesFileName), ValidateCandidates);
        }

        var hashes = EvalContaminationGuard.LoadEvalImageHashes(fullRoot);
        var keys = EvalContaminationGuard.LoadEvalHaltungKeys(fullRoot);
        if (keys.Count == 0)
            throw new InvalidDataException($"Der Prüfdaten-Ordner enthält keine Haltungskennungen: {fullRoot}");

        // Nur echte SHA-256-Hashes und kanonische numerische Haltungskennungen gelten als Schutzdaten.
        var validated = new TrainingPdfReviewProtectionSnapshot(hashes, keys);
        return new EvalProtectionSets(validated.ImageHashes, validated.HoldingKeys);
    }

    private static void ValidateJsonFile(string path, Action<JsonNode, string> validate)
    {
        if (!File.Exists(path))
            return;

        JsonNode node;
        try
        {
            RejectReparsePoint(path);
            node = JsonNode.Parse(File.ReadAllText(path))
                ?? throw Ungueltig(path, "Die Datei ist leer.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"Eval-Schutzdatei '{path}' ist nicht lesbar oder kein gültiges JSON.",
                ex);
        }

        validate(node, path);
    }

    private static void ValidateManifest(JsonNode node, string path)
    {
        if (node is not JsonObject manifest)
            throw Ungueltig(path, "Erwartet wird ein JSON-Objekt.");
        if (manifest["hashes"] is null)
            return;
        if (manifest["hashes"] is not JsonObject hashes)
            throw Ungueltig(path, "'hashes' muss ein JSON-Objekt sein.");

        foreach (var property in hashes.Where(entry =>
                     entry.Key.StartsWith("images/", StringComparison.OrdinalIgnoreCase)))
        {
            if (property.Value is not JsonObject imageEntry
                || imageEntry["sha256"] is not JsonValue shaValue
                || !shaValue.TryGetValue<string>(out var sha256)
                || string.IsNullOrWhiteSpace(sha256))
            {
                throw Ungueltig(path, $"Der Bildhash '{property.Key}' braucht einen SHA-256-Wert als Text.");
            }

            try
            {
                _ = new TrainingPdfReviewProtectionSnapshot([sha256], []);
            }
            catch (InvalidDataException ex)
            {
                throw Ungueltig(path, $"Der Bildhash '{property.Key}' ist kein gültiger SHA-256-Wert.", ex);
            }
        }
    }

    private static void ValidateCandidates(JsonNode node, string path)
    {
        var candidates = node as JsonArray
            ?? (node is JsonObject wrapper ? wrapper["candidates"] as JsonArray : null)
            ?? throw Ungueltig(path, "Erwartet wird ein Array oder ein Objekt mit 'candidates'-Array.");
        if (candidates.Count == 0)
            throw Ungueltig(path, "Die Kandidatenliste ist leer.");

        foreach (var candidate in candidates)
        {
            if (candidate is not JsonObject candidateObject
                || candidateObject["haltung_key"] is not JsonValue holdingValue
                || !holdingValue.TryGetValue<string>(out var holdingKey)
                || string.IsNullOrWhiteSpace(holdingKey))
            {
                throw Ungueltig(path, "Jeder Eval-Kandidat braucht eine Haltungskennung als Text.");
            }

            try
            {
                _ = new TrainingPdfReviewProtectionSnapshot([], [holdingKey]);
            }
            catch (InvalidDataException ex)
            {
                throw Ungueltig(path, $"'{holdingKey}' ist keine gültige numerische Haltungskennung.", ex);
            }
        }
    }

    private static InvalidDataException Ungueltig(string path, string grund, Exception? inner = null)
        => new($"Eval-Schutzdatei '{path}' ist ungültig: {grund}", inner);

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Der Prüfdaten-Pfad ist eine Verknüpfung oder Junction: {path}");
    }
}
