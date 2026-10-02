# Querschnittsregeln aus Audits und Projektprüfung

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Gesamtaudit 23.09.2026: Behebung A01 bis A18
- Auditkorrekturen: Restbefunde 11 bis 18 (19.09.2026)
- Statuskorrektur und erste Projektprüfung (16.09.2026)
- Gesamtaudit 2026-08-14 — umgesetzte Haertungen

## Gesamtaudit 23.09.2026: Behebung A01 bis A18

Bericht `.tmp/audit-gesamt-2026-09-23/GESAMTAUDIT.md` (Codex, extern). Alle 18 Befunde nachgeprueft,
keiner widerlegt; umgesetzt mit Test und Sabotageprobe. Regeln, die nicht zurueckfallen duerfen:

- **WebGIS senden prueft unmittelbar vor dem Schreiben.** `IGeonisWebGisClient.SchreibeAsync(…, erwarteterStand)`
  vergleicht den letzten Lesestand im Client (`WebGisStandVergleich`, ohne Massnahmenliste) und sendet bei
  Abweichung nichts. `WebGisExportPosition.VomServerBestaetigt` haelt fest, ob das Zuruecklesen den Wert belegt.
  Massnahmen werden nur angelegt, wenn das Elternobjekt seit der Pruefung unveraendert ist (A04/A05).
  Ein Zeitlimit je Objekt ist ein Fehler dieses Objekts, ein Benutzerabbruch beendet den Lauf (A06).
- **Holen: eine nach der Vorschau entstandene Dublette sperrt die ganze Position**, nicht nur die GlobalID (A07).
- **Materialgruppe auch fuer Kanalfirmenwerte** (`ErgaenzeGruppenKatalogeAsync`, Gruppenlisten je Lauf einmal) (A09).
- **Wissenssuche: fehlende Pruefdaten = keine Suche.** `GuardedRetrievalFactory.Sperrliste` /
  `EvalContaminationGuard.LoadEvalHaltungKeysStrict`: nur ein bewusst LEERER Pruefdaten-Ordner schaltet den
  Schutz ab; fehlender Ordner, unlesbare `_candidates.json` oder keine Kennung werfen. Der Start setzt den
  Standard-Root VOR der Pruefung (sonst liefe die Vollprotokoll-Suche ohne Sperrliste) und meldet den Grund
  als Startwarnung; die KI arbeitet dann ohne Vergleichswissen (A10).
- **GPU-Zulassung misst neu, wenn waehrend der Messung eine fremde Ladung endete** (`_ladungen_beendet`,
  `gpu_manager._admit_vram_or_raise`); Zwei-Thread-Test `test_messung_vor_dem_ende_einer_fremden_ladung…` (A11).
- **Kosten: spezifische Begriffe vor allgemeinen** (`CostOptimizationEngine`, laengster Schluessel zuerst;
  «Kurzliner DN 300» bleibt Stueckmassnahme) (A12).
- **Hydraulik ueber das Materialvokabular** (`HydraulikMaterialCatalog.ResolveMitHinweis`): Normalbeton ->
  Beton, GUP -> GFK; ohne Rauheit (z. B. Epoxydharz) gilt die Einstellung, der Bericht nennt es sichtbar.
  Die gemerkte Materialwahl je Haltung bleibt (A13).
- **Schachtfelder: Vorlagenschreibweise vor der WebGIS-Umwandlung aufloesen** («STATUS», «Status »):
  `SchachtRecord.AlsWebGisBegriff` faltet den Namen; der Sendeweg sucht die Liste ueber
  `karte.SewerStudioFeld` (A14).
- **Schachttabelle, Auswahlspalten ohne eigene Liste**: `SchachtTabellenAuswahl` haengt einen Altwert
  hinten an und schreibt nur bei echter Aenderung — blosses Verlassen stempelt keine Handmarke (A15).
