using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai;

// Phase 5.3 vorbereitend: LiveDetection nach Application/Ai/Vision/VideoAnalysisModels.cs.

public sealed class LiveDetectionService
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(45);

    private static readonly string Prompt = """
        Du siehst einen Frame aus einer Kanalinspektion (TV-Inspektion Abwasserkanal).
        Analysiere kurz:
        1. Lies den Meterstand aus dem OSD (On-Screen Display), falls sichtbar.
        2. Erkenne sichtbare Schaeden und markiere deren Position im Bild.

        WICHTIG zum Meterstand:
        - Der Meterstand steht UNTEN RECHTS im Bild als kleine Dezimalzahl (z.B. "2.64", "7.90", "14.98").
        - IGNORIERE alle grossen Zahlen im oberen Headertext (Knotennummern wie 74468, 80872 etc.).
        - IGNORIERE Datumsangaben und Dateipfade.
        - Der Meterstand ist IMMER kleiner als 500.
        - Falls kein Meterstand lesbar: meter = null

        Antworte NUR mit gueltigem JSON in diesem Format:
        {"meter": 12.5, "findings": [{"label": "Riss", "severity": 3, "position_clock": "3", "vsa_code_hint": "BAB", "extent_percent": 20, "bbox": [0.3, 0.2, 0.7, 0.6]}]}

        Falls kein Schaden: {"meter": null, "findings": []}
        severity: 1=kaum, 2=leicht, 3=mittel, 4=schwer, 5=kritisch
        position_clock: Uhrzeitlage (12=Scheitel, 6=Sohle, 3=rechts, 9=links)
        bbox: [x1, y1, x2, y2] normalisierte Koordinaten (0.0=links/oben, 1.0=rechts/unten).
          x1,y1 = linke obere Ecke, x2,y2 = rechte untere Ecke der Schadensregion im Bild.
          WICHTIG: bbox bezieht sich auf die Position des Schadens IM BILD, nicht auf die Rohrquerschnitts-Uhrposition.
          Falls Position unklar: bbox weglassen.
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

            // Use /api/chat (required for vision models like qwen3-vl)
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
                var rawMeter = mEl.GetDouble();
                // Plausibilitaet: Kanallaengen sind 0-500m, Knotennummern sind 5+ stellig
                if (rawMeter >= 0 && rawMeter <= 500)
                    meter = rawMeter;
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

                    // Bounding Box parsen (optionales Array [x1, y1, x2, y2], normalisiert 0-1)
                    double? bboxX1 = null, bboxY1 = null, bboxX2 = null, bboxY2 = null;
                    if (f.TryGetProperty("bbox", out var bboxArr) && bboxArr.ValueKind == JsonValueKind.Array)
                    {
                        var coords = new List<double>();
                        foreach (var c in bboxArr.EnumerateArray())
                        {
                            if (c.ValueKind == JsonValueKind.Number)
                                coords.Add(c.GetDouble());
                        }
                        if (coords.Count >= 4)
                        {
                            bboxX1 = Math.Clamp(coords[0], 0, 1);
                            bboxY1 = Math.Clamp(coords[1], 0, 1);
                            bboxX2 = Math.Clamp(coords[2], 0, 1);
                            bboxY2 = Math.Clamp(coords[3], 0, 1);
                        }
                    }

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
                        DiameterReductionMm: null,
                        BboxX1: bboxX1,
                        BboxY1: bboxY1,
                        BboxX2: bboxX2,
                        BboxY2: bboxY2));
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
