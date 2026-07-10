# Zentrale KI-Freigabe-Regel — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eine zentrale, kontextabhaengig strenge Freigabe-Regel einfuehren, die die widerspruechlichen `DefectStatusPolicy` (0.85 nur Confidence) und `AutoApprovalService` (0.92 + Gate + KB + Unsicherheit) vereinheitlicht und fuer Live-Codieren und Video-Analyse gilt.

**Architecture:** Neue reine Policy `StandardAiDecisionPolicy` (hinter `IAiDecisionPolicy`) in `Application/Ai`. Beide bestehenden Regeln delegieren an sie über ein gemeinsames Signal-Modell `AiDecisionSignals`. Zusaetzlich reichern die KI-Event-Factories/Workflows den `AiContext` schon beim Anlegen um die QualityGate-Ampel an, damit die Live-Anzeige die Regel anwenden kann.

**Tech Stack:** C# / .NET 10, xUnit 2.7. Reine Logik ohne I/O in der Application-Schicht.

## Global Constraints

- Kommentare, Commit-Messages, UI-Texte auf Deutsch (CLAUDE.md).
- Neue Logik als eigener Service mit Interface; kein `new` verstreut — `IAiDecisionPolicy` injizierbar mit statischer `Default`-Instanz (CLAUDE.md-Checkliste 1+3).
- Keine neuen NuGet-Pakete.
- TDD: erst der fehlschlagende Test, dann die minimale Implementierung. Haeufige Commits.
- Schwellen exakt: AutoAccept-Confidence = 0.92, Reject-Confidence = 0.60, Max-Unsicherheit = 0.15.
- Schichten: neue Policy in `AuswertungPro.Next.Application/Ai` (kein I/O). `TrafficLight`, `EvidenceVector`, `UncertaintyEstimate` liegen in `AuswertungPro.Next.Application.Ai.QualityGate`.
- Kein Umbau von QualityGate, Pipeline, Dedup oder Selbsttraining.

---

### Task 1: Zentrale Regel `StandardAiDecisionPolicy` + Typen

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/AiDecisionPolicy.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/StandardAiDecisionPolicyTests.cs`

**Interfaces:**
- Consumes: `TrafficLight` (enum Green/Yellow/Red) aus `AuswertungPro.Next.Application.Ai.QualityGate`.
- Produces:
  - `enum AiDecisionOutcome { AutoAccept, Review, Reject }`
  - `record AiDecision(AiDecisionOutcome Outcome, string Reason)`
  - `record AiDecisionSignals(double Confidence, TrafficLight? QualityGate = null, bool? KbAgreement = null, double? EpistemicUncertainty = null)`
  - `interface IAiDecisionPolicy { AiDecision Decide(AiDecisionSignals signals); }`
  - `class StandardAiDecisionPolicy : IAiDecisionPolicy` mit `static StandardAiDecisionPolicy Default { get; }` und `const double AutoAcceptConfidence = 0.92, RejectConfidence = 0.60, MaxEpistemicUncertainty = 0.15`.

- [ ] **Step 1: Fehlschlagenden Test schreiben**

Datei `tests/AuswertungPro.Next.Pipeline.Tests/StandardAiDecisionPolicyTests.cs`:

```csharp
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Regel-Tests fuer die zentrale KI-Freigabe (Audit Fix 3). Kontextabhaengig streng:
/// alle vorhandenen Belege muessen passen, Pflicht sind hohe Sicherheit + gruene Ampel.
/// </summary>
public sealed class StandardAiDecisionPolicyTests
{
    private static AiDecision Decide(AiDecisionSignals s) => StandardAiDecisionPolicy.Default.Decide(s);

    [Fact] // Live-Fall: zwei Belege (Sicherheit + Ampel) reichen fuer Gruen.
    public void LiveZweiBelege_HoheSicherheitUndGrueneAmpel_AutoAccept()
    {
        var d = Decide(new AiDecisionSignals(0.94, TrafficLight.Green));
        Assert.Equal(AiDecisionOutcome.AutoAccept, d.Outcome);
    }

    [Fact] // Der Kern-Fix: hohe Sicherheit, aber Ampel NICHT gruen -> nie AutoAccept.
    public void HoheSicherheit_AberGelbeAmpel_Review()
    {
        var d = Decide(new AiDecisionSignals(0.99, TrafficLight.Yellow));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }

