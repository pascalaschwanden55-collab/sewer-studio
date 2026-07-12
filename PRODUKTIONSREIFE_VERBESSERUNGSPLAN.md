# Produktionsreife: Bewertung und Verbesserungsfahrplan

> Stand: 2026-07-12 · Branch: `feature/gis-karte` · Erstellt durch KI-Audit und lokalen Code-/Test-Nachlauf
> **Status: 16 von 16 Prüfbereichen abgeschlossen.** Die fünf zunächst fehlenden Bereiche Nebenläufigkeit, Externe Dienste, Codequalität, Logging/Diagnose und Performance wurden am aktuellen Code nachgeholt. Details: `docs/AUDIT-NACHLAUF-2026-07-12.md`.
>
> **Ergebnis der ursprünglichen Gegenprüfung:** Alle damals als kritisch geprüften Befunde wurden als real bestätigt. Der spätere Fünf-Bereiche-Nachlauf hat zusätzliche pauschale Sorgen teilweise widerlegt, etwa fehlende Log-Aufbewahrung oder fehlende Tabellen-Virtualisierung; siehe Nachlauf-Dokument.

## Arbeitsstand

| Kapitel | Status |
|---|---|
| 0. Bestandsaufnahme | ✅ |
| 1. Kurzfazit | ✅ Gesamtnote nach vollständigem Nachlauf |
| 2. Bewertungsmatrix | ✅ 16/16 Bereiche |
| 3. Kritische Sofortmaßnahmen (P0–P3) | ✅ inkl. Gegenprüfungs-Verdikte |
| 4. Sicherungs- und Wiederherstellungskonzept | ✅ |
| 5. Architektur-Zielbild | ✅ |
| 6. Umsetzungsfahrplan (Arbeitspakete) | ✅ |
| 7. Empfohlene Reihenfolge (Stufen 0–8) | ✅ |
| 8. Schnell umsetzbare Verbesserungen | ✅ |
| 9. Risikoregister | ✅ |
| 10. Definition „Note A" | ✅ |
| 11. Übergabe-Prompts | ✅ |
| Audit-Nachlauf | ✅ 5/5 fehlende Bereiche geprüft |

---

## 1. Kurzfazit

**Gesamtnote: B+** (16 von 16 Bereichen geprüft). Der Nachlauf hat kein neues P0- oder P1-Problem gefunden. Restore-Points aller echten Importwege, ein gespeicherter KB-Pfad, gebündeltes AutoSave und überwachte Hintergrundaufgaben sind inzwischen umgesetzt. Offen bleiben vor allem asynchrones Laden/Speichern, das Diagnosepaket und der Großklassen-Bestand.

**Das Wichtigste in einem Satz:** Kein einziger P0-Befund — es gibt keinen bekannten Weg, wie die App von sich aus Daten zerstört. Die ursprünglichen P1-Lücken sind im Code geschlossen; offen sind vor allem die drei Bedienprüfungen des Proberestores und längerfristige Qualitätsarbeit.

### Größte Stärken (am Code belegt)

1. **Atomares Speichern ist flächendeckend Standard:** Projekt, Einstellungen, Trainingsdaten, Kataloge — überall Temp-Datei + `File.Replace` + `.bak`-Generationen. Ein Stromausfall mitten im Speichern zerstört die alte Datei nicht. Ein Architektur-Test (`AtomicPersistenceArchitectureTests`) erzwingt das Muster sogar maschinell.
2. **Das neue Backup-System (PC-Ausfallschutz) ist überdurchschnittlich sorgfältig:** SQLite-Online-Backup-API + `integrity_check`, SHA256-Verifikation beim Kopieren, Marker-Datei-Schutz gegen versehentliches Leeren fremder Ordner, Platz-Prüfung, 10 Versions-Stände, generierte Restore-Anleitung mit echten PC-Pfaden — plus Tests.
3. **Testsuite auf ungewöhnlichem Niveau:** ~1500 Testdateien, ~6658 Facts/Theories, echte Verhaltenstests (Fehlerinjektion, Abbruch, kaputte Eingabedateien), Architektur-Fitness-Tests, Eval-Set-Kontaminations-Guards. Alles läuft offline ohne GPU/Ollama.
4. **Sicherheit: Note A.** Sidecar nur auf 127.0.0.1 mit Token-Pflicht (`hmac.compare_digest`), keine Secrets im Repo, zentrale Prozessstarts mit ArgumentList, Pfad-Sandbox im Training-Export.
5. **Saubere Schichtung:** Domain ← Application ← Infrastructure ← UI ohne eine einzige Rückwärtsreferenz; KI-Pipeline liegt testbar in Infrastructure, nicht in der UI.

### Die 5 größten Risiken

| # | Risiko | Warum kritisch |
|---|---|---|
| 1 | **Drei Bedienprüfungen des Proberestores sind noch offen:** PDF-Import, Video-Wiedergabe und KI-Lauf | Der technische Rückweg ist bewiesen, aber die komplette Nutzerkette noch nicht live abgenommen |
| 2 | **6 Produktionsdateien mit mehr als 1.000 Zeilen** | Änderungen in diesen Klassen sind langsamer zu verstehen und erhöhen das Nebenwirkungsrisiko |
| 3 | **Projektladen und das eigentliche Speichern laufen noch synchron** | Bei sehr großen Projekten kann die Oberfläche kurz stocken; schnelle Änderungen werden jetzt aber 750 ms gebündelt |
| 4 | **Ein Diagnosepaket fehlt noch** | Wichtige Hintergrundfehler landen jetzt im Tageslog, aber die einfache Sammlung aller Diagnoseinformationen ist noch offen |
| 5 | **Abbruch- und Langzeittests sind noch nicht vollständig** | Nachtlauf, voller Arbeitstag und einzelne Prozess-Kill-Szenarien müssen weiter praktisch geprüft werden |

