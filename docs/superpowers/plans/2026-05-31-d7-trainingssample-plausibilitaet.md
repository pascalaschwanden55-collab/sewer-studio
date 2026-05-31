# D7: Fachliche Plausibilitätsprüfung vor dem KI-Lernen — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (oder subagent-driven-development). Steps mit Checkbox (`- [ ]`).

**Goal:** Die KI lernt nur aus **fachlich plausiblen** Befunden — nicht nur, weil ein Code technisch existiert. Das schützt die Trainingsdaten/Wissensdatenbank vor offensichtlichem Müll.

**Architecture:** Heute prüft `KnowledgeBaseManager.IsIndexWorthy` nur technisch (Text ≥ 10 Zeichen + Code existiert im Katalog). D7 ergänzt eine **reine, testbare** fachliche Prüfung (`TrainingSamplePlausibility` in Application) und ruft sie in `IsIndexWorthy` auf. Objektive „Müll-Filter", die **keine** gültigen Samples verwerfen.

**Tech Stack:** .NET 10, xUnit. Validator ist pure Logik (kein I/O) in der Application-Schicht, neben `TrainingSample`.

---

## Verifizierte Faktenlage (Stand HEAD 9d5ca5af)

- `KnowledgeBaseManager.IsIndexWorthy` (`:339-348`): nur 3 technische Checks (Beschreibung ≥10, Code nicht leer, `VsaCodeResolver.LookupLabel(code) != null`). Genutzt in `IndexSampleAsync` (`:34`) und im Batch (`:77`).
- `TrainingSample` (`Application/Ai/Training/TrainingSampleModels.cs:49`): trägt u.a. `Code`, `Beschreibung`, `MeterStart`, `MeterEnd`, `IsStreckenschaden`, und `CodeMeta` (`ProtocolEntryCodeMeta?`).
- `ProtocolEntryCodeMeta` (`Domain/Protocol/ProtocolModels.cs:30`): `Severity` ist **`string?`**, Quantifizierung in `Parameters` (Dict<string,string>, OrdinalIgnoreCase), z. B. Key `"vsa.querschnitt.prozent"`.

## Prüf-Regeln (objektiv, verwerfen nur klaren Müll)

1. `MeterStart < 0` → implausibel (negativer Meterstand).
2. `MeterEnd < MeterStart` → implausibel (Meter-Ende vor -Start; invertierter Bereich).
3. Wenn `CodeMeta.Severity` gesetzt **und** als Zahl lesbar **und** nicht in 1..5 → implausibel.
4. Wenn `CodeMeta.Parameters["vsa.querschnitt.prozent"]` gesetzt **und** lesbar **und** nicht in 0..100 → implausibel.

> Bewusst NICHT geprüft (vermeidet Fehl-Verwerfung gültiger Samples): Streckenschaden mit Null-Span (kann eine legitime Einzelframe-Sichtung sein); Uhrlage-Format (variabel); Code-Art vs. IsStreckenschaden (nach D4 produzentenseitig bereits konsistent).

---

## File Structure

| Datei | Verantwortung | Änderung |
|---|---|---|
| `tests/AuswertungPro.Next.Infrastructure.Tests/Training/TrainingSamplePlausibilityTests.cs` | Tests für die 4 Regeln + plausibler Fall | **Create** |
| `src/AuswertungPro.Next.Application/Ai/Training/TrainingSamplePlausibility.cs` | Reine fachliche Plausibilität für ein `TrainingSample` | **Create** |
| `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs:339-348` | `IsIndexWorthy` ruft die Prüfung auf | **Modify** |

---

## Task 1: Validator + Tests (TDD)

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/Training/TrainingSamplePlausibility.cs`
- Create: `tests/AuswertungPro.Next.Infrastructure.Tests/Training/TrainingSamplePlausibilityTests.cs`

- [ ] **Step 1: Failing tests schreiben**

```csharp
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Training;

