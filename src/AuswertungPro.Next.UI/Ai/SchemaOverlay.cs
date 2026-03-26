using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

// ═══════════════════════════════════════════════════════════════════
// Schema-Overlay System: Interaktive Schablonen fuer Rohrvermessung
// Gemeinsame Basis fuer PipeBend, Fuellstand und Einragung.
// ═══════════════════════════════════════════════════════════════════

/// <summary>Zustand eines Schema-Overlays.</summary>
public enum SchemaPhase
{
    None,       // Kein Schema aktiv
    Placing,    // Erster Klick platziert das Schema
    Adjusting,  // Schema platziert, Handles aktiv (Drag/Rotate/Scale)
    Confirmed   // Bestaetigt, Wert uebernommen
}

/// <summary>Typ des Schema-Overlays.</summary>
public enum SchemaType
{
    PipeBend,      // Winkel-Schablone (°)
    FillLevel,     // Fuellstand-Zunge (% Querschnitt)
    Intrusion      // Einragung BAI (% Querschnittsreduktion)
}

/// <summary>
/// Ein Handle am Schema das per Drag bewegt werden kann.
/// </summary>
public sealed record SchemaHandle(
    string Id,           // z.B. "vertex", "arm1", "arm2", "radius", "level", "width"
    NormalizedPoint Position,
    string Cursor = "SizeAll" // WPF Cursor-Typ
);

/// <summary>
/// Abstrakte Basis fuer alle Schema-Overlays.
/// Jedes Schema hat: Center, Handles, Rendering-Daten, Ergebnis.
/// </summary>
public abstract class SchemaOverlayBase
{
    public SchemaPhase Phase { get; protected set; } = SchemaPhase.None;
    public NormalizedPoint Center { get; set; } = new(0.5, 0.5);

    // Rohr-Referenz (aus Kalibrierung)
    public double PipeRadius { get; set; } = 0.35; // normalisiert
    public NormalizedPoint PipeCenter { get; set; } = new(0.5, 0.5);

    /// <summary>Schema am Klickpunkt platzieren.</summary>
    public virtual void Place(NormalizedPoint clickPos)
    {
        Center = clickPos;
        Phase = SchemaPhase.Adjusting;
    }

    /// <summary>Alle draggbaren Handles zurueckgeben.</summary>
    public abstract IReadOnlyList<SchemaHandle> GetHandles();

    /// <summary>Handle per Drag bewegen.</summary>
    public abstract void DragHandle(string handleId, NormalizedPoint mousePos);

    /// <summary>Schema bestaetigen → OverlayGeometry zurueck.</summary>
    public abstract OverlayGeometry Confirm();

    /// <summary>Schema abbrechen.</summary>
    public virtual void Cancel() => Phase = SchemaPhase.None;

    /// <summary>Schema auf Defaults zuruecksetzen (R-Taste).</summary>
    public abstract void Reset();

    /// <summary>Ergebnis-Text fuer Label (z.B. "45°" oder "15%").</summary>
    public abstract string ResultLabel { get; }

    /// <summary>Kalibrierung anwenden.</summary>
    public void ApplyCalibration(PipeCalibration? calibration)
    {
        if (calibration == null) return;
        PipeRadius = calibration.NormalizedDiameter / 2.0;
        PipeCenter = calibration.PipeCenter;
    }
}

// ═══════════════════════════════════════════════════════════════════
// 1. PipeBend: Winkel-Schablone
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Interaktive Bogen-Schablone: Vertex + zwei Schenkel → Winkel in Grad.
/// Handles: vertex (verschieben), arm1/arm2 (Winkel), radius (Groesse).
/// </summary>
public sealed class PipeBendSchema : SchemaOverlayBase
{
    public double AngleDeg { get; set; } = 45;
    public double RotationDeg { get; set; } // 0 = nach rechts
    public double ArmLength { get; set; } = 0.15; // normalisiert
    public bool SnapEnabled { get; set; }

    public override void Place(NormalizedPoint clickPos)
    {
        Center = clickPos;
        AngleDeg = 45;
        RotationDeg = -90; // Nach oben zeigend
        ArmLength = 0.15;
        Phase = SchemaPhase.Adjusting;
    }

