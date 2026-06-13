# Gold-Fund Plan B – Etappe 2: Quantifizierung mit Herkunft & Status

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Der gespeicherte Gold-Fund traegt zu den Quantifizierungs-Werten (Uhrlage, mm, %) auch deren **Herkunft** (CalibrationSource: geschaetzt/auto/manuell) und **Status** (Suggested = KI-Vorschlag). Damit ist die Gold-Fund-Datenschicht ehrlich vollstaendig.

**Kernbefund (verifiziert):** Die Quant-WERTE werden bereits gespeichert. `ApplyQuantificationToEntry(entry, code, quant)` (PlayerWindow.Coding.cs:3786, aufgerufen bei der Event-Erzeugung Z.3477) schreibt in `entry.CodeMeta.Parameters`: `vsa.uhr.von` (ClockPosition), `vsa.hoehe.mm` (HeightMm), `vsa.breite.mm` (WidthMm), `vsa.querschnitt.prozent` (CrossSectionReductionPercent). `FromCodingEvent` klont `ev.Entry.CodeMeta` in `TrainingSample.CodeMeta`. `CodeMeta` wird ueberall automatisch durchgereicht (Mapper-Clone, ApplyUpdatableFields, CloneSample) — also KEIN neuer Nebenpfad noetig.

**Was fehlt:**
1. `CalibrationSource` (aus Etappe 1, jetzt auf jeder `QuantifiedMask`) wird nicht ins CodeMeta geschrieben → die mm-Werte tragen ihre Verlaesslichkeit nicht mit.
2. `QuantificationSource`-Status (Suggested/Confirmed/Corrected) wird gar nicht gespeichert → man weiss nicht, ob eine Messung KI-Vorschlag oder vom Menschen geprueft ist.
3. `ExtentPercent` (Ausdehnung % des Rohrumfangs) wird nicht geschrieben (nur CrossSectionReductionPercent).

**Design-Entscheidung:** Alles als **CodeMeta-Parameter** (`vsa.*`) speichern — konsistent mit den schon vorhandenen Werten, automatisch durch alle Pfade durchgereicht, KEIN neues TrainingSample-Feld, KEIN Merge/Clone-Nebenpfad. (Begruendung: `CalibrationSource` lebt auf der `QuantifiedMask`, die NICHT im CodingEvent steckt — ein CodeMeta-Parameter wird direkt dort geschrieben, wo der Quant verfuegbar ist (ApplyQuantificationToEntry), ohne Plumbing durch Event/Mapper. Falls spaeter typisierte Abfragen noetig sind, ist eine Migration aus CodeMeta einfach.)

**QuantificationSource jetzt:** Da die KI in KEINEM Pfad selbst akzeptiert (Decision=Ignored, bis der Mensch bestaetigt), ist KI-Quant immer **Suggested**. `Confirmed`/`Corrected` setzt spaeter der Korrigieren-Modus (Item 3) bzw. eine manuelle Messung — das ist NICHT Teil dieser Etappe.

**Tech Stack:** C#/.NET 10, xUnit.

---

## File Structure

- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/QuantificationCodeMetaWriter.cs` — testbare Mapping-Logik (Quant → CodeMeta-Parameter), inkl. der neuen Felder.
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs` — `ApplyQuantificationToEntry` delegiert an den Writer (minimaler UI-Eingriff).
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/QuantificationCodeMetaWriterTests.cs`.

Verifizierte Fakten:
- `ApplyQuantificationToEntry(ProtocolEntry entry, string code, MaskQuantificationService.QuantifiedMask quant)` (PlayerWindow.Coding.cs:3786) — schreibt heute 4 Parameter (siehe oben), nutzt `entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code }` und `entry.CodeMeta.Parameters["..."] = wert`.
- `QuantifiedMask` (Infrastructure.Ai.Pipeline) hat nach Etappe 1: `CalibrationSource CalibrationSource`. Werte-Properties: HeightMm?, WidthMm?, ExtentPercent?, CrossSectionReductionPercent?, IntrusionPercent?, ClockPosition?.
- `CalibrationSource { None, Auto, Manual }` in `AuswertungPro.Next.Domain.Models`.
- `ProtocolEntry` + `ProtocolEntryCodeMeta` in `AuswertungPro.Next.Domain.Protocol`. `ProtocolEntryCodeMeta.Parameters` = `Dictionary<string,string>` (OrdinalIgnoreCase).
- Infrastructure referenziert Domain (ProtocolEntry/CodeMeta nutzbar) und hat `QuantifiedMask` im selben Assembly.
- `tests/AuswertungPro.Next.Infrastructure.Tests` existiert.

**WICHTIG (Kollision):** `PlayerWindow.Coding.cs` ist UI = Codex-Revier; Codex arbeitet parallel an Item 2 (Bildvorschau). Der UI-Eingriff hier ist MINIMAL (eine Methode delegiert) und liegt bei ~Z.3786, weit weg vom Event-Panel/der Liste. Vor dem Start pruefen, dass `PlayerWindow.Coding.cs` im Arbeitsbaum sauber ist (kein uncommitteter Codex-Stand). Falls dirty → STOP und Codex erst committen lassen.

---

### Task 1: Testbarer Quant→CodeMeta-Writer mit Herkunft & Status

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/QuantificationCodeMetaWriter.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/QuantificationCodeMetaWriterTests.cs`

- [ ] **Step 1: Failing Tests**

Neue Datei `tests/AuswertungPro.Next.Infrastructure.Tests/QuantificationCodeMetaWriterTests.cs`:
```csharp
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class QuantificationCodeMetaWriterTests
{
    private static MaskQuantificationService.QuantifiedMask Quant(
        CalibrationSource source,
        int? height = 45, int? width = 12, int? extent = 30, int? crossSection = 20, string? clock = "3:00")
        => new("BCA", 0.9, height, width, extent, crossSection, null, clock, source);

    [Fact]
    public void Apply_SchreibtWerteHerkunftUndSuggested()
    {
        var entry = new ProtocolEntry { Code = "BCA" };
        QuantificationCodeMetaWriter.Apply(entry, "BCA", Quant(CalibrationSource.Manual));

        var p = entry.CodeMeta!.Parameters;
        Assert.Equal("3:00", p["vsa.uhr.von"]);
        Assert.Equal("45", p["vsa.hoehe.mm"]);
        Assert.Equal("12", p["vsa.breite.mm"]);
        Assert.Equal("30", p["vsa.ausdehnung.prozent"]);
        Assert.Equal("20", p["vsa.querschnitt.prozent"]);
        Assert.Equal("manuell", p["vsa.kalibrierung.quelle"]);
        Assert.Equal("Vorschlag", p["vsa.quant.quelle"]);
    }

    [Fact]
    public void Apply_HerkunftNone_WirdAlsGeschaetztMarkiert()
    {
        var entry = new ProtocolEntry { Code = "BCA" };
        QuantificationCodeMetaWriter.Apply(entry, "BCA", Quant(CalibrationSource.None));
        Assert.Equal("geschaetzt", entry.CodeMeta!.Parameters["vsa.kalibrierung.quelle"]);
    }

    [Fact]
    public void Apply_OhneWerte_SchreibtNurQuelleWennUeberhauptQuant()
    {
        // Kein einziger Messwert -> keine Wert-Parameter; aber Herkunft/Status sollen
        // nur gesetzt werden, wenn es ueberhaupt eine Quantifizierung gibt.
        var entry = new ProtocolEntry { Code = "BCD" };
        QuantificationCodeMetaWriter.Apply(entry, "BCD",
            new MaskQuantificationService.QuantifiedMask("BCD", 0.9, null, null, null, null, null, null, CalibrationSource.None));

        Assert.False(entry.CodeMeta?.Parameters.ContainsKey("vsa.hoehe.mm") ?? false);
        Assert.False(entry.CodeMeta?.Parameters.ContainsKey("vsa.quant.quelle") ?? false);
    }
}
```
(Pruefe die exakte `QuantifiedMask`-Konstruktor-Reihenfolge nach Etappe 1: `(Label, Confidence, HeightMm, WidthMm, ExtentPercent, CrossSectionReductionPercent, IntrusionPercent, ClockPosition, CalibrationSource)`. Passe den Helfer an, falls abweichend.)