- **KINS-DBF fuellt Schachtfelder nur, wenn leer** (Entscheid), jetzt mit Herkunft `Legacy` = Kanalfirma (A16).
- **Sicherung: ein Ordner, der zur Verknuepfung wurde, behaelt seine bisherige Sicherungskopie**, auch im
  Echtzeit-Spiegel, mit Warnung (A01). A17 (jede Warnung steht im `SewerStudio_Sicherung_Protokoll.txt`, nur
  die Anzeige ist begrenzt) baut auf dem Laufprotokoll der parallelen Sicherungsarbeit auf und wird mit ihr
  eingecheckt.
- **«Speichern unter» nur im selben Projektordner** (`SpeichernUnterRegel`, Entscheid): Videos, Fotos, PDFs,
  Kosten und Dossiers liegen relativ zum Ordner; ein anderer Ordner fand sie nicht mehr. Kopie im Explorer
  anlegen (A02/A08).
- **Einzelbild: unbrauchbares Bild ist nie gruen** — «Bild nicht beurteilbar: zu dunkel/zu hell/ohne Struktur/
  unscharf — manuell prüfen» (A03).
- **Python-Sicherheitspruefung erkennt Alias-Kennungen** (PYSEC/GHSA derselben CVE) (A18).


## Auditkorrekturen: Restbefunde 11 bis 18 (19.09.2026)

Damit sind alle 18 Befunde des Audits vom 18.09.2026 bearbeitet.
Abnahme: `docs/audits/2026-09-18-restbefunde/BEHEBUNG.md`.

- **Ein Retrieval entsteht nur ueber `GuardedRetrievalFactory`.** Drei Stellen bauten eine
  eigene Suche, aber nur der `ServiceProvider` reichte die Sperrliste der reservierten
  Pruefhaltungen weiter; Vollprotokoll-Erstellung und Selbsttraining nicht. Ein
  Transportweg ueber die Einstellungen haette dieselbe Falle: Vergisst ein Aufrufer ihn,
  fehlt der Schutz wieder still. Deshalb setzt die Anwendung den Pruefdaten-Root EINMAL
  beim Start (`ConfigureDefaultEvalSetRoot`), und ein Waechter verbietet
  `new RetrievalService(` sonst ueberall im Produktivcode. Nie einen vierten Einstieg
  daneben bauen.
- **Ein verspaetetes Ladeergebnis ersetzt keinen begonnenen Entwurf.**
  `ShellViewModel.ProjectGeneration` zaehlt jeden Projektwechsel; die Uebernahme prueft
  den Stand vom Ladebeginn. Zusaetzlich sperrt `ProjektLadeGuard` waehrend des Ladens
  Neu, Oeffnen und Projektwechsel ueber denselben `IShellOperationGuard`-Weg wie Import
  und Export. Der Dirty-Guard laeuft VOR dem Laden und gilt danach nicht mehr — genau das
  war die Luecke.
- **Beim Umbenennen wandern ALLE Medienverweise mit.** Bisher nur `FotoPaths`; jetzt auch
  `OriginalFotoPaths` und `ProtocolRevision.ImportVideoPaths`. **`OriginalFotoPaths` wird
  dabei nicht dedupliziert** — die Liste gehoert Index fuer Index zu `FotoPaths`, und ein
  Entfernen wuerde Anzeigebild und Originalquelle gegeneinander verschieben.
- **Einstellungen werden in ihrer Reihenfolge geschrieben.** `SettingsWriteOrder` vergibt
  je Auftrag eine Nummer und schreibt im selben kritischen Abschnitt, in dem sie
  entscheidet. Eine blosse Vorabfrage genuegt NICHT: Zwischen Freigabe und Schreiben
  koennen sich zwei Auftraege erneut ueberholen — genau der Fehler, um den es geht.
  Vorher entnahmen `SaveImmediate` und `FlushPendingSave` ihren Auftrag unter Sperre,
  schrieben ihn aber ausserhalb; der langsamere aeltere gewann.
