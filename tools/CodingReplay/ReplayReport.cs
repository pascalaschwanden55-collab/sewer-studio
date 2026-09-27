using System.Net;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.UseCases.CodingReplay;
using AuswertungPro.Next.Infrastructure.Ai;

namespace CodingReplay;

internal static class ReplayReport
{
    internal static void Write(string output, string packageFolder, IReadOnlyList<ReplayCase> cases,
        IReadOnlyList<CodingReplayResult> results, string? interruption)
    {
        string H(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? "–");
        string CodeText(string code) => VsaCodeResolver.LookupLabel(code) is { Length: > 0 } label ? $"{label} ({code})" : code;
        var measured = results.Count(r => r.Status == "measured");
        var comparisons = cases.Select(c => new { c.Input.Id, Comparison = CodingReplayComparer.Compare(c.ExpectedCode, results.SingleOrDefault(r => r.Id == c.Input.Id)) }).ToArray();
        var html = new StringBuilder("<!doctype html><html lang='de'><meta charset='utf-8'><meta name='viewport' content='width=device-width'>");
        html.Append("<title>SewerStudio – Bildvergleich</title><style>body{font:17px system-ui;background:#eef2f6;color:#162331;margin:32px auto;max-width:1150px;padding:0 20px}article{background:white;border-radius:12px;padding:20px;margin:20px 0;display:grid;grid-template-columns:minmax(260px,45%) 1fr;gap:22px}img{width:100%;height:auto}h1{font-size:30px}h2{font-size:21px;margin-top:0}pre{white-space:pre-wrap;overflow-wrap:anywhere;font-size:13px}p{line-height:1.5}.notice{padding:18px;background:#fff4ce;border-radius:8px}@media(max-width:750px){article{display:block}}</style>");
        html.Append("<h1>Bildvergleich des Codiermodus</h1><p>Mehrmodell-Einzelbildweg mit den bestehenden Player-Regeln. Jeder Fall beginnt mit einer leeren Sitzung.</p>");
        html.Append($"<p><strong>{measured} von {cases.Count} Bildern technisch gemessen.</strong> {results.Count - measured} nicht auswertbar; {cases.Count - results.Count} noch nicht ausgeführt.</p>");
        html.Append("<p class='notice'>Bekannte historische Prüfbilder, keine unabhängige Freigabe. Nicht gemessen: laufendes Video, Bildauswahl, Bedienung, Vorabdurchlauf und Zusammenführung über mehrere Bilder. Zusätzliche Codes sind Prüfhinweise, keine automatisch bewiesenen Fehlalarme. Keine Modelle trainiert oder aktiviert.</p>");
        if (results.Any(r => r.Observation?.Trace.GetValueOrDefault("detector_qualified") == "False"))
            html.Append("<p class='notice'>Der alte YOLO-Detektor ist im Programm nicht freigegeben. Er wurde deshalb nicht als Filter benutzt. Die Ergebnisse benötigen weiterhin eine manuelle Prüfung.</p>");
        html.Append("<ul>");
        foreach (var group in comparisons.GroupBy(c => c.Comparison))
            html.Append($"<li>{group.Count()}: {H(ComparisonText(group.Key))}</li>");
        html.Append("</ul><p>Gezählt werden Bilder und ihre Codevorschläge. Das ist keine Trefferquote für ganze Videoereignisse.</p>");
        if (interruption is not null) html.Append($"<p class='notice'>Lauf nicht vollständig: {H(interruption)}</p>");
        foreach (var c in cases)
        {
            var result = results.SingleOrDefault(r => r.Id == c.Input.Id);
            var events = result?.Observation?.Events;
            var expected = c.ExpectedCode == "LEER" ? "Kein BA-/BB-Schaden bestätigt" : CodeText(c.ExpectedCode);
            var observed = result?.Status != "measured" ? "Nicht auswertbar" : events is null || events.Count == 0 ? "Kein Ereignisvorschlag" : string.Join("; ", events.Select(e => CodeText(e.Code)));
            var fullImage = Path.GetFullPath(Path.Combine(packageFolder, c.Image));
            var relativeImage = Path.GetRelativePath(output, fullImage);
            var imageUrl = Path.IsPathRooted(relativeImage) ? new Uri(fullImage).AbsoluteUri
                : string.Join("/", relativeImage.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
            html.Append($"<article><div><img loading='lazy' src='{H(imageUrl)}' alt='Prüfbild {H(c.Input.Id)}'></div><div><h2>Haltung {H(c.Holding)}</h2><p><strong>Deine Referenz:</strong> {H(expected)}<br><strong>Programmvorschläge:</strong> {H(observed)}</p>");
            html.Append($"<p><strong>{H(ComparisonText(comparisons.Single(r => r.Id == c.Input.Id).Comparison))}</strong><br>Hinweis: {H(result?.Error)}<br>Laufzeit: {H(result is null ? null : $"{result.ElapsedMilliseconds / 1000:F1} s")}</p>");
            if (File.Exists(Path.Combine(output, "osd-inputs", c.Input.Id + ".png")))
                html.Append($"<details><summary>Das Bild für den Meterleser</summary><img loading='lazy' src='osd-inputs/{H(c.Input.Id)}.png' alt='Eingabe für die Metererkennung'></details>");
            html.Append($"<details><summary>Schritte und Nachweise</summary><pre>{H(JsonSerializer.Serialize(new { c.Input, c.ContextNote, result?.Observation }, ReplayFiles.Json))}</pre></details></div></article>");
        }
        html.Append("</html>");
        ReplayFiles.WriteNew(Path.Combine(output, "bericht.html"), Encoding.UTF8.GetBytes(html.ToString()));
        ReplayFiles.WriteJson(Path.Combine(output, "summary.json"), new
        {
            Status = interruption is null && results.Count == cases.Count ? (measured == cases.Count ? "completed_diagnostic" : "completed_with_errors") : "incomplete",
            Planned = cases.Count, Measured = measured, Unmeasured = cases.Count - measured,
            CodeComparison = comparisons.GroupBy(c => c.Comparison).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ComparisonScope = "visible_code_proposals_per_image_not_event_recall",
            Interruption = interruption, ProductionRelease = false, TrainingStarted = false
        });
    }

    private static string ComparisonText(CodingReplayComparison comparison) => comparison switch
    {
        CodingReplayComparison.ExactCodePresent => "Dein genauer Code ist unter den Vorschlägen",
        CodingReplayComparison.SameFamilyPresent => "Die passende Hauptgruppe ist dabei; der genaue Code fehlt",
        CodingReplayComparison.ReferenceNotProposed => "Dein Befund wurde nicht als passender Code vorgeschlagen",
        CodingReplayComparison.NoDamageCodeProposed => "Kein BA-/BB-Schadenscode vorgeschlagen",
        CodingReplayComparison.DamageCodeOnNegative => "Schadensvorschlag auf einem von dir negativ beurteilten Bild",
        CodingReplayComparison.ReviewRequired => "Das Programm verlangt eine manuelle Prüfung; kein negatives Ergebnis",
        _ => "Nicht auswertbar oder noch nicht ausgeführt"
    };
}
