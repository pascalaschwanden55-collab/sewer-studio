using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai;

public sealed record FrameFinding(
    double? Meter,
    IReadOnlyList<string> Findings,
    string Severity,
    string? Raw
);

public sealed class OllamaVisionFindingsService
{
    private readonly OllamaClient _client;
    private readonly string _model;

    public OllamaVisionFindingsService(OllamaClient client, string model)
    {
        _client = client;
        _model = model;
    }

    public async Task<FrameFinding> AnalyzeAsync(string framePngBase64, CancellationToken ct)
    {
        var prompt =
            "Du analysierst ein Kanal-TV Frame (Kanalinspektion).\n" +
            "Erkenne nur sichtbare Schäden/Anomalien: Riss, Infiltration, Wurzeleinwuchs, Ablagerung, Versatz, Korrosion, Einragung, Fremdkörper, Scherben, Einbruch, Deformation, offene Stösse.\n" +
            "Lies den Meterstand aus dem Bild, falls sichtbar (z.B. 18.40 m).\n" +
            "Gib AUSSCHLIESSLICH gültiges JSON zurück (keine Erklärung):\n" +
            "{\n" +
            "  \"meter\": 18.4 | null,\n" +
            "  \"findings\": [\"Riss\", \"Infiltration\"],\n" +
            "  \"severity\": \"low\"|\"mid\"|\"high\"\n" +
            "}\n" +
            "Wenn nichts erkennbar: findings=[], severity=\"low\".";

        var raw = await _client.GenerateAsync(_model, prompt, new[] { framePngBase64 }, ct).ConfigureAwait(false);
        var json = TryExtractFirstJsonObject(raw) ?? "{}";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double? meter = null;
            if (root.TryGetProperty("meter", out var m) && m.ValueKind is JsonValueKind.Number)
                meter = m.GetDouble();

            var findings = new List<string>();
            if (root.TryGetProperty("findings", out var f) && f.ValueKind == JsonValueKind.Array)
            {
                foreach (var it in f.EnumerateArray())
                {
                    if (it.ValueKind == JsonValueKind.String)
                        findings.Add(it.GetString() ?? string.Empty);
                }
            }

            var severity = root.TryGetProperty("severity", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? "low"
                : "low";

            return new FrameFinding(meter, findings, severity, raw);
        }
        catch
        {
            return new FrameFinding(null, Array.Empty<string>(), "low", raw);
        }
    }

    private static string? TryExtractFirstJsonObject(string raw)
    {
        var rx = new Regex(@"{[\s\S]*?}");
        var m = rx.Match(raw);
        return m.Success ? m.Value : null;
    }
}
