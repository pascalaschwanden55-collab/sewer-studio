# Programmaudit AuswertungPro / Sewer Studio

Stand: 2026-05-08  
Repository: `c:\Sewer-Studio_KI_4.3`  
Scope: Solution `AuswertungPro.sln`, C#/.NET/WPF-Anwendung, Import-/Distributionslogik, Video-Playback, KI-/Sidecar-Anbindung, KnowledgeBase, Export, Tests, Build- und Betriebsfaehigkeit.

## Kurzfazit

Das Projekt hat eine solide fachliche Basis: die Schichten sind grob getrennt, die kritische Importlogik faengt viele Fehler pro Datei ab, der Video-Matcher ist deutlich robuster als ein reiner Dateinamenvergleich, es gibt Architekturtests, viele Pipeline-Tests und eine gewachsene KnowledgeBase-/KI-Infrastruktur.

Der initiale Auditlauf fand einen nicht releasefaehigen Stand, weil die Solution wegen des neuen KB-Dashboards nicht kompilierte. Nach der Konsolidierung des Dashboard-Slices ist der Build wieder gruen. Die verbleibenden wichtigsten Themen sind DI-/Lifecycle-Schulden, sehr grosse UI-/Report-Dateien, Sidecar-Auth, Test-Isolation und Repo-Hygiene.

## Gepruefte Nachweise

- `dotnet sln AuswertungPro.sln list`
- `dotnet build AuswertungPro.sln -v minimal`
- `dotnet test AuswertungPro.sln -v minimal --no-restore`
- `rg --files`, `rg "MessageBox.Show"`, `rg "async void"`, `rg "catch\s*\{\s*\}"`, `rg "Process.Start|new HttpClient|\.Result|\.Wait|GetAwaiter\(\)\.GetResult"`
- Stichproben in `src/`, `tests/`, `sidecar/`, `tools/`, `docs/`

## Aktueller Build- und Teststand

- Initialer Buildstatus beim Auditstart: fehlgeschlagen.
- Nachpruefung nach Konsolidierung: `dotnet build AuswertungPro.sln -v minimal` erfolgreich, 0 Warnungen, 0 Fehler.
- Nachpruefung Tests: `dotnet test AuswertungPro.sln -v minimal --no-restore` erfolgreich, 931 bestanden, 1 uebersprungen, 0 Fehler.
- Initialer Fehler:

```text
src\AuswertungPro.Next.Infrastructure\Ai\KnowledgeBase\KbDashboardService.cs(90,20): error CS7036:
Es wurde kein Argument angegeben, das dem erforderlichen Parameter "TopConfusions" von
"KbDashboardSnapshot.KbDashboardSnapshot(...)" entspricht.
```

Die Ursache war ein Vertragsbruch zwischen `KbDashboardSnapshot` und `KbDashboardService`: Das Record-Modell enthaelt `TopConfusions`, der Konstruktoraufruf in `BuildSnapshot()` lieferte diesen Wert initial nicht. Der konsolidierte Slice liest die Confusions nun aus dem ValidationLog, mappt sie auf `ConfusionPair` und prueft das mit einem gezielten Test.

## Auditpunkte mit Verbesserungsvorschlag

