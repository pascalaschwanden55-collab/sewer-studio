# Deepscan 02.10.2026 – Prüfer A: Architektur, Kopplung, Verantwortlichkeiten

Prüfstand: `C:\Sewer-Studio_KI_5.0\.claude\worktrees\release-master`, HEAD `fbff3dd4d` (detached).
Nur gelesen. Zählungen per Textsuche bzw. kleinen Python-Skripten im Scratchpad (`a_groessen.py`,
`a_schicht.py`, `a_uiai.py`, `a_paare.py`). Codex-Bereiche wurden nur gelesen, wo ein Befund dort
endet, und sind höchstens in einem Satz genannt.

## 1) Kurzfazit

Die grobe Schichtung trägt weiter: Application ist fast frei von Dateizugriffen (33 Dateien, davon 1
in `UseCases`), `App.Services` kommt nur noch einmal vor, `UI/Ai` wächst nicht (594 Dateien, 0 neu
seit 01.09.), und 96 neue Dateien seit 01.09. landeten richtig in `Application/UseCases`. Die
Sperrklinken halten: Alle eingefrorenen Dateien und Typen stehen exakt auf ihrem Wert vom 30.09.
Die Bremsen liegen jetzt bei **Regeln, die es mehrfach gibt und die schon verschieden rechnen**:
Eval-Schutz (vier Lesewege, drei Bedeutungen von «leer» bzw. «fehlt»), Meterstand am Bild (zwei
getestete, widersprüchliche Regeln) und Inspektionsdatum (sechs Leser). Dazu kommt ein schnell
wachsender **Parallelbestand Haltungen/Schächte**: 12 Datei-Paare mit 59–91 % gleichem Inhalt, 31 von
86 Commits seit 01.09. mussten beide Seiten anfassen, eine Abweichung ist belegt. Alle drei
`UI/Ai`-Kandidaten sind durch Quelltext-Tests an ihren Ort genagelt; das ist die eigentliche Bremse
beim Umzug nach `Application/UseCases`.

## 2) Befundtabelle

| ID | Prio | Titel | Grösse | Nutzen in einem Satz |
| --- | --- | --- | --- | --- |
| A1 | P1 | Eval-Schutz: vier Lesewege mit drei Bedeutungen von «leer» und «fehlt» | M (3 × S) | Prüfdaten bleiben an jedem Einstieg gleich streng vom Training getrennt. |
| A2 | P1 | Haltungen- und Schächte-Seiten als Parallelbestand (12 Paare, 31/86 Commits doppelt) | M (je Ablauf S) | Eine Änderung trifft beide Seiten gleich, statt dass sie auseinanderlaufen. |
| A3 | P2 | Meterstand am Bild: zwei getestete, widersprüchliche Regeln | S | Handeintrag und Anzeige nehmen keinen veralteten OSD-Meter mehr. |
| A4 | P2 | Projektfelder werden je Leser anders gedeutet (`Datum_Jahr`, Haltungslänge) | M | Ein Feldwert bedeutet in Suche, Training, Dateistempel und Bewertung dasselbe. |
| A5 | P2 | Junction-Schutz in rund 20 lokalen Kopien mit verschiedener Fehlerregel | M (schrittweise S) | Pfadschutz verhält sich überall gleich bei Fehlern und an der Wurzel. |
| A6 | P2 | Zweite Zusammensetzungswurzel in `UI/Ai` und `UI/Services` | S–M | KI-Dienste entstehen an einer Stelle mit gleicher Konfiguration und Sperrliste. |
| A7 | P2 | Service-Locator in 15 Seiten-ViewModels, seit 15.08. unverändert; Projekt-Laden im `ShellViewModel` | M | Projekt-Laden samt Wiederherstellung wird ohne ganzen Container testbar. |
| A8 | P2 | `UI/Ai`: drei Kandidaten für `Application/UseCases` (Meter, Ampel-Nachweis, Trainingsfall speichern) | je S–M | Fachregeln des Codiermodus lassen sich ändern, ohne die Einfrierliste zu umgehen. |
| A9 | P3 | Typen an der Sperrklinke: nur zwei tragen wirklich mehrere Verantwortungen | S | Kein Aufwand für Typen, die nur gross, aber zusammenhängend sind. |
| A10 | P3 | Haltungsschlüssel: Restpunkt aus Z5 (KINS, falscher Kopfkommentar) | S | Die Dokumentation behauptet nicht mehr eine Einheitlichkeit, die es nicht gibt. |

## 3) Befunde im Detail

### A1 (P1) – Eval-Schutz: vier Lesewege, drei Bedeutungen

**Beleg.**