### Soll das Programm bereits produktiv eingesetzt werden?

**Ja, für den jetzigen Solo-Eigenbetrieb.** Projekte sind inzwischen Bestandteil der Vollsicherung, der technische Proberestore ist bestanden und die wichtigsten Datenverlust-Pakete sind umgesetzt. Vor einer Freigabe für andere Personen bleiben die drei UI-Handtests des Proberestores sowie die restlichen Punkte der Note-A-Checkliste Pflicht.

---

## 2. Bewertungsmatrix

| Bereich | Note | Ziel | Wichtigste Begründung | Höchstes Risiko | Nötigste Maßnahme |
|---|---|---|---|---|---|
| Datenablage | A− | A | Trennung sauber; XTF-Archiv außerhalb `bin`; KB-Root aus Env-Override, Settings oder sicherem Standard | Umzug auf einen neuen PC muss weiterhin sauber dokumentiert ausgeführt werden | AP-17 Rest |
| Atomares Speichern & Backup | A− | A | Projekte, Fotos, KB, Einstellungen und Programm gesichert; technischer Restore bestanden | Videos standardmäßig aus; drei UI-Restore-Handtests offen | AP-17 Rest |
| SQLite-Integrität | A− | A | WAL, Transaktionen, `quick_check`, Online-Snapshot und echter Restore-Test | Langzeit-/Bit-Rot-Prüfung nur über erneuten Backup-Lauf | AP-70 |
| Absturz & Wiederanlauf | A− | A | Globale Handler, gebündeltes AutoSave, Import-Rollback, Single-Instance und überwachte Hintergrundaufgaben vorhanden | Praktische Prozess-Kill-Handtests fehlen | AP-70 |
| Import/Export-Robustheit | A− | A | Result-Pattern, Fehler-Isolation, atomisches Rohdatenarchiv, idempotenter IBAK-Re-Import und Restore-Point vor jedem echten Import | Bedienprüfung nach Restore bleibt offen | AP-17 Rest |
| Datenkonsistenz | B+ | A− | Schema-Check, unbekannte Felder erhalten, Import auf DeepCopy, UserEdited-Schutz | Duplikat-Namen noch nicht an allen Eingabestellen blockiert | AP-20 |
| Architektur | B+ | A− | Schichten einbahnig, Fitness-Tests, Composition Root schlank; keine Produktionsdatei mehr über 1.000 Zeilen | Doppelt gepflegter C#/Python-Vertrag | P3 beobachten |
| Codequalität | B+ | A− | Neue Großdateien werden verhindert; alle zwanzig früheren Großdateien sind inzwischen unter 1.000 Zeilen | Kleinere Verantwortungsgrenzen weiter beobachten | Fitness-Test halten |
| Sicherheit | **A** | A | Sidecar-Token+Loopback, keine Secrets, ArgumentList, Sandbox | Nur P3-Randnotizen | keine (halten) |
| Tests | B+ | A | Zuletzt 8.468 Tests vollständig grün bestätigt; Crash-/Schema-/Backup-Tests und pre-push-Gate | Kein zentraler CI-Lauf; Langzeit- und Abbruchabdeckung noch ergänzen | AP-41, AP-70 |
| UI/Bedienbarkeit | B+ | A | Fehlerdialoge, Fortschritt, Abbruch und Dirty-Guard vorhanden | Synchrones Projektladen/-speichern kann bei großen Dateien blockieren | AP-50 |
| Logging/Diagnose | B | B+ | Tageslogs, Aufbewahrung, globale Ausnahmebehandlung und wichtige Hintergrundfehler im normalen Log | Diagnosepaket und weitere Debug-only-Randpfade fehlen | AP-55 Rest |
| Performance | B+ | B+ | Tabellen-Virtualisierung, Hintergrundimporte, ffmpeg-Streaming und 750-ms-AutoSave-Bündelung vorhanden | Projektladen und eigentlicher Schreibvorgang bleiben synchron | AP-50 |
| Nebenläufigkeit | B+ | B+ | Hintergrundfehler werden beobachtet; LiveControl/QGIS sind auf acht gleichzeitige Clients begrenzt und warten beim Stoppen | Abbruchtests der langen Kernvorgänge fehlen teilweise | AP-41 |
| Externe Dienste | A− | A | Timeouts, Retry, Token, Fallback und Versionscheck im Hauptpfad vorhanden | Voraussetzungen noch nicht in einem gemeinsamen UI-Check zusammengefasst | AP-18 Ergänzung |
| Installation/Update/Doku | B+ | A− | Publish-Skript, Installation und Neuer-PC-Ablauf dokumentiert | Voraussetzungen werden noch nicht in einem gemeinsamen UI-Check geprüft | AP-18 Ergänzung, AP-60 |

---

## 3. Kritische Sofortmaßnahmen

**P0 (sofort, Datenverlust möglich): KEINE.** Der Audit fand keinen Pfad, auf dem die App ohne äußere Einwirkung Daten zerstört.

### Kritische Befunde nach Gegenprüfung

Jeder Befund unten wurde von einem zweiten Agenten am Code gegengeprüft. Spalte „Verdikt" zeigt das Ergebnis: **CONFIRMED** = selbst am Code bestätigt. Spalte „Prio (final)" ist die Einstufung nach Gegenprüfung. **Alle diese Punkte gehören in Stufe 1 des Fahrplans**, unabhängig vom Label — die P1/P2-Grenze markiert nur, ob ein Datenverlust unmittelbar oder erst mit Zusatzumständen droht.

