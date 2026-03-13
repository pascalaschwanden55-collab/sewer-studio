using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetection(
    double TimestampSeconds,
    IReadOnlyList<LiveFrameFinding> Findings,
    double? MeterReading,
    string? Error);

public sealed class LiveDetectionService
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(45);

    private static readonly string Prompt = """
        Du siehst einen Frame aus einer Kanalinspektion (TV-Inspektion Abwasserkanal).
        Analysiere kurz:
        1. Lies den METERSTAND: Das ist die Zahl UNTEN RECHTS im Bild (z.B. "0.00", "12.50", "7.90").
           NICHT die Knotennummern, Schachtnummern oder andere Zahlen im Headertext oben.
           Der Meterstand zeigt die gefahrene Distanz in Metern (typisch 0-500m).
           Werte ueber 500 sind KEINE Meterstaende sondern Knotennummern — ignoriere diese.
        2. Erkenne sichtbare Schaeden im Kanalrohr.

        Antworte NUR mit gueltigem JSON in diesem Format:
        {"meter": 12.5, "findings": [{"label": "Riss", "severity": 3, "position_clock": "3", "vsa_code_hint": "BAB", "extent_percent": 20}]}

        Falls kein Schaden: {"meter": 0.0, "findings": []}
        severity: 1=kaum, 2=leicht, 3=mittel, 4=schwer, 5=kritisch
        position_clock: Uhrzeitlage (12=Scheitel, 6=Sohle, 3=rechts, 9=links)
        """;

    private readonly OllamaClient _client;
    private readonly string _model;

    public LiveDetectionService(OllamaClient client, string visionModel)
    {
        _client = client;
        _model = visionModel;
    }

    public async Task<LiveDetection> AnalyzeFrameAsync(
        byte[] pngBytes,
        double timestampSeconds,
        CancellationToken ct)
    {
        try
        {
            using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            frameCts.CancelAfter(FrameTimeout);

            var b64 = Convert.ToBase64String(pngBytes);

            // Use /api/chat (required for vision models like qwen2.5vl)
            var messages = new[]
            {
                new OllamaClient.ChatMessage("user", Prompt, new[] { b64 })
            };
            var raw = await _client.ChatAsync(
                _model, messages, frameCts.Token).ConfigureAwait(false);

            return ParseResponse(raw, timestampSeconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new LiveDetection(timestampSeconds, Array.Empty<LiveFrameFinding>(),
                null, "Timeout");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new LiveDetection(timestampSeconds, Array.Empty<LiveFrameFinding>(),
                null, ex.Message);
        }
    }

    private static LiveDetection ParseResponse(string raw, double timestampSeconds)
    {
        // Extract JSON from potentially wrapped response (```json ... ``` or plain)
        var json = ExtractJson(raw);
        if (string.IsNullOrWhiteSpace(json))
            return new LiveDetection(timestampSeconds, Array.Empty<LiveFrameFinding>(), null, null);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double? meter = null;
            if (root.TryGetProperty("meter", out var mEl) && mEl.ValueKind == JsonValueKind.Number)
            {
                var raw_meter = mEl.GetDouble();
                // Plausibilitaet: Meterstand muss zwischen 0 und 500 liegen
                // Werte > 500 sind Knotennummern die faelschlich als Meter gelesen wurden
                if (raw_meter >= 0 && raw_meter <= 500)
                    meter = raw_meter;
            }

            var findings = new List<LiveFrameFinding>();
            if (root.TryGetProperty("findings", out var fArr) && fArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in fArr.EnumerateArray())
                {
                    var label = f.TryGetProperty("label", out var lbl) ? lbl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(label)) continue;

                    var severity = f.TryGetProperty("severity", out var sev) && sev.ValueKind == JsonValueKind.Number
                        ? Math.Clamp(sev.GetInt32(), 1, 5) : 2;

                    var clock = f.TryGetProperty("position_clock", out var clk) ? clk.ToString() : null;
                    var extent = f.TryGetProperty("extent_percent", out var ext) && ext.ValueKind == JsonValueKind.Number
                        ? (int?)ext.GetInt32() : null;
                    var vsaHint = f.TryGetProperty("vsa_code_hint", out var vsa) ? vsa.GetString() : null;
                    var heightMm = f.TryGetProperty("height_mm", out var hm) && hm.ValueKind == JsonValueKind.Number
                        ? (int?)hm.GetInt32() : null;
                    var widthMm = f.TryGetProperty("width_mm", out var wm) && wm.ValueKind == JsonValueKind.Number
                        ? (int?)wm.GetInt32() : null;
                    var intrusion = f.TryGetProperty("intrusion_percent", out var ip) && ip.ValueKind == JsonValueKind.Number
                        ? (int?)ip.GetInt32() : null;

                    findings.Add(new LiveFrameFinding(
                        Label: label!.Trim(),
                        Severity: severity,
                        PositionClock: clock?.Trim(),
                        ExtentPercent: extent,
                        VsaCodeHint: vsaHint?.Trim(),
                        HeightMm: heightMm,
                        WidthMm: widthMm,
                        IntrusionPercent: intrusion,
                        CrossSectionReductionPercent: null,
                        DiameterReductionMm: null));
                }
            }

            return new LiveDetection(timestampSeconds, findings, meter, null);
        }
        catch
        {
            // JSON parse failed — return empty
            return new LiveDetection(timestampSeconds, Array.Empty<LiveFrameFinding>(), null, null);
        }
    }

    private static string? ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Try to find JSON block in ```json ... ``` or ``` ... ```
        var m = Regex.Match(raw, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        if (m.Success)
            return m.Groups[1].Value;

        // Try to find raw JSON object
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
            return raw[start..(end + 1)];

        return null;
    }
}
