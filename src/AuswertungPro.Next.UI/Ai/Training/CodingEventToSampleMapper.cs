using System;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Mappt CodingEvents (User-Entscheidungen im Codiermodus) auf TrainingSamples.
/// Schliesst den Feedback-Loop: KI-Vorschlag → User-Entscheidung → Trainingsdaten.
/// </summary>
public static class CodingEventToSampleMapper
{
    /// <summary>
    /// Mappt CodingUserDecision auf TrainingSampleStatus.
    /// Accepted/AcceptedWithEdit → Approved (bestaetigt als korrekt).
    /// Rejected → Rejected (KI lag falsch).
    /// Ignored → New (keine Aussage).
    /// </summary>
    public static TrainingSampleStatus MapDecision(CodingUserDecision decision) => decision switch
    {
        CodingUserDecision.Accepted => TrainingSampleStatus.Approved,
        CodingUserDecision.AcceptedWithEdit => TrainingSampleStatus.Approved,
        CodingUserDecision.Rejected => TrainingSampleStatus.Rejected,
        CodingUserDecision.Ignored => TrainingSampleStatus.New,
        _ => TrainingSampleStatus.New
    };

    /// <summary>
    /// Erstellt ein TrainingSample aus einem CodingEvent.
    /// Enthaelt den finalen Code (nach User-Korrektur), Meter-Position und KI-Kontext.
    /// </summary>
    /// <param name="ev">Das Codier-Ereignis mit Entry + AiContext.</param>
    /// <param name="caseId">Fall-ID (HaltungName oder Video-Dateiname).</param>
    /// <param name="framePath">Optionaler Pfad zum Frame-Bild.</param>
    public static TrainingSample FromCodingEvent(CodingEvent ev, string caseId, string? framePath)
    {
        var status = ev.AiContext != null
            ? MapDecision(ev.AiContext.Decision)
            : TrainingSampleStatus.Approved; // Rein manuelle Eingabe = implizit korrekt

        var sourceType = ev.Overlay != null
            ? SourceTypeNames.TeacherAnnotation
            : SourceTypeNames.VideoTimestamp;

        var meterStart = Math.Round(ev.Entry.MeterStart ?? ev.MeterAtCapture, 1);
        var meterEnd = Math.Round(ev.Entry.MeterEnd ?? ev.MeterAtCapture, 1);

        return new TrainingSample
        {
            SampleId = ev.EventId.ToString("N")[..12],
            CaseId = caseId,
            Code = ev.Entry.Code,
            Beschreibung = ev.Entry.Beschreibung,
            MeterStart = meterStart,
            MeterEnd = meterEnd,
            IsStreckenschaden = ev.Entry.IsStreckenschaden,
            TimeSeconds = ev.VideoTimestamp.TotalSeconds,
            FramePath = framePath ?? string.Empty,
            Status = status,
            SourceType = sourceType,
            KiCode = ev.AiContext?.SuggestedCode,
            MatchLevel = ev.AiContext != null
                ? DetermineMatchLevel(ev.Entry.Code, ev.AiContext)
                : MatchLevelNames.ExactMatch, // Manuelle Eingabe = Ground Truth
            Notes = ev.AiContext?.Reason ?? string.Empty,
            Signature = BuildSignature(caseId, ev.Entry.Code, meterStart, meterEnd),
            // BBox aus OverlayGeometry extrahieren (Eingabemarker-Rechteck)
            BboxXCenter = ExtractBboxField(ev.Overlay, bboxCenter: true, isX: true),
            BboxYCenter = ExtractBboxField(ev.Overlay, bboxCenter: true, isX: false),
            BboxWidth = ExtractBboxField(ev.Overlay, bboxCenter: false, isX: true),
            BboxHeight = ExtractBboxField(ev.Overlay, bboxCenter: false, isX: false)
        };
    }

    /// <summary>
    /// Extrahiert BBox-Felder aus OverlayGeometry.
    /// Unterstuetzt sowohl 2-Punkt (Eingabemarker: TopLeft+BottomRight)
    /// als auch 4-Punkt (OverlayToolService: 4 Ecken) Rechtecke.
    /// Berechnet BoundingBox ueber Min/Max aller Punkte fuer Robustheit.
    /// </summary>
    private static double? ExtractBboxField(
        Domain.Models.OverlayGeometry? overlay, bool bboxCenter, bool isX)
    {
        if (overlay?.Points == null || overlay.Points.Count < 2) return null;
        if (overlay.ToolType != Domain.Models.OverlayToolType.Rectangle) return null;

        // BoundingBox ueber Min/Max aller Punkte (robust fuer 2 oder 4 Punkte)
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in overlay.Points)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        double w = maxX - minX;
        double h = maxY - minY;
        if (w <= 0 || h <= 0) return null;

        if (bboxCenter)
            return isX ? minX + w / 2.0 : minY + h / 2.0;
        else
            return isX ? w : h;
    }

    /// <summary>Delegiert an die zentrale Signatur-Methode auf TrainingSample.</summary>
    private static string BuildSignature(string caseId, string code, double meterCenter, double meterEnd)
        => TrainingSample.BuildCanonicalSignature(caseId, code, meterCenter, meterEnd);

    /// <summary>
    /// Bestimmt das MatchLevel basierend auf User-Entscheidung und Code-Vergleich.
    /// </summary>
    private static string DetermineMatchLevel(string finalCode, CodingEventAiContext ai)
    {
        return ai.Decision switch
        {
            CodingUserDecision.Accepted => MatchLevelNames.ExactMatch,
            CodingUserDecision.AcceptedWithEdit => MatchLevelNames.PartialMatch,
            CodingUserDecision.Rejected => MatchLevelNames.Mismatch,
            CodingUserDecision.Ignored => MatchLevelNames.NoFindings,
            _ => MatchLevelNames.NoFindings
        };
    }
}