| Einstieg | Datei:Zeile | Root leer/null | Root fehlt | Unterordner unlesbar |
| --- | --- | --- | --- | --- |
| Gold-Speicher filtern | `Infrastructure/Ai/Training/TrainingSampleFileStore.cs:51-58, 276-286` | fällt auf Umgebungsvariable, dann `C:\KI_BRAIN\eval_set` (Z. 18) | **leere Sätze → alle Samples durchgelassen** (`if (hashes.Count == 0 && holdings.Count == 0) return input;`) | Unter-Sets fallen still weg (s. u.) |
| Wissenssuche | `Infrastructure/Ai/KnowledgeBase/GuardedRetrievalFactory.cs:28-48` | **Schutz aus** (auch wenn `ConfigureDefaultEvalSetRoot` nie lief) | Ausnahme (`LoadEvalHaltungKeysStrict`) | Unter-Sets fallen still weg |
| UI-Lader (KB-Index, Selbsttraining, Codiermodus, Studio) | `UI/Services/EvalContaminationSetProvider.cs:15-56` (statisch, im UI-Projekt) | Schutz aus | Ausnahme | Unter-Sets fallen still weg |
| YOLO-Export/Inventar | `Infrastructure/Ai/Training/Inventory/TrainingInventoryEvalProtectionReader.cs` (624 Z., `DiscoverSetRoots` Z. 92) | Fehler «nicht konfiguriert» | Fehler | **Fehler wird gemeldet** |

Die ersten drei Wege benutzen `EvalContaminationGuard.EnumerateEvalSetRoots`
(`Application/Ai/Training/EvalContaminationGuard.cs:306-328`). Dort fängt ein leerer `catch` jede
Ausnahme der rekursiven Suche ab, Kommentar «Hauptset bleibt trotzdem aktiv». Ein unlesbarer Ordner
irgendwo unter dem Root beendet die Suche; weitere Sets (z. B. `v2`) fehlen dann still, auch im
«strengen» Weg. Die Inventar-Suche nutzt dagegen den Junction-sicheren `TrainingInventoryFileEnumerator`
und meldet übersprungene Ordner.

Dazu zwei getrennt konfigurierte Speicher-Instanzen: `ServiceProvider.cs:437` und `:441` setzen
denselben Root zweimal (Instanz und statische Fassade `TrainingSamplesStore`), `ServiceProvider.KnowledgeBase.cs:41`
ein drittes Mal für die Suche. Der Standardpfad `C:\KI_BRAIN\eval_set` steht doppelt
(`AppSettings.cs:250`, `TrainingSampleFileStore.cs:18`).

**Warum es bremst / Risiko.** «Prüf- und Eval-Daten nie ins Training» ist Querschnittsregel. Wer die
Regel ändert (neues Set, neues Format), muss vier Leser und drei Konfigurationsaufrufe kennen. Belegt
ist: Der Gold-Speicher filtert bei fehlendem Ordner nichts, und ein leerer Eintrag heisst dort etwas
anderes als in Suche und UI. **Nicht belegt** ist ein tatsächlicher Durchschlag ins YOLO-Training:
Der Export prüft über den strengen Inventar-Leser. Betroffen sind die zweite Schutzlinie (Gold-Album,
Laden/Speichern) und die Wissenssuche bei unlesbaren Unterordnern.

**Empfehlung (drei einzeln prüfbare Pakete).**
1. (S) `EnumerateEvalSetRoots` auf `SafeFileEnumeration` umstellen. Übersprungene Ordner im strengen
   Weg als Fehler melden, im milden Weg protokollieren.
2. (S) `TrainingSampleFileStore`: dieselbe Bedeutung wie die anderen Wege. «Leer» heisst aus; ein
   konfigurierter, aber fehlender Ordner sperrt das Speichern, statt alles durchzulassen. **Entscheid
   Pascal nötig:** Der Code nennt das milde Verhalten bewusst «degradiert sicher auf fremden
   Maschinen» (Laptop-Modus).
3. (M) Ein Lader hinter einer Schnittstelle in Application/Infrastructure. `EvalContaminationSetProvider`
   zieht aus `UI/Services` um, und Speicher, Suche und UI nutzen ihn. Der Root wird einmal gesetzt
   statt dreimal. Paket 3 berührt `ServiceProvider` (Codex-Bereich), deshalb danach einplanen.

**Abnahme.** Ein Tabellentest mit den Fällen leer, null, fehlt, defekte `_candidates.json`, unlesbarer
Unterordner und Junction liefert für alle vier Einstiege dasselbe Ergebnis (gesperrt oder aus). Eine
Sabotageprobe mit gelöschtem Eval-Ordner lässt `TrainingSampleFileStore.SaveAsync` nicht mehr still
durch.

### A2 (P1) – Haltungen- und Schächte-Seiten als Parallelbestand