- [ ] **Step 2: RED**

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "Apply_SchreibtWerteHerkunftUndSuggested|Apply_HerkunftNone_WirdAlsGeschaetztMarkiert|Apply_OhneWerte_SchreibtNurQuelleWennUeberhauptQuant" -v minimal
```
Erwartung: Compile-Fehler (QuantificationCodeMetaWriter existiert nicht).

- [ ] **Step 3: Writer implementieren**

Neue Datei `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/QuantificationCodeMetaWriter.cs`:
```csharp
using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Schreibt SAM-Quantifizierungsdaten als CodeMeta-Parameter (vsa.*) in einen ProtocolEntry.
/// Neben den Messwerten auch die HERKUNFT (CalibrationSource: geschaetzt/auto/manuell) und den
/// STATUS (Suggested = KI-Vorschlag). So ist eine geschaetzte mm-Angabe von einer gemessenen
/// unterscheidbar (Gold-Fund-Wahrheit). Aus PlayerWindow.Coding.ApplyQuantificationToEntry extrahiert
/// und um Herkunft/Status/Ausdehnung erweitert.
/// </summary>
public static class QuantificationCodeMetaWriter
{
    public static void Apply(ProtocolEntry entry, string code, MaskQuantificationService.QuantifiedMask quant)
    {
        var hasAnyValue =
            !string.IsNullOrEmpty(quant.ClockPosition)
            || quant.HeightMm.HasValue
            || quant.WidthMm.HasValue
            || quant.ExtentPercent is > 0
            || quant.CrossSectionReductionPercent is > 0;

        if (!hasAnyValue)
            return; // kein Quant -> keine Parameter (auch keine Herkunft/Status)

        entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
        var p = entry.CodeMeta.Parameters;

        if (!string.IsNullOrEmpty(quant.ClockPosition))
            p["vsa.uhr.von"] = quant.ClockPosition;
        if (quant.HeightMm.HasValue)
            p["vsa.hoehe.mm"] = quant.HeightMm.Value.ToString(CultureInfo.InvariantCulture);
        if (quant.WidthMm.HasValue)
            p["vsa.breite.mm"] = quant.WidthMm.Value.ToString(CultureInfo.InvariantCulture);
        if (quant.ExtentPercent is > 0)
            p["vsa.ausdehnung.prozent"] = quant.ExtentPercent.Value.ToString(CultureInfo.InvariantCulture);
        if (quant.CrossSectionReductionPercent is > 0)
            p["vsa.querschnitt.prozent"] = quant.CrossSectionReductionPercent.Value.ToString(CultureInfo.InvariantCulture);

        // Herkunft der Messung (3-Stufen aus Etappe 1) und Status (KI = Vorschlag).
        p["vsa.kalibrierung.quelle"] = HerkunftLabel(quant.CalibrationSource);
        p["vsa.quant.quelle"] = QuantStatusVorschlag;
    }

    // Klartext-Status der Quantifizierung (ASCII-Deutsch wie Codebase-Konvention).
    // Heute setzt nur der KI-Pfad -> immer Vorschlag. Bestaetigt/Korrigiert kommt mit
    // dem Korrigieren-Modus (Item 3); als Konstanten bereitgestellt fuer spaeter.
    public const string QuantStatusVorschlag = "Vorschlag";
    public const string QuantStatusBestaetigt = "bestaetigt";
    public const string QuantStatusKorrigiert = "korrigiert";

    /// <summary>Kalibrierungs-Herkunft als Klartext: geschaetzt / automatisch / manuell.</summary>
    public static string HerkunftLabel(CalibrationSource source) => source switch
    {
        CalibrationSource.Manual => "manuell",
        CalibrationSource.Auto => "automatisch",
        _ => "geschaetzt"
    };
}
```

- [ ] **Step 4: GREEN**

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "Apply_SchreibtWerteHerkunftUndSuggested|Apply_HerkunftNone_WirdAlsGeschaetztMarkiert|Apply_OhneWerte_SchreibtNurQuelleWennUeberhauptQuant" -v minimal
```
Erwartung: 3 PASS.

