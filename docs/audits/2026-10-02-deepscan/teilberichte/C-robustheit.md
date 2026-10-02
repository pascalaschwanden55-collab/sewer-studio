# Deepscan 02.10.2026 – Prüfer C-robustheit: Fehlerbehandlung und Robustheit (src/)

Prüfstand: `release-master`, HEAD `fbff3dd4d`, nur gelesen. Codex-Bereiche (PlayerWindow*, UI/Player,
ProjectImportOrchestrator, ParsedHoldingDistributionController, SettingsFullBackup*,
HaltungsgrafikSvgBuilder, ServiceProvider) ausgenommen. Messskripte und Rohlisten liegen im Scratchpad
unter `deepscan-1002/crob/` (`catches.py`, `classify.py`, `empty.txt`, `swallow.txt`).

## 1) Kurzfazit

Die Fehlerbehandlung ist deutlich besser als ihr Ruf: Von 234 leeren `catch`-Blöcken ausserhalb der
Codex-Bereiche sind rund 200 gewollt (Aufräumen, Protokoll, optionale Suche), nur 4 verschlucken
belegbar einen Fehler, den Pascal sehen müsste. Das grössere Risiko steckt nicht in den leeren
Blöcken, sondern in einem stillen Baustein: `SafeFileEnumeration` überspringt unlesbare oder
verknüpfte Ordner, und nur 2 von 87 Aufrufen werten das aus. Das ist dasselbe Muster wie bei
Göschenen («0 Fehler», aber Dateien fehlen). Daneben gelten drei Schutzregeln aus CLAUDE.md nur an
einem Teil der Stellen: Eval-Schutz im Trainingsspeicher, Befundfotos im Temp-Ordner und der
Abbruch beim Import. Alle Korrekturen sind klein und lassen sich mit je einem Test absichern.

## 2) Befundtabelle

| ID | Prio | Titel | Grösse | Nutzen in einem Satz |
| --- | --- | --- | --- | --- |
| R1 | P1 | Unlesbare Ordner fallen still aus Import und Verteilung | M | Ein Bericht mit «0 Fehler» heisst wieder wirklich «alles gefunden». |
| R2 | P2 | Eval-Schutz im Trainingsspeicher ist «offen bei Fehler» | S | Fehlt der Prüfdatenordner, landen Prüfhaltungen nicht mehr unbemerkt im Lernbestand. |
| R3 | P2 | Befundfotos: drei Temp-Rückfälle ohne Hinweis, Projektprüfung erkennt sie nicht | S | Pascal erfährt sofort, wenn ein Foto im Temp-Ordner liegt, statt es später zu verlieren. |
| R4 | P2 | Abbruch beim Import erreicht die Verteilung nicht | M | «Abbrechen» wirkt auch im langen Schritt «Sammelprotokolle aufteilen und Videos verteilen». |
| R5 | P2 | Schattenauswertung: Rechenfehler sieht aus wie ein Ergebnis | S | Ein gescheiterter Vergleich wird als Fehler gezeigt und nicht als «Gleich» oder «Kein Vergleich» gespeichert. |
| R6 | P2 | Training Center: Verteilung läuft im UI-Thread, ohne Abbruch und ohne Pfadwächter | M | Kein eingefrorenes Fenster; es wird nichts ungeschützt neben die Kundenablage geschrieben. |
| R7 | P2 | Wächter gegen leere `catch` verlangt nur einen Kommentar | S | Neue stille Fehlerpfade brauchen künftig eine echte Begründung. |
| R8 | P3 | Kleine stille Verluste: Importbericht, Konfliktdateien, Trainingsordner | S | Drei Stellen melden künftig «nicht geschrieben» bzw. «übersprungen». |
| R9 | P3 | `ProjectWritePathGuard` ist `internal`; ungeschützter toter Schreibweg | S | Die Regel «Schreiben nur über den Pfadwächter» wird auch ausserhalb von Infrastructure erreichbar. |
| R10 | P3 | Regel «Handwert, auch bewusst leer» widerspricht `FuelleLeeresFeld` | S | Eine einzige, getestete Lesart der Handwertregel statt zwei widersprüchlicher Texte. |

