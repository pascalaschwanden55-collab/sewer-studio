# Metrierung-Naehe-Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein KI-Befund bekommt nur dann einen Meterstand und einen Protokoll-/Event-Eintrag, wenn er nah genug an der Kamera ist; zu weit voraus liegende Befunde werden weiterhin angezeigt, aber nicht metriert/codiert.

**Architecture:** Reine, testbare C#-Logik (`MetrierungProximityEvaluator`) in der Application-Schicht entscheidet pro Befund `Codierbar`/`Voraus`. Eine feste Kopplungseinheit `SegmentedFinding` (Infrastructure) ersetzt die fragile Index-Kopplung von DINO/SAM/Quantifizierung und ist gegen SAM-skipped-boxes robust. Eingehaengt an zwei duennen Stellen: Live-Codiermodus und Video-Vollanalyse.

**Tech Stack:** C# / .NET (net10.0), WPF, xUnit. Geometrie aus vorhandener `PipeCalibration` + `SamMaskResult`/`SamResponse`. Keine Sidecar-Aenderung, kein Tracking.

---

## File Structure

- **Create** `src/AuswertungPro.Next.Application/Ai/MetrierungProximity.cs` — Enum `MetrierungProximity` + Records `MetrierungProximityInput`, `MetrierungProximityResult`, `MetrierungProximityThresholds`. Reine Datentypen.
- **Create** `src/AuswertungPro.Next.Application/Ai/MetrierungProximityEvaluator.cs` — reine Entscheidungslogik.
- **Create** `tests/AuswertungPro.Next.Pipeline.Tests/MetrierungProximityEvaluatorTests.cs` — Logik-Tests.
- **Create** `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/SegmentedFinding.cs` — Record `SegmentedFinding` + statischer `SegmentedFindingBuilder` (Masken-basiert, bbox+Label-Zuordnung).
- **Create** `tests/AuswertungPro.Next.Pipeline.Tests/SegmentedFindingBuilderTests.cs` — Zuordnungs-Tests inkl. skipped-box.
- **Modify** `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs` — Live-Einhaengung: SegmentedFinding bauen, Proximity, "Voraus"-Overlay + Statusmeldung.
- **Modify** `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs` — Vollanalyse-Einhaengung: nur `Codierbar` -> `EnhancedFinding`, `ProximitySuppressedCount`.

---

## Task 1: Application-Datentypen

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/MetrierungProximity.cs`

- [ ] **Step 1: Datei schreiben**

```csharp
namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ergebnis-Stufe der Naehe-Pruefung eines KI-Befunds.</summary>
public enum MetrierungProximity
{
    /// <summary>Nah genug: darf metriert und codiert werden.</summary>
    Codierbar,
    /// <summary>Noch zu weit voraus: anzeigen, aber nicht metrieren/codieren.</summary>
    Voraus
}

/// <summary>
/// Reine Eingabe der Naehe-Pruefung. Alle Koordinaten normiert (0..1).
/// Entkoppelt die Logik von Infrastructure-DTOs.
/// </summary>
public sealed record MetrierungProximityInput(
    double X1, double Y1, double X2, double Y2,   // Befund-Box, normiert
    double VanishX, double VanishY,                // Fluchtpunkt (Rohrmitte), normiert
    double ImageAspect,                            // Bildbreite / Bildhoehe (>= 1 bei Querformat)
    double PipeRadiusNorm);                        // Rohrradius normiert (NormalizedDiameter/2; Fallback 0.5)

/// <summary>Ergebnis der Naehe-Pruefung mit Begruendung und Messwerten (fuer Tests/Diagnose).</summary>
public sealed record MetrierungProximityResult(
    MetrierungProximity Decision,
    string Reason,
    double FillRatio,
    double DistToVanish,
    double OuterRadius,
    bool WandNaehe,
    bool EnthaeltCenter)
{
    public bool IsCodierbar => Decision == MetrierungProximity.Codierbar;
}

