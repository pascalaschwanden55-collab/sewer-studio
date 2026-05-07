# PhotoMeasurement Bogen + Anschluss — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die Bogen- und Anschluss-Werkzeuge im PhotoMeasurementWindow durch die neuen Mockup-Designs ersetzen — Roentgenbild-Wurm (Bogen) und Drag-to-Place (Anschluss).

**Architecture:** Neue `BendWormSchema` und `LateralDragSchema` Klassen im bestehenden SchemaOverlay-System (`SchemaOverlayBase`). Das PhotoMeasurementWindow bekommt neue Render-Methoden die den Schema-Zustand auf den WPF-Canvas zeichnen. Die bestehenden Slider-basierten `DrawBendOverlay`/`DrawLateralOverlay` Methoden werden ersetzt.

**Tech Stack:** WPF/.NET 8, Canvas-Rendering mit Path/Ellipse/Line, normierte Koordinaten (0.0-1.0), PipeCalibration fuer mm-Umrechnung.

**Referenz-Mockup:** `docs/superpowers/specs/photo-measurement-mockup.html` — interaktives HTML das die Zieldarstellung zeigt.

---

## Bestandscode-Kontext

| Datei | Zweck |
|-------|-------|
| `src/.../Views/Windows/PhotoMeasurementWindow.xaml.cs` | Hauptfenster, 1563 Zeilen, Canvas-Rendering |
| `src/.../Views/Windows/PhotoMeasurementWindow.xaml` | XAML mit Toolbar, Canvas, Slider-Panel |
| `src/.../Ai/SchemaOverlay.cs` | Schema-System: `SchemaOverlayBase`, `PipeBendSchema` |
| `src/.../Ai/OverlayToolService.cs` | Geometrie-Builder, Kalibrierung |
| `src/.../Domain/Models/CodingSession.cs` | `OverlayGeometry`, `NormalizedPoint`, `OverlayToolType` |

## Datei-Uebersicht

| Aktion | Datei | Aufgabe |
|--------|-------|---------|
| **Neu** | `src/.../Ai/BendWormSchema.cs` | Bogen-Wurm Schema (Geometrie + State) |
| **Neu** | `src/.../Ai/LateralDragSchema.cs` | Anschluss-Drag Schema (Geometrie + State) |
| **Neu** | `src/.../Ai/BendWormRenderer.cs` | WPF-Canvas-Rendering fuer Bogen-Wurm |
| **Neu** | `src/.../Ai/LateralDragRenderer.cs` | WPF-Canvas-Rendering fuer Anschluss |
| **Aendern** | `src/.../Views/Windows/PhotoMeasurementWindow.xaml` | Slider-Panel fuer Bogen/Anschluss neu |
| **Aendern** | `src/.../Views/Windows/PhotoMeasurementWindow.xaml.cs` | Schema-Integration, alte Draw-Methoden ersetzen |
| **Aendern** | `src/.../Ai/SchemaOverlay.cs` | ggf. SchemaType-Enum erweitern |
| **Test** | `tests/.../BendWormSchemaTests.cs` | Geometrie-Tests |
| **Test** | `tests/.../LateralDragSchemaTests.cs` | Uhrlage-Berechnung, DN-Messung |

---

### Task 1: BendWormSchema — Geometrie und State

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/BendWormSchema.cs`
- Test: `tests/AuswertungPro.Next.Tests/Ai/BendWormSchemaTests.cs`

Schema-Klasse die den Wurm-Zustand haelt. Kein Rendering — nur Geometrie-Berechnung.

- [ ] **Step 1: Test-Datei erstellen**

```csharp
// tests/AuswertungPro.Next.Tests/Ai/BendWormSchemaTests.cs
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Tests.Ai;

public class BendWormSchemaTests
{
    [Fact]
    public void Place_SetsPhaseToAdjusting()
    {
        var schema = new BendWormSchema();
        schema.Place(new NormalizedPoint(0.5, 0.5));
        Assert.Equal(SchemaPhase.Adjusting, schema.Phase);
    }

    [Fact]
    public void ComputeSlices_ReturnsCorrectCount()
    {
        var schema = new BendWormSchema { NumSlices = 10 };
        schema.Place(new NormalizedPoint(0.5, 0.5));
        var slices = schema.ComputeSlices();
        Assert.Equal(10, slices.Count);
    }

    [Fact]
    public void ComputeSlices_PerspectiveScale_NearBiggerThanFar()
    {
        var schema = new BendWormSchema { Kippen = 45, NumSlices = 8 };
        schema.Place(new NormalizedPoint(0.5, 0.5));
        var slices = schema.ComputeSlices();
        // Erste Scheibe (nah) groesser als letzte (fern)
        Assert.True(slices[0].Radius > slices[^1].Radius);
    }

    [Fact]
    public void ComputeSlices_Rundung_NearIsCircle_FarIsEllipse()
    {
        var schema = new BendWormSchema { Kippen = 50, Rundung = 0.3, NumSlices = 6 };
        schema.Place(new NormalizedPoint(0.5, 0.5));
        var slices = schema.ComputeSlices();
        // Naheste Scheibe: Rundung nahe 1.0
        Assert.True(slices[0].Rundung > 0.9);
        // Fernste Scheibe: Rundung nahe 0.3
        Assert.True(slices[^1].Rundung < 0.5);
    }

    [Fact]
    public void Confirm_ReturnsGeometryWithAngle()
    {
        var schema = new BendWormSchema { BendAngleDeg = 90 };
        schema.Place(new NormalizedPoint(0.5, 0.5));
        var geo = schema.Confirm();
        Assert.Equal(OverlayToolType.PipeBend, geo.ToolType);
        Assert.Equal(90.0, geo.ArcDegrees);
    }
}
```

- [ ] **Step 2: Tests ausfuehren, Fehler bestaetigen**

Run: `dotnet test --filter "FullyQualifiedName~BendWormSchemaTests" -v minimal`
Expected: FAIL — `BendWormSchema` existiert nicht.

- [ ] **Step 3: BendWormSchema implementieren**

```csharp
// src/AuswertungPro.Next.UI/Ai/BendWormSchema.cs
using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Bogen-Wurm Schema: Roentgenbild-Darstellung eines Rohrbogens.
/// Scheiben entlang eines Kreisbogens mit progressiver Perspektive.
/// </summary>
public sealed class BendWormSchema : SchemaOverlayBase
{
    // --- Parameter (via Slider steuerbar) ---
    public double BendAngleDeg { get; set; } = 90;       // Bogenwinkel (10-170)
    public double RotationDeg { get; set; }               // Richtung auf dem Bild (0-360)
    public double Neigung { get; set; } = 45;             // Quer-Stauchung (0-85)
    public double Kippen { get; set; }                    // Laengs-Stauchung / Tiefenperspektive (-85..85)
    public double Rundung { get; set; } = 0.6;            // Min-Rundung am fernen Ende (0.1-1.0)
    public int NumSlices { get; set; } = 14;              // Anzahl Scheiben
    public int NumLongs { get; set; } = 10;               // Laengslinien
    public double TubeRadius { get; set; } = 0.12;        // Rohr-Querschnitt (normiert)
    public double BendRadius { get; set; } = 0.3;         // Bogenradius (normiert)