- **Ein verschluckter KB-Loeschfehler ist kein Erfolg.** `TryDeindex` liefert ein
  Ergebnis statt eines leeren `catch`. Die persoenliche Entscheidung bleibt gespeichert —
  sie soll nicht an einer gesperrten KB scheitern —, aber das Training Center meldet
  sichtbar, dass der Eintrag weiter als Vergleichsfall dienen kann.
- **Das Retrieval verwendet nur Vektoren des AKTUELLEN Embedding-Modells**, und der
  Zwischenspeicher ist an das Modell gebunden. Alte und neue Vektoren sind gleich lang,
  stammen aber aus verschiedenen Bedeutungsraeumen: Im Nachweis kam ein fremder Vektor
  mit Score 1,0 als perfekter Treffer zurueck. Lieber kein Vergleichswissen als falsches.
  Der Filter ist fail-closed — nach einem Modellwechsel braucht es einen KB-Neuaufbau.
- **`Content-Length` zaehlt Bytes, `ReadAsync` liefert Zeichen.** LiveControl hatte eine
  eigene Rumpfschleife gegen die Bytezahl und wartete bei Umlauten auf Zeichen, die es
  nie gab. Der gemeinsame `BoundedHttpRequestReader.ReadBodyAsync` rechnet richtig; das
  Duplikat ist geloescht. Nie wieder eine zweite Rumpfschleife daneben bauen.
- `ServiceProvider.cs` lag exakt an der 1000-Zeilen-Grenze. Der Aufbau der
  Wissensdatenbank liegt deshalb jetzt unveraendert in `ServiceProvider.KnowledgeBase.cs`
  (13. Teildatei); die Hauptdatei ist bei 927 Zeilen.

## Statuskorrektur und erste Projektprüfung (16.09.2026)

- `HaltungPruefstatus` verwendet die vorhandene persönliche Markierung
  `BearbeitungErledigt`, niemals `WorkflowStatus` (Sanierung). Offene aktuelle,
  nicht gelöschte KI-Befunde haben Vorrang. Texte: „Bearbeitung offen“,
  „Bearbeitung erledigt“, „KI-Befunde zu prüfen“. Die KI-Ampel sagt nur
  „keine Analyse“, „N offen“ oder „bestätigt“, keine fachliche Gesamtfreigabe.
  Übersicht und nächste Aufgabe verwenden dieselbe Regel. Alte Enum-/Propertynamen
  bleiben kompatibel; `Geprueft` zählt erledigte Haltungen ohne offene KI-Befunde.
- `Application/UseCases/ProjektPruefung` enthält `IProjektPruefung`, Ergebnis/Ziel
  und `ProjektPruefregeln`: Dateiverweise, offene KI-Befunde der Haltungen,
  Meterangaben (bestehende Toleranz 1 m), Schachthöhen und vorhandene
  `ObjektFeldPruefung`-Regeln für bearbeitbare Wurzel-/Unteraktenfelder.
  Höhenregeln werden aus `SchachtHoehenRechnung` wiederverwendet.
- `Infrastructure/Projects/ProjektPruefungService` löst Pfade gegen den echten
  Projektroot auf und prüft vorhandene lokale Dateien lesend. `ImportSourcePathGuard`
  sperrt Netzwerk-/Verknüpfungspfade; nicht prüfbare, fehlende oder leere Dateien
  bleiben als Hinweise sichtbar. Kein rekursives Suchen, kein Schreiben.
  Neue Registrierung `IProjektPruefung` im vorhandenen ServiceProvider: 167 Dienste.
