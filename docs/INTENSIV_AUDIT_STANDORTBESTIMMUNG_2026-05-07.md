# Intensiv-Audit Standortbestimmung - SewerStudio / AuswertungPro

Datum: 2026-05-07  
Branch: `feature/pdf-import-beobachtungen`  
Bewerteter Stand: aktueller Arbeitsbaum inkl. lokaler, noch nicht committeter Aenderungen  
Notenskala: 1.0 = sehr gut, 2.0 = gut, 3.0 = befriedigend, 4.0 = ausreichend, 5.0 = mangelhaft, 6.0 = ungenuegend

## 1. Kurzfazit

SewerStudio ist inzwischen klar testfaehig fuer externe Tester und hat die groessten technischen Sicherheits- und Stabilitaetsluecken der letzten Audit-Runden sichtbar reduziert. Build und Tests sind gruen, die Layer-Struktur ist deutlich besser als noch vor der Architektur-Migration, der Diagnose-/Audit-Tab bringt wichtige Wartungsfunktionen in die App, und die KI-Pipeline hat eine ernstzunehmende technische Basis.

Die wichtigste Standortbestimmung ist aber hart: Das eigentliche Qualitaetsrisiko liegt nicht mehr primaer im Code, sondern in der Daten- und Validierungsqualitaet der KI. Die bestehende KB ist gross, aber noch nicht reif genug fuer autonome fachliche Entscheidungen ohne menschliche Kontrolle. Laut Audit-Schlussanalyse vom 2026-05-06 liegen nur 11 Prozent der Samples im Green-Bereich, 38 Prozent im Red-Bereich, und die ValidationLog-Trefferquote lag bei 52 Prozent. Das ist fuer Assistenz brauchbar, fuer automatische Codierung noch nicht belastbar genug.

Gesamtstand: **Note 2.8**  
Einordnung: professionell testfaehig, technisch stark im Umbau, aber noch nicht produktionshart fuer KI-autonome Entscheidungen.

## 2. Verifikation dieses Audits

Ausgefuehrte Checks gegen den aktuellen Stand:

| Check | Ergebnis |
| --- | --- |
| `dotnet build AuswertungPro.sln -v minimal` | Erfolgreich, 0 Warnungen, 0 Fehler |
| Pipeline Tests | 633 bestanden, 0 fehlgeschlagen |
| Infrastructure Tests | 140 bestanden, 1 uebersprungen |
| Gesamt Tests | 773 bestanden, 1 uebersprungen |

Wichtige Einschraenkung: Dieser Audit ist eine statische Code-, Architektur-, Dokumentations- und Teststandsanalyse. Es wurde kein vollstaendiger manueller UI-Durchlauf mit echten Inspektionsdaten und kein Screenshot-basierter Design-Audit ausgefuehrt. Die KB-Qualitaetszahlen stammen aus `docs/AUDIT_SCHLUSSANALYSE_2026-05-06.md`.

## 3. Scorecard

