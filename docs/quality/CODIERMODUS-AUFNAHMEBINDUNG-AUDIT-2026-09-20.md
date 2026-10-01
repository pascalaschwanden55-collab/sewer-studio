# Erreichbare Sonderpfade und ihre Aufnahmebindung

Ausgangsbefunde einer lesenden Codepruefung des echten MultiModel-Codiermodus am
20.09.2026. Die anschliessende begrenzte Umsetzung ist unten dokumentiert.
Keine Modellinferenz fuer diese Pruefung oder Umsetzung.
Aufrufkette: `CodingMultiModelInferenceWorkflow` prueft Boundary und Structural
vor dem normalen Ergebnis-/Finding-Workflow. Beide Sonderwege sind damit auch
bei Nutzung des MultiModel-Dienstes tatsaechlich erreichbar.

## 1. Structural kann eine alte Ereigniszeit uebernehmen

`PlayerWindow.Coding.Ai.Classifier.Structural.cs:20` liefert
`_codingSessionHost.CurrentVideoTime`. `CodingStructuralClassifierCommandWorkflow.cs:51`
bevorzugt diesen Wert gegenueber der Aufnahmezeit. Der Hostwert wird bei manueller
Ereigniserstellung oder Video-/Meternavigation gesetzt, nicht bei jeder Analyse
(`PlayerWindow.Coding.Events.cs:60`, `CodingNavigationController.cs:90`).

Reproduzierbarer Datenfall: Nach manueller Navigation bei 30 s liefert eine
Analyse bei 120 s einen BCA/BCC-Klassifikatorbefund ohne Detektionen. Meter und
Klassifikation stammen aus 120 s, die neue Zeile bekommt 30 s. Ein bestehender
Test bestaetigt die gegenwaertige Prioritaet bereits mit Capture=4,5 s und
CurrentVideoTime=21 s (`CodingStructuralClassifierCommandWorkflowTests`).

Die Fotoaktion nutzt weiter den asynchronen Anhang
(`PlayerWindow.Coding.Ai.Classifier.Structural.cs:39`). Praezisierung: Dessen
PreferredFrame ist normalerweise eine erneute ffmpeg-Extraktion am gespeicherten
Analysezeitpunkt, nicht einfach der aktuelle Playerframe
(`PlayerWindow.Coding.Photos.Capture.cs:18`). Er ist jedoch nicht an dieselben
PNG-Bytes gebunden. Wenn die Extraktion ausfaellt, wird der dann aktuelle globale
Puffer gelesen; bei fehlgeschlagener Ablage folgt ein aktueller Screenshot
(`CodingAnalyzedFramePhotoAttachmentWorkflow.cs:30`). Nur dieser Fehler-/Fallbackfall
kann einen spaeteren Playerframe unter der alten Ereigniszeit ablegen.

## 2. Boundary-Ende verwirft die uebergebene Zeit und loest Meter erneut auf

Die Player-Callbacks verwerfen den Zeitparameter ausdruecklich mit `_`
(`PlayerWindow.Coding.Ai.Classifier.Boundary.cs:47,49`). `CodingBoundaryContext`
nimmt stattdessen globale LastMeter und aktuelle Playerzeit
(`CodingBoundaryContext.cs:90,93`; Bindungen `PlayerWindow.xaml.cs:56,58`).
`CodingBoundaryClassifierResultWorkflow` uebergibt fuer EnsureEnd zudem EndMeter,
nicht die zuvor aus dem Analysebild aufgeloeste aktuelle Meterzahl.

Reproduzierbarer Zeitfall ohne Import: Aufnahme bei 250 s, BCE-Ergebnis nach
weitergelaufenem Player bei 254 s. Foto ist das gepufferte Analysebild von 250 s,
Ereigniszeit wird 254 s. Mit passendem Import-BCE wird stattdessen dessen Zeit
gewaehlt; die Herkunft dieser Zeit ist kein Aufnahmebeleg.