    [Fact] // Ampel fehlt (Live ohne angereichertes Gate) -> kein zweiter Beleg -> Review.
    public void AmpelFehlt_Review()
    {
        var d = Decide(new AiDecisionSignals(0.99, QualityGate: null));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }

    [Fact] // Rote Ampel -> Reject, egal wie hoch die Sicherheit.
    public void RoteAmpel_Reject()
    {
        var d = Decide(new AiDecisionSignals(0.99, TrafficLight.Red));
        Assert.Equal(AiDecisionOutcome.Reject, d.Outcome);
    }

    [Fact] // Sicherheit unter Reject-Schwelle -> Reject.
    public void SehrNiedrigeSicherheit_Reject()
    {
        var d = Decide(new AiDecisionSignals(0.40, TrafficLight.Green));
        Assert.Equal(AiDecisionOutcome.Reject, d.Outcome);
    }

    [Fact] // Zwischenzone (0.60..0.92) -> Review.
    public void MittlereSicherheit_Review()
    {
        var d = Decide(new AiDecisionSignals(0.80, TrafficLight.Green));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }

    [Fact] // Video-Fall: volle Belegkette -> AutoAccept.
    public void VideoVolleKette_AutoAccept()
    {
        var d = Decide(new AiDecisionSignals(0.95, TrafficLight.Green, KbAgreement: true, EpistemicUncertainty: 0.05));
        Assert.Equal(AiDecisionOutcome.AutoAccept, d.Outcome);
    }

    [Fact] // Video: vorhandener KB-Beleg widerspricht -> Review trotz hoher Sicherheit + gruener Ampel.
    public void VideoKbWiderspruch_Review()
    {
        var d = Decide(new AiDecisionSignals(0.95, TrafficLight.Green, KbAgreement: false, EpistemicUncertainty: 0.05));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }

    [Fact] // Video: vorhandene Unsicherheit zu hoch -> Review.
    public void VideoHoheUnsicherheit_Review()
    {
        var d = Decide(new AiDecisionSignals(0.95, TrafficLight.Green, KbAgreement: true, EpistemicUncertainty: 0.30));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }
}
```

- [ ] **Step 2: Test ausfuehren, Fehlschlag bestaetigen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~StandardAiDecisionPolicy"`
Expected: FAIL — Kompilierfehler „Typ/Name AiDecisionSignals/StandardAiDecisionPolicy nicht gefunden".

- [ ] **Step 3: Minimale Implementierung schreiben**

Datei `src/AuswertungPro.Next.Application/Ai/AiDecisionPolicy.cs`:

```csharp
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ergebnis der zentralen Freigabe-Regel.</summary>
public enum AiDecisionOutcome
{
    AutoAccept, // Gruen: mehrere Belege passen, verlaesslich
    Review,     // Gelb: pruefen
    Reject      // Rot: unbedingt pruefen
}

/// <summary>Freigabe-Entscheidung mit Begruendung fuer Anzeige/Statistik.</summary>
public sealed record AiDecision(AiDecisionOutcome Outcome, string Reason);

/// <summary>
/// Belege eines KI-Befunds. Null = Beleg ist in diesem Kontext nicht vorhanden
/// (z.B. beim Live-Codieren gibt es weder Datenbank-Abgleich noch Unsicherheit).
/// </summary>
public sealed record AiDecisionSignals(
    double Confidence,
    TrafficLight? QualityGate = null,
    bool? KbAgreement = null,
    double? EpistemicUncertainty = null);

/// <summary>Zentrale KI-Freigabe-Regel (Audit Fix 3).</summary>
public interface IAiDecisionPolicy
{
    AiDecision Decide(AiDecisionSignals signals);
}

/// <summary>
/// Kontextabhaengig streng: Pflicht sind hohe Sicherheit (>= 0.92) UND gruene Ampel
/// (zwei unabhaengige Belege). Jeder zusaetzlich vorhandene Beleg (Datenbank-Abgleich,
/// Unsicherheit) darf nicht widersprechen. Fehlende Belege werden nicht gefordert.
/// </summary>
public sealed class StandardAiDecisionPolicy : IAiDecisionPolicy
{
    public const double AutoAcceptConfidence = 0.92;
    public const double RejectConfidence = 0.60;
    public const double MaxEpistemicUncertainty = 0.15;

    public static StandardAiDecisionPolicy Default { get; } = new();

    public AiDecision Decide(AiDecisionSignals s)
    {
        // Rote Ampel oder sehr niedrige Sicherheit -> sofort ablehnen.
        if (s.QualityGate == TrafficLight.Red)
            return new AiDecision(AiDecisionOutcome.Reject, "QualityGate steht auf Rot.");
        if (s.Confidence < RejectConfidence)
            return new AiDecision(AiDecisionOutcome.Reject, $"Sicherheit zu niedrig ({s.Confidence:P0}).");

        // Zwei Pflicht-Belege fuer Gruen: hohe Sicherheit UND gruene Ampel.
        if (s.Confidence < AutoAcceptConfidence)
            return new AiDecision(AiDecisionOutcome.Review, $"Sicherheit unter {AutoAcceptConfidence:P0}.");
        if (s.QualityGate != TrafficLight.Green)
            return new AiDecision(AiDecisionOutcome.Review, "QualityGate nicht auf Gruen.");

        // Jeder zusaetzlich vorhandene Beleg darf nicht widersprechen.
        if (s.KbAgreement == false)
            return new AiDecision(AiDecisionOutcome.Review, "Datenbank-Abgleich widerspricht.");
        if (s.EpistemicUncertainty is { } u && u >= MaxEpistemicUncertainty)
            return new AiDecision(AiDecisionOutcome.Review, $"Unsicherheit zu hoch ({u:F2}).");

        return new AiDecision(AiDecisionOutcome.AutoAccept, "Alle vorhandenen Belege bestaetigt.");
    }
}
```