| Bereich | Note | Status | Beleg / Begruendung |
| --- | ---: | --- | --- |
| Gesamtprodukt | 2.8 | Gut testfaehig | Breiter Funktionsumfang, Build gruen, 773 Tests gruen, aber KI-Qualitaet und grosse Hotspots bleiben. |
| KI-Erkennungsqualitaet | 4.0 | Groesstes fachliches Risiko | 52 Prozent ValidationLog-Accuracy, 11 Prozent Green, 38 Prozent Red. Assistenz ja, autonome Entscheidung nein. |
| KI-Pipeline Engineering | 2.6 | Stark verbessert | Multi-Modell-Pipeline, Telemetrie, Active Learning, Sidecar, Watchdog, Cancellation-Fixes. |
| KnowledgeBase / Brain | 3.2 | Gross, aber unreif | 21.794 Samples und Embeddings, aber CategoryWeights und TrainingRuns historisch leer, wenig Korrekturquote. |
| Architektur | 2.9 | Auf richtigem Weg | Saubere Projektlayer vorhanden; UI und Import enthalten aber weiterhin sehr grosse Klassen. |
| Codequalitaet / Wartbarkeit | 3.1 | Befriedigend | Viele Services und Tests, aber mehrere 1.500-4.700-Zeilen-Hotspots und viele best-effort Catches. |
| Robustheit / Stabilitaet | 2.5 | Deutlich verbessert | Fail-closed Cleanup, Mirror-Verifikation, Sidecar-Restart-Budget, JSON/SQLite-Guards, Build/Test gruen. |
| Security / Safety | 2.4 | Gut mit Restkanten | ProcessRunner, SafeXmlLoader, Pfadschutz und Sidecar-Token vorhanden; manuelle Sidecar-Starts und lokale Pfade bleiben Risiko. |
| Testabdeckung / QA | 2.3 | Stark fuer Services | 773 gruen, viele Regressionstests; UI-, Sidecar-Route- und echte E2E-Tests fehlen weitgehend. |
| Optik / visuelle UI | 2.8 | Funktional-professionell | WPF-Tool wirkt arbeitsorientiert und dicht; Diagnose-Tab hilfreich; manche Fenster sind sehr voll. |
| Ergonomie | 3.0 | Gut fuer Experten | Workflows sind da, aber fuer externe Tester und Curation braucht es mehr Fuehrung und weniger kognitive Last. |
| Performance / Betrieb | 3.0 | Solide lokal | Lokale KI, Batchbetrieb, Telemetrie; aber DB-Telemetrie und Langzeitdrift-Auswertung fehlen. |
| Dokumentation / Auditfaehigkeit | 1.9 | Sehr gut | Test-Briefing, Schlussanalyse, Sicherheits- und Architekturhistorie sind stark. |
| Repo- / Datenhygiene | 3.6 | Schwachstelle | Git-Pack ca. 338 MB, getrackte Benchmark-PNGs, Modell-/Tokenizer-Dateien und ein `.pyc` im Repo. |

## 4. Relevante Kennzahlen

Codeumfang ohne `bin`/`obj`:

| Bereich | CS-Dateien | Zeilen |
| --- | ---: | ---: |
| Domain | 38 | 2.356 |
| Application | 150 | 22.394 |
| Infrastructure | 124 | 44.726 |
| UI | 189 | 75.463 |
| Tests | 94 | 14.171 |

Groesste Hotspot-Dateien:

| Datei | Zeilen | Bewertung |
| --- | ---: | --- |
| `src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor.cs` | 4.691 | Letzte klare ARCH-CRITICAL-Datei. |
| `src/AuswertungPro.Next.UI/Views/Windows/CodingModeWindow.xaml.cs` | 3.892 | UI-Orchestrierung zu gross. |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs` | 3.285 | ViewModel zu breit. |
| `src/AuswertungPro.Next.Application/Reports/ProtocolPdfExporter.cs` | 3.086 | Reporting-Komplexitaet hoch. |
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.CodingMode.cs` | 2.999 | PlayerWindow wurde zerlegt, Coding-Teil bleibt gross. |
| `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs` | 2.536 | Code-behind-lastig. |
| `src/AuswertungPro.Next.UI/Views/Windows/PhotoMeasurementWindow.xaml.cs` | 2.235 | Fachlogik/UI-Mischung wahrscheinlich. |

Weitere Beobachtungen:

- AI-bezogene Dateien verteilen sich noch ueber Domain, Application, Infrastructure, UI und Python-Sidecar. Das ist fachlich nachvollziehbar, aber fuer Wartbarkeit weiterhin anspruchsvoll.
- Viele leere oder best-effort `catch`-Bloecke sitzen in UI-/Cleanup-/Import-Pfaden. In Tests ist das meist harmlos, in Produktivcode erschwert es Diagnose.
- Harte lokale Pfade (`C:\KI_BRAIN`, `E:\Brain`, `D:\...`) sind teilweise bewusstes Produktdesign, teilweise aber noch in Test-/UI-Code sichtbar.
- Das Repo enthaelt getrackte Benchmark-Frames, Modell-/Tokenizer-Dateien, CSV/Prompt-Artefakte und ein Python-Bytecode-File.

## 5. KI-Qualitaet

Note: **4.0 fuer fachliche Erkennungsqualitaet**, **2.6 fuer Pipeline-Engineering**

Staerken:

- Pipeline ist technisch nicht mehr nur ein Experiment: Ollama, Vision-Pipeline, Sidecar, Batchbetrieb, Active Learning, Review Queue, Telemetrie und Evaluation sind als echte Bausteine vorhanden.
- Cancellation-Handling wurde an mehreren Stellen verbessert. `OperationCanceledException` wird in wichtigen KI-/Batch-Komponenten wieder durchgereicht.
- Sidecar-Verfuegbarkeit wurde durch Token-Datei, Health Checks und Watchdog-Restart mit RestartBudget erhoeht.
- Training Center, Review Queue, Schwachstellenansicht und KB-Qualitaetsansicht liefern die Oberflaeche, um die Datenqualitaet zu verbessern.