    /// <summary>Ergebnis einer Scheiben-Berechnung.</summary>
    public sealed record SliceData(
        double CenterX, double CenterY,   // Bildschirm-Position (normiert)
        double Radius,                      // Skalierter Radius
        double Rundung,                     // Ellipsen-Verhaeltnis (1.0=Kreis)
        double TangentAngle,                // Bildschirm-Tangente (Rad)
        bool IsEnd, bool IsMuffe);

    public override void Place(NormalizedPoint clickPos)
    {
        Center = clickPos;
        Phase = SchemaPhase.Adjusting;
    }

    /// <summary>Alle Scheiben berechnen (fuer Rendering).</summary>
    public List<SliceData> ComputeSlices()
    {
        var result = new List<SliceData>(NumSlices);
        double bendRad = BendAngleDeg * Math.PI / 180.0;
        double rotRad = RotationDeg * Math.PI / 180.0;
        double neigRad = Neigung * Math.PI / 180.0;
        double kipRad = Kippen * Math.PI / 180.0;
        double R = BendRadius;
        double r = TubeRadius;
        double ox = Center.X, oy = Center.Y;

        // Neigung+Kippen Transformationsmatrix
        double cr = Math.Cos(rotRad), sr = Math.Sin(rotRad);
        double nf = Math.Cos(neigRad);
        double kf = Math.Cos(kipRad);
        double m00 = cr * cr * kf + sr * sr * nf;
        double m01 = cr * sr * (kf - nf);
        double m11 = sr * sr * kf + cr * cr * nf;

        // Bogenzentrum (Mitte bei ox/oy)
        double midAngle = rotRad;
        double arcCx = ox - R * Math.Cos(midAngle);
        double arcCy = oy - R * Math.Sin(midAngle);
        double arcStart = midAngle - bendRad / 2.0;

        // Perspektive
        double perspStr = Math.Sin(kipRad);
        double absPersp = Math.Abs(perspStr);

        // Alle Bogenmittelpunkte transformieren
        var centers = new (double x, double y)[NumSlices];
        for (int i = 0; i < NumSlices; i++)
        {
            double t = (double)i / (NumSlices - 1);
            double angle = arcStart + t * bendRad;
            double rawX = arcCx + R * Math.Cos(angle);
            double rawY = arcCy + R * Math.Sin(angle);
            double dx = rawX - ox, dy = rawY - oy;
            centers[i] = (ox + dx * m00 + dy * m01, oy + dx * m01 + dy * m11);
        }

        for (int i = 0; i < NumSlices; i++)
        {
            double t = (double)i / (NumSlices - 1);
            var c = centers[i];

            // Perspektiv-Skalierung
            double perspScale = 1.0 - (t - 0.5) * perspStr * 0.9;
            double sliceR = r * perspScale;

            // Progressive Rundung (nah=Kreis, fern=Ellipse)
            double sliceRundung;
            if (absPersp < 0.01)
            {
                sliceRundung = Rundung;
            }
            else
            {
                double maxP = 1.0 + 0.45 * absPersp;
                double minP = 1.0 - 0.45 * absPersp;
                double depth01 = (maxP - perspScale) / (maxP - minP);
                sliceRundung = 1.0 - depth01 * (1.0 - Rundung);
            }

            // Bildschirm-Tangente
            var prev = centers[Math.Max(0, i - 1)];
            var next = centers[Math.Min(NumSlices - 1, i + 1)];
            double tangent = Math.Atan2(next.y - prev.y, next.x - prev.x);

            bool isEnd = i == 0 || i == NumSlices - 1;
            bool isMuffe = isEnd || (NumSlices > 6 &&
                (i == NumSlices / 3 || i == NumSlices * 2 / 3));

            result.Add(new SliceData(c.x, c.y, sliceR, sliceRundung,
                tangent, isEnd, isMuffe));
        }

        return result;
    }

    /// <summary>Bogenmittellinie berechnen (fuer gestrichelte Achse).</summary>
    public List<(double x, double y)> ComputeCenterline(int segments = 40)
    {
        var pts = new List<(double, double)>(segments + 1);
        double bendRad = BendAngleDeg * Math.PI / 180.0;
        double rotRad = RotationDeg * Math.PI / 180.0;
        double ox = Center.X, oy = Center.Y;
        double cr = Math.Cos(rotRad), sr = Math.Sin(rotRad);
        double nf = Math.Cos(Neigung * Math.PI / 180.0);
        double kf = Math.Cos(Kippen * Math.PI / 180.0);
        double m00 = cr * cr * kf + sr * sr * nf;
        double m01 = cr * sr * (kf - nf);
        double m11 = sr * sr * kf + cr * cr * nf;
        double arcCx = ox - BendRadius * Math.Cos(rotRad);
        double arcCy = oy - BendRadius * Math.Sin(rotRad);
        double arcStart = rotRad - bendRad / 2.0;

        for (int i = 0; i <= segments; i++)
        {
            double angle = arcStart + ((double)i / segments) * bendRad;
            double rawX = arcCx + BendRadius * Math.Cos(angle);
            double rawY = arcCy + BendRadius * Math.Sin(angle);
            double dx = rawX - ox, dy = rawY - oy;
            pts.Add((ox + dx * m00 + dy * m01, oy + dx * m01 + dy * m11));
        }
        return pts;
    }

    public override IReadOnlyList<SchemaHandle> GetHandles() => new[]
    {
        new SchemaHandle("center", Center, "SizeAll")
    };

