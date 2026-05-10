# Phase 6.3 Vorbereitung: MultiModelAnalysisService WPF-frei machen — Mini-ADR

Datum: 2026-05-10
Status: **Done** (Steps 1-3 in 1 Commit, File-Move als Folge-Slice)

Geliefert:
- ADR `cb0c8c9` — Mini-ADR Status: Entschieden
- Steps 1-3 `f25ec49` — IPipeCalibrationFromBytes-Interface +
  WpfPipeCalibrationFromBytes-Impl + App.xaml.cs-Wiring +
  MultiModelAnalysisService konsumiert Provider statt BitmapDecoder

Verifikation: grep auf System.Windows / BitmapDecoder /
AutoCalibrationService in MultiModelAnalysisService liefert nur noch
Kommentar-Treffer. Service ist WPF-frei.

Build: 0 Warn / 0 Err. Tests: 1025 PASS / 1 SKIP.

**Folge-Slice** (nicht in dieser ADR):
File-Move MultiModelAnalysisService.cs + MultiModelAnalysisService.Helpers.cs
nach Infrastructure/Ai/Pipeline. Nicht-trivial weil:
- Der Namespace `AuswertungPro.Next.UI.Ai.Pipeline` enthaelt
  mehrere Klassen (DetectionAggregator, MaskQuantificationService,
  ...) die mitziehen muessten oder einzeln aufgesplittet werden.
- ~15 Aufrufer-Files mit `using AuswertungPro.Next.UI.Ai.Pipeline`
  brauchen Update.

Eigener Slice mit eigenem Mini-ADR — die Voraussetzung
(WPF-Freiheit) ist mit dieser ADR erfuellt.

Vorgeschichte:
- AUDIT_SEWERSTUDIO_2026-04-23.md: ARCH-H5 — KI-Services aus UI-Layer
  nach Application/Infrastructure migrieren, "Thin-AI"-Prinzip aus
  CLAUDE.md. CRITICAL.
- PROGRAMMAUDIT_AKTUELL_2026-05-08.md Item 10: "MultiModelAnalysisService
  liegt noch in UI; IImageBitmapAnalyzer ist als Voraussetzung fuer
  Migration vorhanden."
- IImageBitmapAnalyzer (Application/Ai/Imaging) liefert Metadaten,
  IImagePixelDecoder (Application/Imaging) liefert Bgra32-Pixels.
- AutoCalibrationService.TryAutoCalibrate(BitmapSource, int) lebt in
  UI/Ai/ und ist die einzige verbleibende WPF-Kopplung in
  MultiModelAnalysisService (zwei Sites: Zeile ~196 + ~1249).

## Was diese ADR macht

Definiert eine Abstraktion `IPipeCalibrationFromBytes`, die im
Application-Layer lebt, in UI implementiert wird (mit der bestehenden
AutoCalibrationService + BitmapDecoder), und im
MultiModelAnalysisService die direkten BitmapDecoder-Calls ersetzt.

Damit ist MultiModelAnalysisService WPF-frei. Der finale File-Move nach
Infrastructure/Ai/Pipeline ist **dann** ein eigener Folge-Slice
(rein mechanisch: namespace + project reference).

## Was diese ADR NICHT macht

- Keine Aenderung an AutoCalibrationService.cs selbst.
- Keinen File-Move von MultiModelAnalysisService — das ist Folge-Slice.
- Keine Aenderungen an anderen UI/Ai/-Services.
- Keine Erweiterung von IImageBitmapAnalyzer (das liefert nur Metadaten).
- Keine DI-Container-Refactor — bleibt beim Provider-Pattern wie bei
  IImagePixelDecoderProvider.

## Designfragen + Entscheidungen

### Q1 — Wo lebt die Abstraktion?

`AuswertungPro.Next.Application.Ai.Imaging.IPipeCalibrationFromBytes`
neben dem schon existierenden `IImageBitmapAnalyzer`. Entscheidung
bestaetigt.

### Q2 — Was ist die API?

```csharp
public interface IPipeCalibrationFromBytes
{
    /// <summary>Versucht aus PNG/JPG-Bytes eine Pipe-Calibration zu
    /// extrahieren. null wenn nicht decodierbar oder Algo nichts findet.</summary>
    PipeCalibration? TryCalibrate(byte[] imageBytes, int nominalDiameterMm);
}
```

Minimale Schnittstelle. Bytes rein, Calibration raus.

### Q3 — Provider-Pattern oder DI?

Provider-Pattern (analog zu `ImagePixelDecoderProvider`). Static
Accessor in Application; UI registriert die WPF-Impl beim App-Start.

```csharp
public static class PipeCalibrationFromBytesProvider
{
    private static IPipeCalibrationFromBytes? _impl;
    public static void SetImplementation(IPipeCalibrationFromBytes impl) => _impl = impl;
    public static IPipeCalibrationFromBytes? Instance => _impl;
}
```

MultiModelAnalysisService nutzt `PipeCalibrationFromBytesProvider.Instance`
mit Null-Fallback ("kein Auto-Calibration moeglich").

### Q4 — Was bei null-Provider?

In Tests / wenn UI-Schicht nicht initialisiert ist: null-check und
Calibration-Schritt ueberspringen. Aequivalent zum heutigen
"TryAutoCalibrate liefert null"-Pfad.

## Migrations-Schnitt

### Step 1: Application-Interface + Provider

`src/AuswertungPro.Next.Application/Ai/Imaging/IPipeCalibrationFromBytes.cs`
mit Interface + Provider.

### Step 2: UI-Impl + Wiring

`src/AuswertungPro.Next.UI/Imaging/WpfPipeCalibrationFromBytes.cs` mit
Impl die intern BitmapDecoder + AutoCalibrationService.TryAutoCalibrate
nutzt.

App.xaml.cs registriert via
`PipeCalibrationFromBytesProvider.SetImplementation(new WpfPipeCalibrationFromBytes())`.

### Step 3: MultiModelAnalysisService konsumiert Provider

Beide BitmapDecoder-Sites in MultiModelAnalysisService.cs durch
`PipeCalibrationFromBytesProvider.Instance?.TryCalibrate(frameBytes, dn)`
ersetzen. Using-Statement fuer
`AuswertungPro.Next.UI.Ai` (AutoCalibrationService) + WPF-Imaging
entfernen.

### Step 4: Doku + CHANGELOG

ADR Status auf Done. CHANGELOG-Eintrag.

### Verifikation

- Build: 0 Warn / 0 Err (alle 4 Steps).
- Tests: keine Regression.
- Kein UI-Smoke noetig — die Verhaltensgrenze (BitmapDecoder direkt vs.
  via Abstraction die intern dasselbe BitmapDecoder ruft) ist null.

## Was diese ADR ausklammert

- File-Move MultiModelAnalysisService.cs → Infrastructure/Ai/Pipeline
  (eigener Folge-Slice, mechanisch).
- Migration anderer UI/Ai/-Services (eigener Plan).
- Refactor von AutoCalibrationService selbst (lebt weiter in UI/Ai/).

## Erwartetes Ergebnis

- MultiModelAnalysisService.cs hat **keine** WPF-Imports mehr.
- File kann in Folge-Slice ohne Code-Aenderung verschoben werden.
- ARCH-H5-Schuld einen Schritt kleiner.
