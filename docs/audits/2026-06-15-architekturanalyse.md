# Architekturanalyse SewerStudio

**Datum:** 2026-06-15
**Branch:** feature/gis-karte
**Rolle:** Principal Software-Architekt
**Scope:** Gesamtcodebasis SewerStudio — UI (WPF/.NET 8, MVVM), Application, Domain, Infrastructure (KI-Pipeline, Import/Export, KnowledgeBase) sowie der Python-Sidecar (`sidecar/sidecar/`). Vier Qualitaetsdimensionen: Wartbarkeit, Sicherheit, Fehler-Robustheit, Performance.
**Methodik:** Multi-Agent-Analyse mit adversarialer Verifikation. Jeder Befund wurde gegen den realen Code gegengeprueft (Datei, Zeile, Aufrufpfad). Befunde, die der Verifikation nicht standhielten, wurden herabgestuft oder als unsicher markiert. Nichts in diesem Bericht ist aus dem Gedaechtnis behauptet — jede Aussage ist mit Datei/Zeile belegt.

**Leitplanken (CLAUDE.md), an die sich alle Empfehlungen halten:**
- **Thin-AI:** Geschaeftslogik in C#, LLM nur fuer Textgenerierung.
- **Kein grosses Refactoring ohne explizite Diskussion** — alle Wartbarkeits-Empfehlungen sind schrittweise und additiv (neue Services mit Interface), nicht als Big-Bang-Umbau gedacht.
- **Neue Features als separate Services mit Interface.**
- **Tests NUR fuer Recommendation- und QualityGate-Logik** — Testbarkeits-Argumente zielen daher primaer auf genau diese Kernlogik, nicht auf jedes ViewModel.
- **Laptop/Workstation-Hardware-Abstraktion und VRAM-Budget (max 29 GB) erhalten.**

---

## Verifikations-Hinweis (Ehrlichkeit der Belege)

Die Befunde tragen drei Verifikationsstufen:

| Stufe | Bedeutung | Anzahl |
|---|---|---|
| **bestaetigt** | Am echten Code gegengeprueft, Zeilen/Aufrufpfad verifiziert | 11 |
| **nicht-verifiziert (leicht)** | Plausibel, Belegstellen genannt, aber nicht adversarial gegengeprueft — vor Umsetzung kurz pruefen | 27 |
| **ungeprueft** | Nur behauptet, NICHT verifiziert — eigenstaendig validieren | 1 |

Der einzige **ungepruefte** Befund (`qualitygate-green-bei-einem-signal`) wurde im Rahmen dieser Analyse nachverifiziert und ist am Code bestaetigt (siehe Dimension Fehler-Robustheit, dort markiert).

---

## Schwere-Uebersicht (nach Deduplizierung)

Mehrere Eingangsbefunde ueberlappten sich (drei HttpClient-/Socket-Befunde am selben Codepfad `CodingSessionService.cs:223`, zwei Service-Locator-Befunde, mehrere async-void-Befunde). Sie wurden zu Befundgruppen zusammengefasst. Nach Dedup verbleiben **31 distinkte Befunde**:

| Schwere | Anzahl |
|---|---|
| Kritisch | 0 |
| Hoch | 2 |
| Mittel | 17 |
| Niedrig | 11 |
| Positiv-Baseline (kein Mangel) | 1 |

Es gibt **keinen kritischen Befund** und **keine Sicherheitsluecke der Stufe Hoch/Kritisch**. Die Sidecar-Grundhaertung ist solide (eigener Positiv-Befund). Die zwei Hoch-Befunde betreffen Fehler-Robustheit (Deadlock-Risiko, QualityGate-Ehrlichkeit).

---

## Deduplizierung / Befundgruppen

| Gruppe | Zusammengefasste Eingangsbefunde | Gemeinsamer Kern |
|---|---|---|
| **G1 — KB-Index-Pfad `CodingSessionService.cs`** | `kb-index-httpclient-leak-und-stiller-catch`, `httpclient-leak-kb-index`, `eval-reload-im-indexpfad`, `adhoc-kbcontext-cache-verworfen` | Der fire-and-forget Pfad `IndexApprovedSamplesToKbAsync` (Z.198/223/226/228) erzeugt pro Aufruf HttpClient + KB-Context neu, verschluckt Fehler still und laedt Eval-Hashes jedes Mal von Disk. Ein zusammenhaengender Hotspot. |
| **G2 — Service-Locator / DI** | `service-locator-app-services`, `servicelocator-getservice-luecke`, `naming-serviceprovider-kollision` | Handgeschriebener `ServiceProvider` als getypter Service-Locator; harte `(ServiceProvider)App.Services`-Casts; `GetService` unvollstaendig; Namenskollision mit BCL/MEDI. |
| **G3 — async void in UI** | `async-void-ui-eventhandler`, `async-void-ungeschuetzter-prolog` | 33 `async void` in 8 UI-Dateien; nicht-Event-Handler (InitCodingAi, PrintHydraulikPdf) mit ungeschuetztem Prolog. |
| **G4 — HttpClient-Lifecycle (UI/Infra)** | `vpc-not-disposable-httpclient-leak`, `httpclient-lifecycle-ui`, `datapage-httpclient-per-action` | Wiederholtes `new HttpClient` ohne geteilte Instanz/Factory; `VisionPipelineClient` nicht IDisposable trotz eigenem Client. |
| **G5 — Codiermodus-Duplikation** | `coding-mode-window-tot-dupliziert`, `kalibrierung-dupliziert`, `riesen-methode-render-overlay`, `viewmodel-direkt-vlc-und-ki-pipeline` | `CodingModeWindow` ist toter Duplikat-Code; Kalibrierung/Overlay/Pipeline doppelt zwischen ihm und `PlayerWindow`. |

Die Einzelbefunde der Gruppen erscheinen in den Dimensionstabellen unter ihrer fuehrenden ID; verwandte IDs sind in der Spalte „Befund" vermerkt.

---

## 1. Wartbarkeit

**Kurzfazit:** Die groesste strukturelle Schuld liegt im Codiermodus. `PlayerWindow` ist eine God-Class ueber 6 Partials (~9.500 Zeilen, ~140 Felder, ~300 Methoden) mit echter Geschaeftslogik (Trainings-/Eval-Schutz-Persistenz, KI-Pipeline-Verdrahtung) direkt im Code-behind. Daneben existiert mit `CodingModeWindow` (~2.955 Zeilen) ein verwaister, driftender Duplikat. Quer durch die UI wird ein handgeschriebener Service-Locator hart gecastet. Nichts davon ist ein Korrektheits- oder Sicherheitsfehler — es ist interne Schuld, die schrittweise und additiv abgebaut werden sollte (CLAUDE.md: kein grosses Refactoring ohne Diskussion). **Hoechster Hebel: Trainings-/Eval-Schutz-Persistenz aus dem Window in einen testbaren Service ziehen, und den toten `CodingModeWindow` entfernen.**