    public override void DragHandle(string handleId, NormalizedPoint mousePos)
    {
        if (handleId == "center") Center = mousePos;
    }

    public override OverlayGeometry Confirm()
    {
        Phase = SchemaPhase.Confirmed;
        return new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            Points = new List<NormalizedPoint> { Center },
            ArcDegrees = Math.Round(BendAngleDeg, 1)
        };
    }

    public override void Reset()
    {
        BendAngleDeg = 90; RotationDeg = 0; Neigung = 45;
        Kippen = 0; Rundung = 0.6; NumSlices = 14;
    }

    public override string ResultLabel => $"BCC: {BendAngleDeg:F0}\u00b0";
}
```

- [ ] **Step 4: Tests ausfuehren**

Run: `dotnet test --filter "FullyQualifiedName~BendWormSchemaTests" -v minimal`
Expected: alle 5 Tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/BendWormSchema.cs tests/AuswertungPro.Next.Tests/Ai/BendWormSchemaTests.cs
git commit -m "feat: BendWormSchema — Roentgenbild-Wurm Geometrie"
```

---

### Task 2: LateralDragSchema — Geometrie und State

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/LateralDragSchema.cs`
- Test: `tests/AuswertungPro.Next.Tests/Ai/LateralDragSchemaTests.cs`

- [ ] **Step 1: Test-Datei erstellen**

```csharp
// tests/AuswertungPro.Next.Tests/Ai/LateralDragSchemaTests.cs
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Tests.Ai;

public class LateralDragSchemaTests
{
    [Fact]
    public void ClockFromPosition_Right_Returns3()
    {
        var schema = new LateralDragSchema();
        schema.PipeCenter = new NormalizedPoint(0.5, 0.5);
        schema.HolePosition = new NormalizedPoint(0.8, 0.5); // rechts
        Assert.Equal(3.0, schema.ComputedClock, precision: 0);
    }

    [Fact]
    public void ClockFromPosition_Top_Returns12()
    {
        var schema = new LateralDragSchema();
        schema.PipeCenter = new NormalizedPoint(0.5, 0.5);
        schema.HolePosition = new NormalizedPoint(0.5, 0.2); // oben
        Assert.Equal(12.0, schema.ComputedClock, precision: 0);
    }

    [Fact]
    public void ClockFromPosition_Bottom_Returns6()
    {
        var schema = new LateralDragSchema();
        schema.PipeCenter = new NormalizedPoint(0.5, 0.5);
        schema.HolePosition = new NormalizedPoint(0.5, 0.8); // unten
        Assert.Equal(6.0, schema.ComputedClock, precision: 0);
    }

    [Fact]
    public void MeasuredDn_WithCalibration_ReturnsMillimeters()
    {
        var schema = new LateralDragSchema { OpeningRadius = 0.1 };
        // Kalibrierung: 0.5 normiert = 300mm → 1 normiert = 600mm
        var calib = new PipeCalibration
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.5
        };
        double dn = schema.MeasureDn(calib);
        // 0.1 * 2 = 0.2 normiert, 0.2/0.5 * 300 = 120mm
        Assert.Equal(120, dn, precision: 0);
    }

    [Fact]
    public void Confirm_ReturnsLateralCircleGeometry()
    {
        var schema = new LateralDragSchema();
        schema.Place(new NormalizedPoint(0.8, 0.5));
        schema.PipeCenter = new NormalizedPoint(0.5, 0.5);
        var geo = schema.Confirm();
        Assert.Equal(OverlayToolType.LateralCircle, geo.ToolType);
        Assert.Equal(3.0, geo.ClockFrom!.Value, precision: 0);
    }
}
```

- [ ] **Step 2: Tests ausfuehren, Fehler bestaetigen**

Run: `dotnet test --filter "FullyQualifiedName~LateralDragSchemaTests" -v minimal`
Expected: FAIL — `LateralDragSchema` existiert nicht.

- [ ] **Step 3: LateralDragSchema implementieren**

```csharp
// src/AuswertungPro.Next.UI/Ai/LateralDragSchema.cs
using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Anschluss-Schema: Oeffnung frei positionierbar per Drag.
/// Uhrlage wird automatisch aus Position berechnet.
/// DN wird aus Kalibrierung + Oeffnungsgroesse gemessen.
/// </summary>
public sealed class LateralDragSchema : SchemaOverlayBase
{
    // Position der Oeffnung (direkt draggable, normiert)
    public NormalizedPoint HolePosition { get; set; } = new(0.7, 0.5);

    // Oeffnungsgroesse (normiert, aenderbar per Mausrad)
    public double OpeningRadius { get; set; } = 0.08;

    // Typ: formstueck, eingespitzt, gestutzt
    public string Typ { get; set; } = "formstueck";

    /// <summary>Berechnete Uhrlage (0-12) aus Position relativ zum PipeCenter.</summary>
    public double ComputedClock
    {
        get
        {
            double dx = HolePosition.X - PipeCenter.X;
            double dy = HolePosition.Y - PipeCenter.Y;
            double angle = Math.Atan2(dy, dx);
            // 12:00 = oben (-PI/2), im Uhrzeigersinn
            double rawClock = ((angle + Math.PI / 2.0) / (Math.PI * 2.0)) * 12.0;
            return ((rawClock % 12.0) + 12.0) % 12.0;
        }
    }

    /// <summary>Uhrlage auf 0.5h gerundet.</summary>
    public double ComputedClockRounded => Math.Round(ComputedClock * 2.0) / 2.0;

    /// <summary>Winkel vom Zentrum zur Oeffnung (Rad).</summary>
    public double AngleFromCenter => Math.Atan2(
        HolePosition.Y - PipeCenter.Y, HolePosition.X - PipeCenter.X);

    /// <summary>Abstand vom Zentrum (normiert).</summary>
    public double DistanceFromCenter => Math.Sqrt(
        Math.Pow(HolePosition.X - PipeCenter.X, 2) +
        Math.Pow(HolePosition.Y - PipeCenter.Y, 2));

    /// <summary>DN in mm messen (braucht Kalibrierung).</summary>
    public double MeasureDn(PipeCalibration? calib)
    {
        if (calib == null || calib.NormalizedDiameter <= 0) return 0;
        double diameterNorm = OpeningRadius * 2.0;
        return diameterNorm / calib.NormalizedDiameter * calib.NominalDiameterMm;
    }