## 3) Befunde im Detail

### Messung leere `catch`-Blöcke (Grundlage für R7/R8)

- Gesamt `catch` in `src/`: 1'607. Syntaktisch leer: **237** (wie am 30.09., also unverändert).
  Davon 3 in Codex-Bereichen, **234** geprüft. 233 tragen einen Kommentar; mein Parser zählte zwei
  weitere fälschlich (Doku-Kommentar in `BestEffort.cs:10`, Text in `FeedbackIngestionService.cs:101`).
- Zusätzlich **234 nicht leere `catch`**, deren Rumpf nur `return null/false/[]`, `continue` oder
  `break` ist; 144 davon fangen jede Ausnahme. Diese sind nicht klassiert (siehe Grenzen); die
  Stichprobe unten bestätigt, dass die meisten sauber «nicht gefunden» liefern.

Einteilung der 234 leeren Blöcke (Einzelliste im Scratchpad `crob/empty.txt`):

| Klasse | Anzahl | Typische Fälle |
| --- | --- | --- |
| (a) gewollt und harmlos | **200** | Temp-Datei löschen nach Fehler, `Kill`/`Close`/COM-Freigabe, Log- und Telemetriefehler, `OperationCanceledException` beim Schliessen, Zwischenablage, WPF-`DisplayIndex`, PATH-Suche, Ausweichstrategie mit eigener Meldung |
| (b) verschluckt einen sichtbaren Fehler oder verfälscht ein Ergebnis | **4** | einzeln unten (R5, R8) |
| (c) unklar, Prüfung bei Berührung | **30** | Suchwege in Import und Verteilung (`InspectionProtocolFileLocator.cs:262/492/509`, `HoldingFolderDistributor.SidecarXtf.cs:253/334/346`, `KinsImportService.cs:324`, `WinCanDbImportService.cs:665`, `SchachtProtocolFileLocator.cs:81`, `PdfChunking.cs:182`, `XtfPrimaryDamageFormatter.cs:307`), Stammdaten (`CostCalculationService.cs:115/161`, `DropdownOptionsStore.cs:235/265`), Wiederherstellung (`ProjectRecoveryService.cs:286`, `SettingsRestorePointStore.cs:57`), Altmigration (`KnowledgeBasePathService.cs:275/294`), `EvalContaminationGuard.cs:323` |

Klasse (b) einzeln:

1. `Infrastructure/Schatten/SchattenAuswertungService.cs:135` – Bewertungsfehler → Ergebnis mit Status `NurRegeln` (R5).
2. `UI/Services/ImportRunWorkflowController.cs:381` – Importbericht konnte nicht geschrieben werden; keine Meldung (R8).
3. `Infrastructure/Media/MediaConflictCenterService.cs:126` – unlesbare Konfliktdatei fehlt in der Konfliktliste (R8).
4. `Infrastructure/Ai/Training/TrainingCenterImportService.cs:55` – ganzer Fallordner fehlt beim Scan (R8).

Nicht leer, aber gleiche Wirkung: `SchattenAuswertungService.cs:144` (`catch { empfehlung = Empty; }`).

### R1 (P1) – Unlesbare Ordner fallen still aus Import und Verteilung

**Beleg.** `Application/Common/SafeFileEnumeration.cs:78-112`: Bei `UnauthorizedAccessException`,
`DirectoryNotFoundException` und `IOException` wird der Ordner übersprungen. Ein Eintrag entsteht nur,
wenn der Aufrufer eine Liste mitgibt:

```csharp
catch (UnauthorizedAccessException)
{
    skippedDirectories?.Add(dir);
    continue;
}
```

Auch verknüpfte Ordner (Junctions) werden ausgelassen (`CanEnterDirectory`, Zeile 140-155). Das ist
als Schutz richtig, bleibt aber ebenfalls unsichtbar. Es gibt 87 Aufrufe, 72 davon in Import,
Verteilung und Medien. Nur **2** übergeben `skippedDirectories`, beide in `ShaftDistributionService.cs:28/103`.
Ohne Liste laufen zum Beispiel:
- `HoldingFolderDistributor.cs:48`, `:606`, `:752` (PDF-Quellen der Haltungs- und Schachtverteilung),
- `KinsImportService.cs` (8 Aufrufe, Formaterkennung), `WinCanDbImportService.cs` (4),
  `InspectionProtocolFileLocator.cs` (3), `DichtheitImportDistributionService.cs` (3),
  `ImportPostProcessingController.cs:214`, `TrainingCenterImportService.cs`.