- [ ] **Step 4: Test ausfuehren, Erfolg bestaetigen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~StandardAiDecisionPolicy"`
Expected: PASS — 9 Tests gruen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/AiDecisionPolicy.cs tests/AuswertungPro.Next.Pipeline.Tests/StandardAiDecisionPolicyTests.cs
git commit -m "feat(ki): zentrale Freigabe-Regel StandardAiDecisionPolicy (Audit Fix 3)"
```

---

### Task 2: `AutoApprovalService` an die zentrale Regel anschliessen

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/AutoApprovalService.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/AutoApprovalTests.cs` (drei Reason-Assertions)

**Interfaces:**
- Consumes: `IAiDecisionPolicy`, `AiDecisionSignals`, `StandardAiDecisionPolicy.Default`, `AiDecisionOutcome` aus Task 1. `MappedProtocolEntry` (Felder `Confidence`, `QualityGateResult`, `Detection.Evidence`, `Uncertainty`).
- Produces: unveraenderte oeffentliche API `AutoApprovalService.Evaluate(MappedProtocolEntry) -> AutoApprovalResult`; neuer optionaler Ctor-Parameter `IAiDecisionPolicy?`.

- [ ] **Step 1: Bestehende Tests laufen lassen (Ausgangslage)**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~AutoApprovalTests"`
Expected: PASS — 6 Tests gruen (Ausgangszustand vor Umbau).

- [ ] **Step 2: `AutoApprovalService` auf die Policy umstellen**

Ersetze den gesamten Inhalt von `src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/AutoApprovalService.cs` bis vor den `AutoApprovalResult`-Record:

```csharp
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

/// <summary>
/// Duenner Adapter: baut die Belege eines KI-Befunds aus dem MappedProtocolEntry und
/// laesst die zentrale IAiDecisionPolicy entscheiden (Audit Fix 3). Keine eigene Schwellen-Logik mehr.
/// </summary>
public sealed class AutoApprovalService
{
    private readonly IAiDecisionPolicy _policy;

    public AutoApprovalService(IAiDecisionPolicy? policy = null)
        => _policy = policy ?? StandardAiDecisionPolicy.Default;

    public AutoApprovalResult Evaluate(MappedProtocolEntry entry)
    {
        var signals = new AiDecisionSignals(
            Confidence: entry.Confidence,
            QualityGate: entry.QualityGateResult?.TrafficLight,
            KbAgreement: entry.Detection.Evidence?.KbCodeAgreement,
            EpistemicUncertainty: entry.Uncertainty?.EpistemicUncertainty);

        var decision = _policy.Decide(signals);
        return decision.Outcome == AiDecisionOutcome.AutoAccept
            ? AutoApprovalResult.Approved(decision.Reason)
            : AutoApprovalResult.Rejected(decision.Reason);
    }
}
```

Der `AutoApprovalResult`-Record am Dateiende bleibt unveraendert. Die frueheren Properties `MinConfidence`/`MaxEpistemicUncertainty` entfallen (Logik liegt jetzt in der Policy).

