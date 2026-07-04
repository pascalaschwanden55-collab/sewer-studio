# Architektur- & Code-Qualitaetsbericht — SewerStudio (Stand cda3fc06)

## Executive Summary

**Gesamtnote: B (solide, mit klar umrissenen Wartbarkeitsschulden)**

SewerStudio ist nach der grossen 3-Linien-Merge- und Dekompositions-Kampagne in einem ehrlich guten Zustand. Die 4-Schichten-Architektur haelt strikt (keine einzige Aufwaerts-Verletzung ueber 256 Infrastructure- und 189 Application-Dateien), Thin-AI ist vorbildlich umgesetzt (der Sidecar-Client traegt keine Geschaeftslogik), und die Robustheit der Kernpfade (Persistenz, KI-Pipeline, Cancellation, SQLite-Concurrency) ist durch mehrere Audit-Runden sichtbar gehaertet. Die Fachdomaene (VSA-KEK-Mapping, framebasiertes Dedup, QualityGate, Streckenschaden-Logik) ist intakt und durch Charakterisierungs-Tests abgesichert. Die Schwaechen sind durchgaengig Wartbarkeits- und Konsistenz-Themen, kein einziger Critical- oder echter High-Befund: die Codex-Dekomposition ueber-fragmentiert (73 Mikro-Klassen, davon 22 unter 1KB) und legt reine Logik in die UI-Schicht statt nach Application, der Merge hinterliess mehrere tote Konkurrenz-Implementierungen, ein Disposal-Muster ist inkonsistent (drei Page-VMs leaken), und ~264 brittle Quelltext-String-Guards verwaessern die Test-Kennzahl. Nichts davon bedroht Stabilitaet oder Datenintegritaet — die Codebasis ist wartbar, ehrlich und gut getestet, nur ungleichmaessig poliert.

## Bewertung je Dimension

| Dimension | Note | Kernaussage |
|---|---|---|
| Schichtenarchitektur & Abhaengigkeiten | B+ | 4 Schichten strikt eingehalten, keine Aufwaerts-Verletzung; Thin-AI vorbildlich. Schwaeche: 69 reine Logik-Controller liegen in UI statt Application. |
| Dekompositions-Qualitaet | B | Verhaltensneutral, exzellent getestet, aber ueber-fragmentiert; Komplexitaet teils nur in Callback-Baeume verschoben; ungleich verteilt (Training tief, CostCalculator unberuehrt). |
| Kohaerenz & toter Code (3-Linien-Merge) | B | Insgesamt sauber verdrahtet; lokal begrenzte tote Konkurrenz-Duplikate rund um DataPage/Schaechte, deren Application-Versionen den Merge verloren. |
| Robustheit & Fehlerbehandlung | A- | Persistenz, Pipeline, Cancellation, SQLite-Concurrency vorbildlich gehaertet; nur Politur-Punkte (stiller catch, sync-over-async). |
| Test-Qualitaet | B- | Kern verhaltensbasiert und breit; aber ~264 brittle Quelltext-String-Guards + eine 9411-Zeilen-Monolith-Testdatei verwaessern die Kennzahl. |
| Domaenen-Korrektheit (VSA/Pipeline) | A- | Fachdomaene intakt und ehrlich modelliert; Invarianten stimmen; einzige Medium-Schwaeche: zwei divergierbare Streckenschaden-Definitionen. |
| UI/MVVM & WPF-Mechanik | B | Saubere MVVM-Trennung, gute Thread-Hygiene; aber inkonsistentes Disposal (3 Page-VMs leaken), statischer Bridge-Root, UI-Thread-blockierende Frame-Extraktion. |

## Staerken (was solide ist)