- `ProjektPruefdatenKopie.ErfasseAsync` kopiert nur die von den fünf Regeln gelesenen
  Projektwerte in kurzen UI-Abschnitten und lässt dazwischen Bedienung und Abbruch zu.
  `ProjektPruefungViewModel` führt danach einen einzigen durchgehenden, synchronen
  Vergleich dieser Werte auf dem UI-Thread aus; erst dann prüft der bestehende
  `IProjektPruefung`-Dienst die abgelöste Kopie im Hintergrund. Nach dem Lauf und
  unmittelbar vor einem Sprung wird derselbe Vergleich wiederholt. Auch stille
  Änderungen an aktuellem Protokoll, Objektakten, Schacht-Metadaten und GEONIS-Bezug
  werden so an diesen Grenzen erkannt. Beliebige, nicht synchronisierte
  Hintergrundschreiber bleiben ausserhalb dieses UI-Thread-Vertrags. Andere
  Projektmetadaten und Protokollhistorien gehen nicht in die fünf Regeln ein.
  Projektwechsel oder geänderte Prüfdaten verwerfen das Ergebnis. Der
  `ProjektPruefungView` steht in der Nova-Projektübersicht; er virtualisiert die
  Hinweise und hält die Befehle per Tastatur erreichbar.
- `ProjektPruefpunktNavigation.OeffneAsync` öffnet die passende Objektakte mit
  Feldfokus oder das Protokoll mit markierter `EntryId`. Die neue Öffnung löst
  Medienpfade und persönliche Listenergänzungen vor dem Fenster ausserhalb des
  UI-Threads auf und prüft das Ziel nach dem Warten erneut. Nach dem Protokollfenster
  kehrt die Shell zur Übersicht mit dem zuvor gewählten Hinweis zurück.
  `DataPageProtocolWindowController.Open` und dessen bisherige Aufrufer bleiben
  kompatibel. Der bestehende Speichervorgang im Dialog ist davon nicht betroffen.
  `ProjektDatensatzBeobachter` meldet Listen-/Datensatzänderungen und löst seine
  Abonnements bei Wechsel/Dispose. Shell-Aufgabe und Übersicht reagieren dadurch
  sofort auf Erledigt-Markierungen. Viele Importmeldungen werden je UI-Runde
  gebündelt. Statusbindungen beobachten weiterhin die Felder für Medienänderungen.
- Keine neuen Pakete oder Änderungen am Projektformat. Die erste Prüfung ist kein
  Freigabeprotokoll und ersetzt keine vollständige XTF-Normprüfung beim Export.
  Umfang, Messungen und Grenzen: `docs/PROJEKTPRUEFUNG.md`. Tests:
  `ProjektPruefungTests`, `ProjektPruefdatenKopieTests`, `ProjektPruefstatusTests`,
  `ProjektPruefungViewModelTests`, `ProjektPruefungUiTests`,
  `DataPageProtocolWindowControllerTests` sowie bestehende Status-/Kennzahlentests.

## Gesamtaudit 2026-08-14 — umgesetzte Haertungen

Bericht: `docs/audits/2026-08-14-gesamtaudit.md`. Folgendes ist umgesetzt und darf nicht
zurueckgedreht werden:

- **Python-Sperrdatei:** `sidecar/requirements-lock.txt` ist von 40 bekannten Luecken in
  10 Paketen auf 5 in 2 Paketen gehoben. torch/torchvision/tensorrt wurden NICHT
  angefasst (cu128/sm_120 bleibt). Belegt geprueft: CUDA verfuegbar, 273 Sidecar-Tests
  gruen, echter Grounding-DINO-Lauf mit identischen Treffern. `transformers` bleibt
  bewusst auf 4.57.6: 5.3.0 bricht Grounding DINO
  (`'BertModel' object has no attribute 'get_head_mask'`, real getestet). `setuptools`
  bleibt unter 82, weil torch das verlangt. Beide Ausnahmen stehen mit Beleg in
  `sidecar/security/lock_audit_exceptions.json`; `sidecar/security/audit_lock.py` prueft
  sie in der CI und wird auch bei einer VERALTETEN Ausnahme rot.