Schwaechen:

- Die fachliche Ergebnisqualitaet ist noch nicht hoch genug. 52 Prozent Accuracy im ValidationLog ist fuer automatische VSA-Codierung deutlich zu niedrig.
- Nur 11 Prozent Green-Samples bedeuten, dass die KB fuer viele Faelle unsicher oder widerspruechlich bleibt.
- Nur ca. 3 Prozent korrigierte Samples zeigen, dass die Lernschleife noch nicht genug menschliche Wahrheit bekommt.
- `TrainingRuns`, `SanierungDecisionLog` und `CategoryWeights` waren in der letzten KB-Auswertung leer. Damit existiert die Infrastruktur, aber sie steuert die Qualitaet noch nicht ausreichend.
- Langzeit-Telemetrie existiert als JSONL, aber noch nicht als auswertbare DB-Historie.

Audit-Urteil:

Die KI ist als Assistenzsystem wertvoll, aber noch kein autonomer Codierer. Die naechste Qualitaetsstufe entsteht nicht durch das naechste Refactoring, sondern durch konsequente Curation: woechentlich unsichere Samples labeln, Validierungsset ausbauen, Confusion-Cluster gezielt bearbeiten, Schwellenwerte kalibrieren.

Empfohlene Gate-Kriterien fuer "KI produktionsreif":

| Kriterium | Zielwert |
| --- | ---: |
| Green-Anteil | > 30 Prozent |
| ValidationLog Accuracy | > 75 Prozent als Zwischenziel, > 85 Prozent als Produktivziel |
| Manuell validierte Samples | mindestens 500 breit gestreute Faelle |
| Korrigierte Samples | > 15 Prozent bei kritischen Klassen |
| Red-Anteil | < 20 Prozent |
| Externe Blindtests | 0 kritische Fehlcodierungen ohne Warnhinweis |

## 6. Architektur

Note: **2.9**

Staerken:

- Die Zielstruktur Domain / Application / Infrastructure / UI ist real vorhanden.
- Viele Logikbausteine wurden aus UI-Klassen herausgezogen.
- Provider-Pattern und Service-Abstraktionen reduzieren direkte UI-Abhaengigkeiten.
- `TrainingCase` wurde in Richtung POCO/ViewModel-Split bewegt, wodurch Application-Services weniger MVVM mitschleppen.
- `PlayerWindow` wurde bereits stark zerschlagen. Die alte 5.000-Zeilen-Klasse ist nicht mehr der zentrale Monolith.

Schwaechen:

- `HoldingFolderDistributor.cs` ist mit 4.691 Zeilen noch der groesste Architektur-Hotspot. Parsing, Matching, PDF-Korrektur, Sidecar-Dateien, Schacht-/Haltungslogik und Output-Regeln gehoeren in klarere Teilservices.
- UI bleibt mit 75.463 CS-Zeilen der groesste Layer. Das ist fuer ein WPF-Fachtool nicht ungewoehnlich, aber die Wartungsrisiken sitzen dadurch stark im UI.
- `CodingModeWindow.xaml.cs`, `DataPageViewModel.cs`, `TrainingCenterWindow.xaml.cs` und `MultiModelAnalysisService.cs` sind weiterhin breit.
- Manche AI-Orchestrierung liegt noch im UI-Layer. Das erschwert Tests und headless Betrieb.
- Mehrere Prozess-/Dateioperationen sind historisch gewachsen; `ProcessRunner` ist vorhanden, aber noch nicht ueberall vollstaendig durchgezogen.

Audit-Urteil:

Die Architektur hat einen klaren positiven Trend. Sie ist nicht "sauber fertig", aber sie ist inzwischen steuerbar. Der naechste harte Architekturgewinn ist das Zerlegen von `HoldingFolderDistributor` in Parser, Matcher, PdfOutputWriter, SchachtDistributor, HaltungsDistributor und Report/Result-Komponenten.

## 7. KnowledgeBase / Brain

Note: **3.2**

Staerken:

- SQLite-KB hat mit WAL, `busy_timeout`, Foreign Keys fuer Embeddings und klaren Tabellen eine robuste Basis.
- 21.794 Samples und Embeddings sind eine substanzielle Datenbasis.
- Brain-Mirror Health Check, SHA256-Manifest und fail-closed Restore verbessern Backup-Sicherheit deutlich.
- Frame-Cleanup und Versions-Pruning sind jetzt nicht nur manuell im Diagnose-Tab sichtbar, sondern auch als Maintenance-Scheduler vorbereitet.

Schwaechen:

- KB ist gross, aber noch nicht qualitaetsreif: viele Yellow/Red-Samples, geringe Korrekturquote.
- Versions-Pruning bereinigt Versionen, aber Provenance bleibt fachlich nur dann stark, wenn `TrainingRuns` und Sample-Run-Verknuepfung tatsaechlich genutzt werden.
- `Samples.RunId` ist bewusst ohne FK angelegt. Das ist flexibel, aber schwach fuer strikte Nachvollziehbarkeit.
- `CategoryWeights` existiert, war aber historisch leer. Damit fehlt ein wichtiger Hebel, um Klassenungleichgewicht praktisch zu korrigieren.
- Brain-Storage war laut letzter Analyse ca. 113 GB, davon 67 GB Frames. Das ist operativ gross und braucht regelmaessige Wartung.

Audit-Urteil:

Die KB ist keine kleine Demo mehr, sondern ein echter Wissensspeicher. Ihr Problem ist nicht Volumen, sondern Vertrauenswuerdigkeit. Ohne menschliche Validierung bleibt sie eine grosse, teilweise unsichere Sammlung.

## 8. Optik und visuelle UI

Note: **2.8**

Staerken:

- Die UI ist klar als Arbeitswerkzeug gebaut: Daten, Video, Diagnose, Training, Review und Export stehen im Vordergrund.
- Der neue Diagnose-/Audit-Bereich ist sachlich und direkt nutzbar.
- DataGrid-, Tab- und Toolbar-Strukturen passen zu einem technischen Fachtool besser als eine marketingartige Oberflaeche.
- Der Coding-Modus wirkt funktional vollstaendig: Video, Analyse, Uebernahme/Ablehnung, manuelle Codierung und Training sind in einem Workflow erreichbar.

Schwaechen:

- Einige Fenster sind sehr dicht: Training Center, Coding Mode, Data Page und Player-Coding-Teile haben viele Buttons, Tabs und Expertenfunktionen.
- Die UI ist optisch eher "technisches Cockpit" als gefuehrte Testerfahrung. Fuer externe Tester braucht es klare Startpunkte und weniger Entscheidungslast.
- Mehrere grosse Code-behind-Dateien deuten darauf hin, dass UI-Zustand, Interaktion und Fachlogik nicht immer sauber getrennt sind.
- Barrierefreiheit, Tastaturfuehrung, Fokusreihenfolge und visuelle Fehlerzustaende sind statisch nicht ausreichend belegbar.

Audit-Urteil:

Die Optik ist fuer ein internes/professionelles Fachtool gut genug. Fuer breitere Nutzer oder externe Tester waere die groesste Verbesserung nicht "schoener", sondern gefuehrter: klare Modi, weniger parallele Aktionen, bessere Status- und Risiko-Kommunikation.

## 9. Ergonomie

Note: **3.0**

Staerken:

- Hauptablaeufe sind vorhanden: Import, Verteilung, Player, Codiermodus, Batch-SelfTraining, PDF-Import, Diagnose, Wartung.
- Menschliche Review-Arbeit ist bereits im Produktmodell angelegt.
- Diagnose-Tab reduziert die Notwendigkeit, Wartung per Skript oder Wissen ueber Dateipfade auszufuehren.
- DryRun bei Cleanup und Bestaetigung vor Loeschen sind gute Bedien-Sicherungen.

Schwaechen:

- Active Learning ist organisatorisch noch zu wenig gefuehrt. Der wichtigste Hebel liegt beim Labeln, aber die App muss diesen Wochenprozess noch staerker takten.
- Externe Tester brauchen klare Szenarien, sonst sehen sie Funktionsfuelle statt Qualitaetssignale.
- Fehlerzustaende aus Sidecar, KI-Modellen, Pfaden und Datenqualitaet muessen fuer Anwender noch eindeutiger sein: "nicht verfuegbar", "unsicher", "nicht gelernt", "technischer Fehler" sollten getrennt wahrnehmbar sein.
- Einige Pfade und Fachbegriffe setzen Projektwissen voraus.