public class TrainingSamplePlausibilityTests
{
    private static TrainingSample Sample(double start = 5, double end = 5,
        string? severity = null, string? querschnitt = null)
    {
        var s = new TrainingSample
        {
            Code = "BAB", Beschreibung = "Riss laengs am Scheitel",
            MeterStart = start, MeterEnd = end
        };
        if (severity != null || querschnitt != null)
        {
            s.CodeMeta = new ProtocolEntryCodeMeta { Code = "BAB", Severity = severity };
            if (querschnitt != null) s.CodeMeta.Parameters["vsa.querschnitt.prozent"] = querschnitt;
        }
        return s;
    }

    [Fact]
    public void PlausiblesSample_IstPlausibel()
        => Assert.True(TrainingSamplePlausibility.IsFachlichPlausibel(
            Sample(start: 5, end: 8, severity: "3", querschnitt: "40"), out _));

    [Fact]
    public void NegativerMeterstand_IstImplausibel()
        => Assert.False(TrainingSamplePlausibility.IsFachlichPlausibel(Sample(start: -1, end: 2), out _));

    [Fact]
    public void InvertierterBereich_IstImplausibel()
        => Assert.False(TrainingSamplePlausibility.IsFachlichPlausibel(Sample(start: 8, end: 5), out _));

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("-2")]
    public void SeverityAusserhalb1Bis5_IstImplausibel(string sev)
        => Assert.False(TrainingSamplePlausibility.IsFachlichPlausibel(Sample(severity: sev), out _));

    [Fact]
    public void SeverityNichtGesetzt_WirdNichtGeprueft()
        => Assert.True(TrainingSamplePlausibility.IsFachlichPlausibel(Sample(severity: null), out _));

    [Theory]
    [InlineData("150")]
    [InlineData("-5")]
    public void QuerschnittProzentAusserhalb0Bis100_IstImplausibel(string pct)
        => Assert.False(TrainingSamplePlausibility.IsFachlichPlausibel(Sample(querschnitt: pct), out _));
}
```

- [ ] **Step 2: Test laufen, Fehlschlag bestätigen**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter FullyQualifiedName~TrainingSamplePlausibility -v minimal`
Expected: FAIL — Typ `TrainingSamplePlausibility` existiert nicht.

- [ ] **Step 3: Validator implementieren**

```csharp
using System.Globalization;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Fachliche Plausibilitaet eines TrainingSample (D7). Verwirft nur objektiv falsche
/// Samples (negativer/invertierter Meterbereich, Severity ausserhalb 1-5, Querschnitt %
/// ausserhalb 0-100), damit die KI nicht aus Muell lernt. Reine Logik, kein I/O.
/// </summary>
public static class TrainingSamplePlausibility
{
    public static bool IsFachlichPlausibel(TrainingSample sample, out string reason)
    {
        if (sample.MeterStart < 0)
        {
            reason = $"Negativer Meterstand ({sample.MeterStart}).";
            return false;
        }
        if (sample.MeterEnd < sample.MeterStart)
        {
            reason = $"Meter-Ende ({sample.MeterEnd}) vor Meter-Start ({sample.MeterStart}).";
            return false;
        }

        var meta = sample.CodeMeta;
        if (meta is not null)
        {
            if (TryParseInt(meta.Severity, out var sev) && (sev < 1 || sev > 5))
            {
                reason = $"Severity {sev} ausserhalb 1-5.";
                return false;
            }
            if (meta.Parameters.TryGetValue("vsa.querschnitt.prozent", out var pctRaw)
                && TryParseDouble(pctRaw, out var pct) && (pct < 0 || pct > 100))
            {
                reason = $"Querschnitt {pct}% ausserhalb 0-100.";
                return false;
            }
        }

        reason = "";
        return true;
    }

    private static bool TryParseInt(string? value, out int result)
    {
        result = 0;
        return !string.IsNullOrWhiteSpace(value)
            && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        result = 0;
        return !string.IsNullOrWhiteSpace(value)
            && double.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
```