    /// <summary>VSA-Code basierend auf Typ.</summary>
    public string VsaCode => Typ switch
    {
        "eingespitzt" => "BCA.A",
        "gestutzt" => "BCA.C",
        _ => "BCA.B"
    };

    public override void Place(NormalizedPoint clickPos)
    {
        HolePosition = clickPos;
        Phase = SchemaPhase.Adjusting;
    }

    public override IReadOnlyList<SchemaHandle> GetHandles() => new[]
    {
        new SchemaHandle("hole", HolePosition, "SizeAll")
    };

    public override void DragHandle(string handleId, NormalizedPoint mousePos)
    {
        if (handleId == "hole") HolePosition = mousePos;
    }

    /// <summary>Oeffnungsgroesse aendern (Mausrad).</summary>
    public void ScaleOpening(double factor)
    {
        OpeningRadius = Math.Clamp(OpeningRadius * factor, 0.01, 0.4);
    }

    public override OverlayGeometry Confirm()
    {
        Phase = SchemaPhase.Confirmed;
        double clock = ComputedClockRounded;
        return new OverlayGeometry
        {
            ToolType = OverlayToolType.LateralCircle,
            Points = new List<NormalizedPoint> { HolePosition, PipeCenter },
            ClockFrom = clock,
            ClockTo = clock
        };
    }

    public override void Reset()
    {
        OpeningRadius = 0.08;
        Typ = "formstueck";
    }

    public override string ResultLabel
    {
        get
        {
            double clock = ComputedClockRounded;
            return $"{VsaCode} {clock:F1}:00";
        }
    }
}
```

- [ ] **Step 4: Tests ausfuehren**

Run: `dotnet test --filter "FullyQualifiedName~LateralDragSchemaTests" -v minimal`
Expected: alle 5 Tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/LateralDragSchema.cs tests/AuswertungPro.Next.Tests/Ai/LateralDragSchemaTests.cs
git commit -m "feat: LateralDragSchema — Anschluss per Drag positionieren"
```

---

### Task 3: BendWormRenderer — WPF Canvas Rendering

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/BendWormRenderer.cs`

Statische Klasse die `BendWormSchema` auf einen WPF Canvas zeichnet. Uebersetzung der Mockup-Logik (`drawBendOverlay()`) zu WPF Shapes.

- [ ] **Step 1: Renderer implementieren**

```csharp
// src/AuswertungPro.Next.UI/Ai/BendWormRenderer.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Zeichnet einen BendWormSchema-Zustand auf einen WPF Canvas.
/// Alle Koordinaten werden von normiert (0-1) zu Canvas-Pixel umgerechnet.
/// </summary>
public static class BendWormRenderer
{
    private const string Tag = "bend_overlay";

    private static readonly Brush SliceBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(200, 255, 255, 0)));
    private static readonly Brush DimSliceBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(80, 255, 255, 0)));
    private static readonly Brush LongBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(30, 255, 255, 0)));
    private static readonly Brush SurfaceBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(8, 255, 255, 0)));
    private static readonly Brush CenterlineBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(65, 255, 255, 0)));
    private static readonly Brush LabelBg = FreezeBrush(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)));
    private static Brush FreezeBrush(Brush b) { b.Freeze(); return b; }

    /// <summary>Alle Bend-Elemente vom Canvas entfernen.</summary>
    public static void Clear(Canvas canvas)
    {
        for (int i = canvas.Children.Count - 1; i >= 0; i--)
        {
            if (canvas.Children[i] is FrameworkElement fe && Tag.Equals(fe.Tag))
                canvas.Children.RemoveAt(i);
        }
    }

    /// <summary>
    /// Schema auf Canvas zeichnen.
    /// </summary>
    /// <param name="canvas">Ziel-Canvas</param>
    /// <param name="schema">Schema-Zustand</param>
    /// <param name="imgRect">Bild-Bereich im Canvas (Letterbox-korrigiert)</param>
    public static void Render(Canvas canvas, BendWormSchema schema, Rect imgRect)
    {
        Clear(canvas);
        if (schema.Phase == SchemaPhase.None) return;

        var slices = schema.ComputeSlices();
        if (slices.Count == 0) return;

        double w = imgRect.Width, h = imgRect.Height;
        double ox = imgRect.Left, oy = imgRect.Top;

        // Norm → Canvas
        Point toCanvas(double nx, double ny) => new(ox + nx * w, oy + ny * h);

        // ── Rohrflaeche (halbtransparente Segmente) ──
        int segs = 24;
        var allPts = new List<Point[]>(slices.Count);
        foreach (var s in slices)
        {
            var pts = new Point[segs + 1];
            double cosT = Math.Cos(s.TangentAngle), sinT = Math.Sin(s.TangentAngle);
            for (int j = 0; j <= segs; j++)
            {
                double phi = (double)j / segs * Math.PI * 2;
                double lx = s.Radius * s.Rundung * Math.Cos(phi);
                double ly = s.Radius * Math.Sin(phi);
                double px = s.CenterX + (lx * cosT - ly * sinT);
                double py = s.CenterY + (lx * sinT + ly * cosT);
                pts[j] = toCanvas(px, py);
            }
            allPts.Add(pts);
        }

        // Laengslinien
        int longStep = Math.Max(1, segs / schema.NumLongs);
        for (int j = 0; j < segs; j += longStep)
        {
            var fig = new PathFigure { StartPoint = allPts[0][j] };
            for (int i = 1; i < slices.Count; i++)
                fig.Segments.Add(new LineSegment(allPts[i][j], true));

            AddPath(canvas, fig, LongBrush, 1.0);
        }

        // Ringscheiben
        for (int i = 0; i < slices.Count; i++)
        {
            var s = slices[i];
            var pts = allPts[i];
            var fig = new PathFigure { StartPoint = pts[0], IsClosed = true };
            for (int j = 1; j <= segs; j++)
                fig.Segments.Add(new LineSegment(pts[j], true));

            double depthAlpha = 0.25 + 0.65 * (s.Radius / schema.TubeRadius);
            byte alpha = (byte)Math.Clamp(depthAlpha * 255, 40, 240);

            if (s.IsEnd)
            {
                AddPath(canvas, fig, new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 0)), 3.0,
                    new SolidColorBrush(Color.FromArgb(15, 255, 255, 0)));
            }
            else if (s.IsMuffe)
            {
                AddPath(canvas, fig, new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.8), 255, 255, 0)), 2.5);
            }
            else
            {
                AddPath(canvas, fig, new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 0)), 1.3);
            }
        }

        // Bogenachse (gestrichelt)
        var centerline = schema.ComputeCenterline();
        if (centerline.Count > 1)
        {
            var clFig = new PathFigure { StartPoint = toCanvas(centerline[0].x, centerline[0].y) };
            for (int i = 1; i < centerline.Count; i++)
                clFig.Segments.Add(new LineSegment(toCanvas(centerline[i].x, centerline[i].y), true));

            var clPath = MakePath(clFig, CenterlineBrush, 1.2);
            clPath.StrokeDashArray = new DoubleCollection { 5, 4 };
            canvas.Children.Add(clPath);
        }

        // Label
        var labelPos = toCanvas(schema.Center.X, schema.Center.Y - schema.TubeRadius - 0.03);
        AddLabel(canvas, schema.ResultLabel, labelPos);
    }

    private static void AddPath(Canvas canvas, PathFigure fig, Brush stroke, double thickness,
        Brush? fill = null)
    {
        canvas.Children.Add(MakePath(fig, stroke, thickness, fill));
    }

    private static Path MakePath(PathFigure fig, Brush stroke, double thickness,
        Brush? fill = null)
    {
        var path = new Path
        {
            Data = new PathGeometry(new[] { fig }),
            Stroke = stroke,
            StrokeThickness = thickness,
            Fill = fill,
            Tag = Tag
        };
        return path;
    }

    private static void AddLabel(Canvas canvas, string text, Point pos)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = Brushes.Yellow,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(5, 2, 5, 2),
            Background = LabelBg,
            Tag = Tag
        };
        Canvas.SetLeft(tb, pos.X - 40);
        Canvas.SetTop(tb, pos.Y);
        canvas.Children.Add(tb);
    }
}
```

- [ ] **Step 2: Kompilieren**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/BendWormRenderer.cs
git commit -m "feat: BendWormRenderer — WPF Canvas Rendering fuer Bogen-Wurm"
```