Audit-Urteil:

Ergonomie ist fuer Power-User brauchbar, fuer neue Tester mittel. Der naechste UX-Hebel ist ein gefuehrter Curation- und Testmodus mit Tagesziel, Fortschritt, Klassenmix und klaren "unsicher wegen..."-Gruenden.

## 10. Robustheit und Stabilitaet

Note: **2.5**

Staerken:

- Build ist gruen und warnungsfrei.
- 773 Tests laufen erfolgreich.
- Frame-Cleanup ist fail-closed: Wenn aktive Sample-IDs nicht geladen werden oder leer sind und viele PNGs vorhanden sind, wird nicht geloescht.
- Brain-Mirror Restore prueft SHA256/Size per Manifest und bricht bei Mismatch ab.
- Sidecar-Watchdog begrenzt Crash-Loops mit RestartBudget.
- `OperationCanceledException` wird in zentralen Batch-/AI-Pfaden nicht mehr versehentlich verschluckt.
- JSON-/SQLite-Schreibpfade nutzen zunehmend Locks, Transaktionen und atomare Schreibmuster.

Schwaechen:

- Viele best-effort `catch`-Bloecke im UI- und Importbereich koennen Ursachen verdecken.
- MaintenanceScheduler aktualisiert den Frame-Cleanup-State auch bei fail-closed Ergebnis mit Error-Liste. Das verhindert Wiederholschleifen, kann aber einen persistenten Cleanup-Fehler bis zum naechsten Intervall verdecken.
- Sidecar-Routen, echte Prozessausfaelle und UI-Abbruchpfade sind noch nicht ausreichend end-to-end getestet.
- Manche Tests sind maschinen- oder datenpfadabhaengig und werden nur bei vorhandenen lokalen Testdaten relevant.

Audit-Urteil:

Robustheit ist eine der groessten Verbesserungen der letzten Arbeitstage. Fuer externe Tests ist der Stand gut. Fuer produktiven Dauerbetrieb fehlen noch Chaos-/Abbruchtests: App schliessen waehrend Detection, Sidecar killen waehrend Batch, DB sperren waehrend Cleanup, Mirror-Platte entfernen, grosse PDF-Mischordner importieren.

## 11. Security und Safety

Note: **2.4**

Staerken:

- `ProcessRunner` reduziert Command-Injection-Risiken durch ArgumentList-Nutzung.
- `SafeXmlLoader` schuetzt XML-Parsing gegen XXE-/DTD-Risiken.
- Pfad-Sanitizing und `EnsureUniquePath` sind in Kernlogik vorhanden.
- Sidecar bekommt Token-Datei/Env-Token; der UI-Startpfad setzt Auth.
- Brain-Mirror Restore ist gegen korrupte Mirror-DBs besser abgesichert.
- Maintenance-Tools nutzen DryRun/Confirmations und fail-closed Logik.

Restkanten:

- Manuell gestartete Sidecar-Instanzen koennen bei fehlendem Token weiterhin ohne Auth laufen, falls keine Token-Datei/Env gesetzt ist.
- `UseShellExecute=true` wird fuer bewusstes Oeffnen lokaler Dateien/Ordner genutzt. Das ist bei vertrauenswuerdigen, intern erzeugten Pfaden ok, muss aber bei userkontrollierten Pfaden konsequent begrenzt bleiben.
- Harte lokale Pfade koennen auf fremden Testmaschinen zu Fehlverhalten oder falscher Annahme ueber Datenlage fuehren.
- Repo enthaelt beispielhafte Feldframes/Modelldateien. Datenschutz und Lizenzlage sollten vor externer Weitergabe final geprueft werden.

Audit-Urteil:

Security ist deutlich besser als in frueheren Audits. Das Hauptrisiko ist nicht mehr ein einzelnes offensichtliches Injection-Loch, sondern die saubere Begrenzung lokaler Datei-, Sidecar- und Testdatenpfade.

## 12. Testabdeckung und QA

Note: **2.3**

Staerken:

- 773 bestandene Tests sind fuer ein Desktop-Fachtool stark.
- Regressionstests decken viele vorherige Audit-Fixes ab: ProcessRunner, SafeXml, KnowledgeBase, Mirror, Cleanup, RestartBudget, Importpfade.
- Der Build ist warnungsfrei.
- Test-Briefing fuer externe Tester ist vorhanden und fokussiert die richtigen Hotspots.