**Warum es ein Risiko ist.** Liegt ein Teil der Kundenablage in einem gesperrten Ordner, in einer
OneDrive-Platzhalterdatei oder hinter einem zu langen Pfad, fehlen dessen PDFs und Videos im Lauf.
Der Bericht zählt sie nicht als Fehler. Für Pascal sieht das aus wie Göschenen am 04.09. und
Hellgasse am 20.08.: «0 Fehler», aber Protokolle fehlen. CLAUDE.md verlangt: «ein Teillauf bleibt
bis ins Ergebnis als unvollständig sichtbar». Wie oft das bei echten Kundendaten vorkommt, ist nicht
gemessen (Vermutung: selten, aber jedes Mal teuer).

**Empfehlung.** Keinen neuen Baustein bauen, sondern die vorhandene Liste nutzen:
1. In den Einstiegen von Import und Verteilung (die drei `HoldingFolderDistributor`-Stellen,
   `KanalImportDistributionService`, `DichtheitImportDistributionService`, `WinCanDbImportService`,
   `KinsImportService`) eine `List<string> skipped` übergeben.
2. Ist sie nicht leer, eine Zeile «Ordner nicht lesbar, übersprungen: …» in die Meldungen schreiben
   und in die bestehende `Fehlerbilanz` aufnehmen (wie in `ImportMediaPhase` für die Kopierfehler).
3. Später optional eine Fitness-Regel: In `Infrastructure/Import` und `HoldingDistribution` ruft
   niemand `EnumerateFilesSafe` ohne `skippedDirectories` auf.

**Test-Idee.** Testordner mit einem Unterordner, dessen Aufzählung scheitert (Junction auf einen
fehlenden Pfad; die Junction-Tests laufen laut Gedächtnis seit 13.08. im Entwicklermodus). Erwartung:
Bericht nennt den Ordner, die Fehlerzahl ist grösser als 0.

**Fertig, wenn** jeder Import- und Verteillauf übersprungene Ordner namentlich im Bericht zeigt und
ein Test das für Haltungsverteilung und Import belegt. Grösse **M** (7 Dateien, je wenige Zeilen).

### R2 (P2) – Eval-Schutz im Trainingsspeicher ist «offen bei Fehler»

**Beleg.** Die Regel «Prüf- und Eval-Daten nie ins Training» ist an fast allen Einstiegen streng:
`UI/Services/EvalContaminationSetProvider.cs:20-45` wirft bei fehlendem oder defektem Ordner
(«darf Training niemals still freigeben»). `GuardedRetrievalFactory.cs:48` nutzt
`LoadEvalHaltungKeysStrict`. Acht weitere Stellen rufen den strengen Provider.

Die Ausnahme ist der zentrale Speicher `Infrastructure/Ai/Training/TrainingSampleFileStore.cs:276-285`:

```csharp
var hashes = EvalContaminationGuard.LoadEvalImageHashes(EffectiveEvalSetRoot);
var holdings = EvalContaminationGuard.LoadEvalHaltungKeys(EffectiveEvalSetRoot);
if (hashes.Count == 0 && holdings.Count == 0)
    return input;   // ungefiltert
```

Die nicht strengen Lader liefern bei fehlendem Ordner oder defekter Datei eine leere Menge
(`EvalContaminationGuard.cs:82-86`, `:115`, `:241`, `:323`). Dazu kommt eine andere Bedeutung von
«leer»: Der Provider schaltet bei leerer Einstellung bewusst ab. Der Speicher fällt dagegen auf
`SEWERSTUDIO_EVAL_SET_ROOT` oder `C:\KI_BRAIN\eval_set` zurück (`TrainingSampleFileStore.cs:51-54`).
Einen Test für «konfigurierter Ordner fehlt» gibt es nicht; `TrainingSamplesStoreEvalLeakageTests`
prüft nur den Normalfall.