Reproduzierbarer Meterfall: Die aktuelle OSD-Lesung scheitert, der globale Cache
enthaelt noch 14,5 m. Die Hauptaufloesung verwirft einen zu alten Cache und
schaetzt z. B. 15,9 m bei 16 m Haltungsende. Damit besteht die BCE-Plausibilitaet.
EnsureEnd liest trotzdem wieder 14,5 m aus dem Cache, ohne Alterspruefung, und
`ResolvePlausibleEndMeter` behaelt den Wert, solange er nicht mehr als 1 m oberhalb
des Endes liegt. Die Zeile kann deshalb einen anderen Meter als die Entscheidung
haben. Eine gescheiterte OSD-Lesung loescht den Cache nicht
(`CodingOsdMeterReadWorkflow.cs:59`); regulare Meteraufloesung hat dagegen 1,5 s
Cachetoleranz (`Application/Ai/CodingMeterResolver`).

## 3. Boundary-Anfang mischt absichtlich Referenzzeit und sauberes Bild

`CodingBoundaryEventWorkflow.cs:73` nimmt Meter/Zeit aus Import-BCD oder 0 m/0 s.
Das Foto wird separat am FirstCleanFrameSeconds extrahiert (Zeile 80), ersatzweise
aus dem gerade analysierten Bild. Bestehender Test
`EnsureStart_adds_bcd_from_import_reference_and_prefers_clean_frame` speichert
explizit Ereigniszeit 5 s und das Bild von 6,5 s. Das ist gegenwaertige Fachregel,
kein belegter neuer Modellfehler. Fuer eine genaue Belegzuordnung sollten
Referenzzeit und tatsaechliche Fotoaufnahmezeit getrennt nachvollziehbar sein.

## 4. Streckenerstellung startet weiterhin zwei Fotoablagen

Der normale MultiModel-Command gibt jetzt Aufnahmezeit und aufgeloesten Meter
korrekt an den Tracker weiter. Beim CreateOpen startet jedoch
`CodingStreckenschadenActionApplier.cs:41` ueber die Playerbindung
`PlayerWindow.xaml.cs:179` noch den alten asynchronen Fotoanruf. Anschliessend
ergaenzt der Finding-Workflow den Erstbeleg derselben neu erzeugten Trackerzeile
mit der synchronen Ablage der genauen Analysebytes.

Im Dateispeicher gewinnt die erste vorhandene Datei
(`Infrastructure/Ai/CodingFramePhotoFileStore.cs:25`). Wenn die alte Extraktion
zuerst fertig wird, behaelt der exakte zweite Anhang diese Datei. Meist ist das
eine Extraktion derselben nominalen Zeit; Bytegleichheit mit dem untersuchten
PNG ist damit nicht garantiert. Ein falscher spaeterer Frame ist nur im oben
beschriebenen Fallbackfall belegt moeglich, nicht bei jedem Streckenschaden.

Das Schliessen einer vorhandenen Strecke setzt nur MeterEnd und verwendet keinen
neuen Fotobeleg. Die globale Videozeit in CloseTracked wird fuer CloseExisting
nicht in die Ereigniszeit geschrieben. Daraus allein entsteht kein belegter
Zeitfehler. Ob ein fachlicher Endbeleg gewuenscht ist, waere eine separate Frage.

## Begrenzung und naechster Schritt

Der gerade behobene Same-Tick-Menschenschutz neuer Trackerzeilen ist unabhaengig
von diesen offenen Zeit-/Fotobefunden. Die Punkt-Folgebelegregeln werden durch
diesen Audit nicht geaendert. Fuer weitere Korrekturen zuerst kleine Verhaltenstests
mit versetzter Hostzeit, veraltetem OSD-Cache und verzogerter/falscher Fotoextraktion
schreiben. Danach den bildgebundenen Kontext durch die jeweils betroffene
Sonderroute reichen; Importreferenzen und Fotoaufnahmezeit getrennt behandeln.

## Anschliessend umgesetzte Aufnahmebindung

