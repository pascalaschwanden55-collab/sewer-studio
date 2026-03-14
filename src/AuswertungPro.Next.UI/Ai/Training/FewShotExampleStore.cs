// SewerStudio – Few-Shot Beispiel-Bibliothek fuer Qwen Vision
// Speichert kuratierte (Bild, Code, Beschreibung)-Paare nach Schadenskategorie.
// Beim Analysieren neuer Bilder werden die besten Beispiele in den Prompt injiziert.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Ein einzelnes Few-Shot Beispiel: Foto + VSA-Code + Beschreibung.
/// Das Foto wird als JPEG/PNG im Knowledge-Ordner gespeichert,
/// der Metadaten-Index als JSON.
/// </summary>
public sealed record FewShotExample
{
    /// <summary>Eindeutige ID.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>VSA-Code (z.B. "BAB", "BCAAA", "BDA").</summary>
    public string VsaCode { get; init; } = "";

    /// <summary>Schadenskategorie (erste 2 Zeichen: "BA"=Riss, "BB"=Betrieblich, "BC"=Anschluss...).</summary>
    public string Category { get; init; } = "";

    /// <summary>Beschreibung aus dem Protokoll.</summary>
    public string Description { get; init; } = "";

    /// <summary>Uhrzeitlage (z.B. "12 Uhr", "6 Uhr").</summary>
    public string? ClockPosition { get; init; }

    /// <summary>Meter-Position im Rohr.</summary>
    public double MeterPosition { get; init; }

    /// <summary>Rohrmaterial (z.B. "Beton", "Kunststoff_Polypropylen").</summary>
    public string? Material { get; init; }

    /// <summary>Rohrprofil/Durchmesser (z.B. "Kreisprofil 300mm").</summary>
    public string? Profile { get; init; }

    /// <summary>Relativer Pfad zum Bild (relativ zum Knowledge-Root).</summary>
    public string ImageRelativePath { get; init; } = "";

    /// <summary>Qualitaet des Beispiels (0.0-1.0). Hoeher = besseres Beispiel.</summary>
    public double Quality { get; init; } = 0.5;

    /// <summary>Herkunft (z.B. "pdf:634-581", "manual").</summary>
    public string Source { get; init; } = "";

    /// <summary>Erstellungszeitpunkt.</summary>
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Speichert und laedt die Few-Shot Beispiel-Bibliothek.
/// Organisiert nach Schadenskategorie fuer schnellen Zugriff.
/// </summary>
public sealed class FewShotExampleStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _indexPath;
    private readonly string _imagesDir;
    private List<FewShotExample> _examples = new();
    private bool _loaded;

    public FewShotExampleStore()
    {
        var root = KnowledgeRoot.GetRoot();
        _indexPath = Path.Combine(root, "fewshot_examples.json");
        _imagesDir = Path.Combine(root, "fewshot_images");
    }

    /// <summary>Alle geladenen Beispiele.</summary>
    public IReadOnlyList<FewShotExample> Examples => _examples;

    /// <summary>Anzahl Beispiele pro Kategorie.</summary>
    public Dictionary<string, int> CategoryCounts =>
        _examples.GroupBy(e => e.Category)
                 .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Laedt den Index aus der JSON-Datei.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (_loaded) return;

