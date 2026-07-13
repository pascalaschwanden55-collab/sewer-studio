# Wartbarkeits- und Belastbarkeits-Deepscan SewerStudio 4.5.0

## Umsetzungsstand vom 13.07.2026

Die wichtigsten Sofortmaßnahmen aus diesem Bericht sind umgesetzt und geprüft:

- **Release-Protokollierung repariert:** Im Produktionscode gibt es keine `Debug.WriteLine`-Stelle mehr. Wichtige Warnungen und Fehler aus KI, Training, Import, Backup und Dateizugriff landen über `BestEffort` im Tageslog. Reine Statusinformationen verwenden `Trace`.
- **Echter Log-Dateitest ergänzt:** Ein Integrationstest prüft, dass eine `BestEffort`-Warnung tatsächlich als Warnung in die Tageslogdatei geschrieben wird.
- **Schacht-Seite verkleinert:** Die Sanierungsmaßnahmen- und Speicherlogik wurde in `SchachtMassnahmenDialogController` ausgelagert. `SchaechtePage.xaml.cs` liegt nun mit 930 Zeilen wieder unter der Grenze von 1.000 Zeilen.
- **Protokoll-Editor entkoppelt (A1-04 erledigt):** Request-Aufbau, KI-Aufruf und Fehlerbehandlung liegen im testbaren `ProtocolEntryEditorKiViewModel`; die VSA- und Gesamtprüfung liegt im `ProtocolEntryEditorValidationViewModel`. Der Dialog zeigt nur noch Ergebnisse und Feldmarkierungen an. Laufende KI-Aufrufe werden beim Schließen abgebrochen; technische Fehlerdetails landen im Tageslog statt im Nutzerdialog. `ProtocolEntryEditorDialog.xaml.cs` sank von 943 auf 789 Zeilen.
- **Medien-Suche entkoppelt (A1-07, Teilpaket):** `BatchMediaSearchService` wird zentral im `ServiceProvider` bereitgestellt und dem Fenster zusammen mit nur den benötigten Dialog- und Einstellungsdiensten übergeben. Das Fenster erzeugt den Dienst nicht mehr bei jedem Suchlauf und hält den ganzen Container nicht mehr als Feld. Technische Suchfehler landen im Tageslog.
- **Fenster-Dienste zentralisiert (A1-07 erledigt):** Medien-Suche sowie alle Training-Center-Dienste werden zentral bereitgestellt. Review-Warteschlange, SAM und Few-Shot bleiben bedarfsgesteuert; die Review-Warteschlange wird über alle Fenster geteilt. Das Fenster erzeugt diese Datei- und Netzwerkdienste nicht mehr selbst.
- **Diagnose-ViewModel entkoppelt (A1-05, Teilpaket):** `DiagnosticsPageViewModel` erhält nur noch `ILogTailReader` statt des ganzen `ServiceProvider` und greift nicht mehr direkt auf Dateien zu. `DailyLogTailReader` liegt in Infrastructure, liefert höchstens die letzten 200 Zeilen und protokolliert technische Fehler, während die Oberfläche eine verständliche Meldung zeigt.
- **Schattenauswertung entkoppelt (A1-05, Teilpaket):** Das ViewModel erhält Projektzugriff, `ISchattenAuswertungStore`, Service-Fabrik und Projektpfad gezielt statt `ShellViewModel` und `ServiceProvider`. Laden, Berechnen, Speichern und Fehlerpfad sind ohne echte KI oder Dateien testbar; rohe technische Fehler erscheinen nicht mehr in der Oberfläche.
- **VSA-Seite entkoppelt (A1-05, Teilpaket):** `VsaPageViewModel` speichert weder `ShellViewModel` noch `ServiceProvider`. Projektzugriff, Statusaktionen sowie XTF-, PDF-, VSA- und Maßnahmendienst werden gezielt übergeben. Zwei Tests sichern die Abhängigkeiten und den vollständigen Lauf mit Wiederherstellungspunkt, relativen Quelldateien und Projektänderung.
- **Schacht-Matrix entkoppelt (A1-05, Teilpaket):** Das ViewModel erhält Projekt, Projektpfad, Dialoge und Aktualisierungsmeldung gezielt. Der gesamte Container und die Shell werden nicht mehr gespeichert; fünf Tests schützen Laden, Kostenberechnung, Speichern und die Abhängigkeitsgrenze.
- **Medienkonflikte entkoppelt (A1-05, Teilpaket):** Projektzugriff, Quellordner, Dialoge und Video-Start werden gezielt übergeben. Das ViewModel erzeugt weder den Konfliktdienst noch ein Player-Fenster selbst. Drei Tests schützen die Abhängigkeitsgrenze, den fehlenden Projektordner und den Video-Start.
- **Einstellungen entkoppelt (A1-05, Teilpaket):** Einstellungen, Diagnose, Dialoge, Sicherungsdienst, Sicherungsstatus, Meldungen und Programmbereinigung werden gezielt übergeben. Der gemeinsam genutzte Sicherungsstatus bleibt erhalten; die Programmbereinigung wird zentral erzeugt. 156 Einstellungs- und Schutztests sind grün.
- **Export entkoppelt (A1-05, Teilpaket):** Das Export-ViewModel erhält Einstellungen, Dialoge, Excel-Export, Meldungen und Kostenabgleich gezielt. Es speichert den zentralen Container nicht mehr. 14 Export- und Verteilungstests schützen die betroffenen Abläufe.
- **Karte entkoppelt (A1-05, Teilpaket):** Das Karten-ViewModel erhält Einstellungen, Kartendaten und den Video-Start gezielt. Es speichert den zentralen Container nicht mehr; der Video-Player wird außerhalb des ViewModels erzeugt. 34 Karten-Tests schützen Navigation, Pfade und Darstellung.
- **Übersicht entkoppelt (A1-05, Teilpaket):** Das Übersichts-ViewModel erhält Einstellungen, Aktualisierungsmeldung, Dialoge und Projektablage gezielt. Es speichert den zentralen Container nicht mehr. 27 Übersichts-Tests schützen Projektauswahl, Vorschau, Navigation und den Schutz ungespeicherter Änderungen.
- **Schacht-Maßnahmen entkoppelt (A1-05, Vorbereitung):** Der Maßnahmen-Controller erhält nur noch Einstellungen, Dialoge und den Maßnahmenkatalog. Er kennt den zentralen Container nicht mehr. Die bestehenden 24 Schacht- und Maßnahmen-Tests schützen Dialogübergabe, Berechnung und Katalogbearbeitung.
- **Schacht-Seite entkoppelt (A1-05, Teilpaket):** Das Schacht-ViewModel erhält Einstellungen, Dialoge sowie die drei Schacht-Dienste gezielt. ViewModel und Seite speichern den zentralen Container nicht mehr. 29 fokussierte Tests schützen Pflichtfeldwarnung, Protokollbefehle, Maßnahmen und Seitenaufbau.
- **Sanierungs-Matrix entkoppelt (A1-05, Teilpaket):** Das ViewModel erhält Einstellungen, Dialoge, Kostenabgleich und Cockpit-Aktualisierung gezielt. Es speichert den zentralen Container nicht mehr. Zwei neue ViewModel-Tests schützen Projektwurzel, Haltungsaufbau und die Abhängigkeitsgrenze.
- **Projekt-Portabilität zentralisiert (A1-05/A1-07, Vorbereitung):** Der dateibearbeitende Portabilitätsdienst besitzt jetzt einen Application-Vertrag und wird einmal zentral bereitgestellt. Das Import-ViewModel erzeugt ihn nicht mehr selbst. Sechs echte Datei-Tests schützen Pfadumstellung, Fotokopien und Namenskollisionen.
- **Import-Portabilitätsablauf ausgelagert (A1-05, Teilpaket):** Projektprüfung, Fortschritt, Speichern und Ergebnisaufbereitung liegen im testbaren `ImportProjectPortabilityController`. Das Import-ViewModel stößt den Ablauf nur noch an; zwei Verhaltenstests schützen Warnung und Erfolgsfall.
- **Projekt-Fotozuordnung zentralisiert (A1-05/A1-07, Vorbereitung):** Auch die dateibearbeitende Fotozuordnung besitzt jetzt einen Application-Vertrag und wird einmal zentral bereitgestellt. Das Import-ViewModel erzeugt sie nicht mehr selbst. Sechs echte Datei-Tests schützen Zuordnung, Kopieren und relative Projektpfade.
- **Import-Fotozuordnungsablauf ausgelagert (A1-05, Teilpaket):** Projektprüfung, Ordnerauswahl, Fortschritt, Speichern und Ergebnisaufbereitung liegen im testbaren `ImportProjectPhotoAssignmentController`. Das Import-ViewModel stößt den Ablauf nur noch an; zwei Verhaltenstests schützen Warnung und Erfolgsfall.
- **Import-Protokollwerkzeuge ausgelagert (A1-05, Teilpaket):** Verteilung und Neuerzeugung eigener Protokolle liegen in zwei kleinen, getrennt testbaren Controllern. Das Import-ViewModel stößt die Abläufe nur noch an; vier Verhaltenstests schützen Warnungen, Speichern, Status und sichere Fehleranzeige.
- **Protokoll-Neuerzeugung abstrahiert (A1-10, Vorbereitung):** Der dateibearbeitende Dienst ist über `IProtocolRegenerationService` erreichbar und zentral verdrahtet. Die bisherige öffentliche statische Fassade bleibt für bestehende Aufrufer unverändert.
- **Ein-Knopf-Import ausgelagert (A1-05/A1-10, Teilpaket):** Formatprüfung, Hintergrundlauf, Speichern und Ergebnisanzeige liegen im `ImportOneClickProjectController`; der Textbericht wird von einem dateibearbeitenden Dienst geschrieben und Fehler landen im Tageslog. Das Import-ViewModel sank dadurch auf 686 Zeilen. Drei UI-Tests und ein echter Dateitest schützen Start, Erfolg, unbekannte Formate und Berichtinhalt.
- **Import-Berichtsnavigation ausgelagert (A1-05, Teilpaket):** Projektpfad, letzter Bericht, Berichtsordner und sicheres Öffnen liegen im `ImportReportNavigationController`. Das Import-ViewModel kennt keine letzte Berichtsdatei mehr und sank auf 654 Zeilen; zwei Tests schützen fehlende Ordner und den erfolgreichen Öffnungspfad.
- **CSV-Importbericht ausgelagert (A1-05/A1-10, Teilpaket):** Steuerung, Fehlerschutz und Dateiausgabe liegen außerhalb des ViewModels; technische Schreibfehler gehen ins Tageslog. Der neue Adapter verwendet den vorhandenen, atomar schreibenden `ProjectFieldCsvExporter` statt einer zweiten CSV-Logik. Das Import-ViewModel sank auf 603 Zeilen; drei UI-Tests und ein Adaptertest ergänzen die acht bestehenden CSV-Tests.
- **VSA-Katalogstatus ausgelagert (A1-05, Teilpaket):** SEC-/NOD-Erkennung, Statusaufbau und Neuladen liegen im `ImportCatalogController`. Technische Ladefehler gehen ins Tageslog statt als Rohtext in die Oberfläche. Das Import-ViewModel sank auf 563 Zeilen; drei Tests schützen Startstatus, SEC+NOD und fehlenden NOD-Katalog.
- **VSA-Importnachlauf ausgelagert (A1-05, Teilpaket):** Hintergrundbewertung, Fortschritt und Ergebnistext liegen im `ImportVsaEvaluationController`. Technische Fehler gehen ins Tageslog und werden in der Oberfläche nicht mehr offengelegt. Zwei Tests schützen Erfolg und sicheren Fehlerfall; das Import-ViewModel liegt bei 559 Zeilen.
- **Importseite vom God-Container getrennt (A1-05, Teilpaket):** `ImportPageViewModel` speichert den `ServiceProvider` nicht mehr. Dialoge, Einstellungen, Projektablage und die fünf manuellen Importdienste werden gezielt gehalten; alle ausgelagerten Abläufe erhalten ebenfalls nur ihre benötigten Dienste. Ein Architekturtest verhindert die Rückkehr des Containers.
- **Datenseite schrittweise entkoppelt (A1-05, Teilpaket):** Dialoge und Einstellungen werden als eigene Abhängigkeiten gehalten; sämtliche entsprechenden Laufzeit-Zugriffe über den `ServiceProvider` sind entfernt. Ein Architekturtest schützt diese Grenze, während der restliche Container in weiteren kleinen Paketen abgebaut wird.
- **Druckcenter entkoppelt (A1-05, Teilpaket):** Das ViewModel erhält Einstellungen, Dialoge, PDF-Ausgabe und Kostenabgleich gezielt. Es speichert den zentralen Container nicht mehr. Zwölf fokussierte Tests schützen Hintergrund-Aktualisierung, Filter, Auswahl und Leistungsverzeichnis-Aufbau.
- **Push-Schutz repariert:** Der pre-push-Hook prüft jetzt Infrastruktur-, Pipeline- und UI-Tests. Ein roter UI- oder Wartbarkeitstest blockiert damit den Push.
- **Gesamtprüfung grün:** 8.692 Tests bestanden (2.487 Infrastruktur, 1.812 Pipeline, 4.331 UI und 62 ProjectModernizer). Zwei maschinengebundene Tests wurden planmäßig übersprungen. Der Release-Build endet mit 0 Fehlern und 0 Warnungen.

