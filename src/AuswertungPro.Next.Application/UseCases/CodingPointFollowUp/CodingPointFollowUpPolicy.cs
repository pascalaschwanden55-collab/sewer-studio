using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Nur in dieser Sitzung nachweislich erzeugte, unberuehrte KI-Punktvorschlaege
/// duerfen ihren Beleg verbessern. Nach Neuladen fehlt der Nachweis bewusst.</summary>
public static class CodingPointFollowUpPolicy
{
    public const double MaximumAnchorMeters = 1.0;
    public const double MaximumAnchorSeconds = 15;
    public const double MinimumConfidenceGain = 0.10;
    public const double MaximumSamLoss = 0.05;
    private sealed record State(CodingEvent Event, double AnchorMeter, TimeSpan AnchorTime,
        OverlayGeometry AnchorBox, string Fingerprint);
    private static readonly ConditionalWeakTable<CodingSession, Dictionary<Guid, State>> Sessions = new();

    public static void MarkHumanTouched(CodingEvent? ev)
    {
        // EventAdded wird vor dem Anhaengen des Modellkontexts ausgeloest.
        // Auch dort muessen echte Edit-/Fotoaktionen bereits sperren koennen.
        if (ev?.AiContext is null && ev?.Entry.Source == ProtocolEntrySource.Ai)
            ev.AiContext = new CodingEventAiContext { SuggestedCode = ev.Entry.Code };
        if (ev?.AiContext is { } context) context.HumanTouchedAtUtc ??= DateTimeOffset.UtcNow;
    }

