# SewerStudio — Gesamt-Audit & Korrekturplan (für Codex)

**Erstellt 2026-07-04 von Fable. Methode (transparent):** Synthese aus 7 verifizierten Audits (Sanierungsmatrix + Ausdruck 04.07, UI-Inventar 04.07, Daten-Landkarte 03.07, Arch-Audit 01.07, Deepdive 02.07, Deep-Audit/Arch-Quali 20.–21.06) **plus gezielte Nachverifikation am Code vom 04.07** (Greps/Reads, unten je Befund vermerkt). Keine teure Voll-Neuerkundung — bewusst budget-schonend. Punkte ohne frische Verifikation sind als „zu prüfen" markiert.

---

## A. Executive Summary

**Gesamtnote: B+ mit Tendenz A-.** Das Programm ist in den letzten Wochen massiv besser geworden: hartkodierte Root-Pfade beseitigt (Grep 04.07: nur noch 1 harmloser Text-Fallback), atomares Schreiben per Architektur-Test erzwungen, 7585 Tests grün (Baseline 04.07), Import-Robustheit R1–R4 committet, UI auf v4.5-Fluent-Niveau, Datensicherung fertig inkl. Tests. Die größten Hebel, in Reihenfolge: **(0)** ~550 uncommittete Dateien committen — das akuteste Risiko überhaupt; **(1)** die 7 Konsistenz-Pakete der Sanierungsmatrix umsetzen (fertiger Plan liegt vor — dort steckt echtes Geld-Fehler-Potenzial in Offerten); **(2)** Testlücke beim NPK-Leistungsverzeichnis-Exporter schließen (druckt Summen, hat 0 Tests); **(3)** Rest-Politur (leere catch-Blöcke mit Logging, Foto-Converter-Speicher, toter Legacy-Code raus).

---

## B. Befunde nach den 9 Dimensionen

### 1. Architektur & Kopplung
| Befund | Beleg | Schwere | Aufwand |
|---|---|---|---|
| Hardcodierte Root-Pfade: **BESEITIGT** — einziger Treffer ist Text-Fallback in `RestoreAnleitungText.cs:27` (Anleitung, kein Code-Pfad) | Grep `"[CD]:\\` über src, 04.07 | erledigt | — |
| PlayerWindow bleibt God-Klasse; Decomposition-Muster existiert (DamageMarkerController-Pilot) | bekannt, eigenes Vorhaben | Mittel | L |
| UI-Logik→Application (V3-Q9) und CostCalculator-Entzerrung (V3-Q10) offen | docs/ARCHITEKTUR-FAHRPLAN-V3-QUALITAET.md:97,107 | Mittel | L |
| Neue Services sauber mit Interface (z. B. `IFullBackupService`) — Muster hält | Application\Backup\IFullBackupService.cs | positiv | — |

### 2. Konsistenz (größter offener Block)
Vollständig auditiert am 04.07 → **docs/SANIERUNGSMATRIX-KONSISTENZ-PLAN.md** (K1–K7): toter Template-Editor (schreibt in Datei, die niemand liest), Matrix-Summe ≠ Druck-Summe bei Pauschalen, doppelte NPK-Nummern (311.110/221.110 je 2×), stiller Verlust von Hand-Preisen, Prüf-Checker rechnet anders als Preis-Engine, UI-Statistik ≠ PDF-Statistik, 3 Währungsformate. Schwere: Mittel bis fachlich-Hoch (Offerten!). Nicht neu auditieren — Plan umsetzen.

### 3. Robustheit
| Befund | Beleg | Schwere | Aufwand |
|---|---|---|---|
| 14 leere catch-Blöcke in 10 Dateien; teils legitim (`BestEffort.cs`, Timer-Stopper), aber `HoldingFolderDistributor.SidecarXtf.cs` (4×) und `DichtheitImportDistributor.cs` (2×) schlucken Import-Fehler still | Grep 04.07 | Mittel | S |
| Import-Robustheit R1–R4 frisch committet (PDF-Typ-Erkennung, KI-Schiedsrichter) — Pfad ist gut unterwegs | Commits 567384d9a, 230fe084b | positiv | — |
| IBAK Arizona.fdb 32-Bit-Problem: bekannt, bewusst aufgeschoben (.iml-XML als Alternative) | Memory 06/26 | Niedrig | — |

### 4. KI-Pipeline
| Befund | Beleg | Schwere | Aufwand |
|---|---|---|---|
| JSON-Schema für Qwen-Outputs wird technisch erzwungen (`["format"] = formatSchema`) — Regel eingehalten | `Infrastructure/Ai/OllamaClient.cs:152-171` | positiv | — |
| VisionPipelineClient hat Fehlerbehandlung (6 catch-Stellen); differenzierte Nutzer-Meldung „Sidecar aus vs. Token fehlt (401)" **zu prüfen** — bekannte Verwechslungsfalle | `Infrastructure/Ai/Pipeline/VisionPipelineClient.cs`; Memory sidecar-health-token | zu prüfen | S |
| VRAM-Budget/Warmup: keine neuen Befunde; keine Annahmen zu ByteTrack/32B-Eskalation (CLAUDE.md beachtet) | — | — | — |

