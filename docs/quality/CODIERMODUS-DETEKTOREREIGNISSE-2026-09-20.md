# Detektorboxen erreichen die Ereignisliste

Der Codiermodus kann qualifizierte YOLO-Schadensboxen jetzt durch SAM bis zu echten
Ereignisvorschlägen weitergeben. Zuvor wirkte der Detektor nur als Relevanzfilter;
ohne DINO-Treffer gab es keine SAM-Eingabe und keine Schadenszeile.

Die Änderung betrifft „Jetzt analysieren“ und den automatischen Fünfsekundentakt:
Beide verwenden `SingleFrameMultiModelService` und dieselben Player-Workflows.
Der separate Video-Batchdienst bleibt unverändert. Es wurden keine Gewichte
ausgetauscht, Modelle gestartet oder Freigaben geändert.

## Was sich im Ablauf ändert

- `CodingLocalizedDetection` hält Pixelbox, Quelle, getrennte Modellwerte und den
  YOLO-Gewichtshash. Die bestehenden Positionsparameter bleiben erhalten;
  `SingleFrameResult.LocalizedDetections` und `SegmentedFinding.Origin` sind additiv.
- Der Health-Nachweis und die Detektorantwort müssen dasselbe gültige SHA-256
  tragen. Ohne Identität/Qualifikation darf YOLO weder Boxen liefern noch Bilder
  negativ aussortieren. Alte Sidecars ohne Artefaktnachweis fallen auf DINO/SAM
  mit Prüfhinweis zurück.
- `CodingLocalizedDetectionPlan` lässt nur bekannte Klassen und gültige Boxen
  durch. Gleiche Hauptgruppe mit Box-IoU mindestens 0,5 wird einmal segmentiert;
  YOLO- und DINO-Modellwerte bleiben getrennt. Andere Gruppen bleiben getrennte
  Befunde; ihnen wird kein künstlicher Modellkonsens zugeschrieben.
- Die eingefrorene 15er-Klassenkarte liefert Hauptcodes. SONST und generisches BBD
  erhalten keinen geratenen Untercode. Ein YOLO-Hauptcode wird exakt gegen den
  auswählbaren Katalog geprüft und nicht anhand eines importierten Sollbefunds
  zu einem spezifischeren Code umgedeutet.
- SAM muss zum selben Bild passen. Masken werden über Label und die eindeutig
  passende, an Bildgrenzen begrenzte Eingabebox zugeordnet; SAM gibt diese Box
  zurück. Auch verschachtelte Boxen übernehmen dadurch nur ihre eigene Confidence.
  Fehlende/ungültige oder mehrdeutige Herkunft erzeugt
  keine Ereigniszeile. Die bisherigen Nähe-, Segment- und Dublettenregeln bleiben.
- Die Qualitätsampel verwendet pro Befund dessen tatsächliche YOLO-/DINO-Werte
  und SAM-Stabilität. Das höchste YOLO-Signal eines anderen Befunds zählt nicht
  als zusätzliche Stimme. Ein Katalogtitel ist im neuen Herkunftsweg kein
  Sprachmodellbeleg. Unvollständige Modellnachweise begrenzen Grün auf Gelb.
- Die normale Ereignisfactory und auch neue offene Streckenschäden erhalten
  dieselben Modellbelege. Nur beim aktuellen Tick neu erzeugte Streckenzeilen
  werden ergänzt; bestehende Zeilen und vorhandene Zusatzwerte bleiben geschützt.
  `SuggestedByModelSha256` und die vorhandene CodeMeta-Parameterliste tragen die
  Gewichtsbindung. Die Zeile bleibt ein unbestätigter KI-Vorschlag.

## Prüfung und Grenzen

Für ausdrücklich beauftragte Entwicklungsmessungen gibt es jetzt einen getrennten
Einstieg `AnalyzeCandidateFrameAsync`. Sein `CodingDetectorCandidateFrame` bindet
Kandidatenkennung, vorher gewählten und tatsächlich gemessenen Gewichtshash,
Bildhash und Detektorantwort. Falsche Hashes stoppen vor Health/Modellaufrufen.
Der normale Player-Aufruf besitzt keinen Kandidatenparameter. Der Beleg kommt
ausschliesslich aus dem Messhost, nicht aus einer produktiven Freigabe oder einem
Umgebungsschalter. Technisch fehlgeschlagene Bildantworten darf der Messhost nicht
als leere Kandidatenantwort umdeuten.