- **Strikte Schichtdisziplin.** Projekt-Referenzen exakt korrekt (Domain referenzlos, Application->Domain, Infrastructure->Domain+Application, UI->alle). Keine einzige Aufwaerts-Referenz auf Code-Ebene ueber die gesamte Codebasis. Domain ist real dependency-frei (nur `System.Text.Json`-Attribute, kein IO/WPF/HTTP).
- **Thin-AI konsequent.** `VisionPipelineClient` ist reiner typisierter HTTP-Transport hinter `IVisionPipelineClient`, ohne VSA-/Codier-Geschaeftslogik. Die Sidecar-Grenze ist sauber gekapselt.
- **Erstklassige defensive Persistenz.** `TrainingSamplesStore` nutzt Semaphore-Lock, atomares temp+rename, rotierende Backups und gestaffeltes Korruptions-Recovery statt Datenverlust.
- **Ehrliche KI-Pipeline.** `MultiModelAnalysisService` isoliert jede Sidecar-Stufe in try/catch; `degraded=true` wird NICHT als sauberer Negativbefund verbucht (verhindert falsche "sauberes Rohr"-Befunde). `QualityGateService` haelt die Ehrlichkeits-Invariante (Renormalisierung nur ueber vorhandene Signale, `MinSignalsForGreen`-Deckel gegen einzelne halluzinierte Evidenz).
- **Korrekte Concurrency.** Jeder Konsument oeffnet eine eigene `KnowledgeBaseContext`-Verbindung (WAL + busy_timeout), Cancellation wird durchgaengig von echten Fehlern unterschieden, WPF-Threading ist sauber per Dispatcher marshalliert.
- **Verhaltensneutrale, gut getestete Dekomposition.** 102 Test-Dateien fuer die Training-Controller mit echten Edge-Case-Tests; konsistente Namensgebung; jeder extrahierte Controller hat eine echte Verhaltenstest-Datei.
- **Intakte Fachdomaene.** Framebasiertes Dedup, majority-window Voting, Streckenschaden-Zustandslogik und die ortsgebundenen Grundgeruest-Regeln (BCD nur am Anfang, BCE nur am Ende) sind praezise modelliert und voll getestet.
- **Saubere MVVM-Trennung.** Kein einziges `MessageBox.Show` in ViewModels; Dialoge ueber injizierte Services; `BuilderPageViewModel` existiert bereits als korrekte Referenzimplementierung fuer sauberes Disposal.

## Priorisierte Befunde

Es wurden keine Critical- oder High-Befunde bestaetigt. Alle gelisteten Befunde stammen aus der strukturierten Analyse; die adversariale Pruefung hat keinen Befund widerlegt (keine `refuted`-Verdikte). Gruppierung nach Severity:

### High
Keine.

### Medium

**DEC-1 / LAYER-1 — Ueber-Fragmentierung + reine Logik in der UI-Schicht**
*Dimension: Dekomposition / Schichtenarchitektur · Severity: Medium*
*Datei: `src/AuswertungPro.Next.UI/Ai/Training/` (z.B. `SelfTrainingKbUpdateController.cs`, `SelfTrainingRunFinalizerController.cs`)*
Von 73 Dateien sind 69 ohne jede WPF-Abhaengigkeit (reine Entscheidungsregeln/Zustandsmaschinen) und gehoeren konzeptionell nach Application — sie liegen aber im Praesentations-Projekt und sind dort weder wiederverwendbar noch ohne UI-Assembly testbar. Gleichzeitig sind 22 Dateien unter 1KB und 68 stateless-static; mehrere kapseln nur einen trivialen Drei-Zeiler (reine Indirektion). Alle liegen im selben flachen Namespace.
*Empfehlung:* Nicht-WPF-Controller schrittweise (verhaltensneutral) nach `Application/Ai/Training` verschieben; triviale Finalizer/Refresher inline zuruecknehmen; den Ordner in Unter-Namespaces (`SelfTraining/`, `BatchImport/`, `ReviewQueue/`, `Presentation/`) gliedern. Faustregel als Konvention festhalten: Datei ohne `System.Windows`-Bezug = Application-Kandidat.

**DEC-2 — Callback-Threading: Komplexitaet verschoben statt reduziert**
*Dimension: Dekomposition · Severity: Medium*
*Datei: `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs:872`*
`BatchImportAndIndexAsync` und `SelfTrainingRunAsync` wurden nicht verschlankt, sondern in Baeume aus 10-20 Lambda/Action/Func-Parametern pro Aufruf umgeschrieben (`ProcessAsync` nimmt 19 Parameter, 15 davon Delegates). Die Daten-/Zustands-Kopplung bleibt voll erhalten und wird als fehleranfaellige lange Parameterlisten gleichartiger `Action<string>/Action<int>`-Signaturen sichtbar.
*Empfehlung:* Ein kleines getyptes Sink-/Kontext-Objekt (`record TrainingBatchUiSink { Log; SetStatus; SetProgress; ... }`) einfuehren und durchreichen — reduziert 15-Parameter-Signaturen auf 2-3 und macht die Reihenfolge irrelevant. Alternativ einen stateful Application-Service mit `IProgress`/Events.