### 5. Toter / verwaister Code
| Befund | Beleg | Schwere | Aufwand |
|---|---|---|---|
| Komplettes Legacy-Offerten-System ohne Aufrufer: `CalculateOffer/CalculateCombinedOffer`, `LegacyOfferTotalsCalculator` (rundet abweichend Banker's!), `OfferPdfModelFactory.Create`, `offer.sbnhtml`, `offer_profi.sbnhtml` | Audit 04.07, Grep bestätigt | Mittel (Verwechslungsrisiko) | S–M |
| `DiagnosticsPage.xaml` = 17 Zeilen, praktisch leer | UI-Audit 04.07 | Niedrig | S |
| Veralteter Branch `claude/create-splash-screen-C73uv` (überholt durch aktuellen Splash) | git 04.07 | Niedrig | S |

### 6. Tests
| Befund | Beleg | Schwere | Aufwand |
|---|---|---|---|
| `NpkLeistungsverzeichnisExporter`: **0 Tests** — exportiert Geld-Summen fürs LV | Audit 04.07 | **Hoch** | S |
| Kein Cross-Summen-Guard (Matrix-Total == Druckcenter-Netto == LV-Total) | Audit 04.07 | Hoch | M |
| Detail-Dirty-Guard (K4), Checker-Staffelpreise (K5), UI==PDF-Statistik (K6): ungetestete Risiko-Logik | Plan K-Pakete | Mittel | S je |
| Positiv: Backup-Feature voll getestet (Infrastructure\Tests\Backup + SettingsFullBackup*Tests), Test-Isolation etabliert (TestAppDataIsolation), 7585 grün | Testlauf 04.07 | positiv | — |

### 7. Performance
| Befund | Beleg | Schwere | Aufwand |
|---|---|---|---|
| Foto-Hotpath teilgefixt: `DecodePixelWidth` in PhotoGalleryPanel + HoverPreview vorhanden; `FileToImageConverter` (genutzt im TrainingCenter) lädt **ohne** DecodePixelWidth volle Auflösung → Speicher/Ruckeln bei großen Listen | Grep 04.07 | Mittel | S |
| Weitere Hotpaths: keine belegten neuen Befunde; vor Optimierung erst messen („zu prüfen", kein Blindflug) | — | zu prüfen | — |

### 8. UI/UX-Konsistenz
v4.5 „Fluent & Flow" hat die Lücken des UI-Audits geschlossen (Mica, Scrollbars, Tooltips, Slider, Empty-States, Abkoppeln, Dashboard). Offen: **manueller WPF-Smoke** (Checkliste am Ende von docs/UI-MODERNISIERUNG-V4.5-PLAN.md, macht der User) und bewusst die Strg+K-Palette (F7.2, optional).

### 9. Wartbarkeit
| Befund | Beleg | Schwere | Aufwand |
|---|---|---|---|
| Veraltete Doku: Referenzen auf nicht existierenden `DevisGenerator`/`DevisExcelExporter` (u. a. Architektur-Notizen/Skills) | Grep 04.07: 0 Treffer in src | Niedrig | S |
| Einheiten-Erkennung string-verstreut („m"/„Stk"/„h" vs „Std") → K7 UnitKinds | Audit 04.07 | Mittel | S |
| Doppelte Statistik-Klassifizierer UI vs. PDF → K6 | Audit 04.07 | Mittel | M |

### ⚠ Übergreifend, WICHTIGSTER Einzelpunkt
**~550 uncommittete Dateien** im Working Tree (v4.5 komplett + Datensicherung + weitere Lanes). Ein versehentlicher Reset/Checkout oder eine kollidierende Session kann Wochen Arbeit kosten (das gab es schon einmal — Rogue-Cleanup-Vorfall). **Schwere: Kritisch, Aufwand: S.**

---

## C. Priorisierter Umsetzungsplan (Wellen für Codex)

### Welle 0 — SOFORT, vor allem anderen (S)
1. **Arbeitsstand paketweise committen + pushen**: (a) `feat(backup): Datensicherung PC-Ausfall-Schutz` (Application\Backup, Infrastructure\Backup, Settings-UI, Tests); (b) v4.5 nach Unterpaketen (`feat(ui): v4.5 V0 Version+Splash`, `F1 Fluent-Fundament`, `F2 Tooltips`, …); (c) übrige Lanes getrennt. Vorher `dotnet build` + `dotnet test` (Baseline 7585 grün). KEINE Datei zurücksetzen — nur committen.

### Welle 1 — Quick Wins (je S, hoher Nutzen)
2. **NpkLeistungsverzeichnisExporter-Basistests** (Summen, Kapitel-Zwischentotale, Rundung, `="…"`-Schutz) — Testlücke bei Geld-Zahlen.
3. **K3 NPK-Dubletten** fixen (Katalog gegen `D:\Fachwissen\Revision_NPK135.pdf` abgleichen) + Katalog-Validierungswarnung.
4. **K4 Dirty-Guard** im Matrix-Detail (kein stiller Verlust von Hand-Preisen) + Test.
5. Matrix-Kopf „Total **(exkl. MwSt)**" (`SanierungsMatrixPage.xaml:27`); Dossier-Druck bekommt dieselbe Dirty-Warnung wie das NPK-LV.
6. **Leere catches instrumentieren**: `HoldingFolderDistributor.SidecarXtf.cs` (4×) und `DichtheitImportDistributor.cs` (2×) → mindestens `Debug/ILogger`-Meldung + Kommentar warum weiter; Verhalten NICHT ändern.
7. **FileToImageConverter**: `DecodePixelWidth` (z. B. 480) + `BitmapCacheOption.OnLoad` — Speicher-Fix, sichtbar im TrainingCenter.
8. `cost_summary.sbnhtml`: `page-break-inside:avoid` für `.group-row` + `thead`-Wiederholung; „Preis fehlt"-Kennzeichnung statt „0.00 CHF".
9. Zentraler `ChfFormat.Money` (de-CH, Apostroph) — die 4 divergenten Format-Stellen umstellen + Tests mit 12'345.67 (Details K6 im Matrix-Plan).

### Welle 2 — Struktur-/Konsistenzfixes (je M)
10. **K1**: MeasureTemplateEditor auf `MeasureTemplateStore` umhängen (Vorbild `ShellViewModel.cs:626-633`), einmalige Legacy-Migrations-Rückfrage; danach **Legacy-Offerten-Code entfernen** (eigener Commit; Legacy-DATEIEN in %APPDATA% behalten!).
11. **K2**: Pauschalen-Transparenz (Matrix-Kopfzeile „+ Pauschalen aus Tabelle", LV-Option ohne Pauschalen + Fußnote) + **Cross-Summen-Guard-Test** (wichtigster neuer Test).
12. **K5**: CostConsistencyChecker auf die echte `MeasurePricingEngine`-Preisauflösung umstellen (eine Wahrheit) + Staffelpreis-Test; Applier-Fallback-Hint-Refresh.
13. **K6**: EIN Spezialstatistik-Klassifizierer (UI-Resolver löschen/delegieren) + Cross-Test UI==PDF; MwSt-Altbestands-Hinweis mit Recompute-Angebot.
14. **K7 UnitKinds** (Längen-/Stunden-/Stück-Synonyme zentral) + Theory-Tests.
15. Sidecar-Fehlermeldung differenzieren: „läuft nicht" vs „Token/401" (bekannte Falle) — kleine UI-Meldung + Test. Abhängigkeit: keine.
16. Doku-Hygiene: DevisGenerator-Referenzen aus Doku/Notizen entfernen; DiagnosticsPage entweder füllen (Log-Viewer-Kachel) oder Nav-Eintrag ausblenden.

### Welle 3 — Größere Umbauten (L, NUR nach Diskussion mit dem User)
17. V3-Q9: Logik-Controller UI → Application (Fahrplan-Lane CLAUDE führt).
18. V3-Q10: CostCalculator entzerren.
19. PlayerWindow-Decomposition fortsetzen (Muster: exklusiver Zustand→Controller).
20. Echte NPK-135-**Offerten-Vorlage** (Deckblatt/Konditionen, `D:\Fachwissen\Offerten` als Referenz) — neues Feature, eigener Brainstorm.

**Test-Strategie überall:** jede Logik-Änderung mit fokussiertem Test im selben Commit; nach jedem Punkt `dotnet build` + betroffene Testprojekte; Summen-Änderungen mit Vorher/Nachher-Zahl im Commit-Text.

---

## D. BEWUSST NICHT anfassen

- **Verworfen (nicht re-litigieren, Verdikt 2026-06-20):** Microsoft-DI-Umbau, Docker/K8s, adaptives QualityGate, VsaCodeTree-Merge, async-Sidecar-Umbau.
- **Rundungsarchitektur** (Positions- vs. Haltungsebene) und **5-Rappen-Rundung**: dokumentierte Rappen-Differenzen, nur per Guard-Test sichtbar machen — kein Umbau ohne User-Entscheid.
- **Docking-Framework (AvalonDock)**, WPF→WinUI/MAUI, Custom-Window-Chrome: bewusst nicht (NuGet-Regel + Overkill).
- **KI-Modell-Wechsel / qwen2.5-Rückfall / SAM-3-Aktivierung**: Hebel ist Daten/Codierung, nicht Modelle (CLAUDE.md + A/B-Historie).
- **Medienverteilungs-Kopierverhalten** beim Import: gewollt, nicht „optimieren".
- **Legacy-Dateien unter %APPDATA%** löschen: nie — nur toten CODE entfernen („Daten nie verlieren").
- **Thin-AI, Laptop-/Workstation-Abstraktion, VRAM-Budget 29 GB**: Prinzipien, keine Baustellen.