- **CI:** `dotnet restore --locked-mode`, NuGet-Schwachstellenpruefung
  (`.github/scripts/check-dotnet-vulnerable.ps1`, wertet JSON aus — die Textmeldung ist
  uebersetzt und ein englischer Textvergleich fand nie etwas), Sperrdatei-Audit,
  Abdeckungsgrenze und auf Commit-Hashes gepinnte Actions.
  Seit 02.10.2026 (Deepscan T1/T3): `timeout-minutes` je Job (dotnet 45, python 25),
  `--blame-hang-timeout 10m` bei allen vier `dotnet test`-Schritten (Zeitgrenze je Test;
  die WPF-Kindprozesse haben eigene 60-90-s-Grenzen) und der Schritt «Werkzeug-Tests
  (tools/)»: alle 16 Python-Testdateien unter `tools/` (EvalVisibilityReview, VideoLabelTool,
  GroundTruthPipeScaleProbe, skill-linter) laufen in der frischen Trainings-Umgebung
  (`requirements-test.txt` genuegt, keine Skip-Regeln). Entfernt man die Host-Pruefung in
  `review_server_security.py`, wird `test_review_server_security.py` rot (gemessen).
- **Programm-Momentaufnahme:** Ein unlesbarer Ordner ist kein stiller Uebersprung mehr.
  `ProgramSnapshotFileCatalog.IsRequiredDirectory` (src, tests, tools, sidecar, .git)
  laesst die Sicherung fehlschlagen; alle anderen erscheinen in Ergebnis, Manifest und
  Dialog. Die fertige ZIP wird vor der Veroeffentlichung geprueft — mit SELBST
  nachgerechneter CRC-Summe, weil System.IO.Compression beim Lesen keine CRC prueft und
  ein Bitfehler in einer unkomprimiert abgelegten Modellgewichtsdatei sonst durchgeht.
  Die SHA-256 der Sicherung liegt als Nebendatei `<name>.zip.sha256` daneben.
- **QGIS-Bruecke:** Token-Pflicht auf BEIDEN Wegen (eigener Server und Live-Control auf
  demselben Port) ueber `QgisBridgeToken`; das Plugin liest den Token aus
  `.qgis_bridge_token` im AppData-Ordner. Fehlermeldungen nach aussen sind neutral.
- **KI-Ampel:** Gruen verlangt zwei unabhaengige BELEGQUELLEN, nicht zwei Zahlenfelder
  (`EvidenceSourceGrouping`). Sprachmodell, die daraus abgeleitete Plausibilitaet, die
  Bild-Beschreibung desselben Modells und die Aehnlichkeit der Prompt-Beispiele sind EINE
  Quelle. Die Gewichtung im Zahlenwert ist unveraendert. Der Anzeigetext heisst
  „KI-Kriterien erfüllt – prüfen" statt „Sicher".
- **Ein-Knopf-Import:** Verwendet fuer Archiv, Plan-PDF, Medien, namensbasierte
  Protokolle, Kanal und Dichtheit dieselbe `IImportFileStagingSession` und denselben
  `.import-transaction.json`-Marker wie der manuelle Import. Das alte
  `IImportedFileLedger` bleibt nur bis zum Beginn von `Publish` ein zusaetzliches
  Sicherheitsnetz und darf danach nichts mehr loeschen. Der Importbericht bleibt
  absichtlich ausserhalb der Transaktion liegen.
- **CSV:** `CsvCell` entschaerft Formelanfaenge (`=`, `+`, `-`, `@`, Tab, CR) zentral;
  negative Zahlen bleiben Zahlen. Kein Exportweg darf das erneut halb umsetzen.
- **Medienpfade:** Der Protokolleditor zeigt nur Mediendateien, keine `..`-Ausbrueche und
  absolute Pfade nur innerhalb erlaubter Wurzeln (`ProtocolEntryEditorMediaRoots`).