Schwaechen:

- UI-Tests fehlen weitgehend.
- Sidecar-Routen und Python-Integration sind nicht systematisch als Contract-/Integrationstests abgedeckt.
- Performance-/Langzeittests fuer Batchbetrieb, GPU/VRAM, grosse Ordner und DB-Locks sind nicht automatisiert genug.
- Optik/Ergonomie wurden nicht screenshot- oder nutzerbasiert verifiziert.

Audit-Urteil:

Die Service- und Regressionstestlage ist gut. Der naechste professionelle QA-Schritt ist nicht "noch mehr Unit Tests", sondern E2E- und Betriebsrealitaet: UI-Smoke, Sidecar-Contract, Langlauf, Datensatz-Matrix.

## 13. Performance und Betrieb

Note: **3.0**

Staerken:

- Lokaler Betrieb mit Ollama/Sidecar ist realistisch fuer Datenschutz und Offline-Faehigkeit.
- BatchPipeline begrenzt Parallelitaet.
- Telemetrie existiert als JSONL und fasst Pipeline-Laeufe zusammen.
- VRAM-/Systemmonitoring und GPU-Modellwahl sind vorhanden.
- Maintenance fuer Frames, Versionen, Crash Logs und Mirror reduziert schleichende Degradation.

Schwaechen:

- Pipeline-Telemetrie wird noch nicht in SQLite persistiert. Langzeitdrift ueber Wochen ist dadurch nur umstaendlich sichtbar.
- Repo- und Brain-Groesse machen Restore, Clone, Backup und Onboarding schwerer.
- Modelle/Sidecar/Hardwareannahmen sind noch stark lokal gepraegt.
- Grosse WPF-Fenster mit viel Code-behind koennen schwer reproduzierbare UI-Performance-Probleme erzeugen.

Audit-Urteil:

Betrieb ist lokal gut machbar, aber noch nicht operativ "langzeitbeobachtet". SQLite-Telemetrie plus klare Health-Dashboards waeren ein kleiner, aber wertvoller Schritt.

## 14. Dokumentation und Auditfaehigkeit

Note: **1.9**

Staerken:

- Es gibt eine ungewoehnlich gute Audit-Historie.
- `docs/TEST_BRIEFING_2026-05-07.md` ist fuer externe Tester direkt nutzbar.
- `docs/AUDIT_SCHLUSSANALYSE_2026-05-06.md` dokumentiert Architektur-, KI-, KB- und Wartungsstatus mit konkreten Zahlen.
- Bekannte Limitationen sind benannt statt versteckt.

Schwaechen:

- Dokumente sind zahlreich; ein neuer Tester braucht einen klaren Einstiegspunkt.
- Einige historische Zahlen koennen schnell veralten. Wichtig ist, Reports mit Datum/Branch/Buildstatus zu kennzeichnen.
- Release Notes, Nutzerhandbuch und Testerbriefing sollten getrennt bleiben.

Audit-Urteil:

Dokumentation ist ein echter Pluspunkt. Ein kompaktes "Start Here fuer Tester" plus dieses Standortdokument reichen fuer externe Tests.

## 15. Top-Risiken

| Prioritaet | Risiko | Auswirkung | Empfehlung |
| --- | --- | --- | --- |
| P0 | KI-Datenqualitaet reicht nicht fuer autonome Codierung | Falsche VSA-Codes, Vertrauensverlust | Active Learning konsequent starten, woechentlich 100 Samples, Validierungsset ausbauen. |
| P0 | `HoldingFolderDistributor` bleibt 4.691-Zeilen-Hotspot | Import-/Verteilungsregressionen schwer lokalisierbar | Refactor in Parser, Matcher, Writer, Schacht-/Haltungsservices. |
| P1 | UI-/Sidecar-E2E fehlt | Fehler erst bei Tester/Realbetrieb sichtbar | Smoke-Test-Skript plus Sidecar-Contracttests einfuehren. |
| P1 | Telemetrie nur JSONL | Drift und Latenztrends schwer sichtbar | PipelineTelemetry in SQLite persistieren. |
| P1 | Repo-Datenhygiene | Onboarding, Datenschutz, Clone-Groesse | Modelle/Frames/pyc aus Repo, LFS oder externe Artefaktablage. |
| P2 | Viele best-effort Catches | Fehlerursachen verschwinden | UI-Catches gezielt loggen, nur erwartbare Cleanup-Fehler schlucken. |
| P2 | Externe Tester sehen zu viel Komplexitaet | Unklare Rueckmeldungen | Gefuehrte Testpfade und Curation-Checklisten in der App. |