- [ ] **Step 4: Tests laufen, grün bestätigen**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter FullyQualifiedName~TrainingSamplePlausibility -v minimal`
Expected: PASS (alle).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/Training/TrainingSamplePlausibility.cs \
        tests/AuswertungPro.Next.Infrastructure.Tests/Training/TrainingSamplePlausibilityTests.cs
git commit -m "feat(ai): fachliche Plausibilitaet fuer TrainingSamples (D7, Validator + Tests)"
```

---

## Task 2: In das KB-Tor einhängen

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs:339-348`

- [ ] **Step 1: `IsIndexWorthy` um den fachlichen Check erweitern**

Vorher:
```csharp
    public static bool IsIndexWorthy(TrainingSample sample)
    {
        if (string.IsNullOrWhiteSpace(sample.Beschreibung) || sample.Beschreibung.Length < 10)
            return false;
        if (string.IsNullOrWhiteSpace(sample.Code))
            return false;
        if (VsaCodeResolver.LookupLabel(sample.Code) is null)
            return false;
        return true;
    }
```
Nachher:
```csharp
    public static bool IsIndexWorthy(TrainingSample sample)
    {
        if (string.IsNullOrWhiteSpace(sample.Beschreibung) || sample.Beschreibung.Length < 10)
            return false;
        if (string.IsNullOrWhiteSpace(sample.Code))
            return false;
        if (VsaCodeResolver.LookupLabel(sample.Code) is null)
            return false;
        // D7: nur fachlich plausible Befunde lernen (kein Muell in die KB).
        if (!TrainingSamplePlausibility.IsFachlichPlausibel(sample, out var reason))
        {
            Debug.WriteLine($"[KnowledgeBaseManager] Sample {sample.SampleId} fachlich implausibel: {reason}");
            return false;
        }
        return true;
    }
```

> `TrainingSamplePlausibility` liegt im Namespace `AuswertungPro.Next.Application.Ai.Training` — bei Bedarf `using` ergänzen (KnowledgeBaseManager nutzt `TrainingSample` aus demselben Namespace, daher i.d.R. bereits vorhanden).

- [ ] **Step 2: Build**

Run: `dotnet build AuswertungPro.sln -c Debug -v minimal`
Expected: 0 Fehler, 0 Warnungen.

- [ ] **Step 3: Volle Tests**

Run: `dotnet test AuswertungPro.sln --no-build -v minimal`
Expected: alle grün (768 + neue D7-Tests), 1 übersprungen.

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs
git commit -m "feat(ai): KB-Index laesst nur fachlich plausible Samples zu (D7)"
```

---

## Self-Review

1. **Spec-Abdeckung:** „nur fachlich plausible Befunde lernen" → Validator (Task 1) + Einhängen ins KB-Tor (Task 2). ✓
2. **Keine Fehl-Verwerfung:** Nur objektive Müll-Filter; Severity/Querschnitt nur geprüft, wenn gesetzt **und** lesbar; Streckenschaden-Null-Span bewusst erlaubt. ✓
3. **Platzhalter-Scan:** Test- und Implementierungscode vollständig; exakte Datei:Zeilen; echte Feldnamen (`Severity` als string, Parameter-Key `vsa.querschnitt.prozent`). ✓
4. **Typkonsistenz:** `TrainingSamplePlausibility.IsFachlichPlausibel(TrainingSample, out string)` in Task 1 definiert, in Task 2 genau so aufgerufen. ✓
5. **Reuse/Altitude:** Validator in Application neben `TrainingSample` (richtige Heimat); kein Bezug zu `ImportPlausibilityValidator` (anderer Datentyp/HaltungRecord) — keine künstliche Kopplung. ✓

## Was D7 ausdrücklich NICHT tut
- Keine semantische Korrektheitsprüfung „ist der Code fachlich der richtige für dieses Bild" (das ist Aufgabe der menschlichen Bestätigung, nicht eines Regelfilters).
- Keine Änderung am Embedding-/Retrieval-Verhalten.
- Keine Streckenschaden/Code-Art-Kreuzprüfung (Fehl-Verwerfungsrisiko).