**Risiko für Pascal.** Zieht er `KI_BRAIN` auf ein anderes Laufwerk um oder ist die Platte nicht
eingesteckt, speichert `MergeAndSaveAsync` Prüfhaltungen ohne Warnung in `training_samples.json`.
Die nachgelagerte Suche (`GuardedRetrievalFactory`) ist streng belegt. Ob der YOLO-Exportplan
denselben strengen Satz nutzt, habe ich nicht verfolgt. Es fehlt also mindestens eine
Schutzschicht, der Lernbestand ist danach nicht mehr sauber.

**Empfehlung.** Im Speicher den strengen Weg nutzen: Ist ein Ordner gesetzt oder der Standardordner
vorhanden, dann `LoadEvalHaltungKeysStrict` aufrufen und Hashes nur über den validierten Weg laden.
Bei Fehler das Speichern mit klarer Meldung ablehnen. Die Lesart von «leer» an den Provider angleichen.

**Test-Idee.** `ConfigureEvalProtection(@"<tmp>\fehlt")`, dann `MergeAndSaveAsync` mit einem Sample.
Erwartung: Ausnahme mit Ordnername, Datei unverändert.

**Fertig, wenn** ein fehlender oder defekter, aber konfigurierter Prüfdatenordner das Speichern
sichtbar stoppt (Test) und beide Klassen dieselbe Regel für «leer» haben. Grösse **S**.

### R3 (P2) – Befundfotos: drei Temp-Rückfälle ohne Hinweis

**Beleg.** Die Regel «Befundfotos nie in den Temp-Ordner» (Anlass: 16 verlorene Fotos, 12.09.) ist
für den Normalfall umgesetzt. Drei Wege fallen aber still zurück:
- `Application/UseCases/VsaFotos/VsaFotoAblagePolicy.cs:25-27`: ohne Videopfad Ziel `Path.GetTempPath()`.
- `Application/UseCases/VsaFotos/VsaFotoAblage.cs:29-36`: Scheitert das Verschieben, wird der
  Temp-Pfad zurückgegeben, ohne Signal an den Aufrufer.
- `Infrastructure/Ai/CodingFramePhotoFileStore.cs:57-60`: ohne Video `…\Temp\SewerStudio\coding_ai_frames`;
  der Pfad geht über `entry.FotoPaths.Add(path)` ins Projekt (Zeile 36).
- Dazu `UI/Ai/Coding/CodingSnapshotTargetPolicy.cs:15-17` (Aufrufer im Codex-Bereich PlayerWindow).

Die Projektprüfung meldet nur «Datei fehlt» oder «leer» (`Infrastructure/Projects/ProjektPruefungService.cs:23-44`).
Ein Foto unter `%TEMP%` gilt dort als in Ordnung.

**Risiko für Pascal.** Ein Foto sieht gespeichert aus. Nach dem nächsten Aufräumen von Windows ist
es weg. Er merkt es erst bei der Sicherung oder beim Bericht, wie am 12.09.

**Empfehlung.** Zwei kleine Schritte:
1. `VsaFotoAblage.Uebernehme` gibt zusätzlich «liegt noch im Temp» zurück; der Aufrufer zeigt einen
   Warn-Toast «Foto nur vorläufig gespeichert, bitte Video laden».
2. `ProjektPruefungService.Dateifehler` meldet Pfade unter `Path.GetTempPath()` als
   «liegt im Temp-Ordner und geht beim Aufräumen verloren».

**Test-Idee.** Prüfregel-Test mit einem Fotopfad unter einem simulierten Temp-Wurzelordner.
`VsaFotoAblageTests` um den Fall «Ziel gesperrt» mit erwarteter Markierung erweitern.

**Fertig, wenn** die Projektprüfung Temp-Fotos auflistet und beide Aufnahmewege einen sichtbaren
Hinweis geben (je ein Test). Grösse **S**.

### R4 (P2) – Abbruch beim Import erreicht die Verteilung nicht