**Beleg.** Gleichheit nach Normalisierung der Namen (Haltung/Schacht/DataPage → X, ohne Kommentare
und `using`), gemessen mit `difflib`:

| Paar (Haltungen ↔ Schächte) | Zeilen | gleich |
| --- | ---: | ---: |
| `DataPage/DataPageAufklappListeController` ↔ `SchaechteAufklappListeController` | 177/162 | 91 % |
| `ViewModels/Pages/DataPageViewModel.Erledigt` ↔ `SchaechtePageViewModel.Erledigt` | 21/21 | 84 % |
| `DataPage/DataPageNovaWorkspaceController` ↔ `SchaechteNovaWorkspaceController` | 256/312 | 79 % |
| `DataPageViewModel.Verlauf` ↔ `SchaechtePageViewModel.Verlauf` | 49/48 | 76 % |
| `DataPageViewModel.QgisNachfuellen` ↔ `…Schaechte…QgisNachfuellen` | 37/35 | 74 % |
| `DataPage/DataPageAnsichtUmschalter` ↔ `SchaechteAnsichtUmschalter` | 230/134 | 73 % |
| `…WebGisHolen`, `…KatasterKennungen`, `Views/Pages/*.Verlauf` | je ~25–40 | 59–67 % |
| `SanierungsMatrixPageViewModel` ↔ `SchachtSanierungsMatrixPageViewModel` | 995/500 | 31 % (gleicher Sitzungsablauf) |

Seit 01.09. berührten **31 von 86 Commits** an diesen Seiten beide Seiten. Mehrere Paare sind erst
im September entstanden; das Muster wächst also.

**Belegte Abweichungen:**
- QGIS-Ergänzen: Die Haltungsseite ruft `MeldeFelderExternErgaenzt()` (`DataPageViewModel.QgisNachfuellen.cs:35`,
  das heisst Verlauf leeren **und** Ereignis `FelderExternErgaenzt`). Die Schachtseite ruft nur
  `Verlauf.Leere(...)` (`SchaechtePageViewModel.QgisNachfuellen.cs:33`). Das Ereignis, an dem
  `SchaechtePage.AufklappListe.cs:89` die Importwerte neu zeichnet, fehlt. Folge in der Anzeige
  (Importwerte bis zum nächsten Aufklappen alt): Vermutung, nicht bedient. Dieselbe Hilfe heisst
  einmal `MeldeFelderExternErgaenzt` (`DataPageViewModel.cs:41`) und einmal `MeldeUebernahme`
  (`SchaechtePageViewModel.Verlauf.cs:43`).
- Sanierungsmatrix: Nur die Haltungsseite schützt Mehrfach-Massnahmen (`SanierungsMatrixPageViewModel.cs:481`)
  und leert beim Speichern die Tabellenfelder entfernter Massnahmen (`:962-964`, `ClearCosts`). Ob das
  bei Schächten gewollt fehlt: offen. Gleich doppelt gepflegt sind Speichersperre,
  «frisch laden und nur Berührtes einmischen» (Audit W8) und `_touched…`-Buchführung (Haltung Z. 935-950,
  Schacht Z. 353-400).
- Kleinere Unterschiede: «Darf verschieben» hängt einmal an `IsProjectReady`
  (`DataPageAufklappListeController.cs:53`), einmal an `CanMutateShaftData` (`SchaechteAufklappListeController.cs:51`).

**Warum es bremst.** Jede Übernahme (QGIS, GeoShop, WebGIS), jede Verlauf- und Aufklappänderung kostet
zwei Umsetzungen und zwei Prüfungen. Vergessene Hälften fallen nur in einer Ansicht auf.

**Empfehlung (je Ablauf ein kleines Paket, kein Seitenumbau).**
1. (S) Eine gemeinsame Hilfe «externe Übernahme abgeschlossen» (Verlauf leeren, Ereignis, als
   geändert markieren, automatisch speichern) für beide ViewModels. Alle Übernahmewege beider Seiten
   rufen nur sie auf.
2. (S) Die Aufklapp-Controller zu einer generischen Klasse mit zwei Parametern machen: Datensatztyp
   und Änderungsschranke.
3. (M) Für die Sanierungsmatrix eine reine Sitzungsklasse in Application, zum Beispiel
   `KostenMatrixSitzung<TSchluessel>`. Sie enthält Berührt/Geleert, das Einmischen in den frischen
   Stand und die Speichersperren. Beide ViewModels nutzen sie. Die Abweichungen (Mehrfachschutz,
   Leeren) werden dabei bewusst entschieden.

**Abnahme.** Zu 1: Ein Test je Seite und Übernahmeweg verlangt das Ereignis `FelderExternErgaenzt`.
Er ist heute für Schächte-QGIS rot. Zu 2: Die beiden Controller-Dateien entfallen oder sind unter 30
Zeilen; die bestehenden Aufklapptests bleiben grün. Zu 3: Die Sitzungsklasse hat eigene Tests ohne WPF;
beide ViewModels schrumpfen messbar (Sperrklinkenwert senken).