**DEC-3 — Verbliebene God-Klasse `CostCalculatorViewModel` von der Kampagne unberuehrt**
*Dimension: Dekomposition · Severity: Medium*
*Datei: `src/AuswertungPro.Next.UI/ViewModels/Windows/CostCalculatorViewModel.cs:22`*
1595 Zeilen, drei VM-Klassen in einer Datei, echte Domaenenlogik direkt im VM (`RunConsistencyCheck`, `ApplyCatalogPrices`, `SetDnFromImport/SetLengthFromImport/SetConnectionsFromImport`). Waehrend der Training-Bereich erschoepfend zerlegt wurde, blieb die Kostenrechnung klassische God-Klasse — inkonsistenter Architektur-Stil und ungetestete Domaenenlogik im VM.
*Empfehlung:* Konsistenz herstellen: Konsistenz-Pruefung und Mengen-/Preis-Berechnung als testbare Application-Services (`CostConsistencyChecker`, `MeasureQuantityDeriver`) extrahieren, dieselben Muster wie im Training-Bereich.

**COH-1 — Toter Konkurrenz-Parser: `DnValueParser` (Application) wird nie aufgerufen**
*Dimension: Kohaerenz/Merge · Severity: Medium*
*Datei: `src/AuswertungPro.Next.Application/DataPage/DnValueParser.cs:12`*
`DnValueParser.TryParseMillimeters` hat null produktive Aufrufer; die identische Logik laeuft produktiv ueber `DataPageHydraulikReportCalculator.ParseDnMm` (UI). Merge-Artefakt: die Application-Version (richtig platziert) verlor die Verdrahtung, ein Charakterisierungstest haelt die Waise scheinbar lebendig.
*Empfehlung:* `ParseDnMm` nach `Application/DataPage/DnValueParser` ziehen (bzw. den UI-Calculator darauf umstellen), das Duplikat loeschen, den Test auf die genutzte Implementierung umbiegen.

**COH-2 — Toter Konkurrenz-Code: `SchaechteFieldLogic` (Application) komplett verwaist**
*Dimension: Kohaerenz/Merge · Severity: Medium*
*Datei: `src/AuswertungPro.Next.Application/DataPage/SchaechteFieldLogic.cs:13`*
Die Such-/Nr-Spalten-Logik existiert dreifach: Application-Waise + UI-Service `SchaechteSearchMatcher` + inline `ResolveNrColumnName` in `SchaechtePageViewModel.cs:375`. Zwei divergente Merge-Linien loesten dieselbe Zerlegung unterschiedlich; produktiv laeuft die UI-Variante.
*Empfehlung:* Auf `SchaechteFieldLogic` (korrekt in Application platziert) als Single Source of Truth konsolidieren, `SchaechteSearchMatcher` und die inline-Methode darauf umleiten/loeschen, Tests zusammenfuehren.

**COH-3 — Doppelte Klasse `SchaechteTemplateColumnReader` — `Import.Xlsx`-Variante vollstaendig unreferenziert**
*Dimension: Kohaerenz/Merge · Severity: Medium*
*Datei: `src/AuswertungPro.Next.Infrastructure/Import/Xlsx/SchaechteTemplateColumnReader.cs:13`*
Zwei gleichnamige Klassen in verschiedenen Namespaces; produktiv genutzt wird nur die `Export.Excel`-Version. Die `Import.Xlsx`-Version hat null Referenzen (nicht einmal ihr eigener Test) und ist zudem verhaltensabweichend (ihr fehlt `SwapColumnOrder`).
*Empfehlung:* Die unreferenzierte Datei ersatzlos loeschen — risikolos.

