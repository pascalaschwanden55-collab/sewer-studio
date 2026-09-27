# Staerkere Folgebelege fuer ungepruefte KI-Punkte

Der feste Videonachlauf zeigte: Ein schwacher BAI-Vorschlag bei 115 s / 10,13 m
blockierte einen deutlicheren Befund bei 120 s / 10,74 m allein wegen gleicher
Hauptgruppe und weniger als einem Meter Abstand. In der Kontrolle lag die erste
Box auf der Meteranzeige, bei der Spiegelvariante breit im Rohrbild. Der spaetere
Befund erreichte SAM und ging erst in der Ereignisabdeckung verloren.

## Begrenzte neue Regel

`Application/UseCases/CodingPointFollowUp/CodingPointGeometry` vergleicht gueltige
normierte Rechtecke: IoU mindestens 0,30 und Mittelpunktabstand hoechstens 0,15
im normierten x/y-Bildraum. Raeumlich getrennte KI-Punkte blockieren sich nicht
allein wegen gleicher Hauptgruppe. Fehlende Geometrie, manuelle Zeilen, Strecken
und Einmalcodes behalten die bisherigen Regeln. Keine neue Objektprojektion und
kein Flaechenbonus fuer groessere Boxen.

`CodingPointFollowUpPolicy` haelt pro Sitzung den urspruenglichen Punkt, seine
Instanz, Meterzahl, Zeit, Box und einen Fingerabdruck des gesamten Zustands.
Nur nachweislich neue, unberuehrte AI-Punkte erhalten ein Ersetzungsrecht:

- Ignored, ohne menschliche Beruehrung, ReviewContext oder Loeschung; Zustand
  stimmt exakt mit dem registrierten Eintrag ueberein. Altbestand ist gesperrt.
- Genau ein raeumlich passender Punkt derselben Gruppe. Folgebox passt sowohl
  zum unveraenderlichen Ursprung als auch zum aktuellen Beleg.
- OSD-Meterquelle, kein geschaetzter Ursprung, keine Rueckwaertsbewegung; Abstand
  zum Ursprung strikt kleiner als 1 m, Zeit maximal 15 s nach Ursprung und nicht
  vor dem aktuellen Beleg. Auch die Abdeckungszone wandert nicht mit.
- Gleicher nachgewiesener YOLO-Gewichtshash, Modellname, Zweck und Bildraum;
  YOLO-Gewinn mindestens 0,10, SAM-Verlust hoechstens 0,05. Keine rote Ampel,
  kein technischer Fehler, kein Verlust einer gruenen Ampel.
- Vollstaendiger neuer Foto-/Maskenbeleg. Nach der Fotoablage werden alle
  Bedingungen nochmals geprueft; zwischenzeitliche Bearbeitung sperrt.

EventId und EntryId bleiben erhalten. Meter, Zeit, Entry/Fotos, Overlay/Masken
und Herkunft wechseln gemeinsam. `PreviousEvidence` archiviert den vorherigen
vollstaendigen Beleg samt alter Meterzahl und Zeit in einer flachen Liste.
Alte Fotos werden nicht ueberschrieben. Der feste Ursprung wird nie neu gepinnt.

## Menschenschutz und Bildbindung

Entscheidungs-, Edit-, Foto-, Delete-, Copy- und Move-Pfade setzen
`HumanTouchedAtUtc`. Auch bewusstes Ignored und ein abgebrochener Editor sperren.
Accepted, AcceptedWithEdit, Rejected und Altbestand werden nicht ersetzt.
Der Fingerabdruck schuetzt auch unprotokollierte Aenderungen. In dieser Sitzung
registrierte und danach entfernte menschlich verworfene Punkte bleiben als
raeumlicher Sperrbeleg erhalten. Kopien behalten die alte Beleggeschichte und
den Beruehrungsmarker, erhalten aber kein neues Ersetzungsrecht.

Der echte MultiModel-Playeraufruf uebergibt genau `start.FrameBytes` an die
synchrone `AttachExactAnalyzedFramePhoto`-Ablage. Kein spaeterer PreferredFrame
und kein Screenshot dient als Ersatz. Die Ereigniszeit ist die Aufnahmezeit,
nicht die weitergelaufene Playerzeit. Ohne abgeschlossene Fotoablage gibt es
keine Registrierung/Ersetzung. Andere asynchrone Fotoaufrufe bleiben bestehen.

`CodingMultiModelFindingEventCommandWorkflow.ExecuteAnalyzedFrame` buendelt diese
Aufnahmebindung und entlastet den Player. Die aeltere Execute-Schnittstelle bleibt
kompatibel. Auch eine gerade vom Streckentracker angelegte Zeile wird nur ergaenzt,
wenn sie nach EventAdded weiterhin ungeprueft, ohne Beruehrung, Review, Loeschung
oder manuelles Overlay ist. Der Strecken-Appender erhaelt einen bereits vom
Callback gesetzten Kontext. Menschliche Edit-/Fotoaktionen koennen den Marker
auch setzen, bevor der Modellkontext angehaengt wurde. Sieben Same-Tick-Tests
pruefen diese Schutzgrenze, darunter die echten Edit-/Foto-Applier.

Die noch offenen Sonderwege fuer Boundary, Structural und Streckenfotos stehen
getrennt in `CODIERMODUS-AUFNAHMEBINDUNG-AUDIT-2026-09-20.md`.

Replay speichert die Analyse-PNGs bytegleich in `event-photos`, mit Frame-ID und
SHA-256. Ereignissnapshots zeigen aktuelle/vorherige Fotos, Meter, Zeit, Herkunft
und Beruehrung. Entwicklungskandidaten bleiben unqualifiziert und maximal gelb.
Technische Fehler sind ein eigenes Flag; fehlende Produktionsfreigabe allein
blockiert einen sauberen Kandidaten-Folgebeleg nicht.

## Nachweis und Grenzen

Der neue Ereignis-Verhaltenstest scheiterte zuerst mit der alten Unterdrueckung.
Danach bestanden 35 reine Folgebelegtests und 80 gezielte UI-/Ereignis-/Replaytests.
Sie decken reale 115-/120-s-Boxkoordinaten, getrennte nahe Schaeden, Grenzen,
festen Ursprung, mehrere Revisionen, menschliche Eingriffe, alte/ausgetauschte
Instanzen, technische Fehler, verzoegerte Fotoablage und Kandidatenstatus ab.
Alle Tests liefen ohne Modellinferenz. Die vorhandene nullable Warnung in
`VsaFotoAblageTests.cs:81` bleibt bestehen.

Bei gesperrten oder unklaren Faellen bleibt die Zeile erhalten. Der Workflow
protokolliert den Grund; Replay behaelt den Framebeleg. DINO ohne nachgewiesenen
Modellhash erhaelt kein automatisches Ersetzungsrecht.

Die Metertext-Falschbox wird weder geloescht noch durch eine starre Bildrandmaske
ausgeblendet. Sie bleibt ein eigenes Trainingsdaten-/Fehlerkennungsproblem.
Die Geometrieregel verhindert nur ihre Unterdrueckung raeumlich anderer Befunde.
Unabhaengige Codepruefung und gleicher kontrollierter Videonachlauf stehen nach
diesem Implementierungsschritt noch aus. Keine neue Erkennungsquote, keine
fachliche Abnahme und keine Modellaktivierung werden daraus abgeleitet.