- [ ] **Step 5: Build + Commit**

```powershell
dotnet build AuswertungPro.sln -v minimal
git add src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/QuantificationCodeMetaWriter.cs tests/AuswertungPro.Next.Infrastructure.Tests/QuantificationCodeMetaWriterTests.cs
git commit -m "Gold-Fund/Quant: CodeMeta-Writer mit Kalibrierungs-Herkunft + Suggested-Status"
```

---

### Task 2: UI delegiert an den Writer (minimaler Eingriff)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`

**VORAB (Kollision):** `git status --short` pruefen — wenn `PlayerWindow.Coding.cs` als modifiziert erscheint (uncommitteter Codex-Stand), STOP und melden. Nur auf sauberer Datei weiterarbeiten.

- [ ] **Step 1: ApplyQuantificationToEntry an den Writer delegieren**

Den Koerper von `ApplyQuantificationToEntry` (PlayerWindow.Coding.cs:3786 ff., die ~25 Zeilen mit den 4 `entry.CodeMeta.Parameters[...]`-Zuweisungen) ersetzen durch eine Delegation:
```csharp
    private static void ApplyQuantificationToEntry(
        ProtocolEntry entry, string code, MaskQuantificationService.QuantifiedMask quant)
        => AuswertungPro.Next.Infrastructure.Ai.Pipeline.QuantificationCodeMetaWriter.Apply(entry, code, quant);
```
(Pruefe die `using`s: `MaskQuantificationService`/`QuantifiedMask` sind in der Datei schon erreichbar; `QuantificationCodeMetaWriter` voll qualifizieren oder per `using` ergaenzen. Der einzige Aufrufer (Z.3477) bleibt unveraendert. Wenn die Methode sonst nirgends gebraucht wird, kann sie auch ganz entfallen und der Aufruf direkt auf den Writer zeigen — aber die delegierende Methode beizubehalten haelt den Diff minimal.)

- [ ] **Step 2: Build + ganze UI-/Infra-Suite**

```powershell
dotnet build AuswertungPro.sln -v minimal
dotnet test tests/AuswertungPro.Next.UI.Tests -v minimal
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests -v minimal
```
Erwartung: 0 Fehler, beide Suiten gruen (Verhalten unveraendert + 3 neue Tests + neue Felder).

- [ ] **Step 3: Commit (nur die eine Datei)**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs
git commit -m "Gold-Fund/Quant: ApplyQuantificationToEntry an QuantificationCodeMetaWriter delegieren"
```

---

## Self-Review

**Spec-Abdeckung (User-Liste):**
- Uhrlage → `vsa.uhr.von` (war schon da) ✓
- mm-Werte → `vsa.hoehe.mm`/`vsa.breite.mm` (war schon da) ✓
- Querschnitt % → `vsa.querschnitt.prozent` (war schon da) ✓ + NEU `vsa.ausdehnung.prozent`
- Quelle (geschaetzt/auto/manuell) → NEU `vsa.kalibrierung.quelle` (aus Etappe 1) ✓
- Status (Vorschlag) → NEU `vsa.quant.quelle = Suggested` ✓ (Confirmed/Corrected spaeter via Korrigieren-Modus)

**Warum kein neues TrainingSample-Feld / kein Nebenpfad:** Alles in CodeMeta; CodeMeta wird in FromCodingEvent geklont, in ApplyUpdatableFields durchgereicht und in CloneSample kopiert — automatisch ueberall mit. Damit keine Re-Merge-/Export-Stolperfalle wie in Plan A.

**Bewusst NICHT:** Typisiertes QuantificationSource-Feld (CodeMeta-Param genuegt; Migration spaeter trivial). Confirmed/Corrected-Setzen (gehoert zum Korrigieren-Modus, Item 3). KB-Spalten fuer Quant (KB nutzt nur Kern-Felder; die Gold-Fund-Wahrheit liegt im JSON-Store via CodeMeta).

**Kollision:** UI-Eingriff minimal (Delegation), aber `PlayerWindow.Coding.cs` ist Codex-Revier — vor Task 2 Sauberkeit pruefen.