Die nachfolgenden Fundstellen beschreiben weiterhin den Zustand **vor** dieser Umsetzung und bleiben als nachvollziehbares Audit erhalten. Noch offene mittel- und langfristige Punkte stehen in der Roadmap dieses Berichts.

> Stand: 2026-07-13 · Methode: 7 spezialisierte KI-Agenten (parallel, rein lesende Code-Analyse) auf Basis des committeten Standes. Kritische Funde wurden zur Gegenprüfung vorgesehen — **es trat kein Fund der Stufe „Kritisch" auf**, daher waren keine Gegenprüfungen nötig. Kein Framework-/Plattformwechsel vorgeschlagen.
> Belegte Zusatzprüfungen des Orchestrators sind mit ✔ markiert.
>
> **Nachtrag 2026-07-13:** `A6-02` ist umgesetzt. `tools/SidecarE2eSmoke`
> dekodierte drei Bilder aus einem echten Projektvideo und prüfte YOLO, DINO, SAM,
> Quantifizierung sowie die produktive Mehrmodell-Kette gegen einen Golden-Vertrag.
> Ergebnis auf der Zielmaschine: **PASS** (3/3 Bilder, 0 Modellfehler).

---

## 1. Gesamtbewertung Wartbarkeit & Stabilität

**Schulnote: 2,5 (gut–befriedigend).**