**Beleg.** `Infrastructure/Import/ImportMediaPhase.cs:66` liest den Abbruch
(`var ct = ctx?.CancellationToken …`) und gibt ihn nur an die Fotoverteilung weiter (Zeile 194-200).
Die drei folgenden Schritte erhalten ihn nicht, und dazwischen wird er nicht geprüft:
`_protocolDistributor.Distribute(...)` (Zeile 91), `_kanalDistributor.Distribute(...)` (Zeile 122,
Fortschrittstext «Sammelprotokolle aufteilen und Videos verteilen …»), `_dichtheitDistributor.Distribute(...)`
(Zeile 142). `KanalImportDistributionService.cs` (789 Zeilen) und `HoldingFolderDistributor.cs`
(800 Zeilen) enthalten **0** Abbruchstellen.

**Risiko für Pascal.** Drückt er während der Verteilung eines grossen Projekts auf «Abbrechen»
(Göschenen: 1'003 Seiten, 239 Haltungen), läuft der Schritt bis zum Ende weiter. Das Fenster wirkt
hängend, der Abbruch kommt erst danach an.

**Empfehlung.** Additiv einen optionalen `CancellationToken` an die drei `Distribute`-Methoden
anhängen (Standard `default`). Ihn je Haltung oder Chunk prüfen und in `ImportMediaPhase` vor jedem
der drei Schritte `ct.ThrowIfCancellationRequested()` aufrufen. Die Staging-Transaktion nimmt
Begonnenes zurück; das ist schon heute der Weg bei Fehlern.

**Test-Idee.** Kanalverteilung mit vielen Haltungen und einem Token, das nach der ersten Haltung
abbricht. Erwartung: `OperationCanceledException`, keine weiteren Zielordner, Staging rückgängig.

**Fertig, wenn** ein Abbruch spätestens nach der laufenden Haltung greift (Test). Grösse **M**.
Hinweis: Der Aufruf liegt neben `ProjectImportOrchestrator` (Codex-Bereich); die Änderung in
`ImportMediaPhase` mit Codex abstimmen.

### R5 (P2) – Schattenauswertung: Rechenfehler sieht aus wie ein Ergebnis

**Beleg.** `Infrastructure/Schatten/SchattenAuswertungService.cs:130-163`:

```csharp
try { vsaOk = _vsa.EvaluateRecord(klon).Ok; }
catch { /* Noten bleiben leer */ }
...
catch { empfehlung = MeasureRecommendationResult.Empty; }
return new SchattenHaltungErgebnis { Status = SchattenStatus.NurRegeln, ... };
```

`SchattenStatus` kennt keinen Fehlerwert (`Application/Schatten/SchattenAuswertungDtos.cs:7-13`).
Das Ergebnis wird mit `CodierungsHash` gespeichert. Neu gerechnet wird nur bei geänderter Codierung
(`SchattenauswertungPageViewModel.cs:258-260`). `SchattenVergleich.Bewerte` vergleicht danach nur, was
da ist: Scheitert die Klasse und klappt die Massnahme, wird nur die Massnahme verglichen.

**Risiko für Pascal.** Eine Haltung, deren Bewertung abstürzt, erscheint als «Kein Vergleich» oder
sogar als Übereinstimmung bei der Massnahme. Das bleibt so, bis er die Codierung ändert.

**Empfehlung.** Neuer Status `Fehler` mit Fehlertext, gefüllt in beiden `catch`. Die Seite zählt
Fehler getrennt, und `istVeraltet` gilt für `Fehler` immer, damit der nächste Lauf neu rechnet.

**Test-Idee.** Ein `_vsa`-Ersatz, der wirft. Erwartung: `Status == Fehler`, Zeile «Fehler», ein neuer
Lauf rechnet die Haltung erneut.

**Fertig, wenn** ein Bewertungsfehler als Fehler sichtbar ist und nicht im Speicher bleibt (Test). Grösse **S**.

### R6 (P2) – Training Center: Verteilung im UI-Thread, ohne Abbruch und Pfadwächter

**Beleg.** `Infrastructure/Ai/Training/TrainingCenterImportService.cs:387-482`: `DistributeByHaltungAsync`
ist synchron und endet mit `Task.FromResult(...)`. Gleiches gilt für `ScanAsync` (Zeile 20-60).
Der Aufrufer `UI/ViewModels/Windows/TrainingCenterViewModel.cs:337-338` wartet direkt darauf, ohne
`Task.Run`. Text aus dem ganzen Sammel-PDF lesen (Zeile 396), Videoindex rekursiv bauen und
schreiben läuft also im UI-Thread, ohne Abbruch.
Ziel ist `UI/Ai/Training/TrainingCenterDistributionWorkflow.cs:80-85`, also
`<Elternordner des PDF-Ordners>\<PDF-Name>_Training`. Das liegt neben der Kundenablage. Geschrieben
wird ohne Pfadwächter: `Directory.CreateDirectory`, JSON überschreiben, `File.CreateSymbolicLink`,
bei Fehlschlag eine `.link`-Datei (Zeile 441-465).

**Risiko für Pascal.** Das Fenster friert bei grossen PDFs ein. Zudem entstehen Dateien neben dem
Kundenexport. Das verändert keine Originale, verletzt aber die Regel «Schreiben nur über die
Pfadwächter» und folgt einem Ordner, der eine Verknüpfung sein kann.

**Empfehlung.** In `TrainingCenterViewModel` mit `Task.Run` und einem Abbruch-Token aufrufen. Im
Dienst einen Token annehmen und je Chunk prüfen. Vor dem Schreiben `EnsureNotReparsePoint`
beziehungsweise den Staging-Wächter auf den Zielordner anwenden. Den Symlink-Versuch streichen;
der `.link`-Rückfall ist ohnehin der Normalfall ohne Adminrechte.

**Test-Idee.** Zielordner als Junction: Erwartung Ablehnung ohne Schreiben. Token vor dem zweiten
Chunk abbrechen: Erwartung nur ein Ordner.

**Fertig, wenn** das Fenster während der Verteilung bedienbar bleibt, «Abbrechen» wirkt und ein
Junction-Ziel abgelehnt wird (Tests). Grösse **M**.

### R7 (P2) – Wächter gegen leere `catch` verlangt nur einen Kommentar

**Beleg.** `tests/AuswertungPro.Next.UI.Tests/SilentCatchGuardTests.cs:11-13` sucht nur nach
`catch {}` ohne jeden Inhalt. Deshalb tragen alle 233 leeren Blöcke einen Kommentar. 21 davon sind
reine Floskeln, zum Beispiel `// ignore` (`PdfChunking.cs:182`), `// ignore folder errors`
(`TrainingCenterImportService.cs:55`), `/* next */` (`HaltungsDossierPdfBuilder.cs:601`),
`// Swallow layout exceptions` (`SanierungsmassnahmenWindow.xaml.cs:229`). Zwei der vier (b)-Fälle
stehen unter solchen Floskeln. Eine Sperrklinke auf die Anzahl gibt es nicht; die Zahl stieg von
232 (27.09.) auf 237 (30.09.).

**Warum es bremst.** Wer einen kritischen Ablauf ändert, kann am Kommentar nicht erkennen, ob jemand
entschieden hat oder nur den Wächter beruhigt hat. Die Regel vom 27.09. («bei Berührung entscheiden»)
hat kein Werkzeug.

**Empfehlung.** `SilentCatchGuardTests` um zwei Regeln ergänzen: (1) Obergrenze 237 als Sperrklinke,
wie in `MaintainabilityFitnessTests`. (2) Floskelkommentare (`ignore`, `swallow`, `non-fatal`,
`next`, `skip …` ohne Grund) gelten als leer, mit einer namentlichen Ausnahmeliste für heute.

**Fertig, wenn** ein neuer `catch { // ignore }` den Test rot macht und die Zahl nicht mehr steigen
kann. Grösse **S**.

### R8 (P3) – Kleine stille Verluste

| Stelle | Was passiert | Korrektur |
| --- | --- | --- |
| `UI/Services/ImportRunWorkflowController.cs:370-384` | Scheitert `ExportReport`, gibt es keinen Bericht und keinen Hinweis. Pascals Diagnoseweg beginnt aber mit `__IMPORT_REPORTS`. | Im `catch` eine Zeile «Importbericht konnte nicht geschrieben werden: …» an `SetDetailsText` anhängen. |
| `Infrastructure/Media/MediaConflictCenterService.cs:117-128` | Eine unlesbare `_VIDEO_MISSING/AMBIGUOUS.txt` fehlt in der Konfliktliste. Das Video bleibt ohne Hinweis unzugeordnet. | Zähler «n Konfliktdateien nicht lesbar» im `ScanResult`-Hinweis (das Feld gibt es schon). |
| `Infrastructure/Ai/Training/TrainingCenterImportService.cs:31-57` | Ein Ordnerfehler lässt den ganzen Fall weg (`// ignore folder errors`). | Übersprungene Ordner zählen und im Scan-Log nennen. |

Test-Idee je Fall: Ersatz-Delegat wirft, erwarteter Text im Ergebnis. Grösse **S** zusammen.

### R9 (P3) – `ProjectWritePathGuard` ist `internal`; ungeschützter toter Schreibweg

**Beleg.** `Infrastructure/Import/ProjectWritePathGuard.cs:7` ist `internal sealed`. Application und UI
können den Wächter nicht nutzen. Schreibwege dort bauen ihre Ziele selbst, zum Beispiel
`VsaFotoAblage` (R3). Toter Weg: `Application/Media/PhotoImportService.cs:10-21` kopiert mit
`File.Copy` ohne Wächter ins Projekt. Er ist im DI registriert (`ServiceProvider.cs:608`), wird aber
nirgends aufgerufen (kein Treffer für `ImportFolderToProjectMedia`/`.PhotoImport` ausser der
Definition). Ähnlich: `CodingSessionService.CompleteSession()` (Zeile 130-178) startet
`_ = PersistTrainingSamplesFromEventsAsync(...)` ohne Beobachtung. Ausserhalb von Tests ruft nur noch
die Schnittstellen-Vorgabe diesen Weg auf; produktiv läuft `CompleteSessionAsync`.

**Empfehlung.** `PhotoImportService` samt Schnittstelle und Registrierung entfernen (Registrierung
liegt in `ServiceProvider`, also mit Codex abstimmen). Für Schreibziele in Application eine
schmale Schnittstelle `IProjectWriteTarget` in Application anbieten, die Infrastructure mit dem
vorhandenen Wächter erfüllt. Erst einführen, wenn R3 oder R6 sie braucht.

**Fertig, wenn** kein ungeschützter Projekt-Schreibweg ohne Aufrufer mehr existiert. Grösse **S**.

### R10 (P3) – «Handwert, auch bewusst leer» widerspricht `FuelleLeeresFeld`

**Beleg.** CLAUDE.md (Querschnitt) und `docs/architektur/webgis.md:416` sagen: Ein Handwert, auch ein
bewusst leerer, wird nie überschrieben. `Domain/Models/HaltungRecord.cs:131-176` (`FuelleLeeresFeld`)
füllt dagegen jedes leere Feld und setzt `UserEdited = false`. Die Begründung steht dort: Im Raster
geleerte Felder behalten die Handmarke. Das betrifft 9 Aufrufer, unter anderem
`LeereFelderAnwender.cs:51/84` (QGIS-Nachfüllen), `SchachtMasse.cs:75/179`,
`WebGisImportUseCase.cs:361/384` und `GeoShopZiel.cs:85/98`. `LeereFelderAnwenderTests.cs:62-73`
hält dieses Verhalten fest. Ob WebGIS und GeoShop «bewusst leer» vorher im Planer ausfiltern, habe
ich nur teilweise gesehen (`GeoShopAbgleichPlanBuilder.cs:84` prüft `Handgesetzt` für Kennungen).

**Warum es bremst.** Wer die Regel aus CLAUDE.md liest, baut falsche Tests oder «repariert» das
Gegenteil. Fachlich lässt sich «bewusst leer» im Modell heute nicht von «im Raster geleert» trennen.

**Empfehlung.** Mit Pascal die eine gültige Lesart festlegen und in CLAUDE.md und `webgis.md`
nachziehen. Falls «bewusst leer» schützen soll: Das Raster setzt beim Leeren `UserEdited`
ausdrücklich, und `FuelleLeeresFeld` respektiert es. Das ist eine fachliche Entscheidung, keine
reine Codekorrektur.

**Fertig, wenn** Doku und `LeereFelderAnwenderTests` dieselbe Regel nennen. Grösse **S**
(Doku), **M** falls sich das Verhalten ändert.

## 4) Was gut ist und erhalten bleiben soll

- **`BestEffort`** (`Application/Common/BestEffort.cs`) mit Tageslog-Sink; 171 Aufrufe. Das ist der
  richtige Ersatz für stille Blöcke und sollte bei R7/R8 der Standard sein.
- **Fail-closed bei Verknüpfungen:** `DistributionReconciliationService.IstVerknuepfung` und
  `ProtocolEntryEditorMediaPathResolver.IsReparsePoint` liefern bei Lesefehlern `true` («nicht
  anfassen»). Bei der Kanalverteilung geht jede Kopie über `ProjectWritePathGuard` oder das Staging
  (`KanalImportDistributionService.cs:326-341`).
- **Handwert-Schutz an einer Stelle:** `HaltungRecord.SetFieldValue` weist automatische Schreibvorgänge
  auf `UserEdited` zentral ab (Zeile 192). Direkte `Fields[...] =`-Zuweisungen gibt es nur 9-mal,
  alle in Migrations- und Strukturcode.
- **Pflichtbeilagen werden gezählt:** Der Merge-Dienst ist absichtlich tolerant (`PdfMergeService.cs:76`),
  aber `PdfMergeVerification` prüft für Dossiers die Seitenzahl und wirft bei Abweichung.
- **Async-Ränder sind abgesichert:** `SafeFireAndForget` protokolliert ins Tageslog. Globale Handler
  (`App.xaml.cs:169-185`) fangen Dispatcher-, AppDomain- und unbeobachtete Task-Fehler. 19 der 32
  `async void` ausserhalb der Codex-Bereiche haben eigenes try/catch; die übrigen 13 rufen Abläufe, die selbst fangen (Stichprobe: ReviewCorrect, TeacherRefresh, OverlayCanvas).
- **HttpClient-Besitz ist sauber:** `OllamaClient` (`_ownsHttp`), `QuickScanSession`,
  `MeterTimelineService(ownedResource: …)` und `DataPageVideoAnalysisController` (Cache je Timeout,
  Dispose) geben eigene Clients frei. Ein Socket-Problem habe ich nicht gefunden.
- **Göschenen-Lehre umgesetzt:** `ImportMediaPhase` zählt name-basierte Kopierfehler in die
  `Fehlerbilanz` (Zeile 100-108). Die Suche nach `if (!x.Success) continue;` ohne Zählung fand im
  Import nichts mehr.

## 5) Grenzen der Prüfung

- Nur gelesen, nichts gebaut oder ausgeführt. Die Zahlen stammen aus eigenen Python-Skripten
  (Klammerzählung, keine Roslyn-Analyse). Die Klassierung (a)/(c) ist teils per Stichwort
  vorsortiert und von Hand nachgezogen; bei (c) ist sie bewusst vorsichtig.
- Die 234 nicht leeren «return null/false»-Blöcke sind nur stichprobenweise geprüft
  (`XtfHoldingFileReader:40`, `PdfMergeService`, `KinsImportService`, `ImportSourcePathGuard`);
  alle geprüften waren in Ordnung.
- Codex-Bereiche ausgeklammert. Dort wurzeln zwei Punkte: `CodingSnapshotTargetPolicy` (Aufrufer
  PlayerWindow, R3) und die Registrierungen in `ServiceProvider` (R2, R9).
- Ob unlesbare Ordner in echten Kundendaten vorkommen (R1), ist nicht gemessen. Ebenso nicht
  geprüft: ob der YOLO-Exportplan den strengen Eval-Satz nutzt (R2) und ob WebGIS/GeoShop «bewusst
  leer» vorher ausfiltern (R10).
- Laufzeitverhalten (Einfrieren in R6, Abbruchdauer in R4) ist aus dem Code abgeleitet, nicht gemessen.