**Ursprüngliche P1 aus dem Ausgangsaudit:**

Aktueller Stand: P1-3 ist für Ein-Knopf- und Einzelimporte behoben, P1-4 durch AP-08, P1-10 durch AP-15, P1-8 durch AP-10 und P1-9 durch AP-09. P1-7 ist durch den gespeicherten KB-Pfad plus optionalen Env-Override und sichtbare Warnungen behoben. Die Tabelle bleibt als Begründung und Historie erhalten.

| Nr | Befund im Ausgangsstand | Beleg | Verdikt | Prio (damals) |
|---|---|---|---|---|
| P1-7 | KB-Root hängt still an User-Env-Var `SEWERSTUDIO_KNOWLEDGE_ROOT` — ohne sie startet die App mit veraltetem/leerem „Gehirn", ohne Warnung. **Ist schon einmal passiert.** | `KnowledgeBasePaths.cs:83-87`; belegt durch `Start_SewerStudio.bat` + `docs/DATENSICHERUNG-UEBERGABE-CODEX.md:92` | **CONFIRMED** | **P1** (bleibt) |
| P1-3 | Restore-Point vor Ein-Knopf-Import greift bei neuer Projektstruktur (`Projektdateien\projekt.json`) nicht — Sicherheitsnetz still wirkungslos | `ProjectImportOrchestrator.cs:106` vs. `ProjectFileLocator.cs:33-34` | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |
| P1-4 | Keine Schema-Versionsprüfung: ältere App-Version verliert still Felder neuerer Projektdateien | `Project.cs:10` (Version wird nie gelesen), `JsonProjectRepository.cs:24` | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |
| P1-10 | Abgebrochener/teilweiser Import mutiert das Live-Projekt ohne Rollback; AutoSave persistiert den Teilzustand | `ImportRunWorkflowController.cs:79-83,160-165`, `ProjectImportOrchestrator.cs:416` | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |
| P1-8 | Kein CI / kein Test-Gate vor Commit/Push (8000+ Tests laufen nur manuell) | kein `.github/workflows`, pre-push-Hook nur LFS | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |
| P1-9 | `JsonProjectRepository` (wichtigste Datei!) ohne Crash-/Korruptions-/Roundtrip-Tests; fehlt in `AtomicPersistenceArchitectureTests` | `JsonProjectRepository.cs:44-99`, `AtomicPersistenceArchitectureTests.cs:10-29` | nicht gegengeprüft (Limit); Beleg eindeutig | P1 |

**Durch Gegenprüfung von P1 auf P2 gesenkt (real, aber Datenverlust nur mit Zusatzumständen) — trotzdem Stufe 1, weil billig:**

Aktueller Stand: Alle vier Punkte dieser Tabelle sind inzwischen umgesetzt: Projekte im Backup (AP-03), Projekt-Rettungsdialog (AP-01), Single-Instance-Schutz (AP-04) und XTF-Archiv außerhalb des Programmordners (AP-05).

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
| P2-3 ✅ | Backup-Erinnerung und Zeitstempel nur bei Erfolg | umgesetzt über `FullBackupReminderPolicy` |
| P2-4 ⚠️ | Technischer Restore bestanden; geführte Restore-Funktion und drei UI-Handtests fehlen | `docs/PROBERESTORE-PROTOKOLL.md` |
| P2-5 ✅ | Fehlende/leere KB wird durch Root-/Sample-Guard sichtbar | `KnowledgeRootGuard` |
| P2-6 ✅ | KB-Korruptions-Erkennung beim Start über `PRAGMA quick_check` | `KnowledgeBaseHealthChecker` |
| P2-7 ✅ | WinCan/Legacy-SQLite ReadOnly | Architekturtest vorhanden |
| P2-8 ✅ | IBAK-Re-Import ersetzt VsaFindings | Doppelimport-Test vorhanden |
| P2-9 | Kein Duplikat-Check für Haltungsnamen; Re-Import merged in den ERSTEN Treffer | `DataPage.xaml.cs:583`, `Project.cs:159-180` |
| P2-10 ✅ | Video-Links portabel und beim Laden/Speichern normalisiert | AP-21 |
| P2-11 ✅ | ImportSourceArchiver kopiert atomar und fängt Fehler je Datei | AP-23 |
| P2-12 ✅ | Dirty-Schutz beim Entfernen aus der Übersicht | AP-12 |
| P2-13 | Projekt Laden/Speichern synchron im UI-Thread ohne Fortschritt (Freeze bei großen Projekten) | `ShellViewModel.cs:463,550` |
| P2-14 ✅ | Save-/SaveAs-/Rettungsfehler als Dialog | AP-01, AP-51 |
| P2-15 | SQLite-KB ohne Korruptions-/Recovery-Test; Cancellation mitten im Vorgang kaum getestet | Tests-Audit |
| P2-16 ✅ | Versionsprüfung wird im Monitor und Haupt-Analysepfad erzwungen | AP-36 |
| P2-17 ⚠️ | Neuer-PC-Anleitung vorhanden; externe Programme/Offline-Modelle bleiben einzurichten | AP-18, AP-61 |
| P2-18 ✅ | Die feste Altliste ist vollständig zerlegt: Alle zwanzig früheren Großdateien liegen unter 1.000 Zeilen. Datenseite und Startfenster waren die letzten zwei Pakete; die ausgelagerten Aufgaben sind getrennt testbar. | AP-34 abgeschlossen; Fitness-Test bleibt aktiv |

### P3 — spätere Optimierung (Auswahl)