    public override IReadOnlyList<SchemaHandle> GetHandles()
    {
        var (arm1, arm2) = GetArmEndpoints();
        var radiusHandle = GetRadiusHandle();
        return new[]
        {
            new SchemaHandle("vertex", Center, "SizeAll"),
            new SchemaHandle("arm1", arm1, "Cross"),
            new SchemaHandle("arm2", arm2, "Cross"),
            new SchemaHandle("radius", radiusHandle, "SizeNS")
        };
    }

    public override void DragHandle(string handleId, NormalizedPoint mousePos)
    {
        switch (handleId)
        {
            case "vertex":
                Center = mousePos;
                break;
            case "arm1":
            {
                // Arm1 ziehen: Winkel UND Rotation aendern
                double dx = mousePos.X - Center.X, dy = mousePos.Y - Center.Y;
                double mouseAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                // Arm2-Winkel beibehalten, neuen Winkel aus Differenz berechnen
                double arm2Angle = RotationDeg + AngleDeg / 2.0;
                double newAngle = arm2Angle - mouseAngle;
                // Winkel normalisieren auf 0..360, dann clampen
                while (newAngle < 0) newAngle += 360;
                while (newAngle > 360) newAngle -= 360;
                if (newAngle > 180) newAngle = 360 - newAngle;
                AngleDeg = Math.Clamp(newAngle, 5, 175);
                RotationDeg = arm2Angle - AngleDeg / 2.0;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0.02) ArmLength = dist;
                break;
            }
            case "arm2":
            {
                // Arm2 ziehen: Winkel UND Rotation aendern
                double dx = mousePos.X - Center.X, dy = mousePos.Y - Center.Y;
                double mouseAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                // Arm1-Winkel beibehalten, neuen Winkel aus Differenz berechnen
                double arm1Angle = RotationDeg - AngleDeg / 2.0;
                double newAngle = mouseAngle - arm1Angle;
                while (newAngle < 0) newAngle += 360;
                while (newAngle > 360) newAngle -= 360;
                if (newAngle > 180) newAngle = 360 - newAngle;
                AngleDeg = Math.Clamp(newAngle, 5, 175);
                RotationDeg = arm1Angle + AngleDeg / 2.0;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0.02) ArmLength = dist;
                break;
            }
            case "radius":
            {
                double dx = mousePos.X - Center.X, dy = mousePos.Y - Center.Y;
                ArmLength = Math.Clamp(Math.Sqrt(dx * dx + dy * dy), 0.03, 0.45);
                break;
            }
        }
    }

    public override OverlayGeometry Confirm()
    {
        Phase = SchemaPhase.Confirmed;
        double finalAngle = SnapEnabled ? SnapAngle(AngleDeg) : Math.Round(AngleDeg, 1);
        var (arm1, arm2) = GetArmEndpoints();
        return new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            Points = new List<NormalizedPoint> { Center, arm1, arm2 },
            ArcDegrees = finalAngle
        };
    }

    public override void Reset()
    {
        AngleDeg = 45;
        RotationDeg = -90;
        ArmLength = 0.15;
    }

    public override string ResultLabel => $"{AngleDeg:F0}\u00b0";

    /// <summary>Winkel per +/- Taste aendern.</summary>
    public void AdjustAngle(double delta) =>
        AngleDeg = Math.Clamp(AngleDeg + delta, 5, 175);

    /// <summary>Auf Standardwinkel snappen.</summary>
    public void Snap() => AngleDeg = SnapAngle(AngleDeg);

    public (NormalizedPoint arm1, NormalizedPoint arm2) GetArmEndpoints()
    {
        double rad1 = (RotationDeg - AngleDeg / 2.0) * Math.PI / 180.0;
        double rad2 = (RotationDeg + AngleDeg / 2.0) * Math.PI / 180.0;
        return (
            new NormalizedPoint(Center.X + Math.Cos(rad1) * ArmLength, Center.Y + Math.Sin(rad1) * ArmLength),
            new NormalizedPoint(Center.X + Math.Cos(rad2) * ArmLength, Center.Y + Math.Sin(rad2) * ArmLength));
    }

    public NormalizedPoint GetRadiusHandle()
    {
        double rad = RotationDeg * Math.PI / 180.0;
        return new NormalizedPoint(Center.X + Math.Cos(rad) * ArmLength, Center.Y + Math.Sin(rad) * ArmLength);
    }

    private static double SnapAngle(double angle)
    {
        ReadOnlySpan<double> standards = stackalloc double[] { 15, 30, 45, 60, 90, 120, 135, 150 };
        double best = standards[0], bestDelta = Math.Abs(angle - best);
        for (int i = 1; i < standards.Length; i++)
        {
            double delta = Math.Abs(angle - standards[i]);
            if (delta < bestDelta) { best = standards[i]; bestDelta = delta; }
        }
        return best;
    }
}