---

### Task 4: LateralDragRenderer — WPF Canvas Rendering

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/LateralDragRenderer.cs`

- [ ] **Step 1: Renderer implementieren**

```csharp
// src/AuswertungPro.Next.UI/Ai/LateralDragRenderer.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Zeichnet einen LateralDragSchema-Zustand auf einen WPF Canvas.
/// Oeffnung als Ellipse, Uhrlage-Linie, Typ-spezifische Darstellung.
/// </summary>
public static class LateralDragRenderer
{
    private const string Tag = "lateral_overlay";
    private static readonly Brush CyanBrush = FreezeBrush(new SolidColorBrush(Color.FromRgb(0, 255, 255)));
    private static readonly Brush CyanDim = FreezeBrush(new SolidColorBrush(Color.FromArgb(100, 0, 255, 255)));
    private static readonly Brush CyanFill = FreezeBrush(new SolidColorBrush(Color.FromArgb(20, 0, 255, 255)));
    private static readonly Brush LabelBg = FreezeBrush(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)));
    private static Brush FreezeBrush(Brush b) { b.Freeze(); return b; }

    public static void Clear(Canvas canvas)
    {
        for (int i = canvas.Children.Count - 1; i >= 0; i--)
        {
            if (canvas.Children[i] is FrameworkElement fe && Tag.Equals(fe.Tag))
                canvas.Children.RemoveAt(i);
        }
    }

    public static void Render(Canvas canvas, LateralDragSchema schema,
        Rect imgRect, PipeCalibration? calibration)
    {
        Clear(canvas);
        if (schema.Phase == SchemaPhase.None) return;

        double w = imgRect.Width, h = imgRect.Height;
        double ox = imgRect.Left, oy = imgRect.Top;
        Point toCanvas(double nx, double ny) => new(ox + nx * w, oy + ny * h);

        var holePos = toCanvas(schema.HolePosition.X, schema.HolePosition.Y);
        var centerPos = toCanvas(schema.PipeCenter.X, schema.PipeCenter.Y);
        double openPx = schema.OpeningRadius * Math.Min(w, h);

        // ── Uhrlage-Linie (gestrichelt) ──
        var uhrLine = new Line
        {
            X1 = centerPos.X, Y1 = centerPos.Y,
            X2 = holePos.X, Y2 = holePos.Y,
            Stroke = CyanDim, StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 6, 4 },
            Tag = Tag
        };
        canvas.Children.Add(uhrLine);

        // ── Uhrlage-Punkt ──
        var dot = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = CyanBrush, Tag = Tag
        };
        Canvas.SetLeft(dot, holePos.X - 5);
        Canvas.SetTop(dot, holePos.Y - 5);
        canvas.Children.Add(dot);

        // ── Oeffnung ──
        double tangAngle = schema.AngleFromCenter + Math.PI / 2;
        double ellipseA = openPx;
        double ellipseB = openPx * 0.5;

        if (schema.Typ == "eingespitzt")
        {
            // Gezackte Oeffnung
            DrawJaggedEllipse(canvas, holePos, ellipseA, ellipseB, tangAngle);
        }
        else
        {
            // Saubere Ellipse
            var ellipse = new Ellipse
            {
                Width = ellipseA * 2, Height = ellipseB * 2,
                Stroke = CyanBrush, StrokeThickness = 3,
                Fill = CyanFill,
                RenderTransform = new RotateTransform(tangAngle * 180 / Math.PI, ellipseA, ellipseB),
                Tag = Tag
            };
            Canvas.SetLeft(ellipse, holePos.X - ellipseA);
            Canvas.SetTop(ellipse, holePos.Y - ellipseB);
            canvas.Children.Add(ellipse);

            if (schema.Typ == "gestutzt")
            {
                // Muffen-Ring
                var muffRing = new Ellipse
                {
                    Width = ellipseA * 2.24, Height = ellipseB * 2.24,
                    Stroke = CyanDim, StrokeThickness = 1.5,
                    RenderTransform = new RotateTransform(tangAngle * 180 / Math.PI,
                        ellipseA * 1.12, ellipseB * 1.12),
                    Tag = Tag
                };
                Canvas.SetLeft(muffRing, holePos.X - ellipseA * 1.12);
                Canvas.SetTop(muffRing, holePos.Y - ellipseB * 1.12);
                canvas.Children.Add(muffRing);
            }
        }

        // ── Kreuz am Rohrzentrum ──
        double cr = 8;
        canvas.Children.Add(new Line
        {
            X1 = centerPos.X - cr, Y1 = centerPos.Y,
            X2 = centerPos.X + cr, Y2 = centerPos.Y,
            Stroke = CyanDim, StrokeThickness = 1, Tag = Tag
        });
        canvas.Children.Add(new Line
        {
            X1 = centerPos.X, Y1 = centerPos.Y - cr,
            X2 = centerPos.X, Y2 = centerPos.Y + cr,
            Stroke = CyanDim, StrokeThickness = 1, Tag = Tag
        });

        // ── Label ──
        double clock = schema.ComputedClockRounded;
        string dnText;
        if (calibration != null && calibration.NormalizedDiameter > 0)
        {
            double dn = schema.MeasureDn(calibration);
            double ratio = dn / calibration.NominalDiameterMm * 100;
            dnText = $"DN{dn:F0} ({ratio:F0}%)";
        }
        else
        {
            dnText = $"{openPx * 2:F0} px";
        }

        var label = new TextBlock
        {
            Text = $"{schema.VsaCode}  {clock:F1}:00  {dnText}",
            Foreground = CyanBrush,
            FontSize = 12, FontWeight = FontWeights.Bold,
            Padding = new Thickness(5, 2, 5, 2),
            Background = LabelBg, Tag = Tag
        };
        Canvas.SetLeft(label, holePos.X - 60);
        Canvas.SetTop(label, holePos.Y - openPx - 22);
        canvas.Children.Add(label);
    }

    private static void DrawJaggedEllipse(Canvas canvas, Point center,
        double a, double b, double tangAngle)
    {
        int segs = 36;
        double cosT = Math.Cos(tangAngle), sinT = Math.Sin(tangAngle);
        var fig = new PathFigure();
        for (int j = 0; j <= segs; j++)
        {
            double phi = (double)j / segs * Math.PI * 2;
            double jitter = 1.0 + 0.12 * Math.Sin(j * 7.3) + 0.08 * Math.Sin(j * 13.1);
            double lx = a * jitter * Math.Cos(phi);
            double ly = b * jitter * Math.Sin(phi);
            double px = center.X + lx * cosT - ly * sinT;
            double py = center.Y + lx * sinT + ly * cosT;
            if (j == 0) fig.StartPoint = new Point(px, py);
            else fig.Segments.Add(new LineSegment(new Point(px, py), true));
        }
        fig.IsClosed = true;

        var path = new Path
        {
            Data = new PathGeometry(new[] { fig }),
            Stroke = CyanBrush, StrokeThickness = 2.5,
            Fill = CyanFill, Tag = Tag
        };
        canvas.Children.Add(path);
    }
}
```

- [ ] **Step 2: Kompilieren**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/LateralDragRenderer.cs
git commit -m "feat: LateralDragRenderer — WPF Canvas Rendering fuer Anschluss"
```