    public static string Fingerprint(ProtocolEntry entry, OverlayGeometry? overlay,
        CodingEventAiContext? context, double meter, TimeSpan time)
    {
        try { return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { entry, overlay, context, meter, time }))); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException)
        { return ""; } // Unlesbarer Nachweis berechtigt nie zum Ersetzen.
    }

    private static string Fingerprint(CodingEvent ev)
        => Fingerprint(ev.Entry, ev.Overlay, ev.AiContext, ev.MeterAtCapture, ev.VideoTimestamp);

    public static void RegisterNew(CodingSession? session, CodingEvent ev, string expectedFingerprint)
    {
        if (string.IsNullOrEmpty(expectedFingerprint) || session is null || !session.Events.Contains(ev) || !IsUntouchedPoint(ev)
            || !CodingPointGeometry.HasBox(ev.Overlay) || !double.IsFinite(ev.MeterAtCapture)
            || ev.MeterAtCapture < 0 || ev.VideoTimestamp < TimeSpan.Zero
            || ev.Entry.CodeMeta?.Parameters.GetValueOrDefault("vsa.meter.quelle") == "geschaetzt"
            || !HasCompleteImageEvidence(ev.Entry, ev.AiContext!)
            || Fingerprint(ev) != expectedFingerprint) return;
        var states = Sessions.GetOrCreateValue(session);
        // Ein vorhandener Ursprung darf niemals nachtraeglich neu gepinnt werden.
        states.TryAdd(ev.EventId, new State(ev, ev.MeterAtCapture, ev.VideoTimestamp,
            Clone(ev.Overlay!), expectedFingerprint));
    }

    /// <summary>Raumgetrennte KI-Punkte blockieren sich nicht. Fehlende Geometrie,
    /// manuelle Eintraege, Strecken und Einmalcodes behalten ihre bisherigen Regeln.</summary>
    public static IReadOnlyList<CodingEvent> CoverageCandidates(CodingSession? session,
        IEnumerable<CodingEvent> events, string? code, double meter, OverlayGeometry? box)
    {
        Dictionary<Guid, State>? states = null;
        if (session is not null) Sessions.TryGetValue(session, out states);
        var result = events.Where(ev => !IsAiPoint(ev) || !CodingPointGeometry.HasBox(ev.Overlay)
            || !CodingPointGeometry.HasBox(box) || CodingPointGeometry.Matches(ev.Overlay, box))
            .Where(ev => states is null || !states.TryGetValue(ev.EventId, out var state)
                || !IsUntouchedPoint(ev) || Fingerprint(ev) != state.Fingerprint
                || Math.Abs(state.AnchorMeter - meter) < MaximumAnchorMeters).ToList();
        // Die Ruecknahme entfernt die Zeile aus der Liste. Innerhalb derselben Sitzung
        // bleibt sie als menschlich verworfener Beleg erhalten, statt wieder aufzutauchen.
        if (session is not null && states is not null)
            foreach (var state in states.Values)
                if (state.Event.AiContext?.HumanTouchedAtUtc is not null
                    && (state.Event.AiContext.Decision == CodingUserDecision.Rejected || !session.Events.Contains(state.Event))
                    && CodingDedupPolicy.CodesMatch(state.Event.Entry.Code, code)
                    && Math.Abs(state.AnchorMeter - meter) < MaximumAnchorMeters
                    && CodingPointGeometry.Matches(state.AnchorBox, box)
                    && result.All(ev => ev.EventId != state.Event.EventId)) result.Add(state.Event);
        return result;
    }

    public static string? ImprovementBlockReason(CodingSession? session, CodingEvent existing,
        ProtocolEntry entry, OverlayGeometry? box, CodingEventAiContext context, bool meterFromOsd,
        IReadOnlyList<CodingEvent> coverageCandidates)
    {
        if (session is null || !Sessions.TryGetValue(session, out var states)
            || !states.TryGetValue(existing.EventId, out var state) || !ReferenceEquals(state.Event, existing)
            || !session.Events.Contains(existing))
            return "kein Nachweis eines neuen Sitzungsvorschlags";
        if (!IsUntouchedPoint(existing) || Fingerprint(existing) != state.Fingerprint)
            return "menschlich bearbeitet oder seit Erstellung verändert";
        if (entry.Source != ProtocolEntrySource.Ai || entry.IsStreckenschaden || entry.IsDeleted
            || context.Decision != CodingUserDecision.Ignored || context.HumanTouchedAtUtc is not null
            || !string.Equals(existing.Entry.Code, entry.Code, StringComparison.OrdinalIgnoreCase))
            return "kein gleichartiger ungeprüfter Punktbeleg";
        if (!meterFromOsd || entry.MeterStart is not { } meter || !double.IsFinite(meter)
            || entry.Zeit is not { } time || meter < state.AnchorMeter
            || meter - state.AnchorMeter >= MaximumAnchorMeters
            || time < existing.VideoTimestamp || (time - state.AnchorTime).TotalSeconds > MaximumAnchorSeconds)
            return "Meterquelle oder festes Ursprungsfenster passt nicht";
        if (!CodingPointGeometry.Matches(state.AnchorBox, box)
            || !CodingPointGeometry.Matches(existing.Overlay, box)
            || coverageCandidates.Count(ev => CodingDedupPolicy.CodesMatch(ev.Entry.Code, entry.Code)
                && Math.Abs(ev.MeterAtCapture - meter) < MaximumAnchorMeters) != 1)
            return "keine eindeutige räumliche Zuordnung";
        var before = existing.AiContext!;
        if (context.ObservationHasTechnicalFailure
            || before.SamMaskImageWidth != context.SamMaskImageWidth
            || before.SamMaskImageHeight != context.SamMaskImageHeight)
            return "technischer Fehler oder anderer Bildraum";
        if (!CodingLocalizedDetection.IsSha256(context.SuggestedByModelSha256)
            || !string.Equals(before.SuggestedByModelSha256, context.SuggestedByModelSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(before.SuggestedByModelId, context.SuggestedByModelId, StringComparison.Ordinal)
            || existing.Entry.CodeMeta?.Parameters.GetValueOrDefault("ai.detector.purpose")
                != entry.CodeMeta?.Parameters.GetValueOrDefault("ai.detector.purpose"))
            return "Modellquelle ist nicht gleich und nachgewiesen";
        if (!ValidScore(before.Evidence?.YoloConf) || !ValidScore(context.Evidence?.YoloConf)
            || !ValidScore(before.Evidence?.SamMaskStability) || !ValidScore(context.Evidence?.SamMaskStability)
            || context.Evidence!.YoloConf!.Value - before.Evidence!.YoloConf!.Value < MinimumConfidenceGain - 1e-9
            || before.Evidence.SamMaskStability!.Value - context.Evidence.SamMaskStability!.Value > MaximumSamLoss + 1e-9
            || context.QualityGateLevel is null or "Red"
            || before.QualityGateLevel == "Green" && context.QualityGateLevel != "Green")
            return "kein ausreichend stärkerer Beleg bei erhaltener Maskenqualität";
        return null;
    }

    public static bool TryImprove(CodingSession? session, CodingEvent existing, ProtocolEntry entry,
        OverlayGeometry? box, CodingEventAiContext context, bool meterFromOsd,
        IReadOnlyList<CodingEvent> coverageCandidates, out string reason)
    {
        var block = ImprovementBlockReason(session, existing, entry, box, context, meterFromOsd, coverageCandidates);
        if (block is null && !HasCompleteImageEvidence(entry, context))
            block = "vollständiger neuer Bild-/Maskenbeleg fehlt";
        if (block is not null) { reason = block; return false; }

        var states = Sessions.GetOrCreateValue(session!);
        var state = states[existing.EventId];
        var previousContext = Clone(existing.AiContext!);
        previousContext.PreviousEvidence = [];
        context.PreviousEvidence = [.. existing.AiContext!.PreviousEvidence,
            new(ProtocolEntryCloner.CloneLegacyProtocolEntry(existing.Entry), Clone(existing.Overlay),
                previousContext, existing.MeterAtCapture, existing.VideoTimestamp)];
        entry.EntryId = existing.Entry.EntryId;
        existing.Entry = entry;
        existing.Overlay = box;
        existing.AiContext = context;
        existing.MeterAtCapture = entry.MeterStart!.Value;
        existing.VideoTimestamp = entry.Zeit!.Value;
        states[existing.EventId] = state with { Fingerprint = Fingerprint(existing) };
        reason = "stärkerer Folgebeleg; vorheriger Beleg archiviert";
        return true;
    }

    private static bool IsAiPoint(CodingEvent ev) => ev.Entry.Source == ProtocolEntrySource.Ai
        && !ev.Entry.IsStreckenschaden && !CodingDedupPolicy.IsOneTimeCode(ev.Entry.Code);
    private static bool IsUntouchedPoint(CodingEvent ev) => IsAiPoint(ev) && !ev.Entry.IsDeleted
        && ev.ReviewContext is null && ev.AiContext is { Decision: CodingUserDecision.Ignored, HumanTouchedAtUtc: null };
    private static bool ValidScore(double? score) => score is { } value && double.IsFinite(value) && value is >= 0 and <= 1;
    private static bool HasCompleteImageEvidence(ProtocolEntry entry, CodingEventAiContext context)
        => entry.FotoPaths.Count > 0 && entry.FotoPaths.All(path => !string.IsNullOrWhiteSpace(path))
            && !string.IsNullOrWhiteSpace(context.SamMaskRle)
            && context.SamMaskImageWidth is > 0 && context.SamMaskImageHeight is > 0;
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