### A3 (P2) – Meterstand am Bild: zwei getestete, widersprüchliche Regeln

**Beleg.**
- `Application/Ai/CodingMeterResolver.cs:16-39`: OSD desselben Bilds (0–500 m) → zwischengespeicherter OSD-Meter
  **nur bis 1,5 s alt** (`RecentOsdMeterMaxAgeSeconds`) → Videoschätzung (auf 0–1 begrenzt) → Sitzungswert.
- `UI/Ai/Coding/CodingCurrentMeterResolver.cs:14-37`:
  - `Resolve`: OSD ohne Plausibilitätsgrenze → Zeitleiste ohne Begrenzung → Sitzungswert. Gerundet wird nicht.
  - `ResolveManualEntry`: `osdMeter ?? cachedOsdMeter ?? timeline`. Der zwischengespeicherte Wert gilt
    **ohne Altersgrenze**.
- Beide Regeln sind getestet: `CodingMeterResolverTests` und `CodingCurrentMeterResolverTests`,
  dort `ResolveManualEntry_uses_cached_osd_before_video_position`. Der Aufrufer übergibt
  `_codingOsdMeterController.LastMeter`, aber nicht `LastTimestampSeconds`, obwohl der Controller beides
  führt (`UI/Player/CodingOsdMeterController.cs:37-38`). Das ist Codex-Bereich, nur gelesen.

**Risiko.** Die Regel in CLAUDE.md lautet «frische OSD-Metrierung vor Timeline-Schätzung vor letztem
Sessionwert». Beim Handeintrag kann nach einem Sprung im Video ein alter OSD-Wert gewinnen, wenn die
frische Lesung scheitert. Belegt ist die widersprüchliche Regel, nicht ein beobachteter Fehleintrag.

**Empfehlung (S).** `CodingCurrentMeterResolver` delegiert an `CodingMeterResolver`. Dazu den
Zeitstempel des Zwischenspeichers übergeben. Den Test zu «cached ohne Alter» umschreiben, nachdem
Pascal entschieden hat. Die Aufrufstelle liegt in `PlayerWindow.Coding.Events.cs:27`, deshalb mit
Codex abstimmen.

**Abnahme.** Es gibt nur noch eine Implementierung der Reihenfolge. Ein Test «zwischengespeicherter
OSD-Wert 10 s alt, frische Lesung fehlt» liefert beim Handeintrag die Videoschätzung.

### A4 (P2) – Projektfelder werden je Leser anders gedeutet

**Beleg `Datum_Jahr`.** Die Schreiber legen verschiedene Formen ab:
- KINS nur das Jahr: `KinsImportService.cs:180`, `ToString("yyyy")`.
- WinCan `dd.MM.yyyy`: `WinCanDbImportService.Records.cs:43`.
- XTF `dd.MM.yyyy`, unlesbare Werte roh: `VsaKekAbbildung.cs:192/225`.
- PDF nur `\d{2}\.\d{2}\.\d{4}`: `PdfFieldMapping.cs:90`.
- Von Hand: beliebiger Text.

Die Leser deuten verschieden:

| Leser | Datei:Zeile | «5.3.2024» | «24.09.25» | «2024-03-05» |
| --- | --- | --- | --- | --- |
| Dateistempel der Verteilung | `Import/Common/ImportDateStampResolver.cs:48` (de-CH-`TryParse`) | 20240305 | 20250924 | 20240305 |
| Mediensuche | `Media/BatchMediaSearchService.cs:417` (nur `yyyyMMdd`, `dd.MM.yyyy`, `yyyy`) | – | – | – |
| Training (Stichtag 2022) | `Application/Ai/Training/TrainingSampleModels.cs:340` | 05.03.2024 | 2025 | 05.03.2024 |
| Schachtprotokoll | `Import/Pdf/SchachtProtocolParser.cs:473` | bleibt roh | 2025 | 05.03.2024 |

Dazu `HoldingTextNormalizer.cs:35`, mit derselben Formatliste wie `SchachtProtocolParser` (kopiert),
und `UI/Services/SchachtProtocolFolderImportPolicy.cs:217`, eine weitere Liste im UI-Projekt.
Feldnamen: `"Datum_Jahr"` steht 30-mal als Literal und nur 14-mal als `FieldKeys.InspectionYear`.
Über alle 51 `FieldKeys` sind es 1'066 Literale gegen 966 Konstanten.

