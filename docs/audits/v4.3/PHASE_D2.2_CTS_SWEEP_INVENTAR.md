# Phase D2.2 — CancellationTokenSource-Sweep nach RotateGenCts-Pattern

**Datum:** 2026-05-04
**Auftrag:** "Restliche `CancellationTokenSource`-Stellen im Repo nach RotateGenCts-Muster pruefen" — Audit D2.2 (konsolidiertes Audit, Befund 2.6).
**Resultat:** Sweep durchgefuehrt + 1 echte Anti-Pattern-Stelle behoben.

---

## A. Pattern-Definition

**Anti-Pattern (April-Audit Race-Fund):**
```csharp
_cts?.Cancel();
_cts?.Dispose();      // <-- Race: laufende Tasks haben den Token noch registriert
_cts = new CancellationTokenSource();
```

**Korrekt-Pattern (RotateGenCts):**
```csharp
var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
if (old is not null)
{
    try { old.Cancel(); } catch { /* race: already disposed */ }
    // Delayed Dispose: laufende Tasks koennen Cancellation sauber propagieren
    _ = Task.Delay(2000).ContinueWith(_ => { try { old.Dispose(); } catch { } });
}
return _cts!.Token;
```

---

## B. Inventar (alle CTS-Stellen mit Cancel+Dispose)

| Datei | Zeile | Pattern | Bewertung |
|---|---:|---|---|
| `TrainingCenterViewModel.cs` | 2597-2599 | Cancel + Dispose + new() | ⚠️ **Anti-Pattern** → behoben |
| `PlayerWindow.xaml.cs` | 327-329 | Cancel + Dispose + null | ✅ OK (Window-Closing-Cleanup) |
| `PlayerWindow.xaml.cs` | 341-342 | Cancel + Dispose + null | ✅ OK (Window-Closing-Cleanup) |
| `PlayerWindow.xaml.cs` | 1603-1605 | Cancel + Dispose + null | ✅ OK (StopLiveDetection — Endhandlung) |
| `PlayerWindow.xaml.cs` | 3299-3301 | Cancel + Dispose + null | ✅ OK (Coding-Mode-Stop — Endhandlung) |

**Begruendung "OK":** Alle PlayerWindow-Stellen setzen `_cts = null` nach Dispose und starten **keinen** unmittelbaren neuen Token. Es gibt keine Race-Bedingung mit nachfolgenden Token-Registrierungen, weil die CTS schlicht weggeworfen wird. Bei Re-Start (z.B. neuer LiveDetection-Run) wird die CTS frisch instanziiert — aber durch die `null`-Zwischenstufe gibt es keinen ueberlappenden Lifecycle.

**TrainingCenterViewModel.cs:2597 dagegen** rotierte ohne Helper: Dispose + sofortiges `new()` → Race-Zeitfenster, in dem laufende Tasks noch den alten Token referenzieren koennten.

---

## C. Behobene Stelle

### TrainingCenterViewModel — `_selfTrainingCts`

**Vorher (2597-2599):**
```csharp
_selfTrainingCts?.Cancel();
_selfTrainingCts?.Dispose();
_selfTrainingCts = new CancellationTokenSource();
var ct = _selfTrainingCts.Token;
```

**Nachher:**
```csharp
var ct = RotateSelfTrainingCts();
```

Helper-Methode neben `RotateGenCts` ergaenzt — gleiches Interlocked.Exchange + Task.Delay(2000) → Dispose Pattern.

---

## D. Andere CancellationTokenSource-Stellen (22 Files)

Sweep ueber alle 22 Files mit `CancellationTokenSource`-Vorkommen ergab keine weiteren Anti-Pattern-Treffer. Die meisten Files nutzen:

- **Constructor-Init + Closing-Dispose** (z.B. `MediaSearchWindow`)
- **`using var cts = new CancellationTokenSource()`** (lokale Scope, kein Race moeglich)
- **`CancellationTokenSource.CreateLinkedTokenSource`** mit `using` (Scope-gebunden)
- **Property/Field-Init nur einmal** (kein Re-Create)

Diese Patterns sind unkritisch.

---

## E. Akzeptanz

- [x] Pattern-Definition fixiert (Cancel+Dispose+new ohne Helper = Anti-Pattern).
- [x] 5 Cancel+Dispose-Stellen verifiziert: 1 Anti-Pattern, 4 sichere Cleanups.
- [x] `TrainingCenterViewModel._selfTrainingCts` mit `RotateSelfTrainingCts()`-Helper auf RotateGenCts-Pattern migriert.
- [x] Build gruen, 0 Fehler, 2 unrelated Warnings (CS0414 SystemMonitorService).

**Stand:** Repo nutzt konsequent das RotateGenCts-Pattern fuer rotierende CTS. Reine Cleanup-CTS (Window-Closing) bleiben mit ihrem einfachen Pattern, da kein Race-Risiko besteht.