Nach der abgeschlossenen, unveraenderten Videovergleichsserie wurde dieser
separate Fix freigegeben und umgesetzt:

- `CodingMultiModelInferenceWorkflow.ExecuteAnalyzedFrameAsync` loest den Meter
  einmal vor der Inferenz auf. `CodingAnalyzedFrameEvidence` haelt diese Aufloesung,
  Aufnahmezeit und PNG-Bytes fuer Boundary, Structural und normale Findings fest.
  Es wird danach weder globale Playerzeit noch ein spaeterer OSD-Wert eingelesen.
- `CodingMeterResolution.Source` unterscheidet SameFrameOsd, RecentOsd,
  VideoEstimate und SessionFallback. Ein hoechstens 1,5 s alter OSD-Cache ist
  weiter OSD und wird nicht als Zeit-Schaetzung markiert. Fuer Folgebelegersetzung
  gilt separat `SameFrameMeterEvidence`: ein Cache allein berechtigt nicht dazu.
- Structural verwendet den gebundenen Meter und die Aufnahmezeit statt einer
  alten Hostzeit. Seine Fotoablage ist synchron und benutzt die Analysebytes.
- Der bildgebundene BoundaryContext-Einstieg liest weder LastMeter noch aktuelle
  Playerzeit. BCE verwendet den bereits aufgeloesten Meter und die Aufnahmezeit;
  der alte nicht bildgebundene Einstieg bleibt kompatibel.
- BCD behaelt Import-Referenz oder 0 m/0 s. Der Beleg zeigt separat
  `ai.event.position.source`, `ai.frame.time_seconds/sha256/meter/meter_source`
  und `ai.photo.time_seconds/sha256/source`. Das Foto stammt weiter bevorzugt vom
  ersten sauberen Frame; bei gescheiterter Extraktion aus den gebundenen
  Analysebytes. Eine verspaetete falsche globale Bildquelle wird nicht verwendet.
- TrackerCreateOpen bekommt fuer diesen Analyseaufruf eine exakte synchrone
  Fotoaktion. Der alte asynchrone Anhang startet dort nicht mehr. Die spaetere
  Ergaenzung derselben Zeile trifft auf genau dasselbe gespeicherte Bild.
- Menschliche EventAdded-Aenderungen bleiben auch in Boundary-/Structural-
  Appendern erhalten. Alte oeffentliche Positionsvertraege bleiben erhalten;
  neue Belegfelder sind additive Eigenschaften. Keine neue UI/Ai-Datei und keine
  Anhebung der Player-Groessenbegrenzung.

Die gezielten Verhaltenstests pruefen verzoegerte Inferenz, alte Hostzeit,
frischen/alten OSD-Cache, BCE gegen widersprechenden Cache/Import/Playerzeit,
BCD-Referenz mit spaeterem Foto oder Extraktionsausfall, exakte Trackerablage
vor EventAdded und menschliche Bearbeitung. Sie verwenden keine GPU. Die
Aufnahmezeit bleibt der Zeitpunkt des bestehenden Snapshot-Aufrufs; ein
zusaetzlicher Decoder-PTS-Nachweis wurde nicht eingefuehrt.

Abschlusspruefung dieses Standes: 163 gezielte UI-/Replay-/Architekturtests und
40 Meter-/Folgebelegtests bestanden. Der gemeinsame Dev-Build war erfolgreich
mit 0 Warnungen und 0 Fehlern; `git diff --check` ebenfalls erfolgreich.
Der Testbuild meldete die bereits bekannte Nullable-Warnung in
`VsaFotoAblageTests.cs:81`. Architektur-Skill validiert. Der Stand enthaelt auch
die Quellenbegrenzung der raeumlichen Coverage auf modellgebundene YOLO-Belege
und die getrennte SameFrameMeterEvidence im Replay. Fuer diese letzte
Aufnahmebindungs-/Quellenkorrektur liegt noch kein neuer echter Videolauf vor.
