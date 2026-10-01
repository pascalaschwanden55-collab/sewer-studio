using System.Globalization;
using System.Net;
using System.Text;

namespace CodingReplay;

internal static class ReplayVideoReport
{
    internal static void Write(
        string output,
        ReplayVideoPackage package,
        IReadOnlyList<ReplayVideoFrameObservation> frames,
        ReplayVideoFinalization? finalization,
        string? interruption)
    {
        var events = finalization?.Events
            ?? frames.LastOrDefault()?.Events
            ?? [];
        var comparisons = CompareReferences(package, events);
        var assignedIds = comparisons
            .Where(row => row.Event is not null)
            .Select(row => row.Event!.EventId)
            .ToHashSet();
        var duplicateIds = comparisons.SelectMany(row => row.DuplicateEventIds).ToHashSet();
        var unassessed = events
            .Where(ev => !assignedIds.Contains(ev.EventId) && !duplicateIds.Contains(ev.EventId))
            .ToArray();
        var sessionIds = frames.Select(frame => frame.Trace.GetValueOrDefault("session"))
            .Where(value => value?.StartsWith("persistent_video_session:", StringComparison.Ordinal) == true)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var meterSources = frames.GroupBy(frame => frame.MeterSource)
            .ToDictionary(group => group.Key, group => group.Count());

        ReplayFiles.WriteJson(Path.Combine(output, "video-summary.json"), new
        {
            Status = interruption is null && frames.Count == package.Frames.Count
                ? frames.Any(frame => frame.TechnicalError is not null)
                    ? "completed_with_technical_errors"
                    : "completed_diagnostic"
                : "incomplete",
            package.Holding,
            PlannedFrames = package.Frames.Count,
            RecordedFrames = frames.Count,
            PersistentSessionIds = sessionIds,
            OneSessionWithoutReset = sessionIds.Length == 1,
            MeterSources = meterSources,
            FrameOutcomes = frames.GroupBy(frame => frame.Outcome)
                .ToDictionary(group => group.Key, group => group.Count()),
            TechnicalErrorFrames = frames.Count(frame => frame.TechnicalError is not null),
            DetectorQualifiedFrames = frames.Count(frame =>
                frame.Trace.GetValueOrDefault("detector_qualified") == "True"),
            DetectorUnqualifiedFrames = frames.Count(frame =>
                frame.Trace.GetValueOrDefault("detector_qualified") == "False"),
            FinalEvents = events,
            ReferenceAssessment = comparisons,
            UnassessedAdditionalEvents = unassessed,
            AssessmentScope = "only_existing_human_confirmed_reference_events_not_full_video_truth",
            AssessedReferenceCount = package.References.Count,
            PrecisionRecallReported = false,
            finalization?.CanExit,
            finalization?.OpenStretchCount,
            finalization?.TerminalBoundaryPresent,
            Interruption = interruption,
            ProductionRelease = false,
            CandidateActivated = false,
            TrainingStarted = false
        });

        WriteHtml(output, package, frames, events, comparisons, unassessed, sessionIds, meterSources, interruption);
    }

    private static IReadOnlyList<ReferenceAssessment> CompareReferences(
        ReplayVideoPackage package,
        IReadOnlyList<ReplayVideoEventSnapshot> events)
    {
        var used = new HashSet<Guid>();
        var rows = new List<ReferenceAssessment>(package.References.Count);
        foreach (var reference in package.References.OrderBy(item => item.TimestampSeconds))
        {
            var nearby = events
                .Where(ev => ev.MeterStart.HasValue
                             && Math.Abs(ev.MeterStart.Value - reference.Meter)
                             <= package.ReferenceMatchToleranceM)
                .OrderBy(ev => CodeRank(reference.Code, ev.Code))
                .ThenBy(ev => Math.Abs(ev.MeterStart!.Value - reference.Meter))
                .ThenBy(ev => Math.Abs(ev.VideoTimestampSeconds - reference.TimestampSeconds))
                .ToArray();
            var selected = nearby.FirstOrDefault(ev => !used.Contains(ev.EventId));
            if (selected is not null)
                used.Add(selected.EventId);
            var duplicates = selected is null
                ? []
                : nearby.Where(ev => ev.EventId != selected.EventId
                                     && SameMainCode(reference.Code, ev.Code))
                    .Select(ev => ev.EventId)
                    .ToArray();
            var status = selected is null
                ? "reference_not_found"
                : string.Equals(reference.Code, selected.Code, StringComparison.OrdinalIgnoreCase)
                    ? "exact_code"
                    : SameMainCode(reference.Code, selected.Code)
                        ? "same_main_code"
                        : "different_code_at_reference_location";
            rows.Add(new ReferenceAssessment(
                reference,
                status,
                selected,
                selected?.MeterStart - reference.Meter,
                selected?.VideoTimestampSeconds - reference.TimestampSeconds,
                duplicates));
        }
        return rows;
    }

