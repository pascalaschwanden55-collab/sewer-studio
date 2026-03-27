// AuswertungPro – Protokoll-gesteuerte Schadenverifikation
// Statt blinder Erkennung ("was siehst du?") wird die KI gezielt gefragt:
// "Laut Protokoll ist bei X.Xm ein Schaden YYY — bestaetige oder korrigiere."
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Ergebnis der Protokoll-gesteuerten Verifikation eines Frames.
/// </summary>
public sealed record GuidedVerificationResult(
    double? MeterReading,
    bool ProtocolDamageVisible,
    string ConfirmationLevel,       // "bestaetigt", "teilweise", "nicht_sichtbar", "anderer_schaden"
    string? ActualVsaCode,          // Was die KI tatsaechlich sieht
    string? ActualLabel,
    int ActualSeverity,
    string? ActualClock,
    int? ExtentPercent,
    string Explanation);

/// <summary>
/// Fragt die KI gezielt: "Das Protokoll sagt X — was siehst du?"
/// Viel hoehere Trefferquote als blinde Erkennung.
/// </summary>
public sealed class GuidedVerificationService
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(60);
    private readonly OllamaClient _client;
    private readonly string _model;

    public GuidedVerificationService(OllamaClient client, string visionModel)
    {
        _client = client;
        _model = visionModel;
    }

    /// <summary>
    /// Verifiziert einen Frame gegen einen Protokolleintrag.
    /// </summary>
    public async Task<GuidedVerificationResult> VerifyAsync(
        byte[] pngBytes,
        GroundTruthEntry protocolEntry,
        CancellationToken ct)
    {
        try
        {
            using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            frameCts.CancelAfter(FrameTimeout);

            string b64 = Convert.ToBase64String(pngBytes);
            string prompt = BuildPrompt(protocolEntry);

            var messages = new[]
            {
                new OllamaClient.ChatMessage("user", prompt, new[] { b64 })
            };

            var raw = await _client.ChatAsync(_model, messages, frameCts.Token)
                .ConfigureAwait(false);

            return ParseResponse(raw, protocolEntry);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return FallbackResult("Timeout");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FallbackResult(ex.Message);
        }
    }

    private static string BuildPrompt(GroundTruthEntry entry)
    {
        string codeDesc = GetCodeDescription(entry.VsaCode);
        string clockHint = !string.IsNullOrEmpty(entry.ClockPosition)
            ? $" bei {entry.ClockPosition} Uhr"
            : "";
        string streckeHint = entry.IsStreckenschaden
            ? $" (Streckenschaden von {entry.MeterStart:F1}m bis {entry.MeterEnd:F1}m)"
            : "";

        string clockLine = string.IsNullOrEmpty(entry.ClockPosition)
            ? "" : $"- Uhrzeigerposition: {entry.ClockPosition} Uhr\n";
        string charLine = entry.Characterization != null
            ? $"- Charakterisierung: {entry.Characterization}\n" : "";

        string jsonExample =
            "{\n" +
            "  \"meter\": 2.8,\n" +
            "  \"schaden_sichtbar\": true,\n" +
            "  \"bestaetigung\": \"bestaetigt\",\n" +
            $"  \"tatsaechlicher_code\": \"{entry.VsaCode}\",\n" +
            $"  \"tatsaechliches_label\": \"{codeDesc}\",\n" +
            "  \"schweregrad\": 3,\n" +
            $"  \"uhrzeiger\": \"{entry.ClockPosition ?? ""}\",\n" +
            "  \"querschnitt_prozent\": null,\n" +
            "  \"erklaerung\": \"Schaden gut sichtbar bei beschriebener Position\"\n" +
            "}";

        return $"""
            Du siehst einen Frame aus einer Kanalbefahrung (TV-Inspektion Abwasserkanal).

            PROTOKOLL-ANGABE: Bei {entry.MeterStart:F1}m{streckeHint} wurde ein Schaden codiert:
            - VSA-Code: {entry.VsaCode} = {codeDesc}
            - Beschreibung: {entry.Text}
            {clockLine}{charLine}
            AUFGABE:
            1. Lies den METERSTAND unten rechts im Bild (die Zahl in Metern, z.B. "2.80", "15.30").
               NICHT Knotennummern oder Header-Zahlen. Nur die Distanzanzeige (0-500m Bereich).
            2. Pruefe ob der beschriebene Schaden ({codeDesc}{clockHint}) im Bild sichtbar ist.
            3. Falls du einen anderen Schaden siehst, beschreibe diesen.

            Antworte NUR mit gueltigem JSON:
            {jsonExample}

            bestaetigung: "bestaetigt" | "teilweise" | "nicht_sichtbar" | "anderer_schaden"
            schweregrad: 1=kaum, 2=leicht, 3=mittel, 4=schwer, 5=kritisch
            """;
    }

    private static GuidedVerificationResult ParseResponse(string raw, GroundTruthEntry entry)
    {
        var json = ExtractJson(raw);
        if (string.IsNullOrWhiteSpace(json))
            return FallbackResult("Kein JSON in Antwort");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Meterstand
            double? meter = null;
            if (root.TryGetProperty("meter", out var mEl) && mEl.ValueKind == JsonValueKind.Number)
            {
                var v = mEl.GetDouble();
                if (v >= 0 && v <= 500) meter = v;
            }

            // Bestaetigung
            bool visible = root.TryGetProperty("schaden_sichtbar", out var sv)
                && sv.ValueKind == JsonValueKind.True;

            string confirmation = "nicht_sichtbar";
            if (root.TryGetProperty("bestaetigung", out var bEl) && bEl.ValueKind == JsonValueKind.String)
                confirmation = bEl.GetString()?.ToLowerInvariant() ?? "nicht_sichtbar";

            // Tatsaechlicher Code/Label
            string? actualCode = root.TryGetProperty("tatsaechlicher_code", out var tc)
                ? tc.GetString()?.Trim() : null;
            string? actualLabel = root.TryGetProperty("tatsaechliches_label", out var tl)
                ? tl.GetString()?.Trim() : null;

            int severity = root.TryGetProperty("schweregrad", out var sev) && sev.ValueKind == JsonValueKind.Number
                ? Math.Clamp(sev.GetInt32(), 1, 5) : 2;

            string? clock = root.TryGetProperty("uhrzeiger", out var clk)
                ? clk.GetString()?.Trim() : null;

            int? extent = root.TryGetProperty("querschnitt_prozent", out var ext) && ext.ValueKind == JsonValueKind.Number
                ? ext.GetInt32() : null;

            string explanation = root.TryGetProperty("erklaerung", out var expl)
                ? expl.GetString() ?? "" : "";

            return new GuidedVerificationResult(
                MeterReading: meter,
                ProtocolDamageVisible: visible,
                ConfirmationLevel: confirmation,
                ActualVsaCode: actualCode,
                ActualLabel: actualLabel,
                ActualSeverity: severity,
                ActualClock: clock,
                ExtentPercent: extent,
                Explanation: explanation);
        }
        catch
        {
            return FallbackResult("JSON-Parse-Fehler");
        }
    }

    private static GuidedVerificationResult FallbackResult(string reason) =>
        new(null, false, "nicht_sichtbar", null, null, 0, null, null, reason);

    private static string? ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var m = Regex.Match(raw, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        if (m.Success) return m.Groups[1].Value;
        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start >= 0 && end > start) return raw[start..(end + 1)];
        return null;
    }

    /// <summary>VSA-Code Kurzbeschreibungen fuer den Prompt.</summary>
    private static string GetCodeDescription(string vsaCode)
    {
        string baseCode = vsaCode.Split('.')[0].ToUpperInvariant();
        return baseCode switch
        {
            // Bauliche Schaeden (BA*)
            "BAA" => "Verformung",
            "BAB" => "Riss/Bruch",
            "BAC" => "Rohrbruch/Einsturz",
            "BAD" => "Oberflaechenschaden",
            "BAE" => "Verschobene Verbindung",
            "BAF" => "Scherbenbildung",
            "BAG" => "Fehlende Wandteile",
            "BAH" => "Korrosion",
            "BAI" => "Poroese Rohrwand",
            "BAJ" => "Schadhafter Anschluss",
            "BAK" => "Einragender Anschluss",
            "BAL" => "Verschobene Rohrverbindung",
            "BAM" => "Klaffende Rohrverbindung",
            "BAN" => "Mechanischer Verschleiss",
            "BAO" => "Fehlstelle",
            // Betriebliche Schaeden (BB*)
            "BBA" => "Wurzeleinwuchs",
            "BBB" => "Inkrustation/Ablagerung hart",
            "BBC" => "Ablagerung fein",
            "BBD" => "Anhaftungen",
            "BBE" => "Verstopfung/Pfropfen",
            "BBF" => "Eingewachsener Dichtring",
            "BBG" => "Sichtbare Undichtheit",
            // Inventar (BC*)
            "BCA" => "Anschluss",
            "BCB" => "Seitlicher Anschluss",
            "BCC" => "Scheitelanschluss",
            // Sonderfaelle (BD*)
            "BDA" => "Hindernis",
            "BDB" => "Eindringen von Erdreich",
            "BDC" => "Eindringen anderes Material",
            "BDD" => "Nagertiere/Ungeziefer",
            "BDE" => "Sonstiger Schaden",
            "BDBD" => "Eindringen von Boden/Erdreich",
            // Allgemein
            _ => baseCode.Length >= 2 ? baseCode[1] switch
            {
                'A' => $"Baulicher Schaden ({baseCode})",
                'B' => $"Betrieblicher Schaden ({baseCode})",
                'C' => $"Inventar ({baseCode})",
                'D' => $"Sonderbefund ({baseCode})",
                _ => $"Schadenscode {baseCode}"
            } : $"Code {baseCode}"
        };
    }
}