---

### Task 5: XAML Slider-Panel anpassen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PhotoMeasurementWindow.xaml`

Das bestehende `PanelAngle` (Position+Winkel Slider) ersetzen durch zwei separate Panels fuer Bogen und Anschluss.

- [ ] **Step 1: PanelAngle in XAML finden und ersetzen**

Suche `PanelAngle` im XAML. Ersetze das Panel durch zwei separate StackPanels:

```xml
<!-- Bogen-Parameter -->
<StackPanel x:Name="PanelBend" Visibility="Collapsed" Margin="0,8,0,0">
    <TextBlock Text="Bogen (BCC)" FontWeight="SemiBold" Foreground="{DynamicResource AccentBrush}" Margin="0,0,0,4"/>
    <!-- Bogenwinkel -->
    <DockPanel Margin="0,2"><TextBlock Text="Winkel" Width="60" Foreground="White" FontSize="11"/>
        <TextBlock x:Name="TxtBendAngle" Text="90°" Width="40" TextAlignment="Right" Foreground="{DynamicResource AccentBrush}" FontWeight="SemiBold" FontSize="11" DockPanel.Dock="Right"/>
        <Slider x:Name="SliderBendAngle" Minimum="10" Maximum="170" Value="90" TickFrequency="5" IsSnapToTickEnabled="True" ValueChanged="SliderBend_ValueChanged"/></DockPanel>
    <!-- Richtung -->
    <DockPanel Margin="0,2"><TextBlock Text="Richtung" Width="60" Foreground="White" FontSize="11"/>
        <TextBlock x:Name="TxtBendRot" Text="0°" Width="40" TextAlignment="Right" Foreground="{DynamicResource AccentBrush}" FontWeight="SemiBold" FontSize="11" DockPanel.Dock="Right"/>
        <Slider x:Name="SliderBendRot" Minimum="0" Maximum="360" Value="0" ValueChanged="SliderBend_ValueChanged"/></DockPanel>
    <!-- Neigung -->
    <DockPanel Margin="0,2"><TextBlock Text="Neigung" Width="60" Foreground="White" FontSize="11"/>
        <TextBlock x:Name="TxtBendNeig" Text="45°" Width="40" TextAlignment="Right" Foreground="{DynamicResource AccentBrush}" FontWeight="SemiBold" FontSize="11" DockPanel.Dock="Right"/>
        <Slider x:Name="SliderBendNeig" Minimum="0" Maximum="85" Value="45" ValueChanged="SliderBend_ValueChanged"/></DockPanel>
    <!-- Kippen -->
    <DockPanel Margin="0,2"><TextBlock Text="Kippen" Width="60" Foreground="White" FontSize="11"/>
        <TextBlock x:Name="TxtBendKipp" Text="0°" Width="40" TextAlignment="Right" Foreground="{DynamicResource AccentBrush}" FontWeight="SemiBold" FontSize="11" DockPanel.Dock="Right"/>
        <Slider x:Name="SliderBendKipp" Minimum="-85" Maximum="85" Value="0" ValueChanged="SliderBend_ValueChanged"/></DockPanel>
    <!-- Rundung -->
    <DockPanel Margin="0,2"><TextBlock Text="Rundung" Width="60" Foreground="White" FontSize="11"/>
        <TextBlock x:Name="TxtBendRund" Text="60%" Width="40" TextAlignment="Right" Foreground="{DynamicResource AccentBrush}" FontWeight="SemiBold" FontSize="11" DockPanel.Dock="Right"/>
        <Slider x:Name="SliderBendRund" Minimum="10" Maximum="100" Value="60" ValueChanged="SliderBend_ValueChanged"/></DockPanel>
    <!-- Scheiben -->
    <DockPanel Margin="0,2"><TextBlock Text="Scheiben" Width="60" Foreground="White" FontSize="11"/>
        <TextBlock x:Name="TxtBendSlices" Text="14" Width="40" TextAlignment="Right" Foreground="{DynamicResource AccentBrush}" FontWeight="SemiBold" FontSize="11" DockPanel.Dock="Right"/>
        <Slider x:Name="SliderBendSlices" Minimum="6" Maximum="24" Value="14" IsSnapToTickEnabled="True" TickFrequency="1" ValueChanged="SliderBend_ValueChanged"/></DockPanel>
</StackPanel>

<!-- Anschluss-Parameter -->
<StackPanel x:Name="PanelLateral" Visibility="Collapsed" Margin="0,8,0,0">
    <TextBlock Text="Anschluss (BCA)" FontWeight="SemiBold" Foreground="{DynamicResource AccentBrush}" Margin="0,0,0,4"/>
    <!-- Uhrlage (readonly, berechnet) -->
    <DockPanel Margin="0,2"><TextBlock Text="Uhrlage" Width="60" Foreground="White" FontSize="11"/>
        <TextBlock x:Name="TxtLatClock" Text="3:00" Foreground="{DynamicResource AccentBrush}" FontWeight="Bold" FontSize="13"/></DockPanel>
    <!-- Gemessener DN -->
    <DockPanel Margin="0,2"><TextBlock Text="DN" Width="60" Foreground="White" FontSize="11"/>
        <TextBlock x:Name="TxtLatDn" Text="-- mm" Foreground="{DynamicResource AccentBrush}" FontWeight="Bold" FontSize="13"/></DockPanel>
    <!-- Anteil -->
    <DockPanel Margin="0,2"><TextBlock Text="Anteil" Width="60" Foreground="White" FontSize="11"/>
        <TextBlock x:Name="TxtLatRatio" Text="-- %" Foreground="{DynamicResource AccentBrush}" FontSize="11"/></DockPanel>
    <!-- Typ -->
    <DockPanel Margin="0,4"><TextBlock Text="Typ" Width="60" Foreground="White" FontSize="11"/>
        <ComboBox x:Name="CboLatTyp" SelectedIndex="0" SelectionChanged="CboLatTyp_Changed"
                  Background="{DynamicResource HeaderBrush}" Foreground="White" FontSize="11">
            <ComboBoxItem Content="Formstueck (BCA.B)"/>
            <ComboBoxItem Content="Eingespitzt (BCA.A)"/>
            <ComboBoxItem Content="Gestutzt (BCA.C)"/>
        </ComboBox></DockPanel>
    <TextBlock Text="Drag = Oeffnung verschieben&#x0a;Mausrad = Groesse&#x0a;Erst kalibrieren fuer DN in mm"
               Foreground="Gray" FontSize="9" Margin="0,6,0,0" TextWrapping="Wrap"/>
</StackPanel>
```