    private static void WriteHtml(
        string output,
        ReplayVideoPackage package,
        IReadOnlyList<ReplayVideoFrameObservation> frames,
        IReadOnlyList<ReplayVideoEventSnapshot> events,
        IReadOnlyList<ReferenceAssessment> comparisons,
        IReadOnlyList<ReplayVideoEventSnapshot> unassessed,
        IReadOnlyList<string?> sessionIds,
        IReadOnlyDictionary<string, int> meterSources,
        string? interruption)
    {
        string H(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? "–");
        string N(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "–";
        var html = new StringBuilder("<!doctype html><html lang='de'><meta charset='utf-8'><meta name='viewport' content='width=device-width'>");
        html.Append("<title>SewerStudio – Videonachlauf</title><style>body{font:16px system-ui;background:#eef2f6;color:#162331;margin:32px auto;max-width:1200px;padding:0 20px}section{background:#fff;border-radius:12px;padding:20px;margin:18px 0}table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:9px;border-bottom:1px solid #d8e0e8;vertical-align:top}.notice{padding:16px;background:#fff4ce;border-radius:8px}.ok{color:#176b3a}.bad{color:#9a3412}code{overflow-wrap:anywhere}details{margin:8px 0}pre{white-space:pre-wrap;font-size:12px}</style>");
        html.Append($"<h1>Videonachlauf {H(package.Holding)}</h1>");
        html.Append($"<p><strong>{frames.Count} von {package.Frames.Count} festgelegten Bildern protokolliert.</strong> Sitzung(en): {sessionIds.Count}. Ereignisse am Ende: {events.Count}.</p>");
        var technicalErrorCount = frames.Count(frame => frame.TechnicalError is not null);
        if (technicalErrorCount > 0)
            html.Append($"<p class='notice'><strong>{technicalErrorCount} Frame(s) haben einen technischen Fehler.</strong> Fehlende Vorschläge oder Referenztreffer sind deshalb kein belegter Negativbefund.</p>");
        html.Append($"<p class='notice'>Bewertet werden nur die {package.References.Count} vorhandenen, menschlich bestätigten Referenzereignisse. Weitere Zeilen bleiben unbewertet. Dieser Bericht berechnet keine Precision oder Recall und gibt kein Modell frei.</p>");
        if (interruption is not null)
            html.Append($"<p class='notice'>Lauf unvollständig: {H(interruption)}</p>");

        html.Append("<section><h2>Meterherkunft</h2><table><tr><th>Quelle</th><th>Frames</th></tr>");
        foreach (var source in meterSources)
            html.Append($"<tr><td>{H(source.Key)}</td><td>{source.Value}</td></tr>");
        html.Append("</table></section>");

        html.Append($"<section><h2>Bestätigte Ereignisanker ({package.References.Count})</h2><table><tr><th>Referenz</th><th>Ergebnis</th><th>Abweichung</th><th>Beleg</th></tr>");
        foreach (var row in comparisons)
        {
            html.Append($"<tr><td>{H(row.Reference.Code)} bei {N(row.Reference.Meter)} m / {N(row.Reference.TimestampSeconds)} s<br>Review {H(row.Reference.ReviewId)}, {H(row.Reference.ReviewedBy)}</td>");
            html.Append($"<td>{H(row.Status)}<br>{H(row.Event?.Code)}</td>");
            html.Append($"<td>{N(row.MeterDelta)} m<br>{N(row.TimeDeltaSeconds)} s</td>");
            html.Append($"<td>Event {H(row.Event?.EventId)}<br>Meterquelle {H(row.Event?.MeterSource)}<br>Herkunft {H(row.Event?.Origin)}<br>Dubletten: {row.DuplicateEventIds.Count}</td></tr>");
        }
        html.Append("</table></section>");

        html.Append("<section><h2>Zusätzliche Ereigniszeilen</h2>");
        if (unassessed.Count == 0)
            html.Append("<p>Keine zusätzlichen Zeilen.</p>");
        else
        {
            html.Append("<table><tr><th>Code</th><th>Ort / Zeit</th><th>Status</th><th>Herkunft</th></tr>");
            foreach (var ev in unassessed)
                html.Append($"<tr><td>{H(ev.Code)}</td><td>{N(ev.MeterStart)}–{N(ev.MeterEnd)} m / {N(ev.VideoTimestampSeconds)} s</td><td>{H(ev.ReviewState)} / {H(ev.QualityGate)}</td><td>{H(ev.MeterSource)} / {H(ev.Origin)}</td></tr>");
            html.Append("</table><p>Diese Zeilen sind nicht als richtig oder falsch bewertet.</p>");
        }
        html.Append("</section>");

        html.Append("<section><h2>Frame-Protokoll</h2>");
        foreach (var frame in frames)
        {
            html.Append($"<details><summary>{N(frame.TimestampSeconds)} s – {H(frame.Outcome)} – {N(frame.Meter)} m ({H(frame.MeterSource)}) – {frame.EventChanges.Count} Änderung(en)</summary>");
            html.Append($"<pre>{H(System.Text.Json.JsonSerializer.Serialize(frame, ReplayFiles.Json))}</pre></details>");
        }
        html.Append("</section></html>");
        ReplayFiles.WriteNew(Path.Combine(output, "video-bericht.html"), Encoding.UTF8.GetBytes(html.ToString()));
    }

    private static int CodeRank(string expected, string actual)
        => string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)
            ? 0
            : SameMainCode(expected, actual) ? 1 : 2;

    private static bool SameMainCode(string left, string right)
        => left.Length >= 3 && right.Length >= 3
           && string.Equals(left[..3], right[..3], StringComparison.OrdinalIgnoreCase);

    private sealed record ReferenceAssessment(
        ReplayVideoReference Reference,
        string Status,
        ReplayVideoEventSnapshot? Event,
        double? MeterDelta,
        double? TimeDeltaSeconds,
        IReadOnlyList<Guid> DuplicateEventIds);
}