| Nr. | Prioritaet | Bereich | Befund | Risiko | Verbesserungsvorschlag |
| --- | --- | --- | --- | --- | --- |
| 1 | P0 | Build | Initial erwartete `KbDashboardSnapshot` `TopConfusions`, waehrend `KbDashboardService.BuildSnapshot()` es nicht uebergab. In der Nachpruefung ist der Slice buildfaehig. | Solche Modell-/Service-Vertragsbrueche blockieren Anwendung und Tests sofort. | Den jetzigen Fix mit Test beibehalten; bei kuenftigen Dashboard-Modellerweiterungen Service- und UI-Tests im selben Slice aktualisieren. |
| 2 | P0 | Tests | Tests waren initial durch den Buildfehler blockiert; nach Konsolidierung laufen Solution-Tests gruen. | Ohne CI-Gate waere derselbe Fehler leicht erneut eincheckbar. | CI-/Pre-merge-Gate so konfigurieren, dass Compilefehler in Feature-Slices sofort stoppen; Dashboard-Tests als schneller Smoke beibehalten. |
| 3 | P0 | Arbeitsbaum | Initial gab es mehrere untracked/modified Dashboard-Dateien und zwei untracked Dateien im Repo-Root. Die Root-Junk-Dateien sind inzwischen entfernt; uebrig ist der Dashboard-/Audit-Slice. | Unklarer Lieferumfang, hohe Gefahr von vergessenen oder versehentlich falschen Dateien. | Vor Merge/Release gehoerende Dateien staged/committed einordnen und `git status` sauber halten. |
| 4 | P1 | Architektur | Die Schichten sind grundsaetzlich vorhanden und durch Architekturtests abgesichert. Gleichzeitig existieren statische Provider-Bruecken zwischen UI, Application und Infrastructure. | Schwer testbare globale Zustaende, versteckte Startreihenfolge, schwierigere Headless-Nutzung. | Provider-Bruecken schrittweise durch DI-registrierte Interfaces ersetzen; fuer jede Bridge einen Besitzer, Lifetime und Shutdown-Pfad definieren. |
| 5 | P1 | DI/Lifecycle | `ServiceCollectionConfigurator` kommentiert selbst eine laufende Migration; einzelne Services werden noch manuell zusammengesetzt und in Accessors gesetzt. | Dispose-/Lifetime-Fehler, doppelte Singletons, schwierigere Tests. | `WireOperateurAnnotationService` und aehnliche Sonderpfade in DI-Factories ueberfuehren; `HttpClient`, DB-Kontexte und Manager ueber Container-Lifetimes verwalten. |
| 6 | P1 | App-Startup | `App.xaml.cs` setzt viele globale Resolver, startet Background-Tasks und Sidecar per fire-and-forget. | Startup-Fehler koennen verdeckt bleiben; Reihenfolge ist fragil. | Einen `StartupCoordinator` einfuehren, der Phasen, Fehlerstatus und Cancellation strukturiert abbildet; Hintergrunddienste mit klarer Stop-/Dispose-Logik starten. |
| 7 | P1 | Domain | `HaltungRecord` implementiert `INotifyPropertyChanged` direkt in der Domain; der Code markiert das selbst als Tech Debt `ARCH-H1`. | Domain bleibt an UI-Bindings gekoppelt; Headless-CLI und reine Pipeline-Tests werden schwerer. | Mittelfristig Domain-POCO/Record einfuehren und UI-spezifische Bindings in `HaltungRecordViewModel` kapseln. Bis dahin Architekturtest beibehalten, aber keine neuen UI-Abhaengigkeiten in Domain zulassen. |
| 8 | P1 | Datei-Groessen | Mehrere Dateien sind sehr gross, z.B. `CodingModeWindow.xaml.cs`, `ProtocolPdfExporter.cs`, `DataPageViewModel.cs`, `PlayerWindow.CodingMode.cs`. | Aenderungen werden riskanter; Code-Reviews und Tests fokussieren schlechter. | Pro Feature Slice extrahieren: UI-Code-behind zu Commands/ViewModels, PDF-Export zu Composer-Klassen, Analyse-/KI-Logik zu Services. |
| 9 | P1 | UI-Dialoge | `MessageBox.Show` kommt sehr haeufig direkt in UI-Dateien vor. | Uneinheitliche UX, erschwerte Tests, blockierende Dialoge in Workflows. | Vorhandenen `IDialogService` konsequent nutzen und direkte MessageBox-Aufrufe nur noch in einer Adapterklasse erlauben. |
| 10 | P1 | Async | Viele `async void`-Handler und fire-and-forget-Tasks. | Exceptions koennen verloren gehen; Shutdown und Cancel sind unklar. | Nur echte WPF-Events als `async void` belassen; alles andere ueber `AsyncRelayCommand`, `SafeFireAndForget` mit Logging oder orchestrierte Hintergrunddienste fuehren. |
| 11 | P1 | Fehlerbehandlung | Viele leere oder sehr stille `catch`-Bloecke. | Produktionsfehler verschwinden; Diagnose wird schwierig. | Catches klassifizieren: erwartete Best-Effort-Faelle mit Kommentar/Telemetry, unerwartete Fehler mit Logger und strukturiertem Result. |
| 12 | P1 | PDF-Parsing | Es gibt robuste Parsing-Fallbacks in `HoldingFolderDistributor`, daneben auch generische Parser unter `Import/Pdf`. | Parserlogik kann auseinanderlaufen; Vendor-Sonderfaelle werden doppelt gepflegt. | Einen gemeinsamen Parser-Kern mit Parsing-Diagnostics einfuehren; Golden-Fixtures fuer Fretz/KIT/IBAK/WinCan/KINS anlegen. |
| 13 | P1 | Video-Matching | Der Matcher deckt exakte Namen, Basename, Suffix ab erstem `_`, Datum/Haltung und Gegeninspektion ab. `MatchedWithoutDate` ist fachlich nuetzlich, aber riskanter. | Haltungs-only-Matches koennen falsche Videos zuordnen, wenn Ordner Altmaterial enthalten. | Matching-Ergebnis um Confidence/Reason und `RequiresReview` erweitern; `MatchedWithoutDate` im UI/Log deutlich markieren und optional manuell bestaetigen lassen. |
| 14 | P1 | Distribution | Die Distributionslogik faengt pro Datei Fehler ab und erzeugt Missing-/Ambiguous-Marker. Die Pipeline ist aber statisch und ueber viele Partial-Dateien verteilt. | Fachlich robust, aber schwer isoliert zu testen und zu erweitern. | Eine `VideoResolutionPipeline` mit einzelnen, testbaren Schritten einfuehren; `DistributionResult` um eine Trace-Liste der Matching-Entscheidungen erweitern. |
| 15 | P1 | Unmatched/Ambiguous | Ambiguous-Kandidaten werden gemaess Vorgabe nach `__UNMATCHED` kopiert. Einzelne Kopierfehler koennen aber den Pfad stoeren, wenn sie nicht separat protokolliert werden. | Ein defekter Kandidat kann die Diagnose fuer die eigentliche Haltung verschlechtern. | Kandidatenkopien einzeln try/catchen und je Kandidat Status in Markerdatei und Result aufnehmen. |
| 16 | P1 | Pfad-Sicherheit | `ProjectPathResolver` und Sanitizing sind stark; absolute Pfade werden teils bewusst erlaubt. | Projekte koennen maschinengebundene Pfade speichern und spaeter nicht portabel sein. | Persistierte Medien bevorzugt relativ/contained speichern; absolute externe Pfade als Portability-Warnung im Projektstatus anzeigen. |
| 17 | P1 | Video-Deduplizierung | `FindExistingVideo` betrachtet gleiche Dateigroesse als vorhandenes Video. | Gleich grosse, aber unterschiedliche Videos koennen faelschlich als identisch gelten. | Deduplizierung auf `Dateigroesse + Extension + schnellen Partial-Hash` umstellen. |
| 18 | P1 | Sidecar-Auth | Der Python-Sidecar laeuft ohne Auth, wenn kein Token gefunden wird; Docs/OpenAPI bleiben oeffentlich. | Lokale Fremdprozesse oder Browser-Szenarien koennen Sidecar-Endpunkte ansprechen. | Fail-closed als Standard: ohne Token Start verweigern, ausser `SEWER_SIDECAR_AUTH=disabled` ist explizit gesetzt. Docs/OpenAPI bei aktivem Auth ebenfalls schuetzen oder abschaltbar machen. |
| 19 | P1 | Token-Erzeugung | `PythonSidecarService` erzeugt Tokens mit `Guid.NewGuid()` und schreibt sie ohne sichtbare ACL-Haertung. | Token ist brauchbar, aber nicht ideal kryptographisch und Dateizugriff ist nicht explizit eingeschraenkt. | `RandomNumberGenerator.GetBytes()`/Base64Url nutzen und Token-Datei unter Windows mit Benutzer-ACL absichern. |
| 20 | P1 | HTTP-Clients | Mehrere Stellen erzeugen direkt `new HttpClient`, auch in Sidecar-/Health-Pfaden. | Socket-/Timeout-Verhalten ist uneinheitlich; Tests koennen schwer mocken. | `IHttpClientFactory` oder zentrale typed Clients einfuehren; Sidecar-Health und Shutdown ueber denselben Client mit klarer Timeout-Policy. |
| 21 | P1 | Externe Prozesse | Es gibt einen guten `ProcessRunner`, aber direkte `Process.Start`-/ffmpeg-/ShellExecute-Pfade bleiben verteilt. | Argument-, Timeout- und Logging-Policies driften. | Direkte Prozessaufrufe in `IProcessRunner`/`IExternalOpener` buendeln; Spezialfall Streaming-ffmpeg als explizit zugelassenen Wrapper dokumentieren. |
| 22 | P1 | KnowledgeBase | SQLite-Kontext, WAL, busy timeout und writer-semaphore sind gute Grundlagen. Migrationen laufen aber ueber codebasierte Tabellen-/Spaltenupdates. | Wachsende Schemahistorie wird schwer nachvollziehbar und schwer rollbackbar. | Formale Schema-Version/Migration-Ledger einfuehren; Migrationen idempotent loggen und bei Fehlern Diagnose fuer UI/Logs liefern. |
| 23 | P1 | KnowledgeMirror | Mirror/Restore mit Manifest und Excludes ist stark. Konfiguration wirkt jedoch zwischen `AppSettings`, Env und Defaults verteilt. | Nutzer koennen falsche Erwartung haben, was wirklich gespiegelt wird. | Eine einzige Mirror-Konfigurationsquelle mit UI-Status schaffen; Excludes, letzter Sync, letzte Pruefsumme und Restore-Quelle sichtbar machen. |
| 24 | P1 | KI-Pipeline | KbIngestion, QualityGate, Review Queue und Dashboard bilden eine wertvolle Lernschleife. Der aktuelle Dashboard-Slice ist aber noch nicht build-stabil. | Neue KI-Diagnosefunktionen koennen Produktivpfade blockieren. | KI-/Dashboard-Slices in kleinere PRs schneiden: Modellvertrag, Service, UI, Tests jeweils getrennt absichern. |
| 25 | P1 | Sidecar-Ressourcen | VRAM-Watermark und Lazy-/Prewarm-Logik sind vorhanden. Auth- und Modellstatus werden fuer die C#-App nur indirekt sichtbar. | Nutzer sehen bei KI-Ausfall moeglicherweise nur Folgefehler. | `/health` um `auth_required`, Modellslots, Modellladefehler und VRAM-Status erweitern; UI-Diagnose darauf aufbauen. |
| 26 | P2 | Excel-Export | Template-Erhalt und Header-Aliase sind gut. `Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!)` kann bei reinem Dateinamen problematisch sein. | Einzelne Exportaufrufe in aktuelles Verzeichnis koennen fehlschlagen. | Output-Pfad normalisieren: wenn kein DirectoryName vorhanden ist, aktuelles Verzeichnis verwenden oder frueh validieren. |
| 27 | P2 | PDF-Report | `ProtocolPdfExporter.cs` ist funktional stark, aber sehr gross und mischt viele Layout-/Bild-/Fachdaten-Aufgaben. | Layoutaenderungen koennen unerwartete Seiteneffekte erzeugen. | In Header-, Grafik-, Foto-, KI-Summary- und Sanierungs-Composer splitten; Golden-Render-/Snapshot-Tests fuer Standardprotokolle einfuehren. |
| 28 | P2 | Tests | Es gibt viele Tests und Kategorien. Mehrere Tests greifen aber per Reflection auf private Methoden zu. | Refactors brechen Tests trotz gleichem Verhalten; private Implementierung wird zementiert. | Relevante Seams als `internal` Services mit `InternalsVisibleTo` oder als kleine fachliche Interfaces verfuegbar machen. |
| 29 | P2 | Testdaten | Einige Tests referenzieren maschinenspezifische Pfade wie `D:\TESTSAMPLES` oder `C:\KI_BRAIN`. | CI und andere Entwicklerumgebungen koennen instabil werden. | Solche Tests konsequent als `External/Diag` kategorisieren und nur bei gesetzter Env-Var ausfuehren; kleine synthetische Fixtures fuer CI bereitstellen. |
| 30 | P2 | Tooling | `tools/QuickPdfAnalyzer` hat einen wahrscheinlich falschen ProjectReference-Pfad; einige Tools sind nicht in der Solution. | Hilfsprogramme veralten unbemerkt. | Tool-Projekte entweder in eine `tools.sln` aufnehmen und bauen oder archivieren/entfernen. ProjectReferences korrigieren. |
| 31 | P2 | Legacy-Dateien | Alte PowerShell-/Legacy-Artefakte und fruehere Auditdokumente liegen sichtbar im Root/Docs. | Neue Agenten oder Entwickler koennen falsche Einstiegspunkte nutzen. | `_legacy/` oder `docs/archive/` einfuehren; README auf aktuelle C#-Solution und aktuelle Auditdatei fokussieren. |
| 32 | P2 | Modell-/Asset-Hygiene | Grosse Modellgewichte sind ignoriert, vendored Modellcode und einzelne ZIP-/Datenartefakte sind aber im Repo. | Repo-Groesse und Updateaufwand steigen; Herkunft/Versionen sind schwerer nachvollziehbar. | Modelle und vendored Modellcode als versionierte externe Artefakte/Submodule dokumentieren; Repo nur Loader, Checksums und Installskripte halten. |
| 33 | P2 | Dokumentation | `README.md` beschreibt einen frueher gruenen Build/Teststand, der aktuell nicht stimmt. `ARCHITECTURE.md` wirkt historisch. | Betriebs- und Onboarding-Dokumente verlieren Vertrauen. | README um "aktueller Stand" und "bekannte Blocker" aktualisieren; historische Architektur separat kennzeichnen oder ersetzen. |
| 34 | P2 | Logging | Viele Services loggen bereits gut, andere Fehlerpfade sind still oder nur Debug-Ausgabe. | Supportanalyse bleibt uneinheitlich. | Einheitliche Logging-Konvention: Datei/Haltung/Datum/Status bei Import, Request/Endpoint bei Sidecar, Operation/Path/Duration bei IO. |
| 35 | P2 | Release-Prozess | Es gibt Hinweise auf manuelle Audits, aber keinen klaren Release-Check in den sichtbaren Dateien. | Releasefaehigkeit haengt an manuellem Gedaechtnis. | `docs/RELEASE_CHECKLIST.md` anlegen: clean tree, build, test categories, sidecar smoke, sample import, video playback smoke, export smoke. |