// ═══════════════════════════════════════════════════════════════════
// 2. FillLevel: Fuellstand-Zunge (Wasserstand / Sediment)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Interaktive Fuellstand-Zunge: Ein V-foermiges Dreieck von der Sohle
/// nach oben, projiziert in den Rohrquerschnitt.
/// Handles: level (Hoehe), width (Breite der Zunge).
/// Ergebnis: FillPercent (Kreissegment-%).
/// </summary>
public sealed class FillLevelSchema : SchemaOverlayBase
{
    /// <summary>Fuellhoehe als Anteil des Durchmessers (0.0 = leer, 1.0 = voll).</summary>
    public double FillRatio { get; set; } = 0.2;

    /// <summary>Ablagerung, Wasser oder Hindernis.</summary>
    public LevelMode Mode { get; set; } = LevelMode.Water;

    public override void Place(NormalizedPoint clickPos)
    {
        // Klick bestimmt die Fuellhoehe relativ zum Rohr
        Center = PipeCenter;
        double sohle = PipeCenter.Y + PipeRadius;
        double scheitel = PipeCenter.Y - PipeRadius;

        if (Mode == LevelMode.Obstacle)
        {
            // Von oben: Klick-Y relativ zu Scheitel
            FillRatio = Math.Clamp((clickPos.Y - scheitel) / (PipeRadius * 2.0), 0.02, 0.98);
        }
        else
        {
            // Von unten: Klick-Y relativ zu Sohle
            FillRatio = Math.Clamp((sohle - clickPos.Y) / (PipeRadius * 2.0), 0.02, 0.98);
        }
        Phase = SchemaPhase.Adjusting;
    }

    public override IReadOnlyList<SchemaHandle> GetHandles()
    {
        var levelPos = GetLevelLineY();
        return new[]
        {
            new SchemaHandle("level", new NormalizedPoint(PipeCenter.X, levelPos), "SizeNS")
        };
    }

    public override void DragHandle(string handleId, NormalizedPoint mousePos)
    {
        if (handleId != "level") return;
        double sohle = PipeCenter.Y + PipeRadius;
        double scheitel = PipeCenter.Y - PipeRadius;

        if (Mode == LevelMode.Obstacle)
            FillRatio = Math.Clamp((mousePos.Y - scheitel) / (PipeRadius * 2.0), 0.02, 0.98);
        else
            FillRatio = Math.Clamp((sohle - mousePos.Y) / (PipeRadius * 2.0), 0.02, 0.98);
    }

    public override OverlayGeometry Confirm()
    {
        Phase = SchemaPhase.Confirmed;
        double pct = CircleSegmentPercent(FillRatio);
        double levelY = GetLevelLineY();

        // Berechne die Schnittbreite des Kreises auf Hoehe levelY
        double dy = levelY - PipeCenter.Y;
        double halfChord = Math.Sqrt(Math.Max(0, PipeRadius * PipeRadius - dy * dy));

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            Points = new List<NormalizedPoint>
            {
                new(PipeCenter.X - halfChord, levelY),
                new(PipeCenter.X + halfChord, levelY)
            },
            FillPercent = Math.Round(pct, 1),
            LevelSubMode = Mode
        };
    }

    public override void Reset() => FillRatio = 0.2;
    public override string ResultLabel => $"{CircleSegmentPercent(FillRatio):F0}%";

    /// <summary>Y-Position der Fuellstand-Linie (normalisiert).</summary>
    public double GetLevelLineY()
    {
        double sohle = PipeCenter.Y + PipeRadius;
        double scheitel = PipeCenter.Y - PipeRadius;
        return Mode == LevelMode.Obstacle
            ? scheitel + FillRatio * PipeRadius * 2.0
            : sohle - FillRatio * PipeRadius * 2.0;
    }

    /// <summary>Berechnet den Kreissegment-Anteil (%) fuer gegebene Fuellhoehe.</summary>
    private static double CircleSegmentPercent(double hRatio)
    {
        // h = Fuellhoehe relativ zum Durchmesser (0..1)
        // theta = 2 * arccos(1 - 2h)
        // A_segment / A_circle = (theta - sin(theta)) / (2*pi)
        double h = Math.Clamp(hRatio, 0, 1);
        if (h <= 0) return 0;
        if (h >= 1) return 100;
        double theta = 2.0 * Math.Acos(1.0 - 2.0 * h);
        return (theta - Math.Sin(theta)) / (2.0 * Math.PI) * 100.0;
    }
}