**Beleg Haltungslänge.** Diese Leser verwenden eigene Zahlregeln statt `FachzahlParser`:
- `Vsa/VsaEvaluationService.cs:778` (6 Aufrufe ab Z. 113): nur `,`→`.`, unlesbar ergibt **0**.
- `DashboardStatisticsBuilder.cs:392` und `DossierSnapshotBuilder.cs:322`: entfernen Apostroph und
  Leerzeichen.
- `ImportPlausibilityValidator.cs:82`.

`FachzahlParser` lehnt dagegen mehrdeutige Werte ab. Für übliche Werte wie «45,30» rechnen heute alle
gleich; eine Abweichung ist nur bei Tausendertrennern und Einheiten belegt.

**Warum es bremst.** Wer ein Datumsformat ergänzt oder einen neuen Import schreibt, muss sechs Leser
kennen. Die Mediensuche verliert heute bei den drei Beispielen still den Datumshinweis.

**Empfehlung (M).** Einen getypten Leser in Application anlegen, analog `SchachtFeldnamen`, zum
Beispiel `HaltungFeldwerte.LiesInspektionsdatum(record)` und `LiesLaenge(record)`. Er stützt sich auf
`FachzahlParser` und eine Datumsregel. Die Leser werden einzeln umgestellt, die Mediensuche zuerst.
Keine Datenmigration: Gespeicherte Werte bleiben, wie sie sind.

**Abnahme.** Eine Beispieldatei mit Datums- und Längentexten liefert für alle umgestellten Leser
dasselbe Ergebnis. In `src` gibt es keine eigene `dd.MM.yyyy`-Formatliste mehr ausserhalb des Lesers
und der Importparser.

### A5 (P2) – Junction-Schutz in rund 20 lokalen Kopien

**Beleg.** 28 Produktdateien prüfen `FileAttributes.ReparsePoint` selbst. Nur ein kleiner Teil nutzt
gemeinsame Bausteine (`ReparsePointGuard`, `SafeFileEnumeration`, `ProjectMutationPathPolicy`). Die
Regeln unterscheiden sich:
- **Fehler beim Lesen der Attribute.** `Backup/ReparsePointGuard.cs:15-24` gibt `false` zurück (offen).
  `Backup/BackupTargetPathGuard.cs:172-188` sperrt.
- **Bis wohin geprüft wird.**
  - `ProjectMutationPathPolicy.cs:23-27`: bis zur Laufwerkswurzel, also auch oberhalb des Projekts.
  - `ReparsePointGuard.HasReparsePointBelow` (Z. 31-49) und `ImportFileStagingPathGuard.cs:54-61`:
    Wurzel ausgenommen.
  - `PersonalGoldBrainFileService.cs:52-64`: Wurzel eingeschlossen.
- **Fehlender Pfad.** `ImportSourcePathGuard.cs:82-87` erlaubt ihn. `PersonalGoldBrainFileService`
  wirft eine Ausnahme.

Die Unterschiede sind teils gewollt, aber nirgends benannt.

**Warum es bremst.** «Keine Verknüpfungen/Junctions betreten» ist Querschnittsregel. Eine Härtung, zum
Beispiel gegen Fehler offen oder geschlossen, muss heute an 20 Stellen einzeln nachgezogen werden.
Ein aktueller Durchbruch ist **nicht** belegt. Das offene Verhalten von `ReparsePointGuard` bei
Zugriffsfehlern im Spiegel ist ein geringes Risiko (Vermutung: Ein nicht lesbarer Ordner ist auch
nicht löschbar).

**Empfehlung.** (S) Einen Baustein in `Application/Common` mit ausdrücklichen Optionen:
`Wurzel einschliessen`, `oberhalb prüfen`, `bei Fehler sperren`. Danach (je S) bei jeder Berührung eine
lokale Kopie ersetzen, zuerst die Gold- und Trainingsspeicher.

**Abnahme.** Tabellentests für alle Optionen. Ein Sperrklinken-Test zählt die Dateien mit eigener
`ReparsePoint`-Prüfung: heute 28, die Zahl darf nur sinken.

### A6 (P2) – Zweite Zusammensetzungswurzel in `UI/Ai` und `UI/Services`

**Beleg.** 59 UI-Dateien ausserhalb von `ServiceProvider` und `*Composition*` erzeugen mit `new`
Infrastrukturklassen. Die meisten liegen in `Ai` (19) und `Services` (15).
- `OllamaClient` entsteht 6-mal im UI: `CodingAiRuntimeFactory.cs:57`, `CodingOsdMeterService.cs:57`,
  `LiveDetectionRuntimeFactory.cs:63`, `SelfTrainingSessionController.cs:103`,
  `TrainingMeterTimelineServiceFactory.cs:17` und `TrainingStudioWindowDependencyFactory.cs:264`
  (dort fest 90 s Zeitgrenze). Dazu kommen `ServiceProvider.cs:845` (fest 45 s) und 4 Stellen in
  Infrastructure.
