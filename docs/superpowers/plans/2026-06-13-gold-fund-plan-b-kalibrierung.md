# Gold-Fund Plan B – Etappe 1: Kalibrierungs-Wahrheit

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Jede Pixel→mm-Umrechnung traegt ehrlich ihre Herkunft (Keine / Automatisch / Manuell), und der Bug "Auto-Kalibrierung markiert sich als manuell" wird behoben. Damit ist das Fundament gelegt, um Quantifizierungen spaeter verlaesslichkeitsgerecht zu speichern (Etappe 2).

**Architecture:** Neues Enum `CalibrationSource { None, Auto, Manual }` an `PipeCalibration` (Domain). `WasManuallyCalibrated` wird durch `Source` abgeloest. `AutoCalibrationService` setzt `Source = Auto` (statt faelschlich Manual). Die drei echten manuellen Kalibrier-Stellen setzen `Source = Manual`. `MaskQuantificationService.QuantifiedMask` bekommt die Kalibrierungs-Herkunft mitgereicht, sodass ein mm-Wert immer weiss, worauf er beruht. 3-Stufen-Verlaesslichkeit (User-Entscheidung 2026-06-13): Manual=verlaesslich, Auto=Vorschlag, None=geschaetzt (70%-Fallback).

**Tech Stack:** C#/.NET 10, xUnit.

**Scope:** NUR die Kalibrierungs-/Quantifizierungs-Herkunft. NICHT in diesem Plan: Speichern der Quantifizierung im TrainingSample, `QuantificationSource` (Suggested/Confirmed/Corrected), Verdrahtung in den Accept-Pfad — das ist **Etappe 2** (eigener Plan, danach, weil es davon abhaengt wie dieses Modell hier final aussieht).

---

## File Structure

- Modify: `src/AuswertungPro.Next.Domain/Models/CodingSession.cs` — `enum CalibrationSource` + `PipeCalibration.Source` + `IsCalibrated`.
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Calibration/AutoCalibrationService.cs` — `Source = Auto` (Bug-Fix).
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs` (Z.~1449), `.../CodingModeWindow.xaml.cs` (Z.~707), `.../PhotoMeasurementWindow.xaml.cs` (Z.~524) — `Source = Manual`.
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MaskQuantificationService.cs` — `QuantifiedMask.CalibrationSource` + Durchreichung.
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/AutoCalibrationServiceTests.cs`, `tests/AuswertungPro.Next.UI.Tests/MaskQuantificationServiceTests.cs`.

Verifizierte Ist-Fakten:
- `PipeCalibration` (CodingSession.cs:153-222): `WasManuallyCalibrated` (bool get/set); `IsCalibrated => WasManuallyCalibrated && NormalizedDiameter > 0`; Methoden `NormToMm`/`PixelToMm`/`PointToClockHour` etc.
- `WasManuallyCalibrated = true` wird an 4 Stellen gesetzt: AutoCalibrationService.cs:67 (BUG), PlayerWindow.Coding.cs:1449, CodingModeWindow.xaml.cs:707, PhotoMeasurementWindow.xaml.cs:524.
- `AutoCalibrationService.TryAutoCalibrate` (Z.61-68) gibt `new PipeCalibration { NominalDiameterMm, NormalizedDiameter, PipePixelDiameter, PipeCenter, WasManuallyCalibrated = true }`.
- `MaskQuantificationService.QuantifiedMask` = record (Label, Confidence, HeightMm?, WidthMm?, ExtentPercent?, CrossSectionReductionPercent?, IntrusionPercent?, ClockPosition?). `Quantify(mask, w, h, dn)` nutzt hartkodiert `PipeImageWidthRatio = 0.70`. `Quantify(mask, w, h, dn, calibration?)` nutzt `calibration.NormalizedDiameter` nur wenn `calibration.IsCalibrated`, sonst Fallback auf die 0.70-Ueberladung.

---