## Vertiefte Bewertung nach Bereich

### 1. Build, Tests und Lieferfaehigkeit

Der unmittelbar blockierende Fehler liegt im KB-Dashboard-Slice. Das Application-Record `KbDashboardSnapshot` enthaelt seit der Erweiterung `TopConfusions`; der Infrastructure-Service erzeugt den Snapshot aber noch mit dem alten Parametersatz.

Verbesserungsvorschlag: Den Fix als kleinsten P0-Commit behandeln. Direkt danach `dotnet build AuswertungPro.sln -v minimal` und `dotnet test AuswertungPro.sln -v minimal --no-restore` erneut ausfuehren. Der Dashboard-Test sollte mindestens pruefen, dass Confusion-Paare aus dem ValidationLog in `TopConfusions` ankommen.

### 2. Architektur und Schichtentrennung

Positiv ist, dass die Solution in Domain, Application, Infrastructure, UI und Tests gegliedert ist und Architecture Guard Tests existieren. Die Migration Richtung DI ist sichtbar. Kritisch bleiben globale Provider und manuelle Accessors, weil sie Abhaengigkeiten verbergen.

Verbesserungsvorschlag: Eine Bridge-Liste pflegen und pro Sprint 1-2 Bridges durch echte Interfaces ersetzen. Neue Features sollten keine neuen statischen Provider einfuehren.

