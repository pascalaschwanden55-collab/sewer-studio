using System;
using System.Collections.ObjectModel;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Verschiebt bzw. kopiert eine Befund-Kachel (<see cref="CodingEvent"/>) zwischen den beiden
/// Spalten des Abgleich-Panels (KI-Befunde ↔ Import). Reine Datenoperation, unit-testbar.
/// Kopieren erzeugt einen Deep-Clone mit NEUER EventId/EntryId, damit sich Original und Kopie
/// nicht ueber gleiche IDs im Abgleich/Highlighting stoeren.
/// </summary>
public static class CodingEventColumnTransfer
{
    /// <summary>Verschiebt <paramref name="ev"/> aus <paramref name="source"/> nach
    /// <paramref name="target"/> (nach Meter einsortiert). Gibt das verschobene Event zurueck.</summary>
    public static CodingEvent Move(
        CodingEvent ev,
        ObservableCollection<CodingEvent> source,
        ObservableCollection<CodingEvent> target)
    {
        source.Remove(ev);
        InsertSorted(target, ev);
        return ev;
    }

    /// <summary>Fuegt einen Deep-Clone von <paramref name="ev"/> in <paramref name="target"/> ein
    /// (Original bleibt). Gibt den Clone zurueck.</summary>
    public static CodingEvent Copy(CodingEvent ev, ObservableCollection<CodingEvent> target)
    {
        var clone = CloneWithNewIds(ev);
        InsertSorted(target, clone);
        return clone;
    }

    /// <summary>Deep-Clone mit neuen IDs (Entry inkl. Fotos/CodeMeta, Overlay, AiContext).</summary>
    public static CodingEvent CloneWithNewIds(CodingEvent ev)
    {
        return new CodingEvent
        {
            EventId = Guid.NewGuid(),
            MeterAtCapture = ev.MeterAtCapture,
            VideoTimestamp = ev.VideoTimestamp,
            Entry = CloneEntry(ev.Entry),
            Overlay = CloneOverlay(ev.Overlay),
            AiContext = CloneAiContext(ev.AiContext),
        };
    }

    private static void InsertSorted(ObservableCollection<CodingEvent> target, CodingEvent ev)
    {
        var index = target.Count;
        for (var i = 0; i < target.Count; i++)
        {
            if (ev.MeterAtCapture < target[i].MeterAtCapture)
            {
                index = i;
                break;
            }
        }
        target.Insert(index, ev);
    }

    private static ProtocolEntry CloneEntry(ProtocolEntry e)
        => new()
        {
            EntryId = Guid.NewGuid(),
            Code = e.Code,
            Beschreibung = e.Beschreibung,
            MeterStart = e.MeterStart,
            MeterEnd = e.MeterEnd,
            IsStreckenschaden = e.IsStreckenschaden,
            Mpeg = e.Mpeg,
            Zeit = e.Zeit,
            FotoPaths = e.FotoPaths.ToList(),
            Source = e.Source,
            CodeMeta = CloneCodeMeta(e.CodeMeta),
        };

    private static ProtocolEntryCodeMeta? CloneCodeMeta(ProtocolEntryCodeMeta? m)
        => m is null ? null : new ProtocolEntryCodeMeta
        {
            Code = m.Code,
            Parameters = new System.Collections.Generic.Dictionary<string, string>(m.Parameters, StringComparer.OrdinalIgnoreCase),
            Severity = m.Severity,
            Count = m.Count,
            Notes = m.Notes,
            UpdatedAt = m.UpdatedAt,
        };

    private static OverlayGeometry? CloneOverlay(OverlayGeometry? o)
        => o is null ? null : new OverlayGeometry
        {
            GeometryId = Guid.NewGuid(),
            ToolType = o.ToolType,
            Points = o.Points.Select(p => new NormalizedPoint { X = p.X, Y = p.Y }).ToList(),
            Q1Mm = o.Q1Mm,
            Q2Mm = o.Q2Mm,
            ClockFrom = o.ClockFrom,
            ClockTo = o.ClockTo,
            ArcDegrees = o.ArcDegrees,
            DnRatioPercent = o.DnRatioPercent,
            FillPercent = o.FillPercent,
            LevelSubMode = o.LevelSubMode,
            EllipseRadiusXMm = o.EllipseRadiusXMm,
            EllipseRadiusYMm = o.EllipseRadiusYMm,
            SnapshotPath = o.SnapshotPath,
        };

    private static CodingEventAiContext? CloneAiContext(CodingEventAiContext? a)
        => a is null ? null : new CodingEventAiContext
        {
            SuggestedCode = a.SuggestedCode,
            Confidence = a.Confidence,
            Reason = a.Reason,
            Decision = a.Decision,
            QualityGateLevel = a.QualityGateLevel,
            Evidence = CodingEventEvidenceMapper.Clone(a.Evidence),
            SamMaskRle = a.SamMaskRle,
            SamMaskImageWidth = a.SamMaskImageWidth,
            SamMaskImageHeight = a.SamMaskImageHeight,
        };
}
