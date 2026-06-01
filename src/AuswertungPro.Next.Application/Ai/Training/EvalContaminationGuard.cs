using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Reine Pruef-Funktion gegen Eval-Set-Kontamination: verhindert, dass ein Bild aus dem
/// eingefrorenen Eval-Set ueber den KB-/FewShot-Indexpfad in die Trainings-/Retrieval-Daten
/// gelangt (sonst messen Benchmarks keine Generalisierung mehr).
///
/// Spiegelt die Hash-Logik des StageAExporter (SHA-256 als Hex, lowercase, inhaltsbasiert –
/// NICHT dateinamenbasiert), damit derselbe Eval-Hash-Satz wiederverwendet werden kann.
///
/// BEWUSST NICHT in KnowledgeBaseManager.IsIndexWorthy verdrahtet: das ist eine separate,
/// fachliche Betriebsentscheidung (blockieren vs. nur warnen) und gehoert in einen eigenen
/// Schritt. Hier nur die testbare, seiteneffektfreie Pruefung.
/// </summary>
public static class EvalContaminationGuard
{
    /// <summary>
    /// SHA-256-Hex (lowercase) des Datei-Inhalts; null wenn der Pfad leer ist oder die Datei fehlt.
    /// </summary>
    public static string? ComputeFileHash(string? framePath)
    {
        if (string.IsNullOrWhiteSpace(framePath) || !File.Exists(framePath))
            return null;

        using var stream = File.OpenRead(framePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// True, wenn das Bild unter <paramref name="framePath"/> inhaltsgleich zu einem
    /// Eval-Set-Bild ist (Hash-Vergleich gegen <paramref name="evalImageHashes"/>).
    /// Leerer Hash-Satz oder fehlende Datei -> false (kein falscher Alarm).
    /// </summary>
    public static bool IsEvalContaminated(IReadOnlySet<string> evalImageHashes, string? framePath)
    {
        if (evalImageHashes is null || evalImageHashes.Count == 0)
            return false;

        var hash = ComputeFileHash(framePath);
        return hash is not null && evalImageHashes.Contains(hash);
    }

    private static readonly string[] ImageExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

    /// <summary>
    /// Laedt die SHA-256-Hashes (lowercase) der Eval-Set-Bilder. Bevorzugt _manifest.json
    /// (hashes.images/*.sha256, gleiche Quelle wie StageAExporter/eval-set-warden), faellt sonst
    /// auf direkte Hash-Berechnung der images/-Dateien zurueck. Fehlender Pfad/Ordner oder defektes
    /// Manifest -> leerer Satz (Schutz inaktiv statt Crash; degradiert sicher auf fremden Maschinen).
    /// </summary>
    public static IReadOnlySet<string> LoadEvalImageHashes(string? evalSetRoot)
    {
        var empty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(evalSetRoot) || !Directory.Exists(evalSetRoot))
            return empty;

        var manifestPath = Path.Combine(evalSetRoot, "_manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject();
                var hashes = manifest?["hashes"]?.AsObject();
                if (hashes is not null)
                {
                    var fromManifest = hashes
                        .Where(p => p.Key.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
                        .Select(p => p.Value?["sha256"]?.GetValue<string>())
                        .Where(h => !string.IsNullOrWhiteSpace(h))
                        .Select(h => h!.Trim().ToLowerInvariant())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    if (fromManifest.Count > 0)
                        return fromManifest;
                }
            }
            catch
            {
                // Defektes Manifest -> Fallback auf direkte Berechnung statt Crash.
            }
        }

        var imageRoot = Path.Combine(evalSetRoot, "images");
        if (!Directory.Exists(imageRoot))
            return empty;

        return Directory
            .EnumerateFiles(imageRoot, "*.*", SearchOption.TopDirectoryOnly)
            .Where(p => ImageExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Select(ComputeFileHash)
            .Where(h => h is not null)
            .Select(h => h!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