/// <summary>
/// Kalibrierbare Schwellen. Bewusst konservativ: im Zweifel "Voraus".
/// Alle Distanz-Schwellen sind in Einheiten des Rohrradius (1.0 = an der Rohrwand).
/// </summary>
public sealed record MetrierungProximityThresholds(
    double FillNear = 0.70,       // Boxhoehe/Bildhoehe ab der ein Ereignis "querschnittsfuellend nah" ist
    double CenterNear = 0.20,     // Box-Zentrum naeher als das am Fluchtpunkt -> zentral
    double RadialOutside = 0.45,  // Box-Zentrum weiter als das -> klar aussen an der Wand
    double WallTolerance = 0.12)  // Toleranz fuer Wand-/Bildrand-Kontakt
{
    public static MetrierungProximityThresholds Default { get; } = new();
}
```

- [ ] **Step 2: Application bauen**

Run: `dotnet build src/AuswertungPro.Next.Application/AuswertungPro.Next.Application.csproj -clp:ErrorsOnly -nologo`
Expected: 0 Fehler.

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/MetrierungProximity.cs
git commit -m "feat(metrierung): Datentypen fuer Naehe-Pruefung"
```

---

## Task 2: MetrierungProximityEvaluator (TDD)

**Files:**
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/MetrierungProximityEvaluatorTests.cs`
- Create: `src/AuswertungPro.Next.Application/Ai/MetrierungProximityEvaluator.cs`

- [ ] **Step 1: Failing tests schreiben**

```csharp
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class MetrierungProximityEvaluatorTests
{
    private static readonly MetrierungProximityThresholds T = MetrierungProximityThresholds.Default;

    // Fluchtpunkt = Bildmitte, Rohrradius = halber Frame, quadratisches Bild.
    private static MetrierungProximityInput Box(double x1, double y1, double x2, double y2)
        => new(x1, y1, x2, y2, 0.5, 0.5, 1.0, 0.5);

    [Fact]
    public void TunnelFehlmaske_gross_zentral_ohne_Wandnaehe_ist_Voraus()
    {
        // Grosse ovale Maske mittig (0.2..0.8 in x und y): enthaelt Center, aber Rand bleibt
        // innerhalb des Rohrradius (kein Wand-/Bildrandkontakt).
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.20, 0.20, 0.80, 0.80), T);
        Assert.Equal(MetrierungProximity.Voraus, r.Decision);
    }

    [Fact]
    public void NaheMuffe_gross_mit_Bildrandkontakt_ist_Codierbar()
    {
        // Fuellt das Bild fast komplett, Raender am Bildrand -> Wandnaehe.
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.03, 0.03, 0.97, 0.97), T);
        Assert.Equal(MetrierungProximity.Codierbar, r.Decision);
    }

    [Fact]
    public void WandschadenNah_klein_aussen_ist_Codierbar()
    {
        // Kleine Box weit aussen oben (nahe Rohrwand), weit weg vom Fluchtpunkt.
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.46, 0.02, 0.54, 0.12), T);
        Assert.Equal(MetrierungProximity.Codierbar, r.Decision);
    }

    [Fact]
    public void KleinerZentralerFund_weit_voraus_ist_Voraus()
    {
        // Kleine Box direkt am Fluchtpunkt.
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.46, 0.46, 0.54, 0.54), T);
        Assert.Equal(MetrierungProximity.Voraus, r.Decision);
    }

    [Fact]
    public void Konservativ_unklarer_mittlerer_Fund_ist_Voraus()
    {
        // Mittelgross, nicht am Bildrand, Zentrum maessig weit (zwischen CenterNear und RadialOutside).
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.30, 0.30, 0.50, 0.50), T);
        Assert.Equal(MetrierungProximity.Voraus, r.Decision);
    }

    [Fact]
    public void ImageAspect_wird_in_radialer_Distanz_beruecksichtigt()
    {
        // Bei Querformat (Aspect 1.78) zaehlt horizontale Distanz staerker.
        var wide = new MetrierungProximityInput(0.70, 0.48, 0.78, 0.52, 0.5, 0.5, 1.78, 0.5);
        var r = MetrierungProximityEvaluator.Evaluate(wide, T);
        Assert.Equal(MetrierungProximity.Codierbar, r.Decision); // klar rechts aussen
    }
}
```

- [ ] **Step 2: Tests laufen lassen -> Fehlschlag (Evaluator fehlt)**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter MetrierungProximityEvaluatorTests -nologo`
Expected: Compile-Fehler / FAIL (`MetrierungProximityEvaluator` existiert nicht).

- [ ] **Step 3: Evaluator implementieren**