- **`async void`:** `VsaCodeExplorerWindow.ApplyAndCloseAsync` ist ein `Task`; die
  Aufrufer gehen ueber `StartApplyAndClose`, das Ausnahmen anzeigt statt die Oberflaeche
  zu beenden.
- **Uebersprungene Tests:** `UebersprungeneTestsWaechterTests` haelt die sieben zulaessigen
  Skip-Stellen namentlich fest. Ein neuer oder entfernter Skip macht den Waechter rot.

### Nachaudit 2026-08-22 — verifizierte Randhaertungen

- Der IBAK-FDB-Import laedt `fbclient.dll` nur noch aus dem Programmordner. Eine DLL
  aus einem Kunden- oder Importordner wird nie als nativer Treiber verwendet.
- Die acht zuvor ungeschuetzten Review-Server unter `tools/EvalVisibilityReview`
  pruefen nun den Loopback-Host. POST akzeptiert nur JSON mit deklarierter Laenge
  und hoechstens 64 KiB; ihre gemeinsame Regel liegt in
  `review_server_security.py`.
- Der KI-Erststart liest den Sidecar-Token nach dem Prozessstart fuer jeden
  Health-Versuch neu. Dadurch funktioniert auch ein Token, den der Sidecar beim
  ersten Start erst auf die Platte schreibt.
- Blockierende Trainingsskripte besitzen endliche Subprocess-Zeitlimits.
  `osd_hd_validierung_vorbereiten.py` ersetzt vorhandene Ziele nur mit `--force`
  und eigenem Arbeitsmarker; fremde Ordner und Verknuepfungen bleiben unangetastet.
- `TrainingCenterViewModel` gibt seinen eigenen Knowledge-Base-HTTP-Client beim
  Schliessen des Fensters frei. Dispose ist mehrfach sicher aufrufbar.


### Deepscan 02.10.2026, Welle 2 (Klone und leere catch)

- **Leere `catch` (R7):** `SilentCatchGuardTests` lehnt Floskel-Kommentare ab (`ignore`, `next`, `non-fatal`, `best effort` ohne Grund, `skip`, `swallow` ...). Ein leerer `catch` braucht einen Grund: welcher Fehler, warum harmlos, was gilt stattdessen. Die Anzahl der Kommentar-`catch` ist nach oben UND unten festgenagelt (`MaxKommentarCatchBloecke`, heute 230); wer einen Block entfernt, zieht den Wert nach. Einzige Ausnahme mit Verweis: `ParsedHoldingDistributionController` (nach Codex).
- **Schattenauswertung (R5):** Ein Rechenfehler der Bewertung oder der Massnahmenempfehlung wird als `SchattenStatus.Fehler` mit Fehlertext gespeichert, nicht als Teilergebnis. Ein Fehler gilt immer als veraltet (naechster Lauf rechnet neu), ist kein Vergleich und erreicht die KI nicht.
- **Eine Stelle statt zwei Kopien (B4-B6):** `NovaWorkspaceLayout` (Haltungen/Schaechte, Auf-/Zuklappen und Sichtbarkeit), `DossierPdfBausteine` (Kopf, Zustandszelle, Metadaten der Dossier-PDFs; die Schachtliste behaelt ihren Ersatztext am Wappenplatz), `WindowBoundsHelper` (Dialoge im Arbeitsbereich), `CostStoreFileProbe.TryRemove` (User-Overrides), `CadastreTableStamp` (Herkunftszeile und Frische der Kataster-Tabellen), `VsaFormularVerdrahtung` (VSA-Formular in Eintrags-Editor und Beobachtungs-Katalog), `FeldzustandWiederherstellung` (Rueckgaengig am Datensatz), `WorkbenchSourceSuggestionFactory` (Quellvorgabe des Pruefplatzes). Neue Kopien dieser Abschnitte gehoeren nicht mehr in die Seiten, sondern in diese Helfer.