// ═══════════════════════════════════════════════════════════════════
// 3. Intrusion: Einragung BAI (Querschnittsreduktion %)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Interaktive Einragungs-Markierung: Der User markiert wie weit das
/// Dichtungsmaterial in den Rohrquerschnitt hineinragt.
/// Handles: depth (Einragungstiefe), clock (Uhrposition).
/// Ergebnis: Q1 = Einragungstiefe in mm, DnRatioPercent = % des DN.
/// </summary>
public sealed class IntrusionSchema : SchemaOverlayBase
{
    /// <summary>Einragungstiefe als Anteil des Radius (0.0 = am Rand, 1.0 = bis Mitte).</summary>
    public double DepthRatio { get; set; } = 0.15;

    /// <summary>Uhrposition der Einragung (0-12).</summary>
    public double ClockHour { get; set; } = 9;

    /// <summary>Winkelbreite der Einragung in Grad.</summary>
    public double SpreadDeg { get; set; } = 30;

    public override void Place(NormalizedPoint clickPos)
    {
        Center = PipeCenter;
        // Uhrposition aus Klick-Position berechnen
        double dx = clickPos.X - PipeCenter.X;
        double dy = clickPos.Y - PipeCenter.Y;
        double angleRad = Math.Atan2(dx, -dy); // 12 Uhr = oben
        double angleDeg = angleRad * 180.0 / Math.PI;
        if (angleDeg < 0) angleDeg += 360;
        ClockHour = angleDeg / 30.0;

        // Tiefe aus Distanz zum Rohrrand
        double distToCenter = Math.Sqrt(dx * dx + dy * dy);
        double distToEdge = PipeRadius - distToCenter;
        DepthRatio = Math.Clamp(distToEdge / PipeRadius, 0.05, 0.8);

        Phase = SchemaPhase.Adjusting;
    }

    public override IReadOnlyList<SchemaHandle> GetHandles()
    {
        var tip = GetIntrusionTip();
        var edge = GetEdgePoint();
        return new[]
        {
            new SchemaHandle("depth", tip, "Cross"),
            new SchemaHandle("edge", edge, "SizeAll")
        };
    }

    public override void DragHandle(string handleId, NormalizedPoint mousePos)
    {
        double dx = mousePos.X - PipeCenter.X;
        double dy = mousePos.Y - PipeCenter.Y;

        if (handleId == "depth")
        {
            // Tiefe und Uhrposition gleichzeitig anpassen
            double distToCenter = Math.Sqrt(dx * dx + dy * dy);
            DepthRatio = Math.Clamp((PipeRadius - distToCenter) / PipeRadius, 0.05, 0.8);

            double angleRad = Math.Atan2(dx, -dy);
            double angleDeg = angleRad * 180.0 / Math.PI;
            if (angleDeg < 0) angleDeg += 360;
            ClockHour = angleDeg / 30.0;
        }
        else if (handleId == "edge")
        {
            // Breite anpassen
            double angleRad = Math.Atan2(dx, -dy);
            double angleDeg = angleRad * 180.0 / Math.PI;
            if (angleDeg < 0) angleDeg += 360;
            double mainAngle = ClockHour * 30.0;
            double delta = Math.Abs(angleDeg - mainAngle);
            if (delta > 180) delta = 360 - delta;
            SpreadDeg = Math.Clamp(delta * 2.0, 10, 120);
        }
    }