```csharp
using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Reine Naehe-Pruefung: entscheidet, ob ein Befund nah genug zum Metrieren ist.
/// Bezug ist der Fluchtpunkt (Rohrmitte). Distanzen in Einheiten des Rohrradius
/// (1.0 = an der Rohrwand). Konservativ: was nicht klar nah ist, gilt als "Voraus".
/// Regeln siehe Spec 2026-06-03-metrierung-naehe-gate.
/// </summary>
public static class MetrierungProximityEvaluator
{
    public static MetrierungProximityResult Evaluate(MetrierungProximityInput i, MetrierungProximityThresholds t)
    {
        double pipeR = i.PipeRadiusNorm > 0 ? i.PipeRadiusNorm : 0.5;

        double cx = (i.X1 + i.X2) / 2.0;
        double cy = (i.Y1 + i.Y2) / 2.0;
        double fillRatio = Math.Max(0.0, i.Y2 - i.Y1);

        double Dist(double ax, double ay, double bx, double by)
        {
            double dx = (ax - bx) * i.ImageAspect;
            double dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        double distToVanish = Dist(cx, cy, i.VanishX, i.VanishY) / pipeR;

        // groesste Eckendistanz zum Fluchtpunkt (in Rohrradius) -> wie weit reicht die Box nach aussen
        double outerR = 0.0;
        ReadOnlySpan<(double X, double Y)> corners = stackalloc (double, double)[]
        {
            (i.X1, i.Y1), (i.X2, i.Y1), (i.X2, i.Y2), (i.X1, i.Y2)
        };
        foreach (var c in corners)
            outerR = Math.Max(outerR, Dist(c.X, c.Y, i.VanishX, i.VanishY) / pipeR);

        bool enthaeltCenter = i.X1 <= i.VanishX && i.VanishX <= i.X2
                           && i.Y1 <= i.VanishY && i.VanishY <= i.Y2;

        bool touchesBorder = i.X1 <= t.WallTolerance || i.Y1 <= t.WallTolerance
                          || i.X2 >= 1.0 - t.WallTolerance || i.Y2 >= 1.0 - t.WallTolerance;
        bool reachesWall = outerR >= 1.0 - t.WallTolerance;
        bool wandnaehe = touchesBorder || reachesWall;

        MetrierungProximityResult Result(MetrierungProximity d, string reason)
            => new(d, reason, fillRatio, distToVanish, outerR, wandnaehe, enthaeltCenter);

        // 1) Tunnel-Fehlmaske: zentral am Fluchtpunkt, keine Wandnaehe -> Voraus.
        if (enthaeltCenter && distToVanish < t.CenterNear && !wandnaehe)
            return Result(MetrierungProximity.Voraus, "zentral am Fluchtpunkt ohne Wandnaehe");

        // 2) Querschnittsfuellend nah: gross UND Wandnaehe -> Codierbar.
        if (fillRatio >= t.FillNear && wandnaehe)
            return Result(MetrierungProximity.Codierbar, "querschnittsfuellend mit Wandnaehe");

        // 3) Wandschaden nah: deutlich ausserhalb des Fluchtpunktbereichs -> Codierbar.
        if (distToVanish >= t.RadialOutside)
            return Result(MetrierungProximity.Codierbar, "ausserhalb Fluchtpunktbereich (Wandnaehe)");

        // 4) Konservativer Default.
        return Result(MetrierungProximity.Voraus, "nicht eindeutig nah (konservativ)");
    }
}
```

- [ ] **Step 4: Tests laufen lassen -> PASS**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter MetrierungProximityEvaluatorTests -nologo`
Expected: 6 PASS. (Falls ein Grenzfall-Test nicht passt: NUR die Test-Eingabe-Koordinaten justieren, nicht die Schwellen — die Defaults sind fixiert.)

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/MetrierungProximityEvaluator.cs tests/AuswertungPro.Next.Pipeline.Tests/MetrierungProximityEvaluatorTests.cs
git commit -m "feat(metrierung): MetrierungProximityEvaluator mit Naehe-Logik (TDD)"
```

---