Das Programm ist im Normalbetrieb **stabil**: Es gibt keinen Fund mit Datenverlust-, Absturz- oder offenem Sicherheitsrisiko. Speichern ist durchgehend atomar, das Backup ist überdurchschnittlich sorgfältig (SHA256-Verify, SQLite-Online-Snapshot mit `integrity_check`, Versionierung, Ziel-Guards), SQL ist parametrisiert (keine Injection gefunden), die Token-Absicherung des KI-Dienstes ist sauber, und die Schichtarchitektur ist zyklenfrei mit aktiven Fitness-Tests.

Zwei Dinge ziehen die Note nach unten:

- **Blindflug im Fehlerfall (Release-Diagnostik):** Ein großer Teil der Warn-/Fehlerpfade schreibt nur nach `Debug.WriteLine` — das wird im Release-Build vom Compiler **entfernt**. Wenn produktiv etwas still schiefgeht, steht im Tageslog nichts. Für einen Solo-Entwickler, der zugleich Support ist, ist das das praktisch gefährlichste Einzelthema.
- **Belastbarkeit ist unbewiesen:** Die ~4.300 Tests decken die Rechenlogik hervorragend ab, aber es gibt **keinen** echten Dauer-/Belastungstest, **keinen** End-to-End-Test gegen einen laufenden KI-Dienst mit echtem Video und **keine** WPF-UI-Automation. Der auftragsrelevante Schwerpunkt „Belastung" ist damit isoliert nur mit Note 4 bewertbar.