### Task 1: Kalibrierungs-Herkunft (CalibrationSource) + Bug-Fix

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/Models/CodingSession.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Calibration/AutoCalibrationService.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`, `CodingModeWindow.xaml.cs`, `PhotoMeasurementWindow.xaml.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/AutoCalibrationServiceTests.cs`

- [ ] **Step 0: Serialisierung verifizieren**

Pruefen, ob `PipeCalibration` irgendwo per JSON persistiert/geladen wird (Grep `PipeCalibration` in Serialisierungs-/Session-Speicher-Pfaden, z.B. CodingSession-Speicherung):
```powershell
Get-ChildItem -Recurse -Filter *.cs src | Select-String -Pattern "Serialize.*Calibration|Calibration.*Serialize|Session.*Save" | Select-Object -First 20
```
- Wenn `PipeCalibration` NICHT serialisiert wird (sitzungsbezogen, pro Frame neu erstellt) → `WasManuallyCalibrated` kann zu einem computed Property werden (Step 2 Variante A).
- Wenn DOCH serialisiert → `WasManuallyCalibrated` als get/set behalten, `IsCalibrated` beide Quellen beruecksichtigen (Step 2 Variante B). Im Zweifel Variante B (rueckwaerts­kompatibel).

- [ ] **Step 1: Failing Test in AutoCalibrationServiceTests.cs**

Schau dir die vorhandenen Tests dieser Datei an (sie bauen einen `GrayscaleImageFrame` mit kuenstlichen Rohrkanten). Ergaenze einen Test, der bestaetigt, dass eine erfolgreiche Auto-Kalibrierung `Source = Auto` und NICHT manuell ist:
```csharp
    [Fact]
    public void TryAutoCalibrate_MarkiertAlsAuto_NichtManuell()
    {
        // Frame wie in den vorhandenen erfolgreichen Tests dieser Datei aufbauen
        // (klare Rohrkanten, sodass TryAutoCalibrate != null liefert).
        var frame = BuildFrameWithPipeEdges();   // vorhandener Helfer/Muster dieser Datei
        var cal = AutoCalibrationService.TryAutoCalibrate(frame, nominalDiameterMm: 300);

        Assert.NotNull(cal);
        Assert.Equal(CalibrationSource.Auto, cal!.Source);
        Assert.False(cal.WasManuallyCalibrated);   // Bug-Fix: Auto ist NICHT manuell
        Assert.True(cal.IsCalibrated);             // Auto gilt als kalibriert (3-Stufen-Modell)
    }
```
(Falls die Datei keinen Frame-Builder hat, nutze das Aufbau-Muster aus dem bestehenden Positiv-Test dieser Datei.)

- [ ] **Step 2: Enum + Source-Property**

In `CodingSession.cs` ein Enum ergaenzen (nahe `PipeCalibration`):
```csharp
/// <summary>Herkunft der Pixel→mm-Kalibrierung. None=70%-Schaetzung, Auto=Rohrkanten-Erkennung, Manual=Referenzlinie.</summary>
public enum CalibrationSource { None, Auto, Manual }
```
In `PipeCalibration` das Feld ergaenzen:
```csharp
    /// <summary>Herkunft der Kalibrierung (3-Stufen-Verlaesslichkeit). Default None = 70%-Schaetzung.</summary>
    public CalibrationSource Source { get; set; } = CalibrationSource.None;
```

**Variante A (PipeCalibration NICHT serialisiert):** `WasManuallyCalibrated` zu computed machen, alten Setter entfernen:
```csharp
    /// <summary>Manuell kalibriert (Referenzlinie gezogen)? Abgeleitet aus Source.</summary>
    public bool WasManuallyCalibrated => Source == CalibrationSource.Manual;
```
und `IsCalibrated`:
```csharp
    public bool IsCalibrated => Source != CalibrationSource.None && NormalizedDiameter > 0;
```

**Variante B (PipeCalibration serialisiert):** `WasManuallyCalibrated` als get/set belassen, `IsCalibrated` beide beruecksichtigen:
```csharp
    public bool IsCalibrated =>
        (Source != CalibrationSource.None || WasManuallyCalibrated) && NormalizedDiameter > 0;
```
und beim Setzen von `Source = Manual` zusaetzlich `WasManuallyCalibrated = true` lassen (Konsistenz alt/neu).

- [ ] **Step 3: AutoCalibrationService — Source = Auto (Bug-Fix)**

In `AutoCalibrationService.cs` im `return new PipeCalibration { ... }` (Z.61-68) `WasManuallyCalibrated = true` ersetzen durch:
```csharp
            Source = CalibrationSource.Auto