- [ ] **Step 2: Alte PanelAngle-Referenzen entfernen**

Im XAML: `PanelAngle`, `SliderPosition`, `SliderAngle`, `TxtPosition`, `TxtAngle` entfernen.

- [ ] **Step 3: Kompilieren**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: Build FAIL (fehlende Event-Handler — werden in Task 6 ergaenzt).

- [ ] **Step 4: Commit (WIP)**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/PhotoMeasurementWindow.xaml
git commit -m "wip: XAML Slider-Panels fuer Bogen + Anschluss"
```

---

### Task 6: PhotoMeasurementWindow Code-Behind Integration

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PhotoMeasurementWindow.xaml.cs`

Alte `DrawBendOverlay`/`DrawLateralOverlay`/`UpdateAngleOverlay` ersetzen durch Schema+Renderer Aufrufe.

- [ ] **Step 1: Schema-Felder hinzufuegen**

Am Anfang der Klasse (nach den bestehenden Feldern):

```csharp
// Neue Schemas (ersetzen alte Slider-basierte Overlays)
private BendWormSchema? _bendSchema;
private LateralDragSchema? _lateralSchema;
```

- [ ] **Step 2: Tool-Aktivierung anpassen**

In der Tool-Wechsel-Methode (wo `_activeTool` gesetzt wird): Die Sichtbarkeit der neuen Panels steuern und Schemas initialisieren.

```csharp
// Statt: bool isAngle = _activeTool is PhotoTool.Lateral or PhotoTool.Bend;
// Statt: PanelAngle.Visibility = isAngle ? Visibility.Visible : Visibility.Collapsed;

PanelBend.Visibility = _activeTool == PhotoTool.Bend ? Visibility.Visible : Visibility.Collapsed;
PanelLateral.Visibility = _activeTool == PhotoTool.Lateral ? Visibility.Visible : Visibility.Collapsed;

if (_activeTool == PhotoTool.Bend)
{
    _bendSchema = new BendWormSchema();
    _bendSchema.ApplyCalibration(_calibration);
    _bendSchema.Place(new NormalizedPoint(
        _calibration.PipeCenter.X, _calibration.PipeCenter.Y));
    RenderBendOverlay();
}
else if (_activeTool == PhotoTool.Lateral)
{
    _lateralSchema = new LateralDragSchema();
    _lateralSchema.PipeCenter = _calibration.PipeCenter;
    _lateralSchema.PipeRadius = _calibration.NormalizedDiameter / 2.0;
    // Initiale Position: 3:00
    double initAngle = 0; // 3:00 = rechts
    double dist = _lateralSchema.PipeRadius * 0.85;
    _lateralSchema.Place(new NormalizedPoint(
        _lateralSchema.PipeCenter.X + Math.Cos(initAngle) * dist,
        _lateralSchema.PipeCenter.Y + Math.Sin(initAngle) * dist));
    RenderLateralOverlay();
}
```

- [ ] **Step 3: Slider Event-Handler implementieren**

