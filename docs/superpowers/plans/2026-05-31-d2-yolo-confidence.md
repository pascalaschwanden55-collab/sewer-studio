# D2: Echte YOLO-Confidence statt Festwert 0.8 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Das künstlich angehobene „Grün" im Live-Multi-Model-Codierpfad beseitigen, das durch einen hartkodierten `YoloConf: 0.8` in der QualityGate-Evidenz entsteht.

**Architecture:** Der Live-Pfad (`PlayerWindow.AddMultiModelFindingsAsEvents`) baut pro Befund einen `EvidenceVector` und lässt ihn vom `QualityGateService` zu einem Composite-Score fusionieren (Green ≥ 0.75 / Yellow ≥ 0.45). Aktuell wird `YoloConf` auf den Festwert `0.8` gesetzt — ein per-Frame-fremdes, künstliches Signal, das den Score Richtung Grün zieht. Der `QualityGateService` renormalisiert die Gewichte bereits über die **vorhandenen** Signale (nullable), d.h. ein weggelassenes Signal ist sauber.

**Tech Stack:** .NET 10, xUnit, `QualityGateService`/`EvidenceVector`/`CategoryWeights` (Infrastructure/Application Ai.QualityGate).

---

## Entscheidung zuerst (Design-Fork) — bitte wählen, bevor Code

**Option B — YoloConf im Live-Evidence weglassen (EMPFOHLEN).**
YOLO ist hier nur ein **Frame-Relevanz-Vorscreen** (`IsRelevant`), kein Per-Befund-Signal: eine YOLO-Box bestätigt nicht, dass *dieser* DINO/SAM-Befund echt ist. Der Festwert 0.8 (und auch eine Frame-Max-Confidence) ist also konzeptionell falsch als Per-Befund-Evidenz. Lösung: `YoloConf` weglassen → der Composite beruht nur noch auf echten Per-Befund-Signalen (DinoConf, SamMaskStability, PlausibilityScore), die QG renormalisiert. Kleinste, ehrlichste Änderung; keine DTO-/Sidecar-Änderung; gut testbar am QualityGate.

**Option A — echte Frame-Level-YOLO-Confidence propagieren (Alternative).**
`SingleFrameResult` um `YoloMaxConfidence` erweitern, in `SingleFrameMultiModelService` aus `yoloResp.Detections.Max(Confidence)` füllen, im Live-Pfad statt 0.8 verwenden. Behält ein YOLO-Signal, ist aber **frame-level** (grob, nicht per Befund) und schwerer testbar (Pfad braucht HTTP-Mock). Schritte in Anhang A.

> Empfehlung: **Option B**. Begründung oben. Option A nur, wenn ihr bewusst ein (grobes) YOLO-Signal behalten wollt.

---

## Belegstellen (verifiziert, Stand HEAD b8fdf133)

- Festwert: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs:2971-2976`
  ```csharp
  var evidence = new EvidenceVector(
      YoloConf: 0.8,
      DinoConf: dinoConf,
      SamMaskStability: quant.Confidence,
      PlausibilityScore: officialLabel != null ? 0.8 : 0.4
  );
  ```
- QG renormalisiert über vorhandene Signale: `Infrastructure/Ai/QualityGate/QualityGateService.cs:41-67`
- Default-Gewichte: `Infrastructure/Ai/QualityGate/CategoryWeights.cs:13-20` (WYolo=0.10, WDino=0.15, WSam=0.10, WPlausibility=0.10)
- `EvidenceVector`-Signale sind alle `double? = null`: `Application/Ai/QualityGate/EvidenceVector.cs:9-19`

**Rechen-Demonstration der Inflation** (Default-Gewichte, Befund mit DinoConf=0.8, Sam=0.6, Plaus=0.8):
- MIT `YoloConf=0.8`: Composite = (0.8·0.10 + 0.8·0.15 + 0.6·0.10 + 0.8·0.10) / (0.10+0.15+0.10+0.10) = 0.34/0.45 = **0.756 → Green**
- OHNE YoloConf: Composite = (0.8·0.15 + 0.6·0.10 + 0.8·0.10) / (0.15+0.10+0.10) = 0.26/0.35 = **0.743 → Yellow**

Der Festwert kippt diesen Befund künstlich von Yellow auf Green. Genau das fixt Option B.

---

## File Structure (Option B)

| Datei | Verantwortung | Änderung |
|---|---|---|
| `tests/AuswertungPro.Next.Infrastructure.Tests/QualityGate/EvidenceFakeYoloConfTests.cs` | Regressions-Guard: dokumentiert, dass der Festwert 0.8 einen Grenzbefund künstlich grün macht und das Weglassen ihn korrekt gelb lässt | **Create** |
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs` | Live-Multi-Model-Evidenz ohne künstlichen YoloConf | **Modify** `:2971-2976` |

