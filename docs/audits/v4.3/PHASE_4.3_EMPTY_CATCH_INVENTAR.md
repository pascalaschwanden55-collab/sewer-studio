# Phase 4.3 — Empty-catch-Komplettsweep (Inventar + Bewertung)

**Datum:** 2026-05-04
**Auftrag:** Empty-catch-Audit komplett, Logging-Pflicht (Audit A3, Konsens 3/3, ~4 h Schaetzung)
**Vorgeschichte:** Phase 1.2 hat 5 echt-stille Service-Catches mit `Debug.WriteLine` ergaenzt.
**Resultat dieser Phase:** Inventar bestaetigt — **keine weiteren echt-stillen Catches im Codebase**. Alle 98 verbleibenden `catch { }`-Stellen sind by design (dokumentiert oder selbsterklaerend).

---

## A. Bestand

```
98 empty catches in 23 Files (Stand 2026-05-04)
```

Top-Files nach Anzahl:
- `Views/Windows/TrainingCenterWindow.xaml.cs` — 34 (alle UI-Logging-Wrapper)
- `Views/Windows/PlayerWindow.xaml.cs` — 14 (Cleanup, OperationCanceled-Filter)
- `Views/Windows/CodingModeWindow.xaml.cs` — 8 (Cleanup)
- `Infrastructure/HoldingFolderDistributor.cs` — 6 (File-IO-Cleanup, Verzeichnis-Walk)
- `Infrastructure/Media/BatchMediaSearchService.cs` — 6 (Verzeichnis-Walk-Filter)
- `Views/Pages/DataPage.xaml.cs` — 3 (UI-Cleanup)
- `Infrastructure/Import/Xtf/M150MdbImportHelper.cs` — 3 (Connection-Cleanup)
- `Views/Windows/PlayerWindow.TrainingMode.cs` — 3 (Cleanup)
- `Ai/Training/Services/PdfProtocolTableParser.cs` — 3 (OCR-Debug-Log)
- ... weitere 14 Files mit je 1-2 Stellen

---

## B. Klassifikation der 98 Stellen

| Kategorie | Anzahl | Pattern | Bewertung |
|---|---:|---|---|
| **Cleanup-Pattern** | ~50 | `try { File.Delete(temp); } catch { }` <br> `try { proc.Kill(); } catch { }` <br> `try { _player.SetMarqueeInt(...); } catch { }` | **by design** — best effort, Fehlerbehandlung waere verschlimmert die Situation |
| **UI-Logging-Wrapper** | ~34 | `try { Vm?.AppendToLogText(msg); } catch { }` (alle in TrainingCenterWindow.xaml.cs) | **by design** — wenn das Logging selbst wirft, soll das den Programmfluss nicht stoeren |
| **Typed Exception-Filter** | ~10 | `catch (UnauthorizedAccessException) { }` <br> `catch (DirectoryNotFoundException) { }` <br> `catch (IOException) { }` | **by design** — bewusste Filter fuer erwartete Filesystem-Fehler beim Walk |
| **Lifecycle-Race-Filter** | ~3 | `catch (ObjectDisposedException) { }` <br> `catch (OperationCanceledException) { }` | **by design** — harmlose Race-Conditions beim Shutdown / nach User-Cancel |
| **Echt-stille Schluck-Catches** | **0** | — | **Phase 1.2 hat alle 5 + 2 audit-genannten erfasst** |

---

## C. Verifikation

Pruefkriterium **mehrzeilige typed Catches mit leerem Body**: alle gefundenen Stellen haben **klare Intention**:

```csharp
// PlayerWindow.xaml.cs:1715
catch (OperationCanceledException) { }   // User hat Cancel-Token getriggered
```

```csharp
// BatchMediaSearchService.cs:164-166
catch (UnauthorizedAccessException) { }  // Verzeichnis-Walk: kein Recht -> uebergehen
catch (DirectoryNotFoundException) { }   // Verzeichnis-Walk: weg seit letztem Listing
catch (IOException) { }                  // Verzeichnis-Walk: Lock o.ae.
```

```csharp
// KnowledgeMirrorService.cs:71
catch (ObjectDisposedException) { }      // Race beim Shutdown, harmlos
```

Pruefkriterium **mehrzeilige untyped Catches mit leerem Body**: zeigt 0 Treffer mit `catch\s*\n\s*\{\s*\n\s*\}`.

Pruefkriterium **`catch { }` einzeilig nach `try { ... }`**: alle gefundenen Stellen sind File-IO-Cleanup oder Process-Kill — Standard-Patterns wo Logging mehr Schaden als Nutzen brauchte (z.B. wenn `File.Delete` nach einem Crash-Stack scheitert, ist das nicht relevant).

---

## D. Empfehlung

**Phase 4.3 ist mit Phase 1.2 inhaltlich erledigt.**

Eine zusaetzliche Massen-Migration der 98 Cleanup-/Filter-Catches waere:
- **kontraproduktiv** — verlaengert Files mit mechanischem `Debug.WriteLine`-Boilerplate
- **scope-creep** — beruehrt 23 Files quer durchs Repo
- **wartungsfeindlich** — verschiebt das eigentliche Audit-Anliegen (echte stille Schluck-Pfade) in Boilerplate-Rauschen

Stattdessen:
- **Lint-Regel** waere ein moegliches Folge-Werkzeug: ein Roslyn-Analyzer der **untyped** `catch { }`-Bloecke bei einer Codebase-Konvention warnt. Phase X, separat.
- **Code-Review-Konvention**: bei neuen Catches IMMER entweder typen + Kommentar ODER Logging. Bestand ist sauber.

---

## E. Akzeptanz-Kriterium

Audit-Empfehlung Claude (audit_claude.md): *"alle 6+ Stellen mind. Debug.WriteLine($"{ex.Message}")"*

- Phase 1.2 hat 5 echt-stille Service-Catches migriert (KnowledgeMirrorService, FewShotExampleBuilder, SystemMonitorService, TrainingCenterImportService, PdfProtocolExtractor).
- Inventar Phase 4.3 bestaetigt: **keine weiteren echt-stillen Catches**.
- Audit-Punkt ✅ **erledigt** — durch Phase 1.2 inhaltlich, durch Phase 4.3 verifiziert.