        if (File.Exists(_indexPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_indexPath, ct);
                _examples = JsonSerializer.Deserialize<List<FewShotExample>>(json, JsonOpts)
                            ?? new List<FewShotExample>();
            }
            catch
            {
                _examples = new List<FewShotExample>();
            }
        }

        _loaded = true;
    }

    /// <summary>Speichert den Index.</summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_indexPath)!);
        var json = JsonSerializer.Serialize(_examples, JsonOpts);
        await File.WriteAllTextAsync(_indexPath, json, ct);
    }

    /// <summary>Fuegt ein neues Beispiel hinzu und speichert das Bild.</summary>
    public async Task<FewShotExample> AddExampleAsync(
        byte[] imageBytes,
        string imageExtension,
        string vsaCode,
        string description,
        string? clockPosition,
        double meterPosition,
        string? material,
        string? profile,
        string source,
        double quality = 0.5,
        CancellationToken ct = default)
    {
        await LoadAsync(ct);

        var category = vsaCode.Length >= 2 ? vsaCode[..2].ToUpperInvariant() : vsaCode.ToUpperInvariant();

        // Bild speichern
        Directory.CreateDirectory(_imagesDir);
        var ext = imageExtension.StartsWith('.') ? imageExtension : "." + imageExtension;
        var fileName = $"{category}_{vsaCode}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..40] + ext;
        var imagePath = Path.Combine(_imagesDir, fileName);
        await File.WriteAllBytesAsync(imagePath, imageBytes, ct);

        var example = new FewShotExample
        {
            VsaCode = vsaCode.ToUpperInvariant(),
            Category = category,
            Description = description,
            ClockPosition = clockPosition,
            MeterPosition = meterPosition,
            Material = material,
            Profile = profile,
            ImageRelativePath = Path.Combine("fewshot_images", fileName),
            Quality = quality,
            Source = source
        };

        _examples.Add(example);
        await SaveAsync(ct);

        return example;
    }

    /// <summary>
    /// Findet die besten Few-Shot Beispiele fuer eine bestimmte Analyse.
    /// Bevorzugt verschiedene Kategorien fuer maximale Abdeckung.
    /// </summary>
    /// <param name="maxExamples">Maximale Anzahl Beispiele (2-4 empfohlen fuer 7B, 4-6 fuer 32B).</param>
    /// <param name="preferredMaterial">Bevorzugtes Rohrmaterial (falls bekannt).</param>
    public async Task<IReadOnlyList<FewShotExample>> GetBestExamplesAsync(
        int maxExamples = 3,
        string? preferredMaterial = null,
        CancellationToken ct = default)
    {
        await LoadAsync(ct);

        if (_examples.Count == 0)
            return Array.Empty<FewShotExample>();

        // Strategie: Verschiedene Kategorien abdecken, hoechste Qualitaet bevorzugen
        var selected = new List<FewShotExample>();
        var usedCategories = new HashSet<string>();

        // Sortiere: Qualitaet absteigend, Material-Match bevorzugt
        var sorted = _examples
            .OrderByDescending(e => e.Quality)
            .ThenByDescending(e => !string.IsNullOrEmpty(preferredMaterial)
                                   && e.Material != null
                                   && e.Material.Contains(preferredMaterial, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ToList();

        // Runde 1: Je 1 Beispiel pro Kategorie (Diversitaet)
        foreach (var ex in sorted)
        {
            if (selected.Count >= maxExamples) break;
            if (usedCategories.Add(ex.Category))
            {
                if (HasValidImage(ex))
                    selected.Add(ex);
            }
        }

        // Runde 2: Auffuellen mit besten verbleibenden
        if (selected.Count < maxExamples)
        {
            foreach (var ex in sorted)
            {
                if (selected.Count >= maxExamples) break;
                if (!selected.Contains(ex) && HasValidImage(ex))
                    selected.Add(ex);
            }
        }

        return selected;
    }

    /// <summary>
    /// Findet Beispiele fuer eine bestimmte Schadenskategorie.
    /// Nützlich wenn man gezielt aehnliche Schaeden zeigen will.
    /// </summary>
    public async Task<IReadOnlyList<FewShotExample>> GetExamplesForCategoryAsync(
        string category,
        int maxExamples = 2,
        CancellationToken ct = default)
    {
        await LoadAsync(ct);

        return _examples
            .Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Where(HasValidImage)
            .OrderByDescending(e => e.Quality)
            .Take(maxExamples)
            .ToList();
    }

    /// <summary>Absoluter Pfad zum Bild eines Beispiels.</summary>
    public string GetImagePath(FewShotExample example)
        => Path.Combine(KnowledgeRoot.GetRoot(), example.ImageRelativePath);

    /// <summary>Prueft ob das Bild noch existiert.</summary>
    public bool HasValidImage(FewShotExample example)
    {
        var path = GetImagePath(example);
        return File.Exists(path);
    }

    /// <summary>Laedt die Bilddaten eines Beispiels.</summary>
    public async Task<byte[]?> LoadImageAsync(FewShotExample example, CancellationToken ct = default)
    {
        var path = GetImagePath(example);
        if (!File.Exists(path)) return null;
        return await File.ReadAllBytesAsync(path, ct);
    }

    /// <summary>Entfernt ein Beispiel (inkl. Bild).</summary>
    public async Task RemoveAsync(string exampleId, CancellationToken ct = default)
    {
        await LoadAsync(ct);

        var ex = _examples.FirstOrDefault(e => e.Id == exampleId);
        if (ex == null) return;

        // Bild loeschen
        var imgPath = GetImagePath(ex);
        if (File.Exists(imgPath))
        {
            try { File.Delete(imgPath); } catch { }
        }

        _examples.Remove(ex);
        await SaveAsync(ct);
    }

    /// <summary>Statistik der Bibliothek.</summary>
    public string GetSummary()
    {
        if (_examples.Count == 0) return "Keine Beispiele";

        var cats = CategoryCounts;
        var parts = cats.OrderByDescending(kv => kv.Value)
                        .Take(8)
                        .Select(kv => $"{kv.Key}:{kv.Value}");
        return $"{_examples.Count} Beispiele ({string.Join(", ", parts)})";
    }
}