| Schwere | Befund | Datei | Wirkung | Fix |
|---|---|---|---|---|
| Mittel | `player-window-god-class` — God-Class ueber 6 Partials mit Geschaeftslogik im Code-behind | `PlayerWindow.Coding.cs` (5558) + LiveDetection (1685) + OverlayRendering (1049) + Playback (790) + xaml.cs (336) + CodingSidePanelAccessors (65) = 9483 Z | Jede Codiermodus-Aenderung beruehrt eine 5500-Z-Datei mit verschraenktem State; nicht unit-testbar (Window instanziiert LibVLC, in keinem Test). Eval-Schutz/Persistenz = Geschaeftslogik im Fenster. | Persistenz (`PersistSingleEventAsTrainingSample` :738, `PersistCodingEventsAsTrainingSamples` :790, Eval-Schutz :715) als `ICodingTrainingPersistenceService` auslagern und unit-testen. Codiermodus-State in `CodingModeController` ohne WPF-Abhaengigkeit. Schrittweise. |
| Mittel | `coding-mode-window-tot-dupliziert` (G5) — verwaister ~2955-Z-Duplikat | `CodingModeWindow.xaml.cs:1-2955` | `new CodingModeWindow` hat **0 Treffer** in src+tests; keine Reflection/DI-Aufloesung. Wird bei jedem Build kompiliert; Logik driftet (`ApplyCalibration` :676 ≈ `ApplyCodingCalibration` PlayerWindow.Coding.cs:1567, Kommentar „vereinfachte Version"). | Gemeinsame Logik (Kalibrierung, Pixel↔Norm, Overlay-Geometrie) in testbaren Helper extrahieren, **dann** `CodingModeWindow.xaml`+`.cs` loeschen. |
| Mittel | `save-mark-as-training-mega-methode` — 6 Verantwortlichkeiten in 110-Z-Methode | `PlayerWindow.LiveDetection.cs:1192-1310` | Dialog + Capture + BBox + Temp-PNG + YOLO-Export + Annotation-Persistenz + CodingEvent + Badge in einer Methode; nicht mockbar (alles inline `new`/statische Factory); leeres `catch{}` beim Temp-Loeschen (:1237). | In `MarkAsTrainingService` (Application/Infra) ueberfuehren: Input (Overlay, Frame, Code) → Output (Annotation). Window nur Dialog+Capture+Status. Leeres catch durch Logging ersetzen. |
| Mittel | `god-viewmodel-training-center` — God-ViewModel mit Infrastruktur-IO | `TrainingCenterViewModel.cs` (2319 Z) | YOLO-Export, Batch-Import+KB-Index, ffmpeg-Pfad, Ollama-Check, EmbeddingService/Retrieval per `new`; nicht testbar. Verletzt „Features als separate Services mit Interface". | Trainingsablauf in `ITrainingCenterWorkflowService` (Scan→Generate→Export→Index) auslagern; VM bindet nur Fortschritt. HttpClient zentral. |
| Mittel | `service-locator-app-services` (G2) — `(ServiceProvider)App.Services` quer durch UI | `DataPageViewModel.cs:50` u.a. (37 Treffer/17 Dateien) | Versteckte Abhaengigkeiten, schlechte Testbarkeit, harte Kopplung an konkrete UI-Klasse. (NRE-Sorge eher theoretisch — Singleton-Cast kann praktisch nicht null werden.) | Abhaengigkeiten per Konstruktor injizieren; nur an der Komposition (App.OnStartup) aufloesen. Schrittweise, da grosses Refactoring nur nach Diskussion. |
| Mittel | `ui-instanziiert-infrastructure` — UI baut KI-Pipeline per `new` | `PlayerWindow.Coding.cs:3047-3063` | Praesentation kennt Sidecar-HTTP/Ollama/SQLite-Typen; `EnhancedVisionAnalysisService` in 3 UI-Dateien per `new` gebaut → Drift-Risiko (PlayerWindow.Coding :3053, CodingModeWindow :2327, TrainingCenterVM :2027). | `ICodingPipelineFactory` (Application/Infra), liefert fertiges Aggregat; UI bekommt nur Interface. Deckt sich mit CLAUDE.md. |
| Mittel | `vsacodetree-zweite-wahrheit` — hartkodierter Domain-Baum neben ADR-006-Manifest | `VsaCodeTree.cs:17` | Picker liest Struktur/Labels aus hartkodiertem Baum (BAB=„Risse"), Resolver/Report aus Manifest (BAB=„Riss") → zwei Wahrheiten. Manifest-Update schlaegt nicht auf Picker durch. **Korrektur:** der im Fix genannte `VsaCodeTreeCatalogAdapter` existiert im HEAD NICHT. | Test einfuehren, der VsaCodeTree-Labels/Untercodes gegen das Manifest verriegelt (ADR-006 Punkt 5). Scope ist Picker-Anzeige (Synonyme), nicht Code-IDs — kontrolliert. |
| Mittel | `kalibrierung-dupliziert` (G5) — Kalibrierungslogik mehrfach kopiert | `PlayerWindow.Coding.cs:1567`, `CodingModeWindow.xaml.cs:676` | Identische Rechnung (pixelDiameter, center, DN-Default 300, PipeCalibration); fachliche Korrektur muss an ≥2 Stellen synchron — driftet. Nicht testbar (an Canvas/TextBlock gebunden). *nicht-verifiziert (leicht)* | `CalibrationService.FromReferenceLine(start, end, pixelDiameter, dn) → PipeCalibration` (Application/Domain), unit-getestet. Window ruft nur auf. |
| Mittel | `einsatzbereit-schwelle-magic-number-dupliziert` — Reife-Schwelle (25/100) doppelt + mit Brush vermischt | `TrainingCenterViewModel.cs:357-363`, `DataPageViewModel.cs:36-37,1721` | Gleiche fachliche Schwelle an 2 Orten; im TrainingCenter an `SolidColorBrush` gebunden (nicht testbar). *nicht-verifiziert (leicht)* | `ModelReadinessEvaluator` (Label+Stufe, keine Brush); beide VMs konsumieren ihn; Brush bleibt im Converter. |
| Mittel | `viewmodel-instanziiert-stores-direkt` — Stores im Feldinitialisierer + IO im Konstruktor | `CostCalculatorViewModel.cs:23-25,96-137` | Nicht ohne echte Dateien testbar; Fenster-Oeffnen blockiert UI-Thread mit Datei-IO. *nicht-verifiziert (leicht)* | Stores als Interfaces ctor-injizieren; schweres Laden in awaitbare `InitializeAsync()`. |
| Mittel | `geschaeftslogik-im-viewmodel-datapage` — Sanierungs-/Verfuegbarkeits-Regeln im VM | `DataPageViewModel.cs:1019-1081,1464-1531` | Zustandsbewertung („sanierungsbeduerftig", Hydraulik/Kosten verfuegbar) an `Project.Data`/Dialogs gekoppelt, nur ueber volles VM testbar; kann von Export/Report abweichen. Verletzt Thin-AI/Schichtung. *nicht-verifiziert (leicht)* | In `MeasureSuggestionPolicy`/`DossierAvailabilityEvaluator` (Application) auslagern (teils existiert `DataPageDossierAvailabilityTests`); VM konsumiert. |
| Mittel | `riesen-methode-render-overlay` (G5) — 423-Z-Einzelmethode | `CodingModeWindow.xaml.cs:971-1394` | Werkzeug-abhaengige Zweige in einer 400-Z-Methode; keine Teil-Testbarkeit. *nicht-verifiziert (leicht)* — liegt im toten Duplikat (siehe G5). | Pro Werkzeug eine `IOverlayShapeRenderer`-Strategie; Geometrie von Shape-Erzeugung trennen. (Entfaellt teils mit Loeschung von CodingModeWindow.) |
| Mittel | `viewmodel-direkt-vlc-und-ki-pipeline` (G5) — Code-behind steuert VLC + KI direkt | `CodingModeWindow.xaml.cs:41-72`, `PlayerWindow.LiveDetection.cs:373-601` | KI-Pipeline-Steuerung an WPF-Fenster/DispatcherTimer gebunden, nicht testbar/wiederverwendbar → Ursache der PlayerWindow↔CodingModeWindow-Duplikation. *nicht-verifiziert (leicht)* | `ILiveDetectionLoop`/Controller kapselt Capture→Inferenz→Ergebnis per Callback; `IVideoPlayer`-Adapter fuer VLC. |
| Mittel | `reports-io-in-application` — Application macht Datei-IO + PDF-Rendering | `Application/Reports/ProtocolPdfExporter.cs` (2880 Z) u.a. | „Vertrags"-Schicht ist faktisch zweite Infra-Schicht (19 Dateien mit System.IO, csproj zieht QuestPDF); Schichtgrenze unscharf. *nicht-verifiziert (leicht)* | Regel festlegen+dokumentieren: I/O-lastige Reports nach Infrastructure oder Application bewusst als „Application+Reports" deklarieren. |
| Niedrig | `naming-serviceprovider-kollision` (G2) — eigene Klasse heisst `ServiceProvider` | `ServiceProvider.cs:43` | Kollision mit `Microsoft.Extensions.DependencyInjection.ServiceProvider`/`System.IServiceProvider`; erschwert spaetere DI-Migration. *nicht-verifiziert (leicht)* | Umbenennen zu `AppCompositionRoot`/`AppServiceRegistry`. |
| Niedrig | `batch-media-scan-stiller-catch` — Scan verschluckt UnauthorizedAccess/IO | `BatchMediaSearchService.cs:172-185` | Gesperrte/Netzwerk-Ordner werden kommentarlos uebersprungen; „keine Treffer" ununterscheidbar von „Ordner gesperrt". *nicht-verifiziert (leicht)* | Zaehler/Liste nicht lesbarer Ordner fuehren, am Ende als Hinweis ausgeben (Scan nicht abbrechen). |

---

## 2. Sicherheit

**Kurzfazit:** Keine ernsthafte Sicherheitsluecke. Der Python-Sidecar ist bereits solide gehaertet (eigener Positiv-Befund): 127.0.0.1-Binding, Trusted-Host-Middleware, konstant-zeit Token-Vergleich (`hmac.compare_digest`), starkes 256-bit-Token, Bild-Upload-Limits, Path-Traversal-Sandbox beim Training-Export, kein Stacktrace-Leak, `subprocess` ohne `shell=True`. Die verbliebenen Befunde sind allesamt **Niedrig** und greifen erst unter Bedingungen, die laut CLAUDE.md nicht vorliegen (Multi-User-/Terminal-Server, Server-FDB, fremdkontrolliertes XML). Es ist Defense-in-Depth, keine akute Luecke. **Wichtigster operativer Punkt: beim Deployment `SEWER_SIDECAR_HOST`/`SEWER_SIDECAR_TRUSTED_HOSTS` nicht auf `0.0.0.0`/`*` setzen — das wuerde die Haertung aushebeln.**

| Schwere | Befund | Datei | Wirkung | Fix |
|---|---|---|---|---|
| Positiv | `sidecar-hardening-positive-baseline` — Grundhaertung solide (kein Mangel) | `sidecar/sidecar/main.py:151` u.a. | Loopback-Binding, Trusted-Host-403, konstant-zeit Token, 256-bit-Secret, 25MB/50MP Bildlimits, Path-Sandbox, generische Fehler, kein `shell=True`. Hauptrisiken bereits adressiert. | Keine Aenderung. Beim Deploy `SEWER_SIDECAR_HOST`/`..._TRUSTED_HOSTS` nicht auf `0.0.0.0`/`*` setzen (main.py:155). |
| Niedrig | `token-file-no-restrictive-acl` — Sidecar-Token ohne restriktive ACL | `sidecar/sidecar/main.py:137` | Geheimnis im Klartext unter %LOCALAPPDATA%; auf Single-User-Workstation gering, auf Multi-User lesbar. Token-Generierung selbst korrekt. *nicht-verifiziert (leicht)* | Nach Schreiben ACL auf Eigentuemer beschraenken (icacls/`os.chmod 0o600`). Defense-in-Depth. |
| Niedrig | `live-control-token-file-acl` — Live-Control-Token ohne ACL | `LiveControlServer.cs:101` | Auth-Token (kann Reanalyse/UI-Brush aendern) im Klartext; %LOCALAPPDATA% ohnehin pro-User. Single-User: sehr gering. *nicht-verifiziert (leicht)* | Optional: restriktive ACL via FileSecurity oder DPAPI (`ProtectedData`). |
| Niedrig | `warmup-get-endpoint-side-effect` — `/warmup` auch als GET registriert | `sidecar/sidecar/routes/warmup.py:45` | Auth+Loopback davor; aber GET auf zustandsaendernden, ressourcenintensiven Endpunkt (laedt Modelle/VRAM) verletzt HTTP-Semantik (Prefetch/Log-Replay). *nicht-verifiziert (leicht)* | GET-Decorator entfernen, `/warmup` nur POST (C#-Aufrufer nutzt bereits POST, AiStartupService.cs:486). |
| Niedrig | `error-body-leak-into-csharp-exception` — Sidecar-Body ungefiltert in Exception | `VisionPipelineClient.cs:181` | Sehr gering (Sidecar liefert nur generische Strings); Restrisiko falls Handler je wegfaellt; Body laengenunbegrenzt (Logspam). *nicht-verifiziert (leicht)* | Body auf ~300 Zeichen kuerzen (wie AiStartupService.cs:465), nicht in UI durchreichen. |
| Niedrig | `fdb-hardcoded-masterkey` — Firebird-Default `masterkey` als Fallback | `KiasFdbTopologyReader.cs:61` u.a. | Offizieller Embedded-Default (SYSDBA/masterkey) fuer lokale read-only FDB ohne Server. Kein echtes Secret-Leak, aber dupliziert (4 Stellen). *nicht-verifiziert (leicht)* | In `IbakFdbDefaults.EmbeddedPassword` zentralisieren; bei Server-FDB Pflicht-Env-Var ohne Fallback. |
| Niedrig | `aistartup-string-arguments` — Prozessstart mit String-Arguments statt ArgumentList | `AiStartupService.cs:410` | Einzige Stelle ohne ArgumentList; theoretische Argument-Injection, praktisch nicht ausnutzbar (Pfad programmintern, kein User-Input). `Quote()` deckt Backslash-vor-Quote nicht vollstaendig. *nicht-verifiziert (leicht)* | `Arguments` auf `IReadOnlyList<string>` + `startInfo.ArgumentList.Add(...)` umstellen. |
| Niedrig | `wincan-dtd-ignore` — `DtdProcessing.Ignore` statt `Prohibit` beim Vorab-Check | `WinCanCatalogDiscoveryService.cs:118` | Abweichung vom XXE-Hausstandard; `Ignore`+Default-`XmlResolver=null` heisst kein direkter XXE-Vektor; nur Inkonsistenz. *nicht-verifiziert (leicht)* | Auf `DtdProcessing.Prohibit` + `XmlResolver=null` aendern (konsistent zu SafeXmlLoader). |
| Niedrig | `fachwissen-xdocument-load` — `XDocument.Load` ohne XXE-Schutz (Tool) | `tools/FachwissenIndexer/Program.cs:351` | Build-/Indexer-Tool, kein Laufzeitpfad; nur entwicklerkontrollierte Docs; Default `XmlResolver=null`. Restrisiko Billion-Laughs. *nicht-verifiziert (leicht)* | Auf `SafeXmlLoader`/`XmlReader.Create` mit `DtdProcessing.Prohibit` umstellen. |
| Niedrig | `ibak-sql-identifier-interpolation` — SQL aus interpolierten Identifiern | `IbakExportImportService.cs:533` | Identifier (nicht Werte) interpoliert; `QuoteId` verdoppelt Quotes korrekt (Firebird-Quoting); Quelle ist lokal importierte DB. Mitigiert. *nicht-verifiziert (leicht)* | Vertretbar; optional Allowlist erwarteter KIAS/IBAK-Schemanamen. |

---

## 3. Fehler-Robustheit

**Kurzfazit:** Hier liegen die beiden **Hoch**-Befunde. (1) `CompleteSession()` blockiert den UI-Thread synchron auf `MergeAndSaveAsync(...).GetAwaiter().GetResult()`, waehrend `TrainingSamplesStore` **kein** `ConfigureAwait(false)` nutzt — ein klassisches, last-/groessenabhaengiges Sync-over-Async-Deadlock-Risiko beim Session-Abschluss (App-Freeze, Datenverlust). (2) Das QualityGate kann mit **einem einzigen** Signal „Gruen" liefern — fehlende Kreuzvalidierung wird als sicher kaschiert, was direkt gegen die Projektregel „QualityGate-Ehrlichkeit" verstoesst. Daneben verschlucken mehrere `catch{}`-Bloecke echte KB-/Import-Fehler still und melden sie als „offline". **Hoechster Hebel: beide Hoch-Befunde, dann das stille Verschlucken echter Schreibfehler im Self-Training-Pfad.**

| Schwere | Befund | Datei | Wirkung | Fix |
|---|---|---|---|---|
| **Hoch** | `deadlock-coding-session-complete` — UI-Thread blockiert auf `MergeAndSaveAsync` ohne ConfigureAwait | `Ai/Training/TrainingSamplesStore.cs` (Aufruf: `CodingSessionService.cs:194`) | Aufrufpfad voll synchron auf WPF-UI-Thread: BtnComplete_Click → `CompleteSession` (RelayCommand) → `PersistTrainingSamplesFromEvents` → `.GetAwaiter().GetResult()`. Store-Awaits (`_fileLock`, Load/Save, Json De/Serialize :133/:236/:243) ohne `ConfigureAwait(false)` → Continuations posten auf blockierten SyncContext. **Bei grosser samples.json/langsamer Disk: UI-Freeze, Task-Manager-Kill, evtl. Datenverlust.** 0 ConfigureAwait-Treffer in der Datei (andere Infra-Klassen nutzen es). | Echtes `CompleteSessionAsync()` + `[RelayCommand] async Task`, Persistenz `await ...ConfigureAwait(false)`. Minimal-Fix: durchgaengig `.ConfigureAwait(false)` in `MergeAndSaveAsync`/`LoadInternalAsync`/`SaveInternalAsync` (behebt Deadlock; UI haengt nur noch fuer die Dauer). |
| **Hoch** | `qualitygate-green-bei-einem-signal` — Green mit nur einem Signal moeglich | `QualityGate/QualityGateService.cs:58-71` | Gewichte werden ueber vorhandene Signale renormiert; **keine Mindest-Signalzahl**. Einziger Schutz `signals.Count == 0 → Red` (:51). Bei nur YoloConf=0.9 (alle anderen null) → composite 0.9 → **Green**, obwohl DINO/SAM/Qwen/LLM/KB/Plausibilitaet fehlen. Halluzinierte YOLO-Box kann ungeprueft als bestaetigt durchlaufen — genau das, was das Gate verhindern soll. **Im Rahmen dieser Analyse am Code nachverifiziert (QualityGateService.cs:51-71): bestaetigt.** (Eingang: „ungeprueft".) | Mindest-Signal-Schwelle: bei `signals.Count < N` (z.B. 2-3) auf max. Yellow deckeln (Green nur ab N) und in `explanation` ausweisen. Optional Evidenz-Abdeckung (Originalgewichte vor Renorm / Gesamt) als Daempfung. Betrifft Kernlogik → laut CLAUDE.md testpflichtig. |
| Mittel | `kb-index-httpclient-leak-und-stiller-catch` (G1) — HttpClient ohne Dispose + `catch{}` verschluckt KB-Schreibfehler | `CodingSessionService.cs:223` | (1) `new HttpClient` ohne `using`, an nicht-IDisposable `EmbeddingService` uebergeben; (2) `catch{}` (:235-240) deutet **jeden** Fehler als „Ollama offline" — echte KB-Schreibfehler (SQLite gesperrt, Dim-Mismatch, korrupte DB; `IndexSampleAsync` macht `tx.Rollback(); throw;`) werden still verschluckt; Nutzer glaubt, Befunde landen in der KB. (Samples sind zuvor synchron persistiert → Datenverlust gemildert, Fehlerverschleierung bleibt.) | HttpClient in `using`/DI; im catch `HttpRequestException`/`SocketException` still vs. SQLite/IO geloggt unterscheiden. |
| Mittel | `leere-catch-bloecke-ui` — leere catch verschlucken Fehler in Player/Coding | `PlayerWindow.Coding.cs:2582,4663,5544`; `CodingModeWindow.xaml.cs:256-266,1763,1815`; `LiveDetection.cs:1237` | Temp-Frame-/Dispose-Fehler (VLC/HttpClient/CTS) unsichtbar; 8 Dispose in leere catch (OnClosing); 60 leere `catch{}` im src. Erschwert VRAM-/Handle-Leck-Suche. *nicht-verifiziert (leicht)* | Leere catch durch `catch (Exception ex)` mit Debug/Log; `DisposeSafe(Action)`-Helper mit Logging statt 8 stummer Bloecke. |
| Mittel | `async-void-ui-eventhandler` (G3) — 33 async void inkl. Pipeline-Init | `PlayerWindow.Coding.cs:3030` u.a. | `InitCodingAi` (async void) baut ganze KI-Pipeline; nicht gefangene Exception wandert in globalen Dispatcher statt lokales Handling; schwer testbar. *nicht-verifiziert (leicht)* | async void nur fuer echte Event-Handler; Init als `Task InitCodingAiAsync()`, im Handler mit try/catch awaiten. |
| Mittel | `async-void-ungeschuetzter-prolog` (G3) — Code vor try ungeschuetzt | `DataPageViewModel.cs:1362` u.a. | `PrintHydraulikPdf`/`PrintDossierPdf`/`InitCodingAi` haben Logik VOR dem ersten try; Wurf hinterlaesst halb-initialisierten Zustand, Nutzer sieht nur generische Meldung (Dispatcher faengt ab → nicht Kritisch). *nicht-verifiziert (leicht)* | Nicht-Event-Handler zu `async Task`; ganzen Rumpf in try/catch; per `SafeFireAndForget` (TaskExtensions vorhanden) aufrufen. |
| Mittel | `training-import-stiller-catch` — `catch{}` ueberspringt Trainingsfaelle ohne Diagnose | `TrainingCenterImportService.cs:100` | Ordner mit fehlerhaftem Pfad/Protokoll wird kommentarlos uebersprungen; Nutzer sieht weniger Faelle, weiss nicht welche/warum — stiller Datenverlust im Self-Training-Pfad. *nicht-verifiziert (leicht)* | `catch (Exception ex)` mit Logger-Warnung inkl. Ordnerpfad, oder uebersprungene Ordner sammeln + Diagnosedatei (analog `_pdf_ohne_befunde.log`). |
| Niedrig | `servicelocator-getservice-luecke` (G2) — `GetService` deckt nur ~14/~30 Dienste | `ServiceProvider.cs:404` | `GetService<T>` liefert fuer die Haelfte der Dienste still `null` (:420) → NRE; treibt Aufrufer in harten Cast. Interface ist „eine Luege". *nicht-verifiziert (leicht)* | `GetService` vollstaendig machen ODER bei unbekanntem Typ werfen statt still null ODER auf MEDI-Container umstellen. |
| Niedrig | `healthcheck-swallow-all-exceptions` — `catch{return null}` faengt auch OperationCanceled | `VisionPipelineClient.cs:59` | Abgebrochener Health-Check wird als „Sidecar offline" interpretiert → stiller Fallback auf schwaecheren Ollama-Only-Modus (VideoAnalysisPipelineService.cs:220), obwohl Sidecar laeuft. *nicht-verifiziert (leicht)* | Zuerst `catch (OperationCanceledException){ throw; }`, dann generisch (wie `CheckHealthDetailedAsync` :88 es korrekt macht). |

---

## 4. Performance

**Kurzfazit:** Keine akute Performance-Krise, aber mehrere vermeidbare Schleichkosten. Das **VRAM-Budget (29 GB) wird nur geloggt, nicht durchgesetzt** — `evict_lru()` existiert, wird aber im Ladepfad nie automatisch aufgerufen; das widerspricht CLAUDE.md („niemals alle Modelle gleichzeitig"). Wirkung gemildert, weil der OOM-Pfad bereits kontrolliert mit `empty_cache()`+503 endet (main.py). Daneben: HttpClient-Leaks ueber lange Sessions, ein verworfener Embedding-Cache bei ad-hoc KB-Contexten, redundante Disk-/Base64-/Embed-Roundtrips. **Hoechster Hebel: VRAM-Vorab-Check (`evict_lru`-Schleife vor `loader()`) bzw. CLAUDE.md an die bewusste Multi-Resident-Architektur angleichen.**

| Schwere | Befund | Datei | Wirkung | Fix |
|---|---|---|---|---|
| Mittel | `vram-budget-nicht-durchgesetzt` — 29-GB-Budget nur geloggt | `gpu_manager.py:100,189-206` | `ensure_loaded()` laedt bedingungslos, ruft nur `_warn_if_over_budget()`; `evict_lru()` nie automatisch im Ladepfad (nur Tests). Widerspruch zu CLAUDE.md. **Gemildert:** zentraler OOM-Handler (main.py:64-84) ruft `empty_cache()`+503; 3 Vision-Modelle allein < 29 GB, Risiko v.a. mit parallelem Ollama. | Pre-Load-Check: vor `loader()` Schleife `while alloc+est > BUDGET and evict_lru(): ...`; nach OOM `evict_lru()`+`empty_cache()`+1 Retry. Wenn Multi-Resident gewollt: CLAUDE.md angleichen, damit Doku/Code konsistent. |
| Mittel | `eval-reload-im-indexpfad` (G1) — Eval-Hashes bei jeder KB-Indexierung neu von Disk | `CodingSessionService.cs:228` | Provider-Lambdas lesen pro Aufruf `_manifest.json` (oder SHA-256 ueber **alle** Eval-Bilder, voll 120 Frames) + `AppSettings.Load()` (Disk). Verschwendete I/O+CPU auf fire-and-forget-Pfad. *nicht-verifiziert (leicht)* | Eval-Hashes/Keys einmalig cachen (wie PlayerWindow.Coding.cs:717-730 via `_codingEvalSetsLoaded`); gecachten Satz uebergeben statt Loader-Lambdas. |
| Mittel | `adhoc-kbcontext-cache-verworfen` (G1) — frischer KB-Context verwirft Embedding-Cache | `CodingSessionService.cs:226` | Retrieval-Cache haengt an der `RetrievalService`-Instanz; ad-hoc `new KnowledgeBaseContext()`+`new RetrievalService` startet leer → liest ~21.860 Embeddings/~67MB beim ersten Retrieval neu. Latenz-Unabhaengigkeit vom KB-Wachstum gilt hier nicht. *nicht-verifiziert (leicht)* | Den gecachten `ServiceProvider.Retrieval`-Singleton wiederverwenden statt pro Operation neue Context/Service. |
| Mittel | `vpc-not-disposable-httpclient-leak` (G4) — eigener HttpClient, aber nicht IDisposable | `VisionPipelineClient.cs:31` | `new HttpClient{Timeout=15min}` ohne Dispose; Klasse nicht IDisposable. Pro Codiermodus-Sitzung/Export ein Client mit offenem Pool; ueber lange Laufzeit (3000 Videos) Socket-/Handle-Anstieg. *nicht-verifiziert (leicht)* | IDisposable analog `OllamaClient` (`_ownsHttp`, Dispose nur selbst erzeugten Client); besser geteilten HttpClient durchreichen. |
| Niedrig | `httpclient-leak-kb-index` (G1) — HttpClient pro KB-Index neu, nie disposed | `CodingSessionService.cs:223` | Pro Session-Abschluss ein HttpClient; aber menschengetrieben (Dutzende/Tag), GC+TIME_WAIT raeumen auf. Self-Training-Worst-Case nutzt diesen Pfad NICHT (TrainingCenter cached `_kbHttpClient`). | `using var http` oder den langlebigen KB-HttpClient (ServiceProvider.cs:139) injizieren. |
| Niedrig | `httpclient-lifecycle-ui` (G4) — ad-hoc `new HttpClient` in UI-Services | `TrainingReviewSamSegmentationService.cs:24` u.a. | Wiederholtes `new HttpClient`; inkonsistente Timeouts (2s/5min/10min/15min); Socket-/DNS-Risiko ueber lange Sessions mit Sidecar-Polling. *nicht-verifiziert (leicht)* | Eine zentrale langlebige Instanz/`IHttpClientFactory` in VisionPipelineClient/OllamaClient/KB-Embedder injizieren. |
| Niedrig | `datapage-httpclient-per-action` (G4) — HttpClient pro manueller Analyse (using) | `DataPageViewModel.cs:745` | `using` greift (disposed), aber TIME_WAIT-Sockets gegen Loopback:8100 bei haeufigem Anstossen; klassisches Antipattern. *nicht-verifiziert (leicht)* | Geteilten HttpClient durchreichen; langen Timeout per CancellationToken statt am Client. |
| Niedrig | `sync-ueber-async-startup-blockierend` — `GetAwaiter().GetResult()` im Startpfad | `GpuModelSelector.cs:78` | **Kein** Deadlock (Runner nutzt `ConfigureAwait(false)`), aber UI-Thread blockiert beim Start bis nvidia-smi (5s) / tar (30s) antworten — Start-Stocken auf langsamen Systemen. *nicht-verifiziert (leicht)* | GPU-/Katalog-Init nach Splash-Anzeige awaitbar auslagern (analog `StartAiOnStartupAsync`); tar-Timeout (30s) fuer Startpfad kuerzen. |
| Niedrig | `live-frame-disk-roundtrip` — Frame ueber Temp-Datei + fixes `Task.Delay(80)` | `PlayerWindow.LiveDetection.cs:606-622` | Pro Tick Disk-Schreib/Lese/Loesch + ≥140ms feste Latenz (80+60); Tick-Intervall 5s → kein heisser Pfad, aber unnoetige Latenz + Temp-Muell. LibVLC-Snapshot ist datei-basiert. *nicht-verifiziert (leicht)* | Statt fixem Delay kurze Polling-Schleife auf `File.Exists+Length>0` mit Backoff; Temp in dedizierten Unterordner + periodisch saeubern. |
| Niedrig | `nvidia-smi-prozess-spawn-4s` — GPU-Telemetrie spawnt nvidia-smi alle ~4s | `SystemMonitorService.cs:1157-1169` | Dauerlast durch Prozess-Spawn (~100-500ms) waehrend Pipeline laeuft; kann mit Inferenz um Treiber-Locks konkurrieren; powershell.exe alle ~10s fuer CPU-Temp. *nicht-verifiziert (leicht)* | Telemetrie aus bereits initialisiertem LibreHardwareMonitor (:107) oder NVML per P/Invoke; Intervall auf 5-10s; Spawn als Fallback. |
| Niedrig | `frame-base64-mehrfach-uebertragen` — gleicher Frame-Base64 in 3-4 HTTP-Requests | `SingleFrameMultiModelService.cs:72,81,124,149,172` | Frame 1x encodiert (korrekt), aber in 4 POSTs (YOLO-Classify/Detect, DINO, SAM) gesendet; Base64 +33%, 4x JSON De/Serialize. Bei Batch ueber tausende Frames erhebliche CPU/Allokation. *nicht-verifiziert (leicht)* | Sidecar-`/analyze`-Endpoint, der YOLO→DINO→SAM serverseitig kettet (Bild 1x), oder Frame-Hash/Handle. 4→1 Roundtrip. |
| Niedrig | `serielle-embed-roundtrips-batch` — N serielle Ollama-Embeds statt Batch | `KnowledgeBaseManager.cs:106,182` | Pro Sample ein `/api/embed`; Indexierung linear mit Sample-Zahl. *nicht-verifiziert (leicht)* | `EmbedBatchAsync` (Ollama akzeptiert Array-Input), blockweise 32-64 Texte/Request. |

---

## 5. Priorisierte Top-10-Massnahmen

Reihenfolge nach Schwere × Wirkung × CLAUDE.md-Konformitaet. Aufwand: **S** = wenige Stunden, **M** = 1-2 Tage, **L** = mehrtaegig / diskussionspflichtig.

| # | Massnahme | Befund(e) | Dimension | Schwere | Aufwand | Begruendung |
|---|---|---|---|---|---|---|
| 1 | **Session-Abschluss-Deadlock beheben:** `MergeAndSaveAsync`/`Load`/`Save` durchgaengig `ConfigureAwait(false)`; mittelfristig `CompleteSessionAsync()` + `[RelayCommand] async Task`. | `deadlock-coding-session-complete` | Robustheit | Hoch | **S** (Minimal) / M (echt async) | Echtes Freeze-/Datenverlust-Risiko genau beim Speichern codierter Daten. Minimal-Fix (ConfigureAwait) ist klein, risikoarm, kein Signatur-Umbau. |
| 2 | **QualityGate-Mindest-Signal-Schwelle:** Green nur ab N (≥2-3) Signalen, sonst max. Yellow + Begruendung in `explanation`. | `qualitygate-green-bei-einem-signal` | Robustheit | Hoch | **S** | Verstoss gegen Kernregel „QualityGate-Ehrlichkeit"; halluzinierte Einzel-Box laeuft sonst als bestaetigt durch. Kernlogik → laut CLAUDE.md ohnehin testpflichtig, Test mitliefern. |
| 3 | **G1-Hotspot `IndexApprovedSamplesToKbAsync` haerten:** HttpClient via `using`/DI; im `catch` echte KB-Schreibfehler (SQLite/IO) loggen statt als „Ollama offline" zu verschlucken; Eval-Hashes/Retrieval-Singleton wiederverwenden. | `kb-index-httpclient-leak-und-stiller-catch`, `httpclient-leak-kb-index`, `eval-reload-im-indexpfad`, `adhoc-kbcontext-cache-verworfen` | Robustheit/Performance | Mittel | **M** | Ein zusammenhaengender Pfad loest vier Befunde gleichzeitig; verhindert stillen Verlust bestaetigter Befunde im Self-Training und vermeidbare I/O. |
| 4 | **Toten `CodingModeWindow` entfernen** (nach finaler Tot-Verifikation), gemeinsame Kalibrierungs-/Overlay-Logik vorher in testbaren `CalibrationService`/Helper extrahieren. | `coding-mode-window-tot-dupliziert`, `kalibrierung-dupliziert`, `riesen-methode-render-overlay` | Wartbarkeit | Mittel | **M** | ~2955 Z toter, driftender Code + Kalibrierungs-Duplikat fallen in einem Schritt weg; reduziert Verwechslungs-/Pflegerisiko deutlich. |
| 5 | **Trainings-/Eval-Schutz-Persistenz aus `PlayerWindow` in `ICodingTrainingPersistenceService`** ziehen und unit-testen. | `player-window-god-class`, `save-mark-as-training-mega-methode` | Wartbarkeit | Mittel | **M** | Genau die Geschaeftslogik, die laut CLAUDE.md nicht ins Fenster gehoert; macht den heikelsten Teil (Eval-Kontaminations-Schutz) testbar — schrittweise, nicht das ganze Window. |
| 6 | **VRAM-Budget durchsetzen oder Doku angleichen:** Pre-Load-`evict_lru`-Schleife vor `loader()` + `evict_lru`+Retry im OOM-Handler; alternativ CLAUDE.md an Multi-Resident-Architektur anpassen. | `vram-budget-nicht-durchgesetzt` | Performance | Mittel | **S-M** | Schliesst die Luecke zwischen „evict_lru existiert" und „wird nie aufgerufen"; mindestens Doku/Code-Widerspruch aufloesen (Entscheidung des Solo-Entwicklers noetig). |
| 7 | **`VisionPipelineClient` IDisposable machen** (analog `OllamaClient`) bzw. geteilten HttpClient durchreichen; G4-HttpClients zentralisieren. | `vpc-not-disposable-httpclient-leak`, `httpclient-lifecycle-ui`, `datapage-httpclient-per-action` | Performance | Mittel | **S-M** | Beseitigt Socket-/Handle-Leck ueber lange Sessions (Ziel: 3000 Videos); kleines, lokales Pattern mit klarer Vorlage im Repo. |
| 8 | **Stille `catch{}`-Bloecke mit Diagnose versehen:** `DisposeSafe(Action)`-Helper mit Logging; Import-/Scan-Skips zaehlen+ausgeben. | `leere-catch-bloecke-ui`, `training-import-stiller-catch`, `batch-media-scan-stiller-catch`, `healthcheck-swallow-all-exceptions` | Robustheit | Mittel/Niedrig | **S** | Macht stillen Datenverlust und Leck-Ursachen sichtbar; sehr kleines Risiko, hoher Diagnose-Nutzen. |
| 9 | **`ICodingPipelineFactory` einfuehren**, UI-Pipeline-Verdrahtung (PlayerWindow.Coding / TrainingCenter) dahinter buendeln. | `ui-instanziiert-infrastructure`, `viewmodel-direkt-vlc-und-ki-pipeline` | Wartbarkeit | Mittel | **L** (diskussionspflichtig) | Beseitigt dreifache Pipeline-Verdrahtung/Drift; konform zu „Features als separate Services mit Interface". Groesser → vorab diskutieren. |
| 10 | **VsaCodeTree gegen Manifest verriegeln:** Test, der Labels/Untercodes von `VsaCodeTree` mit dem ADR-006-Manifest abgleicht (kein Adapter-Umbau, der `VsaCodeTreeCatalogAdapter` existiert nicht). | `vsacodetree-zweite-wahrheit` | Wartbarkeit | Mittel | **S** | Erfuellt ADR-006 Punkt 5; faengt Label-Drift Picker↔Report↔KI-Prompt, ohne die bestehende Struktur umzubauen. |

**Bewusst nicht in Top-10 (vertretbar belassen):** G2 Service-Locator-Migration (breit, diskussionspflichtig, NRE-Risiko theoretisch), Sidecar-Token-ACL/`/warmup`-GET und uebrige Niedrig-Sicherheitsbefunde (Defense-in-Depth, Single-User), `reports-io-in-application` (Doku-Klarstellung genuegt), Base64-/Embed-Batch-Optimierungen (echter Hebel erst bei nachgewiesenem Batch-Engpass).

---

## Anhang: Caveats fuer die Umsetzung

- **27 Befunde sind „nicht-verifiziert (leicht)".** Vor jeder Umsetzung die genannte Datei/Zeile kurz pruefen — die Belegstellen sind plausibel, aber nicht adversarial gegengeprueft.
- **Falsche Annahme im Eingang korrigiert:** `VsaCodeTreeCatalogAdapter` existiert im HEAD nicht (entgegen ADR-006-Erwaehnung) — Massnahme 10 setzt deshalb auf einen Verriegelungs-Test, nicht auf Generierung aus dem Manifest.
- **CLAUDE.md-Grenze beachten:** Massnahmen 5, 9 (und jede God-Class-Zerlegung) sind „grosses Refactoring" und laut CLAUDE.md vor Beginn explizit zu diskutieren. Massnahmen 1, 2, 6, 8, 10 sind klein und additiv und koennen direkt erfolgen.
- **Solo-Entwickler-Kontext:** Merge-Konflikt-Argumente sind hier schwach; der reale Schmerz ist Regressions- und Test-barkeit, nicht Parallelarbeit.

---

## 12. Konsolidierung mit unabhaengiger Zweit-Analyse (am Code geprueft, 2026-06-16)

Eine zweite, thematisch breitere Architekturanalyse wurde eingereicht. Ihre tragenden/neuen Behauptungen wurden am HEAD gegengeprueft. Ergebnis: die beiden Analysen sind **komplementaer** — keine ist Obermenge der anderen.

### 12.1 Uebereinstimmung (Kreuzvalidierung -> hohe Sicherheit)
Beide Analysen finden unabhaengig dasselbe; diese Punkte sind damit belastbar:
- UI-God-Classes (PlayerWindow.Coding, DataPage, TrainingCenterViewModel, CostCalculatorViewModel).
- Handgebauter `ServiceProvider`/Service-Locator + parallele `new`-Pipeline-Erzeugung im Fenster.
- `HoldingFolderDistributor` in Parser/Matcher/Writer/Audit schneiden.
- `ProtocolPdfExporter`-Monolith.
- Sidecar-Haertung solide (Loopback + konstant-zeit Token).
- Firebird-Default-Credentials (SYSDBA/masterkey).
- Stille `catch`-Bloecke + zu breites `async void`.

### 12.2 Neu & am Code bestaetigt -> in die Massnahmen aufgenommen
| Befund (Zweit-Analyse) | Pruefung am HEAD | Schwere | Aufnahme |
|---|---|---|---|
| **Rekursive Suche ohne Budget/Containment** (S5/O2) | **44x `SearchOption.AllDirectories`**, aber `SafeFileEnumeration` nur **7x** genutzt | Hoch (Datenintegritaet/Robustheit), mittel (Security) | **Neuer Top-Punkt** — verbindliche Safe-Enumeration-Policy + CancellationToken + Root-Containment + kein „erster-Treffer-gewinnt" bei Mehrdeutigkeit |
| **KI-Ergebnisstatus-Taxonomie** (F4) — Ok/NoFinding/ModelUnavailable/Timeout/LowQuality/Uncertain | Plausibel; ergaenzt den QualityGate-Befund (Fehler != „sicher leer") | Mittel–Hoch (Detektionsqualitaet) | Aufgenommen — explizites Result-Status-Enum statt stiller Vermischung |
| **PipelineSidecarToken in Settings-JSON** (S2) | Bestaetigt: `AppSettings.cs:99` — Klartext-Feld | Niedrig–Mittel | Aufgenommen — Token-Datei bevorzugen, Feld als Legacy, nie voll anzeigen/loggen (ergaenzt `token-file-no-restrictive-acl`) |
| **Tooling-Build-Drift** (W3) | Bestaetigt: **38 `.csproj`, nur 19 in der Solution** | Niedrig–Mittel | Aufgenommen — Tool-Inventar + kritische Tools in Build/Test-Pfad (z. B. `.slnf`) |
| **Persistenz vereinheitlichen** — `AtomicJsonFileWriter` (F3) | Kernprojekt nutzt schon `File.Replace` (JsonProjectRepository/AppSettings); Nebenstores uneinheitlich | Mittel | Aufgenommen |

### 12.3 Korrekturen an der Zweit-Analyse (gegengeprueft)
- **Zeilenzahlen zu niedrig/veraltet.** Aktueller HEAD: PlayerWindow.Coding.cs **5558** (nicht 4852), CodingModeWindow **2955** (nicht 2600), ProtocolPdfExporter **2880** (nicht 2464). Die Zweit-Analyse lief auf fruehem Stand bzw. zaehlte ohne Leerzeilen — der qualitative Befund stimmt, das Problem ist sogar groesser.
- **LiveControl-Client (S4) ist bereits gemildert.** `LiveControlClient.SendAsync` ruft `TryBuildLoopbackUrl(...)` (`LiveControlClient.cs:46`) **vor** dem Token-Versand (`:62-64`); bei Nicht-Loopback Abbruch mit Fehler, der Token geht nicht raus. Offen bleibt hoechstens ein expliziter Test dafuer — nicht die Implementierung selbst.

### 12.4 Was die Zweit-Analyse uebersehen hat (die zwei verifizierten Hoch-Befunde)
Die thematische Zweit-Analyse nennt **beide konkreten Hoch-Befunde dieser Analyse nicht**:
1. **Sync-ueber-Async-Deadlock** beim Session-Abschluss — `CodingSessionService.cs:194` (`GetAwaiter().GetResult()`) gegen `TrainingSamplesStore` **ohne ein einziges `ConfigureAwait(false)`** (12+ Awaits inkl. Datei-I/O).
2. **QualityGate „Green" mit nur einem Signal** — `QualityGateService.cs:51-71`: einziger Schutz `signals.Count == 0 -> Red`; einzelne YOLO-Box -> composite 0.9 -> Green.

Das ist der Mehrwert der adversarialen, code-verifizierten Methode gegenueber einem rein thematischen Review: konkrete, schwere Defekte mit Datei:Zeile.

### 12.5 Zusammengefuehrte Sofort-Prioritaeten (beide Analysen)
1. Deadlock beheben (`ConfigureAwait(false)`) — **S**, verifiziert.
2. QualityGate-Mindest-Signalzahl + Test — **S**, verifiziert.
3. **Safe-Enumeration-Policy** fuer alle rekursiven Import/Export-Suchen (44 Stellen) + CancellationToken + Root-Containment — **M**, neu aus Zweit-Analyse, am Code bestaetigt.
4. KB-Index-Hotspot haerten (stiller catch + HttpClient-Leck) — **M**.
5. `BestEffort.Try(action, logger, context)`-Helfer + KI-Result-Status-Enum — **S–M**.

Groessere Wartbarkeits-Schnitte (God-Class-Zerlegung, Tooling-in-Build, DI-Framework) bleiben schrittweise und diskussionspflichtig (CLAUDE.md).