### 3. Import, PDF-Parsing und Video-Zuordnung

Die Kernanforderungen aus den Projektanweisungen sind weitgehend erfuellt: PDF bleibt Quelle der Wahrheit, Haltung/Datum/Filmname werden extrahiert, Video-Matching ist robust, NotFound/Ambiguous werden markiert und Fehler werden pro Datei in Results zurueckgegeben. Die Logik ist jedoch sehr breit und schwer als einzelne Pipeline zu ueberblicken.

Verbesserungsvorschlag: Die bestehende robuste Logik nicht ersetzen, sondern in benannte Schritte zerlegen: `ParsePdf`, `ResolveHolding`, `ResolveVideo`, `ResolveSidecars`, `WriteHoldingFolder`, `WriteMarkers`. Jeder Schritt liefert ein kleines Result mit Trace.

### 4. UI und Video-Playback

Der LibVLC-Controller ist vergleichsweise sauber getrennt. Die groessten Risiken liegen in den grossen Code-behind-Dateien, direkten MessageBox-Aufrufen und verstreuten async/fire-and-forget-Pfaden.

Verbesserungsvorschlag: UI-Refactoring nach Nutzungsrisiko priorisieren: erst Import-/Analyse-/Coding-Workflows, dann Trainingscenter, dann kosmetische Dialoge. Bestehende Controls nicht gross umbauen, sondern Commands und Services extrahieren.