**DOM-1 — Zwei parallele `IsStreckenschadenCode`-Implementierungen koennen divergieren**
*Dimension: Domaenen-Korrektheit · Severity: Medium*
*Datei: `src/AuswertungPro.Next.Domain/VsaCatalog/StreckenschadenCodeClassifier.cs:15`*
Zwei unabhaengige Quellen: Domain `StreckenschadenCodeClassifier` (katalog-blinde HashSet, nur test-relevant) und Infrastructure `VsaCodeResolver.IsStreckenschadenCode` (Katalog-`RequiresRange` + zweite, separat gepflegte HashSet). Alle Live-Konsumenten nutzen die Infrastructure-Variante. Aktuell inhaltsgleich, aber getrennt gepflegt — eine einseitige Aenderung laesst die Definitionen lautlos auseinanderlaufen (entscheidet, ob ein Befund als Strecke A..B oder als Punktschaden gefuehrt wird).
*Empfehlung:* Eine Quelle der Wahrheit etablieren (Domain-Classifier kanonisch, `VsaCodeResolver` nach der `RequiresRange`-Pruefung dorthin delegieren) plus Konsistenz-Test ueber alle Katalog-Codes.

**TQ-1 — 264 Tests pruefen Quelltext-Strings statt Verhalten (brittle Refactor-Guards)**
*Dimension: Test-Qualitaet · Severity: Medium*
*Datei: `tests/AuswertungPro.Next.UI.Tests/TrainingCenterBatchImportArchitectureTests.cs:11`*
~264 Facts lesen Produktiv-Quelltext, schneiden per Klammerzaehlung Methodenrumpf heraus und pruefen auf woertliche Code-Fragmente (2162 `DoesNotContain`-Asserts). Sie sichern kein Laufzeitverhalten, brechen bei jeder verhaltensneutralen Umbenennung und zementieren Implementierungsdetails — kuenstliche Wartungslast.
*Empfehlung:* Da die ausgelagerten Controller bereits echte Verhaltenstests haben, sind diese String-Guards eine redundante Schicht — im Zweifel loeschen. Wo Verdrahtung getestet werden soll: VM-Methode mit Fakes aufrufen und beobachtbare Wirkung zusichern.

**TQ-2 — Eine 9411-Zeilen-Guard-Datei mit 208 Facts und 4075 String-Asserts**
*Dimension: Test-Qualitaet · Severity: Medium*
*Datei: `tests/AuswertungPro.Next.UI.Tests/UiArchitectureGuardTests.cs:1`*
Monolith-Testdatei, die wertvolle Architektur-Fitness-Tests (Service-Locator-Verbot ueber alle Dateien) mit hunderten brittler Delegations-Pins vermischt. Die Groesse erschwert Pflege und Review; die guten Tests drohen im Rauschen unterzugehen.
*Empfehlung:* Echte, refactor-robuste Fitness-Funktionen (Service-Locator-Verbot, Layer-Checks, ggf. Threading per Roslyn) in eine kleine `ArchitectureFitnessTests`-Datei extrahieren und behalten; die methodenrumpf-basierten Delegations-Guards aussondern. Falls Threading-Invarianten getestet werden sollen, auf Roslyn-Syntaxanalyse statt naiver Substring-Suche umstellen.

**UI-1 — Inkonsistentes Disposal-Muster bei Page-ViewModels verursacht Memory-Leaks**
*Dimension: UI/MVVM · Severity: Medium*
*Datei: `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:152` (auch `OverviewPageViewModel:55`, `ProjectPageViewModel:87`)*
Drei Page-VMs abonnieren `_shell.PropertyChanged` mit anonymen Lambdas und implementieren kein `IDisposable`. Da `ShellPageLifecycle.DisposeIfReplaced` nur `IDisposable`-VMs aufraeumt, bleibt jede weggeblaetterte Instanz ueber die Invocation-List des langlebigen Shell-Singletons am Leben — inklusive Collections, Timer, Commands; ihre Handler feuern danach weiter. `BuilderPageViewModel` macht es an gleicher Stelle korrekt.
*Empfehlung:* Auf das `BuilderPageViewModel`-Muster vereinheitlichen: `IDisposable` implementieren, benannter Handler, Unsubscribe in `Dispose()`. `DisposeIfReplaced` raeumt dann automatisch auf.

**UI-2 — Statischer `LiveControlRetryBridge` rootet das letzte DataPageViewModel**
*Dimension: UI/MVVM · Severity: Medium*
*Datei: `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:169`*
Der Konstruktor ruft `LiveControlRetryBridge.Register(...)`; die static-Klasse haelt einen `Func<>` auf die zuletzt erzeugte Instanz. `Reset()` existiert, wird produktiv aber NIE aufgerufen — das statische Feld ist ein dauerhafter GC-Root, und ein per Live-Control angestossener Retry kann Videoanalyse auf einer laengst verlassenen Seiteninstanz starten. Der Kommentar "nur wenn diese Seite lebt" wird nicht erzwungen.
*Empfehlung:* `DataPageViewModel` `IDisposable` machen (siehe UI-1) und in `Dispose()` `LiveControlRetryBridge.Reset()` aufrufen (nur wenn der registrierte Handler der eigene ist).