- `KnowledgeBaseManager` entsteht 3-mal, mit verschiedener Sperrliste: mit Sperrliste in
  `TrainingKbIndexRunner.cs:135` und `TrainingReviewFeedbackServiceFactory.cs:31`, **ohne** in
  `TrainingKnowledgeBaseSampleDeindexer.cs:76`.
- `KnowledgeBaseContext()` ohne Pfad entsteht 6-mal und nutzt den statischen Pfad.
  `ServiceProvider.KnowledgeBase.cs:66` übergibt den Pfad ausdrücklich.

Der Grenztest `ViewModelInfrastructureBoundaryTests.cs:19-45` prüft nur `ViewModels`, `Views` und
`DataPage`, und dort nur sechs Store-Typen.

**Warum es bremst.** Eine Änderung an Ollama-Einstellungen oder an der KB-Sperrliste muss zehn
Fabriken treffen. Der Grenztest sieht die beiden Ordner nicht, in denen das Muster lebt.

**Empfehlung.** (S) Den Grenztest als Sperrklinke auf `UI/Ai` und `UI/Services` ausdehnen, für die Typen
`OllamaClient`, `KnowledgeBaseContext`, `EmbeddingService` und `KnowledgeBaseManager` mit heutigem
Bestand. (M) Je eine kleine Fabrik in Infrastructure: `IOllamaClientFactory(AiRuntimeSettings)` und
`IKnowledgeBaseSitzungFactory`, die die Sperrliste aus A1 selbst bezieht. Die UI-Fabriken stellen
einzeln um. Die Registrierung berührt `ServiceProvider`, deshalb mit Codex abstimmen.

**Abnahme.** Die Sperrklinke wird rot bei einem neuen `new OllamaClient(` in `UI/Ai`. Nach dem Umbau
gibt es `new KnowledgeBaseManager(` nur noch in der Fabrik.

### A7 (P2) – Service-Locator in 15 Seiten-ViewModels; Projekt-Laden im `ShellViewModel`

**Beleg.** `ArchitectureDriftRatchetTests.cs:28-47` friert 15 ViewModels mit `ServiceProvider` im
Konstruktor ein. Die Liste ist seit 15.08. nicht geschrumpft. Verschiedene genutzte
Container-Mitglieder (Näherung): `ShellViewModel` 44 (113 Zugriffe `_sp.`), `DataPageViewModel` 36,
`ImportPageViewModel` 32, `SchaechtePageViewModel` 26 und `ExportPageViewModel` 24.
`ShellViewModel.LoadOrRecover` (`ShellViewModel.cs:702-742`) entscheidet über diese Fragen:
- Darf eine Sicherung eingespielt werden?
- Läuft die Import-Wiederherstellung?
- Wird ein wiederhergestelltes Projekt materialisiert?

Das ist ein sicherheitskritischer Anwendungsfall im ViewModel; dazu kommen Wiederherstellungspunkte
(Z. 957-996). Nebenbei bemerkt: `SchaechtePage.xaml.cs:700` und `QgisBridgeRequestProcessor.cs:82` holen
das Projekt über `App.Current.MainWindow.DataContext`, ein versteckter zweiter Zugangsweg.

**Warum es bremst.** Wer am Laden oder Wiederherstellen etwas ändert, braucht für jeden Test den ganzen
Container. Die Abhängigkeiten der Seiten stehen nicht im Konstruktor.

**Empfehlung.** (M) `ProjektLadenUseCase` in `Application/UseCases` übernimmt `LoadOrRecover` mit dem
Ergebnisdatensatz. Er arbeitet über Schnittstellen zu Projekt- und Import-Wiederherstellung.
`ShellViewModel` behält Dialoge und Status. Danach (je S) bei jeder Berührung ein kleines
Seiten-ViewModel von `ServiceProvider` auf benannte Abhängigkeiten umstellen, zuerst
`ProjectPageViewModel` und `SchattenauswertungPageViewModel` (je 2 Mitglieder).

**Abnahme.** `ShellViewModelImportRecoveryTests` bleibt grün. Der Anwendungsfall hat eigene Tests ohne
`ServiceProvider`: beschädigt, gesperrt, zu neu, Import blockiert. Die Liste in
`ArchitectureDriftRatchetTests` hat einen Eintrag weniger je umgestelltem ViewModel.

### A8 (P2) – `UI/Ai`: drei Kandidaten für `Application/UseCases`

Messung: 594 Dateien und 33'970 Zeilen; 472 Dateien ohne WPF- oder MVVM-Bezug, davon 393 auch ohne
Infrastructure-`using`. Das zeigt nur die Richtung. Ausgewählt nach Fachregel-Gehalt und belegter Bremse:

1. **`UI/Ai/Coding/CodingCurrentMeterResolver.cs`** (59 Z.): Doppel zur Application-Regel (A3).
   Festgenagelt durch Quelltext-Tests: `PlayerWindowCodingEventsArchitectureTests.cs:97-105` verlangt
   Dateipfad und Aufruftext, ebenso `PlayerWindowCodingNavigationArchitectureTests.cs:111, 148`.
2. **Ampel-Nachweis im Codiermodus**:
   - `CodingMultiModelQualityGatePolicy.cs` legt den Plausibilitätswert fest (`0.8 : 0.4`, Z. 26)
     und die Regel «ohne Gate Rot» (Z. 34-43).
   - `CodingLiveFindingQualityGatePolicy.cs` setzt `PlausibilityScore: 0.6` und dieselbe Rot-Regel.
   - `CodingMultiModelEventFactory.cs:81-90` enthält eine dritte Kopie des Nachweisaufbaus. Sie
     weicht ab: Im Rückfallzweig fehlt `YoloConf`. Ihr einziger Aufrufer
     (`CodingMultiModelFindingEventWorkflow.cs:135-147`) übergibt immer einen Nachweis, die Kopie ist
     also toter Code.

   Die Qualitätsampel «muss immer durchlaufen» (Architekturprinzip), ihre Eingangsregel liegt aber
   im UI. Festgenagelt durch `PlayerWindowCodingMultiModelArchitectureTests.cs:77, 93, 143, 152`.