---

## Task 1: Regressions-Test (lockt die Kalibrierung)

**Files:**
- Create: `tests/AuswertungPro.Next.Infrastructure.Tests/QualityGate/EvidenceFakeYoloConfTests.cs`

- [ ] **Step 1: Failing test schreiben**

```csharp
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.QualityGate;

public class EvidenceFakeYoloConfTests
{
    // Default-Gewichte: WYolo=.10 WDino=.15 WSam=.10 WPlausibility=.10
    // Grenzbefund: DinoConf=0.8, Sam=0.6, Plaus=0.8
    private static readonly QualityGateService Gate = new();

    [Fact]
    public void FesterYoloConf_KippteGrenzbefund_KuenstlichAufGruen()
    {
        var mitFestwert = new EvidenceVector(
            YoloConf: 0.8, DinoConf: 0.8, SamMaskStability: 0.6, PlausibilityScore: 0.8);
        Assert.Equal(TrafficLight.Green, Gate.Evaluate(mitFestwert).TrafficLight);
    }

    [Fact]
    public void OhneYoloConf_BleibtGrenzbefund_KorrektGelb()
    {
        var ohneYolo = new EvidenceVector(
            DinoConf: 0.8, SamMaskStability: 0.6, PlausibilityScore: 0.8);
        Assert.Equal(TrafficLight.Yellow, Gate.Evaluate(ohneYolo).TrafficLight);
    }
}
```

- [ ] **Step 2: Test laufen lassen, Erwartung beobachten**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter FullyQualifiedName~EvidenceFakeYoloConf -v minimal`
Erwartung: BEIDE Tests **bestehen sofort** — sie testen das (unveränderte) QG-Verhalten und dokumentieren die Inflation. (Dies ist ein Charakterisierungs-/Regressions-Test, kein Rot→Grün-TDD: er hält die Kalibrierungs-Annahme fest, auf die sich der Code-Fix in Task 2 stützt.)
Falls ein Test fehlschlägt: die Default-Gewichte/Schwellen wurden zwischenzeitlich geändert → Composite-Rechnung im Plan neu prüfen, bevor weitergemacht wird.

- [ ] **Step 3: Commit**

```bash
git add tests/AuswertungPro.Next.Infrastructure.Tests/QualityGate/EvidenceFakeYoloConfTests.cs
git commit -m "test(ai): dokumentiert YoloConf-0.8-Inflation im QualityGate (D2-Vorbereitung)"
```

---

## Task 2: Künstlichen YoloConf aus dem Live-Pfad entfernen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs:2971-2976`

- [ ] **Step 1: Evidenz-Konstruktion ändern (YoloConf weglassen)**

Vorher:
```csharp
            var evidence = new EvidenceVector(
                YoloConf: 0.8,
                DinoConf: dinoConf,
                SamMaskStability: quant.Confidence,
                PlausibilityScore: officialLabel != null ? 0.8 : 0.4
            );
```
Nachher:
```csharp
            // D2: KEIN kuenstlicher YoloConf-Festwert mehr. YOLO ist hier nur ein
            // Frame-Relevanz-Vorscreen, kein Per-Befund-Signal -> weglassen. Der
            // QualityGateService renormalisiert die Gewichte ueber die vorhandenen,
            // echten Per-Befund-Signale (DINO, SAM, Plausibilitaet).
            var evidence = new EvidenceVector(
                DinoConf: dinoConf,
                SamMaskStability: quant.Confidence,
                PlausibilityScore: officialLabel != null ? 0.8 : 0.4
            );
```

