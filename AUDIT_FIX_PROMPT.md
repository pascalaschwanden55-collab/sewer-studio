# KI 4.0 Audit — Fix-Prompt fuer Claude in VS Code

## Kontext
Code-Audit vom 2026-04-11 hat 25 Findings identifiziert. Dieser Prompt beschreibt die Top-Fixes sortiert nach Prioritaet. Alle Findings sind mit Codebeleg verifiziert — keine Mutmassungen.

## Regeln
- Jeder Fix einzeln committen mit Verweis auf Audit-ID (z.B. "Fix A01: ...")
- Keine zusaetzlichen Refactorings, nur die beschriebenen Fixes
- Tests nur fuer Recommendation- und QualityGate-Logik (siehe CLAUDE.md)
- Kommentare auf Deutsch
- Nach jedem Fix: `dotnet build AuswertungPro.sln` pruefen

---

## Fix A01 — KRITISCH: Hardcodierte Firebird-Credentials entfernen
**Datei:** `src/AuswertungPro.Next.Infrastructure/Import/Ibak/IbakExportImportService.cs`  
**Zeile:** 462-463  
**Ist-Zustand:**
```csharp
UserID = Environment.GetEnvironmentVariable("IBAK_FDB_USER") ?? "SYSDBA",
Password = Environment.GetEnvironmentVariable("IBAK_FDB_PASSWORD") ?? "masterkey",
```
**Soll-Zustand:**
- Fallback-Werte `"SYSDBA"` und `"masterkey"` entfernen
- Wenn Env-Vars fehlen: `InvalidOperationException` werfen mit klarer Fehlermeldung
- Fehlermeldung: "IBAK_FDB_USER und IBAK_FDB_PASSWORD muessen als Umgebungsvariablen gesetzt sein."
**Aufwand:** S (15 min)

---

## Fix A03 — HOCH: BAA faelschlich als Riss-Label klassifiziert
**Datei:** `src/AuswertungPro.Next.UI/Ai/AiOverlayConverter.cs`  
**Zeile:** 280-286  
**Ist-Zustand:**
```csharp
private static bool IsRissLabel(string? label)
{
    if (string.IsNullOrEmpty(label)) return false;
    var lower = label.ToLowerInvariant();
    return lower.Contains("riss") || lower.Contains("crack")
        || lower.Contains("baa") || lower.Contains("bab");
}
```
**Problem:** BAA = Deformation (Verformung), NICHT Riss. BAB = Riss. `"baa"` ist hier falsch.  
**Soll-Zustand:**
- `"baa"` aus `IsRissLabel()` entfernen
- Ergebnis: `return lower.Contains("riss") || lower.Contains("crack") || lower.Contains("bab");`
- Optional: Neue Methode `IsDeformationLabel()` mit `"baa"`, `"deform"`, `"verform"` — gibt `OverlayToolType.Rectangle` zurueck
**Aufwand:** S (10 min)

---

## Fix A09 — HOCH: .gitignore um KnowledgeBase.db erweitern
**Datei:** `.gitignore`  
**Problem:** `KnowledgeBase.db` (SQLite, potentiell mehrere GB) ist nicht ignoriert.  
**Soll-Zustand:** Folgende Zeilen ans Ende von `.gitignore` hinzufuegen:
```
# KnowledgeBase SQLite (mehrere GB, nicht ins Repo)
**/Knowledge/*.db
**/KnowledgeBase.db
```
**Aufwand:** S (5 min)

---

## Fix A06 — HOCH: Microsoft.Data.Sqlite Versionen vereinheitlichen
**Dateien:**
- `src/AuswertungPro.Next.Infrastructure/AuswertungPro.Next.Infrastructure.csproj:15` → Version `8.0.2`
- `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj:29` → Version `10.0.3`

**Soll-Zustand:**
- Beide auf `10.0.3` setzen (oder beide auf die hoechste kompatible Version)
- Besser: In `Directory.Build.props` zentral definieren:
  ```xml
  <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.3" />
  ```
**Aufwand:** S (10 min)

---

## Fix A02 — HOCH: MaskQuantificationService Kalibrierung durchreichen
**Datei:** `src/AuswertungPro.Next.UI/Ai/Pipeline/MaskQuantificationService.cs`  
**Zeile:** 158-168  
**Problem:** `QuantifyAll()` ruft immer den unkalibrerten Pfad (hardcodiert 0.70).  
**Ist-Zustand:**
```csharp
public static IReadOnlyList<QuantifiedMask> QuantifyAll(
    SamResponse samResponse, int pipeDiameterMm)
{
    foreach (var mask in samResponse.Masks)
        results.Add(Quantify(mask, samResponse.ImageWidth, samResponse.ImageHeight, pipeDiameterMm));
}
```
**Soll-Zustand:**
```csharp
public static IReadOnlyList<QuantifiedMask> QuantifyAll(
    SamResponse samResponse, int pipeDiameterMm, PipeCalibration? calibration = null)
{
    foreach (var mask in samResponse.Masks)
        results.Add(Quantify(mask, samResponse.ImageWidth, samResponse.ImageHeight, pipeDiameterMm, calibration));
}
```
- Dann alle Aufrufer von `QuantifyAll()` pruefen und `calibration` durchreichen wo verfuegbar.
**Aufwand:** M (30 min)

