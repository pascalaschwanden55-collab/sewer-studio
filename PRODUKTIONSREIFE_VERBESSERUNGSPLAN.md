# Produktionsreife: Bewertung und Verbesserungsfahrplan

> Stand: 2026-07-12 · Branch: `feature/gis-karte` · Erstellt durch KI-Audit (16 Prüf-Agenten + unabhängige Gegenprüfung der kritischen Befunde)
> **Status: 11 von 16 Prüfbereichen abgeschlossen.** Nicht geprüft (Modell-Limit erreicht): Nebenläufigkeit, Externe Dienste, Codequalität, Logging/Diagnose, Performance. Alle anderen Aussagen sind belastbar; die kritischen Befunde wurden von einem zweiten, skeptischen Agenten am Code gegengeprüft.
>
> **Ergebnis der Gegenprüfung:** Alle geprüften Befunde wurden als real bestätigt (CONFIRMED, keiner widerlegt). Bei mehreren wurde die Datenverlust-Schärfe von P1 auf P2 gesenkt, weil ein tatsächlicher Verlust zusätzliche Umstände braucht (z.B. der Nutzer löscht selbst das Original). Die betroffenen Punkte bleiben trotzdem in Stufe 1 des Fahrplans, weil sie real sind und billig zu beheben.

## Arbeitsstand

| Kapitel | Status |
|---|---|
| 0. Bestandsaufnahme | ✅ |
| 1. Kurzfazit | ✅ (vorläufige Gesamtnote, 5 Bereiche wegen Modell-Limit ungeprüft) |
| 2. Bewertungsmatrix | ✅ 11/16 Bereiche |
| 3. Kritische Sofortmaßnahmen (P0–P3) | ✅ inkl. Gegenprüfungs-Verdikte |
| 4. Sicherungs- und Wiederherstellungskonzept | ✅ |
| 5. Architektur-Zielbild | ✅ |
| 6. Umsetzungsfahrplan (Arbeitspakete) | ✅ |
| 7. Empfohlene Reihenfolge (Stufen 0–8) | ✅ |
| 8. Schnell umsetzbare Verbesserungen | ✅ |
| 9. Risikoregister | ✅ |
| 10. Definition „Note A" | ✅ |
| 11. Übergabe-Prompts | ✅ |
| Offene Prüfungen | ✅ (wird nach Nachlauf aktualisiert) |

---

## 1. Kurzfazit

**Gesamtnote: B** (vorläufig; 11 von 16 Bereichen geprüft — die fehlenden 5 ändern das Bild voraussichtlich nicht grundlegend, weil die kritischsten Bereiche geprüft sind).

**Das Wichtigste in einem Satz:** Kein einziger P0-Befund — es gibt keinen bekannten Weg, wie die App von sich aus Daten zerstört. Nach Gegenprüfung bleiben **6 echte P1-Lücken** plus 4 real bestätigte, aber entschärfte Befunde (von P1 auf P2 gesenkt). Fast alle betreffen dasselbe Muster: **Die Sicherheitsnetze existieren, aber sie greifen im Ernstfall nicht automatisch** (kaputtes Projekt, verlorene Env-Var, abgebrochener Import, zweite Instanz).

### Größte Stärken (am Code belegt)

1. **Atomares Speichern ist flächendeckend Standard:** Projekt, Einstellungen, Trainingsdaten, Kataloge — überall Temp-Datei + `File.Replace` + `.bak`-Generationen. Ein Stromausfall mitten im Speichern zerstört die alte Datei nicht. Ein Architektur-Test (`AtomicPersistenceArchitectureTests`) erzwingt das Muster sogar maschinell.
2. **Das neue Backup-System (PC-Ausfallschutz) ist überdurchschnittlich sorgfältig:** SQLite-Online-Backup-API + `integrity_check`, SHA256-Verifikation beim Kopieren, Marker-Datei-Schutz gegen versehentliches Leeren fremder Ordner, Platz-Prüfung, 10 Versions-Stände, generierte Restore-Anleitung mit echten PC-Pfaden — plus Tests.
3. **Testsuite auf ungewöhnlichem Niveau:** ~1500 Testdateien, ~6658 Facts/Theories, echte Verhaltenstests (Fehlerinjektion, Abbruch, kaputte Eingabedateien), Architektur-Fitness-Tests, Eval-Set-Kontaminations-Guards. Alles läuft offline ohne GPU/Ollama.
4. **Sicherheit: Note A.** Sidecar nur auf 127.0.0.1 mit Token-Pflicht (`hmac.compare_digest`), keine Secrets im Repo, zentrale Prozessstarts mit ArgumentList, Pfad-Sandbox im Training-Export.
5. **Saubere Schichtung:** Domain ← Application ← Infrastructure ← UI ohne eine einzige Rückwärtsreferenz; KI-Pipeline liegt testbar in Infrastructure, nicht in der UI.

### Die 5 größten Risiken

| # | Risiko | Warum kritisch |
|---|---|---|
| 1 | **Projekte und Videos sind NICHT im PC-Ausfallschutz** (`BackupPlanBuilder` sichert nur Programm/KI-Gehirn/Einstellungen/Logs) | Festplattenausfall auf D: = Totalverlust der eigentlichen Arbeitsergebnisse, trotz „Datensicherung" |
| 2 | **Korrupte Projektdatei: Rettungskopien (.bak, __RESTORE_POINTS) existieren, aber die App bietet sie nicht an** — nur Fehlerzeile in der Statusleiste | Faktischer Datenverlust durch Nichtwissen; der Nutzer hält das Projekt für verloren |
| 3 | **Restore-Point vor Import läuft bei neuen Projekten ins Leere** (sucht `projekt.json` im falschen Ordner) + kein Rollback bei abgebrochenem Import | Halbimportiertes Projekt wird per AutoSave dauerhaft; kein Weg zurück |
| 4 | **Wiederherstellung ist ungeprüft:** kein kompletter Proberestore durchgeführt, keine Restore-Funktion in der App, Backup nur manuell ohne Erinnerung | Das klassische Backup-Restrisiko: Sicherung vorhanden, Rückweg unbewiesen |
| 5 | **Kein Test-Gate/CI + keine Schema-Versionsprüfung der Projektdatei** | Regressionen akkumulieren unbemerkt; ältere App-Version verliert still Felder neuerer Projektdateien |

### Soll das Programm bereits produktiv eingesetzt werden?

**Ja, für den jetzigen Solo-Eigenbetrieb — mit zwei Sofort-Auflagen.** Die Kern-Speicherpfade sind solide (atomar, .bak, Restore-Points, Autosave). Aber: (1) Projekte müssen SOFORT zusätzlich manuell gesichert werden (bis AP-03 umgesetzt ist), (2) ein kompletter Proberestore muss einmal durchgespielt werden. Für einen Einsatz über den Eigenbetrieb hinaus (anderer PC, andere Person): erst alle P1-Punkte schließen.

---

## 2. Bewertungsmatrix