- [ ] **Step 2: Build**

Run: `dotnet build AuswertungPro.sln -c Debug -v minimal`
Erwartung: 0 Fehler, 0 Warnungen.

- [ ] **Step 3: Volle Tests**

Run: `dotnet test AuswertungPro.sln --no-build -v minimal`
Erwartung: 768 bestanden (766 + 2 neue), 1 übersprungen.

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs
git commit -m "fix(ai): kuenstlichen YoloConf-Festwert aus Live-Multi-Model-Evidenz entfernt (D2)"
```

---

## Anhang A — Alternative (Option A: echte Frame-YOLO-Confidence propagieren)

Nur falls Option A gewählt wird. Ersetzt Task 2 (Task 1 bleibt als Guard sinnvoll).

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/SingleFrameMultiModelService.cs` (Record + 3 Konstruktionen)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs:2972`

- [ ] **A-Step 1:** `SingleFrameResult` um Feld erweitern (`:124-142`):
  ```csharp
  public sealed record SingleFrameResult(
      bool IsRelevant,
      IReadOnlyList<DinoDetectionDto> DinoDetections,
      SamResponse? SamResponse,
      IReadOnlyList<MaskQuantificationService.QuantifiedMask> QuantifiedMasks,
      double YoloTimeMs,
      double DinoTimeMs,
      double SamTimeMs,
      string? Error,
      double? YoloMaxConfidence = null)   // NEU
  ```
  `Empty(...)` bleibt unverändert (Default null).

- [ ] **A-Step 2:** In `AnalyzeFrameAsync` YOLO-Max berechnen und in ALLEN drei `new SingleFrameResult(...)`-Stellen (`:62`, `:78`, `:105`) mitgeben:
  ```csharp
  double? yoloMax = yoloResp.Detections.Count > 0
      ? yoloResp.Detections.Max(d => d.Confidence)
      : (double?)null;
  ```
  (Beim not-relevant-Return `:62` ebenfalls `YoloMaxConfidence: yoloMax`.)

- [ ] **A-Step 3:** `PlayerWindow.Coding.cs:2972` → `YoloConf: mmResult.YoloMaxConfidence` (nullable; ist es null, überspringt die QG es ohnehin).

- [ ] **A-Step 4:** Build + Tests + Commit `fix(ai): echte Frame-YOLO-Confidence statt Festwert (D2, Option A)`.

> Hinweis: Option A ist nur am Record-Feld unit-testbar; der volle Pfad (`AnalyzeFrameAsync`) braucht einen HTTP-Mock für `VisionPipelineClient` und ist hier bewusst nicht abgedeckt.

---

## Ausdrücklich NICHT in D2 (separate Themen)

- Der **Batch-Pfad** (`MultiModelAnalysisService`) setzt YoloConf binär 1.0/0.0 (IsRelevant) und befüllt SamMaskStability/QwenVisionConf laut Kommentar nicht — eigener Befund (k3/Phantom-Liste), nicht Teil von D2.
- **D3** (Pfad-Asymmetrie der Ampel / Lernkreis schließen) bleibt separat.

---

## Self-Review

1. **Spec-Abdeckung:** D2-Ziel „YoloConf:0.8 ehrlich machen" → Option B (Task 2) entfernt den Festwert; Option A (Anhang) propagiert den echten Wert. ✓
2. **Platzhalter-Scan:** Keine TODOs; Testcode vollständig; exakte Datei:Zeilen; Composite-Zahlen ausgerechnet. ✓
3. **Typkonsistenz:** `EvidenceVector` (Application.Ai.QualityGate), `QualityGateService`/`CategoryWeights` (Infrastructure.Ai.QualityGate), `TrafficLight` Enum — Namen gegen Code geprüft. `SingleFrameResult.YoloMaxConfidence` (Option A) konsistent zwischen Record-Def und Verwendung. ✓
4. **Hinweis Testzahl:** „768" in Task 2 Step 3 = aktuelle 766 + 2 neue aus Task 1; falls zwischenzeitlich Tests dazukommen, Zahl entsprechend lesen (qualitativ: alle grün).