## Task 3: SegmentedFinding + Builder (TDD)

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/SegmentedFinding.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/SegmentedFindingBuilderTests.cs`

Kontext: `SamMaskResult` (in `VisionPipelineDtos.cs`) hat `Label`, `Bbox` (IReadOnlyList<double> [x1,y1,x2,y2] in Pixel), `CentroidX/Y`. `DinoDetectionDto` hat `X1,Y1,X2,Y2` (Pixel), `Label`, `Confidence`. `QuantifiedMask` kommt 1:1 aus `samResponse.Masks` (via `QuantifyAll`).

- [ ] **Step 1: SegmentedFinding + Builder schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Feste Kopplungseinheit eines segmentierten Befunds. Ersetzt die fruehere lose
/// Index-Kopplung von DINO/SAM/Quantifizierung (fragil bei SAM-skipped-boxes).
/// </summary>
public sealed record SegmentedFinding(
    DinoDetectionDto? Dino,
    SamMaskResult Mask,
    MaskQuantificationService.QuantifiedMask Quant,
    MetrierungProximityResult Proximity);

/// <summary>
/// Baut SegmentedFindings masken-basiert. Iteriert ueber die SAM-Masken (uebersprungene
/// Boxen existieren dort nicht), paart Mask/Quant per Index INNERHALB der Maskenliste
/// und ordnet DINO ueber bbox-IoU + Label zu (kein Listen-Index ueber Listen hinweg).
/// </summary>
public static class SegmentedFindingBuilder
{
    public static IReadOnlyList<SegmentedFinding> Build(
        SamResponse sam,
        IReadOnlyList<DinoDetectionDto> dinoDetections,
        IReadOnlyList<MaskQuantificationService.QuantifiedMask> quantified,
        double vanishX, double vanishY, double pipeRadiusNorm,
        MetrierungProximityThresholds thresholds)
    {
        var result = new List<SegmentedFinding>(sam.Masks.Count);
        int w = sam.ImageWidth > 0 ? sam.ImageWidth : 1;
        int h = sam.ImageHeight > 0 ? sam.ImageHeight : 1;
        double aspect = (double)w / h;

        for (int m = 0; m < sam.Masks.Count; m++)
        {
            var mask = sam.Masks[m];
            var quant = m < quantified.Count ? quantified[m] : null;
            if (quant == null) continue; // QuantifyAll ist 1:1; defensiv

            var dino = MatchDino(mask, dinoDetections);

            // Box normiert aus der Masken-bbox (traegt die geclampte Input-Box).
            double x1 = 0, y1 = 0, x2 = 1, y2 = 1;
            if (mask.Bbox.Count >= 4)
            {
                x1 = mask.Bbox[0] / w; y1 = mask.Bbox[1] / h;
                x2 = mask.Bbox[2] / w; y2 = mask.Bbox[3] / h;
            }

            var input = new MetrierungProximityInput(
                x1, y1, x2, y2, vanishX, vanishY, aspect,
                pipeRadiusNorm > 0 ? pipeRadiusNorm : 0.5);
            var prox = MetrierungProximityEvaluator.Evaluate(input, thresholds);

            result.Add(new SegmentedFinding(dino, mask, quant, prox));
        }
        return result;
    }

    /// <summary>DINO-Detection mit gleichem Label und hoechster bbox-IoU; null wenn keine plausibel.</summary>
    private static DinoDetectionDto? MatchDino(SamMaskResult mask, IReadOnlyList<DinoDetectionDto> dinos)
    {
        if (mask.Bbox.Count < 4 || dinos.Count == 0) return null;
        double mx1 = mask.Bbox[0], my1 = mask.Bbox[1], mx2 = mask.Bbox[2], my2 = mask.Bbox[3];

        DinoDetectionDto? best = null;
        double bestIou = 0.0;
        foreach (var d in dinos)
        {
            if (!string.Equals(d.Label, mask.Label, StringComparison.OrdinalIgnoreCase)) continue;
            double iou = Iou(mx1, my1, mx2, my2, d.X1, d.Y1, d.X2, d.Y2);
            if (iou > bestIou) { bestIou = iou; best = d; }
        }
        return bestIou >= 0.5 ? best : null; // konservativer Mindest-Overlap
    }

    private static double Iou(double ax1, double ay1, double ax2, double ay2,
                              double bx1, double by1, double bx2, double by2)
    {
        double ix1 = Math.Max(ax1, bx1), iy1 = Math.Max(ay1, by1);
        double ix2 = Math.Min(ax2, bx2), iy2 = Math.Min(ay2, by2);
        double iw = Math.Max(0, ix2 - ix1), ih = Math.Max(0, iy2 - iy1);
        double inter = iw * ih;
        double areaA = Math.Max(0, ax2 - ax1) * Math.Max(0, ay2 - ay1);
        double areaB = Math.Max(0, bx2 - bx1) * Math.Max(0, by2 - by1);
        double union = areaA + areaB - inter;
        return union <= 0 ? 0 : inter / union;
    }
}
```