**UI-3 — Sync-over-async ffmpeg-Frame-Extraktion blockiert den UI-Thread**
*Dimension: UI/MVVM · Severity: Medium*
*Datei: `src/AuswertungPro.Next.UI/Ai/CodingFrameExtractionService.cs:49`*
`TryExtractFrameAtSeconds` ruft `TryExtractFramePngAsync(...).GetAwaiter().GetResult()` direkt aus UI-nahen Handlern (`PlayerWindow.Coding.Photos.Capture`, `CodingBoundaryEventWorkflow`) — anders als `SystemMonitorService` nicht ueber `Task.Run`. Ein ffmpeg-Prozessstart blockiert den Dispatcher genau im interaktiven Codier-Hotpath (Foto-Greifen, BCD/BCE-Grenzereignisse).
*Empfehlung:* Den Pfad async durchziehen (`TryExtractFrameAtSecondsAsync` + `await` in den Aufrufern) oder die Extraktion per `Task.Run` auslagern und das Ergebnis ueber den Dispatcher zuruecksetzen — analog zu `SystemMonitorService`.

### Low

- **LAYER-2 — Application kennt Transport-Details** (`Application/Ai/EnhancedVisionModels.cs:41`): `FromException()` inspiziert `HttpRequestException`/`SocketException`. Transport-Wissen sickert in die Application. *Empfehlung:* Im `VisionPipelineClient` in einen neutralen Fehlertyp (`SidecarUnavailableException`) abbilden — oder bewusst als Vereinfachung dokumentieren.
- **LAYER-3 — VMs instanziieren Infrastructure per `new()`** (`DataPageViewModel.cs:853` u.a.): Harte Kopplung an konkrete Typen erschwert Mocking. Konsistent mit der bewussten "kein MS-DI"-Entscheidung. *Empfehlung:* Wo VM-Orchestrierung getestet werden soll, Interface ueber Konstruktor injizieren (Default-Parameter = konkrete Impl).
- **LAYER-4 — Bildlogik an WPF-Imaging gekoppelt** (`Ai/Training/TechniqueAssessmentService.cs:7`): nutzt `System.Windows.Media.Imaging`, kann nicht ohne Desktop-Stack wandern. *Empfehlung:* Dekodierung (framework-neutral) von reiner Bewertung trennen.
- **DEC-4 — Doppelter XML-Doc-Block** (`TrainingCenterViewModel.cs:1444`): Merge-Artefakt, zwei `<summary>`-Bloecke. *Empfehlung:* Konsolidieren.
- **DEC-5 — `PhotoMeasurementWindow` Code-Behind bleibt 1516 Zeilen** (`Views/Windows/PhotoMeasurementWindow.xaml.cs:22`): Geometrie korrekt ausgelagert, aber Rest-Mathematik (Hit-Testing, Kalibrierung) noch inline und untestbar. *Empfehlung:* Restberechnungen in den `GeometryService` ziehen und testen.
- **COH-4 — Gewinnende Logik-Versionen sitzen in UI statt Application** (`UI/DataPage/DataPageHydraulikReportCalculator.cs:15`): Folge von COH-1/COH-2; im Zuge der Konsolidierung die behaltene Logik nach Application ziehen.
- **ROB-1 — Vollstaendig stiller catch beim Weight-Learning** (`FeedbackIngestionService.cs:106`): inkonsistent zum benachbarten `Debug.WriteLine`-catch. *Empfehlung:* `Debug.WriteLine` ergaenzen.
- **ROB-2 — Sync-over-async im Session-Abschluss** (`CodingSessionService.cs:194`): aktuell deadlock-frei (Infrastructure ohne SynchronizationContext), aber latentes Risiko bei kuenftiger UI-Thread-Nutzung. *Empfehlung:* Voll async machen oder dokumentieren/`Task.Run`-entkoppeln.
- **ROB-3 — `Convert.ToDouble` im FDB-Reader ohne Kultur-Absicherung** (`KiasFdbTopologyReader.cs:144`): bei unerwartetem Typ kippt der ganze Stammdaten-Block statt nur einer Zeile. *Empfehlung:* `CultureInfo.InvariantCulture` + defensives `TryConvert` pro Feld.
- **TQ-3 — `ExtractMethodBody`-Helfer dupliziert, naive Klammerzaehlung** (`DataPageCommandArchitectureTests.cs:45`): fragile Test-Infrastruktur, kann bei Klammern in String-Literalen falsch schneiden. *Empfehlung:* Mit TQ-1/TQ-2 entfernen; falls Guards bleiben, auf Roslyn umstellen.
- **DOM-2 — `TemporalCodeVotingService.Reset()` setzt `_lastConfirmedMeter` nicht zurueck** (`TemporalCodeVotingService.cs:83`): aktuell harmlos (Hysterese prueft zuerst `_lastConfirmedCode`), aber unvollstaendiger Reset. *Empfehlung:* `_lastConfirmedMeter = 0` ergaenzen.
- **DOM-3 — Severity-Policy erreicht 5 nur ueber Querschnittsverringerung** (`QuantificationSeverityPolicy.cs:11`): massive Einragung/Ausdehnung deckelt bei 4. Moegliche Unterbewertung kritischer Befunde. *Empfehlung:* Eskalation auf 5 bei sehr hoher `intrusionPercent`/`extentPercent` fachlich pruefen (keine Code-Aenderung ohne Ruecksprache) oder Grenze kommentieren.
- **DOM-4 — `BBD` als Strecken-Praefix widerspricht CLAUDE.md "kein Basiscode BBD"** (`VsaCodeResolver.cs:397`): fachlich vertretbar (nur Praefix-Anker fuer BBDA/BBDB), aber latenter Stolperstein. *Empfehlung:* Eintrag entfernen oder Kommentar "nur Praefix-Anker, kein gueltiger Basiscode" ergaenzen.
- **UI-4 — Inline-Style-Aufbau im DataPage-Spaltenbau** (`Views/Pages/DataPage.xaml.cs:147`): durchbricht das sonst konsequente Factory-Muster. *Empfehlung:* In `DataGridWrappingTextColumnFactory` auslagern.
- **UI-5 — Blockierendes `Dispatcher.Invoke` statt `BeginInvoke`** (`VsaCodeExplorerWindow.xaml.cs:249`): einzige blockierende Invoke-Stelle, aktuell harmlos, latente Deadlock-Falle. *Empfehlung:* Auf `BeginInvoke` bzw. die `IUiThread`-Abstraktion umstellen.