### 5. KI, Sidecar und KnowledgeBase

Die KI-Infrastruktur ist ambitioniert und fachlich wertvoll: Sidecar, VRAM-Guard, KnowledgeBase, Review-Queue, QualityGate und Mirror bilden eine echte Lernplattform. Gleichzeitig erzeugt genau dieser Bereich aktuell den Buildbruch und enthaelt mehrere Betriebs-/Security-Risiken.

Verbesserungsvorschlag: KI-Funktionen in "produktiver Kern" und "experimentelle Diagnose" trennen. Experimentelle Dashboards duerfen den produktiven Build nicht blockieren; Feature Flags oder separate Testslices helfen.

### 6. Security und Betrieb

Pfad-Sanitizing und ProcessRunner sind gute Sicherheitsanker. Der Sidecar sollte aber nicht unauthentifiziert starten, nur weil keine Tokenquelle gefunden wurde. Auch direkte externe Prozess- und ShellExecute-Pfade sollten zentral bewertet werden.

Verbesserungsvorschlag: Fail-closed fuer Sidecar-Auth, kryptographisches Token, zentrale External-Process-Policy und ein Security-Smoke-Test fuer Sidecar-Endpunkte.

### 7. Export und Reporting

Excel- und PDF-Export sind fachlich umfangreich. Der Excel-Export ist gut testbar, der PDF-Exporter ist zu gross und sollte modularisiert werden.