- [ ] **Step 2: Tests schreiben**

```csharp
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class SegmentedFindingBuilderTests
{
    private static SamMaskResult Mask(string label, double x1, double y1, double x2, double y2)
        => new(label, 0.9, new[] { x1, y1, x2, y2 }, "0", 100, 10000, 10, 10,
               (x1 + x2) / 2, (y1 + y2) / 2);

    private static MaskQuantificationService.QuantifiedMask Quant(string label)
        => new() { Label = label };

    private static DinoDetectionDto Dino(string label, double x1, double y1, double x2, double y2)
        => new(x1, y1, x2, y2, label, 0.77, label);

    [Fact]
    public void SkippedMiddleBox_ordnet_die_zwei_Masken_den_richtigen_DinoBoxen_zu()
    {
        // 3 DINO-Boxen; SAM liefert nur Maske fuer Box 1 und Box 3 (Box 2 uebersprungen).
        var dinos = new List<DinoDetectionDto>
        {
            Dino("crack", 0, 0, 100, 100),
            Dino("root", 200, 200, 300, 300),
            Dino("deposit", 400, 400, 500, 500),
        };
        var masks = new List<SamMaskResult>
        {
            Mask("crack", 0, 0, 100, 100),
            Mask("deposit", 400, 400, 500, 500),
        };
        var sam = new SamResponse(masks, 1000, 1000, 5, requested_boxes: 3, skipped_boxes: 1);
        var quant = new List<MaskQuantificationService.QuantifiedMask> { Quant("crack"), Quant("deposit") };

        var segs = SegmentedFindingBuilder.Build(
            sam, dinos, quant, 0.5, 0.5, 0.5, MetrierungProximityThresholds.Default);

        Assert.Equal(2, segs.Count);
        Assert.Equal("crack", segs[0].Dino!.Label);
        Assert.Equal("deposit", segs[1].Dino!.Label); // NICHT "root" (kein Index-Verrutschen)
    }

    [Fact]
    public void Maske_ohne_passende_DinoBox_hat_Dino_null()
    {
        var dinos = new List<DinoDetectionDto> { Dino("crack", 0, 0, 100, 100) };
        var masks = new List<SamMaskResult> { Mask("root", 800, 800, 900, 900) };
        var sam = new SamResponse(masks, 1000, 1000, 5, requested_boxes: 1, skipped_boxes: 0);
        var quant = new List<MaskQuantificationService.QuantifiedMask> { Quant("root") };

        var segs = SegmentedFindingBuilder.Build(
            sam, dinos, quant, 0.5, 0.5, 0.5, MetrierungProximityThresholds.Default);

        Assert.Single(segs);
        Assert.Null(segs[0].Dino);
        Assert.Equal("root", segs[0].Mask.Label); // Fallback ueber die Maske bleibt nutzbar
    }

    [Fact]
    public void GleichesLabel_zwei_Boxen_wird_ueber_IoU_nicht_Reihenfolge_zugeordnet()
    {
        var dinos = new List<DinoDetectionDto>
        {
            Dino("crack", 0, 0, 100, 100),
            Dino("crack", 600, 600, 700, 700),
        };
        var masks = new List<SamMaskResult> { Mask("crack", 600, 600, 700, 700) };
        var sam = new SamResponse(masks, 1000, 1000, 5, requested_boxes: 2, skipped_boxes: 1);
        var quant = new List<MaskQuantificationService.QuantifiedMask> { Quant("crack") };

        var segs = SegmentedFindingBuilder.Build(
            sam, dinos, quant, 0.5, 0.5, 0.5, MetrierungProximityThresholds.Default);

        Assert.Single(segs);
        Assert.Equal(600, segs[0].Dino!.X1); // die zweite crack-Box (hohe IoU), nicht die erste
    }
}
```

- [ ] **Step 3: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter SegmentedFindingBuilderTests -nologo`
Expected: 3 PASS. (Pruefen: `SamResponse`/`SamMaskResult`/`QuantifiedMask`-Konstruktorparameter exakt wie in `VisionPipelineDtos.cs`/`MaskQuantificationService.cs`. Falls `QuantifiedMask` keinen parameterlosen Init mit `Label` erlaubt, im Testhelfer den real existierenden Konstruktor/Init verwenden.)

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/SegmentedFinding.cs tests/AuswertungPro.Next.Pipeline.Tests/SegmentedFindingBuilderTests.cs
git commit -m "feat(metrierung): SegmentedFinding + Builder (bbox+Label, robust gegen skipped boxes)"
```