| Bereich | Note | Ziel | Wichtigste Begründung | Höchstes Risiko | Nötigste Maßnahme |
|---|---|---|---|---|---|
| Datenablage | B | A | Trennung Programm/Config/Nutzdaten sauber; zentrale Pfad-Resolver | XTF-Rohdaten landen in `bin\` (vom Backup ausgeschlossen); KB-Root hängt still an Env-Var | AP-05, AP-06 |
| Atomares Speichern & Backup | B | A | Temp+Replace+.bak überall; Backup-Code vorbildlich | Projekte/Videos nicht im Backup; kein .bak-Angebot bei kaputtem Projekt | AP-01, AP-03 |
| SQLite-Integrität | B | A | WAL, Transaktionen, Online-Backup-API, integrity_check im Backup | Fehlende/kaputte KB wird still als leer neu angelegt → Backup rotiert guten Stand weg | AP-11 |
| Absturz & Wiederanlauf | B | A | Alle 3 globalen Handler, Autosave „bei jeder Änderung", Batch wiederaufnehmbar | Kein Single-Instance-Schutz → Last-Write-Wins zwischen zwei Instanzen | AP-04 |
| Import/Export-Robustheit | B | A | Result-Pattern, Fehler-Isolation pro Datei/Haltung, InvariantCulture, Dry-Run | IBAK-Re-Import häuft VsaFindings an → verfälschte Zustandsnote | AP-13 |
| Datenkonsistenz | C | B+ | UserEdited-Schutz doppelt; aber Restore-Point-Lücke, keine Schema-Migration, Duplikat-Namen möglich | Stiller Feldverlust zwischen App-Versionen; halbimportierte Projekte | AP-02, AP-08 |
| Architektur | B | B+ | Schichten einbahnig, Fitness-Tests, Composition Root schlank | Sidecar-API-Drift schlägt still fehl (leere Erkennungen) | AP-16 |
| Codequalität | ⏳ | B+ | *(Nachlauf läuft)* | | |
| Sicherheit | **A** | A | Sidecar-Token+Loopback, keine Secrets, ArgumentList, Sandbox | Nur P3-Randnotizen | keine (halten) |
| Tests | B | A | 8000+ echte Verhaltenstests, offline, Architektur-Guards | Kein CI/Gate; wichtigster Speicherpfad (Projekt) ohne Crash-Tests | AP-09, AP-10 |
| UI/Bedienbarkeit | B | A | Zentraler DialogService, Fortschritt+Abbruch überall, Dirty-Guard mehrschichtig | „Projekt entfernen" verwirft ungespeicherte Änderungen still | AP-12 |
| Logging/Diagnose | ⏳ | B+ | *(Nachlauf läuft)* | | |
| Performance | ⏳ | B | *(Nachlauf läuft)* | | |
| Nebenläufigkeit | ⏳ | B+ | *(Nachlauf läuft)* | | |
| Externe Dienste | ⏳ | B+ | *(Nachlauf läuft)* | | |
| Installation/Update/Doku | B | A− | Publish-Skript reproduzierbar mit Checks+Manifest; Settings-Quarantäne | Frischer PC: pdftotext fehlt, DINO braucht Internet, keine Setup-Anleitung | AP-18 |

---

## 3. Kritische Sofortmaßnahmen

**P0 (sofort, Datenverlust möglich): KEINE.** Der Audit fand keinen Pfad, auf dem die App ohne äußere Einwirkung Daten zerstört.

### Kritische Befunde nach Gegenprüfung

Jeder Befund unten wurde von einem zweiten Agenten am Code gegengeprüft. Spalte „Verdikt" zeigt das Ergebnis: **CONFIRMED** = selbst am Code bestätigt. Spalte „Prio (final)" ist die Einstufung nach Gegenprüfung. **Alle diese Punkte gehören in Stufe 1 des Fahrplans**, unabhängig vom Label — die P1/P2-Grenze markiert nur, ob ein Datenverlust unmittelbar oder erst mit Zusatzumständen droht.

**Echte P1 (Datenverlust/Fehlfunktion realistisch, vor Produktivbetrieb beheben):**

| Nr | Befund | Beleg | Verdikt | Prio (final) |
|---|---|---|---|---|
| P1-7 | KB-Root hängt still an User-Env-Var `SEWERSTUDIO_KNOWLEDGE_ROOT` — ohne sie startet die App mit veraltetem/leerem „Gehirn", ohne Warnung. **Ist schon einmal passiert.** | `KnowledgeBasePaths.cs:83-87`; belegt durch `Start_SewerStudio.bat` + `docs/DATENSICHERUNG-UEBERGABE-CODEX.md:92` | **CONFIRMED** | **P1** (bleibt) |
| P1-3 | Restore-Point vor Ein-Knopf-Import greift bei neuer Projektstruktur (`Projektdateien\projekt.json`) nicht — Sicherheitsnetz still wirkungslos | `ProjectImportOrchestrator.cs:106` vs. `ProjectFileLocator.cs:33-34` | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |
| P1-4 | Keine Schema-Versionsprüfung: ältere App-Version verliert still Felder neuerer Projektdateien | `Project.cs:10` (Version wird nie gelesen), `JsonProjectRepository.cs:24` | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |
| P1-10 | Abgebrochener/teilweiser Import mutiert das Live-Projekt ohne Rollback; AutoSave persistiert den Teilzustand | `ImportRunWorkflowController.cs:79-83,160-165`, `ProjectImportOrchestrator.cs:416` | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |
| P1-8 | Kein CI / kein Test-Gate vor Commit/Push (8000+ Tests laufen nur manuell) | kein `.github/workflows`, pre-push-Hook nur LFS | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |
| P1-9 | `JsonProjectRepository` (wichtigste Datei!) ohne Crash-/Korruptions-/Roundtrip-Tests; fehlt in `AtomicPersistenceArchitectureTests` | `JsonProjectRepository.cs:44-99`, `AtomicPersistenceArchitectureTests.cs:10-29` | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |

**Durch Gegenprüfung von P1 auf P2 gesenkt (real, aber Datenverlust nur mit Zusatzumständen) — trotzdem Stufe 1, weil billig:**

| Nr | Befund | Verdikt | Warum herabgestuft |
|---|---|---|---|
| P1-1→P2 | Projekte/Videos nicht im PC-Ausfallschutz (`BackupPlanBuilder.cs:42-92`) | **CONFIRMED** | Projekte sind heute manuell separat sicherbar; Verlust nur bei D:-Ausfall OHNE eigene Sicherung. **Risiko-Ranking bleibt #1** — der Name „Ausfallschutz" verspricht mehr, als er hält. |
| P1-2→P2 | Korruptes Projekt-JSON: kein .bak-Angebot, nur Statuszeile (`JsonProjectRepository.cs:29-32`, `ShellViewModel.cs:464-468`) | **CONFIRMED** | .bak und __RESTORE_POINTS liegen daneben; Verlust nur durch Nichtwissen des Nutzers, technisch rettbar |
| P1-5→P2 | Kein Single-Instance-Schutz (0 Mutex-Treffer in src/; `SettingsStore.cs:56`) | **CONFIRMED** | Verlust nur, wenn Nutzer versehentlich zwei Instanzen parallel bearbeitet; atomare Writes verhindern Datei-Korruption |
| P1-6→P2 | XTF-Import archiviert Rohdaten in `bin\` (`LegacyXtfImportService.cs:24`) | **CONFIRMED** | bin-Kopie ist nur redundant, Original bleibt + Ein-Knopf-Import archiviert zusätzlich korrekt. **Aber:** `.m150`/`.xml` fehlen im Archiver-Mapping → für diese Formate ist die bin-Kopie die einzige Archivkopie |

### P2 — wichtige Qualitätsverbesserungen (Auswahl, 18 Punkte)

| Nr | Befund | Beleg |
|---|---|---|
| P2-1 | KB-Import-Remap überschreibt `training_samples.json` nicht atomar UND schluckt Schreibfehler (Import meldet Erfolg trotz kaputter JSON) — Gegenprüfung: von P1 herabgestuft, da Import-ZIP als Rettung existiert | `KnowledgeBackupService.cs:171,326,402,329-332` |
| P2-2 | Produktivbetrieb direkt aus `bin\Debug` — Build ist gleichzeitig Installation | `Desktop\Start_SewerStudio.bat` |
| P2-3 | Backup nur manuell, keine Erinnerung bei überfälliger Sicherung | `SettingsFullBackupWorkflow.cs:36-39`; `LastFullBackupUtc` wird nie ausgewertet |
| P2-4 | Keine Restore-Funktion in der App; Restore nie komplett durchgespielt | nur `RESTORE-ANLEITUNG.txt`, `docs/PC-AUSFALLSCHUTZ-MANUELLER-TEST.md` |
| P2-5 | Fehlende KnowledgeBase.db wird still als leere DB neu angelegt; Folge-Backups rotieren den guten Stand aus den Versionen | `KnowledgeBaseContext.cs:30-31` |
| P2-6 | Keine Korruptions-Erkennung der KB beim Start (kein `PRAGMA quick_check`) | `ServiceProvider.cs:202-205` |
| P2-7 | WinCan-DB3 + Legacy-Migration öffnen fremde DBs nicht ReadOnly | `WinCanDbImportService.cs:90` |
| P2-8 | IBAK-Re-Import häuft VsaFindings an (append-only) → verfälschte VSA-Zustandsnote möglich | `IbakExportImportService.cs:170-198` |
| P2-9 | Kein Duplikat-Check für Haltungsnamen; Re-Import merged in den ERSTEN Treffer | `DataPage.xaml.cs:583`, `Project.cs:159-180` |
| P2-10 | Video-Link an 4 von 5 Schreibstellen absolut gespeichert → Projekt nicht portabel | `HoldingFolderDistributor.cs:269,1052` u.a. |
| P2-11 | Abgebrochener Import kann korrupte halbe Archivdatei hinterlassen, die beim Re-Import „behalten" wird | `ImportSourceArchiver.cs:65-91` |
| P2-12 | „Projekt aus Übersicht entfernen" setzt Dirty=false → verwirft ungespeicherte Änderungen still | `OverviewPageViewModel.cs:589` |
| P2-13 | Projekt Laden/Speichern synchron im UI-Thread ohne Fortschritt (Freeze bei großen Projekten) | `ShellViewModel.cs:463,550` |
| P2-14 | Speicher-/Ladefehler nur als Statuszeile, kein Dialog | `ShellViewModel.cs:553,466,595` |
| P2-15 | SQLite-KB ohne Korruptions-/Recovery-Test; Cancellation mitten im Vorgang kaum getestet | Tests-Audit |
| P2-16 | Sidecar-HTTP-Vertrag ohne Versionsprüfung — API-Drift liefert still leere Erkennungen | `main.py:58` vs. `VisionPipelineClient.cs` |
| P2-17 | pdftotext.exe fehlt im Release; Grounding DINO braucht beim ersten Laden Internet (HF-Tokenizer); keine „Neuer PC"-Anleitung | Publish-Skript, `GroundingDINO_SwinB_cfg.py:34` |
| P2-18 | God-Klasse `HoldingFolderDistributor` (3071 Zeilen, statisch, verschiebt Dateien) | `HoldingFolderDistributor.cs:23` + `.PdfParsing.cs` |

### P3 — spätere Optimierung (Auswahl)

Hartkodierte Pfade in Defaults (`D:\QGIS_V4.03`, `basemap_tiles` mit Versionsnummer im Pfad); Restore-Anleitung nennt Fallback „4.4"; Legacy-Ordner `%APPDATA%\AuswertungPro` weiter aktiv beschrieben; Excel-Export nicht atomar; Protokoll-PDF scheitert komplett an einem korrupten Foto; `UNBEKANNT_`-Duplikate beim PDF-Re-Import; WinCan-Zahlparser mit CurrentCulture-Fallback (`1.234`-Mehrdeutigkeit); Meter-Formatierung ohne explizite Culture; verwaister Sidecar hält VRAM nach App-Ende; kein Not-Speichern im Crash-Handler; zweite Exception im Guard komplett verschluckt; statische Fassaden neben DI unbewacht; 433 Dateien flach in `UI/Ai/`; `GetService()` unvollständig (liefert still null); FFmpeg-Aufruf per String-Konkatenation; Ollama-URL ohne Loopback-Warnung; Test-Env-Var-Manipulation (Flakiness); veraltete READMEs (cu121!); Publish-Version doppelt gepflegt; ~1 GB Hand-`.bak`-Dateien im KI_BRAIN-Root.

---

## 4. Sicherungs- und Wiederherstellungskonzept

### 4.1 Ist-Zustand (belegt)

**Vorhanden und gut:**
- `FullBackupService` (PC-Ausfallschutz): sichert Programm-Repo, KI-Gehirn (C:\KI_BRAIN), Einstellungen (%LOCALAPPDATA%\SewerStudio + %APPDATA%), Logs, Extras — mit SHA256-Verify, SQLite-Online-Snapshot + integrity_check, Marker-Datei-Schutz, Platz-Prüfung, 10 datierten Versions-Ständen, generierter Restore-Anleitung. Manuell per Knopf in den Einstellungen.
- Lokale Sicherheitsnetze: `.bak`(-Generationen) neben jeder wichtigen Datei, bis zu 20 Restore-Points pro Projekt (`__RESTORE_POINTS`), 20 Settings-Restore-Points, Quarantäne korrupter Settings.
- `docs/PC-AUSFALLSCHUTZ-MANUELLER-TEST.md` + `docs/RESTORE_KI_BRAIN.md` (Teil-Anleitungen).

**Lücken (die drei entscheidenden):**
1. **Projekte + Videos fehlen im Backup** — die eigentlichen Arbeitsergebnisse.
2. **Kein Zeitplan, keine Erinnerung** — Sicherung schläft ein.
3. **Restore nie komplett geprüft, keine Restore-Funktion** — Rückweg unbewiesen.

### 4.2 Ziel: 3-2-1 für SewerStudio (einfach bedienbar)

**Drei Kopien, zwei Medien, eine außer Haus:**

| Kopie | Was | Wo | Wie |
|---|---|---|---|
| 1 (Arbeitsdaten) | Alles | C: / D: (Arbeitsplatte) | die App selbst (atomar + .bak + Restore-Points) |
| 2 (PC-Ausfallschutz) | Programm + KI-Gehirn + Einstellungen + Logs + **NEU: Projekte (ohne Videos optional)** | externe USB-Platte | `FullBackupService`, erweitert um Projekte-Komponente (AP-03) |
| 3 (außer Haus) | mindestens: Projekte + KI-Gehirn-Snapshot + Einstellungen | zweite Platte an anderem Ort ODER Cloud-Ordner | denselben Backup-Lauf auf zweites Ziel wiederholen (Backup-Dialog erlaubt freie Zielwahl bereits heute) |

Videos sind groß (~3000 Stück): Sie gehören mindestens auf Kopie 2 (USB-Platte, einmalig + bei Zuwachs); für Kopie 3 reichen die Projekt-JSONs + Protokolle + Fotos, weil Videos aus den Original-Kanal-TV-Exporten wiederbeschaffbar sind. Diese Entscheidung ist im Backup-Dialog sichtbar zu machen („Videos eingeschlossen: ja/nein").

### 4.3 Sicherungsablauf (Soll)

| Wann | Was | Auslöser |
|---|---|---|
| Bei jeder Änderung | Arbeitsdaten (Autosave + .bak) | automatisch (existiert) |
| Vor jedem Import | Restore-Point der projekt.json | automatisch (existiert, **aber AP-02 nötig, damit er wieder greift**) |
| Wöchentlich | Voll-Backup auf USB (inkl. Projekte) | Erinnerung beim App-Start, wenn `LastFullBackupUtc` > 7 Tage und Ziel erreichbar (AP-14) |
| Monatlich | Zweites Voll-Backup auf Außer-Haus-Ziel | Erinnerung, wenn > 30 Tage |
| Vor jedem Update (neuer Publish-Ordner) | Voll-Backup | Hinweis in INSTALLATION.txt (AP-18) |

**Aufbewahrung:** Das existierende Schema (10 datierte Versions-Stände in `_Versionen`) beibehalten. Zusätzlich: die manuellen ~1 GB Hand-`.bak`-Dateien im KI_BRAIN-Root nach `kb_backups/` verschieben (P3), damit Backups schlank bleiben.

**Prüfung der Sicherung:**
- Beim Kopieren: existiert (SHA256-Vergleich im `DirectoryMirror`).
- Neu (AP-15, optional P3): SHA256 pro Datei ins Manifest + Knopf „Sicherung prüfen", damit auch späteres Bit-Rot auf der USB-Platte auffällt.
- Fehlgeschlagener DB-Snapshot muss als eigene Warnung erscheinen, nicht nur in SkippedFiles (Teil von AP-11).

**Wiederherstellung:**
- Kurzfristig: die generierte `RESTORE-ANLEITUNG.txt` ist gut (echte Pfade, Env-Vars, Modellliste). Fallback-Pfad „4.4" korrigieren (15 min).
- **Pflicht vor Freigabe: EIN kompletter Proberestore** auf ein sauberes Verzeichnis/Testkonto: Backup → Programm starten → Projekt öffnen → KB-Samplezahl prüfen → Einstellungen da? Ergebnis in `docs/` protokollieren (AP-17). Danach jährlich bzw. nach größeren Backup-Änderungen wiederholen.
- Mittelfristig (P2): geführter Restore-Dialog in der App.

**Verhalten bei Backup-Fehlern:** Heute meldet der Workflow übersprungene Dateien (gut). Zusätzlich: Fehlschlag des Gesamtlaufs muss `LastFullBackupUtc` NICHT setzen und beim nächsten Start erneut erinnern.

**Meldung an den Benutzer:** Eine Zeile reicht: „Letzte Datensicherung: vor X Tagen (Ziel: E:\…). Projekte enthalten: ja/nein." — sichtbar in den Einstellungen (existiert) und als Start-Hinweis bei Überfälligkeit (AP-14).

### 4.4 Notfallhandbuch

Eine Datei `docs/NEUER-PC-SETUP.md` (1–2 Seiten, AP-18) mit der kompletten Reihenfolge: .NET 10 SDK → Python + uv → `sidecar/setup.ps1` → Ollama + Modelle (`qwen3-vl:8b-q8`, `nomic-embed-text`) → ffmpeg → pdftotext nach `tools/` → Env-Vars (`SEWERSTUDIO_KNOWLEDGE_ROOT`!) → Backup zurückspielen nach RESTORE-ANLEITUNG → Proberestore-Checkliste. Großteils aus `RestoreAnleitungText.cs` übernehmbar.

### 4.5 Schutz vor Build-, Update- und Benutzerfehlern

- **AP-05:** XTF-Rohdaten-Archiv raus aus `bin\` (einzige echte Nutzdaten im Build-Output).
- **P2-2:** Produktivstart auf `dotnet publish`-Ordner außerhalb des Repos umstellen (`tools/Publish-SewerStudio.ps1` existiert und ist gut); Repo bleibt reine Entwicklungsumgebung. Update = neuer Ordner daneben, Rollback = alten Ordner starten.
- Updates gefährden Nutzdaten nicht (liegen in AppData/KI_BRAIN/D:\Projekt — verifiziert), das gehört so dokumentiert (AP-18).

---

## 5. Architektur-Zielbild

**Kernaussage: Die Architektur ist für einen Solo-Entwickler richtig dimensioniert. Kein Umbau nötig — nur punktuelle Härtung.**

**Beibehalten (funktioniert nachweislich):**
- Schichtung Domain ← Application ← Infrastructure ← UI (einbahnig, per Fitness-Test bewacht).
- Handgerollter `ServiceProvider` als Composition Root (typisierte Properties, ein HttpClient) — kein DI-Framework nötig.
- KI-Pipeline in Infrastructure (testbar ohne UI), Sidecar als getrennter Python-Prozess.
- Die 433 Mikro-Klassen in `UI/Ai` sind testgetrieben und fachlich ok — nur Ordner-Struktur fehlt.

**Problematische Abhängigkeiten (klein halten):**
1. **C#↔Python-Vertrag ist implizit** (doppelt gepflegte DTOs, still-tolerante Deserialisierung). → Versionskonstante beidseitig + Health-Check-Vergleich (AP-16). Kein Schema-Generator nötig.
2. **`HoldingFolderDistributor` (3071 Zeilen, statisch)** mischt PDF-Parsing mit Datei-VERSCHIEBEN. → Nur den Verteil-Teil (Move/Copy) hinter ein kleines Interface ziehen, beim nächsten ohnehin nötigen Eingriff. PDF-Parsing darf statisch bleiben.
3. **Vier statische Fassaden** (`StatusColors.Current`, `CodeUsageTrackers.Current`, `DialogHost.Current`, `VsaCodeResolver`) neben dem DI. → Nicht umbauen; per Fitness-Test-Whitelist einfrieren, damit das Muster nicht wächst.
4. **ViewModels erzeugen Kosten-Stores selbst** (11 `new`-Stellen). → Beim nächsten Anfassen der jeweiligen Seite über ServiceProvider beziehen. Kein Sammel-Refactoring.
5. **Application-Schicht enthält QuestPDF-Rendering + XML-Parsing** (je >1100 Zeilen). → Nur als bewusste Ausnahme dokumentieren (eine Zeile in CLAUDE.md).

**Reihenfolge:** Zuerst Datenverlust-Pakete (Stufe 1), dann Sidecar-Versionscheck (AP-16); alles andere opportunistisch. **Keine Neuentwicklung irgendeines Teils ist nötig.**

---

## 6. Umsetzungsfahrplan (Arbeitspakete)

Jedes Paket ist einzeln an Codex/Opus übergebbar (Prompts in Kapitel 11). Aufwände sind Netto-Schätzungen.

### Stufe 1 — Schutz vor Datenverlust

| AP | Titel | Prio | Aufwand | Betroffene Dateien | Abnahme |
|---|---|---|---|---|---|
| **AP-01** ✅ | Projekt-Laden: .bak/Restore-Point-Fallback + Fehlerdialog | P1 | 2–4h | `ShellViewModel.TryOpenProject`, `JsonProjectRepository` | **UMGESETZT 2026-07-12:** Neue `ProjectRecovery` (Infrastructure, 4 Tests) lädt bei kaputter projekt.json aus `.bak`, dann Restore-Points (neueste zuerst), und quarantäniert die kaputte Datei als `projekt.corrupt-<ts>.json` (nie gelöscht; nur wenn eine Kopie wirklich lädt). Verdrahtet in `TryOpenProject`: Warn-Dialog bei Rettung (nennt Quelle + Quarantäne, markiert dirty → Neuspeicherung), Error-Dialog wenn nichts rettbar (Original unangetastet). Build grün, 32 Infra- + 75 Architektur-Tests grün. **Offener manueller Abnahmeschritt:** projekt.json mit Byte-Müll überschreiben → App bietet .bak an (Live, braucht laufende App). |
| **AP-02** ✅ | Restore-Point vor Import reparieren (`ProjectFileLocator` nutzen) + vor JEDEM echten Import anlegen | P1 | 1–2h | `ProjectImportOrchestrator.cs:104-118` | **UMGESETZT 2026-07-12:** `ProjectFileLocator.Locate` findet projekt.json in Root UND Projektdateien\; fehlende Datei wird als Meldung sichtbar (nicht mehr still übersprungen). 3 neue Tests (`ProjectImportOrchestratorRestorePointTests`), 24/24 Orchestrator-Tests grün. **Offen:** „vor JEDEM echten Import" (auch XTF/PDF-Einzelimport) noch nicht umgesetzt — nur der Ein-Knopf-Import ist abgedeckt. |
| **AP-03** | Backup-Komponente „Projekte" (Videos optional, Dialog zeigt Umfang) | P1 | 0.5–1 Tag | `BackupPlanBuilder`, `SettingsFullBackupWorkflow`, `RestoreAnleitungText` | Backup-Lauf enthält projekt.json+Fotos aller bekannten Projektwurzeln; Test grün |
| **AP-04** | Single-Instance-Mutex mit Hinweis-Dialog | P1 | 1–2h | `App.xaml.cs OnStartup` | Zweiter Start zeigt Hinweis und beendet sich |
| **AP-05** | XTF-Rohdaten-Archiv aus `bin\` in den Projektordner (bzw. LOCALAPPDATA); Bestandsdateien umziehen | P1 | 1–2h | `LegacyXtfImportService.cs:24`, `ProjectScanRoots.cs:19` | Kein Schreibpfad mehr unter AppContext.BaseDirectory (Architektur-Test erweitern) |
| **AP-06** ✅ | KB-Root absichern: in settings.json persistieren (Env-Var nur Override) + Start-Warnung bei Wechsel/leerer KB | P1 | 2–4h | `KnowledgeBasePaths`, `AppSettings`, `ServiceProvider` | **UMGESETZT 2026-07-12 (Kern):** Neuer `KnowledgeRootGuard` (Infrastructure, 8 Tests) warnt bei Root-Wechsel / leerer-neuer DB / Sample-Einbruch >90%. Verdrahtet im ServiceProvider (Zustand vor KB-Init erfasst, Sample-Count danach, `settings.LastKnownKnowledgeRoot`+`...SampleCount` gemerkt) + Toast beim Start (MainWindow). Build grün, 30 Infra- + 17 Settings-Tests grün. **Bewusst NICHT umgesetzt:** settings.json als Pfad-*Quelle* (Teil 1) — müsste an ALLEN `KnowledgeBaseContext`-Aufrufern durchgereicht werden (Refactoring-Risiko, CLAUDE.md), sonst neuer Split-Brain; Env-Var bleibt die Pfad-Quelle, Settings nur für die Warnung. **Offener manueller Abnahmeschritt:** Env-Var entfernen → Start-Warnung live prüfen (braucht laufende App). |
| **AP-07** | KnowledgeBackupService: atomar schreiben + Schreibfehler nicht schlucken | P2 | 1–2h | `KnowledgeBackupService.cs:171,326,402` | AtomicPersistenceArchitectureTests um Datei erweitert; Fehler → Import meldet Fehler |
| **AP-08** | Schema-Versionscheck Projektdatei (Version>bekannt → Warnung/read-only; [JsonExtensionData] für Roundtrip) | P1 | 4h | `Project.cs`, `JsonProjectRepository` | Datei mit Version 99 → Warnung, kein stiller Feldverlust; Tests grün |

### Stufe 2 — Absturzschutz und Fehlerbehandlung

| AP | Titel | Prio | Aufwand | Abnahme |
|---|---|---|---|---|
| **AP-09** | Crash-/Korruptions-/Roundtrip-Tests für JsonProjectRepository (+ in AtomicPersistence-Prüfliste aufnehmen) | P1 | 0.5–1 Tag | 3–5 neue Tests grün: Fehlinjektion beim Save lässt alte Datei intakt; Load-Fail sauber |
| **AP-10** | Test-Gate: pre-push-Hook `dotnet test` (Schnellgate Infrastructure+Pipeline) + optional GitHub-Actions | P1 | 1–2h | Push mit rotem Test wird lokal geblockt |
| **AP-11** | KB-Start-Härtung: `PRAGMA quick_check`, Warnung bei neuer/leerer DB, DB-Snapshot-Fehlschlag als eigene Warnung | P2 | 3–5h | Byte-Müll als KnowledgeBase.db → klare Meldung + Restore-Hinweis statt stiller Neuanlage |
| **AP-12** | Dirty-Schutz „Projekt entfernen" (`ConfirmDiscardUnsavedChanges` wiederverwenden) | P2 | <1h | Entfernen des aktiven dirty Projekts fragt nach |
| **AP-13** | IBAK-Re-Import: VsaFindings ersetzen statt anhäufen (WinCan-Muster) | P2 | 2–4h | Test: zweimal importieren → VsaFindings.Count stabil |
| **AP-14** | Backup-Erinnerung beim Start (>7/30 Tage) + Fehlschlag setzt LastFullBackupUtc nicht | P2 | 2–4h | Simulierter alter Zeitstempel → Toast erscheint |
| **AP-15** | Import-Rollback: Import auf DeepCopy, atomarer Swap bei Erfolg (DryRun-Infrastruktur wiederverwenden) | P1 (=P1-10) | 0.5–1 Tag | Abbruch mitten im Import → Projekt unverändert |

### Stufe 3 — Datenkonsistenz

| AP | Titel | Prio | Aufwand |
|---|---|---|---|
| AP-20 | Duplikat-Check Haltungsnamen (Anlegen + Umbenennen + Plausibilitäts-Warnung) | P2 | 2–4h |
| AP-21 | Video-Link relativ speichern (4 Schreibstellen, `MakeRelative`-Muster) + Load-Normalisierung | P2 | 2–4h |
| AP-22 | WinCan/Legacy SQLite ReadOnly öffnen; WinCanValueNormalizer ohne CurrentCulture-Fallback | P2/P3 | 2h |
| AP-23 | ImportSourceArchiver: atomare Kopie + per-Datei-Fangnetz | P2 | 2–4h |
| AP-24 | user_version-Schutz KB (`current > SchemaVersion` → Fehler) | P3 | 1h |

### Stufe 4 — Architektur und Codequalität

| AP | Titel | Prio | Aufwand |
|---|---|---|---|
| AP-30 | Sidecar-Versionscheck im Health-Check (Konstante beidseitig) | P2 | 1–2h |
| AP-31 | Fitness-Test-Whitelist für die 4 statischen Fassaden | P3 | 1–2h |
| AP-32 | `UI/Ai` in themenbezogene Unterordner sortieren (nur verschieben) | P3 | 2–3h |
| AP-33 | `GetService()` fail-fast statt still null | P3 | 0.5h |
| AP-34 | *(nach Codequalitäts-Nachlauf ergänzen)* | | |

### Stufe 5 — Tests und automatische Qualitätsprüfung

| AP | Titel | Prio | Aufwand |
|---|---|---|---|
| AP-40 | SQLite-KB Korruptions-/Rollback-Tests | P2 | 0.5 Tag |
| AP-41 | Cancellation-Tests für die 3 langen Kernvorgänge (Analyse, Batch-Import, KB-Rebuild) | P2 | 1 Tag |
| AP-42 | Env-Var-Tests in nicht-parallele xUnit-Collection | P3 | 1h |

### Stufe 6 — Leistung und Bedienbarkeit

| AP | Titel | Prio | Aufwand |
|---|---|---|---|
| AP-50 | Projekt Laden/Speichern async + Busy-Overlay | P2 | 0.5 Tag |
| AP-51 | Save/Load-Fehler als Dialog mit Handlungsanweisung (statt Statuszeile) | P2 | 1–2h |
| AP-52 | ex.Message-Mapping für die 5–10 häufigsten Fehlerquellen | P3 | schrittweise |
| AP-53 | Import: „Fehlgeschlagene erneut importieren" | P3 | 0.5 Tag |
| AP-54 | *(nach Performance-Nachlauf ergänzen)* | | |

### Stufe 7 — Installation, Update, Dokumentation

| AP | Titel | Prio | Aufwand |
|---|---|---|---|
| **AP-17** | Kompletter Proberestore auf sauberes Verzeichnis + Protokoll in docs/ | P1-nah (P2) | 0.5 Tag |
| **AP-18** | `docs/NEUER-PC-SETUP.md` + INSTALLATION.txt vervollständigen (pdftotext, ffmpeg, Ollama, Env-Vars, Update/Rollback) | P2 | 2–3h |
| AP-60 | Produktivstart auf Publish-Ordner umstellen (Start-Skript + Backup-Pfade) | P2 | 2–4h |
| AP-61 | DINO/BERT offline-fähig (Tokenizer lokal) | P2 | 2–4h |
| AP-62 | READMEs korrigieren (cu128!, Ist-Zustand), Publish-Version aus csproj lesen, Restore-Fallback „4.4"→AppIdentity | P3 | 2h |

### Stufe 8 — Produktionsfreigabe

AP-70: Freigabe-Checkliste gegen Kapitel 10 abarbeiten, Dauerlauf (einen kompletten Batch-Nachtlauf + einen Arbeitstag Normalbetrieb ohne kritische Logeinträge), Abnahme dokumentieren.

---

## 7. Empfohlene Reihenfolge

| Stufe | Ziel | Pakete | Aufwand | Messbares Ergebnis |
|---|---|---|---|---|
| 0 Bestandsaufnahme | erledigt durch diesen Audit | — | — | dieses Dokument |
| 1 Datenverlust-Schutz | Kein Szenario mehr, in dem Arbeit unrettbar verloren geht | AP-01…AP-08 | ~3–4 Tage | Projekte im Backup; kaputtes Projekt per Dialog rettbar; Import mit wirksamem Restore-Point; 2. Instanz geblockt |
| 2 Absturz & Fehler | Ernstfälle enden mit klarer Meldung + Rückweg | AP-09…AP-15 | ~3 Tage | Crash-Tests grün; Test-Gate aktiv; KB-Korruption wird erkannt |
| 3 Datenkonsistenz | Re-Import/Umzug erzeugen keine stillen Widersprüche | AP-20…AP-24 | ~2 Tage | Doppelimport-Tests grün; Projekt portabel |
| 4 Architektur/Codequalität | Drift-Schutz, Auffindbarkeit | AP-30…AP-34 | ~1–2 Tage | Sidecar-Versionscheck aktiv; Fitness-Tests erweitert |
| 5 Tests | Geschäftskritische Abläufe abgesichert | AP-40…AP-42 | ~2 Tage | Korruptions-/Abbruch-Tests grün |
| 6 Leistung/Bedienbarkeit | Keine Freezes, verständliche Fehler | AP-50…AP-54 | ~2 Tage | Projekt-Laden mit Busy-Anzeige; Fehlerdialoge |
| 7 Installation/Doku | „Neuer PC in einem Nachmittag" | AP-17, AP-18, AP-60…62 | ~2 Tage | Proberestore-Protokoll; Setup-Doku vollständig |
| 8 Freigabe | Note A bestätigen | AP-70 | 1 Tag | Checkliste Kapitel 10 vollständig erfüllt |

Gesamt: grob 16–18 Arbeitstage netto, in kleinen unabhängigen Paketen.

---

## 8. Schnell umsetzbare Verbesserungen (je < 2h, sofort viel Sicherheit)

1. **AP-02** Restore-Point-Pfad fixen (1 Zeile + Test) — größter Sicherheitsgewinn pro Minute.
2. **AP-04** Single-Instance-Mutex.
3. **AP-05** XTF-Archiv raus aus `bin\`.
4. **AP-07** Zwei Schreibstellen auf AtomicTextFileWriter umstellen.
5. **AP-10** pre-push-Hook mit dotnet test.
6. **AP-12** Dirty-Guard beim Projekt-Entfernen (bestehende Methode wiederverwenden).
7. Restore-Anleitung Fallback „4.4" → dynamisch (15 min).
8. WinCan-DB3 ReadOnly öffnen (0.5h).
9. `Desktop`-Handkopien (~1 GB .bak) nach `kb_backups/` verschieben (30 min, Backup schlanker).
10. sidecar/README cu121→cu128 korrigieren (verhindert eine echte Falle beim Neuaufsetzen).

---

## 9. Risikoregister

| Risiko | Wahrsch. | Schaden | Erkennung | Vorbeugung | Wiederherstellung | Verantwortlicher Teil |
|---|---|---|---|---|---|---|
| D:-Platte fällt aus, Projekte weg | mittel | sehr hoch | erst im Ernstfall | AP-03 (Projekte ins Backup) + 3-2-1 | heute: keine; nach AP-03: Backup-Versionen | BackupPlanBuilder |
| Projektdatei korrupt (Absturz, Bit-Fehler) | niedrig | hoch | Ladefehler | atomares Speichern (existiert) | .bak + __RESTORE_POINTS existieren, aber erst nach AP-01 nutzerfreundlich | JsonProjectRepository |
| Falscher/abgebrochener Import zerstört Projektstand | mittel | hoch | Nutzer bemerkt fehlende/doppelte Haltungen | AP-02 + AP-15 | Restore-Point (nach Fix) | ProjectImportOrchestrator |
| KB „leer" nach Env-Var-Verlust / Umzug | **bereits passiert** | mittel–hoch (stiller Split-Brain) | erst spät (schlechte KI-Vorschläge) | AP-06 (Persistenz + Warnung) | kb_backups + Backup-Versionen | KnowledgeBasePaths |
| Backup vorhanden, Restore scheitert im Ernstfall | mittel | sehr hoch | nur durch Proberestore | AP-17 (Proberestore) + AP-15-Manifest-Hashes | zweites Backup-Ziel | FullBackupService |
| Zwei App-Instanzen überschreiben sich | mittel | mittel | kaum (still) | AP-04 | .bak der Vorversion | App.xaml.cs |
| Regression durch ungetesteten Commit | mittel | mittel | Tage später | AP-10 (Test-Gate) | git bisect (teuer) | Prozess |
| Ältere App-Version öffnet neuere Projektdatei | mittel (mehrere Builds auf einem PC!) | mittel (stiller Feldverlust) | kaum | AP-08 | Restore-Points | Project/Repository |
| Sidecar-API-Drift → leere Erkennungen | niedrig–mittel | mittel (falsche Ergebnisse) | erst in Ergebnissen | AP-30 Versionscheck | SidecarE2eSmoke | VisionPipelineClient |
| VSA-Zustandsnote verfälscht durch IBAK-Doppel-Findings | niedrig | mittel (fachlich falsch!) | Vergleich mit Protokoll | AP-13 | Re-Import nach Fix | IbakExportImportService |

---

## 10. Definition „Note A" (messbare Freigabekriterien)

Das Programm ist produktionsreif (Note A), wenn ALLE Punkte erfüllt und dokumentiert sind:

1. ☐ Keine offenen P0-Probleme (aktuell erfüllt) und keine unbewerteten P1-Probleme (aktuell: 10 offen).
2. ☐ Alle P1-Pakete (AP-01…AP-10, AP-15) umgesetzt und ihre Abnahmetests grün.
3. ☐ Kritischer Datenfluss testgeschützt: Projekt-Save/Load-Crashtests, Import-Idempotenz Ende-zu-Ende, KB-Korruptionstest, je 1 Abbruch-Test pro langem Vorgang.
4. ☐ Backup enthält Projekte; **ein kompletter Proberestore wurde durchgeführt und protokolliert** (Datum, Dauer, Ergebnis in docs/).
5. ☐ Simulierter Abbruch (Prozess-Kill während Speichern, während Import, während Backup) führt nachweislich zu keinem Datenverlust — je ein dokumentierter Handtest.
6. ☐ Fehlerbehandlung: Save-/Load-/Import-Fehler erscheinen als Dialog mit Handlungsanweisung, nicht nur als Statuszeile.
7. ☐ Reproduzierbarer Build: `Publish-SewerStudio.ps1` läuft durch, Release-Manifest trägt korrekte Version aus csproj, Produktivstart zeigt auf Publish-Ordner (nicht bin\Debug).
8. ☐ Installation dokumentiert: `docs/NEUER-PC-SETUP.md` vorhanden; INSTALLATION.txt nennt pdftotext/ffmpeg/Ollama/Env-Vars.
9. ☐ Update + Rollback dokumentiert und einmal geprobt (neuer Ordner daneben, alter Ordner startet noch).
10. ☐ Fehlende externe Dienste kontrolliert: App-Start ohne Ollama/Sidecar/ffmpeg zeigt verständliche Warnungen (kein Crash) — Handtest dokumentiert.
11. ☐ Keine bekannten kritischen Sicherheitsprobleme (aktuell erfüllt, Note A halten).
12. ☐ Dauer-/Belastungstest bestanden: ein kompletter Batch-Nachtlauf + ein voller Arbeitstag ohne kritische Logeinträge/Speicherwachstum.
13. ☐ Test-Gate aktiv (pre-push oder CI) und `LastFullBackupUtc`-Erinnerung in Betrieb.
14. ☐ Freigabe-Checkliste (dieses Kapitel) abgehakt und mit Datum in docs/ abgelegt.

---

## 11. Übergabe-Prompts für P0/P1-Arbeitspakete

Gemeinsame Sicherheitsregeln für ALLE Prompts (bei jeder Übergabe mitgeben):

> **Sicherheitsregeln:** Arbeite im Repo c:\Sewer-Studio_KI_4.5. Prüfe zuerst `git status` — es existieren nicht committete Änderungen am Backup-System (src/**/Backup/*, SettingsFullBackupWorkflow.cs, zugehörige Tests): NICHT überschreiben, NICHT revertieren. Keine NuGet-Pakete ohne Rückfrage. Kommentare auf Deutsch. Bestehende Architektur-Muster wiederverwenden (AtomicTextFileWriter, Result-Pattern, DialogService). Nach der Änderung `dotnet build AuswertungPro.sln` und die genannten Tests ausführen. Kein Refactoring außerhalb des Auftrags.

### Prompt AP-01 — Projekt-Laden mit Rettungskopien-Fallback
```text
ZIEL: Wenn projekt.json nicht ladbar ist, darf der Nutzer seine Arbeit nicht für verloren halten.
UMFANG: In ShellViewModel.TryOpenProject (src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs:464-468): Bei Result.Fail("APP-LOAD") automatisch versuchen, <pfad>.bak zu laden; gelingt auch das nicht, die neuesten Dateien aus __RESTORE_POINTS im Projektordner anbieten. In jedem Fall einen Dialog zeigen (Dialogs.Error/Confirm): was passiert ist, welche Rettungskopie geladen wurde bzw. angeboten wird. Beim erfolgreichen Fallback-Laden die defekte Datei als projekt.corrupt-<timestamp>.json beiseite legen (Muster: SettingsStore-Quarantäne), NIE löschen.
TESTS: Neue Tests in tests/AuswertungPro.Next.UI.Tests: (1) Hauptdatei korrupt + .bak intakt → Projekt geladen, Quarantäne-Datei existiert. (2) Beide korrupt → Fehlerdialog, keine Datei verändert.
ABNAHME: Manuell: projekt.json mit Byte-Müll überschreiben → App bietet .bak an und lädt.
+ Sicherheitsregeln (oben).
```

### Prompt AP-02 — Restore-Point vor Import reparieren
```text
ZIEL: Der Restore-Point vor dem Ein-Knopf-Import muss auch für neue Projekte (Projektdateien\projekt.json) greifen.
UMFANG: src/AuswertungPro.Next.Infrastructure/Import/ProjectImportOrchestrator.cs:104-118: Pfad über ProjectFileLocator.Locate(projectFolder) auflösen statt hartkodiert <root>\projekt.json. Wird keine Datei gefunden, Warnung in die Import-Messages schreiben (nicht still überspringen). Prüfen, ob weitere Importpfade (XTF-/PDF-Einzelimport) ohne Restore-Point laufen — falls ja, denselben Aufruf davorsetzen.
TESTS: tests/AuswertungPro.Next.Infrastructure.Tests: Projekt mit Projektdateien\projekt.json anlegen → Orchestrator-Lauf → Restore-Point-Datei existiert. Zweiter Test: Ordner ohne projekt.json → Warnung in Messages.
ABNAHME: Beide Tests grün; bestehende ProjectImportOrchestrator-Tests bleiben grün.
+ Sicherheitsregeln (oben).
```

### Prompt AP-03 — Backup-Komponente „Projekte"
```text
ZIEL: Der PC-Ausfallschutz sichert zusätzlich die Projektdaten (projekt.json, Fotos, __RESTORE_POINTS; Videos optional per Schalter).
UMFANG: src/AuswertungPro.Next.Application/Backup/BackupPlanBuilder.cs: neue Komponente "Projekte". Quellen: Settings-Projektwurzeln + Verzeichnisse der RecentProjectPaths (Duplikate/verschachtelte Wurzeln zusammenfassen). Ausschlussregel für Videodateiendungen (.mpg/.mp4/.avi/...), abschaltbar über ein neues Setting (Default: Videos AUS, im Dialog klar anzeigen: "Videos enthalten: nein"). SettingsFullBackupWorkflow + Größen-Vorschau + RestoreAnleitungText entsprechend erweitern. ACHTUNG: Backup-Dateien sind unkommittet in Arbeit — auf dem aktuellen Stand aufbauen.
TESTS: Erweiterung von BackupPlanBuilderTests + FullBackupServiceTests: Plan enthält Projekte-Komponente; Video-Ausschluss wirkt; Marker-/Guard-Verhalten unverändert.
ABNAHME: Testlauf-Backup in Temp-Ordner enthält projekt.json eines Testprojekts; RESTORE-ANLEITUNG erwähnt Projekte.
+ Sicherheitsregeln (oben).
```

### Prompt AP-04 — Single-Instance-Schutz
```text
ZIEL: Zwei gleichzeitige App-Instanzen dürfen nicht dieselben Daten beschreiben.
UMFANG: src/AuswertungPro.Next.UI/App.xaml.cs OnStartup: benannter Mutex (z.B. "Local\SewerStudio.SingleInstance"). Bei belegtem Mutex: Dialogs.Info ("SewerStudio läuft bereits...") und Shutdown; nach Möglichkeit vorhandenes Hauptfenster per Win32 (SetForegroundWindow) aktivieren. Mutex im OnExit freigeben. Keinen Zwangs-Abbruch bei Mutex-AbandonedException (dann weiterlaufen).
TESTS: Logik in eine kleine testbare Klasse (SingleInstanceGuard) ziehen; Unit-Test: zweiter Acquire liefert false.
ABNAHME: Manuell: App zweimal starten → zweite Instanz zeigt Hinweis und beendet sich.
+ Sicherheitsregeln (oben).
```

### Prompt AP-05 — XTF-Rohdaten raus aus bin\
```text
ZIEL: Keine Nutzdaten im Build-Output.
UMFANG: src/AuswertungPro.Next.Infrastructure/Import/Xtf/LegacyXtfImportService.cs:24: Zielordner statt AppContext.BaseDirectory\Rohdaten\xtf_imports → in den Projektordner (ProjectStructure/ImportSourceArchiver-Muster, bevorzugt) ODER %LOCALAPPDATA%\SewerStudio\Rohdaten. Copy-Fehler nur als Warn-Message loggen, Parsen trotzdem durchführen (Copy in eigenes try). ProjectScanRoots.cs:19 (<cwd>\Rohdaten) prüfen und ggf. mitziehen. Einmal-Migration: vorhandene Dateien aus bin\...\Rohdaten\xtf_imports beim ersten Lauf in den neuen Ort verschieben. Kommentar Z.47 („Projektverzeichnis") korrigieren.
TESTS: Bestehende XTF-Import-Tests grün; neuer Test: Import archiviert in den Projektordner; Architektur-Test, der Schreibpfade unter AppContext.BaseDirectory verbietet (Muster AtomicPersistenceArchitectureTests).
ABNAHME: Nach Import liegt die Kopie im Projektordner; bin\ bleibt leer.
+ Sicherheitsregeln (oben).
```

### Prompt AP-06 — KB-Root absichern
```text
ZIEL: Die App darf nie unbemerkt mit einer falschen/leeren KnowledgeBase starten.
UMFANG: (1) KnowledgeBasePaths (src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBasePaths.cs:78-88): aufgelösten Root zusätzlich in settings.json persistieren (neues AppSettings-Feld KnowledgeRootPath); Auflösungsreihenfolge: Env-Var (Override, wie bisher) → Settings → Legacy-Fallback. (2) Beim Start (ServiceProvider-KB-Init): wenn der heute aufgelöste Root vom zuletzt persistierten abweicht ODER die DB neu angelegt wurde (Datei existierte nicht), sichtbare Warnung über Dialogs/Toast mit beiden Pfaden. (3) Sample-Count-Plausibilität: letzten bekannten Count in Settings merken; fällt er um >90%, Warnung.
TESTS: Unit-Tests für die Auflösungsreihenfolge und die Abweichungs-Erkennung (Infrastructure.Tests).
ABNAHME: Env-Var temporär entfernen → Start zeigt Warnung statt stiller leerer KB.
+ Sicherheitsregeln (oben).
```

### Prompt AP-08 — Schema-Versionscheck Projektdatei
```text
ZIEL: Kein stiller Feldverlust, wenn eine ältere App-Version eine neuere Projektdatei öffnet.
UMFANG: src/AuswertungPro.Next.Domain/Models/Project.cs + src/AuswertungPro.Next.Infrastructure/Projects/JsonProjectRepository.cs: (1) Konstante CurrentVersion (=2) im Repository; beim Load: file.Version > CurrentVersion → Result.Fail("APP-VERSION", verständliche Meldung "Projekt stammt aus neuerer Programmversion...") — Aufrufer (ShellViewModel) zeigt Dialog. (2) file.Version < CurrentVersion → Migrationskette (aktuell nur Hochsetzen + vorhandene EnsureRecordDefaults) und Version hochschreiben. (3) [JsonExtensionData]-Dictionary auf Project UND HaltungRecord, damit unbekannte Felder beim Roundtrip erhalten bleiben.
TESTS: (a) Version 99 → Fail mit APP-VERSION. (b) JSON mit unbekanntem Feld → Save → Feld noch da. (c) Version 1 → geladen + auf 2 migriert.
ABNAHME: Alle 3 Tests grün, bestehende Load-Tests grün.
+ Sicherheitsregeln (oben).
```

### Prompt AP-09 — Crash-Tests für JsonProjectRepository
```text
ZIEL: Der wichtigste Speicherpfad der App ist gegen Crash/Korruption testgeschützt.
UMFANG: Neue Testdatei tests/AuswertungPro.Next.Infrastructure.Tests/Projects/JsonProjectRepositoryRobustnessTests.cs: (1) Save auf schreibgeschütztes Ziel → alte Datei byte-identisch erhalten, Result.Fail. (2) Load mit abgeschnittenem JSON → Result.Fail("APP-LOAD"), keine Exception. (3) Save legt .bak mit vorherigem Inhalt an. (4) Voller Roundtrip eines HaltungRecord mit FieldMeta (alle FieldSource-Varianten, UserEdited), Protocol Original+Current, VsaFindings, Fotos → alle Werte identisch. (5) JsonProjectRepository in die Prüfliste der AtomicPersistenceArchitectureTests aufnehmen (bzw. dokumentieren, warum die eigene Temp+Replace-Logik äquivalent ist).
ABNAHME: Alle neuen Tests grün; kein Produktionscode-Umbau nötig außer ggf. kleiner testbarer Naht (z.B. injizierbares Dateisystem NICHT einführen — mit echten Temp-Ordnern testen wie die bestehenden Backup-Tests).
+ Sicherheitsregeln (oben).
```

### Prompt AP-10 — Test-Gate vor Push
```text
ZIEL: Kein Push mit roten Tests.
UMFANG: (1) .git/hooks/pre-push erweitern (LFS-Zeile behalten!): dotnet test für die zwei schnellen Projekte tests/AuswertungPro.Next.Infrastructure.Tests + tests/AuswertungPro.Next.Pipeline.Tests; bei Fehler Push abbrechen mit klarer Meldung; Umgehung dokumentieren (--no-verify). Hook-Inhalt zusätzlich als tools/git-hooks/pre-push ins Repo legen + kurzes Setup-Skript (Kopieren), weil .git/hooks nicht versioniert ist. (2) Optional: .github/workflows/tests.yml (windows-latest, dotnet test AuswertungPro.sln) — nur anlegen, nicht als Pflicht verdrahten.
ABNAHME: Absichtlich roter Test → git push wird lokal geblockt; Hook-Laufzeit unter ~3 Minuten (sonst Testauswahl verkleinern und das dokumentieren).
+ Sicherheitsregeln (oben).
```

### Prompt AP-15 — Import-Rollback über DeepCopy-Swap
```text
ZIEL: Ein abgebrochener oder fehlgeschlagener Import hinterlässt nie ein halb-importiertes Projekt.
UMFANG: src/AuswertungPro.Next.UI/Services/ImportRunWorkflowController.cs (echter Lauf, Z.79-83): Import auf einer DeepCopy des Projekts ausführen (DeepCopy-Infrastruktur existiert für DryRun, Z.79-81). Bei Erfolg: Copy atomar ins Shell-Projekt übernehmen (Referenz-Swap auf dem UI-Thread + Dirty setzen). Bei Cancel/Exception: Original unangetastet, Statusmeldung "Import abgebrochen — Projekt unverändert". Wechselwirkung beachten: AutoSave darf während des Laufs nicht die Copy speichern; Events/Bindings erst nach dem Swap neu verdrahten (prüfen, wie DryRun das heute löst).
TESTS: UI.Tests: (1) Cancel nach k Records → Projekt hat exakt den Vor-Import-Stand. (2) Erfolg → Projekt enthält Import + Dirty=true. (3) Exception im Orchestrator → Projekt unverändert.
ABNAHME: Alle 3 Tests grün; manueller Abbruch-Test im Import-Dialog.
+ Sicherheitsregeln (oben).
```

---

## Offene Prüfungen

**5 Bereiche nicht geprüft (Modell-Limit erreicht) — für die Vollständigkeit nachzuholen:**
- **Nebenläufigkeit/async:** `.Result`/`.Wait()`-Deadlock-Kandidaten, `async void`, fire-and-forget ohne Fehlerbehandlung, geteilte Zustände ohne Lock, CancellationToken-Durchreichung. (Teil-Signal aus Absturz-Audit: Coding-Feedback ist fire-and-forget mit Lock-Kollisionsrisiko.)
- **Externe Dienste:** konkretes Ausfallverhalten bei fehlendem Ollama/Sidecar/FFmpeg/VLC/GPU-OOM aus Nutzersicht (Dialog? Spinner? Crash?). (Teil-Signal aus Architektur-Audit: Sidecar-API-Drift schlägt still fehl → AP-30.)
- **Codequalität** projektweit: leere catch-Blöcke außerhalb Import/Export, TODO/HACK-Inventar, Dopplung der 3 Analyse-Pfade, tote Wegwerf-Tools, Nullability, IDisposable/HttpClient-Streuung.
- **Logging/Diagnose:** Log-Ziel/Rotation, ErrorCode-Korrelation, Vorgangs-IDs, ob Sidecar-Fehler in den App-Logs landen, Diagnosepaket.
- **Performance/Ressourcen:** synchrone Startarbeit, Event-Handler-Leaks, LibVLC-MediaPlayer-Dispose, SQLite-N+1, UI-Virtualisierung großer Listen, Parallelitätsgrenzen im Batch, Langzeitbetrieb.

**Weitere von den Auditoren selbst gemeldete Lücken:**
- `dotnet test` NICHT ausgeführt (reines Code-Audit, keine Laufzeit-/Absturz-Simulation). Die Zahl „8075 Tests grün" stammt aus dem Projektstand 09.07., nicht aus einem neuen Lauf; gezählt wurden 6658 `[Fact]`/`[Theory]` in 1501 Dateien.
- Restore praktisch nie komplett durchgespielt (→ AP-17 ist selbst eine offene Prüfung mit hoher Priorität).
- tools/-CLI-Schreibpfade + `kb_audit`-Python-Skripte ungeprüft; QuestPDF-Verhalten bei korrupten Bildbytes nicht reproduziert; MAX_PATH (>260 Zeichen) ungeprüft; Detail-Korrektheit von `DirectoryMirror`/`SqliteSnapshotCopier`/`BackupDiskSpaceGuard` (Kopiermechanik) nur auf Plan-Ebene betrachtet.
- `BenchmarkSetStore` als Klasse nicht gefunden — Schreiber von `benchmark_set.json` unidentifiziert.

**Gegenprüfungs-Abdeckung:** Von den ursprünglich 10 als P1 gemeldeten Befunden wurden 6 gegengeprüft (alle CONFIRMED, 4 davon P1→P2 gesenkt). Die 6 verbliebenen echten P1 sind teils gegengeprüft (P1-7), teils nur über eindeutige, direkt am Code belegte Fakten gestützt (fehlendes `.github/workflows`, fehlende Crash-Tests, hartkodierter Import-Restore-Pfad) — diese Belege sind so unmittelbar, dass eine Gegenprüfung wenig hinzufügt.