- [ ] **Step 3: Drei Reason-Assertions an die neuen (deutschen) Begruendungen anpassen**

In `tests/AuswertungPro.Next.UI.Tests/AutoApprovalTests.cs`:
- `LowConfidence_IsRejected`: `Assert.Contains("Confidence", result.Reason)` → `Assert.Contains("Sicherheit", result.Reason)`
- `YellowLight_IsRejected`: `Assert.Contains("Yellow", result.Reason)` → `Assert.Contains("Gruen", result.Reason)`
- `KbDisagrees_IsRejected`: `Assert.Contains("KB", result.Reason)` → `Assert.Contains("Datenbank", result.Reason)`

Die `Assert.True/False(result.IsApproved)` in allen 6 Tests bleiben unveraendert — die Urteile aendern sich nicht.

- [ ] **Step 4: Tests ausfuehren, Erfolg bestaetigen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~AutoApprovalTests"`
Expected: PASS — 6 Tests gruen (gleiche Urteile, angepasste Begruendungen).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/AutoApprovalService.cs tests/AuswertungPro.Next.UI.Tests/AutoApprovalTests.cs
git commit -m "refactor(ki): AutoApprovalService nutzt zentrale Freigabe-Regel"
```

---

### Task 3: `DefectStatusPolicy` (Live-Anzeige) an die zentrale Regel anschliessen

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/DefectStatusPolicy.cs`
- Modify: `tests/AuswertungPro.Next.Pipeline.Tests/DefectStatusPolicyTests.cs`

**Interfaces:**
- Consumes: `StandardAiDecisionPolicy.Default`, `AiDecisionSignals`, `AiDecisionOutcome` (Task 1). `CodingEventAiContext` (Felder `Confidence`, `QualityGateLevel`, `Decision`), `TrafficLight`.
- Produces: unveraenderte Signaturen `DefectStatusPolicy.GetStatus(CodingEvent) -> DefectStatus` und `CanAct(CodingEvent?) -> bool`.

- [ ] **Step 1: Charakterisierungs-Tests an die neue Regel anpassen (fehlschlagend)**

In `tests/AuswertungPro.Next.Pipeline.Tests/DefectStatusPolicyTests.cs`:

(a) Helper `EvMitKonfidenz` um eine optionale Ampel erweitern:

```csharp
    private static CodingEvent EvMitKonfidenz(double confidence, string? gate = null) =>
        new()
        {
            Entry = new ProtocolEntry { Code = "BAB" },
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BAB",
                Confidence = confidence,
                QualityGateLevel = gate,
                Decision = CodingUserDecision.Ignored
            }
        };
```

(b) Green-Zone-Test ersetzen: AutoAccepted verlangt jetzt Sicherheit >= 0.92 UND gruene Ampel.

```csharp
    [Theory]
    [InlineData(1.00)]
    [InlineData(0.92)]
    public void GetStatus_HoheSicherheitUndGrueneAmpel_GibtAutoAccepted(double confidence)
    {
        var ev = EvMitKonfidenz(confidence, gate: "Green");
        Assert.Equal(DefectStatus.AutoAccepted, DefectStatusPolicy.GetStatus(ev));
    }

    [Theory]
    [InlineData(0.99, null)]     // hohe Sicherheit, aber Ampel fehlt
    [InlineData(0.99, "Yellow")] // hohe Sicherheit, aber gelbe Ampel
    [InlineData(0.85, "Green")]  // gruene Ampel, aber Sicherheit unter 0.92
    public void GetStatus_UnvollstaendigeBelege_GibtPending(double confidence, string? gate)
    {
        var ev = EvMitKonfidenz(confidence, gate);
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(ev));
    }
```

(c) Die alten `GetStatus_KonfidenzGreenZone_GibtAutoAccepted`, `GetStatus_KonfidenzYellowZone_GibtPending` und `GetStatus_Schwellwertgrenzen_SindKorrekt` (die auf 0.85/0.60 ohne Ampel bauen) loeschen. Die Red-Zone-Tests (`0.59`, `0.00`) bleiben (Sicherheit < 0.60 → ReviewRequired). Die manuellen-Entscheidungs-Tests und die CanAct-Tests bleiben unveraendert.

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~DefectStatusPolicyTests"`
Expected: FAIL — die neuen AutoAccepted-Faelle scheitern (alte Regel kennt keine Ampel).