---

## Task 4: Live-Einhaengung (Codiermodus)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`

Kontext: `RunCodingAnalysisAsync` ruft `_codingMultiModel.AnalyzeFrameAsync(...)` (`:3002`), dann `ShowMultiModelResults(mmResult)` (`:3025`) und `AddMultiModelFindingsAsEvents(mmResult, captureTimestampSec)` (`:3033`). `mmResult` ist `SingleFrameResult` mit `DinoDetections`, `SamResponse`, `QuantifiedMasks`. Kalibrierung: `_codingOverlayService?.Calibration` (Typ `PipeCalibration`).

- [ ] **Step 1: Helfer zum Bauen der SegmentedFindings ergaenzen**

Neue Methode in `PlayerWindow.Coding.cs` (nahe `AddMultiModelFindingsAsEvents`):

```csharp
    /// <summary>Baut SegmentedFindings aus dem Multi-Model-Ergebnis inkl. Naehe-Pruefung.</summary>
    private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings(SingleFrameResult mmResult)
    {
        if (mmResult.SamResponse == null)
            return System.Array.Empty<SegmentedFinding>();

        var cal = _codingOverlayService?.Calibration;
        double vanishX = cal?.PipeCenter.X ?? 0.5;
        double vanishY = cal?.PipeCenter.Y ?? 0.5;
        double pipeRadius = (cal != null && cal.NormalizedDiameter > 0) ? cal.NormalizedDiameter / 2.0 : 0.5;

        return SegmentedFindingBuilder.Build(
            mmResult.SamResponse,
            mmResult.DinoDetections,
            mmResult.QuantifiedMasks,
            vanishX, vanishY, pipeRadius,
            AuswertungPro.Next.Application.Ai.MetrierungProximityThresholds.Default);
    }
```

- [ ] **Step 2: In RunCodingAnalysisAsync vor Overlay/Event einhaengen**

In `RunCodingAnalysisAsync`, den Block ab `ShowMultiModelResults(mmResult);` (`:3025`) ersetzen durch:

```csharp
                // Naehe-Gate: nur codierbare Befunde metrieren; "Voraus" nur anzeigen.
                var segmented = BuildCodingSegmentedFindings(mmResult);
                int vorausCount = segmented.Count(s => !s.Proximity.IsCodierbar);
                int codierbarCount = segmented.Count - vorausCount;

                // Masken/Overlay rendern (alle, "Voraus" optisch abgesetzt -> Step 3 in ShowMultiModelResults).
                ShowMultiModelResults(mmResult, segmented);

                if (codierbarCount == 0 && vorausCount > 0)
                {
                    SetCodingAiState("Ereignis voraus erkannt - naeher heranfahren",
                        Color.FromRgb(0xF5, 0x9E, 0x0B),
                        $"{vorausCount} voraus");
                    return;
                }

                SetCodingAiState(
                    $"{codierbarCount} Befunde erkannt" + (vorausCount > 0 ? $" ({vorausCount} voraus ignoriert)" : ""),
                    Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"YOLO {mmResult.YoloTimeMs:F0}ms | DINO {mmResult.DinoTimeMs:F0}ms | SAM {mmResult.SamTimeMs:F0}ms");

                // Nur codierbare Befunde als Events.
                AddMultiModelFindingsAsEvents(segmented.Where(s => s.Proximity.IsCodierbar).ToList(), captureTimestampSec);
                return;
```

Hinweis: Die vorhandene `SetCodingAiState`-Zeile (`:2951-2954`) und der alte `AddMultiModelFindingsAsEvents(mmResult, ...)`-Aufruf (`:3033`) entfallen dadurch — sicherstellen, dass sie nicht doppelt bleiben.

- [ ] **Step 3: ShowMultiModelResults um "Voraus"-Stil erweitern**

`ShowMultiModelResults` (`:3095`) Signatur und Body anpassen: zweiter Parameter `IReadOnlyList<SegmentedFinding> segmented`. SAM-Masken weiterhin rendern; fuer `Voraus`-Findings ein abgesetztes Overlay (gestricheltes Rechteck + Label "voraus") auf `CodingOverlayCanvas` zeichnen:

```csharp
    private void ShowMultiModelResults(SingleFrameResult mmResult, IReadOnlyList<SegmentedFinding> segmented)
    {
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);

        if (mmResult.SamResponse != null)
        {
            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                mmResult.SamResponse,
                mmResult.QuantifiedMasks,
                CodingOverlayCanvas.ActualWidth,
                CodingOverlayCanvas.ActualHeight,
                logger: _serviceProvider?.LoggerFactory.CreateLogger("SamMaskRenderer"));
        }

        // "Voraus"-Befunde gestrichelt markieren.
        double cw = CodingOverlayCanvas.ActualWidth, ch = CodingOverlayCanvas.ActualHeight;
        foreach (var s in segmented.Where(s => !s.Proximity.IsCodierbar))
        {
            if (s.Mask.Bbox.Count < 4 || mmResult.SamResponse == null) continue;
            double iw = mmResult.SamResponse.ImageWidth, ih = mmResult.SamResponse.ImageHeight;
            if (iw <= 0 || ih <= 0) continue;
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = System.Math.Max(1, (s.Mask.Bbox[2] - s.Mask.Bbox[0]) / iw * cw),
                Height = System.Math.Max(1, (s.Mask.Bbox[3] - s.Mask.Bbox[1]) / ih * ch),
                Stroke = new SolidColorBrush(Color.FromArgb(200, 0xF5, 0x9E, 0x0B)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Fill = System.Windows.Media.Brushes.Transparent,
                Tag = Ai.Pipeline.SamMaskRenderer.MaskTag,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(rect, s.Mask.Bbox[0] / iw * cw);
            Canvas.SetTop(rect, s.Mask.Bbox[1] / ih * ch);
            CodingOverlayCanvas.Children.Add(rect);
        }

        _showReferenceDn = true;
        RenderReferenceDn();
    }
```

- [ ] **Step 4: AddMultiModelFindingsAsEvents auf SegmentedFinding umstellen**

Signatur aendern zu `AddMultiModelFindingsAsEvents(IReadOnlyList<SegmentedFinding> segmented, double captureTimestampSec)`. Die bisherige Index-Schleife (`:3138-3141`) ersetzen durch Iteration ueber `segmented`:

```csharp
        foreach (var seg in segmented)
        {
            var quant = seg.Quant;
            var dino = seg.Dino;
            // ... unveraenderter Rumpf, der quant/dino nutzt ...
        }
```

Der restliche Methodenrumpf (Pseudofinding-Bau, Resolver, Dedup, QualityGate) bleibt identisch — nur die Quelle von `quant`/`dino` aendert sich von Index auf `seg`.

- [ ] **Step 5: Build**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly -nologo`
Expected: 0 Fehler.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs
git commit -m "feat(metrierung): Live-Naehe-Gate im Codiermodus (Voraus anzeigen, nicht metrieren)"
```

---

## Task 5: Vollanalyse-Einhaengung

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs`

Kontext: nach `QuantifyAll` (`:392`) folgt die Schleife, die `EnhancedFinding` baut (`:401-424`). `pipeDiameterMm` ist vorhanden; eine `PipeCalibration` liegt im Video-Batch i.d.R. NICHT vor -> Fluchtpunkt Bildmitte, Rohrradius-Fallback 0.5.

- [ ] **Step 1: Naehe-Gate vor EnhancedFinding einbauen**

Den Block `var findings = new List<EnhancedFinding>(quantified.Count); for (...) {...}` (`:400-424`) so erweitern, dass pro Maske die Naehe geprueft wird und `Voraus` keine `EnhancedFinding` erzeugt:

```csharp
            var segmented = SegmentedFindingBuilder.Build(
                samResult, dinoResult.Detections, quantified,
                vanishX: 0.5, vanishY: 0.5, pipeRadiusNorm: 0.5,
                AuswertungPro.Next.Application.Ai.MetrierungProximityThresholds.Default);

            int proximitySuppressedCount = 0;
            var findings = new List<EnhancedFinding>(segmented.Count);
            foreach (var seg in segmented)
            {
                var q = seg.Quant;
                if (string.IsNullOrWhiteSpace(q.Label))
                    continue;
                if (!seg.Proximity.IsCodierbar)
                {
                    proximitySuppressedCount++;
                    continue;
                }

                var bbox = GetNormalizedBbox(seg.Mask, samResult.ImageWidth, samResult.ImageHeight);
                findings.Add(new EnhancedFinding(
                    Label: q.Label,
                    VsaCodeHint: VsaCodeResolver.InferCodeFromLabel(q.Label),
                    Severity: EstimateSeverity(q),
                    PositionClock: NormalizeClockPosition(q.ClockPosition),
                    ExtentPercent: q.ExtentPercent,
                    HeightMm: q.HeightMm,
                    WidthMm: q.WidthMm,
                    IntrusionPercent: q.IntrusionPercent,
                    CrossSectionReductionPercent: q.CrossSectionReductionPercent,
                    DiameterReductionMm: null,
                    BboxX1: bbox.X1, BboxY1: bbox.Y1, BboxX2: bbox.X2, BboxY2: bbox.Y2,
                    Notes: $"DINO conf={(seg.Dino?.Confidence ?? q.Confidence):F2}"
                ));
            }