---

## Fix A05 — HOCH: VSA minLength fuer Schaechte differenzieren
**Datei:** `src/AuswertungPro.Next.Infrastructure/Vsa/VsaEvaluationService.cs`  
**Zeilen:** 59, 99, 133  
**Problem:** `const double minLength = 3.0` wird 3x verwendet, aber Schaechte brauchen 0.5m.  
**Soll-Zustand:**
- Methoden-Signatur erweitern: `bool isManhole` Parameter
- `double minLength = isManhole ? 0.5 : 3.0;`
- Oder aus `VsaClassificationTable.DefaultMinLength_m` lesen (existiert bereits auf Zeile 9 der Table-Klasse!)
**Aufwand:** M (30 min)

---

## Fix A07 — HOCH: OllamaClient Circuit Breaker entschaerfen
**Datei:** `src/AuswertungPro.Next.UI/Ai/OllamaClient.cs`  
**Zeile:** 72-92  
**Ist-Zustand:** `FailureRatio = 1.0` (oeffnet nach jedem Fehler bei min. 5 Requests/60s)  
**Soll-Zustand:**
```csharp
.AddCircuitBreaker(new CircuitBreakerStrategyOptions
{
    FailureRatio = 0.5,          // 50% statt 100%
    SamplingDuration = TimeSpan.FromSeconds(60),
    MinimumThroughput = 10,      // 10 statt 5
    BreakDuration = TimeSpan.FromSeconds(30),
    ...
})
```
**Aufwand:** S (10 min)

---

## Fix A08 — HOCH: Sidecar VRAM-Monitor mit Request-Ablehnung
**Datei:** `sidecar/sidecar/main.py`  
**Zeile:** 67-85  
**Problem:** VRAM-Monitor loggt nur, blockiert keine Requests.  
**Soll-Zustand:**
- Globales Flag `_vram_critical: bool = False` einfuehren
- Im Monitor: `_vram_critical = (status == "critical")`
- Middleware oder Dependency in Routes: bei `_vram_critical == True` → HTTP 503 zurueckgeben
**Aufwand:** M (45 min)

---

## Fix A10 — HOCH: Protokoll-Revision Dedup bei Import
**Datei:** `src/AuswertungPro.Next.Infrastructure/Import/Kins/KinsImportService.cs`  
**Zeile:** 569-592  
**Problem:** Kein Duplikat-Check bei `History.Add(record.Protocol.Current)`.  
**Soll-Zustand:**
- Vor `History.Add()`: Hash der aktuellen Entries berechnen
- Vergleich mit Hash der neuen Entries
- Nur hinzufuegen wenn unterschiedlich
**Aufwand:** M (30 min)

---

## Fix A19 — MITTEL: AppSettings.Load() Silent Catch durch Logging ersetzen
**Datei:** `src/AuswertungPro.Next.UI/Ai/AiPlatformConfig.cs`  
**Zeile:** 82-84  
**Ist-Zustand:**
```csharp
try { settings = AppSettings.Load(); } catch { /* ignore */ }
```
**Soll-Zustand:**
```csharp
try { settings = AppSettings.Load(); }
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[AiPlatformConfig] AppSettings.Load fehlgeschlagen: {ex.Message} — verwende Defaults");
}
```
**Aufwand:** S (5 min)

---

## Weitere Findings (niedrigere Prioritaet, spaeter umsetzen)

| ID | Schwere | Kurzbeschreibung |
|----|---------|------------------|
| A04 | HOCH | Zwei Severity-Algorithmen vereinheitlichen (ConfidenceToSeverity vs EstimateSeverity) |
| A11 | MITTEL | AutoCalibration ScanLines erweitern (0.30-0.70) |
| A12 | MITTEL | PipeCalibration.NormToMm() Fallback 500 dokumentieren oder Exception |
| A13 | MITTEL | PixelToMm() Aspect-Ratio konsistent machen |
| A15 | MITTEL | SAM _SAM_MAX_BATCH dynamisch statt hardcodiert 100 |
| A16 | MITTEL | PerFrameTimeout > QwenFrameTimeout sicherstellen |
| A17 | MITTEL | Qwen-Timeout Fallback zu FastModel |
| A18 | MITTEL | ParseClockHour() Fallback 12.0 → null |
| A20 | MITTEL | KinsImportService Encoding-Fallback loggen |
| A22 | NIEDRIG | ClockToNormalized radius*0.8 dokumentieren |
| A23 | NIEDRIG | Qwen-Logging nur bei Diagnostics-Flag |
| A24 | NIEDRIG | Aggregator Magic Numbers in PipelineConfig auslagern |
| A25 | NIEDRIG | CloseGappedDetections() alle Events zurueckgeben |