- [ ] **Step 2: `DefectStatusPolicy.GetStatus` auf die zentrale Regel umstellen**

In `src/AuswertungPro.Next.Application/Ai/DefectStatusPolicy.cs` den Confidence-`switch`-Zweig durch einen Aufruf der zentralen Regel ersetzen. Der `using`-Block bekommt `using System;` und `using AuswertungPro.Next.Application.Ai.QualityGate;`. Neue `GetStatus`-Methode und zwei Helfer:

```csharp
    public static DefectStatus GetStatus(CodingEvent ev)
    {
        if (ev.AiContext == null) return DefectStatus.Pending;

        return ev.AiContext.Decision switch
        {
            CodingUserDecision.Accepted        => DefectStatus.Accepted,
            CodingUserDecision.AcceptedWithEdit => DefectStatus.AcceptedWithEdit,
            CodingUserDecision.Rejected        => DefectStatus.Rejected,
            _ => MapCentralDecision(ev.AiContext)
        };
    }

    // Noch nicht vom Nutzer entschieden: zentrale Freigabe-Regel anwenden.
    // Live-Kontext liefert nur Sicherheit + Ampel; Datenbank-Abgleich/Unsicherheit sind hier nicht vorhanden.
    private static DefectStatus MapCentralDecision(CodingEventAiContext ctx)
    {
        var signals = new AiDecisionSignals(
            Confidence: ctx.Confidence,
            QualityGate: ParseLight(ctx.QualityGateLevel));

        return StandardAiDecisionPolicy.Default.Decide(signals).Outcome switch
        {
            AiDecisionOutcome.AutoAccept => DefectStatus.AutoAccepted,
            AiDecisionOutcome.Review     => DefectStatus.Pending,
            _                            => DefectStatus.ReviewRequired
        };
    }

    private static TrafficLight? ParseLight(string? level)
        => Enum.TryParse<TrafficLight>(level, ignoreCase: true, out var tl) ? tl : null;
```

Die Konstanten `GreenThreshold`/`YellowThreshold` und der alte `Confidence`-`switch` entfallen. `CanAct` bleibt unveraendert (nutzt weiter `GetStatus`).

- [ ] **Step 3: Tests ausfuehren, Erfolg bestaetigen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~DefectStatusPolicyTests"`
Expected: PASS — alle Faelle gruen.

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/DefectStatusPolicy.cs tests/AuswertungPro.Next.Pipeline.Tests/DefectStatusPolicyTests.cs
git commit -m "refactor(ki): DefectStatusPolicy nutzt zentrale Freigabe-Regel (Ampel zaehlt jetzt mit)"
```

---

### Task 4: QualityGate-Ampel beim Anlegen der KI-Events speichern

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Ai/CodingLiveFindingEventFactory.cs:36-42`
- Modify: `src/AuswertungPro.Next.UI/Ai/CodingMultiModelFindingEventWorkflow.cs:120-121`
- Test: `tests/AuswertungPro.Next.UI.Tests/CodingLiveFindingEventFactoryGateLevelTests.cs`

**Interfaces:**
- Consumes: `QualityGateResult.TrafficLight`, `CodingEventAiContext.QualityGateLevel`. Ergebnis der Freigabe-Regel aus Task 3 (nutzt `QualityGateLevel`).
- Produces: `CodingLiveFindingEventDraft.AiContext.QualityGateLevel` ist nach `Create` gesetzt.

- [ ] **Step 1: Fehlschlagenden Test schreiben**

Datei `tests/AuswertungPro.Next.UI.Tests/CodingLiveFindingEventFactoryGateLevelTests.cs`:

```csharp
using System;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Audit Fix 3: Die QualityGate-Ampel muss schon beim Anlegen des KI-Events im AiContext
/// stehen — sonst kennt die Live-Anzeige den zweiten Beleg nicht und nichts wird je gruen.
/// </summary>
public sealed class CodingLiveFindingEventFactoryGateLevelTests
{
    [Fact]
    public void Create_SchreibtAmpelInAiContext()
    {
        var finding = new LiveFrameFinding { Label = "Riss" };
        var gate = new QualityGateResult(0.95, TrafficLight.Green,
            new System.Collections.Generic.Dictionary<string, double>(), "test");

        var draft = CodingLiveFindingEventFactory.Create(
            code: "BAB",
            officialLabel: "Riss laengs",
            finding: finding,
            meter: 12.0,
            videoTime: TimeSpan.FromSeconds(5),
            gateResult: gate);

        Assert.Equal("Green", draft.AiContext.QualityGateLevel);
    }
}
```