## Quick Wins (geringe Muehe, hoher Nutzen)

1. **COH-3: `Import.Xlsx/SchaechteTemplateColumnReader.cs` loeschen.** Null Referenzen, verhaltensabweichend — risikoloses Loeschen, entfernt Verwirrung.
2. **UI-1 + UI-2 in einem Zug.** `DataPageViewModel` `IDisposable` machen, `_shell.PropertyChanged` abmelden und `LiveControlRetryBridge.Reset()` aufrufen — behebt zwei Medium-Leaks mit einem Muster, das (`BuilderPageViewModel`) bereits existiert. Gleiches Muster fuer `OverviewPageViewModel`/`ProjectPageViewModel`.
3. **ROB-1: eine `Debug.WriteLine`-Zeile** im Weight-Learning-catch — stellt die Logging-Konsistenz her.
4. **DEC-4: doppelten `<summary>`-Block konsolidieren** — reine Doku-Hygiene.
5. **DOM-2: `_lastConfirmedMeter = 0` in `Reset()`** — eine Zeile, schliesst ein latentes Cross-Session-Leck.
6. **COH-1: `DnValueParser` konsolidieren** — eine der beiden identischen Methoden behalten (die Application-Version), Duplikat loeschen, Test umbiegen.

## Architektur-Verdikt