```

- [ ] **Step 2: ProximitySuppressedCount leichtgewichtig loggen/zaehlen**

Direkt nach der Schleife (nur falls > 0, ein Eintrag pro Frame):

```csharp
            if (proximitySuppressedCount > 0)
            {
                trace.ProximitySuppressedCount = proximitySuppressedCount;
                _logger.LogDebug("Frame {Frame}: {Count} Befund(e) als 'ahead_of_camera' nicht metriert.",
                    frameIndex, proximitySuppressedCount);
            }
```

Dafuer im Trace-Objekt (gleiche Datei bzw. dessen Definition) ein Feld ergaenzen: `public int ProximitySuppressedCount { get; set; }`. Falls das Trace-Objekt ein Record ist, ein passendes Property ergaenzen; sonst nur den `LogDebug` behalten (Zaehler optional).

- [ ] **Step 3: Build**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly -nologo`
Expected: 0 Fehler. (Pruefen, dass `GetNormalizedBbox`, `EstimateSeverity`, `NormalizeClockPosition`, `VsaCodeResolver.InferCodeFromLabel` unveraendert erreichbar sind — sie waren es vorher in derselben Methode.)

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs
git commit -m "feat(metrierung): Vollanalyse-Naehe-Gate + ProximitySuppressedCount"
```

---

## Task 6: Abschluss-Verifikation

- [ ] **Step 1: Voller Build + voller Pipeline-Testlauf**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly -nologo`
Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj -nologo`
Expected: 0 Build-Fehler; alle bisherigen Tests gruen + neue Evaluator-/Builder-Tests gruen.

- [ ] **Step 2: Akzeptanzkriterien (Spec §13) gegen den Code abhaken** — manuell.

---

## Self-Review (gegen Spec 2026-06-03-metrierung-naehe-gate)

- §3 Ansatz 1 (reine C#-Logik): Task 1/2. ✓
- §4 Naehe-Regel (Toleranz, konservativ, 4 Parameter): Task 1 (Thresholds) + Task 2 (Evaluator). ✓
- §5.5 SegmentedFinding (Masken-basiert, bbox+Label-Zuordnung, robust gegen skipped boxes): Task 3. ✓
- §6.1 Live-Einhaengung + "Voraus"-Overlay + Statusmeldung (alle voraus / gemischt): Task 4. ✓
- §6.2 Vollanalyse-Einhaengung + ProximitySuppressedCount (ahead_of_camera): Task 5. ✓
- §7 Verhalten (Voraus anzeigen, kein Meter/Event): Task 4/5. ✓
- §8 Tests (Evaluator-Faelle + Zuordnung inkl. skipped-box): Task 2 + Task 3. ✓
- §9 Geltung beide Pfade, alle Ereignistypen: Task 4 + Task 5. ✓

Typkonsistenz geprueft: `MetrierungProximity`/`MetrierungProximityResult.IsCodierbar`/`MetrierungProximityThresholds.Default`/`MetrierungProximityEvaluator.Evaluate`/`SegmentedFinding`/`SegmentedFindingBuilder.Build` durchgaengig identisch verwendet. Offene Verifikation in der Umsetzung: exakte Konstruktor-Signaturen von `SamResponse`/`SamMaskResult`/`QuantifiedMask` (Task 3 Step 3) und das Trace-Feld in der Vollanalyse (Task 5 Step 2) — beide als Pruefhinweis im jeweiligen Step vermerkt.