## 16. Empfohlener 30/60/90-Tage-Plan

### Naechste 7 Tage

1. Externe Tests mit `TEST_BRIEFING_2026-05-07.md` starten.
2. Active-Learning-Routine festlegen: 100 unsichere Samples pro Woche labeln.
3. Tester sollen jede KI-Entscheidung als "Assistenzvorschlag" bewerten, nicht als Wahrheit.
4. Bekannte Hotspots besonders testen: Codiermodus, Batch-SelfTraining, PDF-Import, Live-Detection, Quick-Scan.
5. Aktuellen lokalen Stand committen oder bewusst als Sprint-1-Arbeitsstand markieren.

### Naechste 30 Tage

1. `HoldingFolderDistributor` refactoren.
2. PipelineTelemetry nach SQLite schreiben.
3. Sidecar-Contracttests fuer Health, Analyse, Auth, Timeout, Fehlerantworten bauen.
4. UI-Smoke-Test fuer Start, Diagnose-Tab, Training Center, Coding Mode.
5. Repo bereinigen: Benchmarkframes, Modellartefakte und `.pyc` aus Git entfernen.

### Naechste 60 Tage

1. CategoryWeights aktiv nutzen.
2. TrainingRuns bei jedem echten Training/Export schreiben.
3. Review Queue/Curation als gefuehrten Wochenprozess ausbauen.
4. KB-Metriken als Dashboard anzeigen: Green/Yellow/Red, Accuracy, Klasse mit hoechstem Risiko, Drift.
5. Restore-Drill fuer Brain-Mirror dokumentieren und testen.

### Naechste 90 Tage

1. Produktiv-Gate fuer KI definieren und messen.
2. Mindestens 500 externe/manuelle Validierungsfaelle sammeln.
3. Langlauf: mehrstuendiger Batch mit Sidecar-Restart, App-Close, DB-Lock und Mirror-Ausfall.
4. UI-Ergonomie mit echten Testern beobachten und Hotspots vereinfachen.
5. Release-Kandidaten nur noch mit Testmatrix, KB-Qualitaetsreport und bekannten Limitationen freigeben.

## 17. Entscheidungs-Empfehlung

Wenn das Ziel maximale Produktqualitaet ist: **Option 1 priorisieren, Active Learning aktiv betreiben.**  
Grund: Die niedrigste Note sitzt bei der KI-Erkennungsqualitaet, nicht bei der Architektur.

Wenn das Ziel Audit-Optik und Code-Wartbarkeit ist: **HoldingFolderDistributor refactoren.**  
Grund: Danach ist der letzte offensichtliche ARCH-CRITICAL-Hotspot weg.

Wenn das Ziel schneller operativer Nutzen ist: **Pipeline-Telemetrie in SQLite persistieren.**  
Grund: Kleiner Aufwand, sofort bessere Langzeitbeobachtung.

Meine fachliche Reihenfolge:

1. Active Learning / Curation starten.
2. PipelineTelemetry nach SQLite.
3. HoldingFolderDistributor refactoren.
4. UI-/Sidecar-E2E-Tests.
5. Repo-Datenhygiene.

## 18. Schlussurteil

SewerStudio ist kein Prototyp mehr. Es ist ein ernstes, umfangreiches Fachprogramm mit lokaler KI, echter Datenhaltung, Import-/Video-/PDF-Workflows, Wartung, Diagnose und einer wachsenden Testbasis. Der Code hat sichtbare Narben aus schneller Entwicklung, aber die Richtung stimmt: Layer werden sauberer, Risiken werden getestet, Sicherheitsloecher wurden aktiv geschlossen.

Die produktentscheidende Wahrheit bleibt: Die KI muss jetzt durch menschliche Korrektur besser werden. Ohne diese Datenarbeit bleibt sie ein gutes Assistenzsystem mit unsicherem Kern. Mit konsequentem Active Learning kann das Programm in den naechsten Wochen deutlich reifer werden als durch weitere reine Codearbeit.