Hinweis: Falls die realen Pflichtfelder von `LiveFrameFinding` abweichen, beim Schreiben des Tests an den vorhandenen Konstruktor/Initializer anpassen (Typ liegt in `AuswertungPro.Next.Infrastructure.Ai`).

- [ ] **Step 2: Test ausfuehren, Fehlschlag bestaetigen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~CodingLiveFindingEventFactoryGateLevel"`
Expected: FAIL — `QualityGateLevel` ist null (wird noch nicht gesetzt).

- [ ] **Step 3a: `CodingLiveFindingEventFactory` erweitern**

In `src/AuswertungPro.Next.UI/Ai/CodingLiveFindingEventFactory.cs` den `aiContext`-Initializer (Zeile ~36) um eine Zeile ergaenzen:

```csharp
        var aiContext = new CodingEventAiContext
        {
            SuggestedCode = code,
            Confidence = gateResult.CompositeConfidence,
            Reason = finding.Label,
            // Audit Fix 3: Ampel schon beim Anlegen sichern, damit die Live-Anzeige
            // die zentrale Freigabe-Regel (zweiter Beleg) anwenden kann.
            QualityGateLevel = gateResult.TrafficLight.ToString(),
            Decision = CodingUserDecision.Ignored
        };
```

- [ ] **Step 3b: Multi-Model-Workflow anreichern**

In `src/AuswertungPro.Next.UI/Ai/CodingMultiModelFindingEventWorkflow.cs` direkt nach dem `CodingMultiModelEventFactory.Create(...)`-Aufruf (nach Zeile ~120, vor `actions.AttachAnalyzedFramePhoto`):

```csharp
            // Audit Fix 3: Ampel aus dem bereits ausgewerteten gateResult in den AiContext uebernehmen.
            draft.AiContext.QualityGateLevel = gateResult.TrafficLight.ToString();
```

- [ ] **Step 4: Test ausfuehren, Erfolg bestaetigen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~CodingLiveFindingEventFactoryGateLevel"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/CodingLiveFindingEventFactory.cs src/AuswertungPro.Next.UI/Ai/CodingMultiModelFindingEventWorkflow.cs tests/AuswertungPro.Next.UI.Tests/CodingLiveFindingEventFactoryGateLevelTests.cs
git commit -m "feat(ki): QualityGate-Ampel beim Anlegen der KI-Events speichern (Audit Fix 3)"
```

---

### Task 5: Voller Regressionslauf

**Files:** keine.

- [ ] **Step 1: Betroffene Test-Projekte komplett laufen lassen**

Run:
```bash
dotnet build AuswertungPro.sln -c Debug -v minimal
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --no-build
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --no-build
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --no-build
```
Expected: 0 Fehler, keine Regression.

- [ ] **Step 2: Bei gruenem Lauf — Abschluss melden** (kein Commit noetig, alles bereits committet).

---

## Self-Review

**1. Spec coverage:**
- §3 Regel (streng, kontextabhaengig, Reject/Review/AutoAccept) → Task 1. ✓
- §4 zentrale Typen + IAiDecisionPolicy → Task 1; DefectStatusPolicy-Umstellung → Task 3; AutoApprovalService-Adapter → Task 2. ✓
- §5 Ampel beim Anlegen → Task 4. ✓
- §6 Testplan (Policy-Faelle, AutoApproval bleibt gruen, DefectStatus neu, Factory-Anreicherung, Volllauf) → Tasks 1–5. ✓
- §2 Selbsttraining unberuehrt, kein echtes Durchwinken → keine Task fasst diese an. ✓

**2. Placeholder-Scan:** Keine TBD/TODO. Der einzige „falls abweichend"-Hinweis (Task 4, `LiveFrameFinding`-Felder) ist eine konkrete Anpassungsanweisung mit Typ-Ort, kein offener Platzhalter.

**3. Typkonsistenz:** `AiDecisionSignals`, `AiDecision`, `AiDecisionOutcome`, `IAiDecisionPolicy`, `StandardAiDecisionPolicy.Default`, `Decide(...)` durchgaengig gleich in Tasks 1–3. `QualityGateLevel` als String ("Green"/"Yellow"/"Red") aus `TrafficLight.ToString()` (Task 4) passt zu `Enum.TryParse<TrafficLight>` (Task 3). ✓