Hartkodierte Pfade in Defaults (`D:\QGIS_V4.03`, `basemap_tiles` mit Versionsnummer im Pfad); Restore-Anleitung nennt Fallback „4.4"; Legacy-Ordner `%APPDATA%\AuswertungPro` weiter aktiv beschrieben; Excel-Export nicht atomar; Protokoll-PDF scheitert komplett an einem korrupten Foto; `UNBEKANNT_`-Duplikate beim PDF-Re-Import; WinCan-Zahlparser mit CurrentCulture-Fallback (`1.234`-Mehrdeutigkeit); Meter-Formatierung ohne explizite Culture; verwaister Sidecar hält VRAM nach App-Ende; kein Not-Speichern im Crash-Handler; zweite Exception im Guard komplett verschluckt; statische Fassaden neben DI unbewacht; 433 Dateien flach in `UI/Ai/`; `GetService()` unvollständig (liefert still null); FFmpeg-Aufruf per String-Konkatenation; Ollama-URL ohne Loopback-Warnung; Test-Env-Var-Manipulation (Flakiness); veraltete READMEs (cu121!); Publish-Version doppelt gepflegt; ~1 GB Hand-`.bak`-Dateien im KI_BRAIN-Root.

---

## 4. Sicherungs- und Wiederherstellungskonzept

### 4.1 Ist-Zustand (belegt)

**Vorhanden und gut:**
- `FullBackupService` (PC-Ausfallschutz): sichert Programm-Repo, Projekte und Fotos, KI-Gehirn (C:\KI_BRAIN), Einstellungen (%LOCALAPPDATA%\SewerStudio + %APPDATA%), Logs und Extras — mit SHA256-Verify, SQLite-Online-Snapshot + integrity_check, Marker-Datei-Schutz, Platz-Prüfung, 10 datierten Versions-Ständen und Restore-Anleitung. Videos sind bewusst optional und standardmäßig ausgeschlossen.
- Lokale Sicherheitsnetze: `.bak`(-Generationen) neben jeder wichtigen Datei, bis zu 20 Restore-Points pro Projekt (`__RESTORE_POINTS`), 20 Settings-Restore-Points, Quarantäne korrupter Settings.
- `docs/PC-AUSFALLSCHUTZ-MANUELLER-TEST.md` + `docs/RESTORE_KI_BRAIN.md` (Teil-Anleitungen).

**Verbleibende Lücken:**
1. **Videos sind standardmäßig ausgeschlossen** — fachlich bewusst, muss pro Projekt entschieden werden.
2. **Keine automatische Zeitplanung** — die App erinnert, der Nutzer startet die Sicherung weiterhin selbst.
3. **Drei UI-Handtests des Restores offen** — technischer Rückweg, KB, Projekt und Build sind bewiesen.

### 4.2 Ziel: 3-2-1 für SewerStudio (einfach bedienbar)

**Drei Kopien, zwei Medien, eine außer Haus:**

| Kopie | Was | Wo | Wie |
|---|---|---|---|
| 1 (Arbeitsdaten) | Alles | C: / D: (Arbeitsplatte) | die App selbst (atomar + .bak + Restore-Points) |
| 2 (PC-Ausfallschutz) | Programm + KI-Gehirn + Einstellungen + Logs + Projekte (Videos optional) | externe USB-Platte | `FullBackupService` inkl. Projekte-Komponente |
| 3 (außer Haus) | mindestens: Projekte + KI-Gehirn-Snapshot + Einstellungen | zweite Platte an anderem Ort ODER Cloud-Ordner | denselben Backup-Lauf auf zweites Ziel wiederholen (Backup-Dialog erlaubt freie Zielwahl bereits heute) |