`BuildCandidate` nutzt dieselben Klassen-/Box-/Maskenregeln, ohne eine qualifizierte
YOLO-Antwort zu erfinden. Der Ergebnisstatus bleibt `DetectorQualified=false` und
degraded; Quellen tragen `DevelopmentCandidate=true` und `RequiresReview=true`.
Die Maske kann einen echten Ereignisvorschlag erzeugen, aber dessen Ampel wird
höchstens gelb. Die bestehende CodeMeta-Liste nennt `development_candidate`;
Modellkennung und Hash bleiben nachvollziehbar. Leere Kandidatenboxen sortieren
kein Bild negativ aus; DINO/SAM bleibt im vorgesehenen Prüfweg.

Die vorhandenen Klassifikator-Abkürzungen für BCD/BCE/BCA/BCC bleiben separat
erhalten. Ein solcher Vorschlag stammt weiterhin aus dem Klassifikator und ist
kein Detektorkandidaten-Treffer. Messberichte müssen diese Herkunft unterscheiden.

Der neue Test `SingleFrameLocalizedDetectionTests` scheiterte zuerst mit dem
alten Verhalten. Danach erreicht seine qualifizierte YOLO-Box trotz leerem DINO
SAM. `CodingDetectorEventIntegrationTests` führt künstliche Bilder durch den
echten Einzelbilddienst, Segment-/Player-Workflows und `CodingSessionService`.
Er prüft tatsächliche Ereignisse, Herkunft, Hauptcode, fehlende Doppelstimme,
Dubletten, Strecken und den Schutz bereits vorhandener Zusatzwerte.

`CodingLocalizedDetectionPlanTests` schützt falsche/fehlende Hashes, unklare
Klassen, ungültige Koordinaten/Quellen und die Zusammenführung. Die Zähler
`RejectedYoloBoxes` und `RejectedDinoBoxes` zählen verworfene Eingaben;
erfolgreiche Zusammenführungen erhöhen sie nicht und erzeugen deshalb allein
keinen verschlechterten Ergebnisstatus. Auch die nicht vorhandene Klasse
`BAB_scherbe` wird vor einer Zusammenführung verworfen. Die vorhandenen
Einzelbild-, Segment-, Qualitäts- und Streckenprüfungen laufen zusätzlich.

Diese Verhaltenstests benutzen künstliche Daten und keine GPU. Sie beweisen
die Verdrahtung, keine neue Erkennungsquote. Für eine echte Verbesserung fehlen
weiterhin die Videoauswertung der neuen Gewichte und eine unabhängige fachliche
Abnahme. `tools/CodingReplay run-video` haelt inzwischen eine Sitzung samt Tracker
ueber die feste Videobildfolge. `run-video-candidate` prueft separat erzeugte
Boxbelege samt Bild-/Gewichtshash und nutzt den expliziten Einstieg. Lauf und
Frames bleiben `development_candidate_unqualified`; Ereignissnapshots uebernehmen
Quelle, Hash und Zweck. Technische Fehler bleiben Fehler. Details stehen in
`tools/CodingReplay/README.md`. Der feste Nachlauf misst keine Live-Timer-Ticks. Nicht
qualifizierte Kandidaten werden durch diese Änderung nicht produktiv freigegeben.

Die Quellenzuordnung beruht auf Klasse/Box und kein Objekt-Tracker wurde neu
eingeführt. Unterschiedliche Gruppen an derselben Stelle können weiterhin
mehrere Prüfhinweise ergeben. Die 15 Klassen können aus sich heraus keine
spezifischeren VSA-Untercodes oder fehlende Fachparameter belegen.

## Belegter Warmup-Fehler vor dem Videolauf

Das Laufprotokoll `FineTuning-20260920/video-runtime-01/sidecar.log` zeigte am
20.09.2026 bei DINO `selected index k out of range`. Der Warmup schickte 64x64
direkt an den bestehenden Wrapper; Swin-B erwartet 900 Encoder-Positionen fuer
`topk`, das Bild liefert ueber die vier Feature-Stufen nur 85. Nur DINO bekommt
deshalb im Warmup jetzt ein neutrales 640x640-Bild (8500 Positionen). Andere
Warmup-Pfade und echte Analysebilder bleiben unveraendert. Der gezielte Test
scheiterte vor dem Fix an dieser Bedingung; danach bestanden alle vier
`tests/test_warmup.py`-Tests ohne Modellladen oder GPU-Inferenz. Eine reale
GPU-Nachpruefung gehoert weiter zum kontrollierten Videolauf.
