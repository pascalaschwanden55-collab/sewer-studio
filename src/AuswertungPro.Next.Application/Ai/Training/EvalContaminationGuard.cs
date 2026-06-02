using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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

    // ── Haltungs-/CaseId-Sperrliste ────────────────────────────────────────
    // Der Hash-Schutz oben faengt nur PIXELIDENTISCHE Frames. Eval-Frames stammen
    // aber aus denselben Haltungen wie Trainingsdaten – dieselbe reale Schadensstelle
    // kann unter anderem Frame/Hash erneut auftauchen (Content-Hash greift dann nicht).
    // Diese Sperrliste blockt ein Sample anhand seiner Haltung (CaseId), sodass keine
    // der reservierten Eval-Haltungen ueber irgendeinen Frame ins Training/Retrieval gelangt.

    // Schacht-Paar in freiem Text: Zahlenteile duerfen Punkte enthalten (Bereichs-Praefix
    // wie "06.24379"), Trenner ist "-" oder "/". Bewusst dieselbe Form wie die erprobte
    // HaltungIdInFilename-Regex (TrainingCenterImportService), damit dot-praefigierte
    // CaseIds (z. B. "06.24379-06.24377") NICHT still falsch normalisiert werden.
    private static readonly Regex HaltungKeyPattern =
        new(@"\d[\d.]*[-/]\d[\d.]*", RegexOptions.Compiled);

    /// <summary>
    /// Normalisiert eine CaseId/Haltungs-Bezeichnung auf das kanonische Schacht-Paar
    /// (z. B. "287425-81162"). Extrahiert das erste Schacht-Paar-Muster und gleicht
    /// Praefixe/Suffixe sowie einen Bereichs-Praefix je Schachtnummer aus:
    /// "07.638910-1367" -> "638910-1367", "06.24379-06.24377" -> "24379-24377",
    /// "634-581/2025_Saniert" -> "634-581". Ohne Treffer: getrimmte Eingabe (matcht dann
    /// nur bei exakter Gleichheit). Leer -> null.
    /// </summary>
    public static string? NormalizeHaltungKey(string? caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
            return null;
        var m = HaltungKeyPattern.Match(caseId);
        if (!m.Success)
            return caseId.Trim();
        var parts = m.Value.Split('-', '/');
        if (parts.Length != 2)
            return m.Value;
        return $"{StripAreaPrefix(parts[0])}-{StripAreaPrefix(parts[1])}";
    }

    /// <summary>Entfernt einen Bereichs-Praefix vor der Schachtnummer ("06.24379" -> "24379").
    /// Bei fehlendem Punkt oder End-Punkt ("12.") bleibt der Teil unveraendert (kein leerer Key).</summary>
    private static string StripAreaPrefix(string manhole)
    {
        var dot = manhole.LastIndexOf('.');
        return dot >= 0 && dot < manhole.Length - 1 ? manhole[(dot + 1)..] : manhole;
    }

    /// <summary>
    /// True, wenn die Haltung des Samples (CaseId, normalisiert) zu einer reservierten
    /// Eval-Haltung gehoert. Leerer Satz oder leere CaseId -> false (kein Alarm).
    /// </summary>
    public static bool IsEvalHaltung(IReadOnlySet<string> evalHaltungKeys, string? caseId)
    {
        if (evalHaltungKeys is null || evalHaltungKeys.Count == 0)
            return false;
        var key = NormalizeHaltungKey(caseId);
        return key is not null && evalHaltungKeys.Contains(key);
    }

    /// <summary>
    /// Laedt die normalisierten Haltungs-Schluessel der Eval-Kandidaten. Bevorzugt
    /// _candidates.json (Feld haltung_key), faellt sonst auf das Haltungs-Praefix der
    /// images/-Dateinamen (&lt;haltung_key&gt;_&lt;zeit&gt;s_&lt;code&gt;_t+0.png) zurueck. Fehlender
    /// Pfad oder defekte Datei -> leerer Satz (Schutz inaktiv statt Crash; degradiert sicher).
    /// </summary>
    public static IReadOnlySet<string> LoadEvalHaltungKeys(string? evalSetRoot)
    {
        var empty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(evalSetRoot) || !Directory.Exists(evalSetRoot))
            return empty;

        var candidatesPath = Path.Combine(evalSetRoot, "_candidates.json");
        if (File.Exists(candidatesPath))
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(candidatesPath));
                var array = node as JsonArray ?? node?["candidates"] as JsonArray;
                if (array is not null)
                {
                    var keys = array
                        // ToString() statt GetValue<string>(): ein nicht-string haltung_key
                        // (Zahl/Objekt) darf nicht die GANZE Liste per Exception verwerfen.
                        .Select(it => (it as JsonObject)?["haltung_key"]?.ToString())
                        .Select(NormalizeHaltungKey)
                        .Where(k => !string.IsNullOrWhiteSpace(k))
                        .Select(k => k!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    if (keys.Count > 0)
                        return keys;
                }
            }
            catch
            {
                // Defekte Datei -> Fallback auf Dateinamen statt Crash.
            }
        }

        var imageRoot = Path.Combine(evalSetRoot, "images");
        if (!Directory.Exists(imageRoot))
            return empty;

        return Directory
            .EnumerateFiles(imageRoot, "*.*", SearchOption.TopDirectoryOnly)
            .Where(p => ImageExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Select(p => NormalizeHaltungKey(Path.GetFileName(p)))
            .Where(k => k is not null && HaltungKeyPattern.IsMatch(k))
            .Select(k => k!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