    public override OverlayGeometry Confirm()
    {
        Phase = SchemaPhase.Confirmed;
        // Q1 = Einragungstiefe in % des Durchmessers
        double reductionPercent = DepthRatio * 100.0;
        var tip = GetIntrusionTip();
        var edge = GetEdgePoint();

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Level, // Wiederverwendung: Level mit Obstacle-Modus
            Points = new List<NormalizedPoint> { edge, tip, PipeCenter },
            FillPercent = Math.Round(reductionPercent, 1),
            LevelSubMode = LevelMode.Obstacle,
            ClockFrom = Math.Round(ClockHour, 1)
        };
    }

    public override void Reset()
    {
        DepthRatio = 0.15;
        SpreadDeg = 30;
    }

    public override string ResultLabel => $"{DepthRatio * 100:F0}% DN";

    /// <summary>Spitze der Einragung (wie weit sie ins Rohr ragt).</summary>
    public NormalizedPoint GetIntrusionTip()
    {
        double angleDeg = ClockHour * 30.0 - 90; // 12h = oben
        double angleRad = angleDeg * Math.PI / 180.0;
        double r = PipeRadius * (1.0 - DepthRatio);
        return new NormalizedPoint(
            PipeCenter.X + Math.Cos(angleRad) * r,
            PipeCenter.Y + Math.Sin(angleRad) * r);
    }

    /// <summary>Punkt am Rohrrand (Einragungs-Ursprung).</summary>
    public NormalizedPoint GetEdgePoint()
    {
        double angleDeg = ClockHour * 30.0 - 90;
        double angleRad = angleDeg * Math.PI / 180.0;
        return new NormalizedPoint(
            PipeCenter.X + Math.Cos(angleRad) * PipeRadius,
            PipeCenter.Y + Math.Sin(angleRad) * PipeRadius);
    }

    /// <summary>Eckpunkte des Einragungs-Bereichs (fuer Rendering).</summary>
    public (NormalizedPoint left, NormalizedPoint right) GetSpreadEdges()
    {
        double mainAngle = ClockHour * 30.0 - 90;
        double halfSpread = SpreadDeg / 2.0;
        double radL = (mainAngle - halfSpread) * Math.PI / 180.0;
        double radR = (mainAngle + halfSpread) * Math.PI / 180.0;
        return (
            new NormalizedPoint(PipeCenter.X + Math.Cos(radL) * PipeRadius, PipeCenter.Y + Math.Sin(radL) * PipeRadius),
            new NormalizedPoint(PipeCenter.X + Math.Cos(radR) * PipeRadius, PipeCenter.Y + Math.Sin(radR) * PipeRadius));
    }
}

// ═══════════════════════════════════════════════════════════════════
// Schema-Manager: Verwaltet das aktive Schema
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Verwaltet das aktive Schema-Overlay. Wird vom PlayerWindow genutzt
/// fuer Hit-Testing, Drag-Handling und Rendering.
/// </summary>
public sealed class SchemaOverlayManager
{
    private SchemaOverlayBase? _active;
    private string? _dragHandle;

    public SchemaOverlayBase? Active => _active;
    public bool IsActive => _active?.Phase == SchemaPhase.Adjusting;
    public bool IsDragging => _dragHandle != null;

    /// <summary>Neues Schema aktivieren.</summary>
    public void Activate(SchemaOverlayBase schema, PipeCalibration? calibration = null)
    {
        _active = schema;
        _active.ApplyCalibration(calibration);
        _dragHandle = null;
    }

    /// <summary>Schema am Klickpunkt platzieren.</summary>
    public void Place(NormalizedPoint clickPos)
    {
        _active?.Place(clickPos);
    }

    /// <summary>Hit-Test: Welches Handle ist am naechsten zur Mausposition?</summary>
    public string? HitTest(NormalizedPoint mousePos, double threshold = 0.025)
    {
        if (_active?.Phase != SchemaPhase.Adjusting) return null;
        string? closest = null;
        double closestDist = threshold;
        foreach (var handle in _active.GetHandles())
        {
            double dx = mousePos.X - handle.Position.X;
            double dy = mousePos.Y - handle.Position.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = handle.Id;
            }
        }
        return closest;
    }

    /// <summary>Drag starten.</summary>
    public void BeginDrag(string handleId)
    {
        _dragHandle = handleId;
    }

    /// <summary>Drag fortsetzen.</summary>
    public void UpdateDrag(NormalizedPoint mousePos)
    {
        if (_dragHandle == null || _active == null) return;
        _active.DragHandle(_dragHandle, mousePos);
    }

    /// <summary>Drag beenden.</summary>
    public void EndDrag()
    {
        _dragHandle = null;
    }

    /// <summary>Schema bestaetigen → OverlayGeometry.</summary>
    public OverlayGeometry? Confirm()
    {
        var result = _active?.Confirm();
        return result;
    }

    /// <summary>Alles abbrechen.</summary>
    public void Cancel()
    {
        _active?.Cancel();
        _active = null;
        _dragHandle = null;
    }

    /// <summary>Schema auf Defaults zuruecksetzen.</summary>
    public void Reset()
    {
        _active?.Reset();
    }
}