```
(Variante B: zusaetzlich `WasManuallyCalibrated` NICHT setzen — default false.)
Namespace fuer `CalibrationSource` ggf. via `using AuswertungPro.Next.Domain.Models;` (ist in der Datei bereits vorhanden).

- [ ] **Step 4: Drei manuelle Stellen — Source = Manual**

An diesen drei Stellen `WasManuallyCalibrated = true` ersetzen durch `Source = CalibrationSource.Manual` (Variante A) bzw. ergaenzen (Variante B):
- `PlayerWindow.Coding.cs` ~Z.1449 (im `new PipeCalibration { ... }`).
- `CodingModeWindow.xaml.cs` ~Z.707 (im `new PipeCalibration { ... }`).
- `PhotoMeasurementWindow.xaml.cs` ~Z.524 (`_calibration.WasManuallyCalibrated = true;` → `_calibration.Source = CalibrationSource.Manual;`).
(Falls `WasManuallyCalibrated` in Variante A computed wurde, MUESSEN diese drei Stellen auf `Source` umgestellt sein, sonst Compile-Fehler — der Build zeigt sie.)

- [ ] **Step 5: GREEN + Build + Tests**

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter TryAutoCalibrate_MarkiertAlsAuto_NichtManuell -v minimal
dotnet build AuswertungPro.sln -v minimal
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests -v minimal
```
Erwartung: neuer Test PASS, `0 Fehler`, ganze Infra-Suite gruen. Falls bestehende AutoCalibration-/Kalibrierungs-Tests `WasManuallyCalibrated`-Verhalten pruefen, an das neue Modell anpassen (Auto → false ist das gewollte neue Verhalten).

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.Domain/Models/CodingSession.cs src/AuswertungPro.Next.Infrastructure/Ai/Calibration/AutoCalibrationService.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs src/AuswertungPro.Next.UI/Views/Windows/CodingModeWindow.xaml.cs src/AuswertungPro.Next.UI/Views/Windows/PhotoMeasurementWindow.xaml.cs tests/AuswertungPro.Next.Infrastructure.Tests/AutoCalibrationServiceTests.cs
git commit -m "Gold-Fund/Kalibrierung: CalibrationSource (None/Auto/Manual) + Bug-Fix Auto-gilt-als-manuell"
```

---

### Task 2: Quantifizierung traegt die Kalibrierungs-Herkunft

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MaskQuantificationService.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/MaskQuantificationServiceTests.cs`

- [ ] **Step 1: Failing Tests in MaskQuantificationServiceTests.cs**

Schau dir die vorhandenen Tests an (Aufbau eines `SamMaskResult` + Aufruf von `Quantify`). Ergaenze:
```csharp
    [Fact]
    public void Quantify_OhneKalibrierung_HerkunftNone()
    {
        var mask = BuildMask();   // vorhandenes Muster dieser Datei (SamMaskResult)
        var q = MaskQuantificationService.Quantify(mask, 1920, 1080, 300);
        Assert.Equal(CalibrationSource.None, q.CalibrationSource);
    }

    [Fact]
    public void Quantify_MitManuellerKalibrierung_HerkunftManual()
    {
        var mask = BuildMask();
        var cal = new PipeCalibration { NominalDiameterMm = 300, NormalizedDiameter = 0.6, Source = CalibrationSource.Manual };
        var q = MaskQuantificationService.Quantify(mask, 1920, 1080, 300, cal);
        Assert.Equal(CalibrationSource.Manual, q.CalibrationSource);
    }

    [Fact]
    public void Quantify_MitAutoKalibrierung_HerkunftAuto()
    {
        var mask = BuildMask();
        var cal = new PipeCalibration { NominalDiameterMm = 300, NormalizedDiameter = 0.6, Source = CalibrationSource.Auto };
        var q = MaskQuantificationService.Quantify(mask, 1920, 1080, 300, cal);
        Assert.Equal(CalibrationSource.Auto, q.CalibrationSource);
    }
```