Verbesserungsvorschlag: Beim naechsten Report-Feature zuerst eine kleine Composer-Struktur einfuehren und nur den betroffenen Abschnitt migrieren. Dadurch entsteht kein Big-Bang-Refactor.

### 8. Tests und CI

Die Testbasis ist sichtbar gewachsen und deckt Kernbereiche ab. Die groessten Luecken sind Reflection-Tests, externe Testpfade und die aktuell fehlende Buildfaehigkeit.

Verbesserungsvorschlag: CI in drei Ringe teilen: `core` immer, `integration` bei vorhandenen Tools/Fixtures, `external/diag` nur explizit. Reflection-Tests schrittweise durch interne Test-Seams ersetzen.

## Priorisierte Massnahmen

### P0: Vor jeder weiteren Arbeit

1. `KbDashboardService` an `KbDashboardSnapshot.TopConfusions` anpassen. Status: erledigt.
2. Build und Tests erneut laufen lassen. Status: erledigt, Build gruen, Tests gruen.
3. Arbeitsbaum bereinigen und untracked Dashboard-Dateien einordnen. Status: vor Commit noch offen.

### P1: Naechster Stabilisierungssprint

1. DI-/Provider-Bruecken inventarisieren und die kritischsten ersetzen.
2. Sidecar-Auth auf fail-closed umstellen.
3. Import-/Video-Matching als Trace-Pipeline testbarer machen.
4. Direkte MessageBox-, Process.Start-, HttpClient- und async-fire-and-forget-Pfade reduzieren.
5. Tests mit maschinenspezifischen Pfaden isolieren.

### P2: Nachhaltige Wartbarkeit

1. Grosse UI-/Exporter-Dateien in fachliche Module splitten.
2. KnowledgeBase-Migrationen versionieren.
3. Legacy-/Tooling-/Dokumentationsstruktur aufraeumen.
4. Release-Checkliste und Smoke-Tests fuer Import, Video, Sidecar und Export etablieren.

## Empfohlene Definition of Done fuer den naechsten Fix

- `dotnet build AuswertungPro.sln -v minimal` erfolgreich.
- `dotnet test AuswertungPro.sln -v minimal --no-restore` erfolgreich oder mit klar dokumentierten externen Skips.
- Neuer/angepasster Test fuer `TopConfusions`.
- Keine untracked Produktivdateien im Repo-Root.
- README oder Changelog nennt den wiederhergestellten Buildstatus.