```csharp
private void SliderBend_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    if (_bendSchema == null) return;
    _bendSchema.BendAngleDeg = SliderBendAngle.Value;
    _bendSchema.RotationDeg = SliderBendRot.Value;
    _bendSchema.Neigung = SliderBendNeig.Value;
    _bendSchema.Kippen = SliderBendKipp.Value;
    _bendSchema.Rundung = SliderBendRund.Value / 100.0;
    _bendSchema.NumSlices = (int)SliderBendSlices.Value;

    TxtBendAngle.Text = $"{_bendSchema.BendAngleDeg:F0}\u00b0";
    TxtBendRot.Text = $"{_bendSchema.RotationDeg:F0}\u00b0";
    TxtBendNeig.Text = $"{_bendSchema.Neigung:F0}\u00b0";
    TxtBendKipp.Text = $"{_bendSchema.Kippen:F0}\u00b0";
    TxtBendRund.Text = $"{_bendSchema.Rundung * 100:F0}%";
    TxtBendSlices.Text = $"{_bendSchema.NumSlices}";

    RenderBendOverlay();
}

private void CboLatTyp_Changed(object sender, SelectionChangedEventArgs e)
{
    if (_lateralSchema == null) return;
    _lateralSchema.Typ = CboLatTyp.SelectedIndex switch
    {
        1 => "eingespitzt",
        2 => "gestutzt",
        _ => "formstueck"
    };
    RenderLateralOverlay();
}
```

- [ ] **Step 4: Render-Methoden hinzufuegen**

```csharp
private void RenderBendOverlay()
{
    if (_bendSchema == null) return;
    var imgRect = GetImageRenderedRect();
    BendWormRenderer.Render(OverlayCanvas, _bendSchema, imgRect);
    TxtMeasureInfo.Text = _bendSchema.ResultLabel;
    TxtStatus.Text = $"Bogen: {_bendSchema.BendAngleDeg:F0}\u00b0 — Drag=verschieben, Slider=anpassen";
}

private void RenderLateralOverlay()
{
    if (_lateralSchema == null) return;
    var imgRect = GetImageRenderedRect();
    LateralDragRenderer.Render(OverlayCanvas, _lateralSchema, imgRect, _calibration);
    double clock = _lateralSchema.ComputedClockRounded;
    TxtLatClock.Text = $"{clock:F1}:00";
    if (_calibration.WasManuallyCalibrated)
    {
        double dn = _lateralSchema.MeasureDn(_calibration);
        double ratio = dn / _calibration.NominalDiameterMm * 100;
        TxtLatDn.Text = $"DN{dn:F0}";
        TxtLatRatio.Text = $"{ratio:F0} %";
    }
    else
    {
        TxtLatDn.Text = "kalibrieren!";
        TxtLatRatio.Text = "-- %";
    }
    TxtMeasureInfo.Text = _lateralSchema.ResultLabel;
    TxtStatus.Text = $"Anschluss: {_lateralSchema.VsaCode} {clock:F1}:00 — Drag=verschieben, Mausrad=Groesse";
}
```

- [ ] **Step 5: Mouse-Handling fuer Lateral-Drag und Bend-Drag**

In `Canvas_MouseLeftButtonDown`: Fuer Bend und Lateral den Schema-Drag starten.

```csharp
case PhotoTool.Bend:
    _isDragging = true;
    _dragStart = pos;
    _dragStartNorm = norm;
    break;

case PhotoTool.Lateral:
    _isDragging = true;
    _dragStart = pos;
    _dragStartNorm = norm;
    break;
```

In `Canvas_MouseMove` (wenn `_isDragging`):

```csharp
if (_activeTool == PhotoTool.Lateral && _lateralSchema != null)
{
    _lateralSchema.HolePosition = new NormalizedPoint(norm.X, norm.Y);
    RenderLateralOverlay();
    return;
}
if (_activeTool == PhotoTool.Bend && _bendSchema != null)
{
    _bendSchema.Center = new NormalizedPoint(norm.X, norm.Y);
    RenderBendOverlay();
    return;
}
```

In `Canvas_MouseWheel`:

```csharp
if (_activeTool == PhotoTool.Lateral && _lateralSchema != null)
{
    double factor = e.Delta > 0 ? 1.08 : 0.92;
    _lateralSchema.ScaleOpening(factor);
    RenderLateralOverlay();
    e.Handled = true;
    return;
}
if (_activeTool == PhotoTool.Bend && _bendSchema != null)
{
    double factor = e.Delta > 0 ? 1.08 : 0.92;
    _bendSchema.TubeRadius = Math.Clamp(_bendSchema.TubeRadius * factor, 0.02, 0.35);
    _bendSchema.BendRadius = Math.Clamp(_bendSchema.BendRadius * factor, 0.05, 0.5);
    RenderBendOverlay();
    e.Handled = true;
    return;
}
```

- [ ] **Step 6: Alte Methoden entfernen**

Loeschen:
- `DrawLateralOverlay()` (Zeile ~1045-1095)
- `DrawBendOverlay()` (Zeile ~1097-1167)
- `UpdateAngleOverlay()` — ersetzen durch Schema-spezifische Aufrufe
- `SliderPosition_ValueChanged` / `SliderAngle_ValueChanged` — nicht mehr noetig

Alle Referenzen auf `PanelAngle`, `SliderPosition`, `SliderAngle` ersetzen.

- [ ] **Step 7: Kompilieren + Smoke Test**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: Build succeeded.

Manueller Test: App starten, Foto oeffnen, Bogen- und Anschluss-Werkzeug testen.

- [ ] **Step 8: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/PhotoMeasurementWindow.xaml.cs
git commit -m "feat: PhotoMeasurementWindow — neue Bogen + Anschluss Werkzeuge integriert"
```

---

### Task 7: Aufraeum-Commit + Memory Update

- [ ] **Step 1: Alte PipeBendSchema nicht entfernen (Rueckwaertskompatibilitaet)**

Die alte `PipeBendSchema` in `SchemaOverlay.cs` NICHT loeschen — sie wird evtl. noch an anderer Stelle referenziert (CodingModeWindow). Nur als `[Obsolete]` markieren.

- [ ] **Step 2: Alle Tests ausfuehren**

Run: `dotnet test AuswertungPro.sln --filter "Category=Recommendation|FullyQualifiedName~BendWorm|FullyQualifiedName~LateralDrag" -v minimal`
Expected: Alle Tests PASS.

- [ ] **Step 3: Finaler Commit**

```bash
git add -A
git commit -m "feat: PhotoMeasurement Bogen+Anschluss Redesign — Roentgenbild-Wurm + Drag-Anschluss"
```