- **4-Schichten: eingehalten (stark).** Die Richtungsdisziplin ist ueber 256 Infrastructure- und 189 Application-Dateien lueckenlos — keine Aufwaerts-Referenz, Domain real dependency-frei. Die einzige Schicht-Schwaeche ist Zuordnung, nicht Richtung: reine Logik (69 Training-Controller, die gewinnenden DataPage/Schaechte-Versionen) wohnt in UI statt Application. Das verletzt keine Regel, untergraebt aber das Thin-VM-Ideal.
- **Thin-AI: vorbildlich.** Der Sidecar-Client traegt null Geschaeftslogik; VSA-Mapping, Dedup, Voting und QualityGate liegen vollstaendig in C#. Genau wie im Prinzip vorgesehen.
- **Thin-VM: ueberwiegend, aber ungleich.** Im Kostenrechner und Training-Bereich ist Logik echt in Services verlagert; die VMs orchestrieren. Gegenbeispiele: `CostCalculatorViewModel` traegt noch Domaenenlogik (DEC-3), und die durchgesetzten Logik-Versionen aus dem Merge sitzen in UI (COH-4).
- **Dekomposition: gelungen, aber ueberzogen.** Verhaltensneutral und aussergewoehnlich gut getestet — das ist die eigentliche Leistung. Aber das Pendel schlug zu weit (22 Mikro-Klassen unter 1KB, triviale Indirektions-Drei-Zeiler), und in den Orchestrierungs-Methoden wurde Komplexitaet in Delegate-Baeume verschoben statt reduziert (DEC-2). Die Kampagne ist zudem ungleich verteilt: Training tief zerlegt, CostCalculator unberuehrt.
- **3-Linien-Merge: sauber integriert, lokal narbig.** Keine Compile-Kollisionen, keine verwaisten Live-Controller — aber mehrere tote Konkurrenz-Duplikate, bei denen ausgerechnet die architektonisch korrekten Application-Versionen den Merge verloren und durch Tests scheinbar lebendig gehalten werden.

## Risiken & Empfehlung naechste Schritte

**Risiken (alle Wartbarkeit, kein Stabilitaets-/Datenverlustrisiko):**
- **Stilles Auseinanderlaufen der Streckenschaden-Definition (DOM-1)** ist das fachlich relevanteste Risiko: eine einseitige Katalog- oder Set-Aenderung koennte lautlos die Punkt-/Strecken-Klassifikation veraendern. Mit Konsistenz-Test entschaerfbar.
- **Memory-Leaks bei haeufigem Seitenwechsel (UI-1/UI-2)**: akkumulierende tote VMs, deren Handler weiterfeuern; im Extremfall Retry auf nicht sichtbarer Seite.
- **Test-Kennzahl-Verwaesserung (TQ-1/TQ-2)**: ~264 von ~4988 Facts sichern kein Verhalten und erzeugen kuenstliche Wartungslast bei jedem Folge-Refactor — falsches Sicherheitsgefuehl.
- **Toter Code (COH-1..3)**: Navigationslast und Verwechslungsgefahr; die richtige Zerlegung existiert, ist aber nicht verdrahtet.

**Empfohlene Reihenfolge:**
1. **Quick Wins abraeumen** (COH-3 loeschen; UI-1/UI-2 Disposal-Fix; ROB-1; DEC-4; DOM-2) — wenige Stunden, sofort spuerbar.
2. **Merge-Narben konsolidieren** (COH-1/COH-2/COH-4): jeweils die Application-Version zur Single Source of Truth machen, UI-Duplikate loeschen, Tests umbiegen. Stellt das Thin-VM-Prinzip wieder her, das die Extraktionen urspruenglich verfolgten.
3. **DOM-1 absichern**: Streckenschaden-Definition auf eine Quelle vereinheitlichen + Konsistenz-Test ueber alle Katalog-Codes.
4. **Test-Hygiene** (TQ-1/TQ-2/TQ-3): brittle String-Guards aussondern, die echten Fitness-Funktionen in eine kleine, klar benannte Datei extrahieren (ggf. Roslyn-basiert). Reduziert die Wartungslast aller kuenftigen Refactorings.
5. **Mittelfristig, optional**: Dekompositions-Stil vereinheitlichen — `CostCalculatorViewModel` nach demselben Muster zerlegen (DEC-3), Training-Controller nach Application verschieben und Sink-Objekte statt Delegate-Bundles einfuehren (DEC-1/DEC-2). Nur, wenn die betroffenen Bereiche ohnehin angefasst werden — kein dringender Bedarf.

Insgesamt eine gesunde, ehrliche Codebasis mit guter Robustheit und intakter Fachdomaene; die offenen Punkte sind Politur und Konsistenz, nicht Substanz.