**Bereichsnoten der 7 Agenten:**

| Bereich | Note | Kurzurteil |
|---|---|---|
| Architektur / große Klassen / MVVM | 3 | solide, aber versteckte God-Classes + MVVM-Lecks |
| Nebenläufigkeit / Ressourcen | 2 | überdurchschnittlich, wenige klare Lücken |
| Datei- & Projektsicherheit | 2 | defensiv gebaut, nur Härtungspunkte offen |
| Schnittstellen (PDF/DB/QGIS/Excel) | 2 | robust, Restrisiken bei Fremd-DB & Extremgrößen |
| KI-Dienst / GPU / Netzwerk | 2,5 | Kern-Sicherheit stark, Resilienz-Lücken |
| Testabdeckung / Belastung | 4 | Logik top, Dauer/E2E/UI praktisch abwesend |
| Wartbarkeit (Solo) | 2,5 | gutes Fundament, Diagnostik-Blindfleck |

---

## 2. Top-3-kritische Funde

Kein Fund erreichte „Kritisch"; die folgenden drei sind die schwerwiegendsten „Hoch"-Funde mit dem größten realen Effekt.

**T1 — Diagnostik verschwindet im Produktions-Build** (Agent 7, `A8-01`)
`BestEffort.Report` und 81 `Debug.WriteLine`-Stellen (u.a. „Trainings-JSON korrupt" in `TrainingSamplesStore.cs:225`) schreiben über `System.Diagnostics.Debug.WriteLine` — durch `[Conditional("DEBUG")]` im Release entfernt. Der gute `FileLogger` existiert, wird von diesen Pfaden aber nicht genutzt. **Folge:** Stille Fehler hinterlassen im Tageslog keine Spur.
→ *Zentrale Sink von `Debug` auf `Trace`/`ILogger` umstellen (Quick Win < 1h), dann in den Hot-Paths den Logger durchreichen.*

**T2 — Belastbarkeit ist nicht getestet** (Agent 6, `A6-01`/`A6-02`/`A6-03`)
Die 1.072 „UI-Tests" sind zu großen Teilen Quelltext-Guards (137 Dateien lesen `.cs` als String); nur ~25 instanziieren wirklich ein ViewModel. Zum Scan-Zeitpunkt gab es keinen E2E-Test gegen echten Sidecar/Video und keine Dauer-/Leak-Test-Infrastruktur. **Nachtrag:** Der echte Sidecar-/Video-Vertragstest (`A6-02`) ist inzwischen vorhanden und grün. WPF-UI-Automation und der 8-Stunden-Nachtlauf bleiben offen. → *Nachtlauf-Konzept in Punkt 6.*

**T3 — Grossdatei-Guard ist rot, aber das Push-Gate prüft ihn nicht** (Agent 1, `A1-01`/`A1-02` + ✔ Orchestrator-Prüfung)
✔ Verifiziert: `SchaechtePage.xaml.cs` hat **1001 Zeilen** — genau über der Guard-Grenze (>1000, leere Whitelist). Der `MaintainabilityFitnessTests` ist damit **aktuell rot**. ✔ Der pre-push-Hook führt nur `Infrastructure.Tests` + `Pipeline.Tests` aus — die **UI-Tests laufen nicht im Gate**, deshalb blockiert der rote Guard keinen Push und die „alles grün"-Meldung deckte ihn nie ab. Zusätzlich versteckt der reine Datei-Zähler echte God-Classes: ✔ `PlayerWindow` = **119 Dateien / 9.479 Zeilen** über partielle Klassen verteilt.
→ *SchaechtePage-Fachlogik auslagern (bringt Datei unter die Grenze), UI-Tests ins Gate aufnehmen, Guard auf „Zeilen je Typ" erweitern.*

---

## 3. Detaillierte Einzelergebnisse pro Schwerpunkt

### Schwerpunkt 1 — Zu große Klassen & Dateien

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A1-01 | Hoch | `PlayerWindow*.cs` — 97 partielle Teildateien (✔ 119 Dateien/9.479 Z. gesamt) | Zusammengehörige Partials (Coding, LiveDetection, Playback) in echte Controller/Services mit Interface extrahieren |
| A1-02 | Hoch → **erledigt** | `SchaechtePage.xaml.cs` = ✔ 1001 Zeilen (Guard >1000, Whitelist leer → Test rot) | Fachlogik ist ausgelagert; die Datei liegt mit 930 Zeilen wieder unter der Grenze |
| A1-09 | Mittel | `MaintainabilityFitnessTests.cs:16-27` misst Zeilen pro Datei, nicht pro Typ | Regel „Zeilen je (partial) Typ aggregieren, Warnung ab ~1500-2000" ergänzen |
| A1-08 | Mittel | `ArchitectureFitnessTests.cs` (1747 Z., ~70 Token-Tests); `UiArchitectureGuardTests.cs` leer | Struktur- statt String-Token-Prüfungen; leeren Test entfernen/füllen |

### Schwerpunkt 2 — Versteckte UI-/Fachlogik-Abhängigkeiten

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A1-03 | Hoch → **erledigt** | `SchaechtePage.xaml.cs:929` `new ProjectCostStoreRepository(...)` + Persistenzlogik im Code-behind | In `SchachtMassnahmenDialogController` ausgelagert und testgeschützt |
| A1-04 | Hoch → **erledigt** | `ProtocolEntryEditorDialog.xaml.cs:540/609` KI-Vorschlag + VSA-Validierung im Dialog | KI-Ablauf und VSA-Gesamtprüfung sind in zwei kleine ViewModels ausgelagert und mit 10 fokussierten Tests geschützt; der Dialog sank auf 789 Zeilen |
| A1-05 | Hoch → **teilweise erledigt** | `ServiceProvider.cs:50` God-Container mit IO im Ktor; als Ganzes in ~15 ViewModels injiziert | Diagnose, Schattenauswertung, VSA, Schacht-Matrix, Sanierungs-Matrix, Medienkonflikte, Einstellungen, Export, Druckcenter, Karte, Übersicht, Schächte und Import sind auf schmale Abhängigkeiten umgestellt; als Fach-ViewModel verbleibt die Datenseite |
| A1-07 | Mittel → **erledigt** | `TrainingCenterWindow.xaml.cs:83-84`, `MediaSearchWindow.xaml.cs:114` — Dienste per `new` | Medien-Suche, Training-Store, Import, KB-Diagnose, Review-Warteschlange, SAM und Few-Shot werden zentral verdrahtet; bedarfsgesteuertes Erzeugen ist testgeschützt |
| A1-06 | Mittel | `Application/Reports/ProtocolPdfExporter.cs` — QuestPDF-Rendering in Application-Schicht | Hinter `IProtocolPdfExporter` legen, konkrete Umsetzung nach Infrastructure |
| A1-10 | Niedrig | >50 `static class` mit Fachlogik; IO-behaftete ohne Interface (z.B. `ProtocolRegenerationService`) | IO-/seiteneffektbehaftete statisch → Instanz-Service mit Interface; reine Rechner statisch lassen |

### Schwerpunkt 3 — Nebenläufigkeit, Abbruch, Speicher

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A2-01 | Mittel | `VideoFrameExtractor.cs:41-63` — ffmpeg-Prozess wird bei Abbruch nicht getötet | Im catch/finally `p.Kill(entireProcessTree:true)` (wie `ExternalProcessRunner`) |
| A2-02 | Mittel | `TrainingCenterWindow.xaml.cs` — keine CTS, SAM-Segmentierung ohne Token | Fenster-CTS einführen, im Closing `Cancel()+Dispose()`, Token durchreichen |
| A2-03 | Mittel | `DichtheitImportDistributor.cs:112-124` — `GetAwaiter().GetResult()` auf async KI-Aufruf | Kette async machen oder `Task.Run`-Kapselung + `ConfigureAwait(false)` |
| A2-04 | Mittel | `DataPageVideoAnalysisController.cs:82` u.a. — `new HttpClient` pro Aufruf | Langlebigen geteilten Client nutzen (Vorbild: `GetCachedHttpClient`) |
| A2-05 | Niedrig | `VideoAnalysisPipelineWindow.xaml.cs:61-66` — CTS nie disposed | Nach `Cancel()` auch `Dispose()` |
| A2-06 | Niedrig | `DataPage.xaml.cs` — Such-Debounce-Timer bei Unloaded nicht gestoppt | `_searchDebounceTimer.Stop()` im Unloaded ergänzen |

*Positiv bestätigt: SafeFireAndForget/BoundedBackgroundTaskRunner breit genutzt; PlayerWindow-LibVLC-Cleanup sauber; ShellViewModel meldet statische Events in Dispose ab; kein SkiaSharp-Bitmap-Leak (kommt im Code nicht vor).*

### Schwerpunkt 4 — Datei- & Projektsicherheit

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A3-01 | Mittel | `FullBackupSourcesFactory.cs:53-70` — alle `SEWER*`-Env-Vars inkl. Tokens im Klartext in `umgebung.txt`/`RESTORE-ANLEITUNG.txt` | Werte von `*TOKEN*/*SECRET*/*KEY*/*AUTH*` durch `***redigiert***` ersetzen |
| A3-02 | Mittel | `FullBackupService.cs:434-493` — Manifest ohne Pro-Datei-Hashes; `DirectoryMirror` erkennt Änderung nur über Größe+Zeit | SHA256 je Datei ins Manifest; `FullBackupSmoke` um Verify-Modus erweitern |
| A3-03 | Niedrig | `JsonProjectRepository.cs:94-104` / `AppSettings.cs:192` — Klartext-JSON inkl. `PipelineSidecarToken` | Nur den Token per DPAPI (`ProtectedData`, CurrentUser) schützen; projekt.json vorerst Klartext |
| A3-04 | Niedrig | `KnowledgeBasePaths.cs:131-147` — `SEWERSTUDIO_KNOWLEDGE_ROOT` ungeprüft als Wurzel | Leere/relative Werte verwerfen, Override beim Start loggen |
| A3-05 | Niedrig | `SafeShellOpen.cs:8-13` — `.html/.htm` in Whitelist, kein Pfad-Containment | `.html/.htm` entfernen oder Pfad gegen Projektordner prüfen |

### Schwerpunkt 5 — PDF-, Datenbank- & QGIS-Schnittstellen

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A4-01 | Mittel | `IbakFdbConnectionOptions.cs:25-42` — IBAK-`.fdb` wird read-**write** (Embedded) geöffnet | `.fdb` vorher in Temp kopieren oder read-only-Attach erzwingen |
| A4-02 | Niedrig | `BuilderPageViewModel.Output.cs:269/224` — NPK-Export blockiert UI-Thread | Auf `async Task` + `Task.Run` umstellen (wie Haupt-Export) |
| A4-03 | Niedrig | `ExcelTemplateExportService.cs` — ClosedXML hält gesamtes Workbook im RAM | Zeilen-/Record-Limit mit Meldung, oder streamender Writer bei Massenfall |
| A4-04 | Niedrig | `PdfTextExtractor.cs:70/99` — Seiten-Budget nur im PdfPig-Fallback, nicht bei pdftotext | Seitenzahl vorab prüfen; Text bei Obergrenze abschneiden statt voll kopieren |
| A4-05 | Niedrig | `OfferHtmlToPdfRenderer.cs:84-145` — Playwright ohne hartes Gesamt-Timeout | `CancellationTokenSource(CancelAfter)` durchreichen |
| A4-06 | Niedrig | `IbakFdbConnectionOptions.cs:9-10` — SYSDBA/masterkey im Code (Env-übersteuerbar) | Für Embedded belassen; bei Server-Zugriff nur aus Env/Config |

*Positiv bestätigt: SQL parametrisiert (keine Injection); WinCan `.db3` ReadOnly; QGIS-Bridge Loopback-only, GET-only, Exception-gekapselt, in `App.OnExit` disposed; große Haupt-Exports laufen korrekt via `Task.Run`.*

### Schwerpunkt 6 — KI-Dienst, GPU-Ausfälle & lokale Netzwerksicherheit

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A5-01 | Mittel | `DefaultAiStartupLauncher.cs:68` / `App.xaml.cs:231` — Sidecar/Ollama bei App-Ende nicht beendet (~21 GB VRAM bleiben) | Kind-Prozesse via Job-Object verfolgen, in OnExit optional beenden; „KI stoppen"-Knopf |
| A5-02 | Mittel | `sidecar/main.py:92-144` — Trusted-Host-Check per Host-Header trivial umgehbar | Klarstellen: Host-Check = Anti-DNS-Rebinding, **nur der Token** ist die Sperre |
| A5-03 | Mittel | `sidecar/config.py:9` + `start_sidecar.ps1:66` — `SEWER_SIDECAR_HOST` ungeprüft an uvicorn | Bei Nicht-Loopback ablehnen/warnen |
| A5-04 | Mittel | `sidecar/main.py:77-89` + `VisionPipelineClient.cs:186-197` — CUDA-Fehler (kein OOM) → generischer 500, Client wrappt nicht | CUDA-Fehler serverseitig als 503 mit Klartext; oder 500 clientseitig typisiert wrappen |
| A5-05 | Niedrig | `sidecar/main.py:146-154` — Token-Enforcement fail-open bei leerem Token | Fail-closed: ohne Token alle Anfragen ablehnen |
| A5-06 | Niedrig | `QgisBridgeServer.cs:68` — Bridge (8765) ohne Token, read-only Loopback | Bei Mehrbenutzer-Szenario Token wie beim Sidecar; sonst bewusst dokumentieren |
| A5-07 | Niedrig | `AiStartupOrchestrator.cs:76-82` — Ollama ohne Auth, keine Loopback-Erzwingung | Ollama mit `OLLAMA_HOST=127.0.0.1` starten, bei Nicht-Loopback warnen |

*Positiv bestätigt: `secrets.token_urlsafe(32)`, `hmac.compare_digest`, Token nur an Loopback gesendet, Decompression-Bomb-Schutz, OOM → LRU-Eviction + 503 + 1 Retry.*

### Schwerpunkt 7 — Fehlende Belastungs- & Bedienungstests

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A6-01 | Hoch | `ArchitectureFitnessTests.cs` u.a. — 137 UI-Testdateien lesen `.cs` als Text; keine UI-Automation | ~46 ViewModels per `new + Command` testen (Quick Win); StaFact-Fenster-Smoke mittelfristig |
| A6-02 | Hoch → **erledigt** | `tools/SidecarE2eSmoke` + `SidecarRealVideoIntegrationTests` | Echter Sidecar, reales Video, YOLO/DINO/SAM, Quantifizierung und Golden-Vertrag sind umgesetzt und auf der Zielmaschine grün |
| A6-03 | Hoch | Repo-weit kein `[Trait]`, keine Dauer-/Leak-Testlogik | Trait-Kategorien (Unit/Integration/Endurance) + `NightlySoakRunner` |
| A6-04 | Mittel | `integrations/qgis/sewerstudio_bridge/` — keine Tests | Python-Smoke für Bridge-Endpunkte (TestClient) + C#-Vertragstest |
| A6-05 | Mittel | `KnowledgeBaseContext.cs:176-186` — KB-Schema-Migration (ADD COLUMN) im Bestand ungetestet | Test mit Alt-DB (ohne neue Spalten) → Upgrade + Datenerhalt prüfen |
| A6-06 | Mittel | Nur `DbfTableTests` prüft kaputte Dateien; PDF/XTF/WinCan/FDB nicht | Je Importer 1 Negativtest (truncated/leer/gesperrt) → Skip statt Crash |
| A6-07 | Mittel | `sidecar/tests/test_sam.py:24` — SAM/DINO nur mit `pytest -m gpu` | CPU-Smoke (Loader + Antwortschema) oder `-m gpu` verpflichtend vor Batch |
| A6-08 | Niedrig | `KnowledgeBaseContext.cs:52-56` — WAL/busy_timeout-Schutz ungetestet | Parallel-Reindex-vs-Retrieve-Test (kein „database is locked") |

### Schwerpunkt 8 — Wartbarkeit für Solo-Entwickler

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A8-01 | Hoch | `BestEffort.cs:54` + 81× `Debug.WriteLine` — im Release entfernt | Sink auf `Trace`/`ILogger` umstellen; Logger in Hot-Paths durchreichen |
| A8-02 | Mittel | `FindAncestor<T>` in ~7 Dateien dupliziert; 2× rohes `VisualTreeHelper.GetParent` | Auf zentrales `VisualTreeSafe.FindAncestor` konsolidieren |
| A8-03 | Mittel | `AuswertungPro.sln` zieht 12 CLI-Tools in jeden Build; kein `.slnf` | Solution-Filter für den Alltag (4 src + 4 test) |
| A8-04 | Mittel | `AGENTS.md` veraltet (beschreibt reines PDF-Tool, kein KI-Wort) | Auf Ist-Stand heben oder als Weiterleitung auf CLAUDE.md |
| A8-05 | Niedrig | `DiagnosticsPageViewModel.cs:22-40` — nur heutiger Log-Tail, kein Export | „Log-Ordner öffnen" + „Diagnosepaket (ZIP)" ergänzen |
| A8-06 | Niedrig | `Directory.Build.props` — keine Roslyn-Analyzer/Warnungs-Gate | `EnableNETAnalyzers=true`, `AnalysisLevel=latest-recommended` |
| A8-07 | Niedrig | `DataPagePrintController.cs:234` u.a. — rohe `ex.Message` in Nutzer-Dialogen | Zentraler `UserError.Describe(ex)` (kurz für Nutzer, voll ins Log) |

---

## 4. Quick Wins (< 2 Stunden, sofort umsetzbar)

1. **`A8-01` (Teil 1):** `BestEffort.Report`-Standard-Sink von `Debug.WriteLine` auf `Trace.WriteLine`/`ILogger` umstellen — eine Datei, sofort sind Release-Warnungen wieder sichtbar. *Größter Sicherheitsgewinn pro Minute.*
2. **`A3-01`:** Token-Werte in `umgebung.txt`/`RESTORE-ANLEITUNG.txt` redigieren (Namensfilter).
3. **`A2-01`:** ffmpeg bei Abbruch killen (`p.Kill(entireProcessTree:true)`).
4. **`A2-05`/`A2-06`:** CTS disposen + Such-Timer im Unloaded stoppen.
5. **`A8-03`:** `.slnf`-Solution-Filter (nur 4 src + 4 test) → schnellere Builds im Alltag.
6. **`A8-06`:** Roslyn-Analyzer in `Directory.Build.props` aktivieren (ohne TreatWarningsAsErrors).
7. **`A8-04`:** `AGENTS.md` auf CLAUDE.md umleiten (verhindert widersprüchliche Einstiegs-Doku).
8. **`A4-02`:** NPK-Export in `Task.Run` auslagern (kein UI-Einfrieren).
9. **`A5-02`/`A5-03`:** Host-Check-Kommentar klarstellen + Warnung bei Nicht-Loopback-Host.
10. **`A6-03` (Teil 1):** `[Trait]`-Kategorien einführen, damit Endurance-Tests trennbar werden.

---

## 5. Strategische Empfehlungen

1. **Diagnose-Sichtbarkeit herstellen (`A8-01`, `A8-05`, `A8-07`):** Alle fachlich relevanten Warn-/Fehlerpfade auf `ILogger` heben, ein „Diagnosepaket exportieren" bauen, rohe `ex.Message` durch gemappte Nutzertexte ersetzen. Ohne verlässliche Logs bleibt jede Fehlersuche im Feld Rätselraten.
2. **Grossdatei-Guard reparieren und ins Gate nehmen (`A1-09`, `A1-02`, Push-Hook):** Guard auf „Zeilen je Typ" erweitern (schließt das Partial-Schlupfloch), `SchaechtePage` unter die Grenze bringen, und die UI-Tests in den pre-push-Hook aufnehmen — sonst laufen Architektur-Guards nie automatisch.
3. **Echte God-Classes zerlegen statt weiter fragmentieren (`A1-01`, `A1-03`, `A1-04`):** PlayerWindow-Partials und die Fachlogik in SchaechtePage/ProtocolEntryEditorDialog schrittweise in Services/Controller mit Interface überführen — testgeschützt, kein Big-Bang.
4. **DI-Hygiene (`A1-05`, `A1-07`, `A1-10`):** ViewModels nur die benötigten Interfaces geben statt des ganzen `ServiceProvider`; per-`new`-Streuung in Fenstern beenden. Das macht Kernabläufe unit-testbar.
5. **KI-Prozess-Lebenszyklus (`A5-01`, `A5-04`):** Sidecar/Ollama über ein Job-Object verfolgen und beim Beenden kontrolliert stoppen (oder VRAM-Zustand + „KI stoppen"-Knopf); CUDA-Fehler in klare, freundliche Meldungen übersetzen.
6. **Fremd-DB schützen (`A4-01`):** IBAK-`.fdb` nur über eine Temp-Kopie/read-only anfassen — schützt Kunden-Originaldaten.

---

## 6. Vorschlag für erweiterte Teststrategie

**Ziel:** Die risikoreichsten Ketten (echtes Video, echter Sidecar, QGIS, WPF-Runtime, DB-Migration im Bestand) automatisiert absichern — sie laufen heute in **keinem** Test.

**Stufe A — Fundament (Quick Wins, Tage):**
- `[Trait]`-Kategorien (Unit/Integration/Endurance); Standardlauf + Push-Gate = nur Unit; UI-Tests ins Gate aufnehmen.
- Die ~46 ViewModels per `new + Command` testen (kein UI-Thread nötig) — ersetzt echte Deckung, die die String-Guards nur vortäuschen.

**Stufe B — Integration (auf der Zielmaschine, nicht CI):**
- ✔ **Headless Pipeline-Treiber** (`tools/SidecarE2eSmoke`): startet bei Bedarf einen echten Sidecar, dekodiert drei Videobilder, fährt YOLO/DINO/SAM→Quantifizierung durch und prüft den Vertrag gegen `golden/pipeline-contract.v1.json`. Als `[Trait("Category","Integration")]` maschinengebunden ausführbar.
- **Negativtests je Importer** (truncated/leer/gesperrt) → Skip statt Crash (`A6-06`).
- **KB-Migrationstest** mit Alt-DB → Upgrade + Datenerhalt (`A6-05`).
- **QGIS-Bridge-Smoke** (Python-TestClient + C#-Vertragstest) (`A6-04`).
- **SAM/DINO CPU-Smoke** oder `pytest -m gpu` verpflichtend vor jedem Batch (`A6-07`).

**Stufe C — Nachtlauf / Belastung (`tools/NightlySoakRunner`, 8 h, maschinengebunden):**
- Schleife über N Test-Videos für 8 h: pro Runde Video-Import → KI-Auswertung → QualityGate → PDF-Export → KB-Index → QGIS-Sync.
- Nach jeder Runde: `GC.Collect`, Assertion auf **Prozess-RAM-/Handle-Obergrenze** und **Sidecar-Latenz-Perzentil**; **VRAM** via `nvidia-smi` mitschreiben; Abbruch bei Regression/Leak. Ausgabe als CSV.
- **UI-Runtime:** schmaler StaFact-Smoke pro Hauptfenster (öffnen/schließen ohne Exception), damit ein Oberflächen-Absturz im Dauerbetrieb auffällt.

**Wichtig:** LibVLC, GPU, QGIS und Ollama laufen nur auf der Zielmaschine — Stufe B/C sind bewusst **maschinengebundene Jobs**, kein CI. Das Push-Gate bleibt schnell (Unit).

---

## 7. Roadmap für die nächsten zwei Releases (nur Wartbarkeit, keine Technologiewechsel)

### Release N+1 — „Sichtbarkeit & Sicherheitsnetz" (~1 Woche)
- **Diagnose-Sichtbarkeit:** `A8-01` (Sink umstellen + Hot-Paths), `A8-05` (Diagnosepaket), `A8-07` (Fehler-Mapper) — schrittweise beginnen.
- **Test-Gate reparieren:** UI-Tests in den pre-push-Hook; `A1-09` (Guard je Typ); `A1-02`/`A1-03` (SchaechtePage-Fachlogik auslagern → Guard wieder grün).
- **Quick Wins:** `A3-01`, `A2-01`, `A2-05`, `A2-06`, `A8-03`, `A8-06`, `A4-02`, `A5-02`/`A5-03`.
- **Ergebnis:** Fehler werden im Feld sichtbar, Architektur-Guards laufen automatisch, Build schneller.

### Release N+2 — „Belastbarkeit & Entkopplung" (~2 Wochen)
- **Teststrategie Stufe B + C:** ✔ Headless Pipeline-Treiber (`A6-02`) erledigt; offen bleiben NightlySoakRunner (`A6-03`), Importer-Negativtests (`A6-06`), KB-Migrationstest (`A6-05`) und QGIS-Smoke (`A6-04`).
- **God-Class-Abbau (testgeschützt):** PlayerWindow-Partials in Services überführen (`A1-01`); der ProtocolEntryEditorDialog ist bei KI und VSA-Validierung erledigt (`A1-04`).
- **DI-Hygiene:** ViewModels auf Interface-Konstruktoren umstellen (`A1-05`, `A1-07`), beginnend bei den am häufigsten geänderten.
- **KI-Resilienz:** Prozess-Lebenszyklus + CUDA-Fehler-Klassifizierung (`A5-01`, `A5-04`); IBAK-`.fdb` read-only (`A4-01`).
- **Ergebnis:** Der 8-Stunden-Nachtlauf ist fahrbar, die größten Wartungsbremsen sind entschärft, das KI-Subsystem verhält sich bei Ausfällen berechenbar.

---

### Offene Prüfungen (Grenzen dieses Scans)
- Rein statische Analyse, kein Build/Testlauf/Profiler/echter Fuzz-/Pentest (auftragsgemäß). Der rote Grossdatei-Guard (`A1-02`) und die Push-Hook-Lücke wurden vom Orchestrator per `wc -l` / Hook-Inspektion ✔ verifiziert, der tatsächliche UI-Testlauf nicht.
- Nicht vertieft: die ~60 CLI-Tools, Sidecar-Interna der einzelnen Inferenz-Routen, QuestPDF-Speicherverhalten bei sehr vielen Fotos, echte Fremd-DB-Beispieldateien, Laufzeit-Leak-Messung.
