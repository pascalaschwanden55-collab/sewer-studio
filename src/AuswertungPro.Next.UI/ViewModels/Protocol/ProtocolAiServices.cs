using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

/// <summary>
/// Input für KI-Auswertung.
/// - AllowedCodes = alle zulässigen VSA-Schadencodes (dein Katalog)
/// - VideoPathAbs/Zeit = falls verfügbar: Frame aus Video ziehen
/// - ImagePathsAbs = optional: vorhandene Fotos/Frames
/// - ExistingCode/ExistingText = Operator-Eingaben (für QC / Verbesserungsvorschläge)
/// </summary>
public sealed record AiInput(
    string ProjectFolderAbs,
    string? HaltungId,
    double? Meter,
    string? ExistingCode,
    string? ExistingText,
    IReadOnlyList<string> AllowedCodes,
    string? VideoPathAbs = null,
    TimeSpan? Zeit = null,
    IReadOnlyList<string>? ImagePathsAbs = null,
    string? XtfSnippet = null
);

public interface IProtocolAiService
{
    Task<AiSuggestion?> SuggestAsync(AiInput input, CancellationToken ct = default);
}

public interface IAiAllowedCodeCatalogProvider
{
    IReadOnlyList<string> GetAllowedCodes();
}

/// <summary>
/// Lädt ein Code-Katalog-JSON (User/Seed) oder fällt auf Auto-Scan zurück.
/// 
/// Erwartetes JSON-Format:
/// {
///   "codes": ["BAF", "BAG", ...]
/// }
/// </summary>
public sealed class DefaultCodeCatalogProvider : IAiAllowedCodeCatalogProvider
{
    private readonly string? _catalogPath;
    private readonly string? _fallbackScanFolder;
    private IReadOnlyList<string>? _cached;

    public DefaultCodeCatalogProvider(string? catalogPath = null, string? fallbackScanFolder = null)
    {
        _catalogPath = catalogPath;
        _fallbackScanFolder = fallbackScanFolder;
    }

    public IReadOnlyList<string> GetAllowedCodes()
    {
        if (_cached != null)
            return _cached;

        // 1) expliziter JSON-Katalog
        if (!string.IsNullOrWhiteSpace(_catalogPath) && File.Exists(_catalogPath))
        {
            try
            {
                var json = File.ReadAllText(_catalogPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("codes", out var codesEl) && codesEl.ValueKind == JsonValueKind.Array)
                {
                    var list = codesEl.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s!.Trim().ToUpperInvariant())
                        .Distinct()
                        .OrderBy(s => s)
                        .ToList();

                    if (list.Count > 0)
                        return _cached = list;
                }
            }
            catch
            {
                // ignore -> fallback scan
            }
        }

        // 2) Auto-Scan (z.B. Data/classification_*.json)
        if (!string.IsNullOrWhiteSpace(_fallbackScanFolder) && Directory.Exists(_fallbackScanFolder))
        {
            var rx = new Regex("\"(?<c>[A-Z]{3})\"", RegexOptions.Compiled);
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(_fallbackScanFolder, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var txt = File.ReadAllText(file);
                    foreach (Match m in rx.Matches(txt))
                    {
                        var c = m.Groups["c"].Value;
                        // sehr konservativ: 3 Grossbuchstaben
                        if (c.Length == 3)
                            set.Add(c);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            var list = set.OrderBy(x => x).ToList();
            if (list.Count > 0)
                return _cached = list;
        }

        // 3) Letzter Fallback: leer (dann darf KI NICHT codieren)
        return _cached = Array.Empty<string>();
    }
}

public sealed class NoopProtocolAiService : IProtocolAiService
{
    public Task<AiSuggestion?> SuggestAsync(AiInput input, CancellationToken ct = default)
        => Task.FromResult<AiSuggestion?>(null);
}