3. **Codiermodus-Trainingsfall speichern**: `CodingTraining*` umfasst 7 Dateien und 794 Zeilen. Ablauf:
   Eval-Schutz zweimal, Bild aufnehmen, Goldbild ablegen, Beweisbild, Speichern und Indexieren, KB-Status.
   Das läuft parallel zu `Application/UseCases/GoldSampleSpeichern` (6 Dateien, 1'102 Z.; AP06). Der
   Eval-Schutz kommt über den UI-Lader aus A1 (`CodingTrainingSampleEvalProtector.cs:19-21`).
   Festgenagelt durch `PlayerWindowCodingTrainingArchitectureTests.cs:16, 41`.

**Warum es bremst.** Wegen der Einfrierung (`UiAiFreezeArchitectureTests`, Vergleich per Dateiname)
darf dort keine neue Hilfsdatei entstehen. Ein Umzug verlangt zuerst das Umschreiben der
Quelltext-Tests (W01). Fachänderungen landen deshalb oft als Zusatz in bestehenden UI-Dateien.

**Empfehlung.** Je Kandidat ein Paket. Ablauf: (a) Den Quelltext-Test durch einen Verhaltenstest
ersetzen, der denselben Fehler fängt. (b) Die Klasse nach `Application` verschieben bzw. dorthin
delegieren. (c) Den Eintrag aus der Einfrierliste löschen. Reihenfolge: 1 (S, zusammen mit A3), dann
2 (S), dann 3 (M, auf die Bausteine von `GoldSampleSpeichern` aufsetzen).

**Abnahme.** Die Datei verschwindet aus `UI/Ai` und aus der Einfrierliste. Ihr Verhaltenstest ist
bei einer Sabotage rot, zum Beispiel mit vertauschten Plausibilitätswerten oder fehlendem Eval-Schutz.
Kein neuer Quelltext-Test entsteht.

### A9 (P3) – Typen an der Sperrklinke: Wer trägt wirklich mehrere Verantwortungen?

Gemessen: Alle Werte stehen exakt auf dem Stand vom 30.09. Es gibt kein Wachstum und kein Schrumpfen.
Commits seit 01.08. stehen in Klammern.

| Typ | Zeilen/Dateien | Urteil |
| --- | --- | --- |
| `ShellViewModel` | 1'783/5, Hauptdatei 999 (31) | **mehrere**: Navigation, Projekt-Laden und Wiederherstellung, KI-Status, Wiederherstellungspunkte → A7 |
| `SanierungsMatrixPageViewModel` | 995 (3) | **mehrere**: Kostensitzung, Detail-Editor, Katalog-Neupreis → A2.3 |
| `SchaechtePage.xaml.cs` / `SchaechtePageViewModel` | 1'000 / 1'921 in 15 Dateien (28 / 24) | Codebehind schreibt Felder mit Handmarke (`ClearColumn` Z. 895-918, `CommitSchachtDetailKonsolidiert` Z. 818-849) → bei A2 mitziehen |
| `TrainingCenterViewModel` | 997 (3) | sieben Bereiche (Fall-Scan, Samples, Generierung, Selbsttraining, KB, Review, Export), aber selten geändert → nur bei Berührung |
| `TrainingStudioViewModel` | 1'951/5 (3) | delegiert an Anwendungsfälle; zusammenhängend |
| `DossierPreviewFieldPanel` | 1'999/7 (0 seit 15.09.) | baut Ansicht dynamisch; eine Aufgabe |
| `TrainingExportRegistryFileStore` | 1'721/2 | strenges Lesen und Prüfen; zusammenhängend |

**Empfehlung.** Keine eigenen Pakete; die echten Fälle stecken in A2 und A7.

**Abnahme.** Nach A2 und A7 sinken die Sperrklinkenwerte von `ShellViewModel` und
`SanierungsMatrixPageViewModel`.

### A10 (P3) – Haltungsschlüssel: Restpunkt aus Z5

**Beleg.** R1 hat Python und `EvalContaminationGuard` zusammengeführt (`HaltungsidentitaetTests`). Noch
offen sind zwei Punkte:
- Der Kopfkommentar `Import/Common/HoldingKeyNormalizer.cs:5-8` behauptet «Alle Import-Services
  verwenden dieselbe Logik». `KinsImportService.cs:491-498` macht aber nur Trim, Leerzeichen weg und
  Grossschreibung. Der Kommentar in `WinCanDbImportService.cs:601-606` beschreibt den Unterschied
  richtig.
- Die Abgrenzung der vier C#-Normalisierer im Kopfkommentar (Z5-Empfehlung) fehlt. Es sind
  `HoldingKeyNormalizer`, `HoldingIdNormalizer`, `EvalContaminationGuard.NormalizeHaltungKey` und
  `TrainingExportRegistryFileStore.NormalizeStrictHoldingKey`.

**Empfehlung (S).** Die Kopfkommentare korrigieren und abgrenzen. KINS entweder auf
`HoldingKeyNormalizer.Normalize` stellen, mit Test für `/` und Gedankenstrich, oder die Abweichung im
Code begründen.

**Abnahme.** Kein Kommentar behauptet mehr eine Einheitlichkeit, die der Code nicht hat. Der KINS-Test
deckt `100/200` ab.

## 4) Was gut ist und erhalten bleiben soll

- **Schichtung:** Application hat laut Textsuche 33 Dateien mit Dateizugriff (der Analysator zählte am
  30.09. mit anderer Regel 37), in `UseCases` nur eine.
  `App.Services` steht einmal (`MainWindow.xaml.cs:102`). 96 neue Dateien seit 01.09. liegen in
  `UseCases`, 0 neue in `UI/Ai`.
- **Sperrklinken wirken:** Dateien ab 900 und Typen ab 1'500 Zeilen sind unverändert. Die Fassaden und
  ServiceProvider-ViewModels können nur schrumpfen.
- **Gute Einzelstellen, auf die die Empfehlungen aufsetzen:**
  - `GuardedRetrievalFactory`: eine Stelle für die Suche, gut dokumentiert.
  - `FachzahlParser`: lehnt mehrdeutige Werte ab.
  - `ManualGoldTrainingPolicy`: zentral.
  - `GoldSampleSpeichernUseCase`: klare Grenze «dauerhaft gespeichert».
  - `XtfValueNormalizer.NormalizeDate` und `LiesZeitpunkt` sind seit 01.10. eine Regel.
  - `DataPageRightClickController`: von beiden Seiten genutzt, mit Kommentar, warum zwei Wege
    schlecht sind. Das ist das Vorbild für A2.
- **Strenger Inventar-Leser** für den YOLO-Export: Er meldet übersprungene Ordner und ist die Vorlage
  für A1.

## 5) Grenzen der Prüfung

- Kein Build und keine Tests ausgeführt. Alle Aussagen kommen aus dem Lesen des Codes und aus
  Textsuche. Die Ähnlichkeitswerte in A2 sind `difflib`-Werte nach Namensnormalisierung, keine
  semantische Gleichheit.
- «Verschiedene ServiceProvider-Mitglieder» (A7) und «UI-frei» (A8) sind Regex-Näherungen.
- Die Datumsbeispiele in A4 sind am Code nachvollzogen, nicht ausgeführt. Die Auswirkung auf die
  Mediensuche ist nicht an echten Projekten gemessen.
- Codex-Bereiche (`PlayerWindow*`, `UI/Player`, `ServiceProvider`, `ProjectImportOrchestrator`,
  `ParsedHoldingDistributionController`, `SettingsFullBackup*`, `HaltungsgrafikSvgBuilder`) nicht
  bewertet. Sie werden nur genannt, wo ein Befund dort endet (A1 Paket 3, A3, A6).
- Git-Häufigkeiten stammen aus `git log --no-merges` im Arbeitsbaum. Zusammengeführte Zweige können
  sie unterschätzen.
- Nicht geprüft: Python, Sidecar, QGIS-Brücke im Detail, XAML.