- [ ] **Step 2: RED**

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests --filter "Quantify_OhneKalibrierung_HerkunftNone|Quantify_MitManuellerKalibrierung_HerkunftManual|Quantify_MitAutoKalibrierung_HerkunftAuto" -v minimal
```
Erwartung: Compile-Fehler (`QuantifiedMask.CalibrationSource` existiert nicht).

- [ ] **Step 3: QuantifiedMask-Feld + Durchreichung**

In `MaskQuantificationService.cs` das Record erweitern:
```csharp
    public sealed record QuantifiedMask(
        string Label,
        double Confidence,
        int? HeightMm,
        int? WidthMm,
        int? ExtentPercent,
        int? CrossSectionReductionPercent,
        int? IntrusionPercent,
        string? ClockPosition,
        CalibrationSource CalibrationSource = CalibrationSource.None);
```
(Default `None` haelt bestehende `new QuantifiedMask(...)`-Aufrufe gueltig.)

In den `return new QuantifiedMask(...)` der Methode `QuantifyWithRatio` (der kalibrierte Pfad) die Herkunft aus der Kalibrierung uebergeben:
```csharp
            ClockPosition: ComputeClockPosition(mask.CentroidX, mask.CentroidY, imageWidth, imageHeight, calibration),
            CalibrationSource: calibration?.Source ?? CalibrationSource.None);
```
In der oeffentlichen Ueberladung `Quantify(mask, w, h, dn, calibration?)`: der nicht-kalibrierte Zweig ruft `Quantify(mask, w, h, dn)` (70%-Fallback) — dieser liefert `CalibrationSource.None` (Record-Default). Der kalibrierte Zweig ruft `QuantifyWithRatio(..., calibration)` und reicht `calibration.Source` durch (siehe oben).
WICHTIG: In der reinen `Quantify(mask, w, h, dn)`-Ueberladung (ohne Kalibrierung, 0.70) bleibt `CalibrationSource` auf dem Default `None` — nicht explizit setzen noetig, aber sicherstellen, dass dort kein anderer Wert gesetzt wird.

- [ ] **Step 4: GREEN + Build**

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests --filter "Quantify_OhneKalibrierung_HerkunftNone|Quantify_MitManuellerKalibrierung_HerkunftManual|Quantify_MitAutoKalibrierung_HerkunftAuto" -v minimal
dotnet build AuswertungPro.sln -v minimal
dotnet test tests/AuswertungPro.Next.UI.Tests -v minimal
```
Erwartung: 3 PASS, `0 Fehler`, ganze UI-Suite gruen.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MaskQuantificationService.cs tests/AuswertungPro.Next.UI.Tests/MaskQuantificationServiceTests.cs
git commit -m "Gold-Fund/Kalibrierung: QuantifiedMask traegt die Kalibrierungs-Herkunft"
```

---

## Self-Review

**Spec-Abdeckung:**
- 3-Stufen-Verlaesslichkeit (User-Entscheidung): `CalibrationSource` None/Auto/Manual → Task 1.
- Bug "Auto gilt als manuell/sicher": Task 1 (AutoCalibrationService Source=Auto, WasManuallyCalibrated=false) + Test.
- "Nichts Geratetes als Wahrheit": jede QuantifiedMask traegt ihre Herkunft; 70%-Fallback = `None` (geschaetzt) → Task 2 + Tests.
- Rueckwaerts­kompat: Step 0 entscheidet Variante A/B fuer `WasManuallyCalibrated`; `QuantifiedMask.CalibrationSource` mit Default `None` bricht keine bestehenden Aufrufe.

**Bewusst NICHT (= Etappe 2):** QuantificationSource (Suggested/Confirmed/Corrected), Quantifizierung im TrainingSample speichern, Accept-Pfad-Verdrahtung, mm/%-Werte als Gold mitschreiben. Diese Etappe haengt davon ab, wie `CalibrationSource` hier final aussieht — deshalb getrennt.

**Typ-Konsistenz:** `CalibrationSource` (Domain-Enum) durchgaengig in PipeCalibration + QuantifiedMask. `IsCalibrated` Source-basiert (Auto zaehlt jetzt als kalibriert → MaskQuantificationService nutzt auch Auto-NormalizedDiameter statt 70%).