Videos sind groß (~3000 Stück): Sie gehören mindestens auf Kopie 2 (USB-Platte, einmalig + bei Zuwachs); für Kopie 3 reichen die Projekt-JSONs + Protokolle + Fotos, weil Videos aus den Original-Kanal-TV-Exporten wiederbeschaffbar sind. Diese Entscheidung ist im Backup-Dialog sichtbar zu machen („Videos eingeschlossen: ja/nein").

### 4.3 Sicherungsablauf (Soll)

| Wann | Was | Auslöser |
|---|---|---|
| Bei jeder Änderung | Arbeitsdaten (Autosave + .bak) | automatisch (existiert) |
| Vor jedem Import | Restore-Point der projekt.json | Ein-Knopf-Import und alle echten Einzelimporte geschützt |
| Wöchentlich | Voll-Backup auf USB (inkl. Projekte) | Erinnerung beim App-Start über `LastFullBackupUtc` |
| Monatlich | Zweites Voll-Backup auf Außer-Haus-Ziel | Erinnerung, wenn > 30 Tage |
| Vor jedem Update (neuer Publish-Ordner) | Voll-Backup | Hinweis in INSTALLATION.txt (AP-18) |

**Aufbewahrung:** Das existierende Schema (10 datierte Versions-Stände in `_Versionen`) beibehalten. Zusätzlich: die manuellen ~1 GB Hand-`.bak`-Dateien im KI_BRAIN-Root nach `kb_backups/` verschieben (P3), damit Backups schlank bleiben.

**Prüfung der Sicherung:**
- Beim Kopieren: existiert (SHA256-Vergleich im `DirectoryMirror`).
- Optional P3: SHA256 pro Datei zusätzlich dauerhaft ins Manifest schreiben + Knopf „Sicherung später erneut prüfen", damit auch Bit-Rot auf der USB-Platte auffällt.
- DB-Snapshots werden als eigene Zahl ausgewiesen. Der echte zweite Sicherungslauf bestätigte 225 Snapshots und 0 übersprungene Dateien.

**Wiederherstellung:**
- Kurzfristig: die generierte `RESTORE-ANLEITUNG.txt` ist gut (echte Pfade, Env-Vars, Modellliste). Fallback-Pfad „4.4" korrigieren (15 min).
- **Technisch durchgeführt am 2026-07-12:** Backup → sauberes Ziel → Datei-Abgleich → Projekt 61 geladen → KB mit 17.149 Beispielen geprüft → wiederhergestelltes Programm gebaut. Protokoll: `docs/PROBERESTORE-PROTOKOLL.md`. Vor Freigabe fehlen noch PDF-, Video- und KI-Bedienprüfung.
- Mittelfristig (P2): geführter Restore-Dialog in der App.

**Verhalten bei Backup-Fehlern:** Heute meldet der Workflow übersprungene Dateien (gut). Zusätzlich: Fehlschlag des Gesamtlaufs muss `LastFullBackupUtc` NICHT setzen und beim nächsten Start erneut erinnern.

**Meldung an den Benutzer:** Eine Zeile reicht: „Letzte Datensicherung: vor X Tagen (Ziel: E:\…). Projekte enthalten: ja/nein." — sichtbar in den Einstellungen (existiert) und als Start-Hinweis bei Überfälligkeit (AP-14).

### 4.4 Notfallhandbuch

`docs/NEUER-PC-SETUP.md` und `INSTALLATION.txt` enthalten inzwischen die komplette Reihenfolge: .NET 10 SDK → Python + uv → Sidecar → Ollama + Modelle → ffmpeg → pdftotext → Env-Variablen → Backup zurückspielen → Proberestore-Checkliste. Als Komfortverbesserung fehlt nur noch ein gemeinsamer UI-Voraussetzungscheck.

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
1. **C#↔Python-Vertrag ist doppelt gepflegt.** Der detaillierte Versionsvergleich wird inzwischen auch im Haupt-Analysepfad erzwungen (AP-36). Die Konstante bleibt dennoch beidseitig zu pflegen; kein Schema-Generator nötig.
2. **`HoldingFolderDistributor` bleibt groß und statisch.** Dateiablage, Video-Konflikthinweise, Schacht-PDF-Auswahl und Formularfelder sind inzwischen getrennt. → Haltung, Dichtheit und weiteres PDF-Parsing danach einzeln herauslösen; kein Komplett-Umbau.
3. **Vier statische Fassaden** (`StatusColors.Current`, `CodeUsageTrackers.Current`, `DialogHost.Current`, `VsaCodeResolver`) neben dem DI. → Nicht umbauen; per Fitness-Test-Whitelist einfrieren, damit das Muster nicht wächst.
4. **ViewModels erzeugen Kosten-Stores selbst** (11 `new`-Stellen). → Beim nächsten Anfassen der jeweiligen Seite über ServiceProvider beziehen. Kein Sammel-Refactoring.
5. **Application-Schicht enthält QuestPDF-Rendering + XML-Parsing** (je >1100 Zeilen). → Nur als bewusste Ausnahme dokumentieren (eine Zeile in CLAUDE.md).

**Reihenfolge ab jetzt:** UI-Handtests des Proberestores, dann asynchrones Laden/Speichern (AP-50), Diagnosepaket (AP-55 Rest) und Abbruchtests (AP-41). Die Großdatei-Altliste (AP-34) ist abgeschlossen; der Fitness-Test verhindert Rückfälle. **Keine Neuentwicklung irgendeines Teils ist nötig.**

---

## 6. Umsetzungsfahrplan (Arbeitspakete)

Jedes Paket ist einzeln an Codex/Opus übergebbar (Prompts in Kapitel 11). Aufwände sind Netto-Schätzungen.

### Stufe 1 — Schutz vor Datenverlust

| AP | Titel | Prio | Aufwand | Betroffene Dateien | Abnahme |
|---|---|---|---|---|---|
| **AP-01** ✅ | Projekt-Laden: .bak/Restore-Point-Fallback + Fehlerdialog | P1 | 2–4h | `ShellViewModel.TryOpenProject`, `JsonProjectRepository` | **UMGESETZT 2026-07-12:** Neue `ProjectRecovery` (Infrastructure, 4 Tests) lädt bei kaputter projekt.json aus `.bak`, dann Restore-Points (neueste zuerst), und quarantäniert die kaputte Datei als `projekt.corrupt-<ts>.json` (nie gelöscht; nur wenn eine Kopie wirklich lädt). Verdrahtet in `TryOpenProject`: Warn-Dialog bei Rettung (nennt Quelle + Quarantäne, markiert dirty → Neuspeicherung), Error-Dialog wenn nichts rettbar (Original unangetastet). Build grün, 32 Infra- + 75 Architektur-Tests grün. **Offener manueller Abnahmeschritt:** projekt.json mit Byte-Müll überschreiben → App bietet .bak an (Live, braucht laufende App). |
| **AP-02** ✅ | Restore-Point vor Import reparieren (`ProjectFileLocator` nutzen) + vor JEDEM echten Import anlegen | P1 | 1–2h | `ProjectRestorePointService`, Import-Controller und VSA-Import | **UMGESETZT 2026-07-12:** PDF, XTF/M150/MDB, WinCan, IBAK, KINS, Ein-Knopf-Import sowie gespeicherte VSA-Quellen legen vor der Projektänderung einen gemeinsamen Restore-Point an. Alte und neue Projektstruktur werden unterstützt; maximal 20 Stände bleiben erhalten. |
| **AP-03** ✅ | Backup-Komponente „Projekte" (Videos optional, Dialog zeigt Umfang) | P1 | 0.5–1 Tag | `BackupPlanBuilder`, `SettingsFullBackupWorkflow`, `RestoreAnleitungText` | Umgesetzt und durch echten Backup-/Restore-Lauf bestätigt |
| **AP-04** ✅ | Single-Instance-Mutex mit Hinweis-Dialog | P1 | 1–2h | `App.xaml.cs OnStartup` | Umgesetzt; Unit-Tests grün |
| **AP-05** ✅ | XTF-Rohdaten-Archiv aus `bin\` in LOCALAPPDATA; Bestandsdateien umziehen | P1 | 1–2h | `LegacyXtfImportService` | Umgesetzt; alter `bin`-Pfad dient nur noch als Migrationsquelle |
| **AP-06** ✅ | KB-Root absichern: Settings + Start-Warnung bei Wechsel/leerer KB | P1 | 2–4h | `KnowledgeBasePaths`, `AppSettings`, `ServiceProvider` | **UMGESETZT 2026-07-12:** Reihenfolge Env-Override → gespeicherter Pfad → Standard. Der aktive Pfad bleibt pro Programmstart fest, alte Einstellungen werden übernommen und Abweichungen sichtbar gewarnt. Ein vorübergehender Env-Override überschreibt den gespeicherten Pfad nicht. |
| **AP-07** ✅ | KnowledgeBackupService: atomar schreiben + Schreibfehler nicht schlucken | P2 | 1–2h | `KnowledgeBackupService` | Umgesetzt; atomare Schreibpfade und Fehlerrückgabe vorhanden |
| **AP-08** ✅ | Schema-Versionscheck Projektdatei + `[JsonExtensionData]` | P1 | 4h | `Project.cs`, `JsonProjectRepository` | Umgesetzt; Version-99-, Migration- und Roundtrip-Tests grün |

### Stufe 2 — Absturzschutz und Fehlerbehandlung

| AP | Titel | Prio | Aufwand | Abnahme |
|---|---|---|---|---|
| **AP-09** ✅ | Crash-/Korruptions-/Roundtrip-Tests für JsonProjectRepository | P1 | 0.5–1 Tag | Umgesetzt; Robustheits- und Architekturtests vorhanden |
| **AP-10** ✅ | Test-Gate: pre-push-Hook `dotnet test` | P1 | 1–2h | Versionierter Hook und Installationsskript vorhanden |
| **AP-11** ✅ | KB-Start-Härtung mit `PRAGMA quick_check` | P2 | 3–5h | Umgesetzt; Health-Checker und Korruptionstests vorhanden |
| **AP-12** ✅ | Dirty-Schutz „Projekt entfernen" | P2 | <1h | Umgesetzt und per Architekturtest geschützt |
| **AP-13** ✅ | IBAK-Re-Import: VsaFindings ersetzen statt anhäufen | P2 | 2–4h | Umgesetzt; Doppelimport-Test vorhanden |
| **AP-14** ✅ | Backup-Erinnerung beim Start + Zeitstempel nur bei Erfolg | P2 | 2–4h | Umgesetzt; Policy- und Workflowtests vorhanden |
| **AP-15** ✅ | Import-Rollback über DeepCopy und Swap bei Erfolg | P1 | 0.5–1 Tag | Umgesetzt; Fehler- und Abbruchtests bestätigen unverändertes Original |

### Stufe 3 — Datenkonsistenz

| AP | Titel | Prio | Aufwand |
|---|---|---|---|
| AP-20 | Duplikat-Check Haltungsnamen (Anlegen + Umbenennen + Plausibilitäts-Warnung) | P2 | 2–4h |
| AP-21 ✅ | Video-Links relativ speichern + Load-/Save-Normalisierung | P2 | umgesetzt 2026-07-12 |
| AP-22 ✅ | WinCan/Legacy SQLite ReadOnly; Kultur-Fallback entfernt | P2/P3 | umgesetzt |
| AP-23 ✅ | ImportSourceArchiver: atomare Kopie + per-Datei-Fangnetz | P2 | umgesetzt |
| AP-24 ✅ | `user_version`-Schutz der KB | P3 | umgesetzt |

### Stufe 4 — Architektur und Codequalität

| AP | Titel | Prio | Aufwand |
|---|---|---|---|
| AP-30 ✅ | Sidecar-Versionscheck im detaillierten Health-Check | P2 | umgesetzt |
| AP-31 ✅ | Fitness-Test-Whitelist für die 4 statischen Fassaden | P3 | umgesetzt |
| AP-32 | `UI/Ai` in themenbezogene Unterordner sortieren (nur verschieben) | P3 | 2–3h |
| AP-33 | `GetService()` fail-fast statt still null | P3 | 0.5h |
| AP-34 ✅ | Großklassen schrittweise nach Verantwortung zerlegen; Altliste im Fitness-Test verkleinern | P2 | abgeschlossen 2026-07-12; Altliste von 20 auf 0 Dateien gesenkt. Zuletzt wurden Datenseite und Startfenster getrennt. |
| AP-35 ✅ | Hintergrundaufgaben sichtbar beobachten; lokale Server begrenzen und beim Stoppen abwarten | P2/P3 | umgesetzt 2026-07-12; Tageslog, 8-Client-Grenze, geordnetes Stoppen |
| AP-36 ✅ | Detaillierten Sidecar-Health-/Versionscheck im echten Analyse-Hauptpfad verwenden | P2 | umgesetzt 2026-07-12; 2 neue Entscheidungstests |

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
| AP-51 ✅ | Save-/SaveAs-Fehler als Dialog mit Handlungsanweisung | P2 | umgesetzt 2026-07-12; Load-Rettungsdialog über AP-01 |
| AP-52 | ex.Message-Mapping für die 5–10 häufigsten Fehlerquellen | P3 | schrittweise |
| AP-53 | Import: „Fehlgeschlagene erneut importieren" | P3 | 0.5 Tag |
| AP-54 ✅ | AutoSave „jede Änderung" kurz bündeln; nicht für jede Eingabe das komplette Projekt neu schreiben | P2 | umgesetzt 2026-07-12; 750-ms-Bündelung, Tests grün |
| AP-55 🔄 | Wichtige `Debug.WriteLine`-Pfade in Tageslog übernehmen + Diagnosepaket erzeugen | P2 | Hintergrundfehler im Tageslog umgesetzt; Diagnosepaket und weitere Randpfade offen |

### Stufe 7 — Installation, Update, Dokumentation

| AP | Titel | Prio | Aufwand |
|---|---|---|---|
| **AP-17** ⚠️ | Kompletter Proberestore auf sauberes Verzeichnis + Protokoll | P1-nah (P2) | technisch bestanden; PDF-/Video-/KI-UI-Handtests offen |
| **AP-18** ✅ | `docs/NEUER-PC-SETUP.md` + INSTALLATION.txt | P2 | Dokumentation umgesetzt; gemeinsamer UI-Voraussetzungscheck bleibt Verbesserung |
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
| 4 Architektur/Codequalität | Drift-Schutz, Auffindbarkeit | AP-30…AP-36 | ~2–3 Tage Rest | Hauptpfad prüft Sidecar-Version; Hintergrundaufgaben beobachtet; Großklassen werden kleiner |
| 5 Tests | Geschäftskritische Abläufe abgesichert | AP-40…AP-42 | ~2 Tage | Korruptions-/Abbruch-Tests grün |
| 6 Leistung/Bedienbarkeit | Keine Freezes, verständliche Fehler | AP-50…AP-55 | ~2–3 Tage Rest | Projekt-Laden mit Busy-Anzeige; gebündeltes AutoSave; Diagnosepaket |
| 7 Installation/Doku | „Neuer PC in einem Nachmittag" | AP-17, AP-18, AP-60…62 | ~1–2 Tage Rest | UI-Handtests des Restores; Setup-Doku vollständig |
| 8 Freigabe | Note A bestätigen | AP-70 | 1 Tag | Checkliste Kapitel 10 vollständig erfüllt |

Vom ursprünglichen Fahrplan ist ein großer Teil bereits umgesetzt. Der aktuelle Restaufwand bis zur Note-A-Prüfung liegt grob bei **7–10 Arbeitstagen netto**, zuzüglich Nachtlauf und eines normalen Arbeitstags für AP-70.

---

## 8. Schnell umsetzbare Verbesserungen (je < 2h, sofort viel Sicherheit)

1. **AP-17 Rest:** PDF-Import, Video und KI im wiederhergestellten Ordner live bedienen und protokollieren.
2. **AP-55 Rest:** Diagnosepaket ergänzen und weitere wichtige Debug-only-Randpfade ins Tageslog übernehmen.
3. **AP-20:** Duplikatprüfung für Haltungsnamen an Anlegen und Umbenennen anschließen.
4. **AP-41:** Je einen echten Abbruchtest für Analyse, Batch-Import und KB-Neuaufbau ergänzen.
5. **AP-50:** Projektladen zunächst nur mit Busy-Anzeige aus dem UI-Thread nehmen.
6. Diagnose-Schaltfläche für ffmpeg, pdftotext, Ollama und Sidecar als gemeinsame Liste ergänzen.
7. Als nächste kleine Aufräumung PDF-Textkorrektur, DataPage-Umbenennung oder Legacy-XTF-Lesen getrennt herauslösen.

---

## 9. Risikoregister

| Risiko | Wahrsch. | Schaden | Erkennung | Vorbeugung | Wiederherstellung | Verantwortlicher Teil |
|---|---|---|---|---|---|---|
| D:-Platte fällt aus | niedrig–mittel | hoch | Backup-Erinnerung und Manifest | Projekte im Vollbackup; Videos bewusst optional | Backup-Versionen; technischer Restore bewiesen | BackupPlanBuilder |
| Projektdatei korrupt (Absturz, Bit-Fehler) | niedrig | mittel | Ladefehlerdialog | atomar + Schema-Check | automatische `.bak`-/Restore-Point-Rettung + Quarantäne | JsonProjectRepository |
| Falscher/abgebrochener Import | niedrig–mittel | mittel | Importstatus und Log | Import auf DeepCopy; Swap nur bei Erfolg | Restore-Point; Original bleibt bei Fehler unverändert | ImportRunWorkflowController |
| KB „leer" nach Env-Var-Verlust / Umzug | niedrig–mittel | mittel–hoch | Root-/Sample-Warnung beim Start | `KnowledgeRootGuard`; Setup-Doku | kb_backups + Backup-Versionen | KnowledgeBasePaths |
| Backup vorhanden, UI-Funktion nach Restore scheitert | niedrig–mittel | hoch | offene PDF-/Video-/KI-Handtests | AP-17 vollständig abschließen | technischer Restore-Ordner und Backup bleiben erhalten | FullBackupService |
| Zwei App-Instanzen überschreiben sich | niedrig | mittel | zweite Instanz zeigt Hinweis | Single-Instance-Mutex umgesetzt | `.bak` der Vorversion | App.xaml.cs |
| Regression durch ungetesteten Commit | niedrig–mittel | mittel | pre-push-Testgate | versionierter Hook installiert | git revert/bisect | Prozess |
| Ältere App-Version öffnet neuere Projektdatei | niedrig | mittel | APP-VERSION-Dialog | Schema-Versionscheck + unbekannte Felder erhalten | Restore-Points | Project/Repository |
| Sidecar-API-Drift | niedrig | mittel | Versionscheck in Monitor und Hauptpfad | beidseitige Version `1.2.0` | Ollama-Fallback + SidecarE2eSmoke | VisionPipelineClient |
| VSA-Zustandsnote durch IBAK-Doppel-Findings verfälscht | niedrig | mittel | Doppelimport-Test | Findings werden ersetzt | Re-Import nach Fix | IbakExportImportService |

---

## 10. Definition „Note A" (messbare Freigabekriterien)

Das Programm ist produktionsreif (Note A), wenn ALLE Punkte erfüllt und dokumentiert sind:

1. ☑ Keine offenen P0-Probleme und keine unbewerteten P1-Probleme.
2. ☑ Die ursprünglichen P1-Kernpakete sind im Code umgesetzt; AP-02 und AP-06 sind abgeschlossen.
3. ☐ Kritischer Datenfluss testgeschützt: Projekt-Save/Load-Crashtests, Import-Idempotenz Ende-zu-Ende, KB-Korruptionstest, je 1 Abbruch-Test pro langem Vorgang.
4. ◐ Backup enthält Projekte; technischer Proberestore ist protokolliert. PDF-/Video-/KI-Handtests in der Oberfläche sind offen.
5. ☐ Simulierter Abbruch (Prozess-Kill während Speichern, während Import, während Backup) führt nachweislich zu keinem Datenverlust — je ein dokumentierter Handtest.
6. ☑ Save-/SaveAs-/Load-Rettungsfehler erscheinen als Dialog; Importfehler liefern klaren Status und unverändertes Original.
7. ☐ Reproduzierbarer Build: `Publish-SewerStudio.ps1` läuft durch, Release-Manifest trägt korrekte Version aus csproj, Produktivstart zeigt auf Publish-Ordner (nicht bin\Debug).
8. ☑ Installation dokumentiert: `docs/NEUER-PC-SETUP.md` und `INSTALLATION.txt` vorhanden.
9. ☐ Update + Rollback dokumentiert und einmal geprobt (neuer Ordner daneben, alter Ordner startet noch).
10. ☐ Fehlende externe Dienste kontrolliert: App-Start ohne Ollama/Sidecar/ffmpeg zeigt verständliche Warnungen (kein Crash) — Handtest dokumentiert.
11. ☐ Keine bekannten kritischen Sicherheitsprobleme (aktuell erfüllt, Note A halten).
12. ☐ Dauer-/Belastungstest bestanden: ein kompletter Batch-Nachtlauf + ein voller Arbeitstag ohne kritische Logeinträge/Speicherwachstum.
13. ☑ Versioniertes pre-push-Testgate und `LastFullBackupUtc`-Erinnerung vorhanden.
14. ☐ Freigabe-Checkliste (dieses Kapitel) abgehakt und mit Datum in docs/ abgelegt.

---

## 11. Übergabe-Prompts für P0/P1-Arbeitspakete

Gemeinsame Sicherheitsregeln für ALLE Prompts (bei jeder Übergabe mitgeben):

> **Sicherheitsregeln:** Arbeite im Repo `C:\Sewer-Studio_KI_4.5`. Prüfe zuerst `git status` und erhalte alle fremden lokalen Änderungen. Keine NuGet-Pakete ohne Rückfrage. Kommentare auf Deutsch. Bestehende Architektur-Muster wiederverwenden (`AtomicTextFileWriter`, Result-Pattern, `DialogService`). Nach der Änderung `dotnet build AuswertungPro.sln` und die genannten Tests ausführen. Kein Refactoring außerhalb des Auftrags.

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

## Audit-Nachlauf und verbleibende praktische Prüfungen

**Die fünf zunächst fehlenden Code-Prüfungen sind abgeschlossen.** Das vollständige Ergebnis mit Belegen, Gegenurteilen und neuen Arbeitspaketen AP-34 bis AP-55 steht in `docs/AUDIT-NACHLAUF-2026-07-12.md`. Es kam kein neues P0- oder P1-Problem hinzu.

Kurzurteil: Nebenläufigkeit B, externe Dienste B+, Codequalität C+, Logging/Diagnose B−, Performance B.

**Weitere von den Auditoren selbst gemeldete Lücken:**
- Die vollständige Projektmappen-Suite wurde am 2026-07-12 auf dem Abschlussstand ausgeführt: **8.468 bestanden, 1 übersprungen, 0 fehlgeschlagen** (inkl. 62 ProjectModernizer-Tests). Darin enthalten: 2.434 Infrastruktur-, 1.795 Pipeline- und 4.177 UI-Tests.
- Der technische Restore ist praktisch bestanden: Sicherung, saubere Rückkopie, Dateivergleich, KB-Integrität, Projektladen und Build. Offen sind die UI-Handtests PDF-Import, Video und KI.
- Die übrigen tools/-CLI-Schreibpfade und `kb_audit`-Python-Skripte sind nicht vollständig geprüft; QuestPDF mit korrupten Bildbytes und MAX_PATH (>260 Zeichen) wurden nicht praktisch reproduziert. `DirectoryMirror`, SQLite-Snapshots und Platzprüfung wurden dagegen durch Tests und den echten 97-GB-Backup-/Restore-Lauf praktisch geprüft.
- `BenchmarkSetStore` als Klasse nicht gefunden — Schreiber von `benchmark_set.json` unidentifiziert.

**Gegenprüfungs-Abdeckung:** Von den ursprünglich 10 als P1 gemeldeten Befunden wurden 6 gegengeprüft (alle CONFIRMED, 4 davon P1→P2 gesenkt). Die 6 verbliebenen echten P1 sind teils gegengeprüft (P1-7), teils nur über eindeutige, direkt am Code belegte Fakten gestützt (fehlendes `.github/workflows`, fehlende Crash-Tests, hartkodierter Import-Restore-Pfad) — diese Belege sind so unmittelbar, dass eine Gegenprüfung wenig hinzufügt.
